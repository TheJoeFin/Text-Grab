using System.IO;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Utilities;

namespace Tests;

public class MarkdownDocumentUtilitiesTests
{
    [WpfTheory]
    [InlineData("\n", false)]
    [InlineData("\r\n", false)]
    [InlineData("\n", true)]
    [InlineData("\r\n", true)]
    public void FootnotesAndReferenceDefinitionsDoNotRepeatTheDocument(string newline, bool preserveLiteralMarkdown)
    {
        string markdown = string.Join(newline,
            "# Unique heading", "", "Before[^note] and again[^note].", "",
            "[site][target]", "", "[^note]: First **literal** note paragraph.", "",
            "    Second note paragraph.", "", "[^unused]: Unreferenced note.", "",
            "[target]: https://example.com \"Reference title\"", "", "## Unique ending", "", "After.");
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

        Assert.Equal(8, document.Blocks.Count);
        Assert.Equal("Unique heading", new TextRange(document.Blocks.FirstBlock.ContentStart, document.Blocks.FirstBlock.ContentEnd).Text.Trim());
        Block ending = document.Blocks.ElementAt(document.Blocks.Count - 2);
        Assert.Equal("After.", new TextRange(ending.ContentStart, ending.ContentEnd).Text.Trim());
        System.Windows.Documents.List footnotes = Assert.IsType<System.Windows.Documents.List>(document.Blocks.LastBlock);
        ListItem note = Assert.Single(footnotes.ListItems);
        Paragraph body = Assert.IsType<Paragraph>(note.Blocks.FirstBlock);
        Assert.Single(body.Inlines.OfType<Bold>());
        string saved = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document, preserveLiteralMarkdown);
        Assert.Equal(1, saved.Split("# Unique heading", StringSplitOptions.None).Length - 1);
        Assert.Equal(3, saved.Split("[^note]", StringSplitOptions.None).Length - 1);
        Assert.Contains("[^note]: First **literal** note paragraph.", saved);
        Assert.Contains("    Second note paragraph.", saved);
        Assert.Contains("[^unused]: Unreferenced note.", saved);
        Assert.Contains("[target]: https://example.com \"Reference title\"", saved);
        Assert.Contains("[site](https://example.com)", saved);
        Assert.True(saved.IndexOf("[^unused]:", StringComparison.Ordinal) < saved.IndexOf("[target]:", StringComparison.Ordinal));
        Assert.True(saved.IndexOf("After.", StringComparison.Ordinal) < saved.IndexOf("[^note]:", StringComparison.Ordinal));
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);
        Assert.Equal(map.Blocks.OrderBy(block => block.RawStart), map.Blocks);
        foreach (string token in new[] { "Before", "literal", "Second", "Unique ending", "After" })
            Assert.Equal(token, MapAndSlice(document, map, markdown.IndexOf(token, StringComparison.Ordinal), token.Length));
        Assert.Equal("1.\tFirst", MapAndSlice(document, map, markdown.IndexOf("First", StringComparison.Ordinal), "First".Length));

        FlowDocument reloaded = MarkdownFlowDocumentUtilities.CreateFlowDocument(saved, new FontFamily("Segoe UI"), 16);
        Assert.Equal(saved, MarkdownFlowDocumentUtilities.SerializeToMarkdown(reloaded, preserveLiteralMarkdown));
    }

    [WpfFact]
    public void Html_ReadmeLayoutRendersAlignedHeadingsAndLinkedImages()
    {
        const string html = """
            <p align="center">
              <img width="128" align="center" src="font_sample.png">
            </p>
            <h1 align="center">
              Text Grab
            </h1>
            <p align="center">
              Copy any text you can see.
            </p>
            <p align="center">
              <a href="https://example.com/store?a=1&amp;b=2" target="_blank">
                <img src="font_sample.png" width="200" alt="Store link" />
              </a>
            </p>
            <p align="center">
              <img src="font_sample.png" alt="Downloads" />
              <img src="font_sample.png" alt="Release" />
            </p>
            """;
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(html, new FontFamily("Segoe UI"), 16,
            Path.GetFullPath(@"Images\notes.md"));

        Paragraph[] paragraphs = document.Blocks.Cast<Paragraph>().ToArray();
        Assert.Equal(5, paragraphs.Length);
        Assert.All(paragraphs, paragraph => Assert.Equal(System.Windows.TextAlignment.Center, paragraph.TextAlignment));
        Assert.Equal("Text Grab", new TextRange(paragraphs[1].ContentStart, paragraphs[1].ContentEnd).Text.Trim());
        Assert.Equal(System.Windows.FontWeights.Bold, paragraphs[1].FontWeight);
        Assert.Equal("Copy any text you can see.", new TextRange(paragraphs[2].ContentStart, paragraphs[2].ContentEnd).Text.Trim());
        Image[] images = GetImages(document.Blocks).ToArray();
        Assert.Equal(4, images.Length);
        Assert.Equal(128, images[0].Width);
        Assert.Equal(200, images[1].Width);
        Assert.Equal("Store link", AutomationProperties.GetName(images[1]));
        Assert.Equal(new[] { "Downloads", "Release" }, images.Skip(2).Select(AutomationProperties.GetName));
        Hyperlink link = Assert.IsType<Hyperlink>(Assert.Single(paragraphs[3].Inlines));
        Assert.Equal("https://example.com/store?a=1&b=2", link.NavigateUri.OriginalString);
        Assert.Single(GetImages(link.Inlines));

        string saved = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);
        Assert.Contains("<h1 align=\"center\">Text Grab</h1>", saved);
        Assert.Contains("target=\"_blank\"", saved);
        Assert.Contains("<img width=\"128\" align=\"center\" src=\"font_sample.png\">", saved);
        FlowDocument reloaded = MarkdownFlowDocumentUtilities.CreateFlowDocument(saved, new FontFamily("Segoe UI"), 16,
            Path.GetFullPath(@"Images\notes.md"));
        Assert.Equal(5, reloaded.Blocks.Count);
        Assert.Equal(4, GetImages(reloaded.Blocks).Count());
        Assert.Equal(saved, MarkdownFlowDocumentUtilities.SerializeToMarkdown(reloaded));

        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);
        Assert.Equal("Text Grab", MapAndSlice(document, map, html.IndexOf("Text Grab", StringComparison.Ordinal), 9));
        Run headingRun = Assert.IsType<Run>(paragraphs[1].Inlines.FirstInline);
        headingRun.Text = "Updated";
        Assert.Contains("<h1 align=\"center\">Updated Grab</h1>", MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfTheory]
    [InlineData("Before <b>bold</b> and <i>italic</i><br/>after", "Before bold and italic\nafter")]
    [InlineData("<p ALIGN='right'> A &amp; B &#60; C <strong>bold </strong>word</p>", "A & B < C bold word")]
    public void Html_BasicFormattingDecodesEntitiesAndRoundTripsEdits(string html, string expectedText)
    {
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(html, new FontFamily("Segoe UI"), 16);
        Assert.Equal(expectedText, MarkdownFlowDocumentUtilities.GetDocumentPlainText(document).Replace("\r\n", "\n", StringComparison.Ordinal));
        string saved = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);
        FlowDocument reloaded = MarkdownFlowDocumentUtilities.CreateFlowDocument(saved, new FontFamily("Segoe UI"), 16);
        Assert.Equal(MarkdownFlowDocumentUtilities.GetDocumentPlainText(document), MarkdownFlowDocumentUtilities.GetDocumentPlainText(reloaded));
        Assert.Equal(saved, MarkdownFlowDocumentUtilities.SerializeToMarkdown(reloaded));
    }

    [WpfTheory]
    [InlineData("<p><script>alert('hello')</script></p>")]
    [InlineData("<p><b>Unclosed</p>")]
    [InlineData("<div>Unsupported layout</div>")]
    public void Html_UnsupportedOrUnbalancedBlocksRemainLiteral(string html)
    {
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(html, new FontFamily("Segoe UI"), 16);
        Assert.Equal(html, MarkdownFlowDocumentUtilities.GetDocumentPlainText(document));
        Assert.Equal(html, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfFact]
    public void Html_MissingImagesPreserveOriginalTagAndShowTheFailure()
    {
        const string html = "<p><img src='missing.png' width='-1' alt='Missing'></p>";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(html, new FontFamily("Segoe UI"), 16);
        Span image = Assert.IsType<Span>(Assert.Single(Assert.IsType<Paragraph>(Assert.Single(document.Blocks)).Inlines));
        Assert.IsType<string>(image.ToolTip);
        Assert.Empty(GetImages(document.Blocks));
        Assert.Equal(html, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    private static IEnumerable<Image> GetImages(BlockCollection blocks)
    {
        foreach (Block block in blocks)
        {
            IEnumerable<Image> images = block switch
            {
                Paragraph paragraph => GetImages(paragraph.Inlines),
                System.Windows.Documents.List list => list.ListItems.SelectMany(item => GetImages(item.Blocks)),
                Table table => table.RowGroups.SelectMany(group => group.Rows)
                    .SelectMany(row => row.Cells).SelectMany(cell => GetImages(cell.Blocks)),
                _ => []
            };
            foreach (Image image in images)
                yield return image;
        }
    }

    private static IEnumerable<Image> GetImages(InlineCollection inlines)
    {
        foreach (Inline inline in inlines)
        {
            if (inline is InlineUIContainer { Child: Border { Child: Image image } })
                yield return image;
            else if (inline is Span span)
                foreach (Image child in GetImages(span.Inlines))
                    yield return child;
        }
    }

    [WpfTheory]
    [InlineData("Before ![sample](font_sample.png \"OCR sample\") after")]
    [InlineData("# ![sample](font_sample.png \"OCR sample\")")]
    [InlineData("- ![sample](font_sample.png \"OCR sample\")")]
    [InlineData("> ![sample](font_sample.png \"OCR sample\")")]
    [InlineData("**![sample](font_sample.png \"OCR sample\")**")]
    [InlineData("[![sample](font_sample.png \"OCR sample\")](https://example.com)")]
    [InlineData("| Image |\n| --- |\n| ![sample](font_sample.png \"OCR sample\") |")]
    public void Markdown_ImagesRenderAndRoundTripInContainers(string markdown)
    {
        string imagePath = Path.GetFullPath(@"Images\font_sample.png");
        string documentPath = Path.Combine(Path.GetDirectoryName(imagePath)!, "notes.md");
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16, documentPath);

        Image image = Assert.Single(GetImages(document.Blocks));
        BitmapImage bitmap = Assert.IsType<BitmapImage>(image.Source);
        Assert.Equal(imagePath, bitmap.UriSource.LocalPath);
        Assert.True(bitmap.PixelWidth > 0);
        Assert.True(bitmap.PixelHeight > 0);
        Assert.Equal("sample", AutomationProperties.GetName(image));
        Assert.Equal("OCR sample", image.ToolTip);
        Assert.Equal(Stretch.Uniform, image.Stretch);
        Assert.Equal(StretchDirection.DownOnly, image.StretchDirection);
        Assert.Equal(480, image.MaxHeight);
        Assert.Equal(640, image.MaxWidth);

        string serialized = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);
        Assert.Equal(markdown.Replace("\n", Environment.NewLine, StringComparison.Ordinal), serialized);
        Assert.Single(GetImages(MarkdownFlowDocumentUtilities.CreateFlowDocument(serialized, new FontFamily("Segoe UI"), 16, documentPath).Blocks));
    }

    [WpfFact]
    public void Markdown_ReferenceImagesKeepDestinationAltAndTitleWhenSaved()
    {
        const string markdown = "![sample][photo]\n\n[photo]: font_sample.png \"OCR sample\"";
        string documentPath = Path.GetFullPath(@"Images\notes.md");
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16, documentPath);

        Image image = Assert.Single(GetImages(document.Blocks));
        Assert.Equal("sample", AutomationProperties.GetName(image));
        Assert.Equal("OCR sample", image.ToolTip);
        string serialized = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);
        Assert.Contains("![sample](font_sample.png \"OCR sample\")", serialized);
        Assert.Single(GetImages(MarkdownFlowDocumentUtilities.CreateFlowDocument(serialized, new FontFamily("Segoe UI"), 16, documentPath).Blocks));
    }

    [WpfFact]
    public void Markdown_LocalImagesResolveEscapedParentPathsAndReleaseTheFile()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("TextGrabMarkdown-");
        string imagePath = Path.Combine(directory.FullName, "sample image.png");
        try
        {
            File.Copy(Path.GetFullPath(@"Images\font_sample.png"), imagePath);
            string documentPath = Path.Combine(directory.FullName, "notes", "document.md");
            const string markdown = "![sample](../sample%20image.png)";
            FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16, documentPath);
            BitmapImage bitmap = Assert.IsType<BitmapImage>(Assert.Single(GetImages(document.Blocks)).Source);
            Assert.Equal(imagePath, bitmap.UriSource.LocalPath);
            using FileStream exclusive = File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(BitmapCacheOption.OnLoad, bitmap.CacheOption);
            Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
        }
        finally
        {
            File.Delete(imagePath);
            directory.Delete();
        }
    }

    [WpfFact]
    public void Markdown_AbsoluteFileImagesWorkWithoutAnOpenedDocument()
    {
        string imagePath = Path.GetFullPath(@"Images\font_sample.png");
        string markdown = $"![sample]({new Uri(imagePath).AbsoluteUri})";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        Assert.Equal(imagePath, Assert.IsType<BitmapImage>(Assert.Single(GetImages(document.Blocks)).Source).UriSource.LocalPath);
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
    }

    [WpfTheory]
    [InlineData("![missing](not-present.png)", true)]
    [InlineData("![unsaved](font_sample.png)", false)]
    [InlineData("![unsupported](ftp://example.com/image.png)", true)]
    [InlineData("![unsupported](ms-settings:display)", true)]
    [InlineData("![network](file://example.invalid/share/image.png)", true)]
    [InlineData("![invalid](<http://[invalid>)", true)]
    public void Markdown_ImageFailuresRemainVisibleAndPreserveMarkdown(string markdown, bool hasDocumentPath)
    {
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16,
            hasDocumentPath ? Path.GetFullPath(@"Images\notes.md") : null);
        Assert.Empty(GetImages(document.Blocks));
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.GetDocumentPlainText(document));
        Assert.Equal(markdown, MarkdownFlowDocumentUtilities.SerializeToMarkdown(document));
        Span span = Assert.IsType<Span>(Assert.Single(Assert.IsType<Paragraph>(Assert.Single(document.Blocks)).Inlines));
        Assert.IsType<string>(span.ToolTip);
    }

    [WpfFact]
    public void Markdown_OffsetsAfterImagesStillSelectSurroundingText()
    {
        const string markdown = "before ![sample](font_sample.png) after **last**";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16, Path.GetFullPath(@"Images\notes.md"));
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);
        Assert.Single(GetImages(document.Blocks));
        foreach (string token in new[] { "before", "after", "last" })
            Assert.Equal(token, MapAndSlice(document, map, markdown.IndexOf(token, StringComparison.Ordinal), token.Length));
    }

    [WpfFact]
    public void Markdown_NonWebLinkTargetsRemainIntactForEditing()
    {
        const string markdown = """
            [file](file://example.invalid/share/document.txt)

            [protocol](ms-settings:display)

            [relative](notes.md)
            """;
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

        string serialized = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);

        Assert.Contains("[file](file://example.invalid/share/document.txt)", serialized);
        Assert.Contains("[protocol](ms-settings:display)", serialized);
        Assert.Contains("[relative](notes.md)", serialized);
    }

    [WpfFact]
    public void Markdown_RoundTrips_CommonFormatting()
    {
        const string markdown = """
# Heading

Plain **bold** text with a [link](https://example.com).

- one
- two

> quoted

```csharp
Console.WriteLine("hi");
```
""";

        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

        string serialized = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);

        Assert.Contains("# Heading", serialized);
        Assert.Contains("**bold**", serialized);
        Assert.Contains("[link](https://example.com)", serialized);
        Assert.Contains("- one", serialized);
        Assert.Contains("> quoted", serialized);
        Assert.Contains("```csharp", serialized);
        Assert.Contains("Console.WriteLine(\"hi\");", serialized);
    }

    [WpfFact]
    public void Markdown_Tables_RoundTrip_ToPipeTable()
    {
        const string markdown = """
| Name | Value |
| --- | --- |
| Alpha | 42 |
| Beta | 99 |
""";

        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

        string serialized = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);

        Assert.Contains("| Name | Value |", serialized);
        Assert.Contains("| Alpha | 42 |", serialized);
        Assert.Contains("| Beta | 99 |", serialized);
    }

    [WpfFact]
    public void Markdown_TaskLists_RoundTrip_ToCheckboxMarkers()
    {
        const string markdown = """
        - [ ] open item
        - [x] done item
        """;

        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

        string serialized = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);

        Assert.Contains("- [ ] open item", serialized);
        Assert.Contains("- [x] done item", serialized);
    }

    [WpfFact]
    public void Markdown_TaskListCheckBoxes_ToggleAndSynchronizeThroughEditor()
    {
        const string markdown = "- [ ] open item\n- [x] done item";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        RichTextBox editor = new() { Document = document, IsDocumentEnabled = true };
        string synchronized = markdown;
        int changes = 0;
        System.Windows.RoutedEventHandler sync = (_, e) =>
        {
            if (e.OriginalSource is System.Windows.DependencyObject source && MarkdownFlowDocumentUtilities.IsTaskListCheckBox(source))
            {
                synchronized = MarkdownFlowDocumentUtilities.SerializeToMarkdown(editor.Document, preserveLiteralMarkdown: true);
                changes++;
            }
        };
        editor.AddHandler(CheckBox.CheckedEvent, sync);
        editor.AddHandler(CheckBox.UncheckedEvent, sync);

        System.Windows.Documents.List list = Assert.IsType<System.Windows.Documents.List>(Assert.Single(document.Blocks));
        CheckBox[] boxes = list.ListItems.Select(item =>
        {
            Paragraph paragraph = Assert.IsType<Paragraph>(item.Blocks.FirstBlock);
            return Assert.IsType<CheckBox>(Assert.IsType<InlineUIContainer>(paragraph.Inlines.FirstInline).Child);
        }).ToArray();
        Assert.False(boxes[0].IsChecked);
        Assert.True(boxes[1].IsChecked);
        Assert.Equal("open item", AutomationProperties.GetName(boxes[0]));

        ((IToggleProvider)new CheckBoxAutomationPeer(boxes[0])).Toggle();
        ((IToggleProvider)new CheckBoxAutomationPeer(boxes[1])).Toggle();

        Assert.Equal(2, changes);
        Assert.Equal($"- [x] open item{Environment.NewLine}- [ ] done item", synchronized);
        FlowDocument reloaded = MarkdownFlowDocumentUtilities.CreateFlowDocument(synchronized, new FontFamily("Segoe UI"), 16);
        Assert.Equal(synchronized, MarkdownFlowDocumentUtilities.SerializeToMarkdown(reloaded, preserveLiteralMarkdown: true));
    }

    [WpfFact]
    public void Markdown_TaskListCheckBox_DoesNotDisplaceTextMapping()
    {
        const string markdown = "- [ ] alpha beta gamma";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

        Assert.Equal("beta", MapAndSlice(document, map, markdown.IndexOf("beta", StringComparison.Ordinal), 4));
    }

    [WpfFact]
    public void Markdown_OrderedList_RoundTripsStartNumber()
    {
        const string markdown = """
            5. fifth
            6. sixth
            """;

        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(
            markdown,
            new FontFamily("Segoe UI"),
            16);

        System.Windows.Documents.List list =
            Assert.IsType<System.Windows.Documents.List>(Assert.Single(document.Blocks));
        string serialized = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);

        Assert.Equal(5, list.StartIndex);
        Assert.Equal($"5. fifth{Environment.NewLine}6. sixth", serialized);
    }

    [WpfFact]
    public void PlainText_WithMarkdownCharacters_IsEscapedDuringSerialization()
    {
        FlowDocument document = new();
        document.Blocks.Add(new Paragraph(new Run("*literal* [value]")));

        string serialized = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document);

        Assert.Equal(@"\*literal\* \[value\]", serialized);
    }

    [WpfFact]
    public void PreserveLiteralMarkdown_KeepsTypedMarkdownSyntax()
    {
        FlowDocument document = new();
        document.Blocks.Add(new Paragraph(new Run("**bold** [link](https://example.com)")));

        string serialized = MarkdownFlowDocumentUtilities.SerializeToMarkdown(document, preserveLiteralMarkdown: true);

        Assert.Equal("**bold** [link](https://example.com)", serialized);
    }

    /// <summary>
    /// Mirrors exactly what EditTextWindow.SelectInEditor does with a Find &amp; Replace match:
    /// map the raw start and raw start+length offsets to positions independently, then read the
    /// rendered text between them.
    /// </summary>
    private static string MapAndSlice(FlowDocument document, MarkdownFlowDocumentUtilities.MarkdownOffsetMap map, int rawStart, int length)
    {
        TextPointer start = MarkdownFlowDocumentUtilities.MapRawOffsetToPosition(document, map, rawStart);
        TextPointer end = MarkdownFlowDocumentUtilities.MapRawOffsetToPosition(document, map, rawStart + length);
        return new TextRange(start, end).Text;
    }

    private static IEnumerable<(FlowDocument Document, string Source)> OriginalAndCanonicalDocuments(string markdown)
    {
        FlowDocument original = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        yield return (original, markdown);

        // Find searches this serialized source, then SelectInEditor rebuilds the rendered document.
        string canonical = MarkdownFlowDocumentUtilities.SerializeToMarkdown(original, preserveLiteralMarkdown: true);
        yield return (MarkdownFlowDocumentUtilities.CreateFlowDocument(canonical, new FontFamily("Segoe UI"), 16), canonical);
    }

    [WpfTheory]
    [InlineData("```csharp", "```", "\r\n")]
    [InlineData("```csharp", "```", "\n")]
    [InlineData("```", "```", "\r\n")]
    [InlineData("```", "```", "\n")]
    [InlineData("````csharp", "````", "\r\n")]
    [InlineData("`````", "``````", "\n")]
    [InlineData("~~~text", "~~~~~", "\r\n")]
    [InlineData("~~~~~", "~~~~~", "\n")]
    public void MapRawOffsetToPosition_UsesFencedCodeContentSpans(string openingFence, string closingFence, string newline)
    {
        string markdown = $"{openingFence}{newline}alpha beta gamma{newline}{closingFence}";

        foreach ((FlowDocument document, string source) in OriginalAndCanonicalDocuments(markdown))
        {
            MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

            Assert.Equal("alpha beta gamma", MarkdownFlowDocumentUtilities.GetDocumentPlainText(document));
            foreach (string token in new[] { "alpha", "beta", "gamma" })
                Assert.Equal(token, MapAndSlice(document, map, source.IndexOf(token, StringComparison.Ordinal), token.Length));
        }
    }

    [WpfTheory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void MapRawOffsetToPosition_MapsMultilineCodeAndBlankLinesExactly(string newline)
    {
        string code = string.Join(newline, "alpha start", "middle beta gamma", "", "last omega");
        string markdown = $"Before **context**.{newline}{newline}```csharp{newline}{code}{newline}```{newline}{newline}After **context**.";

        foreach ((FlowDocument document, string source) in OriginalAndCanonicalDocuments(markdown))
        {
            MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);
            foreach (string token in new[] { "alpha", "start", "middle", "beta", "gamma", "last", "omega" })
                Assert.Equal(token, MapAndSlice(document, map, source.IndexOf(token, StringComparison.Ordinal), token.Length));

            int start = source.IndexOf("start", StringComparison.Ordinal);
            int betaEnd = source.IndexOf("beta", StringComparison.Ordinal) + "beta".Length;
            Assert.Equal("start\nmiddle beta", MapAndSlice(document, map, start, betaEnd - start).Replace("\r\n", "\n", StringComparison.Ordinal));

            int gammaStart = source.IndexOf("gamma", StringComparison.Ordinal);
            int omegaEnd = source.IndexOf("omega", StringComparison.Ordinal) + "omega".Length;
            Assert.Equal("gamma\n\nlast omega", MapAndSlice(document, map, gammaStart, omegaEnd - gammaStart).Replace("\r\n", "\n", StringComparison.Ordinal));
        }
    }

    [WpfTheory]
    [InlineData("    alpha beta gamma\n    delta epsilon omega")]
    [InlineData("  ```csharp\n  alpha beta gamma\n  delta epsilon omega\n  ```")]
    [InlineData("> ```csharp\n> alpha beta gamma\n> delta epsilon omega\n> ```")]
    [InlineData(">     alpha beta gamma\n>     delta epsilon omega")]
    [InlineData("- ```csharp\n  alpha beta gamma\n  delta epsilon omega\n  ```")]
    [InlineData("- item\n\n      alpha beta gamma\n      delta epsilon omega")]
    public void MapRawOffsetToPosition_SkipsCodeContainerIndentation(string markdown)
    {
        foreach ((FlowDocument document, string source) in OriginalAndCanonicalDocuments(markdown))
        {
            MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);
            foreach (string token in new[] { "beta", "epsilon", "omega" })
                Assert.Equal(token, MapAndSlice(document, map, source.IndexOf(token, StringComparison.Ordinal), token.Length));

            int gammaStart = source.IndexOf("gamma", StringComparison.Ordinal);
            int epsilonEnd = source.IndexOf("epsilon", StringComparison.Ordinal) + "epsilon".Length;
            Assert.Equal("gamma\ndelta epsilon", MapAndSlice(document, map, gammaStart, epsilonEnd - gammaStart).Replace("\r\n", "\n", StringComparison.Ordinal));
        }
    }

    [WpfTheory]
    [InlineData("> ```csharp\n>\talpha beta gamma\n>\tdelta epsilon omega\n> ```")]
    [InlineData("- item\n\n  \t\talpha beta gamma\n  \t\tdelta epsilon omega")]
    public void MapRawOffsetToPosition_HandlesPartiallyExpandedCodeIndentationTabs(string markdown)
    {
        foreach ((FlowDocument document, string source) in OriginalAndCanonicalDocuments(markdown))
        {
            MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);
            foreach (string token in new[] { "beta", "epsilon", "omega" })
                Assert.Equal(token, MapAndSlice(document, map, source.IndexOf(token, StringComparison.Ordinal), token.Length));
        }
    }

    [WpfTheory]
    [InlineData("```csharp\n```", "")]
    [InlineData("```csharp\n\nalpha beta gamma\n\n\n```", "\nalpha beta gamma")]
    [InlineData("```csharp\nalpha beta gamma\n   \n```", "alpha beta gamma\n   ")]
    public void Markdown_CodeLineMappingsPreserveRenderedTextAndSerialization(string markdown, string expectedCode)
    {
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

        Assert.Equal(expectedCode, MarkdownFlowDocumentUtilities.GetDocumentPlainText(document));
        Assert.Equal($"```csharp{Environment.NewLine}{expectedCode}{Environment.NewLine}```",
            MarkdownFlowDocumentUtilities.SerializeToMarkdown(document, preserveLiteralMarkdown: true));
    }

    [WpfFact]
    public void MapRawOffsetToPosition_SkipsStrippedBoldMarkers()
    {
        const string markdown = "Plain **bold** text with a [link](https://example.com).";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

        int rawIndex = markdown.IndexOf("bold", StringComparison.Ordinal);

        Assert.Equal("bold", MapAndSlice(document, map, rawIndex, 4));
    }

    [WpfFact]
    public void MapRawOffsetToPosition_SkipsLinkBracketsAndUrl()
    {
        const string markdown = "Plain **bold** text with a [link](https://example.com).";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

        int rawIndex = markdown.IndexOf("link", StringComparison.Ordinal);

        Assert.Equal("link", MapAndSlice(document, map, rawIndex, 4));
    }

    [WpfFact]
    public void MapRawOffsetToPosition_SkipsHeadingHashPrefix()
    {
        const string markdown = "# My Heading Title";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

        int rawIndex = markdown.IndexOf("Heading", StringComparison.Ordinal);

        Assert.Equal("Heading", MapAndSlice(document, map, rawIndex, 7));
    }

    [WpfFact]
    public void MapRawOffsetToPosition_SkipsListMarkers()
    {
        const string markdown = "- first item\n- second item\n- third item";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

        int rawIndex = markdown.IndexOf("third", StringComparison.Ordinal);

        // A list item's bullet marker is a documented, narrow exception (see GetLocalTextPointer's
        // remarks): no WPF insertion position exists that sits "just past the marker" without also
        // having consumed the item's first real character, so a match starting at the very first
        // character of a list item's content ends up selecting the marker glyph too. The word itself
        // still resolves exactly — this is the one place a couple of extra, harmless characters
        // (the bullet + tab) get selected alongside it.
        Assert.EndsWith("third", MapAndSlice(document, map, rawIndex, 5));
    }

    [WpfFact]
    public void MapRawOffsetToPosition_DoesNotDriftIntoWrongParagraph()
    {
        const string markdown = """
            First paragraph has some words in it.

            Second paragraph also has some words in it.

            Third paragraph has the target word right here.
            """;
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

        int rawIndex = markdown.IndexOf("target", StringComparison.Ordinal);

        Assert.Equal("target", MapAndSlice(document, map, rawIndex, 6));
    }

    [WpfFact]
    public void MapRawOffsetToPosition_HandlesCodeSpanBackticks()
    {
        const string markdown = "Run `dotnet build` to compile the project.";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

        int rawIndex = markdown.IndexOf("dotnet", StringComparison.Ordinal);

        Assert.Equal("dotnet", MapAndSlice(document, map, rawIndex, 6));
    }

    [WpfFact]
    public void MapRawOffsetToPosition_HandlesBoldNestedInsideLinkText()
    {
        const string markdown = "See the [**important** notes](https://example.com) page.";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

        int rawIndex = markdown.IndexOf("important", StringComparison.Ordinal);

        Assert.Equal("important", MapAndSlice(document, map, rawIndex, 9));
    }

    [WpfFact]
    public void MapRawOffsetToPosition_HandlesTextInsideTableCell()
    {
        // Regression test: TextRange.Text does not reliably count characters when a range starts
        // outside a Table and ends inside one of its cells — every position inside a given table
        // row measured that way collapsed to the same offset (the row's end). Offsets here must be
        // resolved relative to the containing cell's own paragraph, never the document.
        const string markdown = """
            | Name | Value |
            | --- | --- |
            | Alpha | fortytwo |
            """;
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

        int rawIndex = markdown.IndexOf("fortytwo", StringComparison.Ordinal);

        Assert.Equal("fortytwo", MapAndSlice(document, map, rawIndex, 8));
    }

    [WpfFact]
    public void MapRawOffsetToPosition_HandlesTextInEarlierTableCellOnSameRow()
    {
        const string markdown = """
            | Name | Value |
            | --- | --- |
            | Alpha | fortytwo |
            """;
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

        int rawIndex = markdown.IndexOf("Alpha", StringComparison.Ordinal);

        Assert.Equal("Alpha", MapAndSlice(document, map, rawIndex, 5));
    }

    [WpfFact]
    public void MapRawOffsetToPosition_MapsBothEndsOfAMatchToTheExactRenderedSubstring()
    {
        const string markdown = "Plain text with a target word right here.";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);
        MarkdownFlowDocumentUtilities.MarkdownOffsetMap map = MarkdownFlowDocumentUtilities.BuildOffsetMap(document);

        int rawIndex = markdown.IndexOf("target word", StringComparison.Ordinal);

        Assert.Equal("target word", MapAndSlice(document, map, rawIndex, 11));
    }
}
