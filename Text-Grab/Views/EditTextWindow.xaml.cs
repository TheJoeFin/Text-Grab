using Humanizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Text_Grab.Controls;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Text_Grab;

/// <summary>
/// Interaction logic for ManipulateTextWindow.xaml
/// </summary>

public partial class EditTextWindow : Wpf.Ui.Controls.FluentWindow
{
    #region Fields

    public static RoutedCommand DeleteAllSelectionCmd = new();
    public static RoutedCommand DeleteAllSelectionPatternCmd = new();
    public static RoutedCommand InsertSelectionOnEveryLineCmd = new();
    public static RoutedCommand IsolateSelectionCmd = new();
    public static RoutedCommand LaunchCmd = new();
    public static RoutedCommand MakeQrCodeCmd = new();
    public static RoutedCommand OcrPasteCommand = new();
    public static RoutedCommand ReplaceReservedCmd = new();
    public static RoutedCommand SingleLineCmd = new();
    public static RoutedCommand SplitOnSelectionCmd = new();
    public static RoutedCommand ToggleCaseCmd = new();
    public static RoutedCommand UnstackCmd = new();
    public static RoutedCommand UnstackGroupCmd = new();
    public bool LaunchedFromNotification = false;
    CancellationTokenSource? cancellationTokenForDirOCR;
    private string historyId = string.Empty;
    private int numberOfContextMenuItems;
    private string? OpenedFilePath;

    private WindowState? prevWindowState;
    private CultureInfo selectedCultureInfo = CultureInfo.CurrentCulture;

    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    #endregion Fields

    #region Constructors

    public EditTextWindow()
    {
        InitializeComponent();
        App.SetTheme();
    }

    public EditTextWindow(string possiblyEncodedString, bool isEncoded = true)
    {
        InitializeComponent();
        App.SetTheme();

        if (isEncoded)
            ReadEncodedString(possiblyEncodedString);
        else
            PassedTextControl.Text = possiblyEncodedString;

        LaunchedFromNotification = true;
    }

    public EditTextWindow(HistoryInfo historyInfo)
    {
        InitializeComponent();
        App.SetTheme();

        PassedTextControl.Text = historyInfo.TextContent;

        historyId = historyInfo.ID;

        if (historyInfo.PositionRect != Rect.Empty)
        {
            this.Left = historyInfo.PositionRect.X;
            this.Top = historyInfo.PositionRect.Y;
            this.Width = historyInfo.PositionRect.Width;
            this.Height = historyInfo.PositionRect.Height;
        }
    }

    #endregion Constructors

    #region Properties

    public CurrentCase CaseStatusOfToggle { get; set; } = CurrentCase.Unknown;

    public bool WrapText { get; set; } = false;
    private bool _IsAccessingClipboard { get; set; } = false;

    #endregion Properties

    #region Methods

    public static Dictionary<string, RoutedCommand> GetRoutedCommands()
    {
        return new Dictionary<string, RoutedCommand>()
        {
            {nameof(SplitOnSelectionCmd), SplitOnSelectionCmd},
            {nameof(IsolateSelectionCmd), IsolateSelectionCmd},
            {nameof(SingleLineCmd), SingleLineCmd},
            {nameof(LaunchCmd), LaunchCmd},
            {nameof(ToggleCaseCmd), ToggleCaseCmd},
            {nameof(ReplaceReservedCmd), ReplaceReservedCmd},
            {nameof(UnstackCmd), UnstackCmd},
            {nameof(UnstackGroupCmd), UnstackGroupCmd},
            {nameof(DeleteAllSelectionCmd), DeleteAllSelectionCmd},
            {nameof(DeleteAllSelectionPatternCmd), DeleteAllSelectionPatternCmd},
            {nameof(InsertSelectionOnEveryLineCmd), InsertSelectionOnEveryLineCmd},
            {nameof(OcrPasteCommand), OcrPasteCommand},
            {nameof(MakeQrCodeCmd), MakeQrCodeCmd}
        };
    }

    public void AddCharsToEditTextWindow(string stringToAdd, SpotInLine spotInLine)
    {
        PassedTextControl.Text = PassedTextControl.Text.AddCharsToEachLine(stringToAdd, spotInLine);
    }

    public void AddThisText(string textToAdd)
    {
        PassedTextControl.AppendText(textToAdd);
    }

    public System.Windows.Controls.TextBox GetMainTextBox()
    {
        return PassedTextControl;
    }

