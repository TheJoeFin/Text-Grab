using Humanizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

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
    public static RoutedCommand SplitAfterSelectionCmd = new();
    public static RoutedCommand ToggleCaseCmd = new();
    public static RoutedCommand UnstackCmd = new();
    public static RoutedCommand UnstackGroupCmd = new();
    public static RoutedCommand WebSearchCmd = new();
    public static RoutedCommand DefaultWebSearchCmd = new();
    public bool LaunchedFromNotification = false;
    private CancellationTokenSource? cancellationTokenForDirOCR;
    private readonly string historyId = string.Empty;
    private int numberOfContextMenuItems;
    private string? OpenedFilePath;
    private readonly DispatcherTimer EscapeKeyTimer = new();
    private int EscapeKeyTimerCount = 0;

    private WindowState? prevWindowState;
    private CultureInfo selectedCultureInfo = CultureInfo.CurrentCulture;
    private ILanguage selectedILanguage = LanguageUtilities.GetCurrentInputLanguage();

    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    // Remember last non-collapsed width for the calc column
    private GridLength _lastCalcColumnWidth = new(1, GridUnitType.Star);

    // Remember text wrapping state before showing calc pane
    private TextWrapping? _previousTextWrapping = null;

    // Store extracted pattern and precision for mouse wheel adjustment
    private ExtractedPattern? currentExtractedPattern = null;
    private int currentPrecisionLevel = ExtractedPattern.DefaultPrecisionLevel;
    private CalculationResult? calculationResult;

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

        if (historyInfo.HasCalcPaneOpen)
        {
            // use the tag to track that it was set from history item
            ShowCalcPaneMenuItem.Tag = true;
            ShowCalcPaneMenuItem.IsChecked = true;

            // Set the width to restore - use history width if valid, otherwise use default
            int widthToRestore = historyInfo.CalcPaneWidth > 0 ? historyInfo.CalcPaneWidth : DefaultSettings.CalcPaneWidth;
            if (widthToRestore <= 0)
                widthToRestore = 400; // Fallback to default

            CalcColumn.Width = new GridLength(widthToRestore, GridUnitType.Pixel);
            _lastCalcColumnWidth = new GridLength(widthToRestore, GridUnitType.Pixel);
        }
    }

    #endregion Constructors

    #region Properties

    public CurrentCase CaseStatusOfToggle { get; set; } = CurrentCase.Unknown;

    public bool WrapText { get; set; } = false;
    private bool IsAccessingClipboard { get; set; } = false;

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
            {nameof(MakeQrCodeCmd), MakeQrCodeCmd},
            {nameof(WebSearchCmd), WebSearchCmd},
            {nameof(DefaultWebSearchCmd), DefaultWebSearchCmd},
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
            PassedTextControl.AppendText($"Failed to read directory: {ex.Message}{Environment.NewLine}");
        }

        if (files is null)
            return;

        List<string> imageFiles = [.. files.Where(x => IoUtilities.ImageExtensions.Contains(Path.GetExtension(x).ToLower()))];

        if (imageFiles.Count == 0)
        {
            PassedTextControl.AppendText($"{folderPath} contains no images");
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

        if (DefaultSettings.EtwShowLangPicker)
        {
            LanguagePicker languagePicker = new();
            languagePicker.LanguageChanged -= LanguagePicker_LanguageChanged;
            languagePicker.LanguageChanged += LanguagePicker_LanguageChanged;
            BottomBarButtons.Children.Add(languagePicker);
        }
    }

    private void LanguagePicker_LanguageChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not LanguagePicker languagePicker)
            return;

        selectedILanguage = languagePicker.SelectedLanguage;

        string tag = selectedILanguage.LanguageTag;

        foreach (MenuItem item in LanguageMenuItem.Items)
        {
            if (item.Tag is ILanguage iLanguageFromTag && iLanguageFromTag.LanguageTag == tag)
                item.IsChecked = true;
            else
                item.IsChecked = false;
        }

        if (selectedILanguage is WindowsAiLang)
        {
            SetCultureAndLanguageToDefault();
            return;
        }

        CultureInfo cultureInfo = new(selectedILanguage.LanguageTag);
        selectedCultureInfo = cultureInfo;
        XmlLanguage xmlLang = XmlLanguage.GetLanguage(selectedILanguage.LanguageTag);
        Language = xmlLang;
    }

    private void SetCultureAndLanguageToDefault()
    {
        selectedCultureInfo = CultureInfo.CurrentCulture;
        string currentInputTag = Windows.Globalization.Language.CurrentInputMethodLanguageTag;
        XmlLanguage xmlDefaultLang = XmlLanguage.GetLanguage(currentInputTag);
        Language = xmlDefaultLang;
    }

    internal HistoryInfo AsHistoryItem()
    {
        int calcPaneWidth = 0;
        if (ShowCalcPaneMenuItem.IsChecked is true && CalcColumn.Width.Value > 0)
        {
            if (CalcColumn.Width.IsStar)
                calcPaneWidth = (int)CalcColumn.ActualWidth;
            else
                calcPaneWidth = (int)CalcColumn.Width.Value;
        }

        HistoryInfo historyInfo = new()
        {
            ID = historyId,
            LanguageTag = LanguageUtilities.GetCurrentInputLanguage().LanguageTag,
            LanguageKind = LanguageKind.Global,
            CaptureDateTime = DateTimeOffset.Now,
            TextContent = PassedTextControl.Text,
            SourceMode = TextGrabMode.EditText,
            CalcPaneWidth = calcPaneWidth,
            HasCalcPaneOpen = ShowCalcPaneMenuItem.IsChecked is true
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

        (string TextContent, OpenContentKind KindOpened) = await IoUtilities.GetContentFromPath(pathOfFileToOpen, isMultipleFiles, selectedILanguage);

        if (KindOpened == OpenContentKind.TextFile
            && !isMultipleFiles
            && !string.IsNullOrWhiteSpace(TextContent))
            UiTitleBar.Title = $"Edit Text | {Path.GetFileName(OpenedFilePath)}";

        PassedTextControl.AppendText(TextContent);
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
        IsAccessingClipboard = true;
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
            IsAccessingClipboard = false;
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
        if (ClipboardWatcherMenuItem.IsChecked is false || IsAccessingClipboard)
            return;

        IsAccessingClipboard = true;
        DataPackageView? dataPackageView = null;

        try
        {
            dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        }
        catch (Exception ex)
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
            catch (Exception ex)
            {
                Debug.WriteLine($"error with dataPackageView.GetTextAsync(). Exception Message: {ex.Message}");
            }
        }
        ;

        IsAccessingClipboard = false;
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
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            ProcessBottomBarKeyPress(e);

        if (e.Key == Key.Escape)
        {
            cancellationTokenForDirOCR?.Cancel();
            EscapeKeyTimerCount++;

            if (EscapeKeyTimerCount == 1)
                EscapeKeyTimer.Start();
        }
    }

    private void ProcessBottomBarKeyPress(System.Windows.Input.KeyEventArgs e)
    {
        UIElementCollection bottomBarButtons = BottomBarButtons.Children;

        int keyNumberPressed = (int)e.Key - 35;

        // D1 is 35
        // ...
        // D9 is 43
        // D0 is 34

        if (keyNumberPressed is < (-1)
            or > 8)
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
            correspondingButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
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
        if (e.Data.GetDataPresent("Text"))
            return;

        // Mark the event as handled, so TextBox's native Drop handler is not called.
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
        System.Drawing.Font currentFont = new(PassedTextControl.FontFamily.ToString(), (float)(PassedTextControl.FontSize * 72.0 / 96.0));
        fd.Font = currentFont;
        DialogResult result = fd.ShowDialog();
        if (result != System.Windows.Forms.DialogResult.OK)
            return;

        Debug.WriteLine(fd.Font);

        DefaultSettings.FontFamilySetting = fd.Font.Name;
        DefaultSettings.FontSizeSetting = fd.Font.Size * 96.0 / 72.0;
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

    public string GetSelectedTextOrAllText()
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

    // Keep calc pane scroll in sync with main text box
    private void PassedTextControl_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        try
        {
            if (CalcResultsTextControl.Visibility != Visibility.Visible)
                return;

            // Obtain internal ScrollViewers for both text boxes
            if (WindowUtilities.GetScrollViewer(PassedTextControl) is ScrollViewer mainSv &&
                WindowUtilities.GetScrollViewer(CalcResultsTextControl) is ScrollViewer calcSv)
            {
                // Mirror vertical offset only (horizontal can differ due to content widths)
                if (!NumericUtilities.AreClose(calcSv.VerticalOffset, mainSv.VerticalOffset))
                    calcSv.ScrollToVerticalOffset(mainSv.VerticalOffset);
            }
        }
        catch { /* no-op */ }
    }

    private void SyncCalcScrollToMain()
    {
        try
        {
            if (CalcResultsTextControl.Visibility != Visibility.Visible)
                return;

            ScrollViewer? mainSv = WindowUtilities.GetScrollViewer(PassedTextControl);
            ScrollViewer? calcSv = WindowUtilities.GetScrollViewer(CalcResultsTextControl);
            if (mainSv is null || calcSv is null)
                return;

            if (!NumericUtilities.AreClose(calcSv.VerticalOffset, mainSv.VerticalOffset))
                calcSv.ScrollToVerticalOffset(mainSv.VerticalOffset);
        }
        catch { /* no-op */ }
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
        string[] splitString = PassedTextControl.Text.Split([Environment.NewLine], StringSplitOptions.None);
        string selectionText = PassedTextControl.SelectedText;
        int initialSelectionStart = PassedTextControl.SelectionStart;
        int selectionPositionInLine = PassedTextControl.SelectionStart;
        for (int i = initialSelectionStart; i >= 0; i--)
        {
            if (PassedTextControl.Text[i] is '\n'
                or '\r')
            {
                selectionPositionInLine = initialSelectionStart - i - 1;
                break;
            }
        }

        int selectionLength = PassedTextControl.SelectionLength;

        if (string.IsNullOrEmpty(splitString.Last()))
            splitString = [.. splitString.SkipLast(1)];

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
                    sb.Append(line).Append(selectionText.PadLeft(selectionPositionInLine + selectionLength - line.Length));
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

    private async void GoogleSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = PassedTextControl.SelectedText;
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format($"https://www.google.com/search?q={searchStringUrlSafe}")));
    }

    private async void BingSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = PassedTextControl.SelectedText;
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format($"https://www.bing.com/search?q={searchStringUrlSafe}")));
    }

    private async void DuckDuckGoSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = PassedTextControl.SelectedText;
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format($"https://duckduckgo.com/?va=d&t=he&q={searchStringUrlSafe}&ia=web")));
    }

    private async void GitHubSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = PassedTextControl.SelectedText;
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format($"https://github.com/search?q={searchStringUrlSafe}")));
    }

    private async void WebSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = PassedTextControl.SelectedText;
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);

        if (e.Parameter is not WebSearchUrlModel webSearcher)
            return;

        Uri searchUri = new($"{webSearcher.Url}{searchStringUrlSafe}");
        _ = await Windows.System.Launcher.LaunchUriAsync(searchUri);
    }

    private async void DefaultWebSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = PassedTextControl.SelectedText;
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);

        WebSearchUrlModel searcher = Singleton<WebSearchUrlModel>.Instance.DefaultSearcher;

        Uri searchUri = new($"{searcher.Url}{searchStringUrlSafe}");
        _ = await Windows.System.Launcher.LaunchUriAsync(searchUri);
    }

    private void KeyedCtrlF(object sender, ExecutedRoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab(PassedTextControl);
    }

    private void KeyedCtrlG(object sender, ExecutedRoutedEventArgs e)
    {
        CheckForGrabFrameOrLaunch();
    }

    private void KeyedEscape(object sender, ExecutedRoutedEventArgs e)
    {
        cancellationTokenForDirOCR?.Cancel();
    }

    private void OpenRecentEditWindowExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string currentText = PassedTextControl.Text;

        HistoryInfo? historyInfo = Singleton<HistoryService>.Instance.GetEditWindows().LastOrDefault();

        if (historyInfo is null)
        {
            // No history available, just open a new window
            EditTextWindow etw = new();
            etw.Show();
            return;
        }

        EditTextWindow etwHistory = new(historyInfo);
        etwHistory.Show();
        etwHistory.Activate();

        if (string.IsNullOrWhiteSpace(currentText))
            Close();
    }

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (LanguageMenuItem is null || sender is not MenuItem clickedMenuItem)
            return;

        if (clickedMenuItem.Tag is not ILanguage ILang)
            return;

        selectedILanguage = ILang;

        try
        {
            CultureInfo cultureInfo = new(selectedILanguage.LanguageTag);
            selectedCultureInfo = cultureInfo;
            XmlLanguage xmlLang = XmlLanguage.GetLanguage(cultureInfo.IetfLanguageTag);
            Language = xmlLang;
        }
        catch (CultureNotFoundException)
        {
            SetCultureAndLanguageToDefault();
        }

        foreach (object? child in BottomBarButtons.Children)
            if (child is LanguagePicker languagePicker)
                languagePicker.Select(selectedILanguage.LanguageTag);

        foreach (MenuItem menuItem in LanguageMenuItem.Items)
            menuItem.IsChecked = false;

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
            PassedTextControl.AppendText(IoUtilities.ListFilesFoldersInDirectory(chosenFolderPath));
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

        bool haveSetLastLang = false;
        string lastTextLang = DefaultSettings.LastUsedLang;
        bool usingTesseract = DefaultSettings.UseTesseract && TesseractHelper.CanLocateTesseractExe();

        if (WindowsAiUtilities.CanDeviceUseWinAI())
        {
            WindowsAiLang windowsAiLang = new();

            MenuItem languageMenuItem = new()
            {
                Header = windowsAiLang.DisplayName,
                Tag = windowsAiLang,
                IsCheckable = true,
            };

            languageMenuItem.Click += LanguageMenuItem_Click;
            captureMenuItem.Items.Add(languageMenuItem);
            if (!haveSetLastLang && windowsAiLang.CultureDisplayName == lastTextLang)
            {
                languageMenuItem.IsChecked = true;
                haveSetLastLang = true;
            }
        }

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

        ILanguage firstLang = LanguageUtilities.GetOCRLanguage();

        foreach (Language language in possibleOCRLanguages)
        {
            MenuItem languageMenuItem = new()
            {
                Header = language.DisplayName,
                Tag = new GlobalLang(language),
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
        grabsHistories = [.. grabsHistories.OrderByDescending(x => x.CaptureDateTime)];

        OpenRecentMenuItem.Items.Clear();

        if (grabsHistories.Count < 1)
        {
            OpenRecentMenuItem.IsEnabled = false;
            return;
        }

        foreach (HistoryInfo history in grabsHistories)
        {
            MenuItem menuItem = new();
            menuItem.Click += (sender, args) =>
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
        PassedTextControl.SelectedText = $"{lineText}{(lineEndsInNewLine ? "" : Environment.NewLine)}{lineText}";
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

            ocrFile.OcrResult = await OcrUtilities.OcrFile(ocrFile.FilePath, selectedLanguage, options);

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
            Filter = "Text documents (.txt)|*.txt|All files (*.*)|*.*",
            DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
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

        // Reset the debounce timer
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
        // If a newline append auto-scrolls the main box, ensure calc scroll follows too
        // Schedule after layout so offsets are accurate
        Dispatcher.BeginInvoke(SyncCalcScrollToMain, DispatcherPriority.Background);
    }

    private DispatcherTimer? _debounceTimer = null;
    private const int DEBOUNCE_DELAY_MS = 300;
    private readonly CalculationService _calculationService = new();

    // Aggregate tracking for calc pane status display
    private enum AggregateType { None, Sum, Average, Count, Min, Max, Median, Product }
    private AggregateType _selectedAggregate = AggregateType.None;

    private void InitializeExpressionEvaluator()
    {
        // Set up debounce timer to avoid excessive calculations
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DEBOUNCE_DELAY_MS)
        };
        _debounceTimer.Tick += DebounceTimer_Tick;
    }

    private async void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();

        if (CalcResultsTextControl.Visibility != Visibility.Visible)
            return;

        await EvaluateExpressions();
    }

    private async Task EvaluateExpressions()
    {
        // Don't waste cycles if the pane isn't visible
        if (CalcResultsTextControl.Visibility != Visibility.Visible)
            return;

        string input = PassedTextControl.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            CalcResultsTextControl.Text = "";
            _calculationService.ClearParameters();
            UpdateAggregateStatusDisplay();
            // Keep scrolls aligned even when clearing
            Dispatcher.BeginInvoke(SyncCalcScrollToMain, DispatcherPriority.Render);
            return;
        }

        // Update calculation service settings
        _calculationService.CultureInfo = selectedCultureInfo ?? CultureInfo.CurrentCulture;
        _calculationService.ShowErrors = ShowErrorsMenuItem.IsChecked == true;

        // Evaluate expressions using the service
        calculationResult = await _calculationService.EvaluateExpressionsAsync(input);

        // Update the text control with results
        CalcResultsTextControl.Text = calculationResult.Output;

        // Update the aggregate status display if an aggregate is selected
        UpdateAggregateStatusDisplay();

        // After updating calc text, its ScrollViewer resets; resync to main scroll
        Dispatcher.BeginInvoke(SyncCalcScrollToMain, DispatcherPriority.Render);

        // Optional status (kept commented)
        // if (result.ErrorCount == 0) { } else { }
    }

    private async void ShowErrorsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Re-evaluate expressions when toggle changes
        await EvaluateExpressions();
    }

    protected override void OnClosed(EventArgs e)
    {
        _debounceTimer?.Stop();
        base.OnClosed(e);
    }

    private async void PasteExecuted(object sender, ExecutedRoutedEventArgs? e = null)
    {
        IsAccessingClipboard = true;
        DataPackageView? dataPackageView = null;

        try
        {
            dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"error with Windows.ApplicationModel.DataTransfer.Clipboard.GetContent(). Exception Message: {ex.Message}");
        }

        if (dataPackageView is null)
        {
            IsAccessingClipboard = false;
            return;
        }

        if (dataPackageView.Contains(StandardDataFormats.Text))
        {
            try
            {
                string textFromClipboard = await dataPackageView.GetTextAsync();
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { AddCopiedTextToTextBox(textFromClipboard); }));
            }
            catch (Exception ex)
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
            catch (Exception ex)
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
            catch (Exception ex)
            {
                Debug.WriteLine($"error with dataPackageView.GetStorageItemsAsync(). Exception Message: {ex.Message}");
            }
        }

        IsAccessingClipboard = false;

        e?.Handled = true;
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
        PassedTextControl.FontFamily = new FontFamily(DefaultSettings.FontFamilySetting);
        PassedTextControl.FontSize = DefaultSettings.FontSizeSetting;
        if (DefaultSettings.IsFontBold)
            PassedTextControl.FontWeight = FontWeights.Bold;
        if (DefaultSettings.IsFontItalic)
            PassedTextControl.FontStyle = FontStyles.Italic;

        TextDecorationCollection tdc = [];
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
        _ = CommandBindings.Add(new CommandBinding(newFullscreenGrab, KeyedCtrlF));

        RoutedCommand newGrabFrame = new();
        _ = newGrabFrame.InputGestures.Add(new KeyGesture(Key.G, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(newGrabFrame, KeyedCtrlG));

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

        RoutedCommand toggleCalcPane = new();
        _ = toggleCalcPane.InputGestures.Add(new KeyGesture(Key.P, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(toggleCalcPane, ToggleCalcPaneExecuted));

        RoutedCommand openRecentEditWindow = new();
        _ = openRecentEditWindow.InputGestures.Add(new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Shift));
        _ = CommandBindings.Add(new CommandBinding(openRecentEditWindow, OpenRecentEditWindowExecuted));

        List<WebSearchUrlModel> searchers = Singleton<WebSearchUrlModel>.Instance.WebSearchers;

        foreach (WebSearchUrlModel searcher in searchers)
        {
            MenuItem searchItem = new()
            {
                Header = $"Search with {searcher.Name}...",
                Command = WebSearchCmd,
                CommandParameter = searcher,
            };

            WebSearchCollection.Items.Add(searchItem);
        }
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

    private void SplitAfterSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string selectedText = PassedTextControl.SelectedText;

        if (string.IsNullOrEmpty(selectedText))
        {
            System.Windows.MessageBox.Show("No text selected", "Did not split lines");
            return;
        }

        StringBuilder textToManipulate = new(PassedTextControl.Text);

        textToManipulate = textToManipulate.Replace(selectedText, selectedText + Environment.NewLine);

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
        string selection = NewlineReturns().Replace(PassedTextControl.SelectedText, Environment.NewLine);
        string[] selectionLines = selection.Split(Environment.NewLine);
        int numberOfLines = selectionLines.Length;

        PassedTextControl.Text = PassedTextControl.Text.UnstackStrings(numberOfLines);
    }

    private void UnstackGroupExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selection = NewlineReturns().Replace(PassedTextControl.SelectedText, Environment.NewLine);
        string[] selectionLines = selection.Split(Environment.NewLine);
        int numberOfLines = selectionLines.Length;

        PassedTextControl.Text = PassedTextControl.Text.UnstackGroups(numberOfLines);
    }

    private void UpdateLineAndColumnText()
    {
        char[] delimiters = [' ', '\r', '\n'];

        if (PassedTextControl.SelectionLength < 1)
        {
            int lineNumber = PassedTextControl.GetLineIndexFromCharacterIndex(PassedTextControl.CaretIndex);
            int columnNumber = PassedTextControl.CaretIndex - PassedTextControl.GetCharacterIndexFromLineIndex(lineNumber);
            int words = PassedTextControl.Text.RemoveNonWordChars().Split(delimiters, StringSplitOptions.RemoveEmptyEntries).Length;

            string text = DefaultSettings.EtwShowWordCount
                ? $"Wrds {words}, Ln {lineNumber + 1}, Col {columnNumber}"
                : $"Ln {lineNumber + 1}, Col {columnNumber}";

            BottomBarText.Text = text;

            // Hide selection-specific UI elements
            MatchCountButton.Visibility = Visibility.Collapsed;
            RegexPatternButton.Visibility = Visibility.Collapsed;
            SimilarMatchesButton.Visibility = Visibility.Collapsed;
            CharDetailsButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            int selectionStartIndex = PassedTextControl.SelectionStart;
            int selectionStopIndex = PassedTextControl.SelectionStart + PassedTextControl.SelectionLength;
            int words = PassedTextControl.Text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).Length;

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

            // Update selection-specific UI elements
            UpdateSelectionSpecificUI();
        }
    }

    private void UpdateSelectionSpecificUI()
    {
        string selectedText = PassedTextControl.SelectedText;

        if (string.IsNullOrEmpty(selectedText))
        {
            MatchCountButton.Visibility = Visibility.Collapsed;
            SimilarMatchesButton.Visibility = Visibility.Collapsed;
            RegexPatternButton.Visibility = Visibility.Collapsed;
            CharDetailsButton.Visibility = Visibility.Collapsed;
            return;
        }

        // Show character details for single character selection
        if (DefaultSettings.EtwShowCharDetails && selectedText.Length == 1)
        {
            char selectedChar = selectedText[0];
            int codePoint = char.ConvertToUtf32(selectedText, 0);
            string unicodeHex = $"U+{codePoint:X4}";

            CharDetailsButtonText.Text = unicodeHex;
            CharDetailsButton.ToolTip = $"{unicodeHex}: {CharacterUtilities.GetUnicodeCategory(selectedChar)}";
            CharDetailsButton.Visibility = Visibility.Visible;
        }
        else if (DefaultSettings.EtwShowCharDetails && selectedText.Length > 1)
        {
            CharDetailsButtonText.Text = $"{selectedText.Length} chars";
            CharDetailsButton.ToolTip = "Click to see character details";
            CharDetailsButton.Visibility = Visibility.Visible;
        }
        else
        {
            CharDetailsButton.Visibility = Visibility.Collapsed;
        }

        // Show match count
        if (DefaultSettings.EtwShowMatchCount && !string.IsNullOrEmpty(selectedText))
        {
            int matchCount = StringMethods.CountMatches(PassedTextControl.Text, selectedText);
            if (MatchCountButton.Content is TextBlock matchButton)
            {
                matchButton.Text = matchCount == 1 ? "1 match" : $"{matchCount} matches";
            }
            MatchCountButton.Visibility = matchCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            MatchCountButton.Visibility = Visibility.Collapsed;
        }

        // Show similar matches count using regex pattern
        if (DefaultSettings.EtwShowSimilarMatches && !string.IsNullOrEmpty(selectedText) && selectedText.Length > 0 && selectedText.Length <= 50)
        {
            // Generate and store the ExtractedPattern if the selection changed
            if (currentExtractedPattern is null || currentExtractedPattern.OriginalText != selectedText)
            {
                currentExtractedPattern = new ExtractedPattern(selectedText, ignoreCase: true);
                currentPrecisionLevel = ExtractedPattern.DefaultPrecisionLevel;
            }

            string regexPattern = currentExtractedPattern.GetPattern(currentPrecisionLevel);
            int similarCount = StringMethods.CountRegexMatches(PassedTextControl.Text, regexPattern);
            if (SimilarMatchesButton.Content is TextBlock similarButton)
            {
                similarButton.Text = similarCount == 1 ? "1 similar" : $"{similarCount} similar";
            }
            string levelLabel = ExtractedPattern.GetLevelLabel(currentPrecisionLevel);
            SimilarMatchesButton.ToolTip = $"Click to Find and Replace with: {regexPattern}\n(Precision: {levelLabel})\nScroll mouse wheel to adjust precision";
            SimilarMatchesButton.Visibility = similarCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            SimilarMatchesButton.Visibility = Visibility.Collapsed;
        }

        // Show regex pattern
        if (DefaultSettings.EtwShowRegexPattern && !string.IsNullOrEmpty(selectedText) && selectedText.Length > 0 && selectedText.Length <= 50)
        {
            // Generate and store the ExtractedPattern if the selection changed
            if (currentExtractedPattern is null || currentExtractedPattern.OriginalText != selectedText)
            {
                currentExtractedPattern = new ExtractedPattern(selectedText, ignoreCase: true);
                currentPrecisionLevel = ExtractedPattern.DefaultPrecisionLevel;
            }

            string regexPattern = currentExtractedPattern.GetPattern(currentPrecisionLevel);
            if (RegexPatternButton.Content is TextBlock regexButton)
            {
                regexButton.Text = regexPattern.Length > 30
                    ? $"Regex: {regexPattern[..27]}..."
                    : $"Regex: {regexPattern}";
            }
            string levelLabel = ExtractedPattern.GetLevelLabel(currentPrecisionLevel);
            RegexPatternButton.ToolTip = $"Click to Find and Replace with: {regexPattern}\n(Precision: {levelLabel})\nScroll mouse wheel to adjust precision";
            RegexPatternButton.Visibility = Visibility.Visible;
        }
        else
        {
            RegexPatternButton.Visibility = Visibility.Collapsed;
        }
    }

    private string GenerateRegexPattern(string text)
    {
        // Use the stored ExtractedPattern if available and matches current text
        if (currentExtractedPattern is not null && currentExtractedPattern.OriginalText == text)
        {
            return currentExtractedPattern.GetPattern(currentPrecisionLevel);
        }

        // Otherwise create new pattern at default precision
        ExtractedPattern extractedPattern = new(text);
        return extractedPattern.GetPattern(ExtractedPattern.DefaultPrecisionLevel);
    }

    private void MatchCountButton_Click(object sender, RoutedEventArgs e)
    {
        // Open find and replace with the selection pre-loaded
        LaunchFindAndReplace();
    }

    private void SimilarMatchesButton_Click(object sender, RoutedEventArgs e)
    {
        string selectedText = PassedTextControl.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
            return;

        // Use the stored ExtractedPattern if available, otherwise create new one
        ExtractedPattern extractedPattern;
        if (currentExtractedPattern is not null && currentExtractedPattern.OriginalText == selectedText)
        {
            extractedPattern = currentExtractedPattern;
        }
        else
        {
            extractedPattern = new ExtractedPattern(selectedText, ignoreCase: true);
        }

        // Launch Find and Replace with regex enabled and execute search
        FindAndReplaceWindow findAndReplaceWindow = WindowUtilities.OpenOrActivateWindow<FindAndReplaceWindow>();
        findAndReplaceWindow.TextEditWindow = this;
        findAndReplaceWindow.StringFromWindow = PassedTextControl.Text;
        findAndReplaceWindow.FindByPattern(extractedPattern, currentPrecisionLevel);
        findAndReplaceWindow.Show();
    }

    private void RegexPatternButton_Click(object sender, RoutedEventArgs e)
    {
        string selectedText = PassedTextControl.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
            return;

        // Use the stored ExtractedPattern if available, otherwise create new one
        ExtractedPattern extractedPattern;
        if (currentExtractedPattern is not null && currentExtractedPattern.OriginalText == selectedText)
        {
            extractedPattern = currentExtractedPattern;
        }
        else
        {
            extractedPattern = new ExtractedPattern(selectedText, ignoreCase: true);
        }

        // Launch Find and Replace with regex enabled and execute search
        FindAndReplaceWindow findAndReplaceWindow = WindowUtilities.OpenOrActivateWindow<FindAndReplaceWindow>();
        findAndReplaceWindow.TextEditWindow = this;
        findAndReplaceWindow.StringFromWindow = PassedTextControl.Text;
        findAndReplaceWindow.FindByPattern(extractedPattern, currentPrecisionLevel);
        findAndReplaceWindow.Show();
    }

    private void PatternButton_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Only handle if we have a valid ExtractedPattern
        if (currentExtractedPattern is null)
            return;

        // Determine scroll direction for animation
        bool scrollingUp = e.Delta > 0;

        // Adjust precision level based on scroll direction
        if (scrollingUp)
        {
            // Scroll up = increase precision (more specific pattern)
            currentPrecisionLevel = Math.Min(currentPrecisionLevel + 1, ExtractedPattern.MaxPrecisionLevel);
        }
        else if (e.Delta < 0)
        {
            // Scroll down = decrease precision (more general pattern)
            currentPrecisionLevel = Math.Max(currentPrecisionLevel - 1, ExtractedPattern.MinPrecisionLevel);
        }

        // Update the UI to reflect the new precision level
        UpdateSelectionSpecificUI();

        // Add visual feedback animation to make the precision change more obvious
        AnimatePrecisionChange(sender, scrollingUp);

        // Mark the event as handled so it doesn't bubble up
        e.Handled = true;
    }

    private void AnimatePrecisionChange(object sender, bool scrollingUp)
    {
        if (sender is not System.Windows.Controls.Button button)
            return;

        // Ensure the button has a RenderTransform for translation
        if (button.RenderTransform is not TranslateTransform)
        {
            button.RenderTransform = new TranslateTransform(0, 0);
        }

        TranslateTransform translateTransform = (TranslateTransform)button.RenderTransform;

        // Create a slide animation based on scroll direction
        // Scrolling up (increasing precision) = slide text up (negative Y)
        // Scrolling down (decreasing precision) = slide text down (positive Y)
        double slideDistance = 10;
        double startY = scrollingUp ? slideDistance : -slideDistance;

        DoubleAnimation slideAnimation = new()
        {
            From = startY,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // Apply the animation to Y translation
        translateTransform.BeginAnimation(TranslateTransform.YProperty, slideAnimation);
    }

    private void CharDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        string selectedText = PassedTextControl.SelectedText;

        if (string.IsNullOrEmpty(selectedText))
            return;

        CharDetailsPopupContent.Children.Clear();

        if (selectedText.Length == 1)
        {
            // Show details for single character in multi-line TextBox
            char c = selectedText[0];
            string details = CharacterUtilities.GetCharacterDetailsText(c);

            System.Windows.Controls.TextBox detailsTextBox = new()
            {
                Text = details,
                FontSize = 12,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                TextWrapping = TextWrapping.Wrap,
                Cursor = System.Windows.Input.Cursors.Arrow,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            CharDetailsPopupContent.Children.Add(detailsTextBox);
        }
        else
        {
            // Show details for multiple characters in one multi-line TextBox
            StringBuilder allDetails = new();
            allDetails.AppendLine($"Character Details ({selectedText.Length} characters)");
            allDetails.AppendLine();

            // Limit to first 10 characters to avoid huge popup
            int charLimit = Math.Min(selectedText.Length, 10);
            for (int i = 0; i < charLimit; i++)
            {
                char c = selectedText[i];
                allDetails.AppendLine(CharacterUtilities.GetCharacterDetailsText(c));

                if (i < charLimit - 1)
                    allDetails.AppendLine(); // Add blank line between characters
            }

            if (selectedText.Length > charLimit)
            {
                allDetails.AppendLine();
                allDetails.AppendLine($"... and {selectedText.Length - charLimit} more");
            }

            System.Windows.Controls.TextBox detailsTextBox = new()
            {
                Text = allDetails.ToString(),
                FontSize = 12,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                TextWrapping = TextWrapping.Wrap,
                Cursor = System.Windows.Input.Cursors.Arrow,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 400
            };
            CharDetailsPopupContent.Children.Add(detailsTextBox);
        }

        CharDetailsPopup.IsOpen = true;
    }

    private void Window_Activated(object sender, EventArgs e)
    {
        PassedTextControl.Focus();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        string windowSizeAndPosition = $"{this.Left},{this.Top},{this.Width},{this.Height}";
        DefaultSettings.EditTextWindowSizeAndPosition = windowSizeAndPosition;

        // Save calc pane width to settings when closing with pane open
        if (ShowCalcPaneMenuItem.IsChecked is true && CalcColumn.Width.Value > 0)
        {
            if (CalcColumn.Width.IsStar)
                DefaultSettings.CalcPaneWidth = (int)CalcColumn.ActualWidth;
            else
                DefaultSettings.CalcPaneWidth = (int)CalcColumn.Width.Value;
        }

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

        EscapeKeyTimer.Interval = TimeSpan.FromMilliseconds(700);
        EscapeKeyTimer.Tick += EscapeKeyTimer_Tick;

        InitializeExpressionEvaluator();

        // Restore calc pane width from settings if not loading from history
        if (ShowCalcPaneMenuItem.Tag is not true && DefaultSettings.CalcPaneWidth > 0)
        {
            _lastCalcColumnWidth = new GridLength(DefaultSettings.CalcPaneWidth, GridUnitType.Pixel);
        }

        if (ShowCalcPaneMenuItem.Tag is not true)
            ShowCalcPaneMenuItem.IsChecked = DefaultSettings.CalcShowPane;

        ShowErrorsMenuItem.IsChecked = DefaultSettings.CalcShowErrors;
        SetCalcPaneVisibility();

        // Wire up calc pane context menu
        HideCalcPaneContextItem.Click += HideCalcPaneContextItem_Click;
        ShowCalcErrorsContextItem.Click += ShowCalcErrorsContextItem_Click;
        CopyAllContextItem.Click += CopyAllContextItem_Click;

        // Attach scrolling synchronization
        try
        {
            PassedTextControl.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(PassedTextControl_ScrollChanged), true);
            CalcResultsTextControl.PreviewMouseWheel -= CalcResultsTextControl_PreviewMouseWheel;
            CalcResultsTextControl.PreviewMouseWheel += CalcResultsTextControl_PreviewMouseWheel;
        }
        catch { /* ignore if not ready yet */ }

        if (WindowsAiUtilities.CanDeviceUseWinAI())
        {
            AiMenuItem.Visibility = Visibility.Visible;

            // Set dynamic header text for TranslateToSystemLanguageMenuItem
            string systemLanguage = LanguageUtilities.GetSystemLanguageForTranslation();
            TranslateToSystemLanguageMenuItem.Header = $"Translate to {systemLanguage}";
        }

        // Initialize selectedILanguage with the last used OCR language from settings
        // This ensures that when images are dropped or pasted, the correct language is used
        selectedILanguage = LanguageUtilities.GetOCRLanguage();
    }

    private void HideCalcPaneContextItem_Click(object sender, RoutedEventArgs e)
    {
        ShowCalcPaneMenuItem.IsChecked = false;
        DefaultSettings.CalcShowPane = false;
        SetCalcPaneVisibility();
    }

    private void ShowCalcErrorsContextItem_Click(object sender, RoutedEventArgs e)
    {
        ShowErrorsMenuItem.IsChecked = !ShowErrorsMenuItem.IsChecked;
        DefaultSettings.CalcShowErrors = ShowErrorsMenuItem.IsChecked;
        _ = EvaluateExpressions();
    }

    private void CopyAllContextItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CalcResultsTextControl.Text))
            return;

        try
        {
            System.Windows.Clipboard.SetDataObject(CalcResultsTextControl.Text, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy calc results to clipboard: {ex.Message}");
        }
    }

    private void CalcResultsTextControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        UpdateCalcAggregates();
    }

    private void UpdateCalcAggregates()
    {
        // Get the text to analyze - selected text if available, otherwise all results
        string textToAnalyze = !string.IsNullOrEmpty(CalcResultsTextControl.SelectedText)
            ? CalcResultsTextControl.SelectedText
            : CalcResultsTextControl.Text;

        // Extract numeric values from the text
        List<double>? numbers = calculationResult?.OutputNumbers;

        // Update menu items based on whether we have numbers
        if (numbers is null || numbers.Count == 0)
        {
            ShowSumContextItem.Header = "Sum: -";
            ShowAverageContextItem.Header = "Average: -";
            ShowMedianContextItem.Header = "Median: -";
            ShowCountContextItem.Header = "Count: 0";
            ShowMinContextItem.Header = "Min: -";
            ShowMaxContextItem.Header = "Max: -";
            ShowProductContextItem.Header = "Product: -";

            ShowSumContextItem.IsEnabled = false;
            ShowAverageContextItem.IsEnabled = false;
            ShowMedianContextItem.IsEnabled = false;
            ShowCountContextItem.IsEnabled = false;
            ShowMinContextItem.IsEnabled = false;
            ShowMaxContextItem.IsEnabled = false;
            ShowProductContextItem.IsEnabled = false;
        }
        else
        {
            double sum = numbers.Sum();
            double average = numbers.Average();
            double median = NumericUtilities.CalculateMedian(numbers);
            int count = numbers.Count;
            double min = numbers.Min();
            double max = numbers.Max();
            double product = numbers.Aggregate(1.0, (acc, val) => acc * val);

            ShowSumContextItem.Header = $"Sum: {NumericUtilities.FormatNumber(sum)}";
            ShowAverageContextItem.Header = $"Average: {NumericUtilities.FormatNumber(average)}";
            ShowMedianContextItem.Header = $"Median: {NumericUtilities.FormatNumber(median)}";
            ShowCountContextItem.Header = $"Count: {count}";
            ShowMinContextItem.Header = $"Min: {NumericUtilities.FormatNumber(min)}";
            ShowMaxContextItem.Header = $"Max: {NumericUtilities.FormatNumber(max)}";
            ShowProductContextItem.Header = $"Product: {NumericUtilities.FormatNumber(product)}";

            ShowSumContextItem.IsEnabled = true;
            ShowAverageContextItem.IsEnabled = true;
            ShowMedianContextItem.IsEnabled = true;
            ShowCountContextItem.IsEnabled = true;
            ShowMinContextItem.IsEnabled = true;
            ShowMaxContextItem.IsEnabled = true;
            ShowProductContextItem.IsEnabled = true;

            // Wire up click handlers to copy values and track selection
            ShowSumContextItem.Click -= SelectAggregate_Click;
            ShowSumContextItem.Click += SelectAggregate_Click;
            ShowSumContextItem.Tag = (AggregateType.Sum, sum);

            ShowAverageContextItem.Click -= SelectAggregate_Click;
            ShowAverageContextItem.Click += SelectAggregate_Click;
            ShowAverageContextItem.Tag = (AggregateType.Average, average);

            ShowMedianContextItem.Click -= SelectAggregate_Click;
            ShowMedianContextItem.Click += SelectAggregate_Click;
            ShowMedianContextItem.Tag = (AggregateType.Median, median);

            ShowCountContextItem.Click -= SelectAggregate_Click;
            ShowCountContextItem.Click += SelectAggregate_Click;
            ShowCountContextItem.Tag = (AggregateType.Count, (double)count);

            ShowMinContextItem.Click -= SelectAggregate_Click;
            ShowMinContextItem.Click += SelectAggregate_Click;
            ShowMinContextItem.Tag = (AggregateType.Min, min);

            ShowMaxContextItem.Click -= SelectAggregate_Click;
            ShowMaxContextItem.Click += SelectAggregate_Click;
            ShowMaxContextItem.Tag = (AggregateType.Max, max);

            ShowProductContextItem.Click -= SelectAggregate_Click;
            ShowProductContextItem.Click += SelectAggregate_Click;
            ShowProductContextItem.Tag = (AggregateType.Product, product);

            // Update checked states based on current selection
            ShowSumContextItem.IsChecked = _selectedAggregate == AggregateType.Sum;
            ShowAverageContextItem.IsChecked = _selectedAggregate == AggregateType.Average;
            ShowMedianContextItem.IsChecked = _selectedAggregate == AggregateType.Median;
            ShowCountContextItem.IsChecked = _selectedAggregate == AggregateType.Count;
            ShowMinContextItem.IsChecked = _selectedAggregate == AggregateType.Min;
            ShowMaxContextItem.IsChecked = _selectedAggregate == AggregateType.Max;
            ShowProductContextItem.IsChecked = _selectedAggregate == AggregateType.Product;
        }
    }

    private void SelectAggregate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not ValueTuple<AggregateType, double> tagData)
        {
            return;
        }

        try
        {
            (AggregateType aggregateType, double value) = tagData;

            // If clicking a checked item, uncheck it and clear selection
            if (menuItem.IsChecked && _selectedAggregate == aggregateType)
            {
                menuItem.IsChecked = false;
                _selectedAggregate = AggregateType.None;
                UpdateAggregateStatusDisplay();
                return;
            }

            // Uncheck all other aggregate menu items (both context menus)
            ShowSumContextItem.IsChecked = false;
            ShowAverageContextItem.IsChecked = false;
            ShowMedianContextItem.IsChecked = false;
            ShowCountContextItem.IsChecked = false;
            ShowMinContextItem.IsChecked = false;
            ShowMaxContextItem.IsChecked = false;
            ShowProductContextItem.IsChecked = false;

            AggregateSumContextItem.IsChecked = false;
            AggregateAverageContextItem.IsChecked = false;
            AggregateMedianContextItem.IsChecked = false;
            AggregateCountContextItem.IsChecked = false;
            AggregateMinContextItem.IsChecked = false;
            AggregateMaxContextItem.IsChecked = false;
            AggregateProductContextItem.IsChecked = false;

            // Check the clicked item
            menuItem.IsChecked = true;

            // Store the selected aggregate type
            _selectedAggregate = aggregateType;

            // Copy value to clipboard
            string valueToCopy = NumericUtilities.FormatNumber(value);
            System.Windows.Clipboard.SetDataObject(valueToCopy, true);

            // Update the status display
            UpdateAggregateStatusDisplay();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to process aggregate selection: {ex.Message}");
        }
    }

    private void UpdateAggregateStatusDisplay()
    {
        if (_selectedAggregate == AggregateType.None || calculationResult is null)
        {
            CalcAggregateStatusBorder.Visibility = Visibility.Collapsed;
            return;
        }

        // Get the text to analyze - all results
        string textToAnalyze = CalcResultsTextControl.Text;

        // Extract numeric values
        List<double> numbers = calculationResult.OutputNumbers;

        if (numbers.Count == 0)
        {
            CalcAggregateStatusBorder.Visibility = Visibility.Collapsed;
            return;
        }

        // Calculate the selected aggregate
        double value;
        string aggregateName;

        switch (_selectedAggregate)
        {
            case AggregateType.Sum:
                value = numbers.Sum();
                aggregateName = "Sum";
                break;
            case AggregateType.Average:
                value = numbers.Average();
                aggregateName = "Average";
                break;
            case AggregateType.Median:
                value = NumericUtilities.CalculateMedian(numbers);
                aggregateName = "Median";
                break;
            case AggregateType.Count:
                value = numbers.Count;
                aggregateName = "Count";
                break;
            case AggregateType.Min:
                value = numbers.Min();
                aggregateName = "Min";
                break;
            case AggregateType.Max:
                value = numbers.Max();
                aggregateName = "Max";
                break;
            case AggregateType.Product:
                value = numbers.Aggregate(1.0, (acc, val) => acc * val);
                aggregateName = "Product";
                break;
            default:
                CalcAggregateStatusBorder.Visibility = Visibility.Collapsed;
                return;
        }

        // Update the status text
        CalcAggregateStatusText.Text = $"{aggregateName}: {NumericUtilities.FormatNumber(value)}";
        CalcAggregateStatusBorder.Visibility = Visibility.Visible;
    }

    private void CalcAggregateStatusBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CalcAggregateStatusText.Text))
            return;

        try
        {
            // Extract just the numeric value from the text (e.g., "Sum: 123.45" -> "123.45")
            string fullText = CalcAggregateStatusText.Text;
            int colonIndex = fullText.IndexOf(':');
            if (colonIndex >= 0 && colonIndex < fullText.Length - 1)
            {
                string valueToCopy = fullText[(colonIndex + 1)..].Trim();
                System.Windows.Clipboard.SetDataObject(valueToCopy, true);
            }
            else
            {
                System.Windows.Clipboard.SetDataObject(fullText, true);
            }

            // Animate the copy action
            AnimateAggregateCopy();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy aggregate value: {ex.Message}");
        }

        e.Handled = true;
    }

    private void CalcAggregateStatusBorder_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        UpdateAggregateContextMenu();
    }

    private void UpdateAggregateContextMenu()
    {
        // Extract numeric values from the text
        List<double>? numbers = calculationResult?.OutputNumbers;

        // Update menu items based on whether we have numbers
        if (numbers is null || numbers.Count == 0)
        {
            AggregateSumContextItem.Header = "Sum: -";
            AggregateAverageContextItem.Header = "Average: -";
            AggregateMedianContextItem.Header = "Median: -";
            AggregateCountContextItem.Header = "Count: 0";
            AggregateMinContextItem.Header = "Min: -";
            AggregateMaxContextItem.Header = "Max: -";
            AggregateProductContextItem.Header = "Product: -";

            AggregateSumContextItem.IsEnabled = false;
            AggregateAverageContextItem.IsEnabled = false;
            AggregateMedianContextItem.IsEnabled = false;
            AggregateCountContextItem.IsEnabled = false;
            AggregateMinContextItem.IsEnabled = false;
            AggregateMaxContextItem.IsEnabled = false;
            AggregateProductContextItem.IsEnabled = false;
        }
        else
        {
            double sum = numbers.Sum();
            double average = numbers.Average();
            double median = NumericUtilities.CalculateMedian(numbers);
            int count = numbers.Count;
            double min = numbers.Min();
            double max = numbers.Max();
            double product = numbers.Aggregate(1.0, (acc, val) => acc * val);

            AggregateSumContextItem.Header = $"Sum: {NumericUtilities.FormatNumber(sum)}";
            AggregateAverageContextItem.Header = $"Average: {NumericUtilities.FormatNumber(average)}";
            AggregateMedianContextItem.Header = $"Median: {NumericUtilities.FormatNumber(median)}";
            AggregateCountContextItem.Header = $"Count: {count}";
            AggregateMinContextItem.Header = $"Min: {NumericUtilities.FormatNumber(min)}";
            AggregateMaxContextItem.Header = $"Max: {NumericUtilities.FormatNumber(max)}";
            AggregateProductContextItem.Header = $"Product: {NumericUtilities.FormatNumber(product)}";

            AggregateSumContextItem.IsEnabled = true;
            AggregateAverageContextItem.IsEnabled = true;
            AggregateMedianContextItem.IsEnabled = true;
            AggregateCountContextItem.IsEnabled = true;
            AggregateMinContextItem.IsEnabled = true;
            AggregateMaxContextItem.IsEnabled = true;
            AggregateProductContextItem.IsEnabled = true;

            // Wire up click handlers
            AggregateSumContextItem.Click -= SelectAggregate_Click;
            AggregateSumContextItem.Click += SelectAggregate_Click;
            AggregateSumContextItem.Tag = (AggregateType.Sum, sum);

            AggregateAverageContextItem.Click -= SelectAggregate_Click;
            AggregateAverageContextItem.Click += SelectAggregate_Click;
            AggregateAverageContextItem.Tag = (AggregateType.Average, average);

            AggregateMedianContextItem.Click -= SelectAggregate_Click;
            AggregateMedianContextItem.Click += SelectAggregate_Click;
            AggregateMedianContextItem.Tag = (AggregateType.Median, median);

            AggregateCountContextItem.Click -= SelectAggregate_Click;
            AggregateCountContextItem.Click += SelectAggregate_Click;
            AggregateCountContextItem.Tag = (AggregateType.Count, (double)count);

            AggregateMinContextItem.Click -= SelectAggregate_Click;
            AggregateMinContextItem.Click += SelectAggregate_Click;
            AggregateMinContextItem.Tag = (AggregateType.Min, min);

            AggregateMaxContextItem.Click -= SelectAggregate_Click;
            AggregateMaxContextItem.Click += SelectAggregate_Click;
            AggregateMaxContextItem.Tag = (AggregateType.Max, max);

            AggregateProductContextItem.Click -= SelectAggregate_Click;
            AggregateProductContextItem.Click += SelectAggregate_Click;
            AggregateProductContextItem.Tag = (AggregateType.Product, product);

            // Update checked states based on current selection
            AggregateSumContextItem.IsChecked = _selectedAggregate == AggregateType.Sum;
            AggregateAverageContextItem.IsChecked = _selectedAggregate == AggregateType.Average;
            AggregateMedianContextItem.IsChecked = _selectedAggregate == AggregateType.Median;
            AggregateCountContextItem.IsChecked = _selectedAggregate == AggregateType.Count;
            AggregateMinContextItem.IsChecked = _selectedAggregate == AggregateType.Min;
            AggregateMaxContextItem.IsChecked = _selectedAggregate == AggregateType.Max;
            AggregateProductContextItem.IsChecked = _selectedAggregate == AggregateType.Product;
        }
    }

    private void AnimateAggregateCopy()
    {
        // Flash the copy icon to indicate the copy action
        DoubleAnimation fadeInOutAnimation = new()
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(300),
            AutoReverse = true
        };

        CalcAggregateCopyIcon.BeginAnimation(OpacityProperty, fadeInOutAnimation);
    }

    private void CalcCopyAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CalcResultsTextControl.Text))
            return;

        try
        {
            System.Windows.Clipboard.SetDataObject(CalcResultsTextControl.Text, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy calc results to clipboard: {ex.Message}");
        }
    }

    private void CalcInfoButton_Click(object sender, RoutedEventArgs e)
    {
        CalcInfoPopup.IsOpen = !CalcInfoPopup.IsOpen;
    }

    private void EscapeKeyTimer_Tick(object? sender, EventArgs e)
    {
        EscapeKeyTimer.Stop();

        if (EscapeKeyTimerCount >= 3)
            Close();

        EscapeKeyTimerCount = 0;
    }

    private void CalcResultsTextControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Forward scrolling intent to the main text box so both stay aligned
        if (Keyboard.Modifiers == ModifierKeys.Control)
            return; // let zoom handler take it

        try
        {
            if (WindowUtilities.GetScrollViewer(PassedTextControl) is ScrollViewer mainSv)
            {
                // Roughly match WPF default: 3 lines per notch; use a small pixel offset
                double delta = -e.Delta; // positive means scroll down
                double lines = delta / 120.0 * 3.0;

                // Estimate line height from font size; 1em ~ FontSize pixels, add padding
                double lineHeight = Math.Max(12, PassedTextControl.FontSize * 1.35);
                mainSv.ScrollToVerticalOffset(mainSv.VerticalOffset + (lines * lineHeight));
                e.Handled = true;
            }
        }
        catch { /* no-op */ }
    }

    private void WindowMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        OpenLastAsGrabFrameMenuItem.IsEnabled = Singleton<HistoryService>.Instance.HasAnyHistoryWithImages();
    }

    private void WrapTextCHBX_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (WrapTextMenuItem.IsChecked)
            PassedTextControl.TextWrapping = TextWrapping.Wrap;
        else
            PassedTextControl.TextWrapping = TextWrapping.NoWrap;

        DefaultSettings.EditWindowIsWordWrapOn = WrapTextMenuItem.IsChecked;
    }

    private void CorrectGuid_Click(object sender, RoutedEventArgs e)
    {
        string workingString = GetSelectedTextOrAllText();

        workingString = workingString.CorrectCommonGuidErrors();

        if (PassedTextControl.SelectionLength == 0)
            PassedTextControl.Text = workingString;
        else
            PassedTextControl.SelectedText = workingString;
    }

    private async void SummarizeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string textToSummarize = GetSelectedTextOrAllText();

        SetToLoading("Summarizing...");

        try
        {
            string summarizedText = await WindowsAiUtilities.SummarizeParagraph(textToSummarize);

            if (PassedTextControl.SelectionLength == 0)
                PassedTextControl.Text = summarizedText;
            else
                PassedTextControl.SelectedText = summarizedText;
        }
        finally
        {
            SetToLoaded();
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
        string textToRewrite = GetSelectedTextOrAllText();

        SetToLoading("Rewriting...");
        try
        {
            string summarizedText = await WindowsAiUtilities.Rewrite(textToRewrite);

            if (PassedTextControl.SelectionLength == 0)
                PassedTextControl.Text = summarizedText;
            else
                PassedTextControl.SelectedText = summarizedText;
        }
        finally
        {
            SetToLoaded();
        }
    }

    private async void ConvertTableMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string textToTable = GetSelectedTextOrAllText();

        SetToLoading("Converting...");

        try
        {
            string summarizedText = await WindowsAiUtilities.TextToTable(textToTable);

            if (PassedTextControl.SelectionLength == 0)
                PassedTextControl.Text = summarizedText;
            else
                PassedTextControl.SelectedText = summarizedText;
        }
        finally
        {
            SetToLoaded();
        }
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

    private async Task PerformTranslationAsync(string targetLanguage)
    {
        string textToTranslate = GetSelectedTextOrAllText();

        SetToLoading($"Translating to {targetLanguage}...");

        try
        {
            string translatedText = await WindowsAiUtilities.TranslateText(textToTranslate, targetLanguage);

            if (PassedTextControl.SelectionLength == 0)
            {
                PassedTextControl.Text = translatedText;
            }
            else
            {
                PassedTextControl.SelectedText = translatedText;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Translation failed: {ex.Message}",
                "Translation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetToLoaded();
        }
    }

    private async void ExtractRegexMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string textDescription = GetSelectedTextOrAllText();

        if (string.IsNullOrWhiteSpace(textDescription))
        {
            System.Windows.MessageBox.Show("Please enter or select text to extract a regex pattern from.",
                "No Text", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetToLoading("Extracting RegEx pattern...");

        string regexPattern;
        try
        {
            regexPattern = await WindowsAiUtilities.ExtractRegex(textDescription);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Regex extraction exception: {ex.Message}");
            System.Windows.MessageBox.Show($"An error occurred while extracting regex: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            SetToLoaded();
            return;
        }

        SetToLoaded();

        if (string.IsNullOrWhiteSpace(regexPattern))
        {
            System.Windows.MessageBox.Show("Failed to extract a regex pattern. The AI service may not be available or could not generate a pattern.",
                "Extraction Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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
                System.Windows.MessageBox.Show("Failed to copy regex pattern to clipboard.",
                    "Copy Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SetToLoading(string message = "")
    {
        IsEnabled = false;

        if (!string.IsNullOrWhiteSpace(message))
            ProgressText.Text = message;

        LoadingStack.Visibility = Visibility.Visible;
    }

    private void SetToLoaded()
    {
        IsEnabled = true;
        LoadingStack.Visibility = Visibility.Collapsed;
    }

    private void ShowCalcPaneMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;

        DefaultSettings.CalcShowPane = menuItem.IsChecked;

        SetCalcPaneVisibility();
    }

    private void CalcToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCalcPaneMenuItem.IsChecked = !ShowCalcPaneMenuItem.IsChecked;
        DefaultSettings.CalcShowPane = ShowCalcPaneMenuItem.IsChecked;
        SetCalcPaneVisibility();
    }

    private void ToggleCalcPaneExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ShowCalcPaneMenuItem.IsChecked = !ShowCalcPaneMenuItem.IsChecked;
        DefaultSettings.CalcShowPane = ShowCalcPaneMenuItem.IsChecked;
        SetCalcPaneVisibility();
    }

    private void SetCalcPaneVisibility()
    {
        // Check if we're loading from history and should ignore default settings
        if (ShowCalcPaneMenuItem.Tag is bool fromHistory && fromHistory)
        {
            ShowCalcPaneMenuItem.Tag = null; // Clear the flag after first use
            // Use ShowCalcPaneMenuItem.IsChecked which was set from history
        }
        else
        {
            // Not from history, apply user's default setting
            ShowCalcPaneMenuItem.IsChecked = DefaultSettings.CalcShowPane;
        }

        if (ShowCalcPaneMenuItem.IsChecked)
        {
            CalcResultsTextControl.Visibility = Visibility.Visible;
            TextBoxSplitter.Visibility = Visibility.Visible;
            CalcPaneShadow.Visibility = Visibility.Visible;

            // Restore previous width if it was collapsed
            if (CalcColumn.Width.Value == 0)
                CalcColumn.Width = _lastCalcColumnWidth;

            // Disable text wrapping when calc pane is visible to maintain vertical alignment
            // Store the previous wrapping state to restore later
            if (PassedTextControl.TextWrapping != TextWrapping.NoWrap)
            {
                _previousTextWrapping = PassedTextControl.TextWrapping;
                PassedTextControl.TextWrapping = TextWrapping.NoWrap;
            }

            _debounceTimer?.Start();
        }
        else
        {
            CalcResultsTextControl.Visibility = Visibility.Collapsed;
            TextBoxSplitter.Visibility = Visibility.Collapsed;
            CalcPaneShadow.Visibility = Visibility.Collapsed;

            // Remember current width, then collapse column to remove pane area
            if (CalcColumn.Width.Value > 0)
                _lastCalcColumnWidth = CalcColumn.Width;
            CalcColumn.Width = new GridLength(0);

            // Restore previous text wrapping setting when calc pane is hidden
            if (_previousTextWrapping.HasValue)
            {
                PassedTextControl.TextWrapping = _previousTextWrapping.Value;
                _previousTextWrapping = null;
            }
        }
    }

    private void ShowErrorsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;

        DefaultSettings.CalcShowErrors = menuItem.IsChecked;
        _ = EvaluateExpressions();
    }

    private void TextBoxSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Toggle between equal split (star) and collapsed
        if (CalcColumn.Width.IsStar)
        {
            // If already star-sized, collapse the pane
            ShowCalcPaneMenuItem.IsChecked = false;
            DefaultSettings.CalcShowPane = false;
            SetCalcPaneVisibility();
        }
        else
        {
            // If collapsed or pixel-sized, set to equal split (1 star = 50% of available space)
            CalcColumn.Width = new GridLength(1, GridUnitType.Star);
            _lastCalcColumnWidth = new GridLength(1, GridUnitType.Star);
        }
    }

    private void RegexManagerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RegexManager regexManager = WindowUtilities.OpenOrActivateWindow<RegexManager>();
        regexManager.Show();
    }

    private void SaveCurrentPatternToRegexManager()
    {
        if (currentExtractedPattern is null)
            return;

        string pattern = currentExtractedPattern.GetPattern(currentPrecisionLevel);
        string sourceText = currentExtractedPattern.OriginalText;

        RegexManager regexManager = WindowUtilities.OpenOrActivateWindow<RegexManager>();
        regexManager.AddPatternFromText(pattern, sourceText, this);
        regexManager.Show();
    }

    private void LoadStoredPatternToSelection(StoredRegex storedPattern)
    {
        if (storedPattern is null)
            return;

        // Open Find and Replace window with this pattern and execute search
        FindAndReplaceWindow findWindow = WindowUtilities.OpenOrActivateWindow<FindAndReplaceWindow>();
        findWindow.FindTextBox.Text = storedPattern.Pattern;
        findWindow.UsePaternCheckBox.IsChecked = true;
        findWindow.TextEditWindow = this;
        findWindow.StringFromWindow = PassedTextControl.Text;
        findWindow.SearchForText();
        findWindow.Show();
    }

    private void ShowRegexExplanation()
    {
        if (currentExtractedPattern is null)
            return;

        string pattern = currentExtractedPattern.GetPattern(currentPrecisionLevel);
        string explanation = pattern.ExplainRegexPattern();

        Wpf.Ui.Controls.MessageBox messageBox = new()
        {
            Title = "Regex Pattern Explanation",
            Content = explanation,
            CloseButtonText = "Close"
        };
        _ = messageBox.ShowDialogAsync();
    }

    private void ExplainPatternMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string? pattern = currentExtractedPattern?.GetPattern(currentPrecisionLevel);

        if (string.IsNullOrEmpty(pattern))
            return;

        // Clear previous content
        CharDetailsPopupContent.Children.Clear();

        // Create explanation text
        string explanation = pattern.ExplainRegexPattern();

        System.Windows.Controls.TextBox explanationTextBox = new()
        {
            Text = explanation,
            FontSize = 12,
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            TextWrapping = TextWrapping.Wrap,
            Cursor = System.Windows.Input.Cursors.Arrow,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 400,
            Padding = new Thickness(8)
        };

        CharDetailsPopupContent.Children.Add(explanationTextBox);
        CharDetailsPopup.IsOpen = true;
    }

    [GeneratedRegex(@"(\r\n|\n|\r)")]
    private static partial Regex NewlineReturns();

    #endregion Methods

    private void SavePatternMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string? pattern = currentExtractedPattern?.GetPattern(currentPrecisionLevel);

        if (string.IsNullOrEmpty(pattern))
            return;

        // open the RegexManager and save this pattern
        RegexManager manager = WindowUtilities.OpenOrActivateWindow<RegexManager>();
        manager.AddPatternFromText(pattern, GetSelectedTextOrAllText(), this);
        manager.Show();

    }

    private void PatternContextOpening(object sender, ContextMenuEventArgs e)
    {
        // sender should be a button if not return
        if (sender is not System.Windows.Controls.Button button)
            return;

        // get the context menu
        if (button.ContextMenu is null)
            return;

        ContextMenu contextMenu = button.ContextMenu;

        // Clear existing dynamic items (keep original static items)
        // Find if "Use Pattern" menu item already exists, remove it to rebuild
        MenuItem? existingUsePatternItem = null;
        foreach (object? item in contextMenu.Items)
        {
            if (item is MenuItem mi && mi.Header?.ToString() == "Use Pattern")
            {
                existingUsePatternItem = mi;
                break;
            }
        }

        if (existingUsePatternItem is not null)
            contextMenu.Items.Remove(existingUsePatternItem);

        // make a context menu item for "use this pattern"
        MenuItem usePatternMenuItem = new()
        {
            Header = "Use Pattern"
        };

        // add all patterns from regex manager as menu items as children to the new "use this pattern" item
        List<StoredRegex> storedPatterns = LoadRegexPatterns();

        if (storedPatterns.Count == 0)
        {
            MenuItem noPatternItem = new()
            {
                Header = "No saved patterns",
                IsEnabled = false
            };
            usePatternMenuItem.Items.Add(noPatternItem);
        }
        else
        {
            foreach (StoredRegex storedPattern in storedPatterns)
            {
                MenuItem patternItem = new()
                {
                    Header = storedPattern.Name,
                    ToolTip = storedPattern.Pattern,
                    Tag = storedPattern
                };

                // wire up click event to override the currentExtractedPattern
                patternItem.Click += (s, args) =>
                {
                    if (s is MenuItem clickedItem && clickedItem.Tag is StoredRegex selectedPattern)
                    {
                        // Create a new ExtractedPattern from the stored pattern
                        // Use the pattern's description or name as the source text
                        string sourceText = string.IsNullOrWhiteSpace(selectedPattern.Description)
                            ? selectedPattern.Name
                            : selectedPattern.Description;

                        // Override the current extracted pattern with the selected stored pattern
                        currentExtractedPattern = new ExtractedPattern(sourceText, ignoreCase: true);
                        currentPrecisionLevel = ExtractedPattern.DefaultPrecisionLevel;

                        // Update the UI to reflect the new pattern
                        UpdateSelectionSpecificUI();

                        // Optionally open Find and Replace with this pattern
                        LoadStoredPatternToSelection(selectedPattern);
                    }
                };

                usePatternMenuItem.Items.Add(patternItem);
            }
        }

        // Add separator before the "Use Pattern" item
        contextMenu.Items.Add(new Separator());

        // Add the "Use Pattern" menu item to the context menu
        contextMenu.Items.Add(usePatternMenuItem);
    }

    private List<StoredRegex> LoadRegexPatterns()
    {
        List<StoredRegex> returnRegexes = [];

        // Load from settings
        string regexListJson = DefaultSettings.RegexList;

        if (!string.IsNullOrWhiteSpace(regexListJson))
        {
            try
            {
                StoredRegex[]? loadedPatterns = JsonSerializer.Deserialize<StoredRegex[]>(regexListJson);
                if (loadedPatterns is not null)
                {
                    foreach (StoredRegex pattern in loadedPatterns)
                        returnRegexes.Add(pattern);
                }
            }
            catch (JsonException)
            {
                // If deserialization fails, start fresh
            }
        }

        // Add default patterns if list is empty
        if (returnRegexes.Count == 0)
        {
            foreach (StoredRegex defaultPattern in StoredRegex.GetDefaultPatterns())
                returnRegexes.Add(defaultPattern);
        }

        return returnRegexes;
    }
}
