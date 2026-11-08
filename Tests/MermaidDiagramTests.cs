using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using Mermaider;
using Text_Grab.Controls;
using Text_Grab.Utilities;

namespace Tests;

public class MermaidDiagramTests
{
    [WpfTheory]
    [InlineData("```mermaid\nflowchart LR\nA[Start] --> B[End]\n```", "flowchart LR\nA[Start] --> B[End]")]
    [InlineData("```MERMAID\nsequenceDiagram\nAlice->>Bob: Hello\n```", "sequenceDiagram\nAlice->>Bob: Hello")]
    [InlineData("> ```mermaid\n> graph TD\n> A --> B\n> ```", "graph TD\nA --> B")]
    [InlineData("- ```mermaid\n  classDiagram\n  Animal <|-- Duck\n  ```", "classDiagram\nAnimal <|-- Duck")]
    public async Task MermaidFencesRenderNativeImagesAndKeepSourceInContainers(string markdown, string expectedSource)
    {
        FlowDocument document = CreateDocument(markdown);
        MermaidDiagram diagram = Assert.Single(GetDiagrams(document.Blocks));
        Assert.Equal(expectedSource, diagram.Source);
        await diagram.RenderCompletion.WaitAsync(TimeSpan.FromSeconds(15));

        Image image = Assert.Single(diagram.Children.OfType<Image>());
        BitmapSource bitmap = Assert.IsAssignableFrom<BitmapSource>(image.Source);
        Assert.True(bitmap.IsFrozen);
        Assert.Equal(Visibility.Visible, image.Visibility);
        Assert.Equal(Visibility.Collapsed, Assert.Single(diagram.Children.OfType<TextBlock>()).Visibility);
        Assert.True(CountPaintedPixels(bitmap) > 100);
        Assert.Equal(StretchDirection.DownOnly, image.StretchDirection);
        Assert.Equal(480, image.MaxHeight);
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [WpfTheory]
    [InlineData("```csharp\nA --> B\n```")]
    [InlineData("```mermaid-example\nA --> B\n```")]
    public void OtherCodeFencesRemainEditableCode(string markdown)
    {
        FlowDocument document = CreateDocument(markdown);
        Assert.Empty(GetDiagrams(document.Blocks));
        Assert.IsType<Run>(Assert.IsType<Paragraph>(Assert.Single(document.Blocks)).Inlines.FirstInline);
        Assert.Equal(markdown.Replace("\n", Environment.NewLine, StringComparison.Ordinal), MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfTheory]
    [InlineData("This is not a Mermaid diagram.")]
    [InlineData("")]
    public async Task InvalidDiagramsShowErrorsAndKeepSourceWithoutChangingTheDocument(string source)
    {
        string markdown = $"```mermaid\n{source}\n```";
        FlowDocument document = CreateDocument(markdown);
        MermaidDiagram diagram = Assert.Single(GetDiagrams(document.Blocks));
        RichTextBox editor = new() { Document = document };
        int changes = 0;
        editor.TextChanged += (_, _) => changes++;

        await diagram.RenderCompletion.WaitAsync(TimeSpan.FromSeconds(15));

        TextBlock fallback = Assert.Single(diagram.Children.OfType<TextBlock>());
        Assert.Equal(Visibility.Visible, fallback.Visibility);
        Assert.StartsWith("Mermaid diagram could not be rendered:", fallback.Text);
        Assert.Contains(source, fallback.Text);
        Assert.IsType<string>(diagram.ToolTip);
        Assert.Null(Assert.Single(diagram.Children.OfType<Image>()).Source);
        Assert.Equal(0, changes);
        Assert.Equal(markdown.Replace("\n", Environment.NewLine, StringComparison.Ordinal), MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfFact]
    public async Task OversizedDiagramsFailVisiblyAndPreserveAllSource()
    {
        string source = "flowchart LR\n" + new string('A', 50_000);
        FlowDocument document = CreateDocument($"```mermaid\n{source}\n```");
        MermaidDiagram diagram = Assert.Single(GetDiagrams(document.Blocks));
        await diagram.RenderCompletion.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Contains("limit", Assert.Single(diagram.Children.OfType<TextBlock>()).Text, StringComparison.OrdinalIgnoreCase);
        Assert.Null(Assert.Single(diagram.Children.OfType<Image>()).Source);
        Assert.Equal(source, diagram.Source);
        Assert.Contains(source, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfFact]
    public async Task RenderingAndThemeChangesPreserveEditsAndOffsetMappings()
    {
        const string markdown = "before\n\n```mermaid\nflowchart LR\nA --> B\n```\n\nafter **last**";
        FlowDocument document = CreateDocument(markdown);
        MermaidDiagram diagram = Assert.Single(GetDiagrams(document.Blocks));
        RichTextBox editor = new() { Document = document };
        int changes = 0;
        editor.TextChanged += (_, _) => changes++;
        await diagram.RenderCompletion.WaitAsync(TimeSpan.FromSeconds(15));
        Image image = Assert.Single(diagram.Children.OfType<Image>());
        BitmapSource light = Assert.IsAssignableFrom<BitmapSource>(image.Source);

        MarkdownFlowDocumentUtilities.ApplyTheme(document, new Border(), isLightTheme: false);
        await diagram.RenderCompletion.WaitAsync(TimeSpan.FromSeconds(15));
        BitmapSource dark = Assert.IsAssignableFrom<BitmapSource>(image.Source);
        Assert.NotSame(light, dark);
        Assert.False(GetPixels(light).SequenceEqual(GetPixels(dark)));
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document).Replace("\r\n", "\n", StringComparison.Ordinal));

        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);
        foreach (string token in new[] { "before", "after", "last" })
        {
            int offset = markdown.IndexOf(token, StringComparison.Ordinal);
            TextPointer start = MarkdownFlowDocumentUtilities.MapRawOffsetToPosition(document, map, offset);
            TextPointer end = MarkdownFlowDocumentUtilities.MapRawOffsetToPosition(document, map, offset + token.Length);
            Assert.Equal(token, new TextRange(start, end).Text);
        }

        int changesBeforeRendering = changes;
        diagram.ApplyTheme(true);
        await diagram.RenderCompletion.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(changesBeforeRendering, changes);
        Paragraph code = document.Blocks.OfType<Paragraph>().ElementAt(1);
        code.Inlines.InsertBefore(code.Inlines.FirstInline, new Run("%% Comment\n"));
        Assert.Contains("%% Comment\nflowchart LR\nA --> B", MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfFact]
    public async Task DiagramsShrinkToFitTheEditorWithoutChangingTheirAspectRatio()
    {
        FlowDocument document = CreateDocument("```mermaid\nflowchart LR\nA[A longer label] --> B[Another label]\n```");
        MermaidDiagram diagram = Assert.Single(GetDiagrams(document.Blocks));
        RichTextBox editor = new() { Document = document, Width = 320, Height = 600 };
        editor.Measure(new Size(320, 600));
        editor.Arrange(new Rect(0, 0, 320, 600));
        editor.UpdateLayout();
        await diagram.RenderCompletion.WaitAsync(TimeSpan.FromSeconds(15));
        await editor.Dispatcher.InvokeAsync(editor.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);

        Image image = Assert.Single(diagram.Children.OfType<Image>());
        BitmapSource bitmap = Assert.IsAssignableFrom<BitmapSource>(image.Source);
        Assert.Equal(224, diagram.MaxWidth);
        Assert.InRange(diagram.ActualWidth, 1, 224);
        Assert.InRange(image.ActualWidth, 1, 224);
        Assert.InRange(image.ActualHeight, 1, 480);
        Assert.Equal(bitmap.Width / bitmap.Height, image.ActualWidth / image.ActualHeight, 2);
    }

    [WpfFact]
    public async Task SourceCharacterLimitAcceptsTheBoundaryAndRejectsOneExtraCharacter()
    {
        string source = "flowchart LR\nA --> B\n" + string.Join('\n', Enumerable.Repeat("%% " + new string('x', 497), 99)) + "\n%% ";
        source += new string('x', 50_000 - source.Length);
        Assert.Equal(50_000, source.Length);
        BitmapSource bitmap = await MermaidImageRenderer.RenderAsync(source, isLightTheme: true);
        Assert.True(CountPaintedPixels(bitmap) > 100);
        await Assert.ThrowsAsync<MermaidResourceLimitException>(() => MermaidImageRenderer.RenderAsync(source + "x", isLightTheme: true));
    }

    [WpfFact]
    public void HaloTextKeepsItsForegroundSeparateFromItsOutline()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="50">
              <text id="label" x="10" y="30" font-size="18" fill="#eeeeee" stroke="#202024" stroke-width="4" paint-order="stroke">
                <tspan id="part" fill="#eeeeee">Readable</tspan>
              </text>
            </svg>
            """;
        XmlDocument document = new();
        document.LoadXml(Encoding.UTF8.GetString(MermaidImageRenderer.PrepareSvg(Encoding.UTF8.GetBytes(svg))));

        XmlElement[] layers = document.GetElementsByTagName("text").Cast<XmlElement>().ToArray();
        Assert.Equal(2, layers.Length);
        Assert.Equal("none", layers[0].GetAttribute("fill"));
        Assert.Equal("#202024", layers[0].GetAttribute("stroke"));
        Assert.Equal("4", layers[0].GetAttribute("stroke-width"));
        Assert.Equal("#eeeeee", layers[1].GetAttribute("fill"));
        Assert.Equal("none", layers[1].GetAttribute("stroke"));
        Assert.All(layers, layer => Assert.False(layer.HasAttribute("paint-order")));
        Assert.Equal("label", layers[1].GetAttribute("id"));
        Assert.False(layers[0].HasAttribute("id"));
        XmlElement haloSpan = Assert.IsType<XmlElement>(layers[0].GetElementsByTagName("tspan")[0]);
        XmlElement foregroundSpan = Assert.IsType<XmlElement>(layers[1].GetElementsByTagName("tspan")[0]);
        Assert.Equal("none", haloSpan.GetAttribute("fill"));
        Assert.False(haloSpan.HasAttribute("id"));
        Assert.Equal("part", foregroundSpan.GetAttribute("id"));
        Assert.Equal("#eeeeee", foregroundSpan.GetAttribute("fill"));
    }

    [WpfTheory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task SankeyAndQuadrantLabelsHaveVisibleForegroundPixelsInBothThemes(bool isLightTheme, bool isQuadrant)
    {
        string source = isQuadrant
            ? "quadrantChart\ntitle Viewer checks\nx-axis Simple --> Complex\ny-axis Optional --> Essential\nquadrant-1 Prioritize\nquadrant-2 Baseline\nquadrant-3 Later\nquadrant-4 Explore\nText: [0.2, 0.8]\nDiagrams: [0.8, 0.7]\nLinks: [0.3, 0.4]"
            : "sankey-beta\nCaptures,Text,70\nCaptures,Images,30\nText,Documents,60\nText,Clipboard,10\nImages,Documents,30";
        string[] expectedLabels = isQuadrant
            ? ["Text", "Diagrams", "Links"]
            : ["Captures", "100", "Text", "70", "Images", "30", "Documents", "90", "Clipboard", "10"];
        string svg = MermaidRenderer.RenderSvg(source, new Mermaider.Models.RenderOptions
        {
            Bg = isLightTheme ? "#f5f5f5" : "#202024",
            Fg = isLightTheme ? "#202024" : "#eeeeee",
            Font = "Segoe UI",
            FontSize = "16px",
            Elevation = 0
        });
        XmlDocument document = new();
        document.LoadXml(Encoding.UTF8.GetString(MermaidImageRenderer.PrepareSvg(Encoding.UTF8.GetBytes(svg))));
        XmlElement[] labels = document.GetElementsByTagName("text").Cast<XmlElement>()
            .Where(text => text.GetAttribute("fill") != "none" && expectedLabels.Contains(text.InnerText)).ToArray();
        Assert.Equal(expectedLabels, labels.Select(label => label.InnerText));
        BitmapSource bitmap = await MermaidImageRenderer.RenderAsync(source, isLightTheme);
        double scale = bitmap.PixelWidth / bitmap.Width;
        foreach (XmlElement label in labels)
        {
            Color foreground = (Color)ColorConverter.ConvertFromString(label.GetAttribute("fill"));
            double x = double.Parse(label.GetAttribute("x"), CultureInfo.InvariantCulture);
            double baseline = double.Parse(label.GetAttribute("y"), CultureInfo.InvariantCulture)
                + double.Parse(label.GetAttribute("dy"), CultureInfo.InvariantCulture);
            double fontSize = double.Parse(label.GetAttribute("font-size").Replace("px", "", StringComparison.Ordinal), CultureInfo.InvariantCulture);
            Int32Rect region = new((int)(x * scale), (int)((baseline - fontSize) * scale), (int)(100 * scale), (int)((fontSize + 2) * scale));
            byte[] pixels = new byte[region.Width * region.Height * 4];
            bitmap.CopyPixels(region, pixels, region.Width * 4, 0);
            int visibleTextPixels = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] >= 240 && Math.Abs(pixels[i] - foreground.B) <= 12
                    && Math.Abs(pixels[i + 1] - foreground.G) <= 12 && Math.Abs(pixels[i + 2] - foreground.R) <= 12)
                    visibleTextPixels++;
            }
            Assert.True(visibleTextPixels >= 30, $"Diagram label '{label.InnerText}' has only {visibleTextPixels} foreground pixels.");
        }
    }

    [WpfFact]
    public void SvgAdapterResolvesNestedColorsAndFontSizesWithoutChangingLabels()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" style="--fg:#000000;--bg:#ffffff;--accent:#ff0000">
            <style>svg { --ink:var(--fg); --size:14px; } text { font-family: 'Segoe UI'; }</style>
            <rect width="20" height="20" fill="color-mix(in srgb, var(--accent, var(--fg)) 25%, var(--bg))" />
            <text font-size="var(--size)" fill="var(--ink)">var(--fg)</text>
            </svg>
            """;
        byte[] prepared = MermaidImageRenderer.PrepareSvg(Encoding.UTF8.GetBytes(svg));
        XmlDocument document = new();
        document.LoadXml(Encoding.UTF8.GetString(prepared));
        XmlElement rect = (XmlElement)document.GetElementsByTagName("rect")[0]!;
        XmlElement text = (XmlElement)document.GetElementsByTagName("text")[0]!;
        Assert.Equal("#ffbfbf", rect.GetAttribute("fill"));
        Assert.Equal("#000000", text.GetAttribute("fill"));
        Assert.Equal("14px", text.GetAttribute("font-size"));
        Assert.Equal("var(--fg)", text.InnerText);

        BitmapSource bitmap = SvgImageLoader.RenderSvg(prepared);
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(20, 20, 1, 1), pixel, 4, 0);
        Assert.Equal(new byte[] { 191, 191, 255, 255 }, pixel);
    }

