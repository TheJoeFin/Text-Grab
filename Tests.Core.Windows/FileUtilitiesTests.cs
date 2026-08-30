using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

// Pure half of the original Tests/FilesIoTests.cs (batch 7a): FileUtilities.GetVisualDocumentFilter
// is Core.Windows-only and needs no app type. The rest of that file needed WPF or app-side members
// and kept the FilesIoTests name in Tests; the IoUtilities-only tests moved separately to
// Tests.Core/IoUtilitiesTests.cs.
public class FileUtilitiesTests
{
    [Fact]
    public void GetVisualDocumentFilter_IncludesPdfSupport()
    {
        string filter = FileUtilities.GetVisualDocumentFilter();

        Assert.Contains("Image and PDF files|", filter);
        Assert.Contains("PDF files|*.pdf", filter);
        Assert.Contains("Image files|", filter);
    }

    // Joined FileUtilities in 7b once GrabFrameFileUtilities followed HistoryInfo to
    // Core.Windows and GetOpenDocumentFilter() no longer needed the app-side
    // OpenDocumentFilterUtilities split.
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
}
