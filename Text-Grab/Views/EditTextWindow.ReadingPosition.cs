using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ICSharpCode.AvalonEdit.Rendering;
using Text_Grab.Core.Editing;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab;

/// <summary>
/// "Pick up where you left off": remembers the top row of the viewport when a long document is closed
/// (or replaced), and offers a button to scroll back to it when the same file or history item is opened.
/// </summary>
public partial class EditTextWindow
{
    // Set while the button is showing; null once it was used, hidden, or never needed.
    private int? _resumePosition;
    private EtwEditorMode _resumeMode;

    private string? GetReadingPositionKey(bool createHistoryId)
    {
        if (!string.IsNullOrWhiteSpace(OpenedFilePath))
            return ReadingPositionStore.KeyForFile(OpenedFilePath);

        if (string.IsNullOrEmpty(historyId) && createHistoryId)
            historyId = Guid.NewGuid().ToString();

        return string.IsNullOrEmpty(historyId) ? null : ReadingPositionStore.KeyForHistoryItem(historyId);
    }

    private void SaveReadingPosition()
    {
        // A still-pending button means the user never moved on from the saved spot; keep that bookmark
        // rather than overwriting it with the top of the document.
        bool bookmarkUntouched = _resumePosition is not null;
        ResumeReadingButton.Visibility = Visibility.Collapsed;
        _resumePosition = null;

        if (string.IsNullOrWhiteSpace(Session.Text))
            return;

        try
        {
            (int position, bool isLongDocument) = GetTopOfViewport();
            IReadOnlyList<int> collapsedRegions = editorMode == EtwEditorMode.Code ? CodeEditorControl.GetCollapsedRegionStarts() : [];

            if (GetReadingPositionKey(createHistoryId: isLongDocument && (position > 0 || collapsedRegions.Count > 0)) is not string key)
                return;

            if (!bookmarkUntouched)
                ReadingPositionStore.SetPosition(key, editorMode.ToString(), position, isLongDocument);

            // Collapsed regions are saved even when the scroll bookmark is left alone: they may have changed.
            if (editorMode == EtwEditorMode.Code)
            {
                ReadingPositionStore.SetFolds(
                    key,
                    isLongDocument ? FoldState.Capture(CodeEditorControl.Document.Text, collapsedRegions) : null);
            }
        }
        catch
        {
            // A bookmark is never worth failing to close or open a document over.
        }
    }

