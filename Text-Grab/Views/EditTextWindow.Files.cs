using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
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
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Text_Grab;

public partial class EditTextWindow
{
    private enum PendingFileCloseAction
    {
        Cancel,
        Save,
        DontSave,
        SaveToHistory,
    }

    public async Task OcrAllImagesInFolder(string folderPath, OcrDirectoryOptions options)
    {
        IEnumerable<string>? files = null;

        if (string.IsNullOrWhiteSpace(folderPath) && string.IsNullOrWhiteSpace(options.Path))
            return;

        if (string.IsNullOrWhiteSpace(folderPath))
            folderPath = options.Path;

        SearchOption searchOption = SearchOption.TopDirectoryOnly;
        if (options.IsRecursive)
            searchOption = SearchOption.AllDirectories;

        try
        {
            files = Directory.GetFiles(folderPath, "*.*", searchOption);
        }
        catch (Exception ex)
        {
            Session.AppendText($"Failed to read directory: {ex.Message}{Environment.NewLine}");
        }

        if (files is null)
            return;

        List<string> imageFiles = [.. files.Where(x => IoUtilities.IsVisualDocumentFileExtension(Path.GetExtension(x).ToLower()))];

        if (imageFiles.Count == 0)
        {
            Session.AppendText($"{folderPath} contains no images or PDFs");
            return;
        }

        ILanguage selectedLanguage = LanguageUtilities.GetOCRLanguage();
        string tesseractLanguageTag = string.Empty;

        if (LanguageMenuItem.Items.Count > 0)
        {
            foreach (MenuItem languageSubItem in LanguageMenuItem.Items)
            {
                if (languageSubItem.IsChecked)
                {
                    if (languageSubItem.Tag is ILanguage iLanguageFromTag) // Changed to ILanguage
                    {
                        selectedLanguage = iLanguageFromTag;
                    }
                    else if (languageSubItem.Tag is string langTag) // Fallback for simple string tags if any
                        tesseractLanguageTag = langTag;
                }
            }
        }

        if (!CaptureLanguageUtilities.IsStaticImageCompatible(selectedLanguage))
            selectedLanguage = CaptureLanguageUtilities.GetUiAutomationFallbackLanguage();

        if (options.OutputHeader)
        {
            Session.AppendText(folderPath);
            Session.AppendText(Environment.NewLine);
            Session.AppendText($"{imageFiles.Count} files found");

            if (!string.IsNullOrEmpty(tesseractLanguageTag))
            {
                Session.AppendText(Environment.NewLine);
                Session.AppendText($"Using {tesseractLanguageTag} from Tesseract.");
                Session.AppendText(Environment.NewLine);
                Session.AppendText("Tesseract can only run single threaded,");
                Session.AppendText(Environment.NewLine);
                Session.AppendText("May be slower if processing many images");
                Session.AppendText(Environment.NewLine);
            }
            else
            {
                Session.AppendText(Environment.NewLine);
                Session.AppendText($"Using {selectedLanguage.DisplayName} from Windows.");
                Session.AppendText(Environment.NewLine);
            }

            if (options.GrabTemplate is GrabTemplate headerTemplate)
            {
                Session.AppendText($"Using template: {headerTemplate.Name}");
                Session.AppendText(Environment.NewLine);
            }

            Session.AppendText("Press Escape to cancel");
            Session.AppendText(Environment.NewLine);
            Session.AppendText(Environment.NewLine);
        }

        cancellationTokenForDirOCR = new();
        Stopwatch stopwatch = new();
        stopwatch.Start();
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        List<AsyncOcrFileResult> ocrFileResults = [];
        foreach (string path in imageFiles)
        {
            AsyncOcrFileResult ocrFileResult = new(path);
            ocrFileResults.Add(ocrFileResult);
        }

        try
        {
            await OcrAllImagesInParallel(options, ocrFileResults, selectedLanguage, tesseractLanguageTag);

            if (options.OutputFooter)
            {
                Session.AppendText(Environment.NewLine);
                Session.AppendText($"----- COMPLETED OCR OF {imageFiles.Count} files");
            }
        }
        catch (OperationCanceledException)
        {
            Session.AppendText(Environment.NewLine);
            int countCompleted = ocrFileResults.Where(r => r.OcrResult is not null).Count();
            Session.AppendText($"----- CANCELLED OCR OF {ocrFileResults.Count - countCompleted}, Completed {countCompleted} files");
        }
        finally
        {
            cancellationTokenForDirOCR.Dispose();
        }

        Mouse.OverrideCursor = null;
        stopwatch.Stop();

        if (options.OutputFooter)
        {
            Session.AppendText(Environment.NewLine);
            Session.AppendText($"----- from {folderPath}");
            Session.AppendText(Environment.NewLine);
            Session.AppendText($"----- and took {stopwatch.Elapsed:c}");
        }
        Session.ScrollToEnd();

        GC.Collect();
        cancellationTokenForDirOCR = null;
    }

