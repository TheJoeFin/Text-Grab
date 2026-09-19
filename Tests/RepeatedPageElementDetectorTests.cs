using System.Collections.Generic;
using System.Drawing;
using Text_Grab.Utilities;

namespace Tests;

public class RepeatedPageElementDetectorTests
{
    private static readonly SizeF LetterPage = new(850, 1100);

    [Fact]
    public void SinglePage_NothingIsIgnored()
    {
        PageTextSnapshot page = Page(
            Word("Acme", 60, 40),
            Word("Corp", 110, 40),
            Word("Body", 60, 200));

        List<HashSet<int>> ignored = RepeatedPageElementDetector.FindRepeatedHeaderFooterElements([page]);

        Assert.Single(ignored);
        Assert.Empty(ignored[0]);
    }

    [Fact]
    public void RunningHeaderAtSamePlaceOnEveryPage_IsIgnoredOnEveryPage()
    {
        PageTextSnapshot page1 = Page(
            Word("Acme", 60, 40),
            Word("Corp", 110, 40),
            Word("Revenue", 60, 200),
            Word("grew", 130, 200));
        PageTextSnapshot page2 = Page(
            Word("Acme", 61, 40),
            Word("Corp", 111, 41),
            Word("Costs", 60, 200),
            Word("fell", 130, 200));

        List<HashSet<int>> ignored = RepeatedPageElementDetector.FindRepeatedHeaderFooterElements([page1, page2]);

        Assert.Equal(new HashSet<int> { 0, 1 }, ignored[0]);
        Assert.Equal(new HashSet<int> { 0, 1 }, ignored[1]);
    }

    [Fact]
    public void IncrementingPageNumbersInFooter_AreIgnored()
    {
        PageTextSnapshot page1 = Page(
            Word("Intro", 60, 300),
            Word("Page", 380, 1050),
            Word("1", 425, 1050),
            Word("of", 445, 1050),
            Word("12", 470, 1050));
        PageTextSnapshot page2 = Page(
            Word("More", 60, 300),
            Word("Page", 380, 1050),
            Word("2", 425, 1050),
            Word("of", 445, 1050),
            Word("12", 470, 1050));
        PageTextSnapshot page3 = Page(
            Word("End", 60, 300),
            Word("Page", 380, 1050),
            Word("10", 423, 1050),
            Word("of", 445, 1050),
            Word("12", 470, 1050));

        List<HashSet<int>> ignored = RepeatedPageElementDetector.FindRepeatedHeaderFooterElements([page1, page2, page3]);

        foreach (HashSet<int> pageIgnored in ignored)
            Assert.Equal(new HashSet<int> { 1, 2, 3, 4 }, pageIgnored);
    }

    [Fact]
    public void RepeatedTableColumnHeaderDirectlyAboveRows_IsKept()
    {
        // Column headers at the top of every page with data rows immediately below them —
        // the same position and text as a running header, but attached to the body.
        PageTextSnapshot page1 = Page(
            Word("Name", 60, 100),
            Word("Qty", 300, 100),
            Word("Widget", 60, 118),
            Word("4", 300, 118));
        PageTextSnapshot page2 = Page(
            Word("Name", 60, 100),
            Word("Qty", 300, 100),
            Word("Gadget", 60, 118),
            Word("7", 300, 118));

        List<HashSet<int>> ignored = RepeatedPageElementDetector.FindRepeatedHeaderFooterElements([page1, page2]);

        Assert.Empty(ignored[0]);
        Assert.Empty(ignored[1]);
    }

    [Fact]
    public void AllNumericFirstRowUnderRepeatedHeader_IsKeptViaTheRowBelow()
    {
        // Header row and an all-numeric first row both "repeat" (digits normalize to #); the
        // second row has a differing cell, which anchors the row above it, then the header.
        PageTextSnapshot page1 = Page(
            Word("Name", 60, 100), Word("Qty", 300, 100),
            Word("1", 60, 118), Word("40", 300, 118),
            Word("Widget", 60, 136), Word("4", 300, 136));
        PageTextSnapshot page2 = Page(
            Word("Name", 60, 100), Word("Qty", 300, 100),
            Word("2", 60, 118), Word("75", 300, 118),
            Word("Gadget", 60, 136), Word("7", 300, 136));

        List<HashSet<int>> ignored = RepeatedPageElementDetector.FindRepeatedHeaderFooterElements([page1, page2]);

        Assert.Empty(ignored[0]);
        Assert.Empty(ignored[1]);
    }

