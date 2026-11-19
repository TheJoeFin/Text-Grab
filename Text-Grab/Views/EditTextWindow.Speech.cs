using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Text_Grab.Models;
using Text_Grab.Services;

namespace Text_Grab;

public partial class EditTextWindow
{
    public static readonly RoutedCommand ReadAloudCmd = new();
    public static readonly RoutedCommand StopSpeakingCmd = new();

    private TtsService? speechMenuService;

    private void ReadAloudCanExecute(object sender, CanExecuteRoutedEventArgs e)
        => e.CanExecute = GetSpeechTextSegments().Any(text => !string.IsNullOrWhiteSpace(text));

    private void ReadAloudExecuted(object sender, ExecutedRoutedEventArgs e)
        => ServiceAccess.GetRequired<TtsService>().Speak(
            string.Join(Environment.NewLine, GetSpeechTextSegments()));

    private void StopSpeakingCanExecute(object sender, CanExecuteRoutedEventArgs e)
        => e.CanExecute = ServiceAccess.GetRequired<TtsService>().IsBusy;

    private void StopSpeakingExecuted(object sender, ExecutedRoutedEventArgs e)
        => ServiceAccess.GetRequired<TtsService>().Stop();

    private IEnumerable<string> GetSpeechTextSegments()
    {
        if (editorMode == EtwEditorMode.Markdown)
        {
            TextSelection selection = MarkdownEditorControl.Selection;
            return [selection.IsEmpty
                ? new TextRange(MarkdownEditorControl.Document.ContentStart, MarkdownEditorControl.Document.ContentEnd).Text
                : selection.Text];
        }

        if (editorMode == EtwEditorMode.Spreadsheet && activeSpreadsheetCellEditor is { } cellEditor)
            return [cellEditor.SelectionLength > 0 ? cellEditor.SelectedText : cellEditor.Text];

        return GetSelectedOrAllTextSegmentsForPreview();
    }

    private void SpeechMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (e.Source != sender || speechMenuService is not null)
            return;

        speechMenuService = ServiceAccess.GetRequired<TtsService>();
        speechMenuService.BusyChanged += SpeechService_BusyChanged;
    }

    private void SpeechMenu_SubmenuClosed(object sender, RoutedEventArgs e)
    {
        if (e.Source == sender)
            UnsubscribeFromSpeechMenu();
    }

    private void UnsubscribeFromSpeechMenu()
    {
        if (speechMenuService is not TtsService service)
            return;

        service.BusyChanged -= SpeechService_BusyChanged;
        speechMenuService = null;
    }

    private void SpeechService_BusyChanged(bool isBusy)
        => Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
}
