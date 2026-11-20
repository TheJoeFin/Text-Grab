using System;
using System.Linq;

namespace Text_Grab.Core.Layouts;

/// <summary>Layout IDs: the built-in ones, and which strings may be an ID at all.</summary>
/// <remarks>
/// An ID names a file in the layout store (<c>layouts/&lt;id&gt;.json</c>) and arrives from the command
/// line and from <c>text-grab://</c> links, which any web page can open. So an ID is a short run of
/// lower-case letters, digits, '.', '-' and '_', starting with a letter or digit, and never a name
/// Windows reserves for a device.
/// </remarks>
public static class LayoutIds
{
    /// <summary>Today's window: the editor with the calc pane to its right.</summary>
    public const string Classic = "classic";

    /// <summary>The editor with the recent items pane to its left (v5 E5 <c>recent</c>).</summary>
    public const string Explorer = "explorer";

    /// <summary>The source in the code editor with its diagram beside it (v5 E5 <c>mermaid</c>).</summary>
    public const string Mermaid = "mermaid";

    /// <summary>The Markdown source in the code editor with its rendered preview beside it (v5 E5 <c>markdown-preview</c>).</summary>
    public const string MarkdownPreview = "markdown-preview";

    public const int MaxLength = 64;

    private static readonly string[] ReservedDeviceNames =
    [
        "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    ];

    public static bool IsValid(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxLength)
            return false;

        if (!char.IsAsciiLetterLower(id[0]) && !char.IsAsciiDigit(id[0]))
            return false;

        foreach (char c in id)
        {
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c is not ('.' or '-' or '_'))
                return false;
        }

        // "con.json" opens the console on Windows, and so does "con.anything".
        string stem = id.Split('.')[0];
        return !ReservedDeviceNames.Contains(stem, StringComparer.Ordinal) && !id.EndsWith('.');
    }
}
