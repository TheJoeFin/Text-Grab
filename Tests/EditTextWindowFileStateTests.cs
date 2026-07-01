using Text_Grab;
using Text_Grab.Models;

namespace Tests;

public class EditTextWindowFileStateTests
{
    [Theory]
    [InlineData(null, false, "Edit Text")]
    [InlineData("", true, "Edit Text")]
    [InlineData(@"C:\Temp\notes.md", false, "Edit Text | notes.md")]
    [InlineData(@"C:\Temp\notes.md", true, "Edit Text | *notes.md")]
    public void GetWindowTitle_ReflectsTrackedFileAndPendingEdits(string? path, bool hasPendingEdits, string expectedTitle)
    {
        Assert.Equal(expectedTitle, EditTextWindow.GetWindowTitle(path, hasPendingEdits));
    }

    [Theory]
    [InlineData(null, "saved", "changed", false)]
    [InlineData("", "saved", "changed", false)]
    [InlineData(@"C:\Temp\notes.md", "same", "same", false)]
    [InlineData(@"C:\Temp\notes.md", "same", "changed", true)]
    public void ShouldShowPendingFileEdits_RequiresTrackedFileAndChangedText(string? path, string savedText, string currentText, bool expected)
    {
        Assert.Equal(expected, EditTextWindow.ShouldShowPendingFileEdits(path, savedText, currentText));
    }

    [Theory]
    [InlineData(null, EtwEditorMode.Text, null, null, ".txt")]
    [InlineData(null, EtwEditorMode.Markdown, null, null, ".md")]
    [InlineData(null, EtwEditorMode.Spreadsheet, null, null, ".tsv")]
    [InlineData(null, EtwEditorMode.Spreadsheet, EtwStructuredTextFormat.Csv, ",", ".csv")]
    [InlineData(null, EtwEditorMode.Spreadsheet, EtwStructuredTextFormat.Tsv, "\t", ".tsv")]
    [InlineData(null, EtwEditorMode.Spreadsheet, EtwStructuredTextFormat.DelimitedText, ",", ".csv")]
    [InlineData(null, EtwEditorMode.Spreadsheet, EtwStructuredTextFormat.DelimitedText, "|", ".tsv")]
    [InlineData(@"C:\Temp\notes.markdown", EtwEditorMode.Text, null, null, ".markdown")]
    [InlineData(@"C:\Temp\data.json", EtwEditorMode.Markdown, null, null, ".json")]
    public void GetDefaultSaveExtension_MatchesEditorMode(
        string? openedFilePath,
        EtwEditorMode editorMode,
        EtwStructuredTextFormat? format,
        string? delimiter,
        string expectedExtension)
    {
        EditTextTableDocument? tableDocument = format.HasValue
            ? new EditTextTableDocument
            {
                Format = format.Value,
                Delimiter = delimiter ?? "\t"
            }
            : null;

        Assert.Equal(expectedExtension, EditTextWindow.GetDefaultSaveExtension(openedFilePath, editorMode, tableDocument));
    }

    [Theory]
    [InlineData(null, EtwEditorMode.Spreadsheet, 1)]
    [InlineData(null, EtwEditorMode.Markdown, 2)]
    [InlineData(null, EtwEditorMode.Text, 3)]
    [InlineData(@"C:\Temp\sheet.csv", EtwEditorMode.Markdown, 1)]
    [InlineData(@"C:\Temp\notes.md", EtwEditorMode.Text, 2)]
    [InlineData(@"C:\Temp\notes.txt", EtwEditorMode.Markdown, 3)]
    [InlineData(@"C:\Temp\data.json", EtwEditorMode.Text, 4)]
    public void GetSaveDocumentFilterIndex_MatchesEditorMode(string? openedFilePath, EtwEditorMode editorMode, int expectedFilterIndex)
    {
        Assert.Equal(expectedFilterIndex, EditTextWindow.GetSaveDocumentFilterIndex(openedFilePath, editorMode));
    }
}
