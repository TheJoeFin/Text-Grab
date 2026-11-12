using System;
using System.Windows.Controls;
using Text_Grab.Core.Editing;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Editing;

/// <summary>
/// The rendered Markdown editor, a <see cref="RichTextBox"/>. The rendered document's text is not the
/// raw Markdown the session's offsets count (markers like <c>**</c> and <c>#</c> are stripped on
/// render), so ranges are mapped through the offset map the window builds with the document.
/// </summary>
/// <remarks>
/// Building the document (theme, margins, wrap, offset map) and showing a range still belong to the
/// window, which owns that state; this surface delegates both to it.
/// </remarks>
internal sealed class MarkdownSurface(
    RichTextBox editor,
    Action<string> loadDocumentFromText,
    Action<TextSpan> selectAndShow) : IEditorSurface
{
    public EtwEditorMode Mode => EtwEditorMode.Markdown;

    /// <summary>The rendered document serialized back to Markdown; <c>null</c> before one is built.</summary>
    public string? GetText() => editor.Document is null
        ? null
        : MarkdownFlowDocumentUtilities.SerializeToMarkdown(editor.Document, preserveLiteralMarkdown: true);

    public void SetText(string text) => loadDocumentFromText(text);

    public void SelectAndShow(TextSpan range) => selectAndShow(range);
}
