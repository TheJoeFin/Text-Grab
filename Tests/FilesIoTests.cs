using System.Drawing;
using System.IO;
using System.Windows;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

public class FilesIoTests
{
    private const string fontSamplePath = @"Images\font_sample.png";

    [WpfFact]
    public async Task CanSaveImagesWithHistory()
    {
        string path = FileUtilities.GetPathToLocalFile(fontSamplePath);
        Bitmap fontSampleBitmap = new(path);

        bool couldSave = await FileUtilities.SaveImageFile(fontSampleBitmap, "newTest.png", FileStorageKind.WithHistory);

        Assert.True(couldSave);
    }

    [WpfFact]
    public async Task SaveImageFile_SucceedsAfterClearTransientImage()
    {
        // Reproduces the race condition: SaveImageFile returns a Task that
        // may still be running when ClearTransientImage nulls the bitmap.
        // The save must complete successfully even when ClearTransientImage
        // is called immediately after the fire-and-forget pattern used by
        // HistoryService.SaveToHistory.
        string path = FileUtilities.GetPathToLocalFile(fontSamplePath);
        Bitmap bitmap = new(path);

        HistoryInfo historyInfo = new()
        {
            ID = "save-race-test",
            ImageContent = bitmap,
            ImagePath = $"race_test_{Guid.NewGuid()}.bmp",
        };

        Task<bool> saveTask = FileUtilities.SaveImageFile(
            historyInfo.ImageContent, historyInfo.ImagePath, FileStorageKind.WithHistory);

        // Mirrors what HistoryService.SaveToHistory does right after the
        // fire-and-forget call — must not cause saveTask to fail.
        historyInfo.ClearTransientImage();

        bool couldSave = await saveTask;
        Assert.True(couldSave);
    }

    [WpfFact]
    public async Task CanSaveTextFilesWithExe()
    {
        string textContent = "abcdef";
        string fileName = "testAbc.txt";

        bool couldSave = await FileUtilities.SaveTextFile(textContent, fileName, FileStorageKind.WithExe);
        Assert.True(couldSave);
    }

    [WpfTheory]
    [InlineData(FileStorageKind.WithExe)]
    [InlineData(FileStorageKind.WithHistory)]
    public async Task CanStoreThenReadTextFilesWithExe(FileStorageKind storageKind)
    {
        string textContent = $"Hello Hello this is a test of the system {DateTime.Now}";
        string fileName = "testAbc.txt";

        _ = await FileUtilities.SaveTextFile(textContent, fileName, storageKind);
        string readString = await FileUtilities.GetTextFileAsync(fileName, storageKind);

        Assert.Equal(textContent, readString);
    }

    [WpfTheory]
    [InlineData(FileStorageKind.WithExe)]
    [InlineData(FileStorageKind.WithHistory)]
    [InlineData(FileStorageKind.Absolute)]
    public async Task ReadNotExistingTextFileEmpty(FileStorageKind storageKind)
    {
        string fileName = "FileNotFound.json";
        string emptyReturn = await FileUtilities.GetTextFileAsync(fileName, storageKind);
        Assert.Empty(emptyReturn);
    }

    [WpfTheory]
    [InlineData(FileStorageKind.WithExe)]
    [InlineData(FileStorageKind.WithHistory)]
    [InlineData(FileStorageKind.Absolute)]
    public async Task ReadNotExistingImageFileEmpty(FileStorageKind storageKind)
    {
        string fileName = "FileNotFound.json";
        Bitmap? emptyReturn = await FileUtilities.GetImageFileAsync(fileName, storageKind);
        Assert.Null(emptyReturn);
    }

    [Theory]
    [InlineData(@"C:\Temp\sheet.csv", EtwEditorMode.Spreadsheet)]
    [InlineData(@"C:\Temp\sheet.TSV", EtwEditorMode.Spreadsheet)]
    [InlineData(@"C:\Temp\sheet.tab", EtwEditorMode.Spreadsheet)]
    [InlineData(@"C:\Temp\notes.md", EtwEditorMode.Markdown)]
    [InlineData(@"C:\Temp\notes.markdown", EtwEditorMode.Markdown)]
    [InlineData(@"C:\Temp\notes.txt", EtwEditorMode.Text)]
    [InlineData(@"C:\Temp\data.json", EtwEditorMode.Text)]
    public void GetEditorModeForPath_UsesFileExtension(string path, EtwEditorMode expectedMode)
    {
        Assert.Equal(expectedMode, IoUtilities.GetEditorModeForPath(path));
    }

    [Theory]
    [InlineData(@"C:\Temp\scan.png", OpenContentKind.Image)]
    [InlineData(@"C:\Temp\scan.PDF", OpenContentKind.PdfDocument)]
    [InlineData(@"C:\Temp\notes.txt", OpenContentKind.TextFile)]
    public void GetOpenContentKindForPath_ClassifiesVisualDocumentsAndText(string path, OpenContentKind expectedKind)
    {
        Assert.Equal(expectedKind, IoUtilities.GetOpenContentKindForPath(path));
    }

    [Theory]
    [InlineData(".png", true)]
    [InlineData(".PDF", true)]
    [InlineData(".txt", false)]
    [InlineData("", false)]
    public void IsVisualDocumentFileExtension_RecognizesImagesAndPdf(string extension, bool expected)
    {
        Assert.Equal(expected, IoUtilities.IsVisualDocumentFileExtension(extension));
    }

    [Fact]
    public void GetVisualDocumentFilter_IncludesPdfSupport()
    {
        string filter = FileUtilities.GetVisualDocumentFilter();

        Assert.Contains("Image and PDF files|", filter);
        Assert.Contains("PDF files|*.pdf", filter);
        Assert.Contains("Image files|", filter);
    }

    [Fact]
    public void GetOpenDocumentFilter_IncludesVisualAndTextOptions()
    {
        string filter = FileUtilities.GetOpenDocumentFilter();

        Assert.Contains("Supported documents|", filter);
        Assert.Contains("Image and PDF files|", filter);
        Assert.Contains("Spreadsheet documents|*.csv;*.tsv;*.tab", filter);
        Assert.Contains("Markdown documents|*.md;*.markdown", filter);
        Assert.Contains("Text documents (*.txt)|*.txt", filter);
        Assert.Contains("All files (*.*)|*.*", filter);
    }

    [WpfFact]
    public void GetDroppedFilePaths_ReturnsExistingFilesOnly()
    {
        string firstPath = Path.GetTempFileName();
        string secondPath = Path.GetTempFileName();
        string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        DataObject dataObject = new(DataFormats.FileDrop, new[] { firstPath, missingPath, secondPath });

        try
        {
            IReadOnlyList<string> paths = App.GetDroppedFilePaths(dataObject);

            Assert.Equal([firstPath, secondPath], paths);
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    [WpfFact]
    public void GetDroppedFileEffect_ReturnsCopyWhenExistingFilesAreDropped()
    {
        string path = Path.GetTempFileName();
        DataObject dataObject = new(DataFormats.FileDrop, new[] { path });

        try
        {
            Assert.Equal(DragDropEffects.Copy, App.GetDroppedFileEffect(dataObject));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [WpfFact]
    public void GetDroppedFileEffect_ReturnsNoneWhenNoFilesCanBeOpened()
    {
        DataObject dataObject = new(DataFormats.Text, "hello");

        Assert.Equal(DragDropEffects.None, App.GetDroppedFileEffect(dataObject));
    }
}
