using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
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
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Text_Grab;

public partial class EditTextWindow
{
    private async void SummarizeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetToLoading("Summarizing...");

        WinAiGenerationResult? failure = null;
        bool resultUnchanged = false;

        try
        {
            string sourceText = GetSelectedTextOrAllText();
            WinAiGenerationResult result = await WindowsAiUtilities.SummarizeParagraph(sourceText);

            if (result.Text is null)
                failure = result;
            else
                resultUnchanged = !DeliverLocalAiResult("Summarize", sourceText, result.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Summarize exception: {ex.Message}");
            failure = WinAiGenerationResult.Failed(WinAiFailure.ModelError, $"Summarizing failed: {ex.Message}");
        }
        finally
        {
            SetToLoaded();
        }

        if (failure is { } summaryFailure)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Summarize Failed",
                Content = summaryFailure.Message ?? "The text could not be summarized.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
        else if (resultUnchanged)
        {
            await ShowLocalAiResultUnchangedAsync("Summarize");
        }
    }

    private async void MeetingNotesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetToLoading("Writing meeting notes...");

        WinAiGenerationResult? failure = null;
        bool resultUnchanged = false;

        try
        {
            string sourceText = GetSelectedTextOrAllText();

            // A long transcript is summarized part by part, so say which part is being read.
            void OnProgress(string stage) => Dispatcher.Invoke(() => SetToLoading(stage));

            WinAiGenerationResult result = await WinAiMeetingNotes.SummarizeAsync(sourceText, OnProgress);

            if (result.Text is null)
                failure = result;
            else
                resultUnchanged = !DeliverLocalAiResult("Meeting notes", sourceText, result.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Meeting notes exception: {ex.Message}");
            failure = WinAiGenerationResult.Failed(WinAiFailure.ModelError, $"Meeting notes failed: {ex.Message}");
        }
        finally
        {
            SetToLoaded();
        }

        if (failure is { } notesFailure)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Meeting Notes Failed",
                Content = notesFailure.Message ?? "The text could not be written up as meeting notes.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
        else if (resultUnchanged)
        {
            await ShowLocalAiResultUnchangedAsync("Meeting notes");
        }
    }

