using System;
using Text_Grab.Core.Editing;
using Text_Grab.Models;

namespace Text_Grab.Editing;

/// <summary>
/// The rendered Markdown editor. The rendered document's text is not the raw Markdown the session's
/// offsets count (markers like <c>**</c> and <c>#</c> are stripped on render), so ranges are mapped
/// through the offset map the window builds with the document.
/// </summary>
/// <remarks>
/// For now this delegates to the window, which owns the rendered document and its offset map.
/// </remarks>
internal sealed class MarkdownSurface(Action<TextSpan> selectAndShow) : IEditorSurface
{
    public EtwEditorMode Mode => EtwEditorMode.Markdown;

    public void SelectAndShow(TextSpan range) => selectAndShow(range);
}
