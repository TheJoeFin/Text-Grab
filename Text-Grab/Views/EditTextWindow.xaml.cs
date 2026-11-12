using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
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
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using Text_Grab.Controls;
using Text_Grab.Core.Editing;
using Text_Grab.Editing;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Pages;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Text_Grab;

/// <summary>
/// Interaction logic for ManipulateTextWindow.xaml
/// </summary>

public partial class EditTextWindow : Wpf.Ui.Controls.FluentWindow
{
    private const string EditTextWindowTitle = "Edit Text";
    private const double SpreadsheetDefaultColumnWidth = 120;
    private const double HorizontalWheelScrollStep = 48;
    private const int WmMouseHWheel = 0x020E;
    private const string SaveDocumentFilter = "Spreadsheet documents (*.csv;*.tsv;*.tab)|*.csv;*.tsv;*.tab|Markdown documents (*.md;*.markdown)|*.md;*.markdown|Text documents (*.txt)|*.txt|All files (*.*)|*.*";
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
    public static RoutedCommand TransposeTableCmd = new();
    public static RoutedCommand UnstackCmd = new();
    public static RoutedCommand UnstackGroupCmd = new();
    public static RoutedCommand WebSearchCmd = new();
    public static RoutedCommand DefaultWebSearchCmd = new();
    public bool LaunchedFromNotification = false;

    /// <summary>
    /// Stable per-instance ID so a "transcription complete" toast can re-activate the exact window
    /// that received the transcript, instead of opening a fresh one with (possibly truncated) text.
    /// </summary>
    internal readonly Guid WindowId = Guid.NewGuid();

    /// <summary>Live audio transcriber, created lazily when the bottom-bar toggle is turned on.</summary>
    private LiveAudioTranscriber? _liveTranscriber;
    private readonly SemaphoreSlim _liveTranscriptionGate = new(1, 1);
    private int _liveTranscriptionRequest;
    private bool _isStoppingLiveTranscriptionForClose;
    private bool _isClosed;

    /// <summary>Which source live transcription captures from (microphone or system loopback).</summary>
    private LiveCaptureSource _liveCaptureSource = LiveCaptureSource.Microphone;

    /// <summary>Cancels an in-progress audio-file transcription; non-null only while one is running.</summary>
    private CancellationTokenSource? _transcriptionCts;
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
    private EditTextTableDocument? tableDocument;
    private readonly SpreadsheetUndoHistory spreadsheetUndoHistory = new();
    private readonly DataTable spreadsheetTable = new();
    private readonly List<DataGridColumn> trackedSpreadsheetColumns = [];
    private List<(int RowIndex, int ColumnIndex)> selectedSpreadsheetCellCoordinates = [];
    private System.Windows.Controls.TextBox? activeSpreadsheetCellEditor;
    private string lastSpreadsheetCellEditorSelectedText = string.Empty;
    private EtwEditorMode editorMode = EtwEditorMode.Text;
    private SpellCheckMode spellCheckMode = SpellCheckMode.Auto;
    private bool isSyncingTextFromSpreadsheet = false;
    private bool isSyncingTextFromMarkdown = false;
    private bool isApplyingSpreadsheetLayout = false;
    private bool isApplyingMarkdownDocument = false;
    private MarkdownFlowDocumentUtilities.MarkdownOffsetMap markdownOffsetMap = new([], []);
    private string? markdownOffsetMapSourceText;
    private bool? markdownAppliedLightTheme;
    private bool isLoadingOpenedFile = false;
    private bool hasPendingFileEdits = false;
    private bool isShowingPendingFileClosePrompt = false;
    private bool allowCloseAfterPendingFilePrompt = false;
    private bool isRestoringSpreadsheetUndoState = false;
    // Cached clipboard state to avoid slow COM calls on every CanExecute query
    private bool _cachedClipboardHasOcrContent = true;
    private int? spreadsheetContextRowIndex;
    private int? spreadsheetContextColumnIndex;
    private SpreadsheetUndoState? pendingSpreadsheetUndoState;
    private string savedFileText = string.Empty;
    private HwndSource? windowSource;
    private EditorSession? editorSession;

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
            Session.Text = possiblyEncodedString;