    // Returns the new window's WindowId so a completion notification can reactivate it specifically
    // (rather than the source window, which never gets the result text) — null if it failed to open.
    private static Guid? OpenTextInNewEditTextWindow(string text, EtwEditorMode resultMode = EtwEditorMode.Text)
    {
        EditTextWindow resultWindow = new(text, isEncoded: false);

        try
        {
            // Set before Show(), matching CreateSelectionWindow — the mode-switch logic in
            // SetEditorMode only touches XAML elements, which InitializeComponent() already wired up.
            if (resultMode != EtwEditorMode.Text)
                resultWindow.SetEditorMode(resultMode);

            resultWindow.Show();
            return resultWindow.WindowId;
        }
        catch (Exception ex)
        {
            _ = new Wpf.Ui.Controls.MessageBox
            {
                Title = ex.Message,
                Content = "An error occurred while trying to open a new window. Please try again.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return null;
        }
    }

    /// <summary>
    /// Delivers a finished Local AI result (Summarize, Meeting Notes) per the
    /// "Send Result to New Window" setting: either opened in a new window, leaving this window's text
    /// untouched, or applied over the selected/all text in this window (one anchor cell in
    /// spreadsheet mode, preserving the other cells) — then fires the completion
    /// notification for whichever window actually ended up with the result. Returns false without
    /// doing any of that when the result is the same as <paramref name="sourceText"/>: a new window
    /// holding a copy of the input (or an in-place "edit" that changes nothing) would only look like
    /// the task did something. The caller should tell the user instead, once the window is
    /// re-enabled (see <see cref="ShowLocalAiResultUnchangedAsync"/>).
    /// </summary>
    private bool DeliverLocalAiResult(string taskDescription, string sourceText, string resultText)
    {
        if (LocalAiResultUtilities.IsUnchanged(sourceText, resultText))
            return false;

        if (DefaultSettings.SendLocalAiResultToNewWindow)
        {
            if (OpenTextInNewEditTextWindow(resultText) is Guid resultWindowId)
                NotifyLocalAiComplete(taskDescription, resultWindowId);
        }
        else
        {
            if (editorMode == EtwEditorMode.Spreadsheet)
            {
                int targetRow = Math.Max(0, GetSpreadsheetCurrentRowIndex() ?? 0);
                int targetColumn = Math.Max(0, GetSpreadsheetCurrentColumnIndex() ?? 0);
                ApplySpreadsheetDocumentChange(
                    document => WriteAggregateResultIntoSpreadsheetDocument(document, resultText, targetRow, targetColumn),
                    targetRow,
                    targetColumn,
                    beginEdit: false);
            }
            else
            {
                ReplaceSelectedTextOrAllText(resultText);
            }

            NotifyLocalAiComplete(taskDescription, WindowId);
        }

        return true;
    }

    /// <summary>
    /// Tells the user a Local AI task finished but produced the same text it started with, so
    /// nothing was opened or replaced. Show this after <see cref="SetToLoaded"/> — the window is
    /// disabled while the task runs.
    /// </summary>
    private static async Task ShowLocalAiResultUnchangedAsync(string taskDescription)
    {
        await new Wpf.Ui.Controls.MessageBox
        {
            Title = "No Changes",
            Content = $"{taskDescription} finished, but the result is the same as the original text, so nothing was changed.",
            CloseButtonText = "OK"
        }.ShowDialogAsync();
    }

    /// <summary>
    /// Runs a Local AI text transform (Rewrite, Convert to Table) and delivers it per the "Send Result
    /// to New Window" setting: either opened in a new window, leaving this window's text untouched, or
    /// applied over the selected/all text in this window (via <see cref="ApplySelectedTextOrAllTextTransformAsync"/>,
    /// which also handles spreadsheet mode) — then fires the completion notification for whichever
    /// window actually ended up with the result. <paramref name="resultMode"/> switches whichever
    /// window gets the result into that editor mode (e.g. Convert to Table switches to Spreadsheet);
    /// pass <see cref="EtwEditorMode.Text"/> to leave the mode alone. Returns false, having opened
    /// nothing and notified no one, when the model handed back the same text it was given (every
    /// cell, in spreadsheet mode) — the caller should tell the user via
    /// <see cref="ShowLocalAiResultUnchangedAsync"/> once the window is re-enabled.
    /// </summary>
    private async Task<bool> PerformLocalAiTransformAsync(string taskDescription, Func<string, Task<string>> transformAsync, EtwEditorMode resultMode = EtwEditorMode.Text)
    {
        if (DefaultSettings.SendLocalAiResultToNewWindow)
        {
            string sourceText = GetSelectedTextOrAllText();
            string resultText = await transformAsync(sourceText);

            if (LocalAiResultUtilities.IsUnchanged(sourceText, resultText))
                return false;

            if (OpenTextInNewEditTextWindow(resultText, resultMode) is Guid resultWindowId)
                NotifyLocalAiComplete(taskDescription, resultWindowId);
        }
        else
        {
            // In spreadsheet mode the transform runs once per cell, so "unchanged" means no cell
            // came back different, not just the last one.
            bool anyChanged = false;

            await ApplySelectedTextOrAllTextTransformAsync(async text =>
            {
                string resultText = await transformAsync(text);
                anyChanged |= !LocalAiResultUtilities.IsUnchanged(text, resultText);
                return resultText;
            });

            if (!anyChanged)
                return false;

            if (resultMode != EtwEditorMode.Text)
                SetEditorMode(resultMode);

            NotifyLocalAiComplete(taskDescription, WindowId);
        }

        return true;
    }

    private void SendLocalAiResultToNewWindowMenuItem_Checked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.SendLocalAiResultToNewWindow = true;
        DefaultSettings.Save();
    }

