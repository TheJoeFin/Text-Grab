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

        FlowDocument document = MarkdownDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

        string serialized = MarkdownDocumentUtilities.SerializeToMarkdown(document);

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

        FlowDocument document = MarkdownDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

        string serialized = MarkdownDocumentUtilities.SerializeToMarkdown(document);

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

        FlowDocument document = MarkdownDocumentUtilities.CreateFlowDocument(markdown, new FontFamily("Segoe UI"), 16);

        string serialized = MarkdownDocumentUtilities.SerializeToMarkdown(document);

        Assert.Contains("- [ ] open item", serialized);
        Assert.Contains("- [x] done item", serialized);
    }

    [WpfFact]
    public void PlainText_WithMarkdownCharacters_IsEscapedDuringSerialization()
    {
        FlowDocument document = new();
        document.Blocks.Add(new Paragraph(new Run("*literal* [value]")));

        string serialized = MarkdownDocumentUtilities.SerializeToMarkdown(document);

        Assert.Equal(@"\*literal\* \[value\]", serialized);
    }

    [WpfFact]
    public void PreserveLiteralMarkdown_KeepsTypedMarkdownSyntax()
    {
        FlowDocument document = new();
        document.Blocks.Add(new Paragraph(new Run("**bold** [link](https://example.com)")));

        string serialized = MarkdownDocumentUtilities.SerializeToMarkdown(document, preserveLiteralMarkdown: true);

        Assert.Equal("**bold** [link](https://example.com)", serialized);
    }

    [Theory]
    [InlineData("#")]
    [InlineData("##")]
    [InlineData(">")]
    [InlineData("  >")]
    [InlineData("-")]
    [InlineData("1.")]
    public void LiveBlockTriggerMarkers_AreRecognized(string marker)
    {
        Assert.True(MarkdownDocumentUtilities.ShouldPromoteLiveBlock(marker));
    }

    [Theory]
    [InlineData("text")]
    [InlineData("hello # world")]
    [InlineData("1.2")]
    public void NonTriggerText_DoesNotPromoteLiveBlock(string text)
    {
        Assert.False(MarkdownDocumentUtilities.ShouldPromoteLiveBlock(text));
    }

    [Theory]
    [InlineData("**bold**")]
    [InlineData("`code`")]
    [InlineData("[link](https://example.com)")]
    [InlineData("[ ] task")]
    [InlineData("[x] done")]
    public void CompletedMarkdownSyntax_PromotesLiveParsing(string text)
    {
        Assert.True(MarkdownDocumentUtilities.ShouldPromoteLiveMarkdown(text));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("[link]")]
    [InlineData("plain text")]
    [InlineData("2026.04 release notes")]
    public void IncompleteMarkdownSyntax_DoesNotPromoteLiveParsing(string text)
    {
        Assert.False(MarkdownDocumentUtilities.ShouldPromoteLiveMarkdown(text));
    }

    [Theory]
    [InlineData("# Heading")]
    [InlineData("> quote")]
    [InlineData("- item")]
    [InlineData("1. item")]
    [InlineData("[link](https://example.com)")]
    [InlineData("```csharp\nConsole.WriteLine(\"hi\");\n```")]
    public void MarkdownLikeText_IsDetectedForPasteParsing(string text)
    {
        Assert.True(MarkdownDocumentUtilities.LooksLikeMarkdown(text));
    }

    [Theory]
    [InlineData("Just a normal sentence.")]
    [InlineData("2026.04 release notes")]
    [InlineData("email me at joe@example.com")]
    public void PlainText_IsNotDetectedAsMarkdown(string text)
    {
        Assert.False(MarkdownDocumentUtilities.LooksLikeMarkdown(text));
    }
}
