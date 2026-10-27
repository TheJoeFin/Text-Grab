using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

public class MarkdownParsingTests
{
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
