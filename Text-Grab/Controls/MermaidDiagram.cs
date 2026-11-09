using Mermaider;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using Text_Grab.Utilities;

namespace Text_Grab.Controls;

internal enum MermaidSaveFormat
{
    Text,
    Png,
    Svg,
}

internal sealed class MermaidDiagram : Grid
{
    private readonly Image image;
    private readonly TextBlock fallback;
    private CancellationTokenSource? rendering;
    private bool isLightTheme = true;
    private string? renderedSvg;

    internal string Source { get; }
    internal Task RenderCompletion { get; private set; }

    internal MermaidDiagram(string source)
    {
        Source = source;
        image = new Image
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            Visibility = Visibility.Collapsed
        };
        fallback = new TextBlock { Text = source, TextWrapping = TextWrapping.Wrap };
        Children.Add(image);
        Children.Add(fallback);
        AutomationProperties.SetName(this, "Mermaid diagram");
        AutomationProperties.SetHelpText(this, source);
        Background = Brushes.Transparent;
        Cursor = Cursors.Hand;
        ContextMenu = CreateContextMenu();
        // The hosting RichTextBox starts a selection on mouse down and captures the mouse, so the click is
        // claimed in the tunnelling phase before the text box sees it.
        PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!HasRenderedImage)
                return;

            e.Handled = true;
            OpenViewer();
        };
        RenderCompletion = RenderAsync();
        Unloaded += (_, _) => rendering?.Cancel();
        Loaded += (_, _) =>
        {
            if (rendering?.IsCancellationRequested == true)
                RenderCompletion = RenderAsync();
        };
    }

    internal void ApplyTheme(bool lightTheme)
    {
        if (isLightTheme == lightTheme)
            return;

        isLightTheme = lightTheme;
        RenderCompletion = RenderAsync();
    }

    internal BitmapSource? RenderedBitmap => image.Source as BitmapSource;

    internal bool HasRenderedImage => renderedSvg is not null && RenderedBitmap is not null;

    internal bool IsLightTheme => isLightTheme;

    internal ContextMenu CreateContextMenu()
    {
        ContextMenu menu = new();
        menu.Items.Add(CreateMenuItem("View full screen", OpenViewer, requiresImage: true));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Save as text...", () => Save(MermaidSaveFormat.Text), requiresImage: false));
        menu.Items.Add(CreateMenuItem("Save as PNG...", () => Save(MermaidSaveFormat.Png), requiresImage: true));
        menu.Items.Add(CreateMenuItem("Save as SVG...", () => Save(MermaidSaveFormat.Svg), requiresImage: true));
        return menu;
    }

    private MenuItem CreateMenuItem(string header, Action action, bool requiresImage)
    {
        MenuItem item = new() { Header = header };
        item.Click += (_, _) => action();
        if (requiresImage)
            item.SetBinding(UIElement.IsEnabledProperty, new System.Windows.Data.Binding { Source = this, Path = new PropertyPath(nameof(IsRendered)) });
        return item;
    }

    public static readonly DependencyProperty IsRenderedProperty =
        DependencyProperty.Register(nameof(IsRendered), typeof(bool), typeof(MermaidDiagram), new PropertyMetadata(false));

    public bool IsRendered
    {
        get => (bool)GetValue(IsRenderedProperty);
        private set => SetValue(IsRenderedProperty, value);
    }

    internal void OpenViewer()
    {
        if (!HasRenderedImage)
            return;

        MermaidViewerWindow viewer = new(this) { Owner = Window.GetWindow(this) };
        viewer.ShowDialog();
    }

    internal void Save(MermaidSaveFormat format)
    {
        (string filter, string extension) = format switch
        {
            MermaidSaveFormat.Text => ("Mermaid diagram | *.mmd|Text | *.txt", ".mmd"),
            MermaidSaveFormat.Png => ("PNG image | *.png", ".png"),
            _ => ("SVG image | *.svg", ".svg"),
        };

        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            FileName = "diagram" + extension,
            Filter = filter,
            DefaultExt = extension,
            AddExtension = true,
            RestoreDirectory = true,
        };
        if (dialog.ShowDialog() is not true)
            return;

        try
        {
            switch (format)
            {
                case MermaidSaveFormat.Text:
                    File.WriteAllText(dialog.FileName, Source);
                    break;
                case MermaidSaveFormat.Svg when renderedSvg is not null:
                    File.WriteAllText(dialog.FileName, renderedSvg, new System.Text.UTF8Encoding(false));
                    break;
                case MermaidSaveFormat.Png when RenderedBitmap is BitmapSource bitmap:
                    PngBitmapEncoder encoder = new();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (FileStream stream = File.Create(dialog.FileName))
                        encoder.Save(stream);
                    break;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            System.Windows.MessageBox.Show($"The diagram could not be saved: {exception.Message}", "Save failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task RenderAsync()
    {
        rendering?.Cancel();
        rendering?.Dispose();
        CancellationTokenSource cancellation = new();
        rendering = cancellation;
        try
        {
            (BitmapSource bitmap, string svg) = await MermaidImageRenderer.RenderWithSvgAsync(Source, isLightTheme, cancellation.Token);
            if (cancellation.IsCancellationRequested)
                return;

            renderedSvg = svg;
            image.Source = bitmap;
            image.Visibility = Visibility.Visible;
            fallback.Visibility = Visibility.Collapsed;
            IsRendered = true;
            ToolTip = "Mermaid diagram. Click to view full screen, right-click to save. Switch to plain text mode to edit the diagram source.";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is MermaidParseException or MermaidSvgException or IOException
            or ArgumentException or InvalidOperationException or FormatException or NotSupportedException
            or OverflowException or RegexMatchTimeoutException or XmlException or OperationCanceledException)
        {
            // Like asynchronous Markdown image loading, failures must stay visible without changing the text tree.
            if (cancellation.IsCancellationRequested)
                return;

            Debug.WriteLine($"Mermaid diagram could not be rendered: {exception.Message}");
            image.Visibility = Visibility.Collapsed;
            renderedSvg = null;
            IsRendered = false;
            fallback.Text = $"Mermaid diagram could not be rendered: {exception.Message}{Environment.NewLine}{Environment.NewLine}{Source}";
            fallback.Visibility = Visibility.Visible;
            ToolTip = exception.Message;
        }
    }
}