    [WpfTheory]
    [InlineData("var(--missing)")]
    [InlineData("var(--loop)")]
    [InlineData("color-mix(in srgb, black 0%, white 0%)")]
    public void SvgAdapterRejectsInvalidStylesExplicitly(string fill)
    {
        string svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" style=\"--loop:var(--loop)\"><rect fill=\"{fill}\"/></svg>";
        Assert.Throws<InvalidDataException>(() => MermaidImageRenderer.PrepareSvg(Encoding.UTF8.GetBytes(svg)));
    }

    private static FlowDocument CreateDocument(string markdown) =>
        MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

    private static IEnumerable<MermaidDiagram> GetDiagrams(BlockCollection blocks)
    {
        foreach (Block block in blocks)
        {
            if (block is Paragraph paragraph)
            {
                foreach (InlineUIContainer container in paragraph.Inlines.OfType<InlineUIContainer>())
                    if (container.Child is MermaidDiagram diagram)
                        yield return diagram;
            }
            else if (block is System.Windows.Documents.List list)
            {
                foreach (MermaidDiagram diagram in list.ListItems.SelectMany(item => GetDiagrams(item.Blocks)))
                    yield return diagram;
            }
        }
    }

    private static byte[] GetPixels(BitmapSource bitmap)
    {
        byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return pixels;
    }

    private static int CountPaintedPixels(BitmapSource bitmap) =>
        GetPixels(bitmap).Where((_, index) => index % 4 == 3).Count(alpha => alpha != 0);
}
