using System.Linq;

namespace Text_Grab.Utilities;

/// <summary>
/// Impure half of the "Open document" filter, split out of <see cref="FileUtilities"/> in the
/// deferred-ledger sweep because it also needs <see cref="GrabFrameFileUtilities"/>, which stays
/// app-side (blocked on <c>HistoryInfo</c> - see its section 7 row). Everything else that used to
/// live alongside it (<see cref="FileUtilities.GetVisualDocumentFilter"/>,
/// <see cref="FileUtilities.GetImageFilter"/>) moved to Text-Grab.Core.Windows under the original
/// FileUtilities name; this calls back into its two now-internal helpers,
/// <see cref="FileUtilities.GetVisualDocumentFilterPattern"/> and
/// <see cref="FileUtilities.GetExtensionsFilterPattern"/>, to build the same string it always did.
/// </summary>
public static class OpenDocumentFilterUtilities
{
    public static string GetOpenDocumentFilter()
    {
        string spreadsheetExtensions = FileUtilities.GetExtensionsFilterPattern(IoUtilities.SpreadsheetExtensions);
        string markdownExtensions = FileUtilities.GetExtensionsFilterPattern(IoUtilities.MarkdownExtensions);
        string grabFrameExtension = $"*{GrabFrameFileUtilities.GrabFrameFileExtension}";
        string supportedExtensions = string.Join(";", new[]
        {
            FileUtilities.GetVisualDocumentFilterPattern(),
            grabFrameExtension,
            spreadsheetExtensions,
            markdownExtensions,
            "*.txt"
        }.Where(pattern => !string.IsNullOrWhiteSpace(pattern)));

        return string.Join("|", new[]
        {
            $"Supported documents|{supportedExtensions}",
            FileUtilities.GetVisualDocumentFilter(),
            GrabFrameFileUtilities.GetGrabFrameFileFilter(),
            $"Spreadsheet documents|{spreadsheetExtensions}",
            $"Markdown documents|{markdownExtensions}",
            "Text documents (*.txt)|*.txt",
            "All files (*.*)|*.*"
        });
    }
}
