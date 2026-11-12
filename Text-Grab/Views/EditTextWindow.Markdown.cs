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
    /// <summary>
    /// <see cref="Editing.MarkdownSurface"/>'s half of syncing the Markdown editor from the text. The session
    /// skips it while the text is being written from the Markdown editor.
    /// </summary>
    private void RefreshMarkdownFromText(string text)
    {
        LoadMarkdownDocumentFromText(text);
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

        if (WrapTextMenuItem.IsChecked)
        {
            MarkdownEditorControl.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            MarkdownEditorControl.Document.PageWidth = double.NaN;
        }
        else
        {
            MarkdownEditorControl.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            MarkdownEditorControl.Document.PageWidth = 4000;
        }
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

    private void EnterMarkdownMode_Click(object sender, RoutedEventArgs e) => SetEditorMode(EtwEditorMode.Markdown);

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
        if (editorMode != EtwEditorMode.Markdown)
            return;

        UpdateLineAndColumnText();
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
        if (isApplyingMarkdownDocument)
            return;

        int caretOffset = GetMarkdownPlainTextOffset(MarkdownEditorControl.CaretPosition);
        string currentParagraphText = FindParent<Paragraph>(MarkdownEditorControl.CaretPosition.Parent) is Paragraph currentParagraph
            ? new TextRange(currentParagraph.ContentStart, currentParagraph.ContentEnd).Text
            : string.Empty;
        bool shouldPromoteMarkdown = MarkdownDocumentUtilities.ShouldPromoteLiveMarkdown(currentParagraphText);

        SyncMarkdownTextFromDocument();
        UpdateLineAndColumnText();

        if (!shouldPromoteMarkdown)
            return;

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

    private void MarkdownEditorControl_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (editorMode != EtwEditorMode.Markdown || e.Text != " ")
            return;

        Paragraph? paragraph = FindParent<Paragraph>(MarkdownEditorControl.CaretPosition.Parent);
        if (paragraph is null)
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
}
