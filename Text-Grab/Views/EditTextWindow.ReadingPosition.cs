using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Threading;
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
    private static readonly TimeSpan ResumeReadingTimeout = TimeSpan.FromSeconds(10);
    private DispatcherTimer? _resumeReadingTimer;
    // The prompt can expire without consuming the saved position.
    private int? _resumePosition;
    private int? _resumeStartPosition;
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
        bool bookmarkPending = _resumePosition is not null;
        int? startPosition = _resumeMode == editorMode ? _resumeStartPosition : null;
        HideResumeReadingBookmark();
        _resumePosition = null;
        _resumeStartPosition = null;

        if (string.IsNullOrWhiteSpace(Session.Text))
            return;

        try
        {
            (int position, bool isLongDocument) = GetTopOfViewport();
            IReadOnlyList<int> collapsedRegions = editorMode == EtwEditorMode.Code ? CodeEditorControl.GetCollapsedRegionStarts() : [];

            if (GetReadingPositionKey(createHistoryId: isLongDocument && (position > 0 || collapsedRegions.Count > 0)) is not string key)
                return;

            // Timing out only hides the prompt. Keep the saved spot if the reader hasn't moved.
            if (!bookmarkPending || (startPosition is int start && position != start))
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

    private void ShowResumeReadingBookmarkIfNeeded()
    {
        HideResumeReadingBookmark();
        _resumePosition = null;
        _resumeStartPosition = null;
        if (_isClosed || GetReadingPositionKey(createHistoryId: false) is not string key)
            return;

        // Folds come back on their own; only the scroll position waits for the bookmark.
        if (editorMode == EtwEditorMode.Code)
            CodeEditorControl.RestoreCollapsedRegions(ReadingPositionStore.GetFolds(key));

        if (ReadingPositionStore.GetPosition(key, editorMode.ToString()) is int position)
            OfferResumeReadingBookmark(position);
    }

    private void OfferResumeReadingBookmark(int position)
    {
        _resumePosition = position;
        _resumeMode = editorMode;
        ResumeReadingButton.LayoutUpdated += UpdateResumeReadingBookmark;
        UpdateResumeReadingBookmark(this, EventArgs.Empty);
        _resumeReadingTimer ??= new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = ResumeReadingTimeout,
        };
        _resumeReadingTimer.Tick += ResumeReadingTimer_Tick;
        _resumeReadingTimer.Start();
    }

    private void HideResumeReadingBookmark()
    {
        ResumeReadingButton.LayoutUpdated -= UpdateResumeReadingBookmark;
        ResumeReadingButton.Visibility = Visibility.Collapsed;
        if (_resumeReadingTimer is not null)
        {
            _resumeReadingTimer.Stop();
            _resumeReadingTimer.Tick -= ResumeReadingTimer_Tick;
        }
    }

    private void ResumeReadingTimer_Tick(object? sender, EventArgs e)
    {
        if (!ResumeReadingButton.IsMouseOver && !ResumeReadingButton.IsKeyboardFocused)
            HideResumeReadingBookmark();
    }

    private Control GetReadingEditor() => editorMode switch
    {
        EtwEditorMode.Code => CodeEditorControl,
        EtwEditorMode.Markdown => MarkdownEditorControl,
        EtwEditorMode.Spreadsheet => SpreadsheetDataGrid,
        _ => PassedTextControl,
    };

    private void UpdateResumeReadingBookmark(object? sender, EventArgs e)
    {
        if (_resumePosition is not int position || _resumeMode != editorMode
            || WindowUtilities.GetScrollViewer(GetReadingEditor()) is not ScrollViewer { ScrollableHeight: > 0 } viewer
            || viewer.Template?.FindName("PART_VerticalScrollBar", viewer) is not ScrollBar { Track: Track track } scrollBar
            || track.ActualHeight <= 0
            || GetReadingPositionOffset(position, viewer) is not double offset)
        {
            ResumeReadingButton.Visibility = Visibility.Collapsed;
            return;
        }

        _resumeStartPosition ??= GetTopOfViewport().Position;

        // Align with the center of the scroll thumb at the destination, without covering the track.
        Point trackTop = track.TranslatePoint(new Point(), ReadingEditorHost);
        double thumbHeight = track.Thumb?.ActualHeight ?? 0;
        double fraction = Math.Clamp(offset / viewer.ScrollableHeight, 0, 1);
        double center = trackTop.Y + thumbHeight / 2 + fraction * (track.ActualHeight - thumbHeight);
        double top = Math.Clamp(center - ResumeReadingButton.Height / 2, 0, Math.Max(0, ReadingEditorHost.ActualHeight - ResumeReadingButton.Height));
        double right = ReadingEditorHost.ActualWidth - scrollBar.TranslatePoint(new Point(), ReadingEditorHost).X;
        ResumeReadingButton.Margin = new Thickness(4, top, right, 0);
        ResumeReadingButton.Visibility = Visibility.Visible;
    }

    private double? GetReadingPositionOffset(int position, ScrollViewer viewer)
    {
        switch (editorMode)
        {
            case EtwEditorMode.Code:
                return CodeEditorControl.TextArea.TextView.GetVisualTopByDocumentLine(Math.Min(position, CodeEditorControl.Document.LineCount));
            case EtwEditorMode.Markdown:
                if (MarkdownEditorControl.Document.ContentStart.GetPositionAtOffset(position) is TextPointer target)
                    return OffsetFromRect(target.GetCharacterRect(LogicalDirection.Forward));
                break;
            case EtwEditorMode.Spreadsheet:
                if (viewer.CanContentScroll && SpreadsheetDataGrid.Items.Count > 0)
                    return position;
                break;
            default:
                if (position <= PassedTextControl.Text.Length)
                    return OffsetFromRect(PassedTextControl.GetRectFromCharacterIndex(position));
                break;
        }

        return null;

        double? OffsetFromRect(Rect rect) => rect.IsEmpty ? null : viewer.VerticalOffset + rect.Top;
    }

    /// <summary>
    /// The top row of text currently in the viewport, in the unit of the active editor: a character
    /// index (text, Markdown), a line (code) or a scroll offset (spreadsheet).
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
                    // The grid can virtualize and scroll in pixels; the offset isn't necessarily a row index.
                    int offset = WindowUtilities.GetScrollViewer(SpreadsheetDataGrid) is ScrollViewer { CanContentScroll: true } viewer
                        ? (int)viewer.VerticalOffset
                        : 0;
                    return (offset, rows >= ReadingPositionStore.MinimumSpreadsheetRows);
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
        HideResumeReadingBookmark();
        Control editor = GetReadingEditor();
        if (_resumePosition is not int position || _resumeMode != editorMode
            || WindowUtilities.GetScrollViewer(editor) is not ScrollViewer viewer
            || GetReadingPositionOffset(position, viewer) is not double offset)
            return;

        _resumePosition = null;
        _resumeStartPosition = null;
        editor.Focus();
        switch (editorMode)
        {
            case EtwEditorMode.Code:
                CodeEditorControl.TextArea.Caret.Line = Math.Min(position, CodeEditorControl.Document.LineCount);
                break;
            case EtwEditorMode.Markdown:
                if (MarkdownEditorControl.Document.ContentStart.GetPositionAtOffset(position) is TextPointer target)
                    MarkdownEditorControl.CaretPosition = target;
                break;
            case EtwEditorMode.Text:
                PassedTextControl.CaretIndex = position;
                break;
        }

        viewer.ScrollToVerticalOffset(offset);
    }
}
