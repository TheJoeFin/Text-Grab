using Markdig.Syntax;
using System.IO;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Controls;
using Text_Grab.Utilities;
using WpfBlock = System.Windows.Documents.Block;

namespace Tests;

public class MarkdownFeatureFixtureTests
{
    [WpfFact]
    public async Task GalleryRendersOnceAndKeepsItsFootnoteWhenSaved()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TextFiles", "MarkdownFeatures.md");
        // The gallery's optional web images are tested separately; keep this regression offline.
        string markdown = File.ReadAllText(path)
            .Replace("https://raw.githubusercontent.com/TheJoeFin/Text-Grab/main/Tests/Images/font_sample.png", "missing-image.png", StringComparison.Ordinal)
            .Replace("https://img.shields.io/badge/Text_Grab-Markdown-teal", "missing-image.png", StringComparison.Ordinal);
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16, path);
        await Task.WhenAll(GetRenderTasks(document.Blocks)).WaitAsync(TimeSpan.FromSeconds(30));

        string text = MarkdownFlowDocumentUtilities.GetDocumentPlainText(document);
        Assert.Equal(1, text.Split("Markdown viewer feature gallery", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, text.Split("End of gallery", StringSplitOptions.None).Length - 1);
        Assert.Contains("must still be present in plain text mode.", text);
        Assert.DoesNotContain("[^viewer-note]", text);
        System.Windows.Documents.List notes = Assert.IsType<System.Windows.Documents.List>(document.Blocks.LastBlock);
        Assert.Equal(2, notes.ListItems.Count);
        string saved = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document, preserveLiteralMarkdown: true);
        Assert.Equal(1, saved.Split("# Markdown viewer feature gallery", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, saved.Split("[^viewer-note]:", StringSplitOptions.None).Length - 1);
        Assert.Contains("[^viewer-note]: A **formatted footnote** with a [link](https://example.com) and `inline code`.", saved);
        Assert.Contains("    This second paragraph belongs to the same footnote.", saved);
    }

    private static IEnumerable<Task> GetRenderTasks(BlockCollection blocks)
    {
        foreach (WpfBlock block in blocks)
        {
            if (block is Paragraph paragraph)
            {
                foreach (InlineUIContainer container in paragraph.Inlines.OfType<InlineUIContainer>())
                    if (container.Child is MermaidDiagram diagram)
                        yield return diagram.RenderCompletion;
            }
            else if (block is System.Windows.Documents.List list)
            {
                foreach (Task task in list.ListItems.SelectMany(item => GetRenderTasks(item.Blocks)))
                    yield return task;
            }
        }
    }

    public static IEnumerable<object[]> ValidDiagramExamples()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TextFiles", "MarkdownFeatures.md");
        string markdown = File.ReadAllText(path);
        MarkdownDocument document = Markdig.Markdown.Parse(markdown);
        foreach (FencedCodeBlock block in document.Descendants().OfType<FencedCodeBlock>())
        {
            if (!string.Equals(block.Info, "mermaid", StringComparison.OrdinalIgnoreCase))
                continue;

            string source = block.Lines.ToString();
            if (source.StartsWith("this is deliberately not a diagram", StringComparison.Ordinal))
                continue;

            yield return [block.Line + 1, source, true];
            yield return [block.Line + 1, source, false];
        }
    }

    [WpfTheory]
    [MemberData(nameof(ValidDiagramExamples))]
    public async Task GalleryDiagramsRenderInBothThemes(int sourceLine, string source, bool isLightTheme)
    {
        BitmapSource bitmap = await MermaidImageRenderer.RenderAsync(source, isLightTheme);
        Assert.True(bitmap.IsFrozen, $"Diagram at line {sourceLine} did not produce a frozen bitmap.");
        byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        Assert.True(pixels.Where((_, index) => index % 4 == 3).Count(alpha => alpha != 0) > 100,
            $"Diagram at line {sourceLine} did not produce visible content.");
    }
}
