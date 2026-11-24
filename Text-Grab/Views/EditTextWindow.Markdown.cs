using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using Text_Grab.Core.Editing;
using Text_Grab.Editing;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Text_Grab;

public partial class EditTextWindow
{
    private bool markdownTableLayoutPending;

    /// <summary>
    /// <see cref="Editing.MarkdownSurface"/>'s half of syncing the Markdown editor from the text. The session
    /// skips it while the text is being written from the Markdown editor.
    /// </summary>
    private void RefreshMarkdownFromText(string text)
    {
        LoadMarkdownDocumentFromText(text);
        UpdateMarkdownFormattingBar();
        UpdateLineAndColumnText();
    }

    private void LoadMarkdownDocumentFromText(string? markdownText)
    {
        isApplyingMarkdownDocument = true;
        MarkdownEditorControl.Document = MarkdownFlowDocumentUtilities.CreateFlowDocument(
            markdownText,
            MarkdownEditorControl.FontFamily,
            MarkdownEditorControl.FontSize,
            OpenedFilePath);
        markdownOffsetMap = MarkdownFlowDocumentUtilities.BuildOffsetMap(MarkdownEditorControl.Document);
        markdownOffsetMapSourceText = markdownText ?? string.Empty;
        ApplyMarkdownTheme();
        MarkdownFlowDocumentUtilities.ApplyBottomScrollMargin(MarkdownEditorControl);
        ApplyMarkdownWrapSetting();
        SetMargins(MarginsMenuItem.IsChecked is true);
        isApplyingMarkdownDocument = false;
    }

    /// <summary>Copies the rendered document, serialized back to Markdown, into the text.</summary>
    private void SyncMarkdownTextFromDocument() => SessionImpl.SyncTextFromEditor(EtwEditorMode.Markdown);

    private void ApplyMarkdownTheme()
    {
        if (MarkdownEditorControl.Document is null)
            return;

        bool isLightTheme = SystemThemeUtility.IsLightTheme();
        MarkdownFlowDocumentUtilities.ApplyTheme(
            MarkdownEditorControl.Document,
            this,
            isLightTheme);
        markdownAppliedLightTheme = isLightTheme;
        ScheduleMarkdownTableLayout();
    }

    /// <summary>
    /// Re-theming walks and restyles the whole document, which invalidates layout for every block.
    /// On window activation it is only needed when the system theme changed while the window was inactive.
    /// </summary>
    private void ApplyMarkdownThemeIfChanged()
    {
        if (markdownAppliedLightTheme == SystemThemeUtility.IsLightTheme())
            return;

        ApplyMarkdownTheme();
    }

    private void ApplyMarkdownWrapSetting()
    {
        if (MarkdownEditorControl.Document is null)
            return;

        // Keep horizontal scrolling available even when wrapping if a table needs a minimum page width.
        MarkdownEditorControl.Document.PageWidth = WrapTextMenuItem.IsChecked ? double.NaN : 4000;
        ScheduleMarkdownTableLayout();
    }

    private void ScheduleMarkdownTableLayout()
    {
        if (markdownTableLayoutPending)
            return;

        markdownTableLayoutPending = true;
        _ = Dispatcher.BeginInvoke(() =>
        {
            markdownTableLayoutPending = false;
            if (_isClosed || editorMode != EtwEditorMode.Markdown || isApplyingMarkdownDocument
                || MarkdownEditorControl.ViewportWidth <= 0)
                return;

            // WPF raises TextChanged for TableColumn.Width writes, even though no text changed.
            isApplyingMarkdownDocument = true;
            try
            {
                MarkdownFlowDocumentUtilities.FitTableColumns(MarkdownEditorControl.Document,
                    MarkdownEditorControl.ViewportWidth, VisualTreeHelper.GetDpi(MarkdownEditorControl).PixelsPerDip);
            }
            finally
            {
                isApplyingMarkdownDocument = false;
            }
        }, DispatcherPriority.Background);
    }

    private void ReloadMarkdownDocumentAndRestoreCaret(int targetPlainTextOffset)
    {
        SyncMarkdownTextFromDocument();
        LoadMarkdownDocumentFromText(Session.Text);

        if (MarkdownEditorControl.Document is null)
            return;

        TextPointer caretPosition = GetMarkdownTextPointerAtPlainTextOffset(targetPlainTextOffset);
        MarkdownEditorControl.Selection.Select(caretPosition, caretPosition);
    }

