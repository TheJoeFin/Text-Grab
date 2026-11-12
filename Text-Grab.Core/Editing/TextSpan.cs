namespace Text_Grab.Core.Editing;

/// <summary>
/// A range of an editor session's text, as plain-text offsets into <see cref="IEditorSession.Text"/>.
/// A line break counts as the characters it is made of, so <c>\r\n</c> is two.
/// </summary>
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>The offset just past the span's last character.</summary>
    public int End => Start + Length;
}
