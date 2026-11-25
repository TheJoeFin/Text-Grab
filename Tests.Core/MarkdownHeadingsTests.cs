using Text_Grab.Core.Editing;

namespace Text_Grab.Tests.Core;

/// <summary>What the table of contents pane lists for a document, and when the contents button appears.</summary>
public class MarkdownHeadingsTests
{
    private static string[] Titles(string? markdown) => [.. MarkdownHeadings.Parse(markdown).Select(heading => heading.Title)];

    private static int[] Levels(string? markdown) => [.. MarkdownHeadings.Parse(markdown).Select(heading => heading.Level)];

    // ---- What is a heading ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Just some text\nand more text")]
    public void NoHeadings_ParsesToNothingAndHasNone(string? markdown)
    {
        Assert.Empty(MarkdownHeadings.Parse(markdown));
        Assert.False(MarkdownHeadings.HasAny(markdown));
    }

    [Fact]
    public void EveryLevelFromOneToSixIsAHeading()
    {
        const string Document = "# One\n## Two\n### Three\n#### Four\n##### Five\n###### Six\n####### Seven";

        Assert.Equal([1, 2, 3, 4, 5, 6], Levels(Document));
        Assert.Equal(["One", "Two", "Three", "Four", "Five", "Six"], Titles(Document));
    }

    [Theory]
    [InlineData("#tag")]
    [InlineData("#hashtag and more")]
    [InlineData("#")]
    [InlineData("##")]
    [InlineData("# ")]
    [InlineData("    # indented four spaces is code")]
    [InlineData("\t# tab indented is code")]
    [InlineData("text # not at the start")]
    public void WhatLooksLikeAHeadingButIsNot_IsSkipped(string line)
        => Assert.False(MarkdownHeadings.HasAny(line));

    [Fact]
    public void UpToThreeSpacesOfIndentAreAllowed()
        => Assert.Equal(["a", "b", "c"], Titles("# a\n # b\n   # c\n    # d"));

    [Fact]
    public void ATabAfterTheHashesSeparatesTheTitle()
        => Assert.Equal(["Tabbed"], Titles("#\tTabbed"));

    [Fact]
    public void AClosingRunOfHashesIsNotPartOfTheTitle()
    {
        Assert.Equal(["Title"], Titles("## Title ##"));
        Assert.Equal(["Title"], Titles("## Title ####   "));
        Assert.Equal(["C#"], Titles("# C#"));
        Assert.Equal(["Issue #12"], Titles("# Issue #12"));
    }

    [Fact]
    public void ABlankTitleIsNotListed()
        => Assert.Empty(MarkdownHeadings.Parse("# \n## ##\n###   "));

    // ---- What a title reads as ----

    [Theory]
    [InlineData("# **Bold** and *italic*", "Bold and italic")]
    [InlineData("# Use `code` here", "Use code here")]
    [InlineData("# A [link](https://example.com/page) inside", "A link inside")]
    [InlineData("# An ![image](a.png) too", "An image too")]
    [InlineData("# ~~Gone~~ now", "Gone now")]
    [InlineData("# Number \\#1", "Number #1")]
    [InlineData("# snake_case_name", "snake_case_name")]
    [InlineData("#    Spaced    out   ", "Spaced out")]
    public void TheTitleIsWithoutInlineMarkup(string line, string expected)
        => Assert.Equal([expected], Titles(line));

    // ---- Where headings are not ----

    [Fact]
    public void AHashLineInAFencedCodeBlockIsNotAHeading()
    {
        const string Document = "# Real\n```bash\n# a comment\n## another\n```\n## After";

        Assert.Equal(["Real", "After"], Titles(Document));
    }

    [Fact]
    public void AFenceEndsOnlyWithTheSameMarkAtLeastAsLong()
    {
        const string Document = "````\n```\n# still code\n```\n# also code\n````\n# Out";

        Assert.Equal(["Out"], Titles(Document));
    }

    [Fact]
    public void ATildeFenceIsNotClosedByBackticks()
    {
        const string Document = "~~~\n# in\n```\n# still in\n~~~\n# out";

        Assert.Equal(["out"], Titles(Document));
    }

    [Fact]
    public void AFenceThatIsNeverClosedRunsToTheEnd()
        => Assert.Empty(MarkdownHeadings.Parse("```\n# not a heading\n## nor this"));

    [Fact]
    public void AnInlineTripleBacktickSpanDoesNotOpenAFence()
        => Assert.Equal(["After"], Titles("```code``` on a line\n# After"));

