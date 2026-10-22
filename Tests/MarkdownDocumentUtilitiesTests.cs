using System.Windows.Documents;
using System.Windows.Media;
using Text_Grab.Utilities;

namespace Tests;

public class MarkdownDocumentUtilitiesTests
{
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
