using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TextGrab.AutomationHost;

public partial class MainWindow : Window
{
    private const string DefaultKnownText = "The quick brown fox jumps over the lazy dog.\nText Grab fixture line two: 0123456789.";
    private const string DefaultMultilingualText = "English: deterministic text\nالعربية: نص ثابت للاختبار\nעברית: טקסט בדיקה קבוע\n日本語: 固定テスト文字列\n中文: 固定测试文本\n한국어: 고정 테스트 텍스트";
    private readonly FixtureOptions options;
    private readonly FixtureStateWriter stateWriter;
    private TextBlock? coordinateDpiReadout;
    private string selectedSurface = "KnownText";
    private string displayedText = DefaultKnownText;

    public MainWindow(FixtureOptions options)
    {
        this.options = options;
        stateWriter = new FixtureStateWriter(options.StateFile);
        InitializeComponent();
        displayedText = string.IsNullOrWhiteSpace(options.DisplayText) ? DefaultKnownText : options.DisplayText;
        SurfaceTextInput.Text = displayedText;
        SelectSurface(options.Surface);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ShowSurface();
        UpdateWindowState("ready");
        InputTarget.Focus();
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        UpdateWindowState("activated");
    }

    private void Window_Changed(object sender, EventArgs e)
    {
        UpdateWindowState("window-changed");
    }