    private void SendLocalAiResultToNewWindowMenuItem_Unchecked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.SendLocalAiResultToNewWindow = false;
        DefaultSettings.Save();
    }

    private void NotifyOnLocalAiCompleteMenuItem_Checked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.NotifyOnLocalAiComplete = true;
        DefaultSettings.Save();
    }

    private void NotifyOnLocalAiCompleteMenuItem_Unchecked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.NotifyOnLocalAiComplete = false;
        DefaultSettings.Save();
    }

    /// <summary>
    /// Shows a completion toast for a Local AI task if the user opted in. These tasks run with the
    /// window disabled (see <see cref="SetToLoading"/>), so a user who has switched away otherwise
    /// has no signal that the result is ready, mirroring the transcription-complete notification.
    /// <paramref name="windowId"/> is whichever window actually holds the result: this window for an
    /// in-place edit (Rewrite, Convert to Table, Translate, Extract RegEx), or the new window for
    /// Summarize/Meeting Notes, which open the result separately instead of replacing the source text.
    /// </summary>
    private void NotifyLocalAiComplete(string taskDescription, Guid windowId)
    {
        if (DefaultSettings.NotifyOnLocalAiComplete)
            NotificationUtilities.ShowLocalAiCompleteToast(taskDescription, windowId);
    }

    private async void RestartLocalLlmMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RestartLocalLlmMenuItem.IsEnabled = false;

        try
        {
            await WindowsAiUtilities.RestartWindowsAiAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Restarting the Local LLM failed: {ex.Message}");
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Restart Local LLM Failed",
                Content = $"The Local LLM could not be restarted: {ex.Message}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
        finally
        {
            RestartLocalLlmMenuItem.IsEnabled = true;
        }
    }

    private void LearnAiMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string url = "https://learn.microsoft.com/en-us/windows/ai/apis/phi-silica";
        Uri source = new(url, UriKind.Absolute);
        System.Windows.Navigation.RequestNavigateEventArgs ev = new(source, url);
        Process.Start(new ProcessStartInfo(ev.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private async void RewriteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetToLoading("Rewriting...");

        bool resultChanged;
        try
        {
            resultChanged = await PerformLocalAiTransformAsync("Rewrite", text => WindowsAiUtilities.Rewrite(text));
        }
        finally
        {
            SetToLoaded();
        }

        if (!resultChanged)
            await ShowLocalAiResultUnchangedAsync("Rewrite");
    }

    private async void ConvertTableMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetToLoading("Converting...");

        bool resultChanged;
        try
        {
            resultChanged = await PerformLocalAiTransformAsync("Convert to Table", text => WindowsAiUtilities.TextToTable(text), EtwEditorMode.Spreadsheet);
        }
        finally
        {
            SetToLoaded();
        }

        if (!resultChanged)
            await ShowLocalAiResultUnchangedAsync("Convert to Table");
    }

    private async void TranslateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string targetLanguage)
            return;

        await PerformTranslationAsync(targetLanguage);
    }

    private async void TranslateToSystemLanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Get system language using the helper from LanguageUtilities
        string systemLanguage = LanguageUtilities.GetSystemLanguageForTranslation();
        await PerformTranslationAsync(systemLanguage);
    }

    private async void TranslateToEnglish_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("English");

    private async void TranslateToSpanish_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("Spanish");

    private async void TranslateToFrench_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("French");

    private async void TranslateToGerman_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("German");

    private async void TranslateToItalian_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("Italian");

    private async void TranslateToPortuguese_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("Portuguese");

    private async void TranslateToRussian_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("Russian");

    private async void TranslateToJapanese_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("Japanese");

    private async void TranslateToChineseSimplified_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("Chinese (Simplified)");

    private async void TranslateToKorean_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("Korean");

    private async void TranslateToArabic_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("Arabic");

    private async void TranslateToHindi_Click(object sender, RoutedEventArgs e) => await PerformTranslationAsync("Hindi");

    private async Task PerformTranslationAsync(string targetLanguage)
    {
        SetToLoading($"Translating to {targetLanguage}...");

        // Captured from inside the transform so a failure can be reported after the text is applied
        // instead of silently putting the original text back.
        TranslationResult? failedResult = null;

        // Whichever window ends up with the translated text — this window for an in-place apply, or
        // the newly opened one — so the completion notification (if any) points at the right one.
        Guid? resultWindowId = null;

        // Set when the model handed back the text it was given, so no window is opened and the
        // user is told nothing changed instead of getting a completion notification.
        bool resultUnchanged = false;

        try
        {
            if (DefaultSettings.SendLocalAiResultToNewWindow)
            {
                string sourceText = GetSelectedTextOrAllText();
                TranslationResult result = await WinAiTranslator.TranslateAsync(sourceText, targetLanguage);

                if (!result.Succeeded)
                    failedResult = result;
                else if (LocalAiResultUtilities.IsUnchanged(sourceText, result.Text))
                    resultUnchanged = true;
                else
                    resultWindowId = OpenTextInNewEditTextWindow(result.Text);
            }
            else
            {
                // Runs per cell in spreadsheet mode, so "unchanged" means no cell came back different.
                bool anyChanged = false;

                await ApplySelectedTextOrAllTextTransformAsync(async text =>
                {
                    TranslationResult result = await WinAiTranslator.TranslateAsync(text, targetLanguage);

                    if (!result.Succeeded)
                        failedResult ??= result;
                    else
                        anyChanged |= !LocalAiResultUtilities.IsUnchanged(text, result.Text);

                    return result.Text;
                });

                if (failedResult is null)
                {
                    if (anyChanged)
                        resultWindowId = WindowId;
                    else
                        resultUnchanged = true;
                }
            }
        }
        catch (Exception ex)
        {
            failedResult = new TranslationResult(string.Empty, TranslationFailure.ModelError, $"Translation failed: {ex.Message}");
        }
        finally
        {
            SetToLoaded();
        }

        if (failedResult is { } failure)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = failure.Failure is TranslationFailure.NotNeeded ? "Nothing to Translate" : "Translation Failed",
                Content = failure.Message ?? "The text could not be translated.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
        else if (resultUnchanged)
        {
            await ShowLocalAiResultUnchangedAsync($"Translation to {targetLanguage}");
        }
        else if (resultWindowId is Guid windowId)
        {
            NotifyLocalAiComplete($"Translation to {targetLanguage}", windowId);
        }
    }

    private async void ExtractRegexMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string textDescription = GetSelectedTextOrAllText();

        if (string.IsNullOrWhiteSpace(textDescription))
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "No Text",
                Content = "Please enter or select text to extract a regex pattern from.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return;
        }

        SetToLoading("Extracting RegEx pattern...");

        WinAiGenerationResult extraction;
        try
        {
            extraction = await WindowsAiUtilities.ExtractRegex(textDescription);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Regex extraction exception: {ex.Message}");
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Error",
                Content = $"An error occurred while extracting regex: {ex.Message}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            SetToLoaded();
            return;
        }

        SetToLoaded();

        if (extraction.Text is not string regexPattern)
        {
            // The shared language model reports why it could not answer, so show that instead of a
            // guess about what went wrong.
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Extraction Failed",
                Content = extraction.Message ?? "Failed to extract a regex pattern.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return;
        }

        NotifyLocalAiComplete("Extract RegEx", WindowId);

        // Clean up any model artifacts like <\/PRED> tags
        regexPattern = regexPattern.Replace("<\\/PRED>", "").Replace("</PRED>", "").Trim();

        // Create detailed explanation using the ExplainRegexPattern extension method
        string explanation = regexPattern.ExplainRegexPattern();

        // Create a selectable TextBox for the message box content
        System.Windows.Controls.TextBox explanationTextBox = new()
        {
            Text = explanation,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 400
        };

        // Show message box with Copy and Cancel buttons
        Wpf.Ui.Controls.MessageBoxResult result = await new Wpf.Ui.Controls.MessageBox
        {
            Title = "RegEx Pattern Extracted",
            Content = explanationTextBox,
            PrimaryButtonText = "Copy",
            CloseButtonText = "Cancel"
        }.ShowDialogAsync();

        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            try
            {
                System.Windows.Clipboard.SetDataObject(regexPattern, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to copy regex to clipboard: {ex.Message}");
                await new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Copy Failed",
                    Content = "Failed to copy regex pattern to clipboard.",
                    CloseButtonText = "OK"
                }.ShowDialogAsync();
            }
        }
    }
}