        LaunchedFromNotification = true;
    }

    public EditTextWindow(HistoryInfo historyInfo)
    {
        InitializeComponent();
        App.SetTheme();

        Session.Text = historyInfo.TextContent;
        editorMode = historyInfo.EditorMode;
        tableDocument = EditTextTableDocument.TryDeserialize(historyInfo.EditTextTableDocumentJson);

        historyId = historyInfo.ID;

        if (historyInfo.PositionRect != System.Drawing.RectangleF.Empty)
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

    /// <summary>The document in this window and the editor showing it.</summary>
    /// <remarks>
    /// Created on first use rather than in the constructors: handlers that run inside
    /// <c>InitializeComponent</c> (<c>Window_Initialized</c> re-themes the Markdown document, which
    /// raises its TextChanged) already need it, and by then the editors it wraps exist.
    /// </remarks>
    public IEditorSession Session => editorSession ??= CreateEditorSession();

    #endregion Properties

    #region Methods

    private EditorSession CreateEditorSession()
    {
        TextBoxSurface textSurface = new(PassedTextControl);
        EditorSession session = new(
            textSurface,
            new MarkdownSurface(range => SelectInMarkdownEditor(range.Start, range.Length)),
            new SpreadsheetSurface(textSurface),
            () => editorMode);
        EditorModeChanged += (_, _) => session.RaiseModeChanged();
        return session;
    }

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
            {nameof(SplitAfterSelectionCmd), SplitAfterSelectionCmd},
            {nameof(OcrPasteCommand), OcrPasteCommand},
            {nameof(MakeQrCodeCmd), MakeQrCodeCmd},
            {nameof(TransposeTableCmd), TransposeTableCmd},
            {nameof(WebSearchCmd), WebSearchCmd},
            {nameof(DefaultWebSearchCmd), DefaultWebSearchCmd},
        };
    }

    public void AddCharsToEditTextWindow(string stringToAdd, SpotInLine spotInLine)
    {
        Session.Text = Session.Text.AddCharsToEachLine(stringToAdd, spotInLine);
    }

    public void JoinLinesInEditTextWindow(string joiningText, bool trimLineBeforeJoining, string textAtBeginning = "", string textAtEnd = "")
    {
        ApplySelectedTextOrAllTextTransform(text => text.JoinLines(joiningText, trimLineBeforeJoining, textAtBeginning, textAtEnd));
    }

    public void AddThisText(string textToAdd)
    {
        Session.AppendText(textToAdd);
    }

    public System.Windows.Controls.TextBox GetMainTextBox()
    {
        return PassedTextControl;
    }

    public event EventHandler? EditorModeChanged;

    public void RemoveCharsFromEditTextWindow(int numberOfChars, SpotInLine spotInLine)
    {
        Session.Text = Session.Text.RemoveFromEachLine(numberOfChars, spotInLine);
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

        if (selectedILanguage is not GlobalLang)
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

    private void TransposeTableCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = editorMode == EtwEditorMode.Spreadsheet;
    }

    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typedParent)
                return typedParent;

            current = current switch
            {
                TextElement textElement => textElement.Parent,
                _ => VisualTreeHelper.GetParent(current)
            };
        }

        return null;
    }

    /// <param name="deferMarkdownLoad">
    /// When switching to Markdown, skip building the document from the current text. The caller is about
    /// to replace the text and refresh the document itself, so building it here would be thrown away.
    /// </param>
    private void SetEditorMode(EtwEditorMode mode, bool deferMarkdownLoad = false)
    {
        bool isModeAlreadyApplied = mode switch
        {
            EtwEditorMode.Spreadsheet => SpreadsheetDataGrid.Visibility == Visibility.Visible
                && PassedTextControl.Visibility != Visibility.Visible
                && MarkdownEditorControl.Visibility != Visibility.Visible,
            EtwEditorMode.Markdown => MarkdownEditorControl.Visibility == Visibility.Visible
                && PassedTextControl.Visibility != Visibility.Visible
                && SpreadsheetDataGrid.Visibility != Visibility.Visible,
            _ => PassedTextControl.Visibility == Visibility.Visible
                && SpreadsheetDataGrid.Visibility != Visibility.Visible
                && MarkdownEditorControl.Visibility != Visibility.Visible
        };

        if (editorMode == mode && isModeAlreadyApplied)
        {
            if (mode == EtwEditorMode.Markdown && !deferMarkdownLoad)
                ApplyMarkdownTheme();

            UpdateSpreadsheetModeUi();
            UpdateLineAndColumnText();
            return;
        }

        if (mode == EtwEditorMode.Spreadsheet)
        {
            if (editorMode == EtwEditorMode.Markdown && MarkdownEditorControl.Visibility == Visibility.Visible)
                SyncMarkdownTextFromDocument();

            EnsureSpreadsheetDocumentFromText();
            RebuildSpreadsheetTable();
            PassedTextControl.Visibility = Visibility.Collapsed;
            MarkdownEditorControl.Visibility = Visibility.Collapsed;
            SpreadsheetDataGrid.Visibility = Visibility.Visible;
            editorMode = EtwEditorMode.Spreadsheet;
            SpreadsheetDataGrid.Focus();
        }
        else if (mode == EtwEditorMode.Markdown)
        {
            if (editorMode == EtwEditorMode.Spreadsheet && SpreadsheetDataGrid.Visibility == Visibility.Visible)
                SyncSpreadsheetDocumentFromTable();

            if (!deferMarkdownLoad)
                LoadMarkdownDocumentFromText(Session.Text);

            SpreadsheetDataGrid.Visibility = Visibility.Collapsed;
            PassedTextControl.Visibility = Visibility.Collapsed;
            MarkdownEditorControl.Visibility = Visibility.Visible;
            editorMode = EtwEditorMode.Markdown;
            MarkdownEditorControl.Focus();
        }
        else
        {
            if (editorMode == EtwEditorMode.Spreadsheet && SpreadsheetDataGrid.Visibility == Visibility.Visible)
                SyncSpreadsheetDocumentFromTable();
            else if (editorMode == EtwEditorMode.Markdown && MarkdownEditorControl.Visibility == Visibility.Visible)
                SyncMarkdownTextFromDocument();

            SpreadsheetDataGrid.Visibility = Visibility.Collapsed;
            MarkdownEditorControl.Visibility = Visibility.Collapsed;
            PassedTextControl.Visibility = Visibility.Visible;
            editorMode = EtwEditorMode.Text;
            PassedTextControl.Focus();
        }

        UpdateSpreadsheetModeUi();
        UpdateLineAndColumnText();
        EditorModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EditorModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender == RawTextModeMenuItem)
            SetEditorMode(EtwEditorMode.Text);
        else if (sender == SpreadsheetModeMenuItem)
            SetEditorMode(EtwEditorMode.Spreadsheet);
        else if (sender == MarkdownModeMenuItem)
            SetEditorMode(EtwEditorMode.Markdown);
    }

    private void ToggleMenuItem(MenuItem menuItem, RoutedEventHandler handler)
    {
        menuItem.IsChecked = !menuItem.IsChecked;
        handler(menuItem, new RoutedEventArgs());
    }

    private void EnterRawTextMode_Click(object sender, RoutedEventArgs e) => SetEditorMode(EtwEditorMode.Text);

    private void ToggleAlwaysOnTop_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(AlwaysOnTop, AlwaysOnTop_Checked);

    private void ToggleHideBottomBar_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(HideBottomBarMenuItem, HideBottomBarMenuItem_Click);

    private void ToggleLaunchFullscreenOnLoad_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(LaunchFullscreenOnLoad, LaunchFullscreenOnLoad_Click);

    private void ToggleRestorePosition_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(RestorePositionMenuItem, RestorePositionMenuItem_Checked);

    private void ToggleMargins_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(MarginsMenuItem, MarginsMenuItem_Checked);

    private void ToggleWrapText_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(WrapTextMenuItem, WrapTextCHBX_Checked);

    private void ToggleShowMathErrors_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(ShowErrorsMenuItem, ShowErrorsMenuItem_Click);

    private bool TrySetClipboardText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetDataObject(text, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T matchingParent)
                return matchingParent;

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    internal HistoryInfo AsHistoryItem()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            SyncSpreadsheetDocumentFromTable();
        else if (editorMode == EtwEditorMode.Markdown)
            SyncMarkdownTextFromDocument();

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
            TextContent = Session.Text,
            SourceMode = TextGrabMode.EditText,
            CalcPaneWidth = calcPaneWidth,
            HasCalcPaneOpen = ShowCalcPaneMenuItem.IsChecked is true,
            EditorMode = editorMode,
            EditTextTableDocumentJson = tableDocument?.SerializeToJson()
        };

        if (string.IsNullOrWhiteSpace(historyInfo.ID))
            historyInfo.ID = Guid.NewGuid().ToString();

        return historyInfo;
    }

    internal void LimitNumberOfCharsPerLine(int numberOfChars, SpotInLine spotInLine)
    {
        Session.Text = Session.Text.LimitCharactersPerLine(numberOfChars, spotInLine);
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<FirstRunWindow>();
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

    private void AddCopiedTextToTextBox(string textToAdd)
    {
        if (AppUtilities.TextGrabSettings.EtwNormalizeLineEndingsOnPaste)
            textToAdd = NormalizeLineEndings(textToAdd);

        Session.ReplaceSelection(textToAdd);
        int currentSelectionIndex = Session.Selection.Start;
        int currentSelectionLength = Session.Selection.Length;

        Session.Selection = new TextSpan(currentSelectionIndex + currentSelectionLength, 0);
    }

    private void AddRemoveAtMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddOrRemoveWindow addRemoveWindow = new()
        {
            Owner = this,
            SelectedTextFromEditTextWindow = Session.SelectedText
        };
        addRemoveWindow.ShowDialog();
    }

    private void JoinLinesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        JoinLinesWindow joinLinesWindow = new()
        {
            Owner = this
        };
        joinLinesWindow.ShowDialog();
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
        e.CanExecute = ExternalLinkUtilities.TryCreate(GetPossibleUrlText(), out _);
    }

    private string GetPossibleUrlText()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            return GetSpreadsheetSelectedText().Trim();

        string possibleURL = Session.SelectedText;

        if (string.IsNullOrEmpty(possibleURL))
            possibleURL = Session.Text.GetWordAtCursorPosition(Session.Selection.Start);

        return possibleURL;
    }

    private void CanOcrPasteExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        // Use cached value to avoid slow COM clipboard access on every CanExecute query.
        // The cache is updated by Clipboard_ContentChanged and on window load.
        e.CanExecute = _cachedClipboardHasOcrContent;
    }

    private void UpdateCachedClipboardOcrState()
    {
        try
        {
            DataPackageView view = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            _cachedClipboardHasOcrContent =
                view.Contains(StandardDataFormats.Text)
                || view.Contains(StandardDataFormats.Bitmap)
                || view.Contains(StandardDataFormats.StorageItems);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"error updating clipboard OCR state: {ex.Message}");
            _cachedClipboardHasOcrContent = true; // optimistic fallback
        }
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
        Session.ReplaceSelection(" ");
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
        // Always keep OCR paste cache fresh regardless of clipboard watcher state.
        UpdateCachedClipboardOcrState();

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
        string clipboardText = Session.Text;
        try { System.Windows.Clipboard.SetDataObject(clipboardText, true); } catch { }
        this.Close();
    }

    private async void CopyClosePasteBTN_Click(object sender, RoutedEventArgs e)
    {
        string clipboardText = Session.Text;
        try { System.Windows.Clipboard.SetDataObject(clipboardText, true); } catch { }
        this.Close();
        await WindowUtilities.TryInsertString(clipboardText);
    }

    private void DeleteAllSelectionExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selectionToDelete = Session.SelectedText;

        Session.Text = Session.Text.RemoveAllInstancesOf(selectionToDelete);
    }

    private void DeleteAllSelectionPatternExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selectionToDelete = Session.SelectedText;
        string Pattern = selectionToDelete.ExtractSimplePattern();
        MatchCollection Matches = Regex.Matches(Session.Text, Pattern, RegexOptions.Multiline);
        StringBuilder sb = new(Session.Text);
        for (int i = Matches.Count - 1; i >= 0; i--)
        {
            Match match = Matches[i];

            sb.Remove(match.Index, match.Length);
        }

        Session.Text = sb.ToString();
    }

    private void DeleteSelectedTextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Session.ReplaceSelection(String.Empty);
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

    public string GetSelectedTextOrAllText() => Session.GetSelectedTextOrAllText();

    internal IEnumerable<string> GetSelectedOrAllTextSegmentsForPreview()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            return GetSelectedOrPopulatedSpreadsheetCellTexts();

        return [GetSelectedTextOrAllText()];
    }

    public bool IsSpreadsheetMode => editorMode == EtwEditorMode.Spreadsheet;

    private IEnumerable<string> GetSelectedOrAllTextSegmentsForEdit()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            return GetSelectedOrPopulatedSpreadsheetCellTexts();

        return [GetSelectedTextOrAllText()];
    }

    private void ReplaceSelectedTextOrAllText(string updatedText) => Session.ReplaceSelectedTextOrAllText(updatedText);

    private void ApplySelectedTextOrAllTextTransform(Func<string, string> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (TryApplySpreadsheetTextTransform(transform))
            return;

        string updatedText = transform(GetSelectedTextOrAllText());
        ReplaceSelectedTextOrAllText(updatedText);
    }

    private async Task ApplySelectedTextOrAllTextTransformAsync(Func<string, Task<string>> transformAsync)
    {
        ArgumentNullException.ThrowIfNull(transformAsync);

        if (await TryApplySpreadsheetTextTransformAsync(transformAsync))
            return;

        string updatedText = await transformAsync(GetSelectedTextOrAllText());
        ReplaceSelectedTextOrAllText(updatedText);
    }

    private void GrabFrameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CheckForGrabFrameOrLaunch();
    }

    private async void CaptureFromCameraMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string? capturedImagePath = await CameraCaptureUtilities.CaptureImageFromCameraAsync(this);

        if (capturedImagePath is null)
            return;

        await OpenPath(capturedImagePath);
    }

    private void ManageGrabTemplates_Click(object sender, RoutedEventArgs e)
    {
        PostGrabActionEditor editor = new();
        editor.Show();
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

    private IntPtr EditTextWindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmMouseHWheel || Keyboard.Modifiers == ModifierKeys.Control)
            return IntPtr.Zero;

        ScrollViewer? scrollViewer = GetHorizontalPanTargetScrollViewer();
        if (scrollViewer is null || scrollViewer.ScrollableWidth <= 0)
            return IntPtr.Zero;

        short delta = unchecked((short)((wParam.ToInt64() >> 16) & 0xFFFF));
        double deltaSteps = delta / 120.0;
        if (NumericUtilities.AreClose(deltaSteps, 0))
            return IntPtr.Zero;

        double targetOffset = scrollViewer.HorizontalOffset + (deltaSteps * HorizontalWheelScrollStep);
        scrollViewer.ScrollToHorizontalOffset(Math.Clamp(targetOffset, 0, scrollViewer.ScrollableWidth));
        handled = true;
        return IntPtr.Zero;
    }

    private ScrollViewer? GetHorizontalPanTargetScrollViewer()
    {
        if (editorMode == EtwEditorMode.Spreadsheet && SpreadsheetDataGrid.Visibility == Visibility.Visible)
            return WindowUtilities.GetScrollViewer(SpreadsheetDataGrid);

        if (editorMode == EtwEditorMode.Markdown && MarkdownEditorControl.Visibility == Visibility.Visible)
            return WindowUtilities.GetScrollViewer(MarkdownEditorControl);

        if (CalcResultsTextControl.Visibility == Visibility.Visible && CalcResultsTextControl.IsMouseOver)
            return WindowUtilities.GetScrollViewer(CalcResultsTextControl);

        return WindowUtilities.GetScrollViewer(PassedTextControl);
    }

    // Keep calc pane scroll in sync with main text box
    private void PassedTextControl_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        try
        {
            if (CalcResultsTextControl.Visibility != Visibility.Visible)
                return;

            // Obtain internal ScrollViewers for both text boxes
            if (WindowUtilities.GetScrollViewer(PassedTextControl) is ScrollViewer mainSv
                && WindowUtilities.GetScrollViewer(CalcResultsTextControl) is ScrollViewer calcSv
                && !NumericUtilities.AreClose(calcSv.VerticalOffset, mainSv.VerticalOffset))
            {
                // Mirror vertical offset only (horizontal can differ due to content widths)
                calcSv.ScrollToVerticalOffset(mainSv.VerticalOffset);
            }
        }
        catch { /* no-op */ }
    }

    private void InsertSelectionOnEveryLine(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string[] splitString = Session.Text.Split([Environment.NewLine], StringSplitOptions.None);
        string selectionText = Session.SelectedText;
        int initialSelectionStart = Session.Selection.Start;
        int selectionPositionInLine = Session.Selection.Start;
        for (int i = initialSelectionStart; i >= 0; i--)
        {
            if (Session.Text[i] is '\n'
                or '\r')
            {
                selectionPositionInLine = initialSelectionStart - i - 1;
                break;
            }
        }

        int selectionLength = Session.Selection.Length;

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

        Session.Text = sb.ToString();
    }

    private void InsertSelectionOnEveryLineCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        // Operates on absolute character positions within the flat document text, which
        // has no meaningful equivalent in Spreadsheet mode, so keep it disabled there.
        if (editorMode == EtwEditorMode.Spreadsheet
            || string.IsNullOrEmpty(Session.SelectedText)
            || Session.SelectedText.Contains(Environment.NewLine)
            || Session.SelectedText.Contains('\r')
            || Session.SelectedText.Contains('\n'))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void IsolateSelectionCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        // Isolate/Delete-All-Instances act on the flat document text, which has no
        // meaningful per-cell equivalent in Spreadsheet mode, so keep them disabled there.
        e.CanExecute = editorMode != EtwEditorMode.Spreadsheet
            && !string.IsNullOrEmpty(Session.SelectedText);
    }

    private void CanSearchSelectionExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !string.IsNullOrEmpty(GetSpreadsheetAwareSelectedText());
    }

    private void IsolateSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(Session.SelectedText))
            Session.Text = Session.SelectedText;
    }

    private async void GoogleSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = Session.SelectedText;
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format($"https://www.google.com/search?q={searchStringUrlSafe}")));
    }

    private async void BingSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = Session.SelectedText;
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format($"https://www.bing.com/search?q={searchStringUrlSafe}")));
    }

    private async void DuckDuckGoSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = Session.SelectedText;
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format($"https://duckduckgo.com/?va=d&t=he&q={searchStringUrlSafe}&ia=web")));
    }

    private async void GitHubSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = Session.SelectedText;
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format($"https://github.com/search?q={searchStringUrlSafe}")));
    }

    private async void WebSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = GetSpreadsheetAwareSelectedText();
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);

        if (e.Parameter is not WebSearchUrlModel webSearcher)
            return;

        Uri searchUri = new($"{webSearcher.Url}{searchStringUrlSafe}");
        _ = await Windows.System.Launcher.LaunchUriAsync(searchUri);
    }

    private async void DefaultWebSearchExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string possibleSearch = GetSpreadsheetAwareSelectedText();
        string searchStringUrlSafe = WebUtility.UrlEncode(possibleSearch);

        WebSearchUrlModel searcher = Singleton<WebSearchUrlCatalog>.Instance.DefaultSearcher;

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

    private void LaunchFindAndReplace()
    {
        FindAndReplaceWindow findAndReplaceWindow = WindowUtilities.OpenOrActivateWindow<FindAndReplaceWindow>();

        findAndReplaceWindow.StringFromWindow = Session.Text;
        findAndReplaceWindow.TextEditWindow = this;
        findAndReplaceWindow.Show();

        string selectedText = IsSpreadsheetMode ? GetSpreadsheetSelectedText() : Session.SelectedText;

        if (selectedText.Length > 0)
        {
            findAndReplaceWindow.SetFindText(selectedText.Trim());
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

    private async void LaunchUriExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        await ExternalLinkLauncher.OpenAsync(GetPossibleUrlText());
    }

    private void ClearGrabTemplateMenuItems()
    {
        foreach (object item in GrabTemplateMenuItem.Items)
        {
            if (item is MenuItem oldItem)
                oldItem.Click -= GrabTemplateMenuItem_Click;
        }
        GrabTemplateMenuItem.Items.Clear();
        GrabTemplateMenuItem.Items.Add(new MenuItem { Header = "Loading…", IsEnabled = false });
    }

    private void ClearDynamicMenuItems()
    {
        ClearRecentTextMenuItems();
        ClearLanguageMenuItems();
        ClearGrabTemplateMenuItems();
        Singleton<HistoryService>.Instance.ClearRecentGrabsMenuItems(OpenRecentGrabsMenuItem);
    }

    private void MakeQrCodeCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !string.IsNullOrWhiteSpace(GetQrCodeSourceText());
    }

    private void MakeQrCodeExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string text = GetQrCodeSourceText();

        if (string.IsNullOrWhiteSpace(text))
            return;

        QrCodeWindow window = new(text);
        window.CenterOverThisWindow(this);
        window.Show();
    }

    private string GetQrCodeSourceText()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            return GetSpreadsheetSelectedText();

        return GetSelectedTextOrAllText();
    }

    private void AddedLineAboveCommand(object sender, ExecutedRoutedEventArgs e)
    {
        int replaceCaret = Session.Selection.Start + Environment.NewLine.Length;
        int selectionLength = Session.Selection.Length;

        SelectLine();
        string lineText = Session.SelectedText;
        Session.ReplaceSelection($"{Environment.NewLine}{lineText}");
        Session.Selection = new TextSpan(replaceCaret, selectionLength);
    }

    private void DuplicateSelectedLine(object sender, ExecutedRoutedEventArgs e)
    {
        int replaceCaret = Session.Selection.Start;
        int selectionLength = Session.Selection.Length;
        SelectLine();
        string lineText = Session.SelectedText;
        bool lineEndsInNewLine = lineText.EndsWithNewline();
        Session.ReplaceSelection($"{lineText}{(lineEndsInNewLine ? "" : Environment.NewLine)}{lineText}");
        int length = lineText.Length;
        if (!lineEndsInNewLine)
            length += Environment.NewLine.Length;

        Session.Selection = new TextSpan(replaceCaret + length, selectionLength);
    }

    private void MarginsMenuItem_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem marginsMenuItem)
            return;

        DefaultSettings.EtwUseMargins = marginsMenuItem.IsChecked;
        SetMargins(MarginsMenuItem.IsChecked);
    }

    private void MoveLineDown(object? sender, ExecutedRoutedEventArgs? e)
    {
        SelectLine(sender, e);

        string lineText = Session.SelectedText;
        Session.ReplaceSelection("");
        string textBoxText = Session.Text;
        int selectionIndex = Session.Selection.Start;
        int indexOfNextNewline = textBoxText.Length;

        if (!Session.Text.EndsWith(Environment.NewLine))
        {
            Session.Text += Environment.NewLine;
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

        Session.ReplaceRange(new TextSpan(indexOfNextNewline, 0), lineText);
    }

    private void MoveLineDownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveLineDown(sender, null);
    }

    private void MoveLineUp(object? sender, ExecutedRoutedEventArgs? e)
    {
        SelectLine(sender, e);
        string lineText = Session.SelectedText;
        Session.ReplaceSelection("");
        string textBoxText = Session.Text;
        int selectionIndex = Session.Selection.Start;
        int indexOfPreviousNewline = 0;

        IEnumerable<int> indicesOfNewLine = textBoxText.AllIndexesOf(Environment.NewLine);

        foreach (int newLineIndex in indicesOfNewLine)
        {
            int newLineEnd = newLineIndex + Environment.NewLine.Length;
            if (newLineEnd < selectionIndex)
                indexOfPreviousNewline = newLineEnd;
        }

        Session.ReplaceRange(new TextSpan(indexOfPreviousNewline, 0), lineText);
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

    private static EditTextWindow CreateSelectionWindow(string selectedText, EtwEditorMode targetMode)
    {
        EditTextWindow selectionWindow = new(selectedText, false);
        if (targetMode != EtwEditorMode.Text)
            selectionWindow.SetEditorMode(targetMode);

        return selectionWindow;
    }

    private void NewWindowWithText_Clicked(object sender, RoutedEventArgs e)
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            OpenNewWindowWithSpreadsheetSelection();
            return;
        }

        if (editorMode == EtwEditorMode.Markdown)
        {
            OpenNewWindowWithMarkdownSelection();
            return;
        }

        string selectedText = Session.SelectedText;
        EditTextWindow newEtwWithText = CreateSelectionWindow(selectedText, EtwEditorMode.Text);
        newEtwWithText.Show();
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

        int caretIndex = Session.Selection.Start;

        AddPossibleSpellingErrorsToRightClickMenu(caretIndex);

        AddPossibleURLToRightClickMenu(caretIndex);

        AddPossibleMailToToRightClickMenu(caretIndex);

    }

    private void PassedTextControl_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (editorMode is EtwEditorMode.Spreadsheet or EtwEditorMode.Markdown)
            return;

        UpdateLineAndColumnText();
    }

    private void PassedTextControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLineAndColumnText();
        if (editorMode != EtwEditorMode.Markdown)
            SetMargins(MarginsMenuItem.IsChecked is true);
    }

    private const int SpellCheckDisableThreshold = 10_000;
    // A "very long word" is one the spell checker will choke on (GUIDs, manifest tokens, etc.)
    private const int SpellCheckLongWordLength = 25;
    private const int SpellCheckLongWordCountThreshold = 3;

    /// <summary>
    /// Returns true when spell checking should be active for the given text.
    /// Disabled for large documents and for content that contains several very long
    /// unspaced tokens (app manifests, GUIDs, base64 blobs, etc.).
    /// </summary>
    internal static bool ShouldEnableSpellCheck(string text)
    {
        if (text.Length > SpellCheckDisableThreshold)
            return false;

        int longWordCount = 0;
        int wordStart = -1;
        for (int i = 0; i <= text.Length; i++)
        {
            bool isWordChar = i < text.Length && !char.IsWhiteSpace(text[i]);
            if (isWordChar)
            {
                if (wordStart < 0)
                    wordStart = i;
            }
            else if (wordStart >= 0)
            {
                if (i - wordStart >= SpellCheckLongWordLength)
                {
                    longWordCount++;
                    if (longWordCount >= SpellCheckLongWordCountThreshold)
                        return false;
                }
                wordStart = -1;
            }
        }

        return true;
    }

    /// <summary>
    /// Resolves whether spell check should be active for the given mode and text.
    /// Always On / Off force the state; Auto defers to <see cref="ShouldEnableSpellCheck(string)"/>.
    /// </summary>
    internal static bool ShouldEnableSpellCheck(SpellCheckMode mode, string text) => mode switch
    {
        SpellCheckMode.AlwaysOn => true,
        SpellCheckMode.Off => false,
        _ => ShouldEnableSpellCheck(text),
    };

    /// <summary>
    /// Applies the current <see cref="spellCheckMode"/> to the editors. In Auto mode the
    /// content is re-evaluated (the scan is O(n) but short-circuits early, so it stays fast
    /// even for large pastes); Always On / Off force the state regardless of content.
    /// </summary>
    private void ApplySpellCheckMode()
    {
        bool shouldSpellCheck = ShouldEnableSpellCheck(spellCheckMode, Session.Text);

        if (SpellCheck.GetIsEnabled(PassedTextControl) != shouldSpellCheck)
            SpellCheck.SetIsEnabled(PassedTextControl, shouldSpellCheck);

        if (SpellCheck.GetIsEnabled(MarkdownEditorControl) != shouldSpellCheck)
            SpellCheck.SetIsEnabled(MarkdownEditorControl, shouldSpellCheck);
    }

    private void SetSpellCheckMode(SpellCheckMode mode)
    {
        spellCheckMode = mode;
        SetSpellCheckMenuItems();
        ApplySpellCheckMode();
    }

    private void SetSpellCheckMenuItems()
    {
        SpellCheckAutoMenuItem.IsChecked = spellCheckMode == SpellCheckMode.Auto;
        SpellCheckAlwaysOnMenuItem.IsChecked = spellCheckMode == SpellCheckMode.AlwaysOn;
        SpellCheckOffMenuItem.IsChecked = spellCheckMode == SpellCheckMode.Off;
    }

    private void SpellCheckModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem
            || !Enum.TryParse(menuItem.Tag?.ToString(), out SpellCheckMode mode))
        {
            // Re-sync the check marks so a stray click can't leave the menu inconsistent.
            SetSpellCheckMenuItems();
            return;
        }

        SetSpellCheckMode(mode);
        DefaultSettings.EtwSpellCheckMode = mode.ToString();
        DefaultSettings.Save();
    }

    private void PassedTextControl_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DefaultSettings.EditWindowStartFullscreen && prevWindowState is not null)
        {
            this.WindowState = prevWindowState.Value;
            prevWindowState = null;
        }

        ApplySpellCheckMode();

        UpdateLineAndColumnText();

        // Reset the debounce timer
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
        // If a newline append auto-scrolls the main box, ensure calc scroll follows too
        // Schedule after layout so offsets are accurate
        Dispatcher.BeginInvoke(SyncCalcScrollToMain, DispatcherPriority.Background);

        if (isSyncingTextFromSpreadsheet || isSyncingTextFromMarkdown)
        {
            if (isSyncingTextFromMarkdown)
                ResetSpreadsheetUndoHistory();

            UpdatePendingFileEditState();
            return;
        }

        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            RefreshSpreadsheetFromText();
            UpdatePendingFileEditState();
            return;
        }

        if (editorMode == EtwEditorMode.Markdown)
        {
            // OpenPath builds the document once itself after setting the text.
            if (!(isLoadingOpenedFile && IsLoaded))
                RefreshMarkdownFromText();

            UpdatePendingFileEditState();
            return;
        }

        ResetSpreadsheetUndoHistory();
        // Invalidate the cached document instead of eagerly re-parsing the entire text
        // on every keystroke. It will be rebuilt lazily when switching to spreadsheet mode.
        tableDocument = null;
        UpdatePendingFileEditState();
    }

    private DispatcherTimer? _debounceTimer = null;
    private const int DEBOUNCE_DELAY_MS = 300;
    private readonly CalculationService _calculationService = new();
    private AggregateType _selectedAggregate = AggregateType.None;

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
                string textFromClipboard;
                if (editorMode == EtwEditorMode.Text
                    && ClipboardUtilities.TryGetHtmlTableAsTabSeparated(out string htmlTableText))
                    textFromClipboard = htmlTableText;
                else
                    textFromClipboard = await dataPackageView.GetTextAsync();

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
                List<OcrOutput> outputs = await OcrEngineUtilities.GetTextFromRandomAccessStream(stream, LanguageUtilities.GetOCRLanguage());
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
                    List<OcrOutput> outputs = await OcrEngineUtilities.GetTextFromRandomAccessStream(stream, LanguageUtilities.GetOCRLanguage());
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
            await OcrSourceUtilities.GetTextFromPreviousFullscreenRegion(PassedTextControl);
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
            Session.Text = copiedText;
        }
        catch (Exception ex)
        {
            Session.Text = rawEncodedString;
            Session.Text += ex.Message;
        }
    }

    private void RemoveDuplicateLines_Click(object sender, RoutedEventArgs e)
    {
        Session.Text = Session.Text.RemoveDuplicateLines();
    }

    private void ShuffleLinesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplySelectedTextOrAllTextTransform(text => text.ShuffleLines());
    }

    private void ReplaceReservedCharsCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        // Simplified: reserved chars (spaces, punctuation) are nearly always present in
        // any non-empty text, so scanning the full content on every CanExecute is wasteful.
        e.CanExecute = GetSelectedOrAllTextSegmentsForEdit().Any(text => text.Length > 0);
    }

    private void ReplaceReservedCharsCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ApplySelectedTextOrAllTextTransform(text => text.ReplaceReservedCharacters());
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

        if (!Enum.TryParse(DefaultSettings.EtwSpellCheckMode, out SpellCheckMode savedSpellCheckMode))
            savedSpellCheckMode = SpellCheckMode.Auto;
        SetSpellCheckMode(savedSpellCheckMode);

        SetBottomBarButtons();
    }

    private void SyncTextFromActiveEditor()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            SyncSpreadsheetDocumentFromTable();
        else if (editorMode == EtwEditorMode.Markdown)
            SyncMarkdownTextFromDocument();
    }

    /// <summary>
    /// The mirror of <see cref="SyncTextFromActiveEditor"/>: pushes <c>Session.Text</c> out
    /// to whichever editor is showing, the same way <see cref="PassedTextControl_TextChanged"/> does.
    /// Used by anything that writes straight into the raw text box while another editor is active —
    /// streamed audio transcription, most of all. A no-op in raw-text mode.
    /// </summary>
    private void SyncActiveEditorFromText()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            RefreshSpreadsheetFromText();
        else if (editorMode == EtwEditorMode.Markdown)
            RefreshMarkdownFromText();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        LaunchFindAndReplace();
    }

    private void SetFontFromSettings()
    {
        PassedTextControl.FontFamily = new FontFamily(DefaultSettings.FontFamilySetting);
        PassedTextControl.FontSize = DefaultSettings.FontSizeSetting;
        MarkdownEditorControl.FontFamily = PassedTextControl.FontFamily;
        MarkdownEditorControl.FontSize = PassedTextControl.FontSize;
        SpreadsheetDataGrid.FontFamily = PassedTextControl.FontFamily;
        SpreadsheetDataGrid.FontSize = PassedTextControl.FontSize;
        if (DefaultSettings.IsFontBold)
            PassedTextControl.FontWeight = FontWeights.Bold;
        if (DefaultSettings.IsFontItalic)
            PassedTextControl.FontStyle = FontStyles.Italic;

        TextDecorationCollection tdc = [];
        if (DefaultSettings.IsFontUnderline) tdc.Add(TextDecorations.Underline);
        if (DefaultSettings.IsFontStrikeout) tdc.Add(TextDecorations.Strikethrough);
        PassedTextControl.TextDecorations = tdc;

        if (MarkdownEditorControl.Document is not null)
        {
            MarkdownEditorControl.Document.FontFamily = MarkdownEditorControl.FontFamily;
            MarkdownEditorControl.Document.FontSize = MarkdownEditorControl.FontSize;
            ApplyMarkdownTheme();
        }
    }

    private void SetMargins(bool AreThereMargins)
    {
        Thickness padding = new(0);
        double editorWidth = editorMode == EtwEditorMode.Markdown && MarkdownEditorControl.ActualWidth > 0
            ? MarkdownEditorControl.ActualWidth
            : PassedTextControl.ActualWidth;

        if (AreThereMargins)
        {
            if (editorWidth < 400)
                padding = new Thickness(10, 0, 10, 0);
            else if (editorWidth < 1000)
                padding = new Thickness(50, 0, 50, 0);
            else if (editorWidth < 1400)
                padding = new Thickness(100, 0, 100, 0);
            else
                padding = new Thickness(160, 0, 160, 0);
        }

        PassedTextControl.Padding = padding;
        MarkdownEditorControl.Padding = padding;
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<SettingsWindow>();
    }

    private void SetupRoutedCommands()
    {
        _ = CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, SpreadsheetUndoExecuted, SpreadsheetUndoCanExecute));
        _ = CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, SpreadsheetRedoExecuted, SpreadsheetRedoCanExecute));
        _ = CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, SpreadsheetCutExecuted, SpreadsheetCopyCanExecute));
        _ = CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, SpreadsheetCopyExecuted, SpreadsheetCopyCanExecute));
        _ = CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, SpreadsheetPasteExecuted, SpreadsheetPasteCanExecute));

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

        RoutedCommand findAndReplaceCommand = new();
        _ = findAndReplaceCommand.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control | ModifierKeys.Shift));
        _ = CommandBindings.Add(new CommandBinding(findAndReplaceCommand, FindAndReplaceMenuItem_Click));

        RoutedCommand newWindowWithSelectionCommand = new();
        _ = newWindowWithSelectionCommand.InputGestures.Add(new KeyGesture(Key.N, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(newWindowWithSelectionCommand, NewWindowWithText_Clicked));

        List<WebSearchUrlModel> searchers = Singleton<WebSearchUrlCatalog>.Instance.WebSearchers;

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
        e.CanExecute = GetSelectedOrAllTextSegmentsForEdit()
            .Any(text => text.Contains(Environment.NewLine)
                || text.Contains('\r')
                || text.Contains('\n'));
    }

    private void SingleLineCmdExecuted(object sender, ExecutedRoutedEventArgs? e = null)
    {
        ApplySelectedTextOrAllTextTransform(text => text.MakeStringSingleLine());
    }

    private void SplitOnSelectionCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        // Spreadsheet mode has its own cell-aware "Split Cells" feature
        // (SplitSelectedSpreadsheetCells); this flat-text version doesn't apply there.
        if (editorMode == EtwEditorMode.Spreadsheet
            || string.IsNullOrEmpty(Session.SelectedText))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private async void SplitOnSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string selectedText = Session.SelectedText;

        if (string.IsNullOrEmpty(selectedText))
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Did not split lines",
                Content = "No text selected",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return;
        }

        StringBuilder textToManipulate = new(Session.Text);

        textToManipulate = textToManipulate.Replace(selectedText, Environment.NewLine + selectedText);

        Session.Text = textToManipulate.ToString();
    }

    private async void SplitAfterSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string selectedText = Session.SelectedText;

        if (string.IsNullOrEmpty(selectedText))
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Did not split lines",
                Content = "No text selected",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return;
        }

        StringBuilder textToManipulate = new(Session.Text);

        textToManipulate = textToManipulate.Replace(selectedText, selectedText + Environment.NewLine);

        Session.Text = textToManipulate.ToString();
    }

    private void ToggleCase(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            CaseStatusOfToggle = CurrentCase.Unknown;
            ApplySelectedTextOrAllTextTransform(text =>
            {
                CurrentCase caseStatus = StringMethods.DetermineToggleCase(text);
                return caseStatus switch
                {
                    CurrentCase.Lower => selectedCultureInfo.TextInfo.ToLower(text),
                    CurrentCase.Camel => selectedCultureInfo.TextInfo.ToTitleCase(text),
                    CurrentCase.Upper => selectedCultureInfo.TextInfo.ToUpper(text),
                    _ => text,
                };
            });
            return;
        }

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

        if (Session.Selection.Length == 0)
            Session.Text = textToModify;
        else
            Session.ReplaceSelection(textToModify);
    }

    private void ToggleCaseCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        // Simplified: avoid scanning every character on each CanExecute query.
        // Any non-empty text segment is likely to contain letters.
        e.CanExecute = GetSelectedOrAllTextSegmentsForEdit().Any(text => text.Length > 0);
    }

    private void TrimEachLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            TryApplySpreadsheetTextTransform(StringMethods.TrimEachLine);
            return;
        }

        Session.Text = Session.Text.TrimEachLine();
    }

    private void CleanUpText_Click(object sender, RoutedEventArgs e)
    {
        bool correctToLatin = AppUtilities.ShouldCorrectToLatin();
        ApplySelectedTextOrAllTextTransform(text => text.CleanUpText(correctToLatin));
    }

    private void TryToAlphaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplySelectedTextOrAllTextTransform(text => text.TryFixToLetters());
    }

    private void TryToNumberMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplySelectedTextOrAllTextTransform(text => text.TryFixToNumbers());
    }

    private void UnstackExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selection = NewlineReturns().Replace(Session.SelectedText, Environment.NewLine);
        string[] selectionLines = selection.Split(Environment.NewLine);
        int numberOfLines = selectionLines.Length;

        Session.Text = Session.Text.UnstackStrings(numberOfLines);
    }

    private void UnstackGroupExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selection = NewlineReturns().Replace(Session.SelectedText, Environment.NewLine);
        string[] selectionLines = selection.Split(Environment.NewLine);
        int numberOfLines = selectionLines.Length;

        Session.Text = Session.Text.UnstackGroups(numberOfLines);
    }

    private void Window_Activated(object sender, EventArgs e)
    {
        // Pick up a spell-check mode changed from the settings page while this window was open.
        if (Enum.TryParse(DefaultSettings.EtwSpellCheckMode, out SpellCheckMode settingsMode)
            && settingsMode != spellCheckMode)
            SetSpellCheckMode(settingsMode);

        if (editorMode == EtwEditorMode.Spreadsheet)
            SpreadsheetDataGrid.Focus();
        else if (editorMode == EtwEditorMode.Markdown)
        {
            ApplyMarkdownThemeIfChanged();
            MarkdownEditorControl.Focus();
        }
        else
            PassedTextControl.Focus();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _isClosed = true;
        ++_liveTranscriptionRequest;

        // Stop any in-progress audio-file transcription so it doesn't touch a torn-down window.
        _transcriptionCts?.Cancel();

        // Forced shutdown can bypass the deferred close. Dispose queues cleanup behind the
        // transcriber's current operation; stale starts and dispatcher deliveries are ignored.
        if (_liveTranscriber is not null)
        {
            _liveTranscriber.PhraseRecognized -= LiveTranscriber_PhraseRecognized;
            _liveTranscriber.Dispose();
            _liveTranscriber = null;
        }

        DetachSpreadsheetColumnWidthTracking();
        System.Windows.DataObject.RemovePastingHandler(MarkdownEditorControl, MarkdownEditorControl_Pasting);

        windowSource?.RemoveHook(EditTextWindowMessageHook);

        EscapeKeyTimer.Stop();
        EscapeKeyTimer.Tick -= EscapeKeyTimer_Tick;

        MarkdownEditorControl.RemoveHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(MarkdownEditorControl_RequestNavigate));
        PassedTextControl.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(PassedTextControl_ScrollChanged));
        CalcResultsTextControl.PreviewMouseWheel -= CalcResultsTextControl_PreviewMouseWheel;

        HideCalcPaneContextItem.Click -= HideCalcPaneContextItem_Click;
        ShowCalcErrorsContextItem.Click -= ShowCalcErrorsContextItem_Click;
        CopyAllContextItem.Click -= CopyAllContextItem_Click;

        ClearDynamicMenuItems();

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

        AppServices.Provider.GetRequiredService<ClipboardMonitorService>().ContentChanged -= Clipboard_ContentChanged;

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

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isStoppingLiveTranscriptionForClose)
        {
            e.Cancel = true;
            return;
        }

        if (_liveTranscriber is not null)
        {
            e.Cancel = true;
            _isStoppingLiveTranscriptionForClose = true;
            await StopLiveTranscriptionAndCloseAsync();
            return;
        }

        SyncTextFromActiveEditor();
        UpdatePendingFileEditState();

        if (allowCloseAfterPendingFilePrompt)
        {
            allowCloseAfterPendingFilePrompt = false;
            SaveWindowTextToHistoryIfNeeded();
            return;
        }

        if (isShowingPendingFileClosePrompt)
        {
            e.Cancel = true;
            return;
        }

        if (!hasPendingFileEdits)
        {
            SaveWindowTextToHistoryIfNeeded();
            return;
        }

        e.Cancel = true;
        isShowingPendingFileClosePrompt = true;
        _ = HandlePendingFileClosePromptAsync();
    }

    private void Window_Initialized(object sender, EventArgs e)
    {
        PassedTextControl.PreviewMouseWheel += HandlePreviewMouseWheel;
        MarkdownEditorControl.PreviewMouseWheel += HandlePreviewMouseWheel;
        MarkdownEditorControl.PreviewTextInput += MarkdownEditorControl_PreviewTextInput;
        System.Windows.DataObject.AddPastingHandler(MarkdownEditorControl, MarkdownEditorControl_Pasting);
        SetFontFromSettings();
        UpdateSpreadsheetModeUi();
        UpdateWindowTitle();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SetupRoutedCommands();

        if (windowSource is null)
        {
            nint windowHandle = new WindowInteropHelper(this).Handle;
            windowSource = HwndSource.FromHwnd(windowHandle);
            windowSource?.AddHook(EditTextWindowMessageHook);
        }

        PassedTextControl.ContextMenu = this.FindResource("ContextMenuResource") as ContextMenu;
        if (PassedTextControl.ContextMenu != null)
            numberOfContextMenuItems = PassedTextControl.ContextMenu.Items.Count;
        MarkdownEditorControl.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(MarkdownEditorControl_RequestNavigate));

        CheckRightToLeftLanguage();

        RestoreWindowSettings();

        // One app-wide subscription (v5 F5), started here on the UI thread as the direct WinRT
        // subscription was. ContentChanged forwards every change, as WinRT's event did.
        ClipboardMonitorService clipboardMonitor = AppServices.Provider.GetRequiredService<ClipboardMonitorService>();
        clipboardMonitor.ContentChanged -= Clipboard_ContentChanged;
        clipboardMonitor.ContentChanged += Clipboard_ContentChanged;
        clipboardMonitor.Start();

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

            NotifyOnLocalAiCompleteMenuItem.IsChecked = DefaultSettings.NotifyOnLocalAiComplete;
            SendLocalAiResultToNewWindowMenuItem.IsChecked = DefaultSettings.SendLocalAiResultToNewWindow;
        }

        // Audio transcription runs locally on the CPU via Whisper (whisper.cpp), so it's available
        // on every supported device. The Whisper model is downloaded on first use.
        if (AudioTranscriptionUtilities.IsAudioTranscriptionSupported())
        {
            CaptureTranscribeAudioMenuItem.Visibility = Visibility.Visible;
            TranscriptionOptionsMenuItem.Visibility = Visibility.Visible;
            OpenAudioVideoMenuItem.Visibility = Visibility.Visible;
            TranscribeJustIconMenuItem.IsChecked = DefaultSettings.TranscribeButtonJustIcon;
            LiveTranscriptionLabel.Visibility = DefaultSettings.TranscribeButtonJustIcon ? Visibility.Collapsed : Visibility.Visible;
        }

        // Initialize selectedILanguage with the last used OCR language from settings
        // This ensures that when images are dropped or pasted, the correct language is used
        selectedILanguage = LanguageUtilities.GetOCRLanguage();

        // Warm up the cached clipboard OCR state so CanOcrPasteExecute is accurate immediately.
        UpdateCachedClipboardOcrState();

        if (editorMode == EtwEditorMode.Spreadsheet)
            SetEditorMode(EtwEditorMode.Spreadsheet);
        else if (editorMode == EtwEditorMode.Markdown)
            SetEditorMode(EtwEditorMode.Markdown);
        else
            UpdateSpreadsheetModeUi();
    }

    private void EscapeKeyTimer_Tick(object? sender, EventArgs e)
    {
        EscapeKeyTimer.Stop();

        if (EscapeKeyTimerCount >= 3)
            Close();

        EscapeKeyTimerCount = 0;
    }

    private void WrapTextCHBX_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (WrapTextMenuItem.IsChecked)
            PassedTextControl.TextWrapping = TextWrapping.Wrap;
        else
            PassedTextControl.TextWrapping = TextWrapping.NoWrap;

        ApplyMarkdownWrapSetting();

        DefaultSettings.EditWindowIsWordWrapOn = WrapTextMenuItem.IsChecked;
    }

    private void CorrectGuid_Click(object sender, RoutedEventArgs e)
    {
        ApplySelectedTextOrAllTextTransform(text => text.CorrectCommonGuidErrors());
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

    private void RegexManagerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RegexManager regexManager = WindowUtilities.OpenOrActivateWindow<RegexManager>();
        regexManager.Show();
    }

    [GeneratedRegex(@"(\r\n|\n|\r)")]
    private static partial Regex NewlineReturns();

    #endregion Methods
}
