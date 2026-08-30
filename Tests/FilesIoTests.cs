using System.Drawing;
using System.IO;
using System.Windows;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

// App-coupled remainder of the original file (batch 7a). Its IoUtilities-only tests moved to
// Tests.Core/IoUtilitiesTests.cs and its FileUtilities.GetVisualDocumentFilter test moved to
// Tests.Core.Windows/FileUtilitiesTests.cs. What is left here is blocked on WPF ([WpfFact]/
// [WpfTheory] needing Xunit.StaFact, which cannot be referenced outside Tests) or on the
// app-side OpenDocumentFilterUtilities/App classes, so it kept the original FilesIoTests name.
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

    [Fact]
    public void GetOpenDocumentFilter_IncludesVisualAndTextOptions()
    {
        string filter = OpenDocumentFilterUtilities.GetOpenDocumentFilter();

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
