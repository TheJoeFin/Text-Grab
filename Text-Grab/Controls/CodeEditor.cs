using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Indentation;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Core.Editing;

namespace Text_Grab.Controls;

/// <summary>
/// The Edit Text Window's code editor: AvalonEdit's <see cref="TextEditor"/>, themed to match the app.
/// </summary>
/// <remarks>
/// <para>
/// AvalonEdit's built-in highlighting colors are made for a white background. On the dark theme each
/// color is remapped through <see cref="CodeHighlightColors"/>. The theme is read from
/// <see cref="Control.Foreground"/>, which the window binds to the theme's primary text brush, so a
/// theme change re-colors the editor without anyone telling it.
/// </para>
/// <para>
/// AvalonEdit binds Ctrl+D (delete line) and Ctrl+I (indent). The Edit Text Window uses both
/// (duplicate line, isolate selection), so those two bindings are removed and the keys reach the window.
/// </para>
/// <para>
/// Languages with a <see cref="CodeFolding"/> rule get collapse/expand markers in the gutter. The
/// regions are recomputed shortly after the text stops changing; AvalonEdit anchors existing regions to
/// the text, so collapsed regions stay collapsed through edits around them.
/// </para>
/// </remarks>
public class CodeEditor : TextEditor
{
    /// <summary>How long the text must be still before the folding regions are recomputed.</summary>
    public static readonly TimeSpan FoldingUpdateDelay = TimeSpan.FromMilliseconds(400);

    private static readonly RoutedCommand[] CommandsTheWindowOwns = [AvalonEditCommands.DeleteLine, AvalonEditCommands.IndentSelection];

