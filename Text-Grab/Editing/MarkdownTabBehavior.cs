using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Text_Grab.Utilities;

namespace Text_Grab.Editing;

/// <summary>
/// What Tab and Shift+Tab do in the rendered Markdown editor, by where the caret is. The editor has
/// <c>AcceptsTab</c>, so a Tab it does not handle here is the RichTextBox's own: the next table cell
/// (the previous one for Shift+Tab), or a tab character.
/// </summary>
/// <remarks>
/// <para>
/// Markdown cannot keep a plain tab or ordinary spaces at the start of a paragraph: two or three
/// spaces are dropped on reload and four or more (or a tab) turn the paragraph into a code block. An
/// em space is ordinary text to Markdown, so it is the indent there.
/// </para>
/// <list type="bullet">
/// <item>In a table cell: left to the RichTextBox, which moves between cells.</item>
/// <item>In a list item: one level deeper (Shift+Tab: one level up). The first item of a list has
/// nothing above it to nest under, and a top-level item has no level above it; both do nothing.</item>
/// <item>In a code block: a tab character, and Shift+Tab removes the one before the caret.</item>
/// <item>At the start of a paragraph or on an empty line: an em space.</item>
/// <item>Across several paragraphs: each one's start is indented (outdented).</item>
/// <item>Anywhere else in text: a tab character, as the RichTextBox does.</item>
/// </list>
/// </remarks>
internal static class MarkdownTabBehavior
{
    /// <summary>The indent Tab inserts where a tab or spaces would not survive a save and reload.</summary>
    internal const string Indent = " ";

    /// <summary>Handles the key; <c>false</c> leaves it to the RichTextBox's own Tab.</summary>
    internal static bool TryHandle(RichTextBox editor, bool outdent)
    {
        ArgumentNullException.ThrowIfNull(editor);
        TextSelection selection = editor.Selection;

        if (Ancestor<TableCell>(selection.Start.Parent) is not null)
            return false;

        if (Ancestor<ListItem>(selection.Start.Parent) is ListItem item)
        {
            ChangeListLevel(editor, item, outdent);
            return true;
        }

        if (selection.Start.Paragraph is not Paragraph paragraph)
            return false;

        if (MarkdownFlowDocumentUtilities.IsCodeBlock(paragraph))
        {
            if (!outdent)
                return false;

            RemoveIndentBefore(selection.Start);
            return true;
        }

        if (!selection.IsEmpty && selection.End.Paragraph != paragraph)
        {
            ChangeParagraphIndents(editor, paragraph, selection.End, outdent);
            return true;
        }

        if (outdent)
        {
            if (!RemoveIndentBefore(selection.Start))
                RemoveIndentAtStart(paragraph);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(new TextRange(paragraph.ContentStart, selection.Start).Text))
            return false;

        // Setting the text leaves it selected, and the next Tab would replace it instead of adding to it.
        selection.Text = Indent;
        editor.CaretPosition = selection.End;
        return true;
    }

    private static void ChangeListLevel(RichTextBox editor, ListItem item, bool outdent)
    {
        bool isNested = item.Parent is List { Parent: ListItem };
        if (outdent ? !isNested : item.PreviousListItem is null)
            return;

        // WPF copies the parent's marker into the list it nests, so restyle in the same undo step.
        using (editor.DeclareChangeBlock())
        {
            (outdent ? EditingCommands.DecreaseIndentation : EditingCommands.IncreaseIndentation).Execute(null, editor);
            MarkdownFlowDocumentUtilities.ApplyListStyling(editor.Document);
        }
    }

    private static void ChangeParagraphIndents(RichTextBox editor, Paragraph first, TextPointer selectionEnd, bool outdent)
    {
        using (editor.DeclareChangeBlock())
        {
            for (Block? block = first; block is not null && block.ContentStart.CompareTo(selectionEnd) < 0; block = block.NextBlock)
            {
                if (block is not Paragraph paragraph || MarkdownFlowDocumentUtilities.IsCodeBlock(paragraph))
                    continue;

                if (outdent)
                    RemoveIndentAtStart(paragraph);
                else
                    paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward).InsertTextInRun(Indent);
            }
        }
    }

    /// <summary>Deletes the indent character just before <paramref name="position"/>, if that is what is there.</summary>
    private static bool RemoveIndentBefore(TextPointer position)
    {
        TextPointer? previous = position.GetNextInsertionPosition(LogicalDirection.Backward);
        return previous is not null && RemoveIfIndent(previous, position);
    }

    private static void RemoveIndentAtStart(Paragraph paragraph)
    {
        TextPointer start = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        TextPointer? next = start.GetNextInsertionPosition(LogicalDirection.Forward);
        if (next is not null)
            RemoveIfIndent(start, next);
    }

    private static bool RemoveIfIndent(TextPointer from, TextPointer to)
    {
        TextRange range = new(from, to);
        if (range.Text is not [char character] || character is not ('\t' or ' ' or ' ' or ' '))
            return false;

        range.Text = string.Empty;
        return true;
    }

    private static T? Ancestor<T>(DependencyObject? start) where T : DependencyObject
    {
        for (DependencyObject? current = start; current is not null; current = LogicalTreeHelper.GetParent(current))
        {
            if (current is T match)
                return match;
        }

        return null;
    }
}