    [Fact]
    public void RepeatedTextInPageBody_IsNotTreatedAsHeader()
    {
        PageTextSnapshot page1 = Page(Word("Total", 60, 550), Word("Body", 60, 300));
        PageTextSnapshot page2 = Page(Word("Total", 60, 550), Word("Other", 60, 300));

        List<HashSet<int>> ignored = RepeatedPageElementDetector.FindRepeatedHeaderFooterElements([page1, page2]);

        Assert.Empty(ignored[0]);
        Assert.Empty(ignored[1]);
    }

    [Fact]
    public void DifferentHeaderTextAtSamePlace_IsKept()
    {
        PageTextSnapshot page1 = Page(Word("Chapter One", 60, 40, width: 120), Word("Body", 60, 300));
        PageTextSnapshot page2 = Page(Word("Appendix B", 60, 40, width: 120), Word("More", 60, 300));

        List<HashSet<int>> ignored = RepeatedPageElementDetector.FindRepeatedHeaderFooterElements([page1, page2]);

        Assert.Empty(ignored[0]);
        Assert.Empty(ignored[1]);
    }

    [Fact]
    public void SameHeaderTextAtDifferentPlace_IsKept()
    {
        PageTextSnapshot page1 = Page(Word("Acme", 60, 40), Word("Body", 60, 300));
        PageTextSnapshot page2 = Page(Word("Acme", 600, 40), Word("More", 60, 300));

        List<HashSet<int>> ignored = RepeatedPageElementDetector.FindRepeatedHeaderFooterElements([page1, page2]);

        Assert.Empty(ignored[0]);
        Assert.Empty(ignored[1]);
    }

    [Fact]
    public void OcrNoiseInRepeatedHeader_StillMatches()
    {
        PageTextSnapshot page1 = Page(Word("Confidential", 60, 40, width: 110), Word("Body", 60, 300));
        PageTextSnapshot page2 = Page(Word("Confidentia1", 60, 40, width: 110), Word("More", 60, 300));

        List<HashSet<int>> ignored = RepeatedPageElementDetector.FindRepeatedHeaderFooterElements([page1, page2]);

        Assert.Equal(new HashSet<int> { 0 }, ignored[0]);
        Assert.Equal(new HashSet<int> { 0 }, ignored[1]);
    }

    [Fact]
    public void HeaderMissingFromSomePages_IsStillIgnoredWhereItAppears()
    {
        // Five pages; the header is on pages 1, 2 and 4 only — each occurrence matches two of
        // the other four pages, clearing the one-third threshold.
        PageTextSnapshot withHeader1 = Page(Word("Draft", 60, 40), Word("A", 60, 300));
        PageTextSnapshot withHeader2 = Page(Word("Draft", 60, 40), Word("B", 60, 300));
        PageTextSnapshot without3 = Page(Word("C", 60, 300));
        PageTextSnapshot withHeader4 = Page(Word("Draft", 60, 40), Word("D", 60, 300));
        PageTextSnapshot without5 = Page(Word("E", 60, 300));

        List<HashSet<int>> ignored = RepeatedPageElementDetector.FindRepeatedHeaderFooterElements(
            [withHeader1, withHeader2, without3, withHeader4, without5]);

        Assert.Equal(new HashSet<int> { 0 }, ignored[0]);
        Assert.Equal(new HashSet<int> { 0 }, ignored[1]);
        Assert.Empty(ignored[2]);
        Assert.Equal(new HashSet<int> { 0 }, ignored[3]);
        Assert.Empty(ignored[4]);
    }

    [Theory]
    [InlineData("Page 3 of 12", "PAGE # OF #")]
    [InlineData("  Acme   Corp ", "ACME CORP")]
    [InlineData("2026-09-13", "#-#-#")]
    [InlineData("", "")]
    public void NormalizeText_CollapsesCaseWhitespaceAndDigits(string input, string expected)
    {
        Assert.Equal(expected, RepeatedPageElementDetector.NormalizeText(input));
    }

    private static PageTextSnapshot Page(params PageTextElement[] elements) => new(elements, LetterPage);

    private static PageTextElement Word(string text, float left, float top, float width = 40, float height = 12)
        => new(text, new RectangleF(left, top, width, height));
}
