using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Controls;
using Text_Grab.Utilities;

namespace Tests;

public class MarkdownExtensionTests
{
    [WpfTheory]
    [InlineData("~~")]
    [InlineData("==")]
    [InlineData("++")]
    [InlineData("~")]
    [InlineData("^")]
    public void EmphasisExtrasRenderDistinctlyAndPreserveEdits(string delimiter)
    {
        string markdown = $"before {delimiter}word{delimiter} after";
        FlowDocument document = CreateDocument(markdown);
        Span span = Assert.IsType<Span>(Assert.IsType<Paragraph>(Assert.Single(document.Blocks)).Inlines.ElementAt(1));
        Assert.Equal(FontWeights.Normal, span.FontWeight);
        Assert.Equal(FontStyles.Normal, span.FontStyle);
        switch (delimiter)
        {
            case "~~":
                Assert.Equal(TextDecorationLocation.Strikethrough, Assert.Single(span.TextDecorations).Location);
                break;
            case "++":
                Assert.Equal(TextDecorationLocation.Underline, Assert.Single(span.TextDecorations).Location);
                break;
            case "==":
                Assert.Equal(Color.FromRgb(255, 235, 130), Assert.IsType<SolidColorBrush>(span.Background).Color);
                break;
            case "~":
            case "^":
                Assert.Equal(delimiter == "~" ? BaselineAlignment.Subscript : BaselineAlignment.Superscript, span.BaselineAlignment);
                Assert.Equal(12, span.FontSize);
                break;
        }
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
        Assert.IsType<Run>(Assert.Single(span.Inlines)).Text = "changed ";
        span.Inlines.Add(new Bold(new Run("bold")));
        string expected = $"before {delimiter}changed **bold**{delimiter} after";
        foreach (bool preserveLiteralMarkdown in new[] { false, true })
        {
            string saved = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document, preserveLiteralMarkdown);
            Assert.Equal(expected, saved);
            Assert.Equal(saved, MarkdownFlowDocumentUtilities.SerializeToMarkdown(CreateDocument(saved), preserveLiteralMarkdown));
        }
    }

    [WpfTheory]
    [InlineData("before ~~strike~~ ==mark== H~sub~O x^super^ and ++inserted++ after $x^2$ tail")]
    [InlineData("> before ~~strike~~ ==mark== H~sub~O x^super^ and ++inserted++ after $x^2$ tail")]
    [InlineData("- before ~~strike~~ ==mark== H~sub~O x^super^ and ++inserted++ after $x^2$ tail")]
    [InlineData("| Heading |\n| --- |\n| before ~~strike~~ ==mark== H~sub~O x^super^ and ++inserted++ after $x^2$ tail |")]
    [InlineData("before[^note]\n\n[^note]: before ~~strike~~ ==mark== H~sub~O x^super^ and ++inserted++ after $x^2$ tail")]
    public void ExtensionsInContainersKeepTheirSyntaxAndFindMappings(string markdown)
    {
        FlowDocument document = CreateDocument(markdown);
        foreach (bool preserveLiteralMarkdown in new[] { false, true })
        {
            string saved = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document, preserveLiteralMarkdown);
            Assert.Contains("~~strike~~ ==mark== H~sub~O x^super^ and ++inserted++ after $x^2$ tail", saved);
            Assert.Equal(saved, MarkdownFlowDocumentUtilities.SerializeToMarkdown(CreateDocument(saved), preserveLiteralMarkdown));
        }
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);
        foreach (string token in new[] { "strike", "mark", "sub", "super", "inserted", "after", "tail" })
        {
            int rawStart = markdown.IndexOf(token, StringComparison.Ordinal);
            TextPointer start = MarkdownFlowDocumentUtilities.MapRawOffsetToPosition(document, map, rawStart);
            TextPointer end = MarkdownFlowDocumentUtilities.MapRawOffsetToPosition(document, map, rawStart + token.Length);
            Assert.Equal(token, new TextRange(start, end).Text);
        }
    }

    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void HighlightContrastAndScriptSizesFollowThemeAndParentFont(bool isLightTheme)
    {
        FlowDocument document = CreateDocument("# Heading H~2~O x^2^ ==marked==");
        Paragraph heading = Assert.IsType<Paragraph>(Assert.Single(document.Blocks));
        Border resources = new();
        SolidColorBrush foreground = isLightTheme ? Brushes.Black : Brushes.White;
        resources.Resources["TextFillColorPrimaryBrush"] = foreground;
        for (int pass = 0; pass < 2; pass++)
        {
            MarkdownFlowDocumentUtilities.ApplyTheme(document, resources, isLightTheme);
            Span[] spans = heading.Inlines.OfType<Span>().ToArray();
            Assert.Equal(3, spans.Length);
            Assert.Equal(heading.FontSize * 0.75, spans[0].FontSize);
            Assert.Equal(heading.FontSize * 0.75, spans[1].FontSize);
            Assert.Equal(BaselineAlignment.Subscript, spans[0].BaselineAlignment);
            Assert.Equal(BaselineAlignment.Superscript, spans[1].BaselineAlignment);
            Color background = Assert.IsType<SolidColorBrush>(spans[2].Background).Color;
            Assert.Same(foreground, spans[2].Foreground);
            double foregroundLuminance = Luminance(foreground.Color);
            double backgroundLuminance = Luminance(background);
            double contrast = (Math.Max(foregroundLuminance, backgroundLuminance) + 0.05) /
                (Math.Min(foregroundLuminance, backgroundLuminance) + 0.05);
            Assert.True(contrast >= 4.5, $"Highlight contrast was {contrast}.");
        }
        Assert.Equal("# Heading H~2~O x^2^ ==marked==", MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfFact]
    public void EscapedDelimitersAndCodeDoNotBecomeExtensionsWhenSaved()
    {
        const string literal = "~~strike~~ ==mark== H~2~O x^2^ ++inserted++ $x^2$";
        FlowDocument document = new(new Paragraph(new Run(literal)));
        string saved = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);
        FlowDocument reloaded = CreateDocument(saved);
        Assert.Equal(literal, MarkdownFlowDocumentUtilities.GetDocumentPlainText(reloaded));
        Assert.Empty(GetMath(reloaded));
        Assert.Empty(Assert.IsType<Paragraph>(Assert.Single(reloaded.Blocks)).Inlines.OfType<Span>());
        string code = $"`{literal}`";
        FlowDocument codeDocument = CreateDocument(code);
        Assert.Empty(GetMath(codeDocument));
        Assert.Equal(code, MarkdownFlowDocumentUtilities.SerializeToMarkdown(codeDocument));
    }

    [WpfTheory]
    [InlineData("before $x^2 + y^2 = z^2$ after")]
    [InlineData("$\\frac{1}{2}$")]
    [InlineData("$$\n\\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}\n$$")]
    [InlineData("> $$\n> \\sum_{i=1}^{n} i\n> $$")]
    [InlineData("- $$\n  \\frac{a}{b}\n  $$")]
    [InlineData("Body[^note]\n\n[^note]:\n    $$\n    \\sqrt{x}\n    $$")]
    public void InlineAndDisplayMathRenderNativelyAndRoundTrip(string markdown)
    {
        FlowDocument document = CreateDocument(markdown);
        MathFormula formula = Assert.Single(GetMath(document));
        Image image = Assert.Single(formula.Children.OfType<Image>());
        DrawingImage drawing = Assert.IsType<DrawingImage>(image.Source);
        Assert.True(drawing.IsFrozen);
        Assert.Equal(Visibility.Visible, image.Visibility);
        Assert.Equal(Visibility.Collapsed, Assert.Single(formula.Children.OfType<TextBlock>()).Visibility);
        Assert.True(GetPixels(image).Where((_, index) => index % 4 == 3).Count(alpha => alpha != 0) > 15);
        string saved = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);
        Assert.Contains(formula.Markdown, saved.Replace("\n> ", "\n", StringComparison.Ordinal)
            .Replace("\n    ", "\n", StringComparison.Ordinal).Replace("\n  ", "\n", StringComparison.Ordinal));
        FlowDocument reloaded = CreateDocument(saved);
        Assert.Single(GetMath(reloaded));
        Assert.Equal(saved, MarkdownFlowDocumentUtilities.SerializeToMarkdown(reloaded));
    }

    [WpfTheory]
    [InlineData("\\notacommand{x}")]
    [InlineData("\\frac{1}")]
    public void UnsupportedMathFailsVisiblyAndPreservesSource(string source)
    {
        string markdown = $"before ${source}$ after";
        FlowDocument document = CreateDocument(markdown);
        MathFormula formula = Assert.Single(GetMath(document));
        TextBlock fallback = Assert.Single(formula.Children.OfType<TextBlock>());
        Assert.Equal(Visibility.Visible, fallback.Visibility);
        Assert.StartsWith("Math formula could not be rendered:", fallback.Text);
        Assert.Contains(source, fallback.Text);
        Assert.Equal(Visibility.Collapsed, Assert.Single(formula.Children.OfType<Image>()).Visibility);
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfFact]
    public void MathLimitsFailVisiblyAndPreserveAllSource()
    {
        foreach (string source in new[] { new string('a', 4001), new string('{', 33) + "x" + new string('}', 33) })
        {
            FlowDocument document = CreateDocument($"${source}$");
            MathFormula formula = Assert.Single(GetMath(document));
            TextBlock fallback = Assert.Single(formula.Children.OfType<TextBlock>());
            Assert.Equal(Visibility.Visible, fallback.Visibility);
            Assert.Contains("limited", fallback.Text);
            Assert.Null(Assert.Single(formula.Children.OfType<Image>()).Source);
            Assert.Equal($"${source}$", MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
        }
    }

    [WpfFact]
    public void MathThemeAndSizeChangesKeepSourceWithoutChangingTheTextTree()
    {
        const string markdown = "before $\\frac{1}{2}$ after";
        FlowDocument document = CreateDocument(markdown);
        MathFormula formula = Assert.Single(GetMath(document));
        Image image = Assert.Single(formula.Children.OfType<Image>());
        byte[] light = GetPixels(image);
        double width = image.Source.Width;
        RichTextBox editor = new() { Document = document };
        int changes = 0;
        editor.TextChanged += (_, _) => changes++;
        formula.ApplyTheme(Brushes.White, 24);
        Assert.True(image.Source.Width > width);
        Assert.False(light.SequenceEqual(GetPixels(image)));
        Assert.Equal(0, changes);
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
        GC.KeepAlive(editor);
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte value) => value / 255.0 <= 0.04045 ? value / 255.0 / 12.92 : Math.Pow((value / 255.0 + 0.055) / 1.055, 2.4);
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }

    private static byte[] GetPixels(Image image)
    {
        int width = Math.Max(1, (int)Math.Ceiling(image.Source.Width));
        int height = Math.Max(1, (int)Math.Ceiling(image.Source.Height));
        image.Measure(new Size(width, height));
        image.Arrange(new Rect(0, 0, width, height));
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(image);
        byte[] pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    private static FlowDocument CreateDocument(string markdown) =>
        MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

    private static IEnumerable<MathFormula> GetMath(FlowDocument document)
    {
        for (TextPointer? position = document.ContentStart; position is not null;
            position = position.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart &&
                position.GetAdjacentElement(LogicalDirection.Forward) is InlineUIContainer { Child: MathFormula formula })
                yield return formula;
        }
    }
}