    private void ToggleWriteTxtFileForEachImage_Click(object sender, RoutedEventArgs e)
    {
        ReadFolderOfImagesWriteTxtFiles.IsChecked = !ReadFolderOfImagesWriteTxtFiles.IsChecked;
    }

    internal static string GetWindowTitle(string? openedFilePath, bool hasPendingEdits)
    {
        if (string.IsNullOrWhiteSpace(openedFilePath))
            return EditTextWindowTitle;

        string fileName = Path.GetFileName(openedFilePath);
        if (hasPendingEdits)
            fileName = $"*{fileName}";

        return $"{EditTextWindowTitle} | {fileName}";
    }

    internal static bool ShouldShowPendingFileEdits(string? openedFilePath, string savedText, string currentText)
    {
        return !string.IsNullOrWhiteSpace(openedFilePath)
            && !string.Equals(savedText, currentText, StringComparison.Ordinal);
    }

    internal static string GetDefaultSaveExtension(string? openedFilePath, EtwEditorMode editorMode, EditTextTableDocument? tableDocument)
    {
        string existingExtension = Path.GetExtension(openedFilePath ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(existingExtension))
            return existingExtension;

        return editorMode switch
        {
            EtwEditorMode.Spreadsheet => GetSpreadsheetSaveExtension(tableDocument),
            EtwEditorMode.Markdown => ".md",
            _ => ".txt"
        };
    }

    internal static int GetSaveDocumentFilterIndex(string? openedFilePath, EtwEditorMode editorMode)
    {
        string existingExtension = Path.GetExtension(openedFilePath ?? string.Empty);
        if (IoUtilities.IsSpreadsheetFileExtension(existingExtension))
            return 1;

        if (IoUtilities.IsMarkdownFileExtension(existingExtension))
            return 2;

        if (string.Equals(existingExtension, ".txt", StringComparison.OrdinalIgnoreCase))
            return 3;

        if (!string.IsNullOrWhiteSpace(existingExtension))
            return 4;

        return editorMode switch
        {
            EtwEditorMode.Spreadsheet => 1,
            EtwEditorMode.Markdown => 2,
            _ => 3
        };
    }

