using Text_Grab.Controls;
using Text_Grab.Models;

namespace Tests;

public class FindAndReplaceWindowTests
{
    [Fact]
    public void GetMatchTextForEditing_PreservesRawWhitespace()
    {
        List<FindResult> results =
        [
            new() { Text = "word·word", RawText = "word word" },
            new() { Text = "line⏎break", RawText = $"line{Environment.NewLine}break" },
            new() { Text = "tab⇥value", RawText = "tab\tvalue" },
        ];

        string editText = FindAndReplaceWindow.GetMatchTextForEditing(results);

        Assert.Equal(
            $"word word{Environment.NewLine}line{Environment.NewLine}break{Environment.NewLine}tab\tvalue",
            editText);
    }

    [Theory]
    [InlineData("old value", "new value", false, "new value")]
    [InlineData("old value", "new value", true, "old value")]
    [InlineData("cached", null, false, "cached")]
    public void ResolveSearchSourceText_UsesCurrentEditorTextOutsideSpreadsheetMode(
        string cachedText,
        string? editorText,
        bool isSpreadsheetSearch,
        string expected)
    {
        Assert.Equal(
            expected,
            FindAndReplaceWindow.ResolveSearchSourceText(cachedText, editorText, isSpreadsheetSearch));
    }
}
