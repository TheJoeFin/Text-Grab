using Humanizer;
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
using Text_Grab.Interfaces;
using Text_Grab.Models;
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
    private EtwEditorMode editorMode = EtwEditorMode.Text;
    private SpellCheckMode spellCheckMode = SpellCheckMode.Auto;
    private bool isSyncingTextFromSpreadsheet = false;
    private bool isSyncingTextFromMarkdown = false;
    private bool isApplyingSpreadsheetLayout = false;
    private bool isApplyingMarkdownDocument = false;
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

    private enum PendingFileCloseAction
    {
        Cancel,
        Save,
        DontSave,
        SaveToHistory,
    }

    private sealed class SpreadsheetCellTextWrappingConverter(EditTextWindow owner, int columnIndex) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return owner.GetSpreadsheetCellTextWrapping(value, columnIndex);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Windows.Data.Binding.DoNothing;
        }
    }

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
        editorMode = historyInfo.EditorMode;
        tableDocument = EditTextTableDocument.TryDeserialize(historyInfo.EditTextTableDocumentJson);

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
        PassedTextControl.Text = PassedTextControl.Text.AddCharsToEachLine(stringToAdd, spotInLine);
    }

    public void JoinLinesInEditTextWindow(string joiningText, bool trimLineBeforeJoining, string textAtBeginning = "", string textAtEnd = "")
    {
        ApplySelectedTextOrAllTextTransform(text => text.JoinLines(joiningText, trimLineBeforeJoining, textAtBeginning, textAtEnd));
    }

    public void AddThisText(string textToAdd)
    {
        PassedTextControl.AppendText(textToAdd);
    }

    public System.Windows.Controls.TextBox GetMainTextBox()
    {
        return PassedTextControl;
    }

    internal void EnterSpreadsheetMode() => SetEditorMode(EtwEditorMode.Spreadsheet);

    /// <summary>
    /// Pastes the clipboard into the spreadsheet editor, parsing HTML tables
    /// (including colspan/rowspan) when present. Used by the text-grab://
    /// paste-spreadsheet protocol activation.
    /// </summary>
    internal void PasteClipboardIntoSpreadsheet() => PasteIntoSpreadsheet();

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

        List<string> imageFiles = [.. files.Where(x => IoUtilities.IsVisualDocumentFileExtension(Path.GetExtension(x).ToLower()))];

        if (imageFiles.Count == 0)
        {
            PassedTextControl.AppendText($"{folderPath} contains no images or PDFs");
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
            PassedTextControl.AppendText(folderPath);
            PassedTextControl.AppendText(Environment.NewLine);
            PassedTextControl.AppendText($"{imageFiles.Count} files found");

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

            if (options.GrabTemplate is GrabTemplate headerTemplate)
            {
                PassedTextControl.AppendText($"Using template: {headerTemplate.Name}");
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
                PassedTextControl.AppendText($"----- COMPLETED OCR OF {imageFiles.Count} files");
            }
        }
        catch (OperationCanceledException)
        {
            PassedTextControl.AppendText(Environment.NewLine);
            int countCompleted = ocrFileResults.Where(r => r.OcrResult is not null).Count();
            PassedTextControl.AppendText($"----- CANCELLED OCR OF {ocrFileResults.Count - countCompleted}, Completed {countCompleted} files");
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

    private void ApplySpreadsheetDocumentChange(
        Action<EditTextTableDocument> changeAction,
        int? focusRow = null,
        int? focusColumn = null,
        bool beginEdit = true)
    {
        CommitSpreadsheetEditsAndCapturePendingHistory();
        SpreadsheetUndoState? beforeChange = CreateCurrentSpreadsheetUndoState(syncFromTable: true);

        if (tableDocument is null)
            return;

        changeAction(tableDocument);
        tableDocument.EnsureMinimumSize();
        RecordSpreadsheetUndoChange(beforeChange, CreateCurrentSpreadsheetUndoState(syncFromTable: false));
        RebuildSpreadsheetTable();
        UpdateTextFromSpreadsheetDocument();

        if (focusRow.HasValue && focusColumn.HasValue)
        {
            Dispatcher.BeginInvoke(
                () => FocusSpreadsheetCell(focusRow.Value, focusColumn.Value, beginEdit),
                DispatcherPriority.Background);
        }
    }

    private SpreadsheetUndoState? CreateCurrentSpreadsheetUndoState(bool syncFromTable = false)
    {
        if (syncFromTable && editorMode == EtwEditorMode.Spreadsheet)
            SyncSpreadsheetDocumentFromTable(writeText: false);

        if (tableDocument is null)
            return null;

        tableDocument.EnsureMinimumSize();
        return new SpreadsheetUndoState(
            tableDocument.SerializeToJson(),
            GetSpreadsheetCurrentRowIndex(),
            GetSpreadsheetCurrentColumnIndex());
    }

    private int? GetSpreadsheetCurrentRowIndex()
    {
        int rowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        return rowIndex >= 0 ? rowIndex : null;
    }

    private int? GetSpreadsheetCurrentColumnIndex()
    {
        return SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex;
    }

    private void CommitSpreadsheetEditsAndCapturePendingHistory()
    {
        if (editorMode != EtwEditorMode.Spreadsheet)
            return;

        _ = SpreadsheetDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        _ = SpreadsheetDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        CaptureCommittedSpreadsheetEditIfPending();
    }

    private void CaptureCommittedSpreadsheetEditIfPending()
    {
        if (pendingSpreadsheetUndoState is null || isRestoringSpreadsheetUndoState)
            return;

        SpreadsheetUndoState beforeChange = pendingSpreadsheetUndoState;
        pendingSpreadsheetUndoState = null;
        RecordSpreadsheetUndoChange(beforeChange, CreateCurrentSpreadsheetUndoState(syncFromTable: true));
    }

    private void RecordSpreadsheetUndoChange(SpreadsheetUndoState? beforeChange, SpreadsheetUndoState? afterChange)
    {
        spreadsheetUndoHistory.RecordChange(beforeChange, afterChange);
        CommandManager.InvalidateRequerySuggested();
    }

    private void ResetSpreadsheetUndoHistory()
    {
        bool wasNonEmpty = spreadsheetUndoHistory.CanUndo || spreadsheetUndoHistory.CanRedo;
        spreadsheetUndoHistory.Clear();
        pendingSpreadsheetUndoState = null;
        // Only invalidate when the undo/redo availability actually changed to avoid
        // triggering all CanExecute handlers on every text change in text mode.
        if (wasNonEmpty)
            CommandManager.InvalidateRequerySuggested();
    }

    private void RestoreSpreadsheetUndoState(SpreadsheetUndoState stateToRestore)
    {
        EditTextTableDocument? restoredDocument = EditTextTableDocument.TryDeserialize(stateToRestore.DocumentJson);
        if (restoredDocument is null)
            return;

        isRestoringSpreadsheetUndoState = true;
        try
        {
            pendingSpreadsheetUndoState = null;
            tableDocument = restoredDocument;
            RebuildSpreadsheetTable();
            UpdateTextFromSpreadsheetDocument();
        }
        finally
        {
            isRestoringSpreadsheetUndoState = false;
        }

        if (SpreadsheetDataGrid.Items.Count == 0 || SpreadsheetDataGrid.Columns.Count == 0)
        {
            UpdateLineAndColumnText();
            return;
        }

        int focusRow = Math.Clamp(stateToRestore.FocusRow ?? 0, 0, SpreadsheetDataGrid.Items.Count - 1);
        int focusColumn = Math.Clamp(stateToRestore.FocusColumn ?? 0, 0, SpreadsheetDataGrid.Columns.Count - 1);

        Dispatcher.BeginInvoke(
            () => FocusSpreadsheetCell(focusRow, focusColumn, beginEdit: false),
            DispatcherPriority.Background);
        UpdateLineAndColumnText();
    }

    private void CopySpreadsheetColumnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        int columnIndex =
            spreadsheetContextColumnIndex
            ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex
            ?? -1;

        if (columnIndex < 0)
            return;
        List<string> values = [];

        foreach (DataRow row in spreadsheetTable.Rows)
        {
            if (columnIndex >= spreadsheetTable.Columns.Count)
                break;

            values.Add(row[columnIndex]?.ToString() ?? string.Empty);
        }

        TrySetClipboardText(string.Join(Environment.NewLine, values));
    }

    private void CopySpreadsheetRowsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        List<DataRowView> selectedRows = [.. SpreadsheetDataGrid.SelectedItems.OfType<DataRowView>()];

        if (selectedRows.Count == 0 && SpreadsheetDataGrid.CurrentItem is DataRowView currentRow)
            selectedRows.Add(currentRow);

        if (selectedRows.Count == 0)
            return;

        string rowText = string.Join(
            Environment.NewLine,
            selectedRows.Select(row => string.Join("\t", row.Row.ItemArray.Select(value => value?.ToString() ?? string.Empty))));

        TrySetClipboardText(rowText);
    }

    private void CopySpreadsheetSelectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = TryCopySpreadsheetSelectionToClipboard(GetSelectedSpreadsheetCellCoordinates());
    }

    private void AddSpreadsheetColumnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        int currentColumnIndex =
            spreadsheetContextColumnIndex
            ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex
            ?? ((tableDocument?.ColumnCount ?? 1) - 1);
        int insertIndex = Math.Clamp(currentColumnIndex + 1, 0, Math.Max(tableDocument?.ColumnCount ?? 0, 0));

        ApplySpreadsheetDocumentChange(
            document => document.InsertColumn(insertIndex),
            SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem),
            insertIndex);
    }

    private void AddSpreadsheetRowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        int currentRowIndex = spreadsheetContextRowIndex ?? SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        if (currentRowIndex < 0)
            currentRowIndex = (tableDocument?.RowCount ?? 1) - 1;

        int insertIndex = Math.Clamp(currentRowIndex + 1, 0, Math.Max(tableDocument?.RowCount ?? 0, 0));
        int focusColumn = SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0;

        ApplySpreadsheetDocumentChange(
            document => document.InsertRow(insertIndex),
            insertIndex,
            focusColumn);
    }

    private void TransposeTableCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = editorMode == EtwEditorMode.Spreadsheet;
    }

    private void TransposeTableExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        int currentRowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        int currentColumnIndex = SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0;

        ApplySpreadsheetDocumentChange(document => document.Transpose());

        if (SpreadsheetDataGrid.Items.Count == 0 || SpreadsheetDataGrid.Columns.Count == 0)
            return;

        int focusRow = Math.Clamp(currentColumnIndex, 0, SpreadsheetDataGrid.Items.Count - 1);
        int focusColumn = Math.Clamp(Math.Max(0, currentRowIndex), 0, SpreadsheetDataGrid.Columns.Count - 1);

        Dispatcher.BeginInvoke(
            () => FocusSpreadsheetCell(focusRow, focusColumn),
            DispatcherPriority.Background);
    }

    private void DeleteSpreadsheetColumnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        int columnIndex = spreadsheetContextColumnIndex ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? -1;
        if (columnIndex < 0)
            return;

        int nextColumnIndex = Math.Max(0, Math.Min(columnIndex, (tableDocument?.ColumnCount ?? 1) - 2));
        int rowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);

        ApplySpreadsheetDocumentChange(
            document => document.DeleteColumn(columnIndex),
            Math.Max(0, rowIndex),
            nextColumnIndex);
    }

    private void DeleteSpreadsheetRowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        int rowIndex = spreadsheetContextRowIndex ?? SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        if (rowIndex < 0)
            return;

        int nextRowIndex = Math.Max(0, Math.Min(rowIndex, (tableDocument?.RowCount ?? 1) - 2));
        int columnIndex = SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0;

        ApplySpreadsheetDocumentChange(
            document => document.DeleteRow(rowIndex),
            nextRowIndex,
            columnIndex);
    }

    private void EnsureSpreadsheetDocumentFromText()
    {
        if (tableDocument is not null)
        {
            tableDocument.EnsureMinimumSize();
            return;
        }

        tableDocument = EditTextTableDocument.CreateFromText(PassedTextControl.Text);
    }

    private void FocusSpreadsheetCell(int rowIndex, int columnIndex, bool beginEdit = true)
    {
        if (rowIndex < 0
            || columnIndex < 0
            || rowIndex >= SpreadsheetDataGrid.Items.Count
            || columnIndex >= SpreadsheetDataGrid.Columns.Count)
        {
            return;
        }

        object rowItem = SpreadsheetDataGrid.Items[rowIndex];
        DataGridColumn column = SpreadsheetDataGrid.Columns[columnIndex];

        SelectSpreadsheetCell(rowItem, column, clearExistingSelection: true);

        if (beginEdit)
            SpreadsheetDataGrid.BeginEdit();
    }

    private void MoveSpreadsheetColumnLeftMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveSpreadsheetColumn(-1);
    }

    private void MoveSpreadsheetColumnRightMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveSpreadsheetColumn(1);
    }

    private void MoveSpreadsheetColumn(int direction)
    {
        int fromIndex = spreadsheetContextColumnIndex ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? -1;
        if (fromIndex < 0)
            return;

        int toIndex = fromIndex + direction;
        if (toIndex < 0 || toIndex >= (tableDocument?.ColumnCount ?? 0))
            return;

        int rowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        ApplySpreadsheetDocumentChange(
            document => document.MoveColumn(fromIndex, toIndex),
            Math.Max(0, rowIndex),
            toIndex);
    }

    private void MoveSpreadsheetRowDownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveSpreadsheetRow(1);
    }

    private void MoveSpreadsheetRowUpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveSpreadsheetRow(-1);
    }

    private void MoveSpreadsheetRow(int direction)
    {
        int fromIndex = spreadsheetContextRowIndex ?? SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        if (fromIndex < 0)
            return;

        int toIndex = fromIndex + direction;
        if (toIndex < 0 || toIndex >= (tableDocument?.RowCount ?? 0))
            return;

        int columnIndex = SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0;
        ApplySpreadsheetDocumentChange(
            document => document.MoveRow(fromIndex, toIndex),
            toIndex,
            columnIndex);
    }

    private void HideSelectionSpecificUi()
    {
        MatchCountButton.Visibility = Visibility.Collapsed;
        RegexPatternButton.Visibility = Visibility.Collapsed;
        SimilarMatchesButton.Visibility = Visibility.Collapsed;
        CharDetailsButton.Visibility = Visibility.Collapsed;
    }

    private void RebuildSpreadsheetTable()
    {
        if (tableDocument is null)
            return;

        DetachSpreadsheetColumnWidthTracking();
        isApplyingSpreadsheetLayout = true;
        spreadsheetTable.BeginInit();
        spreadsheetTable.Clear();
        spreadsheetTable.Columns.Clear();

        foreach (string columnName in tableDocument.ColumnNames)
            spreadsheetTable.Columns.Add(columnName, typeof(string));

        foreach (List<string> row in tableDocument.Rows)
        {
            DataRow dataRow = spreadsheetTable.NewRow();
            for (int columnIndex = 0; columnIndex < tableDocument.ColumnNames.Count; columnIndex++)
                dataRow[columnIndex] = columnIndex < row.Count ? row[columnIndex] ?? string.Empty : string.Empty;

            spreadsheetTable.Rows.Add(dataRow);
        }

        spreadsheetTable.EndInit();

        SpreadsheetDataGrid.ItemsSource = spreadsheetTable.DefaultView;
        selectedSpreadsheetCellCoordinates = [];
        SpreadsheetDataGrid.Columns.Clear();

        for (int columnIndex = 0; columnIndex < spreadsheetTable.Columns.Count; columnIndex++)
        {
            DataColumn column = spreadsheetTable.Columns[columnIndex];
            double width = tableDocument.ColumnWidths.ElementAtOrDefault(columnIndex) ?? SpreadsheetDefaultColumnWidth;
            DataGridTextColumn gridColumn = new()
            {
                Header = EditTextTableDocument.GetSpreadsheetColumnLabel(columnIndex),
                Binding = new System.Windows.Data.Binding($"[{column.ColumnName}]"),
                ElementStyle = CreateSpreadsheetDisplayTextStyle(columnIndex),
                EditingElementStyle = CreateSpreadsheetEditingTextStyle(columnIndex),
                MinWidth = SpreadsheetDefaultColumnWidth,
                Width = new DataGridLength(Math.Max(SpreadsheetDefaultColumnWidth, width)),
            };

            SpreadsheetDataGrid.Columns.Add(gridColumn);
            TrackSpreadsheetColumnWidth(gridColumn);
        }

        SpreadsheetDataGrid.Items.Refresh();
        isApplyingSpreadsheetLayout = false;
    }

    private void RefreshSpreadsheetFromText(bool rebuildTable = true)
    {
        if (isSyncingTextFromSpreadsheet)
            return;

        EditTextTableDocument? existingDocument = tableDocument;
        tableDocument = EditTextTableDocument.CreateFromText(
            PassedTextControl.Text,
            existingDocument?.MinimumRowCount ?? EditTextTableDocument.DefaultMinimumRowCount,
            existingDocument?.MinimumColumnCount ?? EditTextTableDocument.DefaultMinimumColumnCount);

        if (existingDocument is not null)
            tableDocument.ApplyViewMetricsFrom(existingDocument);

        if (rebuildTable)
            RebuildSpreadsheetTable();
        UpdateLineAndColumnText();
    }

    private void RefreshMarkdownFromText()
    {
        if (isSyncingTextFromMarkdown)
            return;

        LoadMarkdownDocumentFromText(PassedTextControl.Text);
        UpdateLineAndColumnText();
    }

    private void LoadMarkdownDocumentFromText(string? markdownText)
    {
        isApplyingMarkdownDocument = true;
        MarkdownEditorControl.Document = MarkdownDocumentUtilities.CreateFlowDocument(
            markdownText,
            MarkdownEditorControl.FontFamily,
            MarkdownEditorControl.FontSize);
        ApplyMarkdownTheme();
        ApplyMarkdownWrapSetting();
        SetMargins(MarginsMenuItem.IsChecked is true);
        isApplyingMarkdownDocument = false;
    }

    private void SyncMarkdownTextFromDocument()
    {
        if (MarkdownEditorControl.Document is null)
            return;

        isSyncingTextFromMarkdown = true;
        PassedTextControl.Text = MarkdownDocumentUtilities.SerializeToMarkdown(
            MarkdownEditorControl.Document,
            preserveLiteralMarkdown: true);
        isSyncingTextFromMarkdown = false;
    }

    private void ApplyMarkdownTheme()
    {
        if (MarkdownEditorControl.Document is null)
            return;

        MarkdownDocumentUtilities.ApplyTheme(
            MarkdownEditorControl.Document,
            this,
            SystemThemeUtility.IsLightTheme());
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
        LoadMarkdownDocumentFromText(PassedTextControl.Text);

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

    private void SetEditorMode(EtwEditorMode mode)
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
            if (mode == EtwEditorMode.Markdown)
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

            LoadMarkdownDocumentFromText(PassedTextControl.Text);
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
    }

    private void SpreadsheetDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (isRestoringSpreadsheetUndoState)
            return;

        pendingSpreadsheetUndoState = CreateCurrentSpreadsheetUndoState(syncFromTable: true);
    }

    private void SpreadsheetDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Cancel)
        {
            pendingSpreadsheetUndoState = null;
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                CaptureCommittedSpreadsheetEditIfPending();
                UpdateLineAndColumnText();
            },
            DispatcherPriority.Background);
    }

    private void SpreadsheetDataGrid_CurrentCellChanged(object sender, EventArgs e)
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            UpdateLineAndColumnText();
    }

    private void SpreadsheetDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        int rowIndex = e.Row.GetIndex();
        e.Row.Header = (rowIndex + 1).ToString(CultureInfo.InvariantCulture);
        e.Row.SizeChanged -= SpreadsheetRow_SizeChanged;
        e.Row.SizeChanged += SpreadsheetRow_SizeChanged;

        double? rowHeight = tableDocument?.RowHeights.ElementAtOrDefault(rowIndex);
        if (rowHeight.HasValue)
            e.Row.Height = rowHeight.Value;
        else
            e.Row.ClearValue(HeightProperty);
    }

    private void SpreadsheetDataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            if (!ShouldHandleSpreadsheetDeleteKey(SpreadsheetDataGrid.SelectedCells.Count, IsSpreadsheetCellEditorFocused()))
                return;

            e.Handled = true;
            ClearSelectedSpreadsheetCellValues();
            return;
        }

        if (e.Key == Key.C
            && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            && !IsSpreadsheetCellEditorFocused())
        {
            e.Handled = true;
            _ = TryCopySpreadsheetSelectionToClipboard(GetSelectedSpreadsheetCellCoordinates());
            return;
        }

        if (e.Key == Key.X
            && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            && !IsSpreadsheetCellEditorFocused())
        {
            e.Handled = true;
            _ = TryCutSelectedSpreadsheetCellValues();
            return;
        }

        if (e.Key == Key.V
            && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            && !IsSpreadsheetCellEditorFocused())
        {
            e.Handled = true;
            PasteIntoSpreadsheet();
            return;
        }

    }

    private void SpreadsheetDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        spreadsheetContextRowIndex = null;
        spreadsheetContextColumnIndex = null;

        if (FindVisualParent<Thumb>(e.OriginalSource as DependencyObject) is not null)
            return;

        if (FindVisualParent<DataGridColumnHeader>(e.OriginalSource as DependencyObject) is DataGridColumnHeader columnHeader
            && columnHeader.Column is DataGridColumn dataGridColumn)
        {
            SelectSpreadsheetColumn(dataGridColumn.DisplayIndex);
            e.Handled = true;
            return;
        }

        if (FindVisualParent<DataGridRowHeader>(e.OriginalSource as DependencyObject) is DataGridRowHeader rowHeader
            && rowHeader.DataContext is not null)
        {
            SelectSpreadsheetRow(rowHeader.DataContext);
            e.Handled = true;
        }
    }

    private void SpreadsheetDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        spreadsheetContextRowIndex = null;
        spreadsheetContextColumnIndex = null;

        if (FindVisualParent<Thumb>(e.OriginalSource as DependencyObject) is not null)
            return;

        if (FindVisualParent<DataGridColumnHeader>(e.OriginalSource as DependencyObject) is DataGridColumnHeader columnHeader
            && columnHeader.Column is DataGridColumn dataGridColumn)
        {
            spreadsheetContextColumnIndex = dataGridColumn.DisplayIndex;
            SelectSpreadsheetColumn(dataGridColumn.DisplayIndex);
            SpreadsheetDataGrid.ContextMenu = FindResource("SpreadsheetColumnHeaderContextMenu") as ContextMenu;
            return;
        }

        if (FindVisualParent<DataGridRowHeader>(e.OriginalSource as DependencyObject) is DataGridRowHeader rowHeader
            && rowHeader.DataContext is not null)
        {
            spreadsheetContextRowIndex = SpreadsheetDataGrid.Items.IndexOf(rowHeader.DataContext);
            SelectSpreadsheetRow(rowHeader.DataContext);
            SpreadsheetDataGrid.ContextMenu = FindResource("SpreadsheetRowHeaderContextMenu") as ContextMenu;
            return;
        }

        if (FindVisualParent<System.Windows.Controls.DataGridCell>(e.OriginalSource as DependencyObject) is System.Windows.Controls.DataGridCell dataGridCell
            && dataGridCell.DataContext is not null
            && dataGridCell.Column is DataGridColumn clickedCellColumn)
        {
            spreadsheetContextRowIndex = SpreadsheetDataGrid.Items.IndexOf(dataGridCell.DataContext);
            spreadsheetContextColumnIndex = clickedCellColumn.DisplayIndex;

            bool isCellAlreadySelected = GetSelectedSpreadsheetCellCoordinates().Contains((spreadsheetContextRowIndex.Value, spreadsheetContextColumnIndex.Value));
            SelectSpreadsheetCell(dataGridCell.DataContext, clickedCellColumn, clearExistingSelection: !isCellAlreadySelected);
            SpreadsheetDataGrid.ContextMenu = FindResource("SpreadsheetContextMenu") as ContextMenu;
            return;
        }

        SpreadsheetDataGrid.ContextMenu = FindResource("SpreadsheetContextMenu") as ContextMenu;
    }

    private void SpreadsheetDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        UpdateSelectedSpreadsheetCellCoordinates();

        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            UpdateLineAndColumnText();

            if (CalcResultsTextControl.Visibility == Visibility.Visible)
            {
                UpdateCalcPaneTextDisplay();
                UpdateAggregateStatusDisplay();
            }
        }
    }

    private void ClearSelectedSpreadsheetCellValues()
    {
        ClearSpreadsheetCellValuesAndSync(GetSelectedSpreadsheetCellCoordinates());
    }

    internal static void ClearSpreadsheetCellValues(DataTable dataTable, IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        foreach ((int rowIndex, int columnIndex) in cellCoordinates.Distinct())
        {
            if (rowIndex < 0
                || rowIndex >= dataTable.Rows.Count
                || columnIndex < 0
                || columnIndex >= dataTable.Columns.Count)
            {
                continue;
            }

            dataTable.Rows[rowIndex][columnIndex] = string.Empty;
        }
    }

    internal static bool TryCutSpreadsheetCellValues(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates,
        Func<string, bool> trySetClipboardText)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(cellCoordinates);
        ArgumentNullException.ThrowIfNull(trySetClipboardText);

        string selectionText = BuildSpreadsheetSelectionText(dataTable, cellCoordinates);
        if (string.IsNullOrEmpty(selectionText) || !trySetClipboardText(selectionText))
            return false;

        ClearSpreadsheetCellValues(dataTable, cellCoordinates);
        return true;
    }

    private void PasteIntoSpreadsheet()
    {
        string clipboardText;
        try
        {
            if (!ClipboardUtilities.TryGetHtmlTableAsTabSeparated(out clipboardText))
                clipboardText = System.Windows.Clipboard.GetText();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PasteIntoSpreadsheet: clipboard read failed. {ex.Message}");
            return;
        }

        if (string.IsNullOrEmpty(clipboardText))
            return;

        if (AppUtilities.TextGrabSettings.EtwNormalizeLineEndingsOnPaste)
            clipboardText = NormalizeLineEndings(clipboardText);

        int startRow = Math.Max(0, SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem));
        int startCol = Math.Max(0, SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0);

        // Parse clipboard text into a 2D array of cell values
        string[] lines = clipboardText.Split('\n');
        List<string[]> pastedRows = [];
        foreach (string line in lines)
            pastedRows.Add(line.TrimEnd('\r').Split('\t'));

        // Remove trailing empty row artifact produced by a final newline in copied table text
        while (pastedRows.Count > 1 && pastedRows[^1].Length == 1 && pastedRows[^1][0].Length == 0)
            pastedRows.RemoveAt(pastedRows.Count - 1);

        if (pastedRows.Count == 0)
            return;

        int maxPastedCols = pastedRows.Max(row => row.Length);

        ApplySpreadsheetDocumentChange(document =>
        {
            // Expand the document to fit the pasted data if necessary
            int requiredRows = startRow + pastedRows.Count;
            int requiredCols = startCol + maxPastedCols;
            document.RowCount = Math.Max(document.RowCount, requiredRows);
            document.ColumnCount = Math.Max(document.ColumnCount, requiredCols);
            document.MinimumRowCount = Math.Max(document.MinimumRowCount, requiredRows);
            document.MinimumColumnCount = Math.Max(document.MinimumColumnCount, requiredCols);
            document.EnsureMinimumSize();

            // Write values into the target cells
            for (int r = 0; r < pastedRows.Count; r++)
            {
                int targetRow = startRow + r;
                for (int c = 0; c < pastedRows[r].Length; c++)
                {
                    int targetCol = startCol + c;
                    if (targetRow < document.Rows.Count && targetCol < document.Rows[targetRow].Count)
                        document.Rows[targetRow][targetCol] = pastedRows[r][c];
                }
            }
        }, startRow, startCol);
    }

    internal static string BuildSpreadsheetSelectionText(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        List<(int RowIndex, int ColumnIndex)> validCoordinates = [.. cellCoordinates
            .Distinct()
            .Where(cell => cell.RowIndex >= 0
                && cell.RowIndex < dataTable.Rows.Count
                && cell.ColumnIndex >= 0
                && cell.ColumnIndex < dataTable.Columns.Count)];

        if (validCoordinates.Count == 0)
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            validCoordinates
                .GroupBy(cell => cell.RowIndex)
                .OrderBy(group => group.Key)
                .Select(group => string.Join(
                    "\t",
                    group.OrderBy(cell => cell.ColumnIndex)
                        .Select(cell => dataTable.Rows[cell.RowIndex][cell.ColumnIndex]?.ToString() ?? string.Empty))));
    }

    internal static List<double> ExtractSpreadsheetSelectionNumbers(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        List<double> numbers = [];

        foreach ((int rowIndex, int columnIndex) in cellCoordinates.Distinct())
        {
            if (rowIndex < 0
                || rowIndex >= dataTable.Rows.Count
                || columnIndex < 0
                || columnIndex >= dataTable.Columns.Count)
            {
                continue;
            }

            string cellText = dataTable.Rows[rowIndex][columnIndex]?.ToString() ?? string.Empty;
            if (NumericUtilities.TryExtractFirstDouble(cellText, out double number))
                numbers.Add(number);
        }

        return numbers;
    }

    internal static string BuildSpreadsheetSelectionNumbersPreviewText(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        return string.Join(
            Environment.NewLine,
            ExtractSpreadsheetSelectionNumbers(dataTable, cellCoordinates)
                .Select(NumericUtilities.FormatNumber));
    }

    internal static List<(int RowIndex, int ColumnIndex)> GetSelectedOrPopulatedSpreadsheetCellCoordinates(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> selectedCellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(selectedCellCoordinates);

        List<(int RowIndex, int ColumnIndex)> validSelectedCoordinates = [.. selectedCellCoordinates
            .Distinct()
            .Where(cell => cell.RowIndex >= 0
                && cell.RowIndex < dataTable.Rows.Count
                && cell.ColumnIndex >= 0
                && cell.ColumnIndex < dataTable.Columns.Count)];

        if (validSelectedCoordinates.Count > 0)
            return validSelectedCoordinates;

        List<(int RowIndex, int ColumnIndex)> populatedCoordinates = [];

        for (int rowIndex = 0; rowIndex < dataTable.Rows.Count; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < dataTable.Columns.Count; columnIndex++)
            {
                string cellValue = dataTable.Rows[rowIndex][columnIndex]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(cellValue))
                    populatedCoordinates.Add((rowIndex, columnIndex));
            }
        }

        return populatedCoordinates;
    }

    internal static void TransformSpreadsheetDocumentCellValues(
        EditTextTableDocument document,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates,
        Func<string, string> transform)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cellCoordinates);
        ArgumentNullException.ThrowIfNull(transform);

        document.EnsureMinimumSize();

        foreach ((int rowIndex, int columnIndex) in cellCoordinates.Distinct())
        {
            if (rowIndex < 0
                || rowIndex >= document.Rows.Count
                || columnIndex < 0
                || document.Rows[rowIndex] is null
                || columnIndex >= document.Rows[rowIndex].Count)
            {
                continue;
            }

            string updatedValue = transform(document.Rows[rowIndex][columnIndex] ?? string.Empty);
            ArgumentNullException.ThrowIfNull(updatedValue);
            document.Rows[rowIndex][columnIndex] = updatedValue;
        }
    }

    internal static void SetSpreadsheetDocumentCellValues(
        EditTextTableDocument document,
        IEnumerable<(int RowIndex, int ColumnIndex, string Value)> cellValues)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cellValues);

        document.EnsureMinimumSize();

        foreach ((int rowIndex, int columnIndex, string value) in cellValues.Distinct())
        {
            if (rowIndex < 0
                || rowIndex >= document.Rows.Count
                || columnIndex < 0
                || document.Rows[rowIndex] is null
                || columnIndex >= document.Rows[rowIndex].Count)
            {
                continue;
            }

            document.Rows[rowIndex][columnIndex] = value ?? string.Empty;
        }
    }

    internal static bool AreSpreadsheetDocumentCellsWrapped(
        EditTextTableDocument document,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        document.EnsureMinimumSize();

        List<(int RowIndex, int ColumnIndex)> validCoordinates = [.. cellCoordinates
            .Distinct()
            .Where(cell => cell.RowIndex >= 0
                && cell.RowIndex < document.Rows.Count
                && cell.ColumnIndex >= 0
                && cell.ColumnIndex < document.ColumnNames.Count)];

        return validCoordinates.Count > 0
            && validCoordinates.All(cell => document.IsCellWrapped(cell.RowIndex, cell.ColumnIndex));
    }

    internal static void SetSpreadsheetDocumentCellWrapState(
        EditTextTableDocument document,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates,
        bool shouldWrap)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        document.EnsureMinimumSize();

        foreach ((int rowIndex, int columnIndex) in cellCoordinates.Distinct())
            document.SetCellWrap(rowIndex, columnIndex, shouldWrap);
    }

    internal static void ClearSpreadsheetDocumentRowHeights(EditTextTableDocument document, IEnumerable<int> rowIndices)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(rowIndices);

        document.EnsureMinimumSize();

        foreach (int rowIndex in rowIndices.Distinct())
            document.SetRowHeight(rowIndex, null);
    }

    internal static double? GetSpreadsheetPersistedRowHeight(double rowHeight)
    {
        if (double.IsNaN(rowHeight) || double.IsInfinity(rowHeight) || rowHeight <= 0)
            return null;

        return rowHeight;
    }

    private void UpdateSelectedSpreadsheetCellCoordinates()
    {
        selectedSpreadsheetCellCoordinates = [.. SpreadsheetDataGrid.SelectedCells
            .Select(cell => (
                RowIndex: SpreadsheetDataGrid.Items.IndexOf(cell.Item),
                ColumnIndex: cell.Column?.DisplayIndex ?? -1))
            .Where(cell => cell.RowIndex >= 0 && cell.ColumnIndex >= 0)
            .Distinct()];
    }

    private List<(int RowIndex, int ColumnIndex)> GetSelectedSpreadsheetCellCoordinates()
    {
        return [.. selectedSpreadsheetCellCoordinates];
    }

    private List<(int RowIndex, int ColumnIndex)> GetSelectedOrCurrentSpreadsheetCellCoordinates()
    {
        List<(int RowIndex, int ColumnIndex)> selectedCells = GetSelectedSpreadsheetCellCoordinates();
        if (selectedCells.Count > 0)
            return selectedCells;

        int rowIndex = spreadsheetContextRowIndex ?? SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        int columnIndex = spreadsheetContextColumnIndex ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? -1;

        if (rowIndex < 0 || columnIndex < 0)
            return [];

        return [(rowIndex, columnIndex)];
    }

    private List<(int RowIndex, int ColumnIndex)> GetSelectedOrPopulatedSpreadsheetCellCoordinates()
    {
        return GetSelectedOrPopulatedSpreadsheetCellCoordinates(spreadsheetTable, GetSelectedSpreadsheetCellCoordinates());
    }

    private IEnumerable<string> GetSelectedOrPopulatedSpreadsheetCellTexts()
    {
        foreach ((int rowIndex, int columnIndex) in GetSelectedOrPopulatedSpreadsheetCellCoordinates())
            yield return spreadsheetTable.Rows[rowIndex][columnIndex]?.ToString() ?? string.Empty;
    }

    private bool TryApplySpreadsheetTextTransform(Func<string, string> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (editorMode != EtwEditorMode.Spreadsheet)
            return false;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        EnsureSpreadsheetDocumentFromText();

        if (tableDocument is null)
            return true;

        List<(int RowIndex, int ColumnIndex)> targetCells = GetSelectedOrPopulatedSpreadsheetCellCoordinates();
        if (targetCells.Count == 0)
            return true;

        int focusRow = Math.Max(0, SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem));
        int focusColumn = Math.Max(0, SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0);

        ApplySpreadsheetDocumentChange(
            document => TransformSpreadsheetDocumentCellValues(document, targetCells, transform),
            focusRow,
            focusColumn);
        UpdateLineAndColumnText();
        return true;
    }

    private async Task<bool> TryApplySpreadsheetTextTransformAsync(Func<string, Task<string>> transformAsync)
    {
        ArgumentNullException.ThrowIfNull(transformAsync);

        if (editorMode != EtwEditorMode.Spreadsheet)
            return false;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        EnsureSpreadsheetDocumentFromText();

        if (tableDocument is null)
            return true;

        List<(int RowIndex, int ColumnIndex)> targetCells = GetSelectedOrPopulatedSpreadsheetCellCoordinates();
        if (targetCells.Count == 0)
            return true;

        int focusRow = Math.Max(0, SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem));
        int focusColumn = Math.Max(0, SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0);
        List<(int RowIndex, int ColumnIndex, string Value)> transformedCells = [];

        foreach ((int rowIndex, int columnIndex) in targetCells)
        {
            if (rowIndex < 0
                || rowIndex >= tableDocument.Rows.Count
                || columnIndex < 0
                || columnIndex >= tableDocument.Rows[rowIndex].Count)
            {
                continue;
            }

            string updatedValue = await transformAsync(tableDocument.Rows[rowIndex][columnIndex] ?? string.Empty);
            ArgumentNullException.ThrowIfNull(updatedValue);
            transformedCells.Add((rowIndex, columnIndex, updatedValue));
        }

        ApplySpreadsheetDocumentChange(
            document => SetSpreadsheetDocumentCellValues(document, transformedCells),
            focusRow,
            focusColumn);
        UpdateLineAndColumnText();
        return true;
    }

    private void SpreadsheetUndoCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        e.CanExecute = spreadsheetUndoHistory.CanUndo;
        e.Handled = true;
    }

    private void SpreadsheetCopyCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        e.CanExecute = GetSelectedSpreadsheetCellCoordinates().Count > 0;
        e.Handled = true;
    }

    private void SpreadsheetPasteCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        e.CanExecute = true;
        e.Handled = true;
    }

    private void SpreadsheetRedoCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        e.CanExecute = spreadsheetUndoHistory.CanRedo;
        e.Handled = true;
    }

    private void SpreadsheetUndoExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        SpreadsheetUndoState? previousState = spreadsheetUndoHistory.Undo(CreateCurrentSpreadsheetUndoState(syncFromTable: true));
        if (previousState is null)
            return;

        RestoreSpreadsheetUndoState(previousState);
        CommandManager.InvalidateRequerySuggested();
        e.Handled = true;
    }

    private void SpreadsheetRedoExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        SpreadsheetUndoState? nextState = spreadsheetUndoHistory.Redo(CreateCurrentSpreadsheetUndoState(syncFromTable: true));
        if (nextState is null)
            return;

        RestoreSpreadsheetUndoState(nextState);
        CommandManager.InvalidateRequerySuggested();
        e.Handled = true;
    }

    private void SpreadsheetCopyExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        _ = TryCopySpreadsheetSelectionToClipboard(GetSelectedSpreadsheetCellCoordinates());
        e.Handled = true;
    }

    private void SpreadsheetCutExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        _ = TryCutSelectedSpreadsheetCellValues();
        e.Handled = true;
    }

    private void SpreadsheetPasteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        PasteIntoSpreadsheet();
        e.Handled = true;
    }

    private bool IsSpreadsheetCellEditorFocused()
    {
        if (Keyboard.FocusedElement is not DependencyObject focusedElement)
            return false;

        return FindVisualParent<System.Windows.Controls.DataGridCell>(focusedElement) is not null
            && FindVisualParent<System.Windows.Controls.TextBox>(focusedElement) is not null;
    }

    internal static bool ShouldHandleSpreadsheetDeleteKey(int selectedCellCount, bool isCellEditorFocused)
    {
        return !isCellEditorFocused && selectedCellCount > 0;
    }

    private void SpreadsheetColumnWidthChanged(object? sender, EventArgs e)
    {
        if (isApplyingSpreadsheetLayout || tableDocument is null || sender is not DataGridColumn column)
            return;

        int columnIndex = SpreadsheetDataGrid.Columns.IndexOf(column);
        if (columnIndex < 0)
            return;

        double width = column.ActualWidth > 0 ? column.ActualWidth : column.Width.DisplayValue;
        tableDocument.SetColumnWidth(columnIndex, width);
    }

    private void SpreadsheetRow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (isApplyingSpreadsheetLayout || tableDocument is null || sender is not DataGridRow row)
            return;

        int rowIndex = row.GetIndex();
        if (rowIndex < 0)
            return;

        double? height = GetSpreadsheetPersistedRowHeight(row.Height);
        if (!height.HasValue)
            return;

        tableDocument.SetRowHeight(rowIndex, height.Value);
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

    private void EnterSpreadsheetMode_Click(object sender, RoutedEventArgs e) => SetEditorMode(EtwEditorMode.Spreadsheet);

    private void EnterMarkdownMode_Click(object sender, RoutedEventArgs e) => SetEditorMode(EtwEditorMode.Markdown);

    private void ToggleAlwaysOnTop_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(AlwaysOnTop, AlwaysOnTop_Checked);

    private void ToggleHideBottomBar_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(HideBottomBarMenuItem, HideBottomBarMenuItem_Click);

    private void ToggleLaunchFullscreenOnLoad_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(LaunchFullscreenOnLoad, LaunchFullscreenOnLoad_Click);

    private void ToggleRestorePosition_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(RestorePositionMenuItem, RestorePositionMenuItem_Checked);

    private void ToggleMargins_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(MarginsMenuItem, MarginsMenuItem_Checked);

    private void ToggleWrapText_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(WrapTextMenuItem, WrapTextCHBX_Checked);

    private void ToggleShowMathErrors_Click(object sender, RoutedEventArgs e) => ToggleMenuItem(ShowErrorsMenuItem, ShowErrorsMenuItem_Click);

    private void ToggleWriteTxtFileForEachImage_Click(object sender, RoutedEventArgs e)
    {
        ReadFolderOfImagesWriteTxtFiles.IsChecked = !ReadFolderOfImagesWriteTxtFiles.IsChecked;
    }

    private void SyncSpreadsheetDocumentFromTable(bool writeText = true)
    {
        tableDocument ??= EditTextTableDocument.CreateFromText(PassedTextControl.Text);

        tableDocument.ColumnNames = [.. spreadsheetTable.Columns
            .Cast<DataColumn>()
            .Select(column => column.ColumnName)];

        tableDocument.Rows = [.. spreadsheetTable.Rows
            .Cast<DataRow>()
            .Select(row => spreadsheetTable.Columns
                .Cast<DataColumn>()
                .Select(column => row[column]?.ToString() ?? string.Empty)
                .ToList())];

        int furthestNonEmptyRowIndex = -1;
        int furthestNonEmptyColumnIndex = -1;

        for (int rowIndex = 0; rowIndex < tableDocument.Rows.Count; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < tableDocument.Rows[rowIndex].Count; columnIndex++)
            {
                if (string.IsNullOrWhiteSpace(tableDocument.Rows[rowIndex][columnIndex]))
                    continue;

                furthestNonEmptyRowIndex = Math.Max(furthestNonEmptyRowIndex, rowIndex);
                furthestNonEmptyColumnIndex = Math.Max(furthestNonEmptyColumnIndex, columnIndex);
            }
        }

        tableDocument.RowCount = Math.Max(tableDocument.RowCount, furthestNonEmptyRowIndex + 1);
        tableDocument.ColumnCount = Math.Max(tableDocument.ColumnCount, furthestNonEmptyColumnIndex + 1);
        tableDocument.MinimumColumnCount = Math.Max(tableDocument.MinimumColumnCount, spreadsheetTable.Columns.Count);
        tableDocument.MinimumRowCount = Math.Max(tableDocument.MinimumRowCount, spreadsheetTable.Rows.Count);
        CaptureSpreadsheetLayoutMetrics();
        tableDocument.EnsureMinimumSize();

        if (writeText)
            UpdateTextFromSpreadsheetDocument();
    }

    private void CaptureSpreadsheetLayoutMetrics()
    {
        if (tableDocument is null)
            return;

        for (int columnIndex = 0; columnIndex < SpreadsheetDataGrid.Columns.Count; columnIndex++)
        {
            DataGridColumn column = SpreadsheetDataGrid.Columns[columnIndex];
            double width = column.ActualWidth > 0 ? column.ActualWidth : column.Width.DisplayValue;
            tableDocument.SetColumnWidth(columnIndex, width);
        }

        foreach (object item in SpreadsheetDataGrid.Items)
        {
            if (item == CollectionView.NewItemPlaceholder)
                continue;

            if (SpreadsheetDataGrid.ItemContainerGenerator.ContainerFromItem(item) is not DataGridRow row)
                continue;

            int rowIndex = row.GetIndex();
            if (rowIndex < 0)
                continue;

            double? height = GetSpreadsheetPersistedRowHeight(row.Height);
            tableDocument.SetRowHeight(rowIndex, height);
        }
    }

    private void DetachSpreadsheetColumnWidthTracking()
    {
        foreach (DataGridColumn column in trackedSpreadsheetColumns)
            DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn))?.RemoveValueChanged(column, SpreadsheetColumnWidthChanged);

        trackedSpreadsheetColumns.Clear();
    }

    private void TrackSpreadsheetColumnWidth(DataGridColumn column)
    {
        trackedSpreadsheetColumns.Add(column);
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn))?.AddValueChanged(column, SpreadsheetColumnWidthChanged);
    }

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

    private bool TryCopySpreadsheetSelectionToClipboard(IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        string selectionText = BuildSpreadsheetSelectionText(spreadsheetTable, cellCoordinates);
        return !string.IsNullOrEmpty(selectionText) && TrySetClipboardText(selectionText);
    }

    private void ClearSpreadsheetCellValuesAndSync(IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        List<(int RowIndex, int ColumnIndex)> targetCellCoordinates = [.. cellCoordinates];
        if (targetCellCoordinates.Count == 0)
            return;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        SpreadsheetUndoState? beforeChange = CreateCurrentSpreadsheetUndoState(syncFromTable: true);

        ClearSpreadsheetCellValues(spreadsheetTable, targetCellCoordinates);
        SyncSpreadsheetDocumentFromTable();
        RecordSpreadsheetUndoChange(beforeChange, CreateCurrentSpreadsheetUndoState(syncFromTable: false));
        UpdateLineAndColumnText();
    }

    private bool TryCutSelectedSpreadsheetCellValues()
    {
        List<(int RowIndex, int ColumnIndex)> selectedCellCoordinates = GetSelectedSpreadsheetCellCoordinates();
        if (selectedCellCoordinates.Count == 0)
            return false;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        SpreadsheetUndoState? beforeChange = CreateCurrentSpreadsheetUndoState(syncFromTable: true);

        if (!TryCutSpreadsheetCellValues(spreadsheetTable, selectedCellCoordinates, TrySetClipboardText))
            return false;

        SyncSpreadsheetDocumentFromTable();
        RecordSpreadsheetUndoChange(beforeChange, CreateCurrentSpreadsheetUndoState(syncFromTable: false));
        UpdateLineAndColumnText();
        return true;
    }

    private void UpdateSpreadsheetModeUi()
    {
        bool isSpreadsheetMode = editorMode == EtwEditorMode.Spreadsheet;
        bool isMarkdownMode = editorMode == EtwEditorMode.Markdown;

        AddSpreadsheetRowButton.Visibility = isSpreadsheetMode ? Visibility.Visible : Visibility.Collapsed;
        AddSpreadsheetColumnButton.Visibility = isSpreadsheetMode ? Visibility.Visible : Visibility.Collapsed;
        AddSpreadsheetRowMenuItem.Visibility = isSpreadsheetMode ? Visibility.Visible : Visibility.Collapsed;
        AddSpreadsheetColumnMenuItem.Visibility = isSpreadsheetMode ? Visibility.Visible : Visibility.Collapsed;
        RawTextModeMenuItem.IsChecked = editorMode == EtwEditorMode.Text;
        SpreadsheetModeMenuItem.IsChecked = isSpreadsheetMode;
        MarkdownModeMenuItem.IsChecked = isMarkdownMode;
        CommandManager.InvalidateRequerySuggested();
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

    private System.Windows.Data.Binding CreateSpreadsheetCellTextWrappingBinding(int columnIndex)
    {
        return new System.Windows.Data.Binding
        {
            Converter = new SpreadsheetCellTextWrappingConverter(this, columnIndex),
            Mode = BindingMode.OneWay
        };
    }

    private Style CreateSpreadsheetDisplayTextStyle(int columnIndex)
    {
        Style style = new(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, CreateSpreadsheetCellTextWrappingBinding(columnIndex)));
        style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Top));
        return style;
    }

    private Style CreateSpreadsheetEditingTextStyle(int columnIndex)
    {
        Style style = new(typeof(System.Windows.Controls.TextBox));
        style.Setters.Add(new Setter(System.Windows.Controls.TextBox.TextWrappingProperty, CreateSpreadsheetCellTextWrappingBinding(columnIndex)));
        style.Setters.Add(new Setter(System.Windows.Controls.TextBox.AcceptsReturnProperty, false));
        style.Setters.Add(new Setter(System.Windows.Controls.TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Top));
        return style;
    }

    private TextWrapping GetSpreadsheetCellTextWrapping(object? rowItem, int columnIndex)
    {
        if (tableDocument is null || rowItem is not DataRowView dataRowView)
            return TextWrapping.NoWrap;

        int rowIndex = dataRowView.Row.Table.Rows.IndexOf(dataRowView.Row);
        if (rowIndex < 0)
            return TextWrapping.NoWrap;

        return tableDocument.IsCellWrapped(rowIndex, columnIndex)
            ? TextWrapping.Wrap
            : TextWrapping.NoWrap;
    }

    private static MenuItem? GetContextMenuItem(ContextMenu contextMenu, string itemTag)
    {
        return contextMenu.Items
            .OfType<MenuItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, itemTag, StringComparison.Ordinal));
    }

    private void SelectSpreadsheetColumn(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= SpreadsheetDataGrid.Columns.Count)
            return;

        SpreadsheetDataGrid.SelectedItems.Clear();
        SpreadsheetDataGrid.SelectedCells.Clear();

        DataGridColumn column = SpreadsheetDataGrid.Columns[columnIndex];
        object? firstRowItem = null;

        foreach (object item in SpreadsheetDataGrid.Items)
        {
            if (ReferenceEquals(item, CollectionView.NewItemPlaceholder))
                continue;

            firstRowItem ??= item;
            SpreadsheetDataGrid.SelectedCells.Add(new DataGridCellInfo(item, column));
        }

        if (firstRowItem is not null)
        {
            SpreadsheetDataGrid.CurrentCell = new DataGridCellInfo(firstRowItem, column);
            SpreadsheetDataGrid.ScrollIntoView(firstRowItem, column);
        }

        UpdateSelectedSpreadsheetCellCoordinates();
        SpreadsheetDataGrid.Focus();
        UpdateLineAndColumnText();
    }

    private void SelectSpreadsheetCell(object rowItem, DataGridColumn column, bool clearExistingSelection)
    {
        if (clearExistingSelection)
        {
            SpreadsheetDataGrid.SelectedItems.Clear();
            SpreadsheetDataGrid.SelectedCells.Clear();
        }

        SpreadsheetDataGrid.CurrentCell = new DataGridCellInfo(rowItem, column);

        if (!SpreadsheetDataGrid.SelectedCells.Any(cell => ReferenceEquals(cell.Item, rowItem) && cell.Column == column))
            SpreadsheetDataGrid.SelectedCells.Add(new DataGridCellInfo(rowItem, column));

        SpreadsheetDataGrid.ScrollIntoView(rowItem, column);
        UpdateSelectedSpreadsheetCellCoordinates();
        SpreadsheetDataGrid.Focus();
        UpdateLineAndColumnText();
    }

    private void SelectSpreadsheetRow(object rowItem)
    {
        SpreadsheetDataGrid.SelectedItems.Clear();
        SpreadsheetDataGrid.SelectedCells.Clear();

        if (SpreadsheetDataGrid.Columns.Count == 0)
            return;

        DataGridColumn firstColumn = SpreadsheetDataGrid.Columns[0];
        foreach (DataGridColumn column in SpreadsheetDataGrid.Columns)
            SpreadsheetDataGrid.SelectedCells.Add(new DataGridCellInfo(rowItem, column));

        SpreadsheetDataGrid.CurrentCell = new DataGridCellInfo(rowItem, firstColumn);
        SpreadsheetDataGrid.ScrollIntoView(rowItem, firstColumn);
        UpdateSelectedSpreadsheetCellCoordinates();
        SpreadsheetDataGrid.Focus();
        UpdateLineAndColumnText();
    }

    private void UpdateTextFromSpreadsheetDocument()
    {
        if (tableDocument is null)
            return;

        isSyncingTextFromSpreadsheet = true;
        PassedTextControl.Text = tableDocument.SerializeToText();
        isSyncingTextFromSpreadsheet = false;
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
            TextContent = PassedTextControl.Text,
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

    private static string GetSpreadsheetSaveExtension(EditTextTableDocument? tableDocument)
    {
        if (tableDocument is null)
            return ".tsv";

        return tableDocument.Format switch
        {
            EtwStructuredTextFormat.Csv => ".csv",
            EtwStructuredTextFormat.Tsv => ".tsv",
            EtwStructuredTextFormat.DelimitedText when string.Equals(tableDocument.Delimiter, ",", StringComparison.Ordinal) => ".csv",
            _ => ".tsv"
        };
    }

    internal void LimitNumberOfCharsPerLine(int numberOfChars, SpotInLine spotInLine)
    {
        PassedTextControl.Text = PassedTextControl.Text.LimitCharactersPerLine(numberOfChars, spotInLine);
    }

    internal async void OpenPath(string pathOfFileToOpen, bool isMultipleFiles = false)
    {
        ResetSpreadsheetUndoHistory();
        (string TextContent, OpenContentKind KindOpened) = await IoUtilities.GetContentFromPath(pathOfFileToOpen, isMultipleFiles, selectedILanguage);
        bool shouldTrackOpenedFile = KindOpened == OpenContentKind.TextFile && !isMultipleFiles;

        if (KindOpened == OpenContentKind.TextFile)
        {
            EtwEditorMode targetMode = isMultipleFiles
                ? EtwEditorMode.Text
                : IoUtilities.GetEditorModeForPath(pathOfFileToOpen);

            if (IsLoaded)
                SetEditorMode(targetMode);
            else
                editorMode = targetMode;
        }

        isLoadingOpenedFile = true;
        try
        {
            PassedTextControl.Text = TextContent;

            if (!IsLoaded)
                return;

            if (editorMode == EtwEditorMode.Spreadsheet)
                RefreshSpreadsheetFromText();
            else if (editorMode == EtwEditorMode.Markdown)
                RefreshMarkdownFromText();
        }
        finally
        {
            isLoadingOpenedFile = false;
            SyncTextFromActiveEditor();
            SetOpenedFileState(shouldTrackOpenedFile ? pathOfFileToOpen : null);
        }
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

    private void CaptureMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        LoadLanguageMenuItems(LanguageMenuItem);
        LoadGrabTemplateMenuItems(GrabTemplateMenuItem);
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

    internal IEnumerable<string> GetSelectedOrAllTextSegmentsForPreview()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            return GetSelectedOrPopulatedSpreadsheetCellTexts();

        return [GetSelectedTextOrAllText()];
    }

    public bool IsSpreadsheetMode => editorMode == EtwEditorMode.Spreadsheet;

    public void CommitSpreadsheetAndSync()
    {
        CommitSpreadsheetEditsAndCapturePendingHistory();
        SyncSpreadsheetDocumentFromTable(writeText: false);
    }

    public void NavigateToSpreadsheetCell(int rowIndex, int columnIndex)
    {
        Dispatcher.BeginInvoke(
            () => FocusSpreadsheetCell(rowIndex, columnIndex, beginEdit: false),
            DispatcherPriority.Background);
    }

    public List<FindResult> SearchSpreadsheetCells(Regex pattern)
    {
        if (tableDocument is null) return [];
        tableDocument.EnsureMinimumSize();
        List<FindResult> results = [];
        int count = 1;

        for (int row = 0; row < tableDocument.RowCount; row++)
        {
            List<string> rowData = tableDocument.Rows[row];
            for (int col = 0; col < tableDocument.ColumnCount; col++)
            {
                string cellValue = col < rowData.Count ? rowData[col] ?? string.Empty : string.Empty;
                foreach (Match m in pattern.Matches(cellValue))
                {
                    int previewStart = Math.Max(0, m.Index - 12);
                    int previewEnd = Math.Min(cellValue.Length, m.Index + m.Length + 12);
                    results.Add(new FindResult
                    {
                        RowIndex = row,
                        ColumnIndex = col,
                        Index = m.Index,
                        Text = TextSearchUtilities.FormatMatchTextForDisplay(m.Value),
                        PreviewLeft = cellValue[previewStart..m.Index],
                        PreviewRight = cellValue[(m.Index + m.Length)..previewEnd],
                        Length = m.Length,
                        Count = count++
                    });
                }
            }
        }
        return results;
    }

    public void ReplaceInSpreadsheetCells(
        IEnumerable<FindResult> targets,
        string replaceWith,
        Regex pattern)
    {
        CommitSpreadsheetEditsAndCapturePendingHistory();
        SyncSpreadsheetDocumentFromTable(writeText: false);

        if (tableDocument is null) return;

        SpreadsheetUndoState? beforeState = CreateCurrentSpreadsheetUndoState(syncFromTable: false);

        IEnumerable<(int RowIndex, int ColumnIndex, string Value)> updates = targets
            .Where(r => r.RowIndex.HasValue && r.ColumnIndex.HasValue)
            .GroupBy(r => (r.RowIndex!.Value, r.ColumnIndex!.Value))
            .Select(g =>
            {
                int row = g.Key.Item1, col = g.Key.Item2;
                string oldValue = row < tableDocument.Rows.Count && col < tableDocument.Rows[row].Count
                    ? tableDocument.Rows[row][col] ?? string.Empty : string.Empty;

                HashSet<int> indicesToReplace = [.. g.Select(r => r.Index)];
                string newValue = pattern.Replace(oldValue, m =>
                    indicesToReplace.Contains(m.Index) ? m.Result(replaceWith) : m.Value);

                return (RowIndex: row, ColumnIndex: col, Value: newValue);
            });

        SetSpreadsheetDocumentCellValues(tableDocument, updates);
        RebuildSpreadsheetTable();
        UpdateTextFromSpreadsheetDocument();
        RecordSpreadsheetUndoChange(beforeState, CreateCurrentSpreadsheetUndoState(syncFromTable: false));
    }

    private IEnumerable<string> GetSelectedOrAllTextSegmentsForEdit()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            return GetSelectedOrPopulatedSpreadsheetCellTexts();

        return [GetSelectedTextOrAllText()];
    }

    private void ReplaceSelectedTextOrAllText(string updatedText)
    {
        ArgumentNullException.ThrowIfNull(updatedText);

        if (PassedTextControl.SelectionLength == 0)
            PassedTextControl.Text = updatedText;
        else
            PassedTextControl.SelectedText = updatedText;
    }

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

    private void EditMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        // Load text-only templates fresh each time the Edit menu opens and use them to
        // populate both the whole-text and per-line apply submenus.
        List<GrabTemplate> textOnlyTemplates = [.. GrabTemplateManager.GetAllTemplates().Where(template => template.IsTextOnly && template.IsValid)];

        PopulateTemplateMenu(ApplyGrabTemplateMenuItem, textOnlyTemplates, ApplyGrabTemplateItem_Click);
        PopulateTemplateMenu(ApplyGrabTemplatePerLineMenuItem, textOnlyTemplates, ApplyGrabTemplatePerLineItem_Click);
    }

    private static void PopulateTemplateMenu(MenuItem parent, List<GrabTemplate> templates, RoutedEventHandler clickHandler)
    {
        parent.Items.Clear();

        if (templates.Count == 0)
        {
            parent.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (GrabTemplate template in templates)
        {
            MenuItem templateItem = new()
            {
                Header = template.Name,
                ToolTip = string.IsNullOrWhiteSpace(template.Description) ? null : template.Description,
                Tag = template,
            };
            templateItem.Click += clickHandler;
            parent.Items.Add(templateItem);
        }

        parent.Visibility = Visibility.Visible;
    }

    private void ApplyGrabTemplateItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: GrabTemplate template })
            return;

        ApplySelectedTextOrAllTextTransform(text => GrabTemplateExecutor.ApplyTextOnlyTemplate(template, text));
        GrabTemplateManager.RecordUsage(template.Id);
    }

    private void ApplyGrabTemplatePerLineItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: GrabTemplate template })
            return;

        ApplySelectedTextOrAllTextTransform(text => ApplyTemplatePerLine(template, text));
        GrabTemplateManager.RecordUsage(template.Id);
    }

    /// <summary>
    /// Splits <paramref name="text"/> into lines and applies the text-only template to each
    /// non-blank line independently, preserving blank lines and the overall line structure.
    /// </summary>
    private static string ApplyTemplatePerLine(GrabTemplate template, string text)
    {
        string[] lines = text.Split(Environment.NewLine);

        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                lines[i] = GrabTemplateExecutor.ApplyTextOnlyTemplate(template, lines[i]);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void GrabFrameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CheckForGrabFrameOrLaunch();
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
        CaptureLanguageUtilities.PersistSelectedLanguage(selectedILanguage);

        if (selectedILanguage is not GlobalLang)
        {
            SetCultureAndLanguageToDefault();
        }
        else
        {
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
        }

        foreach (object? child in BottomBarButtons.Children)
            if (child is LanguagePicker languagePicker)
                languagePicker.Select(selectedILanguage.LanguageTag);

        foreach (MenuItem menuItem in LanguageMenuItem.Items)
            menuItem.IsChecked = false;

        clickedMenuItem.IsChecked = true;
    }

    private void LoadGrabTemplateMenuItems(MenuItem grabTemplateMenuItem)
    {
        // Remember which template (if any) was previously selected
        GrabTemplate? previouslySelected = grabTemplateMenuItem.Items
            .OfType<MenuItem>()
            .FirstOrDefault(m => m.IsChecked && m.Tag is GrabTemplate)
            ?.Tag as GrabTemplate;

        grabTemplateMenuItem.Items.Clear();

        MenuItem noneItem = new()
        {
            Header = "(None)",
            IsCheckable = true,
            IsChecked = previouslySelected is null,
            StaysOpenOnClick = true,
        };
        noneItem.Click += GrabTemplateMenuItem_Click;
        grabTemplateMenuItem.Items.Add(noneItem);

        foreach (GrabTemplate template in GrabTemplateManager.GetAllTemplates())
        {
            MenuItem templateMenuItem = new()
            {
                Header = template.Name,
                IsCheckable = true,
                IsChecked = previouslySelected?.Id == template.Id,
                Tag = template,
                StaysOpenOnClick = true,
            };
            templateMenuItem.Click += GrabTemplateMenuItem_Click;
            grabTemplateMenuItem.Items.Add(templateMenuItem);
        }
    }

    private void GrabTemplateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem clickedItem)
            return;

        foreach (MenuItem item in GrabTemplateMenuItem.Items)
            item.IsChecked = false;

        clickedItem.IsChecked = true;
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

        bool usingTesseract = DefaultSettings.UseTesseract && TesseractHelper.CanLocateTesseractExe();
        List<ILanguage> availableLanguages = await CaptureLanguageUtilities.GetCaptureLanguagesAsync(usingTesseract);
        availableLanguages = [.. availableLanguages.Where(CaptureLanguageUtilities.IsStaticImageCompatible)];
        int selectedIndex = CaptureLanguageUtilities.FindPreferredLanguageIndex(
            availableLanguages,
            DefaultSettings.LastUsedLang,
            LanguageUtilities.GetOCRLanguage());

        for (int i = 0; i < availableLanguages.Count; i++)
        {
            ILanguage language = availableLanguages[i];
            MenuItem languageMenuItem = new()
            {
                Header = language.DisplayName,
                Tag = language,
                IsCheckable = true,
                IsChecked = i == selectedIndex,
                StaysOpenOnClick = true,
            };
            languageMenuItem.Click += LanguageMenuItem_Click;
            captureMenuItem.Items.Add(languageMenuItem);
        }
    }

    private void LoadRecentTextHistory()
    {
        List<HistoryInfo> grabsHistories = Singleton<HistoryService>.Instance.GetEditWindows();
        grabsHistories = [.. grabsHistories.OrderByDescending(x => x.CaptureDateTime)];

        ClearRecentTextMenuItems();

        if (grabsHistories.Count < 1)
        {
            OpenRecentMenuItem.IsEnabled = false;
            return;
        }

        OpenRecentMenuItem.IsEnabled = true;

        foreach (HistoryInfo history in grabsHistories)
        {
            MenuItem menuItem = new() { Tag = history.ID };
            menuItem.Click += RecentTextHistoryMenuItem_Click;

            if (PassedTextControl.Text == history.TextContent)
                menuItem.IsEnabled = false;

            string snippet = history.TextContent.Trim().Replace("\t", " ").MakeStringSingleLine().Truncate(40);
            menuItem.Header = $"{history.CaptureDateTime.Humanize().Trim()} | {snippet}";
            menuItem.Icon = new SymbolIcon
            {
                Symbol = history.EditorMode switch
                {
                    EtwEditorMode.Spreadsheet => SymbolRegular.Table24,
                    EtwEditorMode.Markdown => SymbolRegular.Markdown20,
                    _ => SymbolRegular.TextT24,
                }
            };
            OpenRecentMenuItem.Items.Add(menuItem);
        }
    }

    private void RecentTextHistoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string historyId)
            return;

        HistoryInfo? selectedHistory = Singleton<HistoryService>.Instance.GetTextHistoryById(historyId);

        if (selectedHistory is null)
        {
            menuItem.IsEnabled = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(PassedTextControl.Text))
        {
            ResetSpreadsheetUndoHistory();
            PassedTextControl.Text = selectedHistory.TextContent;
            tableDocument = EditTextTableDocument.TryDeserialize(selectedHistory.EditTextTableDocumentJson);
            editorMode = selectedHistory.EditorMode;
            SetEditorMode(editorMode);
            return;
        }

        EditTextWindow etw = new(selectedHistory);
        etw.Show();
    }

    private void ClearRecentTextMenuItems()
    {
        foreach (object item in OpenRecentMenuItem.Items)
        {
            if (item is MenuItem oldItem)
                oldItem.Click -= RecentTextHistoryMenuItem_Click;
        }
        OpenRecentMenuItem.Items.Clear();
    }

    private void ClearLanguageMenuItems()
    {
        foreach (object item in LanguageMenuItem.Items)
        {
            if (item is MenuItem oldItem)
                oldItem.Click -= LanguageMenuItem_Click;
        }
        LanguageMenuItem.Items.Clear();
    }

    private void ClearGrabTemplateMenuItems()
    {
        foreach (object item in GrabTemplateMenuItem.Items)
        {
            if (item is MenuItem oldItem)
                oldItem.Click -= GrabTemplateMenuItem_Click;
        }
        GrabTemplateMenuItem.Items.Clear();
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

    private static EditTextWindow CreateSelectionWindow(string selectedText, EtwEditorMode targetMode)
    {
        EditTextWindow selectionWindow = new(selectedText, false);
        if (targetMode != EtwEditorMode.Text)
            selectionWindow.SetEditorMode(targetMode);

        return selectionWindow;
    }

    private void OpenNewWindowWithSpreadsheetSelection()
    {
        CommitSpreadsheetEditsAndCapturePendingHistory();

        List<(int RowIndex, int ColumnIndex)> selectedCellCoordinates = GetSelectedSpreadsheetCellCoordinates();
        string selectedText = BuildSpreadsheetSelectionText(spreadsheetTable, selectedCellCoordinates);
        bool hasSelection = selectedCellCoordinates.Count > 0;

        EditTextWindow newEtwWithText = CreateSelectionWindow(
            selectedText,
            hasSelection ? EtwEditorMode.Spreadsheet : EtwEditorMode.Text);
        newEtwWithText.Show();
    }

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

        string selectedText = PassedTextControl.SelectedText;
        EditTextWindow newEtwWithText = CreateSelectionWindow(selectedText, EtwEditorMode.Text);
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
            Filter = FileUtilities.GetOpenDocumentFilter(),
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
        bool shouldSpellCheck = ShouldEnableSpellCheck(spellCheckMode, PassedTextControl.Text);

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
            ? MarkdownDocumentUtilities.GetDocumentPlainText(
                MarkdownDocumentUtilities.CreateFlowDocument(
                    pastedText,
                    MarkdownEditorControl.FontFamily,
                    MarkdownEditorControl.FontSize)).Length
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

    private void MarkdownEditorControl_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
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
            calculationResult = null;
            UpdateCalcPaneTextDisplay();
            _calculationService.ClearParameters();
            UpdateAggregateStatusDisplay();
            // Keep scrolls aligned even when clearing
            await Dispatcher.InvokeAsync(SyncCalcScrollToMain, DispatcherPriority.Render);
            return;
        }

        // Update calculation service settings
        _calculationService.CultureInfo = selectedCultureInfo ?? CultureInfo.CurrentCulture;
        _calculationService.ShowErrors = ShowErrorsMenuItem.IsChecked == true;

        // Evaluate expressions using the service
        calculationResult = await _calculationService.EvaluateExpressionsAsync(input);

        // Update the text control with results or current spreadsheet selection preview
        UpdateCalcPaneTextDisplay();

        // Update the aggregate status display if an aggregate is selected
        UpdateAggregateStatusDisplay();

        // After updating calc text, its ScrollViewer resets; resync to main scroll
        await Dispatcher.InvokeAsync(SyncCalcScrollToMain, DispatcherPriority.Render);

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

    private void RemoveDuplicateLines_Click(object sender, RoutedEventArgs e)
    {
        PassedTextControl.Text = PassedTextControl.Text.RemoveDuplicateLines();
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

    private void SyncTextFromActiveEditor()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            SyncSpreadsheetDocumentFromTable();
        else if (editorMode == EtwEditorMode.Markdown)
            SyncMarkdownTextFromDocument();
    }

    private bool SaveCurrentDocument(bool saveAs = false)
    {
        SyncTextFromActiveEditor();

        string fileText = PassedTextControl.Text;
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

        File.WriteAllText(targetFilePath, fileText);
        SetOpenedFileState(targetFilePath);
        return true;
    }

    private void SetOpenedFileState(string? openedFilePath)
    {
        OpenedFilePath = openedFilePath;
        savedFileText = string.IsNullOrWhiteSpace(openedFilePath) ? string.Empty : PassedTextControl.Text;
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

        hasPendingFileEdits = ShouldShowPendingFileEdits(OpenedFilePath, savedFileText, PassedTextControl.Text);
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
            && !string.IsNullOrWhiteSpace(PassedTextControl.Text))
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

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        LaunchFindAndReplace();
    }

    private void SelectAllMenuItem_Click(Object? sender = null, RoutedEventArgs? e = null)
    {
        if (!IsLoaded)
            return;

        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            SpreadsheetDataGrid.SelectAllCells();
            SpreadsheetDataGrid.Focus();
            return;
        }

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
        if (TrySelectSpreadsheetWord())
            return;

        (int wordStart, int wordLength) = PassedTextControl.Text.CursorWordBoundaries(PassedTextControl.CaretIndex);

        PassedTextControl.Select(wordStart, wordLength);
    }

    private bool TrySelectSpreadsheetWord()
    {
        if (editorMode != EtwEditorMode.Spreadsheet)
            return false;

        if (TryGetFocusedSpreadsheetCellEditor(out System.Windows.Controls.TextBox? focusedEditor))
        {
            (int editorWordStart, int editorWordLength) = focusedEditor.Text.CursorWordBoundaries(focusedEditor.CaretIndex);
            focusedEditor.Select(editorWordStart, editorWordLength);
            return true;
        }

        int rowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        int? columnIndex = SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex;
        if (rowIndex < 0
            || columnIndex is null
            || rowIndex >= spreadsheetTable.Rows.Count
            || columnIndex.Value < 0
            || columnIndex.Value >= spreadsheetTable.Columns.Count)
        {
            return true;
        }

        string cellText = spreadsheetTable.Rows[rowIndex][columnIndex.Value]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cellText))
            return true;

        (int wordStart, int wordLength) = cellText.CursorWordBoundaries(0);
        FocusSpreadsheetCell(rowIndex, columnIndex.Value);

        Dispatcher.BeginInvoke(
            () =>
            {
                if (TryGetFocusedSpreadsheetCellEditor(out System.Windows.Controls.TextBox? editor))
                    editor.Select(wordStart, wordLength);
            },
            DispatcherPriority.Background);

        return true;
    }

    private bool TryGetFocusedSpreadsheetCellEditor([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Windows.Controls.TextBox? editor)
    {
        editor = null;

        if (Keyboard.FocusedElement is not DependencyObject focusedElement)
            return false;

        if (FindVisualParent<System.Windows.Controls.DataGridCell>(focusedElement) is null)
            return false;

        editor = FindVisualParent<System.Windows.Controls.TextBox>(focusedElement);
        return editor is not null;
    }

    private void SelectWordMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SelectWord();
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
        if (string.IsNullOrEmpty(PassedTextControl.SelectedText))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private async void SplitOnSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string selectedText = PassedTextControl.SelectedText;

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

        StringBuilder textToManipulate = new(PassedTextControl.Text);

        textToManipulate = textToManipulate.Replace(selectedText, Environment.NewLine + selectedText);

        PassedTextControl.Text = textToManipulate.ToString();
    }

    private async void SplitAfterSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string selectedText = PassedTextControl.SelectedText;

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

        StringBuilder textToManipulate = new(PassedTextControl.Text);

        textToManipulate = textToManipulate.Replace(selectedText, selectedText + Environment.NewLine);

        PassedTextControl.Text = textToManipulate.ToString();
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

        if (PassedTextControl.SelectionLength == 0)
            PassedTextControl.Text = textToModify;
        else
            PassedTextControl.SelectedText = textToModify;
    }

    private void ToggleCaseCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        // Simplified: avoid scanning every character on each CanExecute query.
        // Any non-empty text segment is likely to contain letters.
        e.CanExecute = GetSelectedOrAllTextSegmentsForEdit().Any(text => text.Length > 0);
    }

    private void TrimEachLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        static string TrimEachLine(string workingString)
        {
            string[] stringSplit = workingString.Split(Environment.NewLine);
            string finalString = "";

            foreach (string line in stringSplit)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    finalString += line.Trim() + Environment.NewLine;
            }

            return finalString;
        }

        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            TryApplySpreadsheetTextTransform(TrimEachLine);
            return;
        }

        PassedTextControl.Text = TrimEachLine(PassedTextControl.Text);
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
        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            HideSelectionSpecificUi();

            int rowCount = spreadsheetTable.Rows.Count;
            int columnCount = spreadsheetTable.Columns.Count;

            if (SpreadsheetDataGrid.SelectedCells.Count == 0)
            {
                if (SpreadsheetDataGrid.CurrentCell.Column is not null)
                {
                    int currentRowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
                    int currentColumnIndex = SpreadsheetDataGrid.CurrentCell.Column.DisplayIndex;

                    BottomBarText.Text = currentRowIndex >= 0
                        ? $"Rows {rowCount}, Cols {columnCount}, Row {currentRowIndex + 1}, Col {currentColumnIndex + 1}"
                        : $"Rows {rowCount}, Cols {columnCount}";
                }
                else
                {
                    BottomBarText.Text = $"Rows {rowCount}, Cols {columnCount}";
                }

                return;
            }

            int selectedRowCount = SpreadsheetDataGrid.SelectedCells
                .Select(cell => SpreadsheetDataGrid.Items.IndexOf(cell.Item))
                .Where(index => index >= 0)
                .Distinct()
                .Count();
            int selectedColumnCount = SpreadsheetDataGrid.SelectedCells
                .Select(cell => cell.Column.DisplayIndex)
                .Distinct()
                .Count();

            BottomBarText.Text =
                $"Rows {rowCount}, Cols {columnCount}, Selected {SpreadsheetDataGrid.SelectedCells.Count} cells ({selectedRowCount} rows x {selectedColumnCount} cols)";
            return;
        }

        if (editorMode == EtwEditorMode.Markdown)
        {
            HideSelectionSpecificUi();

            string plainText = MarkdownEditorControl.Document is null
                ? string.Empty
                : MarkdownDocumentUtilities.GetDocumentPlainText(MarkdownEditorControl.Document);
            string selectedText = MarkdownEditorControl.Selection.Text.TrimEnd('\r', '\n');

            BottomBarText.Text = string.IsNullOrEmpty(selectedText)
                ? $"Markdown, Chars {plainText.Length}"
                : $"Markdown, Selected {selectedText.Length} chars";
            return;
        }

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
        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            HideSelectionSpecificUi();
            return;
        }

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
        // Pick up a spell-check mode changed from the settings page while this window was open.
        if (Enum.TryParse(DefaultSettings.EtwSpellCheckMode, out SpellCheckMode settingsMode)
            && settingsMode != spellCheckMode)
            SetSpellCheckMode(settingsMode);

        if (editorMode == EtwEditorMode.Spreadsheet)
            SpreadsheetDataGrid.Focus();
        else if (editorMode == EtwEditorMode.Markdown)
        {
            ApplyMarkdownTheme();
            MarkdownEditorControl.Focus();
        }
        else
            PassedTextControl.Focus();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
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

        // Warm up the cached clipboard OCR state so CanOcrPasteExecute is accurate immediately.
        UpdateCachedClipboardOcrState();

        if (editorMode == EtwEditorMode.Spreadsheet)
            SetEditorMode(EtwEditorMode.Spreadsheet);
        else if (editorMode == EtwEditorMode.Markdown)
            SetEditorMode(EtwEditorMode.Markdown);
        else
            UpdateSpreadsheetModeUi();
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
        List<double> numbers = GetCurrentAggregateNumbers();

        // Update menu items based on whether we have numbers
        if (numbers.Count == 0)
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
        if (_selectedAggregate == AggregateType.None)
        {
            CalcAggregateStatusBorder.Visibility = Visibility.Collapsed;
            return;
        }

        List<double> numbers = GetCurrentAggregateNumbers();

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

    private void UpdateCalcPaneTextDisplay()
    {
        if (TryGetSpreadsheetSelectionNumbersPreviewText(out string selectionPreviewText))
        {
            CalcResultsTextControl.Text = selectionPreviewText;
            return;
        }

        CalcResultsTextControl.Text = calculationResult?.Output ?? string.Empty;
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
        List<double> numbers = GetCurrentAggregateNumbers();

        // Update menu items based on whether we have numbers
        if (numbers.Count == 0)
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

    private List<double> GetCurrentAggregateNumbers()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            List<(int RowIndex, int ColumnIndex)> selectedCellCoordinates = GetSelectedSpreadsheetCellCoordinates();
            if (selectedCellCoordinates.Count > 0)
                return ExtractSpreadsheetSelectionNumbers(spreadsheetTable, selectedCellCoordinates);
        }

        return calculationResult?.OutputNumbers ?? [];
    }

    private bool TryGetSpreadsheetSelectionNumbersPreviewText(out string previewText)
    {
        previewText = string.Empty;

        if (editorMode != EtwEditorMode.Spreadsheet)
            return false;

        List<(int RowIndex, int ColumnIndex)> selectedCellCoordinates = GetSelectedSpreadsheetCellCoordinates();
        if (selectedCellCoordinates.Count == 0)
            return false;

        previewText = BuildSpreadsheetSelectionNumbersPreviewText(spreadsheetTable, selectedCellCoordinates);
        return !string.IsNullOrEmpty(previewText);
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

    private void SpreadsheetContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu contextMenu)
            return;

        MenuItem? wrapTextMenuItem = GetContextMenuItem(contextMenu, "SpreadsheetWrapTextToggle");
        if (wrapTextMenuItem is null)
            return;

        EnsureSpreadsheetDocumentFromText();

        List<(int RowIndex, int ColumnIndex)> targetCells = GetSelectedOrCurrentSpreadsheetCellCoordinates();
        bool hasTargetCells = tableDocument is not null && targetCells.Count > 0;

        wrapTextMenuItem.IsEnabled = hasTargetCells;
        wrapTextMenuItem.IsChecked = hasTargetCells && AreSpreadsheetDocumentCellsWrapped(tableDocument!, targetCells);
    }

    private void ToggleSpreadsheetWrapTextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;

        List<(int RowIndex, int ColumnIndex)> targetCells = GetSelectedOrCurrentSpreadsheetCellCoordinates();
        if (targetCells.Count == 0)
            return;

        int focusRow = Math.Max(0, spreadsheetContextRowIndex ?? SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem));
        int focusColumn = Math.Max(0, spreadsheetContextColumnIndex ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0);

        ApplySpreadsheetDocumentChange(
            document =>
            {
                SetSpreadsheetDocumentCellWrapState(document, targetCells, menuItem.IsChecked);
                ClearSpreadsheetDocumentRowHeights(document, targetCells.Select(cell => cell.RowIndex));
            },
            focusRow,
            focusColumn,
            beginEdit: false);
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

    private async void SummarizeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetToLoading("Summarizing...");

        try
        {
            await ApplySelectedTextOrAllTextTransformAsync(text => WindowsAiUtilities.SummarizeParagraph(text));
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
        SetToLoading("Rewriting...");
        try
        {
            await ApplySelectedTextOrAllTextTransformAsync(text => WindowsAiUtilities.Rewrite(text));
        }
        finally
        {
            SetToLoaded();
        }
    }

    private async void ConvertTableMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetToLoading("Converting...");

        try
        {
            await ApplySelectedTextOrAllTextTransformAsync(text => WindowsAiUtilities.TextToTable(text));
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

        try
        {
            await ApplySelectedTextOrAllTextTransformAsync(text => WindowsAiUtilities.TranslateText(text, targetLanguage));
        }
        catch (Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Translation Error",
                Content = $"Translation failed: {ex.Message}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
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
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "No Text",
                Content = "Please enter or select text to extract a regex pattern from.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
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

        if (string.IsNullOrWhiteSpace(regexPattern))
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Extraction Failed",
                Content = "Failed to extract a regex pattern. The AI service may not be available or could not generate a pattern.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
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
                await new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Copy Failed",
                    Content = "Failed to copy regex pattern to clipboard.",
                    CloseButtonText = "OK"
                }.ShowDialogAsync();
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
        findWindow.UsePatternCheckBox.IsChecked = true;
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
        returnRegexes.AddRange(AppUtilities.TextGrabSettingsService.LoadStoredRegexes());

        // Add default patterns if list is empty
        if (returnRegexes.Count == 0)
        {
            foreach (StoredRegex defaultPattern in StoredRegex.GetDefaultPatterns())
                returnRegexes.Add(defaultPattern);
        }

        return returnRegexes;
    }
}
