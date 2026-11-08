using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using WpfMath.Parsers;
using WpfMath.Rendering;
using XamlMath;
using XamlMath.Exceptions;

namespace Text_Grab.Controls;

internal sealed class MathFormula : Grid
{
    private readonly string source;
    private readonly bool isDisplay;
    private readonly Image image;
    private readonly TextBlock fallback;

    internal string Markdown { get; }

    internal MathFormula(string source, string markdown, bool isDisplay)
    {
        this.source = source;
        this.isDisplay = isDisplay;
        Markdown = markdown;
        image = new Image { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = 480 };
        fallback = new TextBlock { Text = markdown, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        Children.Add(image);
        Children.Add(fallback);
        AutomationProperties.SetName(this, "Math formula");
        AutomationProperties.SetHelpText(this, source);
    }

    internal void ApplyTheme(Brush foreground, double fontSize)
    {
        fallback.Foreground = foreground;
        fallback.FontSize = fontSize;
        try
        {
            ValidateSource();
            TexFormula formula = WpfTeXFormulaParser.Instance.Parse(source);
            TexEnvironment environment = WpfTeXEnvironment.Create(isDisplay ? TexStyle.Display : TexStyle.Text,
                fontSize, "Segoe UI", foreground);
            DrawingVisual visual = new();
            using (DrawingContext context = visual.RenderOpen())
                formula.RenderTo(context, environment, fontSize);
            Rect bounds = visual.ContentBounds;
            if (bounds.IsEmpty || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) ||
                bounds.Width <= 0 || bounds.Height <= 0 || bounds.Width > 8192 || bounds.Height > 8192)
                throw new InvalidOperationException("The formula is empty or exceeds the supported rendering size.");

            DrawingImage drawing = new(visual.Drawing);
            drawing.Freeze();
            image.Source = drawing;
            image.Visibility = Visibility.Visible;
            fallback.Visibility = Visibility.Collapsed;
            ToolTip = "Math formula. Switch to plain text mode to edit the formula source.";
        }
        catch (Exception exception) when (exception is TexException or ArgumentException or InvalidOperationException
            or FormatException or NotSupportedException or OverflowException)
        {
            Debug.WriteLine($"Math formula could not be rendered: {exception.Message}");
            image.Visibility = Visibility.Collapsed;
            fallback.Text = $"Math formula could not be rendered: {exception.Message}{Environment.NewLine}{Markdown}";
            fallback.Visibility = Visibility.Visible;
            ToolTip = exception.Message;
        }
    }

    private void ValidateSource()
    {
        if (source.Length > 4000)
            throw new InvalidOperationException("Math formulas are limited to 4,000 source characters.");
        int depth = 0;
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] == '\\')
            {
                index++;
                continue;
            }
            if (source[index] == '{' && ++depth > 32)
                throw new InvalidOperationException("Math formulas are limited to 32 nested groups.");
            if (source[index] == '}')
                depth--;
        }
    }
}
