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
}
