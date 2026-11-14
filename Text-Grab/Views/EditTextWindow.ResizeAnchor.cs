using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Rendering;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab;

/// <summary>
/// Keeps the reader's place when the window is resized or maximized. Re-wrapping text moves the line
/// that was at the top of the viewport, so the top row is remembered while the viewport is stable and
/// scrolled back to once the resizing has settled (debounced, so the document is not jerked around mid-drag).
/// </summary>
public partial class EditTextWindow
{
    private static readonly TimeSpan ResizeRestoreDelay = TimeSpan.FromMilliseconds(180);

    private DispatcherTimer? _resizeRestoreTimer;
    private int? _anchorPosition;
    private EtwEditorMode _anchorMode;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(EditorScrollChanged));
    }

    private Control? GetAnchorEditor() => editorMode switch
    {
        EtwEditorMode.Code => CodeEditorControl,
        EtwEditorMode.Markdown => MarkdownEditorControl,
        EtwEditorMode.Spreadsheet => null,
        _ => PassedTextControl,
    };

    private void EditorScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isClosed
            || e.OriginalSource is not ScrollViewer sv
            || GetAnchorEditor() is not Control editor
            || !sv.IsDescendantOf(editor))
        {
            return;
        }

        bool viewportChanged = e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0;

        if (viewportChanged)
        {
            // Resize in progress: the anchor from before it started is kept; wait for it to settle.
            if (_anchorPosition is not null && _anchorMode == editorMode)
                RestartResizeRestoreTimer();
            return;
        }

        // While a restore is pending the layout is in flux; don't overwrite the anchor with a shifted one.
        if (_resizeRestoreTimer is { IsEnabled: true })
            return;

        try
        {
            (int position, bool _) = GetTopOfViewport();
            _anchorPosition = position;
            _anchorMode = editorMode;
        }
        catch
        {
            _anchorPosition = null;
        }
    }

    private void RestartResizeRestoreTimer()
    {
        _resizeRestoreTimer ??= new DispatcherTimer(ResizeRestoreDelay, DispatcherPriority.Background, ResizeRestoreTimer_Tick, Dispatcher)
        {
            IsEnabled = false,
        };

        _resizeRestoreTimer.Stop();
        _resizeRestoreTimer.Start();
    }

    private void ResizeRestoreTimer_Tick(object? sender, EventArgs e)
    {
        _resizeRestoreTimer?.Stop();

        if (_isClosed || _anchorPosition is not int anchor || _anchorMode != editorMode)
            return;

        try
        {
            (int current, bool _) = GetTopOfViewport();
            if (current != anchor)
                ScrollViewportTopTo(anchor);
        }
        catch
        {
            // Restoring the view is best effort.
        }
    }

    /// <summary>Scrolls the given top row (see GetTopOfViewport) to the top without moving the caret or focus.</summary>
    private void ScrollViewportTopTo(int position)
    {
        switch (editorMode)
        {
            case EtwEditorMode.Code:
                {
                    int line = Math.Clamp(position < 1 ? 1 : position, 1, CodeEditorControl.Document.LineCount);
                    CodeEditorControl.UpdateLayout();
                    CodeEditorControl.ScrollToVerticalOffset(CodeEditorControl.TextArea.TextView.GetVisualTopByDocumentLine(line));
                    break;
                }
            case EtwEditorMode.Markdown:
                {
                    if (MarkdownEditorControl.Document.ContentStart.GetPositionAtOffset(position) is not TextPointer target
                        || WindowUtilities.GetScrollViewer(MarkdownEditorControl) is not ScrollViewer sv)
                    {
                        return;
                    }

                    MarkdownEditorControl.UpdateLayout();
                    Rect rect = target.GetCharacterRect(LogicalDirection.Forward);
                    if (!rect.IsEmpty)
                        sv.ScrollToVerticalOffset(sv.VerticalOffset + rect.Top);
                    break;
                }
            case EtwEditorMode.Spreadsheet:
                break;
            default:
                {
                    if (position > PassedTextControl.Text.Length
                        || WindowUtilities.GetScrollViewer(PassedTextControl) is not ScrollViewer sv)
                    {
                        return;
                    }

                    PassedTextControl.UpdateLayout();
                    Rect rect = PassedTextControl.GetRectFromCharacterIndex(position);
                    if (!rect.IsEmpty)
                        sv.ScrollToVerticalOffset(sv.VerticalOffset + rect.Top);
                    break;
                }
        }
    }
}
