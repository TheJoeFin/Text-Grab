using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
/// </remarks>
public class CodeEditor : TextEditor
{
    private static readonly RoutedCommand[] CommandsTheWindowOwns = [AvalonEditCommands.DeleteLine, AvalonEditCommands.IndentSelection];

    public CodeEditor()
    {
        ShowLineNumbers = true;
        Options.HighlightCurrentLine = true;
        Options.EnableHyperlinks = false;
        Options.EnableEmailHyperlinks = false;
        Options.IndentationSize = 4;

        RemoveCommandBindingsTheWindowOwns();
        SpaceOutTheGutter();
        Loaded += (_, _) => ApplyTheme();
    }

    /// <summary>True while the editor is showing the dark theme's colors.</summary>
    public bool IsDarkTheme { get; private set; }

    protected override IVisualLineTransformer CreateColorizer(IHighlightingDefinition highlightingDefinition)
        => new ThemeAwareHighlightingColorizer(highlightingDefinition, this);

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == ForegroundProperty)
            ApplyTheme();
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

        if (isDark == IsDarkTheme)
            return;

        IsDarkTheme = isDark;
        foreach (ThemeAwareHighlightingColorizer colorizer in TextArea.TextView.LineTransformers.OfType<ThemeAwareHighlightingColorizer>())
            colorizer.ClearCache();

        TextArea.TextView.Redraw();
    }

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