    private void SurfaceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SurfaceSelector.SelectedValue is string surface)
        {
            selectedSurface = surface;
            if (IsLoaded)
            {
                ShowSurface();
                UpdateWindowState("surface-changed");
            }
        }
    }

    private void ShowSelectedSurface_Click(object sender, RoutedEventArgs e)
    {
        ShowSurface();
        UpdateWindowState("surface-shown");
    }

    private void UpdateContent_Click(object sender, RoutedEventArgs e)
    {
        displayedText = SurfaceTextInput.Text;
        ShowSurface();
        UpdateWindowState("display-text-updated");
    }

    private void ResetWindowBounds_Click(object sender, RoutedEventArgs e)
    {
        Left = 100;
        Top = 100;
        Width = 1000;
        Height = 780;
        UpdateWindowState("bounds-reset");
    }

    private void InputTarget_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ReceivedText is null)
        {
            return;
        }

        ReceivedText.Text = InputTarget.Text;
        if (IsLoaded)
        {
            UpdateWindowState("input-changed");
        }
    }

    private void ClearInput_Click(object sender, RoutedEventArgs e)
    {
        InputTarget.Clear();
        InputTarget.Focus();
        UpdateWindowState("input-cleared");
    }

    private void SelectSurface(string requestedSurface)
    {
        ComboBoxItem? match = SurfaceSelector.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), requestedSurface, StringComparison.OrdinalIgnoreCase));

        SurfaceSelector.SelectedItem = match ?? SurfaceSelector.Items.OfType<ComboBoxItem>().First();
    }

    private void ShowSurface()
    {
        SurfaceContent.Content = selectedSurface switch
        {
            "DirectText" => CreateDirectTextSurface(),
            "Multilingual" => CreateMultilingualSurface(),
            "OcrSamples" => CreateOcrSamplesSurface(),
            "QrBarcode" => CreateQrBarcodeSurface(),
            "Table" => CreateTableSurface(),
            "Empty" => CreateEmptySurface(),
            "Contrast" => CreateContrastSurface(),
            "CoordinateDpi" => CreateCoordinateDpiSurface(),
            _ => CreateKnownTextSurface()
        };
    }

    private FrameworkElement CreateKnownTextSurface()
    {
        StackPanel panel = CreateSurfacePanel("Known English and multiline text", "KnownTextSurface");
        panel.Children.Add(CreateTextBlock(displayedText, "KnownTextDisplay", 18));
        panel.Children.Add(CreateTextBox(DefaultKnownText, "KnownTextValue", true));
        return panel;
    }

    private FrameworkElement CreateDirectTextSurface()
    {
        StackPanel panel = CreateSurfacePanel("Native UI Automation text controls", "DirectTextSurface");
        panel.Children.Add(CreateTextBox(displayedText, "DirectTextNativeValue", true));

        RichTextBox richText = new()
        {
            IsReadOnly = true,
            Height = 100,
            Margin = new Thickness(0, 8, 0, 0),
            Document = new FlowDocument(new Paragraph(new Run(DefaultKnownText)))
        };
        AutomationProperties.SetAutomationId(richText, "DirectTextRichText");
        AutomationProperties.SetName(richText, "Native rich text UI Automation source");
        panel.Children.Add(richText);

        TextBox editableText = CreateTextBox("Editable direct text control", "DirectTextEditable", false);
        editableText.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(editableText);
        return panel;
    }

    private FrameworkElement CreateMultilingualSurface()
    {
        StackPanel panel = CreateSurfacePanel("Multilingual, RTL, and CJK text", "MultilingualSurface");
        panel.Children.Add(CreateTextBlock(DefaultMultilingualText, "MultilingualTextDisplay", 17));

        TextBlock rtlText = CreateTextBlock("RTL sample: مرحبًا بالعالم — שלום עולם", "RightToLeftText", 17);
        rtlText.FlowDirection = FlowDirection.RightToLeft;
        rtlText.HorizontalAlignment = HorizontalAlignment.Stretch;
        panel.Children.Add(rtlText);
        panel.Children.Add(CreateTextBox(DefaultMultilingualText, "MultilingualTextValue", true));
        return panel;
    }

    private FrameworkElement CreateOcrSamplesSurface()
    {
        StackPanel panel = CreateSurfacePanel("OCR sample images from Tests\\Images", "OcrSamplesSurface");
        ComboBox sampleSelector = new()
        {
            Margin = new Thickness(0, 0, 0, 8),
            ItemsSource = OcrSamples,
            DisplayMemberPath = nameof(OcrSample.Name),
            SelectedIndex = 0
        };
        AutomationProperties.SetAutomationId(sampleSelector, "OcrSampleSelector");
        AutomationProperties.SetName(sampleSelector, "OCR sample image selector");

        Image image = new()
        {
            Height = 370,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AutomationProperties.SetAutomationId(image, "OcrSampleImage");
        AutomationProperties.SetName(image, "Selected OCR sample image");

        TextBlock description = CreateTextBlock(string.Empty, "OcrSampleDescription", 15);
        sampleSelector.SelectionChanged += (_, _) =>
        {
            if (sampleSelector.SelectedItem is OcrSample sample)
            {
                image.Source = LoadFixtureImage(sample.FileName);
                description.Text = $"Expected content: {sample.ExpectedText}";
                UpdateWindowState("ocr-sample-changed");
            }
        };
        image.Source = LoadFixtureImage(OcrSamples[0].FileName);
        description.Text = $"Expected content: {OcrSamples[0].ExpectedText}";

        panel.Children.Add(sampleSelector);
        panel.Children.Add(description);
        panel.Children.Add(image);
        return panel;
    }

    private FrameworkElement CreateQrBarcodeSurface()
    {
        StackPanel panel = CreateSurfacePanel("QR code and deterministic barcode", "QrBarcodeSurface");
        StackPanel content = new() { Orientation = Orientation.Horizontal };

        Image qrCode = new()
        {
            Source = LoadFixtureImage("QrCodeTestImage.png"),
            Width = 230,
            Height = 230,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 24, 0)
        };
        AutomationProperties.SetAutomationId(qrCode, "QrCodeImage");
        AutomationProperties.SetName(qrCode, "QR code sample; expected text is Text Grab QR fixture");
        content.Children.Add(qrCode);

        content.Children.Add(CreateBarcode());
        panel.Children.Add(content);
        panel.Children.Add(CreateTextBlock("QR expected text: Text Grab QR fixture. Barcode value: TEXT-GRAB-123 (Code 39).", "QrBarcodeExpectedText", 16));
        return panel;
    }

    private FrameworkElement CreateTableSurface()
    {
        StackPanel panel = CreateSurfacePanel("Table-like content", "TableSurface");
        Grid table = new() { ShowGridLines = true, Margin = new Thickness(0, 8, 0, 0) };
        AutomationProperties.SetAutomationId(table, "TableLikeGrid");
        AutomationProperties.SetName(table, "Deterministic table-like content");
        for (int index = 0; index < 3; index++)
        {
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        string[,] cells =
        {
            { "Item", "Quantity", "Price" },
            { "Apples", "4", "$3.20" },
            { "Oranges", "6", "$5.10" }
        };

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                TextBlock cell = new()
                {
                    Text = cells[row, column],
                    Padding = new Thickness(8),
                    FontWeight = row == 0 ? FontWeights.SemiBold : FontWeights.Normal
                };
                AutomationProperties.SetAutomationId(cell, $"TableCell{row}{column}");
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                table.Children.Add(cell);
            }
        }

        panel.Children.Add(table);
        panel.Children.Add(CreateTextBlock("Image fixture: Table-Test.png", "TableImageFixtureDescription", 15));
        Image tableImage = new()
        {
            Source = LoadFixtureImage("Table-Test.png"),
            Height = 220,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AutomationProperties.SetAutomationId(tableImage, "TableSampleImage");
        panel.Children.Add(tableImage);
        return panel;
    }

    private FrameworkElement CreateEmptySurface()
    {
        StackPanel panel = CreateSurfacePanel("Empty / no-text capture region", "EmptySurface");
        Border emptyRegion = new()
        {
            Height = 300,
            Background = Brushes.White,
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1)
        };
        AutomationProperties.SetAutomationId(emptyRegion, "EmptyNoTextRegion");
        AutomationProperties.SetName(emptyRegion, "Intentionally empty no-text region");
        panel.Children.Add(emptyRegion);
        panel.Children.Add(CreateTextBlock("The bordered region above intentionally contains no text.", "EmptySurfaceDescription", 15));
        return panel;
    }

    private FrameworkElement CreateContrastSurface()
    {
        StackPanel panel = CreateSurfacePanel("High-contrast and color samples", "ContrastSurface");
        TextBlock status = CreateTextBlock($"System high contrast enabled: {SystemParameters.HighContrast}", "HighContrastStatus", 16);
        panel.Children.Add(status);

        UniformGrid colors = new() { Columns = 2, Margin = new Thickness(0, 8, 0, 0) };
        AutomationProperties.SetAutomationId(colors, "HighContrastColorSamples");
        AddColorSample(colors, "Black on white", Brushes.Black, Brushes.White, "ColorSampleBlackOnWhite");
        AddColorSample(colors, "White on black", Brushes.White, Brushes.Black, "ColorSampleWhiteOnBlack");
        AddColorSample(colors, "Yellow on black", Brushes.Yellow, Brushes.Black, "ColorSampleYellowOnBlack");
        AddColorSample(colors, "Cyan on navy", Brushes.Cyan, Brushes.Navy, "ColorSampleCyanOnNavy");
        panel.Children.Add(colors);
        return panel;
    }

    private FrameworkElement CreateCoordinateDpiSurface()
    {
        StackPanel panel = CreateSurfacePanel("Coordinate and DPI grid", "CoordinateDpiSurface");
        coordinateDpiReadout = CreateTextBlock(WindowMetricsText.Text, "CoordinateDpiReadout", 16);
        panel.Children.Add(coordinateDpiReadout);

        Canvas grid = new()
        {
            Width = 700,
            Height = 350,
            Background = Brushes.WhiteSmoke,
            Margin = new Thickness(0, 8, 0, 0)
        };
        AutomationProperties.SetAutomationId(grid, "CoordinateDpiGrid");
        AutomationProperties.SetName(grid, "Coordinate grid with 50 device-independent-pixel intervals");
        for (int x = 0; x <= 700; x += 50)
        {
            grid.Children.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = 350, Stroke = Brushes.LightSlateGray, StrokeThickness = 1 });
            TextBlock label = new() { Text = x.ToString(), FontSize = 11 };
            Canvas.SetLeft(label, x + 2);
            Canvas.SetTop(label, 2);
            grid.Children.Add(label);
        }

        for (int y = 0; y <= 350; y += 50)
        {
            grid.Children.Add(new Line { X1 = 0, X2 = 700, Y1 = y, Y2 = y, Stroke = Brushes.LightSlateGray, StrokeThickness = 1 });
            TextBlock label = new() { Text = y.ToString(), FontSize = 11 };
            Canvas.SetLeft(label, 2);
            Canvas.SetTop(label, y + 2);
            grid.Children.Add(label);
        }

        panel.Children.Add(grid);
        return panel;
    }

    private StackPanel CreateSurfacePanel(string title, string automationId)
    {
        StackPanel panel = new();
        AutomationProperties.SetAutomationId(panel, automationId);
        AutomationProperties.SetName(panel, title);
        panel.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(CreateTextBlock(displayedText, "SurfaceOverrideText", 15));
        return panel;
    }

    private static TextBlock CreateTextBlock(string text, string automationId, double fontSize)
    {
        TextBlock block = new()
        {
            Text = text,
            FontSize = fontSize,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        AutomationProperties.SetAutomationId(block, automationId);
        return block;
    }

    private static TextBox CreateTextBox(string text, string automationId, bool isReadOnly)
    {
        TextBox box = new()
        {
            Text = text,
            IsReadOnly = isReadOnly,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 48,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        AutomationProperties.SetAutomationId(box, automationId);
        return box;
    }

    private static void AddColorSample(Panel parent, string text, Brush foreground, Brush background, string automationId)
    {
        Border sample = new()
        {
            Background = background,
            Margin = new Thickness(4),
            Padding = new Thickness(14),
            Child = new TextBlock { Text = text, Foreground = foreground, FontSize = 18 }
        };
        AutomationProperties.SetAutomationId(sample, automationId);
        AutomationProperties.SetName(sample, text);
        parent.Children.Add(sample);
    }

    private static Canvas CreateBarcode()
    {
        const string value = "*TEXT-GRAB-123*";
        Canvas barcode = new() { Width = 440, Height = 230, Background = Brushes.White };
        AutomationProperties.SetAutomationId(barcode, "BarcodeSample");
        AutomationProperties.SetName(barcode, "Deterministic Code 39 barcode sample with value TEXT-GRAB-123");
        double x = 14;
        foreach (char character in value)
        {
            int encoding = Code39Encodings[character];
            for (int element = 0; element < 9; element++)
            {
                double width = (encoding & (1 << (8 - element))) == 0 ? 2 : 4;
                if (element % 2 == 0)
                {
                    Rectangle bar = new() { Width = width, Height = 180, Fill = Brushes.Black };
                    Canvas.SetLeft(bar, x);
                    Canvas.SetTop(bar, 12);
                    barcode.Children.Add(bar);
                }

                x += width;
            }

            x += 2;
        }

        TextBlock label = new() { Text = "TEXT-GRAB-123", FontSize = 18 };
        Canvas.SetLeft(label, 120);
        Canvas.SetTop(label, 195);
        barcode.Children.Add(label);
        return barcode;
    }

    private static BitmapImage LoadFixtureImage(string fileName)
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Images", fileName);
        return new BitmapImage(new Uri(path, UriKind.Absolute));
    }

    private void UpdateWindowState(string eventName)
    {
        if (!IsLoaded)
        {
            return;
        }

        (string bounds, string monitor, uint dpi) = GetWindowMetrics();
        string state = $"Bounds: {bounds} | Monitor: {monitor} | DPI: {dpi}";
        WindowMetricsText.Text = state;
        if (coordinateDpiReadout is not null)
        {
            coordinateDpiReadout.Text = state;
        }

        stateWriter.Write(new FixtureState(
            DateTimeOffset.UtcNow,
            eventName,
            selectedSurface,
            displayedText,
            InputTarget.Text,
            bounds,
            monitor,
            dpi));
    }

    private (string Bounds, string Monitor, uint Dpi) GetWindowMetrics()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        Point origin = PointToScreen(new Point(0, 0));
        string bounds = $"{origin.X:F0},{origin.Y:F0},{ActualWidth:F0}x{ActualHeight:F0}";
        uint dpi = GetDpiForWindow(handle);
        if (dpi == 0)
        {
            dpi = (uint)Math.Round(VisualTreeHelper.GetDpi(this).PixelsPerInchX);
        }

        IntPtr monitorHandle = MonitorFromWindow(handle, 2);
        MonitorInfoEx monitorInfo = new() { Size = Marshal.SizeOf<MonitorInfoEx>() };
        if (monitorHandle != IntPtr.Zero && GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            string monitor = $"{monitorInfo.DeviceName} ({monitorInfo.MonitorLeft},{monitorInfo.MonitorTop},{monitorInfo.MonitorRight - monitorInfo.MonitorLeft}x{monitorInfo.MonitorBottom - monitorInfo.MonitorTop})";
            return (bounds, monitor, dpi);
        }

        return (bounds, "unavailable", dpi);
    }

    private static readonly OcrSample[] OcrSamples =
    [
        new("English font sample", "font_sample.png", "The quick brown fox text sample."),
        new("English paragraph sample", "paragraph-test-image.png", "Multi-line English paragraph sample."),
        new("Japanese language sample", "Ja-Lang-Image.png", "Japanese OCR language sample.")
    ];

    private static readonly IReadOnlyDictionary<char, int> Code39Encodings = new Dictionary<char, int>
    {
        ['0'] = 0x034, ['1'] = 0x121, ['2'] = 0x061, ['3'] = 0x160, ['4'] = 0x031,
        ['5'] = 0x130, ['6'] = 0x070, ['7'] = 0x025, ['8'] = 0x124, ['9'] = 0x064,
        ['A'] = 0x109, ['B'] = 0x049, ['C'] = 0x148, ['D'] = 0x019, ['E'] = 0x118,
        ['F'] = 0x058, ['G'] = 0x00D, ['H'] = 0x10C, ['I'] = 0x04C, ['J'] = 0x01C,
        ['K'] = 0x103, ['L'] = 0x043, ['M'] = 0x142, ['N'] = 0x013, ['O'] = 0x112,
        ['P'] = 0x052, ['Q'] = 0x007, ['R'] = 0x106, ['S'] = 0x046, ['T'] = 0x016,
        ['U'] = 0x181, ['V'] = 0x0C1, ['W'] = 0x1C0, ['X'] = 0x091, ['Y'] = 0x190,
        ['Z'] = 0x0D0, ['-'] = 0x085, ['.'] = 0x184, [' '] = 0x0C4, ['$'] = 0x094,
        ['/'] = 0x0A8, ['+'] = 0x0A2, ['%'] = 0x08A, ['*'] = 0x02A
    };

    private sealed record OcrSample(string Name, string FileName, string ExpectedText);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public int MonitorLeft;
        public int MonitorTop;
        public int MonitorRight;
        public int MonitorBottom;
        public int WorkLeft;
        public int WorkTop;
        public int WorkRight;
        public int WorkBottom;
        public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx monitorInfo);
}
