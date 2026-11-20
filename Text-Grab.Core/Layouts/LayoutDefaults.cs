using System;
using Text_Grab.Core.Editing;
using Text_Grab.Models;

namespace Text_Grab.Core.Layouts;

/// <summary>
/// How a layout's <see cref="EtwLayoutDefinition.DefaultMode"/> and
/// <see cref="EtwLayoutDefinition.DefaultCodeLanguage"/> meet the mode a window would otherwise open in
/// (v5 E5 <c>mermaid</c>). Pure, so the whole matrix is tested without a window.
/// </summary>
/// <remarks>
/// <para>The rule, in order of strength:</para>
/// <list type="number">
/// <item>A history item's saved mode and code language win. Reopening restores what was saved; the
/// window never asks this class.</item>
/// <item>A file opened as a single tracked text file takes the layout's mode over the mode its type
/// implies, when that type implies only <see cref="EtwEditorMode.Text"/> or
/// <see cref="EtwEditorMode.Markdown"/>. That is how <c>.mmd</c> (Text) and <c>.md</c> (Markdown) reach
/// Code mode in the Mermaid and Markdown layouts. A file whose type needs its mode (a table opens as a
/// spreadsheet, a source file as code with its own language) keeps it, and so does a multi-file open.</item>
/// <item>A window with no file and no history item, still in its default Text mode when the layout is first
/// applied (<c>--layout mermaid</c>, a link, a new window), takes the layout's mode. A mode somebody
/// already chose is never replaced.</item>
/// <item>Switching layouts later (E6 <c>picker</c>, <see cref="ModeOnSwitch"/>) changes the mode only when
/// the new layout names one, and never when that would lose something.</item>
/// <item>A null <see cref="EtwLayoutDefinition.DefaultMode"/> changes nothing, which is why Classic and
/// every window with no layout behave as before.</item>
/// </list>
/// </remarks>
public static class LayoutDefaults
{
    /// <summary>The mode a single text file opens in: <paramref name="fileMode"/>, unless the layout's default replaces it.</summary>
    /// <param name="fileMode">What the file's type implies (<c>IoUtilities.GetEditorModeForPath</c>).</param>
    /// <param name="layout">The layout the window shows, or null.</param>
    public static EtwEditorMode ModeForFile(EtwEditorMode fileMode, EtwLayoutDefinition? layout)
        => layout?.DefaultMode is EtwEditorMode layoutMode && fileMode is EtwEditorMode.Text or EtwEditorMode.Markdown
            ? layoutMode
            : fileMode;

    /// <summary>The mode for a window with no file when <paramref name="layout"/> is first applied.</summary>
    /// <param name="currentMode">The window's mode now; anything but <see cref="EtwEditorMode.Text"/> was chosen on purpose and stays.</param>
    public static EtwEditorMode ModeForBlankWindow(EtwEditorMode currentMode, EtwLayoutDefinition? layout)
        => currentMode == EtwEditorMode.Text && layout?.DefaultMode is EtwEditorMode layoutMode ? layoutMode : currentMode;

    /// <summary>
    /// The mode for a window that already holds a document when the person switches to
    /// <paramref name="layout"/> (v5 E6 <c>picker</c>): the layout's <see cref="EtwLayoutDefinition.DefaultMode"/> if
    /// it names one, unless leaving the current mode would lose something.
    /// </summary>
    /// <remarks>
    /// Nothing is lost by leaving Text or Code. A spreadsheet whose table carries formatting (wrapped
    /// cells, column widths, row heights) stays a spreadsheet. Markdown mode stays Markdown mode: there
    /// is no cheap check that the rendered document gives back the same text, so it is never left.
    /// A layout with no default mode (Classic, Explorer) changes nothing.
    /// </remarks>
    /// <param name="currentMode">The window's mode now.</param>
    /// <param name="layout">The layout being switched to.</param>
    /// <param name="spreadsheetHasFormatting"><see cref="Text_Grab.Models.EditTextTableDocument.HasViewFormatting"/> for the window's table.</param>
    public static EtwEditorMode ModeOnSwitch(EtwEditorMode currentMode, EtwLayoutDefinition? layout, bool spreadsheetHasFormatting)
    {
        if (layout?.DefaultMode is not EtwEditorMode layoutMode || layoutMode == currentMode)
            return currentMode;

        return currentMode switch
        {
            EtwEditorMode.Markdown => currentMode,
            EtwEditorMode.Spreadsheet when spreadsheetHasFormatting => currentMode,
            _ => layoutMode,
        };
    }

    /// <summary>
    /// The language to set when a switch enters Code mode: the layout's, but only if the window has no
    /// language yet; otherwise null, which means leave it.
    /// </summary>
    public static CodeLanguageChoice? CodeLanguageOnSwitch(bool languageAlreadySet, EtwLayoutDefinition? layout)
        => languageAlreadySet ? null : CodeLanguageFor(null, layout);

    /// <summary>
    /// The highlighting for a document the layout put in Code mode: the language its file extension gave,
    /// else the layout's <see cref="EtwLayoutDefinition.DefaultCodeLanguage"/>.
    /// </summary>
    /// <param name="fileLanguageName">The <see cref="CodeLanguage.Name"/> for the file's extension, or null.</param>
    /// <param name="layout">The layout the window shows, or null.</param>
    public static CodeLanguageChoice CodeLanguageFor(string? fileLanguageName, EtwLayoutDefinition? layout)
    {
        if (!string.IsNullOrWhiteSpace(fileLanguageName))
            return new CodeLanguageChoice(fileLanguageName, Detect: false);

        string? wanted = layout?.DefaultCodeLanguage;

        // No default is Plain Text, not a guess: Mermaid source would otherwise be detected as something else.
        if (string.IsNullOrWhiteSpace(wanted))
            return new CodeLanguageChoice(null, Detect: false);

        // A name this build does not list falls back to detection (E4 Design).
        return CodeLanguages.Find(wanted) is CodeLanguage known
            ? new CodeLanguageChoice(known.Name, Detect: false)
            : new CodeLanguageChoice(null, Detect: true);
    }
}

/// <summary>A code language to set: <see cref="Name"/>, null for Plain Text, or <see cref="Detect"/> to look at the text.</summary>
public readonly record struct CodeLanguageChoice(string? Name, bool Detect);