    [Fact]
    public void YamlFrontMatterIsSkipped()
    {
        const string Document = "---\ntitle: x\n# a yaml comment\n---\n# Heading\n";

        Assert.Equal(["Heading"], Titles(Document));
    }

    [Fact]
    public void ALoneRuleOnTheFirstLineIsNotFrontMatter()
        => Assert.Equal(["Heading"], Titles("---\n# Heading"));

    // ---- Offsets ----

    [Fact]
    public void AnOffsetIsWhereTheHeadingsLineStarts()
    {
        const string Document = "Intro\n\n# One\ntext\n  ## Two\n";

        IReadOnlyList<MarkdownHeading> headings = MarkdownHeadings.Parse(Document);

        Assert.Equal([Document.IndexOf("# One", StringComparison.Ordinal), Document.IndexOf("  ## Two", StringComparison.Ordinal)], headings.Select(heading => heading.Offset));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void EveryLineEndingCountsAsTheCharactersItIsMadeOf(string eol)
    {
        string document = $"Intro{eol}# One{eol}text{eol}## Two";

        IReadOnlyList<MarkdownHeading> headings = MarkdownHeadings.Parse(document);

        Assert.Equal(["One", "Two"], headings.Select(heading => heading.Title));
        Assert.All(headings, heading => Assert.Equal('#', document[heading.Offset]));
    }

    // ---- HasAny ----

    [Fact]
    public void HasAny_IsTrueForTheFirstHeadingAnywhere()
    {
        Assert.True(MarkdownHeadings.HasAny("text\n\ntext\n## Late"));
        Assert.True(MarkdownHeadings.HasAny("# Early\ntext"));
    }

    [Fact]
    public void HasAny_IsFalseWhenEveryHashLineIsInCode()
        => Assert.False(MarkdownHeadings.HasAny("```\n# not\n```\n#tag"));
}

/// <summary>How the table of contents tree nests the headings.</summary>
public class MarkdownOutlineTests
{
    private static IReadOnlyList<MarkdownOutlineNode> Outline(string markdown)
        => MarkdownOutline.Build(MarkdownHeadings.Parse(markdown));

    private static string Describe(IEnumerable<MarkdownOutlineNode> nodes)
        => string.Join(",", nodes.Select(node => node.Children.Count == 0 ? node.Title : $"{node.Title}({Describe(node.Children)})"));

    [Fact]
    public void NoHeadings_NoTree()
        => Assert.Empty(MarkdownOutline.Build([]));

    [Fact]
    public void HeadingsNestUnderTheNearestShallowerOne()
        => Assert.Equal("A(B(C),D),E", Describe(Outline("# A\n## B\n### C\n## D\n# E")));

    [Fact]
    public void ASkippedLevelStillNestsUnderTheHeadingAbove()
        => Assert.Equal("A(C),D", Describe(Outline("# A\n### C\n# D")));

    [Fact]
    public void ADocumentThatStartsDeepKeepsThoseHeadingsAtTheTop()
        => Assert.Equal("Deep,Shallow(Deeper)", Describe(Outline("### Deep\n# Shallow\n## Deeper")));

    [Fact]
    public void EqualLevelsAreSiblings()
        => Assert.Equal("A,B,C", Describe(Outline("## A\n## B\n## C")));

    [Fact]
    public void EachNodeKnowsItsPositionInTheFlatList()
    {
        IReadOnlyList<MarkdownOutlineNode> roots = Outline("# A\n## B\n# C\n## D");

        Assert.Equal([0, 2], roots.Select(node => node.Index));
        Assert.Equal(1, roots[0].Children[0].Index);
        Assert.Equal(3, roots[1].Children[0].Index);
    }

    [Fact]
    public void ShapeIgnoresWhereTheHeadingsAre_ButNotTheirLevelsOrTitles()
    {
        IReadOnlyList<MarkdownHeading> before = MarkdownHeadings.Parse("# A\n## B");

        Assert.True(MarkdownOutline.HaveSameShape(before, MarkdownHeadings.Parse("text\n# A\nmore text\n## B")));
        Assert.False(MarkdownOutline.HaveSameShape(before, MarkdownHeadings.Parse("# A\n### B")));
        Assert.False(MarkdownOutline.HaveSameShape(before, MarkdownHeadings.Parse("# A\n## C")));
        Assert.False(MarkdownOutline.HaveSameShape(before, MarkdownHeadings.Parse("# A")));
    }
}