    internal async Task OpenPath(string pathOfFileToOpen, bool isMultipleFiles = false)
    {
        if (GrabFrameFileUtilities.IsGrabFrameFile(pathOfFileToOpen))
        {
            if (!await App.TryToOpenFilePathAsync(pathOfFileToOpen))
                System.Windows.MessageBox.Show($"Failed to open Grab Frame file:\n{pathOfFileToOpen}");
            return;
        }

        // Audio files are transcribed on-device rather than opened as text. Routing this here means
        // CLI arguments, File > Open, and drag/drop all reach the same transcription path.
        if (AudioTranscriptionUtilities.IsAudioFile(pathOfFileToOpen))
        {
            AudioDebugLog.Write($"OpenPath: audio file detected, routing to transcription: {pathOfFileToOpen}");
            await TranscribeAudioFilesAsync([pathOfFileToOpen]);
            return;
        }

        SetToLoading("Loading file...");
        try
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            var result = await FileOpenUtilities.GetContentFromPath(pathOfFileToOpen, isMultipleFiles, selectedILanguage);
            if (_isClosed || result is not (string TextContent, OpenContentKind KindOpened))
                return;

            ResetSpreadsheetUndoHistory();
            bool shouldTrackOpenedFile = KindOpened == OpenContentKind.TextFile && !isMultipleFiles;
            OpenedFilePath = shouldTrackOpenedFile ? pathOfFileToOpen : null;

            if (KindOpened == OpenContentKind.TextFile)
            {
                EtwEditorMode targetMode = isMultipleFiles
                    ? EtwEditorMode.Text
                    : IoUtilities.GetEditorModeForPath(pathOfFileToOpen);

                // The text and Markdown document are replaced just below, so don't build the old text's document.
                if (IsLoaded)
                    SetEditorMode(targetMode, deferMarkdownLoad: true);
                else
                    editorMode = targetMode;
            }

            isLoadingOpenedFile = true;
            try
            {
                Session.Text = TextContent;

                if (IsLoaded && editorMode == EtwEditorMode.Spreadsheet)
                    RefreshSpreadsheetFromText();
                else if (IsLoaded && editorMode == EtwEditorMode.Markdown)
                    RefreshMarkdownFromText();
            }
            finally
            {
                isLoadingOpenedFile = false;
                SyncTextFromActiveEditor();
                SetOpenedFileState(shouldTrackOpenedFile ? pathOfFileToOpen : null);
            }
            Singleton<HistoryService>.Instance.RecordRecentFile(pathOfFileToOpen, KindOpened);
        }
        finally
        {
            if (!_isClosed)
                SetToLoaded();
        }
    }

    private void ETWindow_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        // If dropping raw text onto the ETW let the default drag/drop events occur
        bool isText = e.Data.GetDataPresent("Text");

        if (isText)
        {
            e.Handled = false;
            return;
        }

        // After here we will now allow the dropping of "non-text" content
        e.Effects = System.Windows.DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void ETWindow_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent("Text"))
            return;

        // Mark the event as handled, so TextBox's native Drop handler is not called.
        e.Handled = true;
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        try
        {
            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop, true))
                return;

            if (e.Data.GetData(System.Windows.DataFormats.FileDrop, true) is not string[] fileNames)
                return;

            List<string> existingFiles = [.. fileNames.Where(File.Exists)];
            if (existingFiles.Count == 0)
                return;

            // Audio files are transcribed on-device rather than opened as text.
            List<string> audioFiles = [.. existingFiles.Where(AudioTranscriptionUtilities.IsAudioFile)];
            List<string> otherFiles = [.. existingFiles.Where(path => !AudioTranscriptionUtilities.IsAudioFile(path))];

            bool openAsMultiple = existingFiles.Count > 1;
            foreach (string possibleFilePath in otherFiles)
                await OpenPath(possibleFilePath, openAsMultiple);

            if (audioFiles.Count > 0)
            {
                // Drop the wait cursor; TranscribeAudioFilesAsync shows the loading overlay instead.
                Mouse.OverrideCursor = null;
                await TranscribeAudioFilesAsync(audioFiles);
            }
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void ListFilesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        FolderBrowserDialog folderBrowserDialog1 = new();
        DialogResult result = folderBrowserDialog1.ShowDialog();

        if (result is not System.Windows.Forms.DialogResult.OK)
            return;

        string chosenFolderPath = folderBrowserDialog1.SelectedPath;
        try
        {
            Session.AppendText(IoUtilities.ListFilesFoldersInDirectory(chosenFolderPath));
        }
        catch (Exception ex)
        {
            Session.AppendText($"Failed: {ex.Message}{Environment.NewLine}");
        }
    }

    private async Task OcrAllImagesInParallel(OcrDirectoryOptions options, List<AsyncOcrFileResult> ocrFileResults, ILanguage selectedLanguage, string tesseractLanguageTag)
    {
        if (cancellationTokenForDirOCR is null)
            return;

        int degreesOfParallel = 6;

        if (!string.IsNullOrEmpty(tesseractLanguageTag))
            degreesOfParallel = 1;

        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = degreesOfParallel,
            CancellationToken = cancellationTokenForDirOCR.Token
        };

        await Parallel.ForEachAsync(ocrFileResults, parallelOptions, async (ocrFile, ct) =>
        {
            ct.ThrowIfCancellationRequested();

            ocrFile.OcrResult = await OcrSourceUtilities.OcrFile(ocrFile.FilePath, selectedLanguage, options);

            // to get the TextBox to update whenever OCR Finishes:
            if (!options.WriteTxtFiles)
            {
                await System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    Session.AppendText(Environment.NewLine);
                    Session.AppendText(ocrFile.OcrResult);
                    Session.ScrollToEnd();
                });
            }
        });
    }

    private async void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Create OpenFileDialog 
        Microsoft.Win32.OpenFileDialog dlg = new()
        {
            // Set filter for file extension and default file extension 
            DefaultExt = ".txt",
            Filter = FileUtilities.GetOpenDocumentFilter(),
            DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        bool? result = dlg.ShowDialog();

        if (result is true && File.Exists(dlg.FileName))
            await OpenPath(dlg.FileName);
    }

    private async void ReadFolderOfImages_Click(object sender, RoutedEventArgs e)
    {
        FolderBrowserDialog folderBrowserDialog = new();
        DialogResult result = folderBrowserDialog.ShowDialog();

        if (result is not System.Windows.Forms.DialogResult.OK)
            return;

        string chosenFolderPath = folderBrowserDialog.SelectedPath;

        GrabTemplate? selectedTemplate = null;
        foreach (MenuItem item in GrabTemplateMenuItem.Items)
        {
            if (item.IsChecked && item.Tag is GrabTemplate grabTemplate)
            {
                selectedTemplate = grabTemplate;
                break;
            }
        }

        OcrDirectoryOptions ocrDirectoryOptions = new()
        {
            Path = chosenFolderPath,
            IsRecursive = RecursiveFoldersCheck.IsChecked is true,
            WriteTxtFiles = ReadFolderOfImagesWriteTxtFiles.IsChecked is true,
            OutputFileNames = OutputFilenamesCheck.IsChecked is true,
            OutputFooter = OutputFooterCheck.IsChecked is true,
            OutputHeader = OutputHeaderCheck.IsChecked is true,
            GrabTemplate = selectedTemplate,
        };

        if (Directory.Exists(chosenFolderPath))
            await OcrAllImagesInFolder(chosenFolderPath, ocrDirectoryOptions);
    }

    private void SaveAsBTN_Click(object sender, RoutedEventArgs e)
    {
        _ = SaveCurrentDocument(saveAs: true);
    }

    private void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        _ = SaveCurrentDocument();
    }

    private string GetDefaultSaveExtension()
    {
        return GetDefaultSaveExtension(OpenedFilePath, editorMode, tableDocument);
    }

    private int GetSaveDocumentFilterIndex()
    {
        return GetSaveDocumentFilterIndex(OpenedFilePath, editorMode);
    }

    private bool SaveCurrentDocument(bool saveAs = false)
    {
        SyncTextFromActiveEditor();

        string fileText = Session.Text;
        string? targetFilePath = saveAs ? null : OpenedFilePath;

        if (string.IsNullOrEmpty(targetFilePath))
        {
            Microsoft.Win32.SaveFileDialog dialog = new()
            {
                DefaultExt = GetDefaultSaveExtension(),
                Filter = SaveDocumentFilter,
                FilterIndex = GetSaveDocumentFilterIndex(),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() is not true)
                return false;

            targetFilePath = dialog.FileName;
        }

        bool refreshImagePaths = editorMode == EtwEditorMode.Markdown
            && !string.Equals(OpenedFilePath, targetFilePath, StringComparison.OrdinalIgnoreCase);
        int markdownCaretOffset = refreshImagePaths
            ? GetMarkdownPlainTextOffset(MarkdownEditorControl.CaretPosition)
            : 0;
        File.WriteAllText(targetFilePath, fileText);
        SetOpenedFileState(targetFilePath);
        Singleton<HistoryService>.Instance.RecordRecentFile(targetFilePath, OpenContentKind.TextFile);
        if (refreshImagePaths)
            ReloadMarkdownDocumentAndRestoreCaret(markdownCaretOffset);
        return true;
    }

    private void SetOpenedFileState(string? openedFilePath)
    {
        OpenedFilePath = openedFilePath;
        savedFileText = string.IsNullOrWhiteSpace(openedFilePath) ? string.Empty : Session.Text;
        hasPendingFileEdits = false;
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        string windowTitle = GetWindowTitle(OpenedFilePath, hasPendingFileEdits);
        Title = windowTitle;
        UiTitleBar.Title = windowTitle;
    }

    private void UpdatePendingFileEditState()
    {
        if (isLoadingOpenedFile)
            return;

        hasPendingFileEdits = ShouldShowPendingFileEdits(OpenedFilePath, savedFileText, Session.Text);
        UpdateWindowTitle();
    }

    private async Task<PendingFileCloseAction> PromptForPendingFileEditsAsync()
    {
        if (string.IsNullOrWhiteSpace(OpenedFilePath))
            return PendingFileCloseAction.Cancel;

        string fileName = Path.GetFileName(OpenedFilePath);
        PendingFileCloseAction closeButtonAction = PendingFileCloseAction.Cancel;
        Wpf.Ui.Controls.ContentDialog promptDialog = new(PendingFileCloseDialogHost)
        {
            Title = $"Save changes to {fileName}?",
            Content = "You have pending edits. Save the file, discard the changes, or keep the current version in Text Grab history.",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Don't Save",
            CloseButtonText = "Save to History",
            DefaultButton = Wpf.Ui.Controls.ContentDialogButton.Primary,
        };

        promptDialog.ButtonClicked += (_, e) =>
        {
            if (e.Button == Wpf.Ui.Controls.ContentDialogButton.Close)
                closeButtonAction = PendingFileCloseAction.SaveToHistory;
        };

        Wpf.Ui.Controls.ContentDialogResult result = await promptDialog.ShowAsync();

        if (result == Wpf.Ui.Controls.ContentDialogResult.Primary)
            return PendingFileCloseAction.Save;

        if (result == Wpf.Ui.Controls.ContentDialogResult.Secondary)
            return PendingFileCloseAction.DontSave;

        if (closeButtonAction == PendingFileCloseAction.SaveToHistory)
            return closeButtonAction;

        return PendingFileCloseAction.Cancel;
    }

    private void SaveWindowTextToHistoryIfNeeded()
    {
        if (string.IsNullOrEmpty(OpenedFilePath)
            && !string.IsNullOrWhiteSpace(Session.Text))
            Singleton<HistoryService>.Instance.SaveToHistory(this);
    }

    private void SaveWindowTextToHistoryNow()
    {
        Singleton<HistoryService>.Instance.SaveToHistory(this);
        Singleton<HistoryService>.Instance.WriteHistory();
    }

    private async Task HandlePendingFileClosePromptAsync()
    {
        try
        {
            switch (await PromptForPendingFileEditsAsync())
            {
                case PendingFileCloseAction.Save:
                    if (!SaveCurrentDocument())
                        return;
                    break;
                case PendingFileCloseAction.DontSave:
                    break;
                case PendingFileCloseAction.SaveToHistory:
                    SaveWindowTextToHistoryNow();
                    break;
                case PendingFileCloseAction.Cancel:
                default:
                    return;
            }

            allowCloseAfterPendingFilePrompt = true;
            Close();
        }
        finally
        {
            isShowingPendingFileClosePrompt = false;
        }
    }
}
