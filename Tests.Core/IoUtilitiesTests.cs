using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

// Pure half of the original Tests/FilesIoTests.cs (batch 7a): these three methods only touch
// Text_Grab.Utilities.IoUtilities, which is plain Core. The rest of that file needed WPF
// ([WpfFact]/[WpfTheory]) or app-side FileUtilities/OpenDocumentFilterUtilities/App members and
// kept the FilesIoTests name in Tests. FileUtilities.GetVisualDocumentFilter, the other pure
// method in that file, is Core.Windows-only and moved separately to
// Tests.Core.Windows/FileUtilitiesTests.cs.
public class IoUtilitiesTests
{
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
}