    private int GetMarkdownPlainTextOffset(TextPointer position)
    {
        if (MarkdownEditorControl.Document is null)
            return 0;

        return new TextRange(MarkdownEditorControl.Document.ContentStart, position).Text.Length;
    }

    private TextPointer GetMarkdownTextPointerAtPlainTextOffset(int targetPlainTextOffset)
    {
        if (MarkdownEditorControl.Document is null)
            return MarkdownEditorControl.CaretPosition;

        TextPointer navigator = MarkdownEditorControl.Document.ContentStart;
        TextPointer lastInsertionPosition = navigator;

        while (navigator is not null)
        {
            int currentOffset = new TextRange(MarkdownEditorControl.Document.ContentStart, navigator).Text.Length;
            if (currentOffset >= targetPlainTextOffset)
                return navigator;

            lastInsertionPosition = navigator;
            TextPointer? next = navigator.GetNextInsertionPosition(LogicalDirection.Forward);
            if (next is null)
                break;

            navigator = next;
        }

        return lastInsertionPosition;
    }

    private static string EscapeMarkdownTableCell(string value)
    {
        return value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "<br />", StringComparison.Ordinal);
    }

    internal void EnterMarkdownMode() => SetEditorMode(EtwEditorMode.Markdown);

    private void EnterMarkdownMode_Click(object sender, RoutedEventArgs e) => EnterMarkdownMode();

    private void OpenNewWindowWithMarkdownSelection()
    {
        TextSelection selection = MarkdownEditorControl.Selection;
        bool hasSelection = !selection.IsEmpty;
        string selectedText = selection.Text;

        EditTextWindow newEtwWithText = CreateSelectionWindow(
            selectedText,
            hasSelection ? EtwEditorMode.Markdown : EtwEditorMode.Text);
        newEtwWithText.Show();
    }

    private void MarkdownEditorControl_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Markdown || isApplyingMarkdownDocument)
            return;

        UpdateMarkdownFormattingBar();
        UpdateLineAndColumnText();
    }

    private void UpdateMarkdownFormattingBar()
    {
        bool isMarkdown = editorMode == EtwEditorMode.Markdown;
        bool canEdit = isMarkdown && !MarkdownEditorControl.IsReadOnly;
        MarkdownToolbar.Visibility = isMarkdown ? Visibility.Visible : Visibility.Collapsed;
        MarkdownToolbar.UpdateAvailableActions(canEdit
            ? MarkdownFlowDocumentUtilities.GetAvailableFormatting(MarkdownEditorControl)
            : MarkdownFormattingAction.None, canEdit);
    }

    private void ApplyMarkdownFormat(MarkdownFormattingAction action, int headingLevel) =>
        EditMarkdownDocument(() => MarkdownFlowDocumentUtilities.ApplyFormatting(
            MarkdownEditorControl, action, headingLevel, this, SystemThemeUtility.IsLightTheme()));

    private void InsertMarkdownTable(int columns, int rows) =>
        EditMarkdownDocument(() => MarkdownFlowDocumentUtilities.InsertTable(
            MarkdownEditorControl, columns, rows, this, SystemThemeUtility.IsLightTheme()));

    private void EditMarkdownDocument(Action edit)
    {
        if (editorMode != EtwEditorMode.Markdown || MarkdownEditorControl.IsReadOnly)
            return;

        MarkdownEditorControl.Focus();
        // The toolbar changes the rendered document directly. Don't live-promote it as typed Markdown.
        isApplyingMarkdownDocument = true;
        try
        {
            using (MarkdownEditorControl.DeclareChangeBlock())
                edit();
        }
        finally
        {
            isApplyingMarkdownDocument = false;
        }

        SyncMarkdownTextFromDocument();
        UpdateMarkdownFormattingBar();
        UpdateLineAndColumnText();
        ScheduleMarkdownTableLayout();
    }

    private void MarkdownEditorControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Markdown)
            return;

        UpdateLineAndColumnText();
        SetMargins(MarginsMenuItem.IsChecked is true);
    }

    private void MarkdownEditorControl_TextChanged(object sender, TextChangedEventArgs e)
    {
        HideMarkdownBlockActions();
        if (isApplyingMarkdownDocument)
            return;

        SyncMarkdownTextFromDocument();
        if (editorMode == EtwEditorMode.Markdown)
            UpdateMarkdownFormattingBar();
        UpdateLineAndColumnText();
        ScheduleMarkdownTableLayout();

        TextPointer caret = MarkdownEditorControl.CaretPosition;
        if (caret.Paragraph is not Paragraph currentParagraph
            || MarkdownFlowDocumentUtilities.IsCodeBlock(currentParagraph)
            || !MarkdownDocumentUtilities.ShouldPromoteLiveMarkdown(new TextRange(currentParagraph.ContentStart, currentParagraph.ContentEnd).Text))
            return;

        int caretOffset = GetMarkdownPlainTextOffset(caret);
        Dispatcher.BeginInvoke(
            () =>
            {
                if (editorMode != EtwEditorMode.Markdown || isApplyingMarkdownDocument)
                    return;

                ReloadMarkdownDocumentAndRestoreCaret(caretOffset);
                UpdateLineAndColumnText();
            },
            DispatcherPriority.Background);
    }

    private void MarkdownTaskListCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isApplyingMarkdownDocument || e.OriginalSource is not DependencyObject source
            || !MarkdownFlowDocumentUtilities.IsTaskListCheckBox(source))
            return;

        SyncMarkdownTextFromDocument();
        UpdateLineAndColumnText();
    }

    private Paragraph? hoveredCodeParagraph;
    private Table? hoveredMarkdownTable;

    private void MarkdownEditorControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) =>
        UpdateMarkdownBlockActions(e.GetPosition(MarkdownEditorControl));

    internal void UpdateMarkdownBlockActions(Point mouse, TextPointer? tapCaret = null)
    {
        if (editorMode != EtwEditorMode.Markdown)
            return;

        // Blank space to the right of short code lines doesn't hit-test as text, so while the
        // buttons are showing, stay put anywhere within the block's vertical extent. That lets
        // the mouse travel across the block to the top-right corner without losing them.
        if (hoveredCodeParagraph is not null && CodeBlockActionsPanel.Visibility == Visibility.Visible)
        {
            Rect first = hoveredCodeParagraph.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            Rect last = hoveredCodeParagraph.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
            if (mouse.Y >= first.Top - 8 && mouse.Y <= last.Bottom + 8)
                return;
        }

        if (hoveredMarkdownTable is not null && IsWithinMarkdownTableHeight(hoveredMarkdownTable, mouse.Y, out _))
            return;

        TextPointer? pointer = tapCaret ?? MarkdownEditorControl.GetPositionFromPoint(mouse, false);
        TextPointer? tablePointer = pointer ?? MarkdownEditorControl.GetPositionFromPoint(mouse, true);
        if (FindParent<Table>(tablePointer?.Parent) is Table table && IsWithinMarkdownTableHeight(table, mouse.Y, out Rect start))
        {
            HideCodeBlockActions();
            ShowMarkdownTableAction(table, start.TopLeft);
            return;
        }

        HideMarkdownTableAction();
        ShowCodeBlockActionsFor(pointer is null ? null : FindParent<Paragraph>(pointer.Parent));
    }

    private void MarkdownEditorControl_TouchTapped(object sender, TouchTappedEventArgs e) =>
        UpdateMarkdownBlockActions(e.Position, MarkdownEditorControl.CaretPosition);

    private static bool IsWithinMarkdownTableHeight(Table table, double y, out Rect start)
    {
        start = table.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        Rect end = table.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
        return !start.IsEmpty && !end.IsEmpty && y >= start.Top - 6 && y <= end.Bottom;
    }

    private void ShowMarkdownTableAction(Table table, Point blockStart)
    {
        hoveredMarkdownTable = table;
        Point inGrid = MarkdownEditorControl.TranslatePoint(blockStart, (UIElement)OpenMarkdownTableButton.Parent);
        bool hasScrollBar = WindowUtilities.GetScrollViewer(MarkdownEditorControl)?.ComputedVerticalScrollBarVisibility == Visibility.Visible;
        double right = 4 + (hasScrollBar ? SystemParameters.VerticalScrollBarWidth : 0);
        OpenMarkdownTableButton.Margin = new Thickness(0, Math.Max(0, inGrid.Y + 2), right, 0);
        OpenMarkdownTableButton.Visibility = Visibility.Visible;
    }

    private void HideMarkdownTableAction()
    {
        hoveredMarkdownTable = null;
        OpenMarkdownTableButton.Visibility = Visibility.Collapsed;
    }

    /// <summary>Shows the copy and open buttons when <paramref name="paragraph"/> is a code block, or hides them.</summary>
    private void ShowCodeBlockActionsFor(Paragraph? paragraph)
    {
        if (!MarkdownFlowDocumentUtilities.TryGetCodeBlock(paragraph, out _, out _))
        {
            HideCodeBlockActions();
            return;
        }

        if (ReferenceEquals(paragraph, hoveredCodeParagraph) && CodeBlockActionsPanel.Visibility == Visibility.Visible)
            return;

        hoveredCodeParagraph = paragraph;
        Rect blockStart = paragraph!.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        Point inGrid = MarkdownEditorControl.TranslatePoint(blockStart.TopLeft, (UIElement)CodeBlockActionsPanel.Parent);
        // Inside the block's top-right corner, so reaching the buttons never leaves the block.
        CodeBlockActionsPanel.Margin = new Thickness(0, Math.Max(0, inGrid.Y + 2), 28, 0);
        CodeBlockActionsPanel.Visibility = Visibility.Visible;
    }

    private void HideCodeBlockActions()
    {
        hoveredCodeParagraph = null;
        CodeBlockActionsPanel.Visibility = Visibility.Collapsed;
    }

    private void MarkdownEditorControl_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!CodeBlockActionsPanel.IsMouseOver && !OpenMarkdownTableButton.IsMouseOver)
            HideMarkdownBlockActions();
    }

    private void MarkdownBlockActions_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!MarkdownEditorControl.IsMouseOver)
            HideMarkdownBlockActions();
    }

    private void HideMarkdownBlockActions()
    {
        HideCodeBlockActions();
        HideMarkdownTableAction();
    }

    private void MarkdownEditorControl_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 || e.ExtentHeightChange != 0)
            HideMarkdownBlockActions();

        if (e.ViewportWidthChange != 0)
        {
            HideMarkdownTableAction();
            ScheduleMarkdownTableLayout();
        }
    }

    private void OpenMarkdownTableButton_Click(object sender, RoutedEventArgs e) =>
        CreateSpreadsheetWindowForHoveredMarkdownTable()?.Show();

    internal EditTextWindow? CreateSpreadsheetWindowForHoveredMarkdownTable()
    {
        if (hoveredMarkdownTable is not Table table)
            return null;

        EditTextTableDocument document = MarkdownFlowDocumentUtilities.CreateSpreadsheetDocument(table);
        EditTextWindow window = new(document.SerializeToText(), false) { tableDocument = document };
        window.EnterSpreadsheetMode();
        return window;
    }

    private void CopyCodeBlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (!MarkdownFlowDocumentUtilities.TryGetCodeBlock(hoveredCodeParagraph, out string code, out _))
            return;

        try { System.Windows.Clipboard.SetDataObject(code, true); } catch { }
    }

    private void OpenCodeBlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (!MarkdownFlowDocumentUtilities.TryGetCodeBlock(hoveredCodeParagraph, out string code, out string fenceInfo))
            return;

        EditTextWindow codeWindow = new(code, false);
        codeWindow.SetCodeLanguage(CodeLanguages.FindByFenceInfo(fenceInfo)?.Name);
        codeWindow.SetEditorMode(EtwEditorMode.Code);
        codeWindow.Show();
    }

    private void MarkdownEditorControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (editorMode == EtwEditorMode.Markdown && e.Key == Key.Tab && !MarkdownEditorControl.IsReadOnly
            && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            e.Handled = MarkdownTabBehavior.TryHandle(MarkdownEditorControl, outdent: Keyboard.Modifiers == ModifierKeys.Shift);
            return;
        }

        if (editorMode != EtwEditorMode.Markdown || e.Key != Key.Enter
            || Keyboard.Modifiers != ModifierKeys.None)
            return;

        TextSelection selection = MarkdownEditorControl.Selection;
        if (IsSelectionInMarkdownCodeBlock(selection))
        {
            using (MarkdownEditorControl.DeclareChangeBlock())
            {
                if (!selection.IsEmpty)
                    selection.Text = string.Empty;
                MarkdownEditorControl.CaretPosition = selection.Start.InsertLineBreak().GetInsertionPosition(LogicalDirection.Forward);
            }
            e.Handled = true;
            return;
        }

        if (!selection.IsEmpty || FindParent<ListItem>(MarkdownEditorControl.CaretPosition.Parent) is not ListItem item
            || !MarkdownFlowDocumentUtilities.IsTaskListItem(item))
            return;

        // Enter on a still-empty to-do ends the list, same as bullets and numbers do:
        // drop the checkbox and let the default handling leave the list.
        if (MarkdownFlowDocumentUtilities.IsEmptyTaskListItem(item))
        {
            MarkdownFlowDocumentUtilities.RemoveTaskListMarker(item);
            MarkdownEditorControl.CaretPosition = item.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            return;
        }

        // Otherwise continue the list with a fresh unchecked checkbox.
        e.Handled = true;
        EditingCommands.EnterParagraphBreak.Execute(null, MarkdownEditorControl);

        if (FindParent<ListItem>(MarkdownEditorControl.CaretPosition.Parent) is ListItem newItem
            && newItem != item
            && !MarkdownFlowDocumentUtilities.IsTaskListItem(newItem)
            && newItem.Blocks.FirstBlock is Paragraph newParagraph)
        {
            MarkdownEditorControl.CaretPosition = MarkdownFlowDocumentUtilities.InsertTaskListMarker(newParagraph);
        }
    }

    private void MarkdownEditorControl_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (editorMode != EtwEditorMode.Markdown || e.Text != " ")
            return;

        Paragraph? paragraph = FindParent<Paragraph>(MarkdownEditorControl.CaretPosition.Parent);
        if (paragraph is null || MarkdownFlowDocumentUtilities.IsCodeBlock(paragraph))
            return;

        string lineTextBeforeSpace = new TextRange(paragraph.ContentStart, MarkdownEditorControl.CaretPosition).Text;
        if (!MarkdownDocumentUtilities.ShouldPromoteLiveBlock(lineTextBeforeSpace))
            return;

        int paragraphStartOffset = GetMarkdownPlainTextOffset(paragraph.ContentStart);
        Dispatcher.BeginInvoke(
            () =>
            {
                if (editorMode != EtwEditorMode.Markdown || isApplyingMarkdownDocument)
                    return;

                ReloadMarkdownDocumentAndRestoreCaret(paragraphStartOffset);
                UpdateLineAndColumnText();
            },
            DispatcherPriority.Background);
    }

    private void MarkdownEditorControl_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (editorMode != EtwEditorMode.Markdown)
            return;

        string? pastedText = e.DataObject.GetData(System.Windows.DataFormats.UnicodeText) as string
            ?? e.DataObject.GetData(System.Windows.DataFormats.Text) as string;
        if (string.IsNullOrEmpty(pastedText))
            return;

        e.CancelCommand();

        TextSelection selection = MarkdownEditorControl.Selection;
        if (IsSelectionInMarkdownCodeBlock(selection))
        {
            using (MarkdownEditorControl.DeclareChangeBlock())
            {
                if (!selection.IsEmpty)
                    selection.Text = string.Empty;
                TextPointer insertion = selection.Start.GetPositionAtOffset(0, LogicalDirection.Forward)!;
                insertion.InsertTextInRun(MarkdownDocumentUtilities.NormalizeNewlines(pastedText));
                MarkdownEditorControl.CaretPosition = insertion;
            }
            return;
        }

        bool shouldParseAsMarkdown = MarkdownDocumentUtilities.LooksLikeMarkdown(pastedText);
        int selectionStartOffset = GetMarkdownPlainTextOffset(MarkdownEditorControl.Selection.Start);
        int renderedPasteLength = shouldParseAsMarkdown
            ? MarkdownFlowDocumentUtilities.GetDocumentPlainText(
                MarkdownFlowDocumentUtilities.CreateFlowDocument(
                    pastedText,
                    MarkdownEditorControl.FontFamily,
                    MarkdownEditorControl.FontSize,
                    OpenedFilePath)).Length
            : pastedText.Length;

        MarkdownEditorControl.Selection.Text = pastedText;

        if (shouldParseAsMarkdown)
        {
            Dispatcher.BeginInvoke(
                () =>
                {
                    if (editorMode != EtwEditorMode.Markdown || isApplyingMarkdownDocument)
                        return;

                    ReloadMarkdownDocumentAndRestoreCaret(selectionStartOffset + renderedPasteLength);
                    UpdateLineAndColumnText();
                },
                DispatcherPriority.Background);
        }
    }

    private async void MarkdownEditorControl_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        await ExternalLinkLauncher.OpenAsync(e.Uri.OriginalString);
    }

    private static bool IsSelectionInMarkdownCodeBlock(TextSelection selection) =>
        selection.Start.Paragraph is Paragraph paragraph && selection.End.Paragraph == paragraph
        && MarkdownFlowDocumentUtilities.IsCodeBlock(paragraph);
}