    public async Task OcrAllImagesInFolder(string folderPath, OcrDirectoryOptions options)
    {
        IEnumerable<String>? files = null;

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
        catch (System.Exception ex)
        {
            PassedTextControl.AppendText($"Failed to read directory: {ex.Message}{Environment.NewLine}");
        }

        if (files is null)
            return;

        List<string> imageFiles = files.Where(x => IoUtilities.ImageExtensions.Contains(Path.GetExtension(x).ToLower())).ToList();

        if (imageFiles.Count == 0)
        {
            PassedTextControl.AppendText($"{folderPath} contains no images");
            return;
        }

        Language selectedLanguage = LanguageUtilities.GetOCRLanguage();
        string tesseractLanguageTag = string.Empty;

        if (LanguageMenuItem.Items.Count > 0)
        {
            foreach (MenuItem languageSubItem in LanguageMenuItem.Items)
            {
                if (languageSubItem.IsChecked)
                {
                    if (languageSubItem.Tag is Language language)
                        selectedLanguage = language;
                    else if (languageSubItem.Tag is TessLang tesseractLanguage)
                        tesseractLanguageTag = tesseractLanguage.LanguageTag;
                }
            }
        }

        if (options.OutputHeader)
        {
            PassedTextControl.AppendText(folderPath);
            PassedTextControl.AppendText(Environment.NewLine);
            PassedTextControl.AppendText($"{imageFiles.Count} images found");

            if (!string.IsNullOrEmpty(tesseractLanguageTag))
            {
                PassedTextControl.AppendText(Environment.NewLine);
                PassedTextControl.AppendText($"Using {tesseractLanguageTag} from Tesseract.");
                PassedTextControl.AppendText(Environment.NewLine);
                PassedTextControl.AppendText("Tesseract can only run single threaded,");
                PassedTextControl.AppendText(Environment.NewLine);
                PassedTextControl.AppendText("May be slower if processing many images");
                PassedTextControl.AppendText(Environment.NewLine);
            }
            else
            {
                PassedTextControl.AppendText(Environment.NewLine);
                PassedTextControl.AppendText($"Using {selectedLanguage.DisplayName} from Windows.");
                PassedTextControl.AppendText(Environment.NewLine);
            }

            PassedTextControl.AppendText("Press Escape to cancel");
            PassedTextControl.AppendText(Environment.NewLine);
            PassedTextControl.AppendText(Environment.NewLine);
        }

        cancellationTokenForDirOCR = new();
        Stopwatch stopwatch = new();
        stopwatch.Start();
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        List<AsyncOcrFileResult> ocrFileResults = new();
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
                PassedTextControl.AppendText(Environment.NewLine);
                PassedTextControl.AppendText($"----- COMPLETED OCR OF {imageFiles.Count} images");
            }
        }
        catch (OperationCanceledException)
        {
            PassedTextControl.AppendText(Environment.NewLine);
            int countCompleted = ocrFileResults.Where(r => r.OcrResult is not null).Count();
            PassedTextControl.AppendText($"----- CANCELLED OCR OF {ocrFileResults.Count - countCompleted}, Completed {countCompleted} images");
        }
        finally
        {
            cancellationTokenForDirOCR.Dispose();
        }

        Mouse.OverrideCursor = null;
        stopwatch.Stop();

        if (options.OutputFooter)
        {
            PassedTextControl.AppendText(Environment.NewLine);
            PassedTextControl.AppendText($"----- from {folderPath}");
            PassedTextControl.AppendText(Environment.NewLine);
            PassedTextControl.AppendText($"----- and took {stopwatch.Elapsed:c}");
        }
        PassedTextControl.ScrollToEnd();

        GC.Collect();
        cancellationTokenForDirOCR = null;
    }

    public void OpenMostRecentTextHistoryItem()
    {
        PassedTextControl.Text = Singleton<HistoryService>.Instance.GetLastTextHistory();
    }

    public void RemoveCharsFromEditTextWindow(int numberOfChars, SpotInLine spotInLine)
    {
        PassedTextControl.Text = PassedTextControl.Text.RemoveFromEachLine(numberOfChars, spotInLine);
    }

    public void SetBottomBarButtons()
    {
        BottomBarButtons.Children.Clear();

        List<CollapsibleButton> buttons = CustomBottomBarUtilities.GetBottomBarButtons(this);

        if (DefaultSettings.ScrollBottomBar)
            BottomBarScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        else
            BottomBarScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

        if (DefaultSettings.ShowCursorText)
            BottomBarText.Visibility = Visibility.Visible;
        else
            BottomBarText.Visibility = Visibility.Collapsed;

        foreach (CollapsibleButton collapsibleButton in buttons)
            BottomBarButtons.Children.Add(collapsibleButton);
    }

    internal HistoryInfo AsHistoryItem()
    {
        HistoryInfo historyInfo = new()
        {
            ID = historyId,
            LanguageTag = LanguageUtilities.GetCurrentInputLanguage().LanguageTag,
            CaptureDateTime = DateTimeOffset.Now,
            TextContent = PassedTextControl.Text,
            SourceMode = TextGrabMode.EditText,
        };

        if (string.IsNullOrWhiteSpace(historyInfo.ID))
            historyInfo.ID = Guid.NewGuid().ToString();

        return historyInfo;
    }

    internal void LimitNumberOfCharsPerLine(int numberOfChars, SpotInLine spotInLine)
    {
        PassedTextControl.Text = PassedTextControl.Text.LimitCharactersPerLine(numberOfChars, spotInLine);
    }

    internal async void OpenPath(string pathOfFileToOpen, bool isMultipleFiles = false)
    {
        OpenedFilePath = pathOfFileToOpen;

        (string TextContent, OpenContentKind KindOpened) = await IoUtilities.GetContentFromPath(pathOfFileToOpen, isMultipleFiles);

        if (KindOpened == OpenContentKind.TextFile
            && !isMultipleFiles
            && !string.IsNullOrWhiteSpace(TextContent))
            UiTitleBar.Title = $"Edit Text | {Path.GetFileName(OpenedFilePath)}";

        PassedTextControl.AppendText(TextContent);
    }

    private static string ListFilesFoldersInDirectory(string chosenFolderPath)
    {
        IEnumerable<string> files = Directory.EnumerateFiles(chosenFolderPath);
        IEnumerable<string> folders = Directory.EnumerateDirectories(chosenFolderPath);
        StringBuilder listOfNames = new();
        listOfNames.Append(chosenFolderPath).Append(Environment.NewLine).Append(Environment.NewLine);
        foreach (string folder in folders)
            listOfNames.Append($"{folder.AsSpan(1 + chosenFolderPath.Length, (folder.Length - 1) - chosenFolderPath.Length)}{Environment.NewLine}");

        foreach (string file in files)
            listOfNames.Append($"{file.AsSpan(1 + chosenFolderPath.Length, (file.Length - 1) - chosenFolderPath.Length)}{Environment.NewLine}");
        return listOfNames.ToString();
    }

    private static async Task<string> OcrFile(string path, Language? selectedLanguage, string tesseractLanguageTag, OcrDirectoryOptions options)
    {
        StringBuilder returnString = new();
        if (options.OutputFileNames)
            returnString.AppendLine(Path.GetFileName(path));
        try
        {
            string ocrText = await OcrUtilities.OcrAbsoluteFilePathAsync(path, selectedLanguage, tesseractLanguageTag);

            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                returnString.AppendLine(ocrText);

                if (options.WriteTxtFiles && Path.GetDirectoryName(path) is string dir)
                {
                    using StreamWriter outputFile = new(Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(path)}.txt"));
                    outputFile.WriteLine(ocrText);
                }
            }
            else
                returnString.AppendLine($"----- No Text Extracted{Environment.NewLine}");

        }
        catch (Exception ex)
        {
            returnString.AppendLine($"Failed to read {path}: {ex.Message}{Environment.NewLine}");
        }

        return returnString.ToString();
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<FirstRunWindow>();
    }

    private void AddCopiedTextToTextBox(string textToAdd)
    {
        PassedTextControl.SelectedText = textToAdd;
        int currentSelectionIndex = PassedTextControl.SelectionStart;
        int currentSelectionLength = PassedTextControl.SelectionLength;

        PassedTextControl.Select(currentSelectionIndex + currentSelectionLength, 0);
    }

    private void AddPossibleMailToToRightClickMenu(int caretIndex)
    {
        string possibleEmail = PassedTextControl.SelectedText;

        if (string.IsNullOrEmpty(possibleEmail))
            possibleEmail = PassedTextControl.Text.GetWordAtCursorPosition(caretIndex);

        if (!possibleEmail.IsValidEmailAddress())
            return;

        MenuItem emailMi = new()
        {
            Header = $"Email: {possibleEmail}"
        };
        emailMi.Click += (sender, e) =>
        {
            Process.Start(new ProcessStartInfo($"mailto:{possibleEmail}") { UseShellExecute = true });
        };

        PassedTextControl.ContextMenu?.Items.Insert(0, new Separator());
        PassedTextControl.ContextMenu?.Items.Insert(0, emailMi);
    }

    private void AddPossibleSpellingErrorsToRightClickMenu(int caretIndex)
    {
        int cmdIndex = 0;
        SpellingError spellingError;
        spellingError = PassedTextControl.GetSpellingError(caretIndex);
        if (spellingError is not null
            && PassedTextControl.ContextMenu is not null)
        {
            foreach (string str in spellingError.Suggestions)
            {
                MenuItem mi = new()
                {
                    Header = str,
                    FontWeight = FontWeights.Bold,
                    Command = System.Windows.Documents.EditingCommands.CorrectSpellingError,
                    CommandParameter = str,
                    CommandTarget = PassedTextControl
                };
                PassedTextControl.ContextMenu.Items.Insert(cmdIndex, mi);
                cmdIndex++;
            }

            if (cmdIndex == 0)
            {
                MenuItem mi = new()
                {
                    Header = "no suggestions",
                    IsEnabled = false
                };
                PassedTextControl.ContextMenu.Items.Insert(cmdIndex, mi);
                cmdIndex++;
            }

            PassedTextControl.ContextMenu.Items.Insert(cmdIndex, new Separator());
            cmdIndex++;
            MenuItem ignoreAllMI = new()
            {
                Header = "Ignore All",
                Command = System.Windows.Documents.EditingCommands.IgnoreSpellingError,
                CommandTarget = PassedTextControl
            };
            PassedTextControl.ContextMenu.Items.Insert(cmdIndex, ignoreAllMI);
            cmdIndex++;
            PassedTextControl.ContextMenu.Items.Insert(cmdIndex, new Separator());
        }
    }

    private void AddPossibleURLToRightClickMenu(int caretIndex)
    {
        string possibleURL = PassedTextControl.SelectedText;

        if (string.IsNullOrEmpty(possibleURL))
        {
            possibleURL = PassedTextControl.Text.GetWordAtCursorPosition(caretIndex);
        }

        if (Uri.TryCreate(possibleURL, UriKind.Absolute, out Uri? uri))
        {
            string headerText = $"Try to go to: {possibleURL}";
            if (headerText.Length > 36)
                headerText = string.Concat(headerText.AsSpan(0, 36), "...");

            MenuItem urlMi = new()
            {
                Header = headerText
            };
            urlMi.Click += (sender, e) =>
            {
                Process.Start(new ProcessStartInfo(possibleURL) { UseShellExecute = true });
            };
            PassedTextControl.ContextMenu?.Items.Insert(0, new Separator());
            PassedTextControl.ContextMenu?.Items.Insert(0, urlMi);
        }
    }

    private void AddRemoveAtMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddOrRemoveWindow addRemoveWindow = new()
        {
            Owner = this,
            SelectedTextFromEditTextWindow = PassedTextControl.SelectedText
        };
        addRemoveWindow.ShowDialog();
    }

    private void AlwaysOnTop_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is MenuItem aotMi && aotMi.IsChecked)
            Topmost = true;
        else
            Topmost = false;

        DefaultSettings.EditWindowIsOnTop = Topmost;
    }

    private void CanLaunchUriExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        string possibleURL = PassedTextControl.SelectedText;

        if (string.IsNullOrEmpty(possibleURL))
            possibleURL = PassedTextControl.Text.GetWordAtCursorPosition(PassedTextControl.CaretIndex);
        if (Uri.TryCreate(possibleURL, UriKind.Absolute, out _))
        {
            e.CanExecute = true;
            return;
        }

        e.CanExecute = false;
    }

    private void CanOcrPasteExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        _IsAccessingClipboard = true;
        DataPackageView? dataPackageView = null;

        try
        {
            dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"error with Windows.ApplicationModel.DataTransfer.Clipboard.GetContent(). Exception Message: {ex.Message}");
            e.CanExecute = false;
        }
        finally
        {
            _IsAccessingClipboard = false;
        }

        if (dataPackageView is null)
        {
            e.CanExecute = false;
            return;
        }

        if (dataPackageView.Contains(StandardDataFormats.Text)
            || dataPackageView.Contains(StandardDataFormats.Bitmap)
            || dataPackageView.Contains(StandardDataFormats.StorageItems))
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void CaptureMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        LoadLanguageMenuItems(LanguageMenuItem);
    }

    private void CheckForGrabFrameOrLaunch()
    {
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        foreach (Window window in allWindows)
            if (window is GrabFrame grabFrame)
            {
                grabFrame.Activate();
                grabFrame.DestinationTextBox = PassedTextControl;
                return;
            }

        Keyboard.Focus(PassedTextControl);
        PassedTextControl.IsInactiveSelectionHighlightEnabled = true;
        PassedTextControl.SelectedText = " ";
        if (BottomBarButtons.Children.Count > 0
            && BottomBarButtons.Children[0] is CollapsibleButton collapsibleButton)
            collapsibleButton.Focus();
        GrabFrame gf = new()
        {
            DestinationTextBox = PassedTextControl
        };
        gf.Show();
    }

    private void CheckRightToLeftLanguage()
    {
        if (LanguageUtilities.GetCurrentInputLanguage().IsRightToLeft())
            PassedTextControl.TextAlignment = TextAlignment.Right;
    }

    private async void Clipboard_ContentChanged(object? sender, object e)
    {
        if (ClipboardWatcherMenuItem.IsChecked is false || _IsAccessingClipboard)
            return;

        _IsAccessingClipboard = true;
        DataPackageView? dataPackageView = null;

        try
        {
            dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"error with Windows.ApplicationModel.DataTransfer.Clipboard.GetContent(). Exception Message: {ex.Message}");
        }

        if (dataPackageView is not null && dataPackageView.Contains(StandardDataFormats.Text))
        {
            string text = string.Empty;
            try
            {
                text = await dataPackageView.GetTextAsync();
                text += Environment.NewLine;
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { AddCopiedTextToTextBox(text); }));
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"error with dataPackageView.GetTextAsync(). Exception Message: {ex.Message}");
            }
        };

        _IsAccessingClipboard = false;
    }

    private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void ContactMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format("mailto:support@textgrab.net")));
    }

    private void CopyCloseBTN_Click(object sender, RoutedEventArgs e)
    {
        string clipboardText = PassedTextControl.Text;
        try { System.Windows.Clipboard.SetDataObject(clipboardText, true); } catch { }
        this.Close();
    }

    private async void CopyClosePasteBTN_Click(object sender, RoutedEventArgs e)
    {
        string clipboardText = PassedTextControl.Text;
        try { System.Windows.Clipboard.SetDataObject(clipboardText, true); } catch { }
        this.Close();
        await WindowUtilities.TryInsertString(clipboardText);
    }

    private void DeleteAllSelectionExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selectionToDelete = PassedTextControl.SelectedText;

        PassedTextControl.Text = PassedTextControl.Text.RemoveAllInstancesOf(selectionToDelete);
    }

    private void DeleteAllSelectionPatternExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selectionToDelete = PassedTextControl.SelectedText;
        string Pattern = selectionToDelete.ExtractSimplePattern();
        MatchCollection Matches = Regex.Matches(PassedTextControl.Text, Pattern, RegexOptions.Multiline);
        StringBuilder sb = new(PassedTextControl.Text);
        for (int i = Matches.Count - 1; i >= 0; i--)
        {
            Match match = Matches[i];

            sb.Remove(match.Index, match.Length);
        }

        PassedTextControl.Text = sb.ToString();
    }

    private void DeleteSelectedTextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        PassedTextControl.SelectedText = String.Empty;
    }

    private void EditBottomBarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        BottomBarSettings bbs = new()
        {
            Owner = this
        };
        bbs.ShowDialog();
    }

    private void EditTextWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
            return;

        UIElementCollection bottomBarButtons = BottomBarButtons.Children;

        int keyNumberPressed = (int)e.Key - 35;

        // D1 is 35
        // ...
        // D9 is 43
        // D0 is 34

        if (keyNumberPressed < -1
            || keyNumberPressed > 8)
            return;

        // since D9 is next to D0 it makes sense
        // to call buttons next to each other as well
        if (keyNumberPressed == -1)
            keyNumberPressed += 10;

        if (bottomBarButtons.Count <= keyNumberPressed)
            return;

        if (bottomBarButtons[keyNumberPressed] is not CollapsibleButton correspondingButton)
            return;

        e.Handled = true;

        if (correspondingButton.Command is ICommand buttonCommand)
            buttonCommand.Execute(null);
        else
            correspondingButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
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

    private void ETWindow_Drop(object sender, System.Windows.DragEventArgs e)
    {
        // Mark the event as handled, so TextBox's native Drop handler is not called.

        if (e.Data.GetDataPresent("Text"))
            return;

        e.Handled = true;
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop, true))
        {
            string[]? fileNames = e.Data.GetData(System.Windows.DataFormats.FileDrop, true) as string[];
            // Check for a single file or folder.
            if (fileNames?.Length is 1)
            {
                // Check for a file (a directory will return false).
                if (File.Exists(fileNames[0]))
                    OpenPath(fileNames[0], false);
            }
            else if (fileNames?.Length > 1)
            {
                foreach (string possibleFilePath in fileNames)
                {
                    if (File.Exists(possibleFilePath))
                        OpenPath(possibleFilePath, true);
                }
            }
        }
        Mouse.OverrideCursor = null;
    }

    private void FeedbackMenuItem_Click(object sender, RoutedEventArgs ev)
    {
        Uri source = new("https://github.com/TheJoeFin/Text-Grab/issues", UriKind.Absolute);
        System.Windows.Navigation.RequestNavigateEventArgs e = new(source, "https://github.com/TheJoeFin/Text-Grab/issues");
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void FindAndReplaceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        LaunchFindAndReplace();
    }

    private void FontMenuItem_Click(object sender, RoutedEventArgs e)
    {
        using FontDialog fd = new();
        System.Drawing.Font currentFont = new(PassedTextControl.FontFamily.ToString(), (float)((PassedTextControl.FontSize * 72.0) / 96.0));
        fd.Font = currentFont;
        DialogResult result = fd.ShowDialog();
        if (result != System.Windows.Forms.DialogResult.OK)
            return;

        Debug.WriteLine(fd.Font);

        DefaultSettings.FontFamilySetting = fd.Font.Name;
        DefaultSettings.FontSizeSetting = (fd.Font.Size * 96.0 / 72.0);
        DefaultSettings.IsFontBold = fd.Font.Bold;
        DefaultSettings.IsFontItalic = fd.Font.Italic;
        DefaultSettings.IsFontUnderline = fd.Font.Underline;
        DefaultSettings.IsFontStrikeout = fd.Font.Strikeout;
        DefaultSettings.Save();

        SetFontFromSettings();
    }

    private async void FSGDelayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await Task.Delay(2000);
        WindowUtilities.LaunchFullScreenGrab(PassedTextControl);
    }

    private void FullScreenGrabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab(PassedTextControl);
    }

    private string GetSelectedTextOrAllText()
    {
        string textToModify;
        if (PassedTextControl.SelectionLength == 0)
            textToModify = PassedTextControl.Text;
        else
            textToModify = PassedTextControl.SelectedText;
        return textToModify;
    }

    private void GrabFrameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CheckForGrabFrameOrLaunch();
    }

    private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Source: StackOverflow, read on Sep. 10, 2021
        // https://stackoverflow.com/a/53698638/7438031

        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        e.Handled = true;

        if (e.Delta > 0)
            PassedTextControl.FontSize += 4;
        else if (e.Delta < 0)
        {
            if (PassedTextControl.FontSize > 4)
                PassedTextControl.FontSize -= 4;
        }
    }

    private void HideBottomBarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is MenuItem bbMi && bbMi.IsChecked)
        {
            BottomBar.Visibility = Visibility.Collapsed;
            DefaultSettings.EditWindowBottomBarIsHidden = true;
        }
        else
        {
            BottomBar.Visibility = Visibility.Visible;
            DefaultSettings.EditWindowBottomBarIsHidden = false;
        }
    }

    private void InsertSelectionOnEveryLine(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string[] splitString = PassedTextControl.Text.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.None);
        string selectionText = PassedTextControl.SelectedText;
        int initialSelectionStart = PassedTextControl.SelectionStart;
        int selectionPositionInLine = PassedTextControl.SelectionStart;
        for (int i = initialSelectionStart; i >= 0; i--)
        {
            if (PassedTextControl.Text[i] == '\n'
                || PassedTextControl.Text[i] == '\r')
            {
                selectionPositionInLine = initialSelectionStart - i - 1;
                break;
            }
        }

        int selectionLength = PassedTextControl.SelectionLength;

        if (string.IsNullOrEmpty(splitString.Last()))
            splitString = splitString.SkipLast(1).ToArray();

        StringBuilder sb = new();
        foreach (string line in splitString)
        {
            if (line.Length >= selectionPositionInLine
                && line.Length >= (selectionPositionInLine + selectionLength))
            {
                if (line.Substring(selectionPositionInLine, selectionLength) != selectionText)
                    sb.Append(line.Insert(selectionPositionInLine, selectionText));
                else
                    sb.Append(line);
            }
            else
            {
                if (line.Length > selectionPositionInLine)
                    sb.Append(line.Insert(selectionPositionInLine, selectionText));
                else
                    sb.Append(line).Append(selectionText.PadLeft((selectionPositionInLine + selectionLength) - line.Length));
            }
            sb.Append(Environment.NewLine);
        }

        PassedTextControl.Text = sb.ToString();
    }

    private void InsertSelectionOnEveryLineCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PassedTextControl.SelectedText)
            || PassedTextControl.SelectedText.Contains(Environment.NewLine)
            || PassedTextControl.SelectedText.Contains('\r')
            || PassedTextControl.SelectedText.Contains('\n'))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void IsolateSelectionCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PassedTextControl.SelectedText))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void IsolateSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(PassedTextControl.SelectedText))
            PassedTextControl.Text = PassedTextControl.SelectedText;
    }

    private void keyedCtrlF(object sender, ExecutedRoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab(PassedTextControl);
    }

    private void keyedCtrlG(object sender, ExecutedRoutedEventArgs e)
    {
        CheckForGrabFrameOrLaunch();
    }

    private void KeyedEscape(object sender, ExecutedRoutedEventArgs e)
    {
        cancellationTokenForDirOCR?.Cancel();
    }

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (LanguageMenuItem is null || sender is not MenuItem clickedMenuItem)
            return;

        foreach (MenuItem menuItem in LanguageMenuItem.Items)
        {
            menuItem.IsChecked = false;
        }

        clickedMenuItem.IsChecked = true;
    }

    private void LaunchFindAndReplace()
    {
        FindAndReplaceWindow findAndReplaceWindow = WindowUtilities.OpenOrActivateWindow<FindAndReplaceWindow>();

        findAndReplaceWindow.StringFromWindow = PassedTextControl.Text;
        findAndReplaceWindow.TextEditWindow = this;
        findAndReplaceWindow.Show();


        if (PassedTextControl.SelectedText.Length > 0)
        {
            findAndReplaceWindow.FindTextBox.Text = PassedTextControl.SelectedText.Trim();
            findAndReplaceWindow.FindTextBox.Select(findAndReplaceWindow.FindTextBox.Text.Length, 0);
            findAndReplaceWindow.SearchForText();
        }
    }

    private void LaunchFullscreenOnLoad_Click(object sender, RoutedEventArgs e)
    {
        DefaultSettings.EditWindowStartFullscreen = LaunchFullscreenOnLoad.IsChecked;
        DefaultSettings.Save();
    }

    private void LaunchQuickSimpleLookup(object sender, RoutedEventArgs e)
    {
        QuickSimpleLookup qsl = new()
        {
            DestinationTextBox = PassedTextControl,
            IsFromETW = true
        };
        qsl.Show();
    }

    private void LaunchUriExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string possibleURL = PassedTextControl.SelectedText;

        if (string.IsNullOrEmpty(possibleURL))
            possibleURL = PassedTextControl.Text.GetWordAtCursorPosition(PassedTextControl.CaretIndex);
        if (Uri.TryCreate(possibleURL, UriKind.Absolute, out _))
            Process.Start(new ProcessStartInfo(possibleURL) { UseShellExecute = true });
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
            PassedTextControl.AppendText(ListFilesFoldersInDirectory(chosenFolderPath));
        }
        catch (Exception ex)
        {
            PassedTextControl.AppendText($"Failed: {ex.Message}{Environment.NewLine}");
        }
    }

    private async void LoadLanguageMenuItems(MenuItem captureMenuItem)
    {
        if (captureMenuItem.Items.Count > 0)
            return;

        // TODO Find a way to combine with the FSG language drop down

        bool haveSetLastLang = false;
        string lastTextLang = DefaultSettings.LastUsedLang;
        bool usingTesseract = DefaultSettings.UseTesseract && TesseractHelper.CanLocateTesseractExe();

        if (usingTesseract)
        {
            List<ILanguage> tesseractLanguages = await TesseractHelper.TesseractLanguages();

            foreach (TessLang language in tesseractLanguages.Cast<TessLang>())
            {
                MenuItem languageMenuItem = new()
                {
                    Header = language.DisplayName,
                    Tag = language,
                    IsCheckable = true,
                };
                languageMenuItem.Click += LanguageMenuItem_Click;

                captureMenuItem.Items.Add(languageMenuItem);

                if (!haveSetLastLang && language.CultureDisplayName == lastTextLang)
                {
                    languageMenuItem.IsChecked = true;
                    haveSetLastLang = true;
                }
            }
        }

        IReadOnlyList<Language> possibleOCRLanguages = OcrEngine.AvailableRecognizerLanguages;

        Language firstLang = LanguageUtilities.GetOCRLanguage();

        foreach (Language language in possibleOCRLanguages)
        {
            MenuItem languageMenuItem = new()
            {
                Header = language.DisplayName,
                Tag = language,
                IsCheckable = true,
            };
            languageMenuItem.Click += LanguageMenuItem_Click;

            captureMenuItem.Items.Add(languageMenuItem);

            if (!haveSetLastLang &&
                (language.AbbreviatedName.Equals(firstLang?.AbbreviatedName.ToLower(), StringComparison.CurrentCultureIgnoreCase)
                || language.LanguageTag.Equals(firstLang?.LanguageTag.ToLower(), StringComparison.CurrentCultureIgnoreCase)))
            {
                languageMenuItem.IsChecked = true;
                haveSetLastLang = true;
            }
        }
        if (!haveSetLastLang && captureMenuItem.Items[0] is MenuItem firstMenuItem)
        {
            firstMenuItem.IsChecked = true;
        }
    }

    private void LoadRecentTextHistory()
    {
        List<HistoryInfo> grabsHistories = Singleton<HistoryService>.Instance.GetEditWindows();
        grabsHistories = grabsHistories.OrderByDescending(x => x.CaptureDateTime).ToList();

        OpenRecentMenuItem.Items.Clear();

        if (grabsHistories.Count < 1)
        {
            OpenRecentMenuItem.IsEnabled = false;
            return;
        }

        foreach (HistoryInfo history in grabsHistories)
        {
            MenuItem menuItem = new();
            menuItem.Click += (object sender, RoutedEventArgs args) =>
            {
                if (string.IsNullOrWhiteSpace(PassedTextControl.Text))
                {
                    PassedTextControl.Text = history.TextContent;
                    return;
                }

                EditTextWindow etw = new(history);
                etw.Show();
            };

            if (PassedTextControl.Text == history.TextContent)
                menuItem.IsEnabled = false;

            menuItem.Header = $"{history.CaptureDateTime.Humanize()} | {history.TextContent.MakeStringSingleLine().Truncate(20)}";
            OpenRecentMenuItem.Items.Add(menuItem);
        }
    }

    private void MakeQrCodeCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GetSelectedTextOrAllText()))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void MakeQrCodeExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PassedTextControl.Text))
            return;

        string text = GetSelectedTextOrAllText();

        QrCodeWindow window = new(text);
        window.CenterOverThisWindow(this);
        window.Show();
    }

    private void AddedLineAboveCommand(object sender, ExecutedRoutedEventArgs e)
    {
        int replaceCaret = PassedTextControl.CaretIndex + Environment.NewLine.Length;
        int selectionLength = PassedTextControl.SelectionLength;

        SelectLine();
        string lineText = PassedTextControl.SelectedText;
        PassedTextControl.SelectedText = $"{Environment.NewLine}{lineText}";
        PassedTextControl.Select(replaceCaret, selectionLength);
    }

    private void DuplicateSelectedLine(object sender, ExecutedRoutedEventArgs e)
    {
        int replaceCaret = PassedTextControl.CaretIndex;
        int selectionLength = PassedTextControl.SelectionLength;
        SelectLine();
        string lineText = PassedTextControl.SelectedText;
        bool lineEndsInNewLine = lineText.EndsWithNewline();
        PassedTextControl.SelectedText = $"{ lineText}{(lineEndsInNewLine ? "" : Environment.NewLine)}{ lineText}";
        int length = lineText.Length;
        if (!lineEndsInNewLine)
            length += Environment.NewLine.Length;

        PassedTextControl.Select(replaceCaret + length, selectionLength);
    }

    private void MarginsMenuItem_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem marginsMenuItem)
            return;

        DefaultSettings.EtwUseMargins = marginsMenuItem.IsChecked;
        SetMargins(MarginsMenuItem.IsChecked);
    }

    private async void MenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        LoadRecentTextHistory();
        await Singleton<HistoryService>.Instance.PopulateMenuItemWithRecentGrabs(OpenRecentGrabsMenuItem);
    }

    private void MoveLineDown(object? sender, ExecutedRoutedEventArgs? e)
    {
        SelectLine(sender, e);

        string lineText = PassedTextControl.SelectedText;
        PassedTextControl.SelectedText = "";
        string textBoxText = PassedTextControl.Text;
        int selectionIndex = PassedTextControl.SelectionStart;
        int indexOfNextNewline = textBoxText.Length;

        if (!PassedTextControl.Text.EndsWith(Environment.NewLine))
        {
            PassedTextControl.Text += Environment.NewLine;
        }

        IEnumerable<int> indicesOfNewLine = textBoxText.AllIndexesOf(Environment.NewLine);

        foreach (int newLineIndex in indicesOfNewLine)
        {
            int newLineEnd = newLineIndex;
            if (newLineEnd >= selectionIndex)
            {
                indexOfNextNewline = newLineEnd + Environment.NewLine.Length;
                break;
            }
        }

        PassedTextControl.Select(indexOfNextNewline, 0);
        PassedTextControl.SelectedText = lineText;
    }

    private void MoveLineDownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveLineDown(sender, null);
    }

    private void MoveLineUp(object? sender, ExecutedRoutedEventArgs? e)
    {
        SelectLine(sender, e);
        string lineText = PassedTextControl.SelectedText;
        PassedTextControl.SelectedText = "";
        string textBoxText = PassedTextControl.Text;
        int selectionIndex = PassedTextControl.SelectionStart;
        int indexOfPreviousNewline = 0;

        IEnumerable<int> indicesOfNewLine = textBoxText.AllIndexesOf(Environment.NewLine);

        foreach (int newLineIndex in indicesOfNewLine)
        {
            int newLineEnd = newLineIndex + Environment.NewLine.Length;
            if (newLineEnd < selectionIndex)
                indexOfPreviousNewline = newLineEnd;
        }

        PassedTextControl.Select(indexOfPreviousNewline, 0);
        PassedTextControl.SelectedText = lineText;
    }

    private void MoveLineUpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveLineUp(sender, null);
    }

    private void NewFullscreen_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab(PassedTextControl);
    }

    private void NewWindow_Clicked(object sender, RoutedEventArgs e)
    {
        EditTextWindow newETW = new();
        newETW.Show();
    }

    private void NewWindowWithText_Clicked(object sender, RoutedEventArgs e)
    {
        string selectedText = PassedTextControl.SelectedText;
        PassedTextControl.SelectedText = "";
        EditTextWindow newEtwWithText = new(selectedText, false);
        newEtwWithText.Show();
    }

    private async Task OcrAllImagesInParallel(OcrDirectoryOptions options, List<AsyncOcrFileResult> ocrFileResults, Language selectedLanguage, string tesseractLanguageTag)
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

            ocrFile.OcrResult = await OcrFile(ocrFile.FilePath, selectedLanguage, tesseractLanguageTag, options);

            // to get the TextBox to update whenever OCR Finishes:
            if (!options.WriteTxtFiles)
            {
                await System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    PassedTextControl.AppendText(Environment.NewLine);
                    PassedTextControl.AppendText(ocrFile.OcrResult);
                    PassedTextControl.ScrollToEnd();
                });
            }
        });
    }

    private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Create OpenFileDialog 
        Microsoft.Win32.OpenFileDialog dlg = new()
        {
            // Set filter for file extension and default file extension 
            DefaultExt = ".txt",
            Filter = "Text documents (.txt)|*.txt"
        };

        bool? result = dlg.ShowDialog();

        if (result is true && File.Exists(dlg.FileName))
            OpenPath(dlg.FileName);
    }

    private void OpenGrabFrame_Click(object sender, RoutedEventArgs e)
    {
        CheckForGrabFrameOrLaunch();
    }

    private void OpenLastAsGrabFrameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Singleton<HistoryService>.Instance.GetLastHistoryAsGrabFrame();
    }
    private void PassedTextControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        PassedTextControl.ContextMenu = null;


        ContextMenu? baseContextMenu = this.FindResource("ContextMenuResource") as ContextMenu;

        while (baseContextMenu is not null
            && baseContextMenu.Items.Count > numberOfContextMenuItems)
            baseContextMenu.Items.RemoveAt(0);

        PassedTextControl.ContextMenu = baseContextMenu;

        int caretIndex = PassedTextControl.CaretIndex;

        AddPossibleSpellingErrorsToRightClickMenu(caretIndex);

        AddPossibleURLToRightClickMenu(caretIndex);

        AddPossibleMailToToRightClickMenu(caretIndex);

    }

    private void PassedTextControl_SelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateLineAndColumnText();
    }

    private void PassedTextControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLineAndColumnText();
        SetMargins(MarginsMenuItem.IsChecked is true);
    }

    private void PassedTextControl_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DefaultSettings.EditWindowStartFullscreen && prevWindowState is not null)
        {
            this.WindowState = prevWindowState.Value;
            prevWindowState = null;
        }

        UpdateLineAndColumnText();
    }

    private async void PasteExecuted(object sender, ExecutedRoutedEventArgs? e = null)
    {
        _IsAccessingClipboard = true;
        DataPackageView? dataPackageView = null;

        try
        {
            dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"error with Windows.ApplicationModel.DataTransfer.Clipboard.GetContent(). Exception Message: {ex.Message}");
        }

        if (dataPackageView is null)
        {
            _IsAccessingClipboard = false;
            return;
        }

        if (dataPackageView.Contains(StandardDataFormats.Text))
        {
            try
            {
                string textFromClipboard = await dataPackageView.GetTextAsync();
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { AddCopiedTextToTextBox(textFromClipboard); }));
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"error with dataPackageView.GetTextAsync(). Exception Message: {ex.Message}");
            }
        }
        else if (dataPackageView.Contains(StandardDataFormats.Bitmap))
        {
            try
            {
                RandomAccessStreamReference streamReference = await dataPackageView.GetBitmapAsync();
                using IRandomAccessStream stream = await streamReference.OpenReadAsync();
                List<OcrOutput> outputs = await OcrUtilities.GetTextFromRandomAccessStream(stream, LanguageUtilities.GetOCRLanguage());
                string text = OcrUtilities.GetStringFromOcrOutputs(outputs);

                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { AddCopiedTextToTextBox(text); }));
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"error with dataPackageView.GetBitmapAsync(). Exception Message: {ex.Message}");
            }
        }
        else if (dataPackageView.Contains(StandardDataFormats.StorageItems))
        {
            try
            {
                IReadOnlyList<IStorageItem> storageItems = await dataPackageView.GetStorageItemsAsync();
                foreach (IStorageItem storageItem in storageItems)
                {
                    if (!storageItem.IsOfType(StorageItemTypes.File))
                        continue;
                    IStorageFile storageFile = (IStorageFile)storageItem;
                    if (!IoUtilities.ImageExtensions.Contains(storageFile.FileType))
                        continue;

                    using IRandomAccessStream stream = await storageFile.OpenAsync(FileAccessMode.Read);
                    List<OcrOutput> outputs = await OcrUtilities.GetTextFromRandomAccessStream(stream, LanguageUtilities.GetOCRLanguage());
                    string text = OcrUtilities.GetStringFromOcrOutputs(outputs);

                    System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { AddCopiedTextToTextBox(text); }));
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"error with dataPackageView.GetStorageItemsAsync(). Exception Message: {ex.Message}");
            }
        }

        _IsAccessingClipboard = false;

        if (e is not null)
            e.Handled = true;
    }

    private async void PreviousRegion_Click(object sender, RoutedEventArgs e)
    {
        HistoryService hs = Singleton<HistoryService>.Instance;

        if (hs.HasAnyFullscreenHistory())
            await OcrUtilities.GetTextFromPreviousFullscreenRegion(PassedTextControl);
    }

    private async void RateAndReview_Click(object sender, RoutedEventArgs e)
    {
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format("ms-windows-store:REVIEW?PFN={0}", "40087JoeFinApps.TextGrab_kdbpvth5scec4")));
    }

    private void ReadEncodedString(string possiblyEncodedString)
    {
        string rawEncodedString = possiblyEncodedString[5..];
        try
        {
            // restore the padding '=' in base64 string
            switch (rawEncodedString.Length % 4)
            {
                case 2: rawEncodedString += "=="; break;
                case 3: rawEncodedString += "="; break;
            }
            byte[] encodedBytes = Convert.FromBase64String(rawEncodedString);
            string copiedText = Encoding.UTF8.GetString(encodedBytes);
            PassedTextControl.Text = copiedText;
        }
        catch (Exception ex)
        {
            PassedTextControl.Text = rawEncodedString;
            PassedTextControl.Text += ex.Message;
        }
    }
    private async void ReadFolderOfImages_Click(object sender, RoutedEventArgs e)
    {
        FolderBrowserDialog folderBrowserDialog = new();
        DialogResult result = folderBrowserDialog.ShowDialog();

        if (result is not System.Windows.Forms.DialogResult.OK)
            return;

        string chosenFolderPath = folderBrowserDialog.SelectedPath;

        OcrDirectoryOptions ocrDirectoryOptions = new()
        {
            Path = chosenFolderPath,
            IsRecursive = RecursiveFoldersCheck.IsChecked is true,
            WriteTxtFiles = ReadFolderOfImagesWriteTxtFiles.IsChecked is true,
            OutputFileNames = OutputFilenamesCheck.IsChecked is true,
            OutputFooter = OutputFooterCheck.IsChecked is true,
            OutputHeader = OutputHeaderCheck.IsChecked is true
        };

        if (Directory.Exists(chosenFolderPath))
            await OcrAllImagesInFolder(chosenFolderPath, ocrDirectoryOptions);
    }

    private void RemoveDuplicateLines_Click(object sender, RoutedEventArgs e)
    {
        PassedTextControl.Text = PassedTextControl.Text.RemoveDuplicateLines();
    }

    private void ReplaceReservedCharsCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        bool containsAnyReservedChars = false;

        if (PassedTextControl.SelectionLength > 0)
        {
            foreach (char reservedChar in StringMethods.ReservedChars)
            {
                if (PassedTextControl.SelectedText.Contains(reservedChar))
                    containsAnyReservedChars = true;
            }
        }
        else
        {
            foreach (char reservedChar in StringMethods.ReservedChars)
            {
                if (PassedTextControl.Text.Contains(reservedChar))
                    containsAnyReservedChars = true;
            }
        }

        if (containsAnyReservedChars)
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void ReplaceReservedCharsCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (PassedTextControl.SelectionLength > 0)
            PassedTextControl.SelectedText = PassedTextControl.SelectedText.ReplaceReservedCharacters();
        else
            PassedTextControl.Text = PassedTextControl.Text.ReplaceReservedCharacters();
    }

    private void RestorePositionMenuItem_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem restoreMenuItem)
            return;

        DefaultSettings.RestoreEtwPositions = restoreMenuItem.IsChecked;
    }

    private void RestoreThisPosition_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.SetWindowPosition(this);
    }

    private void RestoreWindowSettings()
    {
        if (DefaultSettings.EditWindowStartFullscreen
                    && string.IsNullOrWhiteSpace(OpenedFilePath)
                    && !LaunchedFromNotification)
        {
            WindowUtilities.LaunchFullScreenGrab(PassedTextControl);
            LaunchFullscreenOnLoad.IsChecked = true;
            prevWindowState = this.WindowState;
            WindowState = WindowState.Minimized;
        }

        if (DefaultSettings.EditWindowIsOnTop)
        {
            AlwaysOnTop.IsChecked = true;
            Topmost = true;
        }

        RestorePositionMenuItem.IsChecked = DefaultSettings.RestoreEtwPositions;

        if (DefaultSettings.RestoreEtwPositions)
            WindowUtilities.SetWindowPosition(this);

        if (!DefaultSettings.EditWindowIsWordWrapOn)
        {
            WrapTextMenuItem.IsChecked = false;
            PassedTextControl.TextWrapping = TextWrapping.NoWrap;
        }

        if (DefaultSettings.EditWindowBottomBarIsHidden)
        {
            HideBottomBarMenuItem.IsChecked = true;
            BottomBar.Visibility = Visibility.Collapsed;
        }

        if (DefaultSettings.EtwUseMargins)
        {
            MarginsMenuItem.IsChecked = true;
            SetMargins(true);
        }

        SetBottomBarButtons();
    }

    private void SaveAsBTN_Click(object sender, RoutedEventArgs e)
    {
        string fileText = PassedTextControl.Text;

        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            Filter = "Text Files(*.txt)|*.txt|All(*.*)|*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            RestoreDirectory = true,
        };

        if (dialog.ShowDialog() is true)
        {
            File.WriteAllText(dialog.FileName, fileText);
            OpenedFilePath = dialog.FileName;
            UiTitleBar.Title = $"Edit Text | {OpenedFilePath.Split('\\').LastOrDefault()}";
        }
    }

    private void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        string fileText = PassedTextControl.Text;

        if (string.IsNullOrEmpty(OpenedFilePath))
        {
            Microsoft.Win32.SaveFileDialog dialog = new()
            {
                Filter = "Text Files(*.txt)|*.txt|All(*.*)|*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() is true)
            {
                File.WriteAllText(dialog.FileName, fileText);
                OpenedFilePath = dialog.FileName;
                UiTitleBar.Title = $"Edit Text | {OpenedFilePath.Split('\\').LastOrDefault()}";
            }
        }
        else
        {
            File.WriteAllText(OpenedFilePath, fileText);
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        LaunchFindAndReplace();
    }

    private void SelectAllMenuItem_Click(Object? sender = null, RoutedEventArgs? e = null)
    {
        if (!IsLoaded)
            return;

        PassedTextControl.SelectAll();
    }

    private void SelectionContainsNewLinesCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (PassedTextControl.SelectedText.Contains(Environment.NewLine)
            || PassedTextControl.SelectedText.Contains('\r')
            || PassedTextControl.SelectedText.Contains('\n'))
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void SelectLine(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        (int lineStart, int lineLength) = PassedTextControl.Text.GetStartAndLengthOfLineAtPosition(PassedTextControl.SelectionStart);

        PassedTextControl.Select(lineStart, lineLength);
    }

    private void SelectLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SelectLine();
    }

    private void SelectNoneMenuItem_Click(Object? sender = null, RoutedEventArgs? e = null)
    {
        if (!IsLoaded)
            return;

        PassedTextControl.Select(0, 0);
    }

    private void SelectWord(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        (int wordStart, int wordLength) = PassedTextControl.Text.CursorWordBoundaries(PassedTextControl.CaretIndex);

        PassedTextControl.Select(wordStart, wordLength);
    }

    private void SelectWordMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SelectWord();
    }

    private void SetFontFromSettings()
    {
        PassedTextControl.FontFamily = new System.Windows.Media.FontFamily(DefaultSettings.FontFamilySetting);
        PassedTextControl.FontSize = DefaultSettings.FontSizeSetting;
        if (DefaultSettings.IsFontBold)
            PassedTextControl.FontWeight = FontWeights.Bold;
        if (DefaultSettings.IsFontItalic)
            PassedTextControl.FontStyle = FontStyles.Italic;

        TextDecorationCollection tdc = new();
        if (DefaultSettings.IsFontUnderline) tdc.Add(TextDecorations.Underline);
        if (DefaultSettings.IsFontStrikeout) tdc.Add(TextDecorations.Strikethrough);
        PassedTextControl.TextDecorations = tdc;
    }

    private void SetMargins(bool AreThereMargins)
    {

        if (AreThereMargins)
        {
            if (PassedTextControl.ActualWidth < 400)
                PassedTextControl.Padding = new Thickness(10, 0, 10, 0);
            else if (PassedTextControl.ActualWidth < 1000)
                PassedTextControl.Padding = new Thickness(50, 0, 50, 0);
            else if (PassedTextControl.ActualWidth < 1400)
                PassedTextControl.Padding = new Thickness(100, 0, 100, 0);
            else
                PassedTextControl.Padding = new Thickness(160, 0, 160, 0);
        }
        else
            PassedTextControl.Padding = new Thickness(0);
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<SettingsWindow>();
    }

    private void SetupRoutedCommands()
    {
        RoutedCommand newFullscreenGrab = new();
        _ = newFullscreenGrab.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(newFullscreenGrab, keyedCtrlF));

        RoutedCommand newGrabFrame = new();
        _ = newGrabFrame.InputGestures.Add(new KeyGesture(Key.G, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(newGrabFrame, keyedCtrlG));

        RoutedCommand selectLineCommand = new();
        _ = selectLineCommand.InputGestures.Add(new KeyGesture(Key.L, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(selectLineCommand, SelectLine));

        RoutedCommand IsolateSelectionCommand = new();
        _ = IsolateSelectionCommand.InputGestures.Add(new KeyGesture(Key.I, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(IsolateSelectionCommand, IsolateSelectionCmdExecuted));

        RoutedCommand SaveCommand = new();
        _ = SaveCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(SaveCommand, SaveBTN_Click));

        RoutedCommand SaveAsCommand = new();
        _ = SaveAsCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Shift | ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(SaveAsCommand, SaveAsBTN_Click));

        RoutedCommand OpenCommand = new();
        _ = OpenCommand.InputGestures.Add(new KeyGesture(Key.O, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(OpenCommand, OpenFileMenuItem_Click));

        RoutedCommand moveLineUpCommand = new();
        _ = moveLineUpCommand.InputGestures.Add(new KeyGesture(Key.Up, ModifierKeys.Alt));
        _ = CommandBindings.Add(new CommandBinding(moveLineUpCommand, MoveLineUp));

        RoutedCommand moveLineDownCommand = new();
        _ = moveLineDownCommand.InputGestures.Add(new KeyGesture(Key.Down, ModifierKeys.Alt));
        _ = CommandBindings.Add(new CommandBinding(moveLineDownCommand, MoveLineDown));

        RoutedCommand toggleCaseCommand = new();
        _ = toggleCaseCommand.InputGestures.Add(new KeyGesture(Key.F3, ModifierKeys.Shift));
        _ = CommandBindings.Add(new CommandBinding(toggleCaseCommand, ToggleCase));

        RoutedCommand replaceReservedCharsCommand = new();
        _ = replaceReservedCharsCommand.InputGestures.Add(new KeyGesture(Key.R, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(replaceReservedCharsCommand, ReplaceReservedCharsCmdExecuted));

        RoutedCommand UnstackCommand = new();
        _ = UnstackCommand.InputGestures.Add(new KeyGesture(Key.U, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(UnstackCommand, UnstackExecuted));

        RoutedCommand NewLookupCommand = new();
        _ = NewLookupCommand.InputGestures.Add(new KeyGesture(Key.Q, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(NewLookupCommand, LaunchQuickSimpleLookup));

        RoutedCommand selectWordCommand = new();
        _ = selectWordCommand.InputGestures.Add(new KeyGesture(Key.W, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(selectWordCommand, SelectWord));

        RoutedCommand pasteCommand = new();
        _ = pasteCommand.InputGestures.Add(new KeyGesture(Key.V, ModifierKeys.Control | ModifierKeys.Shift));
        _ = CommandBindings.Add(new CommandBinding(pasteCommand, PasteExecuted));

        RoutedCommand selectAllCommand = new();
        _ = selectAllCommand.InputGestures.Add(new KeyGesture(Key.A, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(selectAllCommand, SelectAllMenuItem_Click));

        RoutedCommand EscapeKeyed = new();
        _ = EscapeKeyed.InputGestures.Add(new KeyGesture(Key.Escape));
        _ = CommandBindings.Add(new CommandBinding(EscapeKeyed, KeyedEscape));

        RoutedCommand AddedLineAbove = new();
        _ = AddedLineAbove.InputGestures.Add(new KeyGesture(Key.Enter, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(AddedLineAbove, AddedLineAboveCommand));

        RoutedCommand duplicateLine = new();
        _ = duplicateLine.InputGestures.Add(new KeyGesture(Key.D, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(duplicateLine, DuplicateSelectedLine));
    }

    private void SingleLineCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        string textToOperateOn = GetSelectedTextOrAllText();

        if (textToOperateOn.Contains(Environment.NewLine)
            || textToOperateOn.Contains('\r')
            || textToOperateOn.Contains('\n'))
        {
            e.CanExecute = true;
            return;
        }

        e.CanExecute = false;
    }

    private void SingleLineCmdExecuted(object sender, ExecutedRoutedEventArgs? e = null)
    {
        if (PassedTextControl.SelectedText.Length > 0)
            PassedTextControl.SelectedText = PassedTextControl.SelectedText.MakeStringSingleLine();
        else
            PassedTextControl.Text = PassedTextControl.Text.MakeStringSingleLine();
    }

    private void SplitOnSelectionCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PassedTextControl.SelectedText))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void SplitOnSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string selectedText = PassedTextControl.SelectedText;

        if (string.IsNullOrEmpty(selectedText))
        {
            System.Windows.MessageBox.Show("No text selected", "Did not split lines");
            return;
        }

        StringBuilder textToManipulate = new(PassedTextControl.Text);

        textToManipulate = textToManipulate.Replace(selectedText, Environment.NewLine + selectedText);

        PassedTextControl.Text = textToManipulate.ToString();
    }

    private void ToggleCase(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string textToModify = GetSelectedTextOrAllText();

        if (CaseStatusOfToggle == CurrentCase.Unknown)
            CaseStatusOfToggle = StringMethods.DetermineToggleCase(textToModify);

        TextInfo currentTI = selectedCultureInfo.TextInfo;

        switch (CaseStatusOfToggle)
        {
            case CurrentCase.Lower:
                textToModify = currentTI.ToLower(textToModify);
                CaseStatusOfToggle = CurrentCase.Camel;
                break;
            case CurrentCase.Camel:
                textToModify = currentTI.ToTitleCase(textToModify);
                CaseStatusOfToggle = CurrentCase.Upper;
                break;
            case CurrentCase.Upper:
                textToModify = currentTI.ToUpper(textToModify);
                CaseStatusOfToggle = CurrentCase.Lower;
                break;
            default:
                break;
        }

        if (PassedTextControl.SelectionLength == 0)
            PassedTextControl.Text = textToModify;
        else
            PassedTextControl.SelectedText = textToModify;
    }

    private void ToggleCaseCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        bool containsLetters = false;
        string text = GetSelectedTextOrAllText();

        foreach (char letter in text)
            if (char.IsLetter(letter))
                containsLetters = true;

        if (containsLetters)
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void TrimEachLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string workingString = PassedTextControl.Text;
        string[] stringSplit = workingString.Split(Environment.NewLine);

        string finalString = "";
        foreach (string line in stringSplit)
            if (!string.IsNullOrWhiteSpace(line))
                finalString += line.Trim() + Environment.NewLine;

        PassedTextControl.Text = finalString;
    }

    private void TryToAlphaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string workingString = GetSelectedTextOrAllText();

        workingString = workingString.TryFixToLetters();

        if (PassedTextControl.SelectionLength == 0)
            PassedTextControl.Text = workingString;
        else
            PassedTextControl.SelectedText = workingString;
    }

    private void TryToNumberMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string workingString = GetSelectedTextOrAllText();

        workingString = workingString.TryFixToNumbers();

        if (PassedTextControl.SelectionLength == 0)
            PassedTextControl.Text = workingString;
        else
            PassedTextControl.SelectedText = workingString;
    }

    private void UnstackExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selection = Regex.Replace(PassedTextControl.SelectedText, @"(\r\n|\n|\r)", Environment.NewLine);
        string[] selectionLines = selection.Split(Environment.NewLine);
        int numberOfLines = selectionLines.Length;

        PassedTextControl.Text = PassedTextControl.Text.UnstackStrings(numberOfLines);
    }

    private void UnstackGroupExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selection = Regex.Replace(PassedTextControl.SelectedText, @"(\r\n|\n|\r)", Environment.NewLine);
        string[] selectionLines = selection.Split(Environment.NewLine);
        int numberOfLines = selectionLines.Length;

        PassedTextControl.Text = PassedTextControl.Text.UnstackGroups(numberOfLines);
    }

    private void UpdateLineAndColumnText()
    {
        if (PassedTextControl.SelectionLength < 1)
        {
            int lineNumber = PassedTextControl.GetLineIndexFromCharacterIndex(PassedTextControl.CaretIndex);
            int columnNumber = PassedTextControl.CaretIndex - PassedTextControl.GetCharacterIndexFromLineIndex(lineNumber);

            BottomBarText.Text = $"Ln {lineNumber + 1}, Col {columnNumber}";
        }
        else
        {
            int selectionStartIndex = PassedTextControl.SelectionStart;
            int selectionStopIndex = PassedTextControl.SelectionStart + PassedTextControl.SelectionLength;

            int selStartLine = PassedTextControl.GetLineIndexFromCharacterIndex(selectionStartIndex);

            if (selStartLine < 0)
            {
                BottomBarText.Text = $"Ln -, Col -";
                return;
            }

            int selStartCol = selectionStartIndex - PassedTextControl.GetCharacterIndexFromLineIndex(selStartLine);
            int selStopLine = PassedTextControl.GetLineIndexFromCharacterIndex(selectionStopIndex); ;
            int selStopCol = selectionStopIndex - PassedTextControl.GetCharacterIndexFromLineIndex(selStopLine); ;
            int selLength = PassedTextControl.SelectionLength;
            int numbOfSelectedLines = selStopLine - selStartLine;

            if (numbOfSelectedLines > 0)
                BottomBarText.Text = $"Ln {selStartLine + 1}:{selStopLine + 1}, Col {selStartCol}:{selStopCol}, Len {selLength}, Lines {numbOfSelectedLines + 1}";
            else
                BottomBarText.Text = $"Ln {selStartLine + 1}, Col {selStartCol}:{selStopCol}, Len {selLength}";
        }
    }

    private void Window_Activated(object sender, EventArgs e)
    {
        PassedTextControl.Focus();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        string windowSizeAndPosition = $"{this.Left},{this.Top},{this.Width},{this.Height}";
        DefaultSettings.EditTextWindowSizeAndPosition = windowSizeAndPosition;
        DefaultSettings.Save();

        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged -= Clipboard_ContentChanged;

        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is GrabFrame grabFrame)
            {
                grabFrame.DestinationTextBox = null;
            }
            else if (window is FullscreenGrab fullScreenGrab)
                fullScreenGrab.DestinationTextBox = null;
            else if (window is FindAndReplaceWindow findAndReplaceWindow)
                findAndReplaceWindow.ShouldCloseWithThisETW(this);
        }

        GC.Collect();
        WindowUtilities.ShouldShutDown();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (string.IsNullOrEmpty(OpenedFilePath)
            && !string.IsNullOrWhiteSpace(PassedTextControl.Text))
            Singleton<HistoryService>.Instance.SaveToHistory(this);
    }
    private void Window_Initialized(object sender, EventArgs e)
    {
        PassedTextControl.PreviewMouseWheel += HandlePreviewMouseWheel;
        SetFontFromSettings();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SetupRoutedCommands();

        PassedTextControl.ContextMenu = this.FindResource("ContextMenuResource") as ContextMenu;
        if (PassedTextControl.ContextMenu != null)
            numberOfContextMenuItems = PassedTextControl.ContextMenu.Items.Count;

        CheckRightToLeftLanguage();

        RestoreWindowSettings();

        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged -= Clipboard_ContentChanged;
        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged += Clipboard_ContentChanged;
    }

    private void WindowMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        OpenLastAsGrabFrameMenuItem.IsEnabled = Singleton<HistoryService>.Instance.HasAnyHistoryWithImages();
    }

    private void WrapTextCHBOX_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if ((bool)WrapTextMenuItem.IsChecked)
            PassedTextControl.TextWrapping = TextWrapping.Wrap;
        else
            PassedTextControl.TextWrapping = TextWrapping.NoWrap;

        DefaultSettings.EditWindowIsWordWrapOn = (bool)WrapTextMenuItem.IsChecked;
    }
    #endregion Methods
}