    private void ShowResumeReadingButtonIfNeeded()
    {
        if (_isClosed || GetReadingPositionKey(createHistoryId: false) is not string key)
            return;

        // Folds come back on their own; only the scroll position waits for the button.
        if (editorMode == EtwEditorMode.Code)
            CodeEditorControl.RestoreCollapsedRegions(ReadingPositionStore.GetFolds(key));

        _resumePosition = ReadingPositionStore.GetPosition(key, editorMode.ToString());
        _resumeMode = editorMode;
        ResumeReadingButton.Visibility = _resumePosition is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// The top row of text currently in the viewport, in the unit of the active editor: a character
    /// index (text, Markdown), a line (code) or a row (spreadsheet).
    /// </summary>
    private (int Position, bool IsLongDocument) GetTopOfViewport()
    {
        switch (editorMode)
        {
            case EtwEditorMode.Code:
                {
                    TextView textView = CodeEditorControl.TextArea.TextView;
                    int line = textView.GetDocumentLineByVisualTop(textView.ScrollOffset.Y)?.LineNumber ?? 1;
                    return (line > 1 ? line : 0, CodeEditorControl.Document.TextLength >= ReadingPositionStore.MinimumTextLength);
                }
            case EtwEditorMode.Markdown:
                {
                    TextPointer? top = MarkdownEditorControl.GetPositionFromPoint(new Point(2, 2), true);
                    int offset = top is null ? 0 : MarkdownEditorControl.Document.ContentStart.GetOffsetToPosition(top);
                    return (offset, Session.Text.Length >= ReadingPositionStore.MinimumTextLength);
                }
            case EtwEditorMode.Spreadsheet:
                {
                    int rows = SpreadsheetDataGrid.Items.Count;
                    bool isScrollingByRow = WindowUtilities.GetScrollViewer(SpreadsheetDataGrid) is ScrollViewer { CanContentScroll: true };
                    int row = isScrollingByRow ? (int)WindowUtilities.GetScrollViewer(SpreadsheetDataGrid)!.VerticalOffset : 0;
                    return (row, rows >= ReadingPositionStore.MinimumSpreadsheetRows);
                }
            default:
                {
                    int firstVisibleLine = PassedTextControl.GetFirstVisibleLineIndex();
                    int index = firstVisibleLine > 0 ? PassedTextControl.GetCharacterIndexFromLineIndex(firstVisibleLine) : 0;
                    return (index, PassedTextControl.Text.Length >= ReadingPositionStore.MinimumTextLength);
                }
        }
    }

    private void ResumeReadingButton_Click(object sender, RoutedEventArgs e)
    {
        ResumeReadingButton.Visibility = Visibility.Collapsed;
        int? position = _resumePosition;
        _resumePosition = null;

        // The bookmark's unit belongs to the mode it was saved in.
        if (position is not int target || _resumeMode != editorMode)
            return;

        switch (editorMode)
        {
            case EtwEditorMode.Code:
                ScrollCodeEditorTo(target);
                break;
            case EtwEditorMode.Markdown:
                ScrollMarkdownTo(target);
                break;
            case EtwEditorMode.Spreadsheet:
                ScrollSpreadsheetTo(target);
                break;
            default:
                ScrollTextTo(target);
                break;
        }
    }

    private void ScrollTextTo(int index)
    {
        if (index > PassedTextControl.Text.Length)
            return;

        PassedTextControl.Focus();
        PassedTextControl.CaretIndex = index;

        // ScrollToLine only makes the line visible (at the bottom edge when coming from above),
        // so scroll the line to the top of the view instead.
        PassedTextControl.UpdateLayout();
        Rect charRect = PassedTextControl.GetRectFromCharacterIndex(index);
        if (!charRect.IsEmpty && WindowUtilities.GetScrollViewer(PassedTextControl) is ScrollViewer scrollViewer)
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + charRect.Top);
        else
            PassedTextControl.ScrollToLine(PassedTextControl.GetLineIndexFromCharacterIndex(index));
    }

    private void ScrollCodeEditorTo(int line)
    {
        line = Math.Min(line, CodeEditorControl.Document.LineCount);
        CodeEditorControl.Focus();
        CodeEditorControl.TextArea.Caret.Line = line;
        CodeEditorControl.UpdateLayout();
        CodeEditorControl.ScrollToVerticalOffset(CodeEditorControl.TextArea.TextView.GetVisualTopByDocumentLine(line));
    }

    private void ScrollMarkdownTo(int offset)
    {
        TextPointer? target = MarkdownEditorControl.Document.ContentStart.GetPositionAtOffset(offset);
        if (target is null)
            return;

        MarkdownEditorControl.Focus();
        MarkdownEditorControl.CaretPosition = target;
        MarkdownEditorControl.UpdateLayout();

        Rect charRect = target.GetCharacterRect(LogicalDirection.Forward);
        if (!charRect.IsEmpty && WindowUtilities.GetScrollViewer(MarkdownEditorControl) is ScrollViewer scrollViewer)
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + charRect.Top);
    }

    private void ScrollSpreadsheetTo(int row)
    {
        if (SpreadsheetDataGrid.Items.Count == 0 || WindowUtilities.GetScrollViewer(SpreadsheetDataGrid) is not ScrollViewer scrollViewer)
            return;

        // The grid virtualizes rows, so its vertical offset counts rows rather than pixels.
        scrollViewer.ScrollToVerticalOffset(Math.Min(row, SpreadsheetDataGrid.Items.Count - 1));
    }
}
