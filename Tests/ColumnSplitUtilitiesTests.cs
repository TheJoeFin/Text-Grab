using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

public class ColumnSplitUtilitiesTests
{
    [Fact]
    public void SplitCell_Delimiter_SplitsOnLiteralString()
    {
        // Given
        SplitColumnOptions options = new() { Mode = SplitMode.Delimiter, DelimiterText = " " };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("John Smith", options);

        // Then
        Assert.Equal(["John", "Smith"], parts);
    }

    [Fact]
    public void SplitCell_Delimiter_MultiCharacterDelimiter()
    {
        // Given
        SplitColumnOptions options = new() { Mode = SplitMode.Delimiter, DelimiterText = ", " };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("a, b, c", options);

        // Then
        Assert.Equal(["a", "b", "c"], parts);
    }

    [Fact]
    public void SplitCell_Delimiter_EmptyDelimiterReturnsWholeValue()
    {
        // Given
        SplitColumnOptions options = new() { Mode = SplitMode.Delimiter, DelimiterText = "" };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("John Smith", options);

        // Then
        Assert.Equal(["John Smith"], parts);
    }

    [Fact]
    public void SplitCell_Regex_SplitsOnPattern()
    {
        // Given
        SplitColumnOptions options = new() { Mode = SplitMode.Regex, Pattern = @"\s*-\s*" };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("ABC - 123 - XY", options);

        // Then
        Assert.Equal(["ABC", "123", "XY"], parts);
    }

    [Fact]
    public void SplitCell_Regex_InvalidPatternReturnsWholeValue()
    {
        // Given - an unbalanced group is an invalid regex
        SplitColumnOptions options = new() { Mode = SplitMode.Regex, Pattern = "(" };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("anything", options);

        // Then
        Assert.Equal(["anything"], parts);
    }

    [Fact]
    public void SplitCell_Regex_IgnoreCaseSplitsOnLetterRegardlessOfCase()
    {
        // Given
        SplitColumnOptions options = new() { Mode = SplitMode.Regex, Pattern = "x", IgnoreCase = true };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("aXbxc", options);

        // Then
        Assert.Equal(["a", "b", "c"], parts);
    }

    [Fact]
    public void SplitCell_FixedLength_SplitsFromStart()
    {
        // Given
        SplitColumnOptions options = new() { Mode = SplitMode.FixedLength, Length = 3 };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("ABC12345", options);

        // Then
        Assert.Equal(["ABC", "12345"], parts);
    }

    [Fact]
    public void SplitCell_FixedLength_SplitsFromEnd()
    {
        // Given
        SplitColumnOptions options = new() { Mode = SplitMode.FixedLength, Length = 3, SplitFromEnd = true };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("ABC12345", options);

        // Then
        Assert.Equal(["ABC12", "345"], parts);
    }

    [Fact]
    public void SplitCell_FixedLength_LengthBeyondValueClampsToWhole()
    {
        // Given
        SplitColumnOptions options = new() { Mode = SplitMode.FixedLength, Length = 100 };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("short", options);

        // Then
        Assert.Equal(["short", ""], parts);
    }

    [Fact]
    public void SplitCell_Delimiter_KeepLeft_AttachesSplitterToLeftPart()
    {
        // Given
        SplitColumnOptions options = new()
        {
            Mode = SplitMode.Delimiter,
            DelimiterText = ".",
            SplitterHandling = SplitterHandling.KeepLeft,
        };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("20.30", options);

        // Then
        Assert.Equal(["20.", "30"], parts);
    }

    [Fact]
    public void SplitCell_Delimiter_KeepRight_AttachesSplitterToRightPart()
    {
        // Given
        SplitColumnOptions options = new()
        {
            Mode = SplitMode.Delimiter,
            DelimiterText = ".",
            SplitterHandling = SplitterHandling.KeepRight,
        };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("20.30", options);

        // Then
        Assert.Equal(["20", ".30"], parts);
    }

    [Fact]
    public void SplitCell_Delimiter_KeepLeft_MultipleSplitters()
    {
        // Given
        SplitColumnOptions options = new()
        {
            Mode = SplitMode.Delimiter,
            DelimiterText = ".",
            SplitterHandling = SplitterHandling.KeepLeft,
        };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("a.b.c", options);

        // Then
        Assert.Equal(["a.", "b.", "c"], parts);
    }

    [Fact]
    public void SplitCell_Regex_KeepRight_AttachesMatchToRightPart()
    {
        // Given
        SplitColumnOptions options = new()
        {
            Mode = SplitMode.Regex,
            Pattern = "-",
            SplitterHandling = SplitterHandling.KeepRight,
        };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("a-b-c", options);

        // Then
        Assert.Equal(["a", "-b", "-c"], parts);
    }

    [Fact]
    public void SplitCell_PatternItem_SavedRegex_SplitsOnMatchedSpans()
    {
        // Given a saved regex used as the delimiter
        PatternItem hexPattern = new(new StoredRegex("Hex", @"#[0-9a-fA-F]{6}"));
        SplitColumnOptions options = new() { Mode = SplitMode.Regex, PatternItem = hexPattern };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("a #FFFFFF b #000000 c", options);

        // Then - matched spans are removed, leaving the gaps between them
        Assert.Equal(["a ", " b ", " c"], parts);
    }

    [Fact]
    public void SplitCell_PatternItem_NoMatchReturnsWholeValue()
    {
        // Given
        PatternItem hexPattern = new(new StoredRegex("Hex", @"#[0-9a-fA-F]{6}"));
        SplitColumnOptions options = new() { Mode = SplitMode.Regex, PatternItem = hexPattern };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("no colors here", options);

        // Then
        Assert.Equal(["no colors here"], parts);
    }

    [Fact]
    public void SplitCell_PatternItem_TakesPrecedenceOverRawPattern()
    {
        // Given a PatternItem plus a conflicting raw Pattern - the PatternItem should win
        PatternItem hexPattern = new(new StoredRegex("Hex", @"#[0-9a-fA-F]{6}"));
        SplitColumnOptions options = new() { Mode = SplitMode.Regex, PatternItem = hexPattern, Pattern = " " };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell("a #FFFFFF b", options);

        // Then - split on the hex color, not on spaces
        Assert.Equal(["a ", " b"], parts);
    }

    [Fact]
    public void SplitCell_NullValueTreatedAsEmpty()
    {
        // Given
        SplitColumnOptions options = new() { Mode = SplitMode.Delimiter, DelimiterText = "," };

        // When
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell(null!, options);

        // Then
        Assert.Equal([""], parts);
    }
}