    /// <summary>Languages whose blocks are braces, so Enter after "{" indents and "}" outdents.</summary>
    private static readonly HashSet<string> BraceLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        CodeLanguages.CSharp, CodeLanguages.Cpp, CodeLanguages.Css, CodeLanguages.Java,
        CodeLanguages.JavaScript, CodeLanguages.Json, CodeLanguages.Php,
    };

    private readonly DispatcherTimer foldingTimer;
    private readonly SearchPanel searchPanel;
    private readonly SelectionOccurrencesRenderer occurrencesRenderer;
    private readonly BracketHighlightRenderer bracketRenderer;
    private FoldingManager? foldingManager;
    private int textRevision;
    private bool searchTemplateApplied;

    /// <summary>Documents longer than this are scanned off the UI thread.</summary>
    private const int SynchronousFoldingLimit = 50_000;

    public CodeEditor()
    {
        ShowLineNumbers = true;
        Options.HighlightCurrentLine = true;
        Options.EnableHyperlinks = false;
        Options.EnableEmailHyperlinks = false;
        Options.IndentationSize = 4;
        Options.EnableRectangularSelection = true; // Alt+drag selects a column

        // Ctrl+F opens an inline find bar; F3 / Shift+F3 step through matches while it is open.
        searchPanel = SearchPanel.Install(TextArea);

        occurrencesRenderer = new SelectionOccurrencesRenderer(this);
        bracketRenderer = new BracketHighlightRenderer(this);
        TextArea.TextView.BackgroundRenderers.Add(occurrencesRenderer);
        TextArea.TextView.BackgroundRenderers.Add(bracketRenderer);
        TextArea.SelectionChanged += (_, _) => TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        TextArea.Caret.PositionChanged += (_, _) => TextArea.TextView.InvalidateLayer(KnownLayer.Selection);

        RemoveCommandBindingsTheWindowOwns();
        SpaceOutTheGutter();
        Loaded += (_, _) => ApplyTheme();

        foldingTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = FoldingUpdateDelay };
        foldingTimer.Tick += (_, _) =>
        {
            foldingTimer.Stop();
            _ = UpdateFoldingsInBackgroundAsync();
        };
        TextChanged += (_, _) =>
        {
            textRevision++;
            if (foldingManager is null)
                return;

            foldingTimer.Stop();
            foldingTimer.Start();
        };
    }

    /// <summary>True while the editor is showing the dark theme's colors.</summary>
    public bool IsDarkTheme { get; private set; }

    /// <summary>The folding regions, or null when the current language has no folding rule.</summary>
    public FoldingManager? Foldings => foldingManager;

    /// <summary>Shows spaces, tabs and line endings as faint marks.</summary>
    public bool ShowWhitespace
    {
        get => Options.ShowSpaces;
        set
        {
            Options.ShowSpaces = value;
            Options.ShowTabs = value;
            Options.ShowEndOfLine = value;
        }
    }

    /// <summary>The find bar, so tests and the window can open or close it.</summary>
    public SearchPanel Search => searchPanel;

    protected override IVisualLineTransformer CreateColorizer(IHighlightingDefinition highlightingDefinition)
        => new ThemeAwareHighlightingColorizer(highlightingDefinition, this);

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == ForegroundProperty)
            ApplyTheme();
        else if (e.Property == SyntaxHighlightingProperty)
        {
            ConfigureFolding();
            ConfigureIndentation();
        }
    }

    /// <summary>
    /// Brace languages indent after "{" and outdent on "}"; every other language keeps the previous
    /// line's indentation, which is what AvalonEdit's default strategy does.
    /// </summary>
    private void ConfigureIndentation()
    {
        string? name = SyntaxHighlighting?.Name;
        TextArea.IndentationStrategy = name is not null && BraceLanguages.Contains(name)
            ? new CSharpIndentationStrategy(Options)
            : new DefaultIndentationStrategy();
    }

    /// <summary>Recomputes the folding regions from the text now, keeping collapsed ones collapsed.</summary>
    public void UpdateFoldings()
    {
        if (foldingManager is null)
            return;

        IEnumerable<NewFolding> foldings = CodeFolding.Find(Document.Text, SyntaxHighlighting?.Name)
            .Select(region => new NewFolding(region.Start, region.End));
        foldingManager.UpdateFoldings(foldings, -1);
    }

    /// <summary>
    /// Scans for folding regions on a worker thread so a large document can't freeze the UI, then
    /// applies them if the text hasn't changed in the meantime.
    /// </summary>
    private async Task UpdateFoldingsInBackgroundAsync()
    {
        if (foldingManager is null)
            return;

        string text = Document.Text;
        string? language = SyntaxHighlighting?.Name;
        int revision = textRevision;
        FoldingManager? manager = foldingManager;

        IReadOnlyList<TextSpan> regions = await Task.Run(() => CodeFolding.Find(text, language));

        if (revision != textRevision || manager != foldingManager)
            return;

        manager.UpdateFoldings(regions.Select(region => new NewFolding(region.Start, region.End)), -1);
    }

    /// <summary>Expands every collapsed region that hides part of the range from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public void ExpandFoldingsAround(int start, int end)
    {
        if (foldingManager is null)
            return;

        foreach (FoldingSection folding in foldingManager.AllFoldings)
        {
            if (folding.IsFolded && folding.StartOffset < Math.Max(end, start + 1) && folding.EndOffset > start)
                folding.IsFolded = false;
        }
    }

    /// <summary>Adds the folding markers for a language with a folding rule, and removes them otherwise.</summary>
    private void ConfigureFolding()
    {
        bool supported = CodeFolding.IsSupported(SyntaxHighlighting?.Name);

        if (supported && foldingManager is null)
        {
            foldingManager = FoldingManager.Install(TextArea);
            ApplyFoldingMarginTheme();
        }
        else if (!supported && foldingManager is not null)
        {
            foldingTimer.Stop();
            FoldingManager.Uninstall(foldingManager);
            foldingManager = null;
        }

        if (Document.TextLength > SynchronousFoldingLimit)
            _ = UpdateFoldingsInBackgroundAsync();
        else
            UpdateFoldings();
    }

    private void ApplyFoldingMarginTheme()
    {
        Color textColor = Foreground is SolidColorBrush brush ? brush.Color : Colors.Black;
        Brush markerBrush = LineNumbersForeground ?? Brushes.Gray;
        Brush hoverBackground = Freeze(new SolidColorBrush(Color.FromArgb(0x26, textColor.R, textColor.G, textColor.B)));

        foreach (FoldingMargin margin in TextArea.LeftMargins.OfType<FoldingMargin>())
        {
            margin.SetValue(FoldingMargin.FoldingMarkerBrushProperty, markerBrush);
            margin.SetValue(FoldingMargin.FoldingMarkerBackgroundBrushProperty, Brushes.Transparent);
            margin.SetValue(FoldingMargin.SelectedFoldingMarkerBrushProperty, Foreground);
            margin.SetValue(FoldingMargin.SelectedFoldingMarkerBackgroundBrushProperty, hoverBackground);
        }
    }

    /// <summary>Re-reads the theme from the foreground brush and re-colors the editor if it changed.</summary>
    public void ApplyTheme()
    {
        Color textColor = Foreground is SolidColorBrush brush ? brush.Color : Colors.Black;
        bool isDark = CodeHighlightColors.IsDarkThemeText(ToDrawingColor(textColor));

        Color accent = TryFindResource("SystemAccentColorPrimary") is Color accentColor ? accentColor : Color.FromRgb(0x00, 0x78, 0xD4);
        TextArea.SelectionBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x66, accent.R, accent.G, accent.B)));
        TextArea.SelectionBorder = null;
        TextArea.SelectionForeground = null;
        TextArea.SelectionCornerRadius = 0;
        TextArea.Caret.CaretBrush = Freeze(new SolidColorBrush(textColor));
        TextArea.TextView.CurrentLineBackground = Freeze(new SolidColorBrush(Color.FromArgb(isDark ? (byte)0x14 : (byte)0x0C, textColor.R, textColor.G, textColor.B)));
        // Not null: the property already is null, so null never reaches the renderer, which keeps
        // AvalonEdit's default translucent green border.
        TextArea.TextView.CurrentLineBorder = Freeze(new Pen(Brushes.Transparent, 0));
        TextArea.TextView.NonPrintableCharacterBrush = LineNumbersForeground ?? Brushes.Gray;
        occurrencesRenderer.Fill = Freeze(new SolidColorBrush(Color.FromArgb(isDark ? (byte)0x40 : (byte)0x30, accent.R, accent.G, accent.B)));
        bracketRenderer.Fill = Freeze(new SolidColorBrush(Color.FromArgb(0x30, textColor.R, textColor.G, textColor.B)));
        bracketRenderer.Outline = Freeze(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0x99, accent.R, accent.G, accent.B))), 1));
        searchPanel.MarkerBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x80, accent.R, accent.G, accent.B)));
        TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        ApplyFoldingMarginTheme();
        ApplySearchPanelTheme(textColor, accent, isDark);

        if (isDark == IsDarkTheme)
            return;

        IsDarkTheme = isDark;
        foreach (ThemeAwareHighlightingColorizer colorizer in TextArea.TextView.LineTransformers.OfType<ThemeAwareHighlightingColorizer>())
            colorizer.ClearCache();

        TextArea.TextView.Redraw();
    }

    /// <summary>
    /// The find bar's built-in template paints with system colors (white) and the app's implicit
    /// control styles, so give it the editor's own palette: system color keys for the template, and
    /// explicit brushes for the pieces it exposes.
    /// </summary>
    private void ApplySearchPanelTheme(Color textColor, Color accent, bool isDark)
    {
        // The editor's background is usually Transparent (#00FFFFFF), which says nothing about the
        // window behind it, so only an opaque background is used as the base.
        Color editorBackground = Background is SolidColorBrush { Color.A: 255 } back ? back.Color : (isDark ? Color.FromRgb(0x20, 0x20, 0x20) : Colors.White);
        byte lift = isDark ? (byte)0x14 : (byte)0;
        Color panel = isDark
            ? Color.FromRgb((byte)Math.Min(255, editorBackground.R + lift), (byte)Math.Min(255, editorBackground.G + lift), (byte)Math.Min(255, editorBackground.B + lift))
            : Color.FromRgb(0xF3, 0xF3, 0xF3);
        Color field = isDark ? Color.FromRgb(0x2D, 0x2D, 0x2D) : Colors.White;
        Color border = Color.FromArgb(0x55, textColor.R, textColor.G, textColor.B);
        Color hover = Color.FromArgb(0x26, textColor.R, textColor.G, textColor.B);

        SolidColorBrush panelBrush = Freeze(new SolidColorBrush(panel));
        SolidColorBrush textBrush = Freeze(new SolidColorBrush(textColor));
        SolidColorBrush fieldBrush = Freeze(new SolidColorBrush(field));
        SolidColorBrush borderBrush = Freeze(new SolidColorBrush(border));
        SolidColorBrush hoverBrush = Freeze(new SolidColorBrush(hover));

        if (!searchTemplateApplied)
        {
            searchPanel.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(SearchPanelTemplateXaml);
            searchTemplateApplied = true;
        }

        searchPanel.Background = panelBrush;
        searchPanel.Foreground = textBrush;
        searchPanel.BorderBrush = borderBrush;

        ResourceDictionary resources = searchPanel.Resources;
        resources[SystemColors.ControlBrushKey] = panelBrush;
        resources[SystemColors.ControlLightBrushKey] = panelBrush;
        resources[SystemColors.ControlLightLightBrushKey] = hoverBrush;
        resources[SystemColors.ControlDarkBrushKey] = borderBrush;
        resources[SystemColors.ControlTextBrushKey] = textBrush;
        resources[SystemColors.WindowBrushKey] = fieldBrush;
        resources[SystemColors.WindowTextBrushKey] = textBrush;
        resources[SystemColors.MenuBrushKey] = panelBrush;
        resources[SystemColors.MenuTextBrushKey] = textBrush;
        resources[SystemColors.HighlightBrushKey] = Freeze(new SolidColorBrush(Color.FromArgb(0x66, accent.R, accent.G, accent.B)));
        resources[SystemColors.HighlightTextBrushKey] = textBrush;

        // Replace the app's rounded, filled text box style with a plain one that reads on the panel.
        Style textBoxStyle = new(typeof(TextBox));
        textBoxStyle.Setters.Add(new Setter(Control.BackgroundProperty, fieldBrush));
        textBoxStyle.Setters.Add(new Setter(Control.ForegroundProperty, textBrush));
        textBoxStyle.Setters.Add(new Setter(Control.BorderBrushProperty, borderBrush));
        textBoxStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        textBoxStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
        textBoxStyle.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty, textBrush));
        resources[typeof(TextBox)] = textBoxStyle;

        Style checkBoxStyle = new(typeof(CheckBox));
        checkBoxStyle.Setters.Add(new Setter(Control.ForegroundProperty, textBrush));
        resources[typeof(CheckBox)] = checkBoxStyle;
    }

    private const string SearchPanelTemplateXaml = """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:search="clr-namespace:ICSharpCode.AvalonEdit.Search;assembly=ICSharpCode.AvalonEdit"
                         TargetType="{x:Type search:SearchPanel}">
          <ControlTemplate.Resources>
            <Style x:Key="FindButton" TargetType="ButtonBase">
              <Setter Property="Width" Value="26"/>
              <Setter Property="Height" Value="26"/>
              <Setter Property="Margin" Value="2,0,0,0"/>
              <Setter Property="Padding" Value="0"/>
              <Setter Property="Focusable" Value="False"/>
              <Setter Property="FontFamily" Value="Segoe Fluent Icons, Segoe MDL2 Assets"/>
              <Setter Property="FontSize" Value="12"/>
              <Setter Property="Foreground" Value="{DynamicResource {x:Static SystemColors.ControlTextBrushKey}}"/>
              <Setter Property="Template">
                <Setter.Value>
                  <ControlTemplate TargetType="ButtonBase">
                    <Border x:Name="Chrome" Background="Transparent" CornerRadius="4">
                      <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                      <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource {x:Static SystemColors.ControlLightLightBrushKey}}"/>
                      </Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate>
                </Setter.Value>
              </Setter>
            </Style>
            <Style x:Key="FindToggle" TargetType="ToggleButton" BasedOn="{StaticResource FindButton}">
              <Setter Property="FontFamily" Value="Segoe UI"/>
              <Setter Property="FontSize" Value="12"/>
              <Setter Property="FontWeight" Value="SemiBold"/>
              <Setter Property="Template">
                <Setter.Value>
                  <ControlTemplate TargetType="ToggleButton">
                    <Border x:Name="Chrome" Background="Transparent" CornerRadius="4">
                      <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                      <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource {x:Static SystemColors.ControlLightLightBrushKey}}"/>
                      </Trigger>
                      <Trigger Property="IsChecked" Value="True">
                        <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource {x:Static SystemColors.HighlightBrushKey}}"/>
                      </Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate>
                </Setter.Value>
              </Setter>
            </Style>
          </ControlTemplate.Resources>
          <Border Background="{DynamicResource {x:Static SystemColors.ControlBrushKey}}"
                  BorderBrush="{DynamicResource {x:Static SystemColors.ControlDarkBrushKey}}"
                  BorderThickness="1,0,0,1" CornerRadius="0,0,0,6" Padding="4"
                  HorizontalAlignment="Right" VerticalAlignment="Top" Cursor="Arrow">
            <StackPanel Orientation="Horizontal">
              <TextBox x:Name="PART_searchTextBox" Width="180" Height="26" VerticalContentAlignment="Center"
                       Text="{Binding SearchPattern, RelativeSource={RelativeSource TemplatedParent}, UpdateSourceTrigger=PropertyChanged}"/>
              <ToggleButton Style="{StaticResource FindToggle}" Content="Aa" Margin="6,0,0,0" ToolTip="Match case"
                            IsChecked="{Binding MatchCase, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}"/>
              <ToggleButton Style="{StaticResource FindToggle}" Content="ab" ToolTip="Match whole words"
                            IsChecked="{Binding WholeWords, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}"/>
              <ToggleButton Style="{StaticResource FindToggle}" Content=".*" ToolTip="Use regular expressions"
                            IsChecked="{Binding UseRegex, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}"/>
              <Button Style="{StaticResource FindButton}" Margin="6,0,0,0" ToolTip="Previous match (Shift+F3)"
                      Command="{x:Static search:SearchCommands.FindPrevious}">
                <Path Data="M1,6 L5,2 L9,6" Width="10" Height="8" Stretch="Uniform" StrokeThickness="1.4" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"
                      Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=ButtonBase}}"/>
              </Button>
              <Button Style="{StaticResource FindButton}" ToolTip="Next match (F3)"
                      Command="{x:Static search:SearchCommands.FindNext}">
                <Path Data="M1,2 L5,6 L9,2" Width="10" Height="8" Stretch="Uniform" StrokeThickness="1.4" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"
                      Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=ButtonBase}}"/>
              </Button>
              <Button Style="{StaticResource FindButton}" ToolTip="Close (Esc)"
                      Command="{x:Static search:SearchCommands.CloseSearchPanel}">
                <Path Data="M1,1 L9,9 M9,1 L1,9" Width="9" Height="9" Stretch="Uniform" StrokeThickness="1.4" StrokeStartLineCap="Round" StrokeEndLineCap="Round"
                      Stroke="{Binding Foreground, RelativeSource={RelativeSource AncestorType=ButtonBase}}"/>
              </Button>
            </StackPanel>
          </Border>
        </ControlTemplate>
        """;

    private void RemoveCommandBindingsTheWindowOwns()
    {
        // The default input handler copies its bindings onto the text area when it attaches, so remove
        // them from both: the text area for now, the handler for whenever it is attached again.
        CommandBindingCollection attached = TextArea.CommandBindings;
        for (int i = attached.Count - 1; i >= 0; i--)
        {
            if (IsOwnedByTheWindow(attached[i]))
                attached.RemoveAt(i);
        }

        ICollection<CommandBinding> handlerBindings = TextArea.DefaultInputHandler.Editing.CommandBindings;
        foreach (CommandBinding binding in handlerBindings.Where(IsOwnedByTheWindow).ToList())
            handlerBindings.Remove(binding);

        static bool IsOwnedByTheWindow(CommandBinding binding) => Array.IndexOf(CommandsTheWindowOwns, binding.Command) >= 0;
    }

    /// <summary>
    /// AvalonEdit draws line numbers against the editor's left edge and the code against the gutter's
    /// dotted line. Pad both. (The editor's own Padding does not reach its scrolling text area.)
    /// </summary>
    private void SpaceOutTheGutter()
    {
        foreach (UIElement margin in TextArea.LeftMargins)
        {
            if (margin is LineNumberMargin lineNumbers)
                lineNumbers.Margin = new Thickness(6, 0, 6, 0);
        }

        TextArea.TextView.Margin = new Thickness(6, 0, 0, 0);
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    internal static System.Drawing.Color ToDrawingColor(Color color) => System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

    internal static Color ToMediaColor(System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary>
    /// Highlights as AvalonEdit does, except that on the dark theme each highlighting color is swapped
    /// for a dark-theme version, made once per color and cached.
    /// </summary>
    private sealed class ThemeAwareHighlightingColorizer(IHighlightingDefinition definition, CodeEditor editor)
        : HighlightingColorizer(definition)
    {
        private readonly Dictionary<HighlightingColor, HighlightingColor> darkColors = new(ReferenceEqualityComparer.Instance);

        public void ClearCache() => darkColors.Clear();

        protected override void ApplyColorToElement(VisualLineElement element, HighlightingColor color)
        {
            base.ApplyColorToElement(element, editor.IsDarkTheme ? GetDarkColor(color) : color);
        }

        private HighlightingColor GetDarkColor(HighlightingColor color)
        {
            if (darkColors.TryGetValue(color, out HighlightingColor? dark))
                return dark;

            dark = color.Clone();
            if (color.Foreground?.GetColor(CurrentContext) is Color foreground)
                dark.Foreground = new SimpleHighlightingBrush(ToMediaColor(CodeHighlightColors.ForegroundForDarkTheme(ToDrawingColor(foreground))));

            if (color.Background?.GetColor(CurrentContext) is Color background)
                dark.Background = new SimpleHighlightingBrush(ToMediaColor(CodeHighlightColors.BackgroundForDarkTheme(ToDrawingColor(background))));

            dark.Freeze();
            darkColors[color] = dark;
            return dark;
        }
    }
}
