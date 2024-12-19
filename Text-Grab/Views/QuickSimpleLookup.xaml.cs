using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab.Views;

public partial class QuickSimpleLookup : Wpf.Ui.Controls.FluentWindow
{
    #region Fields

    public TextBox? DestinationTextBox;
    private readonly string cacheFilename = "QuickSimpleLookupCache.csv";
    private bool isPuttingValueIn = false;
    private LookupItem? lastSelection;
    private int rowCount = 0;
    private string valueUnderEdit = string.Empty;
    private LookupItem? itemUnderEdit;
    private static readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private List<LookupItem> lookupItems = [];

    #endregion Fields

    #region Constructors

    public QuickSimpleLookup()
    {
        InitializeComponent();
        App.SetTheme();
    }

    #endregion Constructors

    #region Properties

    public bool IsEditingDataGrid { get; set; } = false;
    public bool IsFromETW { get; set; } = false;
    public List<LookupItem> ItemsDictionary { get; set; } = [];

    #endregion Properties

    #region Methods

    private static LookupItem ParseStringToLookupItem(char splitChar, string row)
    {
        List<string> cells = [.. row.Split(splitChar)];
        LookupItem newRow = new();
        if (cells.FirstOrDefault() is string firstCell)
            newRow.ShortValue = firstCell;

        newRow.LongValue = "";
        if (cells.Count > 1 && cells[1] is not null)
            newRow.LongValue = string.Join(" ", cells.Skip(1).ToArray());
        return newRow;
    }

    private static IEnumerable<LookupItem> ParseStringToRows(string clipboardContent, bool isCSV = false)
    {
        List<string> rows = [.. clipboardContent.Split(Environment.NewLine)];

        char splitChar = isCSV ? ',' : '\t';

        foreach (string row in rows)
        {
            LookupItem newRow = ParseStringToLookupItem(splitChar, row);

            if (!string.IsNullOrWhiteSpace(newRow.ToString()))
                yield return newRow;
        }
    }

    private void AddItemBtn_Click(object sender, RoutedEventArgs e)
    {
        if (SearchBox is not TextBox searchTextBox)
            return;

        AddToLookUpResults('\t', searchTextBox.Text);
        searchTextBox.Clear();
        GoToEndOfMainDataGrid();
    }

    private void AddToLookUpResults(char splitChar, string text)
    {
        LookupItem newItem = ParseStringToLookupItem(splitChar, text);

        MainDataGrid.ItemsSource = null;
        ItemsDictionary.Add(newItem);
        lookupItems.Add(newItem);

        UpdateRowCount();
        MainDataGrid.ScrollIntoView(ItemsDictionary.LastOrDefault());
        AddItemBtn.Visibility = Visibility.Collapsed;
        SaveBTN.Visibility = Visibility.Visible;
    }

    private void ClearOrExit()
    {
        if (string.IsNullOrEmpty(SearchBox.Text))
        {
            this.Close();
            return;
        }

        lastSelection = GetMainDataGridSelection().FirstOrDefault();

        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void EditingTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb)
            return;

        valueUnderEdit = tb.Text;

        tb.Focus();
        tb.SelectAll();
    }

    private void EditWindowToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggleButton
            && toggleButton.IsChecked is true)
            PasteToggleButton.IsChecked = false;
    }

    private void EnterAltMI_Click(object sender, RoutedEventArgs e)
    {
        PutValueIntoClipboard(KeyboardModifiersDown.CtrlAlt);
    }

    private void EnterButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsEditingDataGrid)
            return;
        e.Handled = true;
        if (SearchBox is TextBox searchTextBox && searchTextBox.Text.Contains('\t'))
        {
            AddToLookUpResults('\t', searchTextBox.Text);
            searchTextBox.Clear();
            GoToEndOfMainDataGrid();
        }
        else
            PutValueIntoClipboard();
    }

    private void EnterCtrlMI_Click(object sender, RoutedEventArgs e)
    {
        PutValueIntoClipboard(KeyboardModifiersDown.Ctrl);
    }

    private void EnterShiftCtrlMI_Click(object sender, RoutedEventArgs e)
    {
        PutValueIntoClipboard(KeyboardModifiersDown.ShiftCtrl);
    }

    private void EnterShiftMI_Click(object sender, RoutedEventArgs e)
    {
        PutValueIntoClipboard(KeyboardModifiersDown.Shift);
    }

    private void ErrorBarOkay_Click(object sender, RoutedEventArgs e)
    {
        ErrorBar.Visibility = Visibility.Collapsed;
    }

    private void FluentWindow_Closed(object sender, EventArgs e)
    {
        if (!isPuttingValueIn)
            WindowUtilities.ShouldShutDown();
    }

    private List<LookupItem> GetMainDataGridSelection()
    {
        if (MainDataGrid.SelectedItems is not List<LookupItem> selectedItems
            || selectedItems.Count == 0)
        {
            selectedItems = [];
            if (MainDataGrid.SelectedItem is not LookupItem selectedLookupItem)
                return selectedItems;

            selectedItems.Add(selectedLookupItem);
        }

        return selectedItems;
    }

    private void GoToBeginningOfMainDataGrid()
    {
        if (MainDataGrid.ItemsSource is not List<LookupItem> lookupItemsList)
            return;

        if (lookupItemsList.Count < 1)
            return;

        MainDataGrid.ScrollIntoView(lookupItemsList.First());
        MainDataGrid.SelectedIndex = 0;
    }

    private void GoToEndOfMainDataGrid()
    {
        if (MainDataGrid.ItemsSource is not List<LookupItem> lookupItemsList)
            return;

        if (lookupItemsList.Count < 1)
            return;

        MainDataGrid.ScrollIntoView(lookupItemsList.Last());
        MainDataGrid.SelectedItem = lookupItemsList.Last();
    }

    private void MainDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        IsEditingDataGrid = true;
        itemUnderEdit = e.Row.Item as LookupItem;
    }

    private void MainDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        IsEditingDataGrid = false;

        if (e.EditAction == DataGridEditAction.Cancel)
        {
            itemUnderEdit = null;
            return;
        }

        LookupItem? editedRow = e.Row.Item as LookupItem;
        int prevIndex = -1;

        if (itemUnderEdit is not null)
        {
            prevIndex = lookupItems.IndexOf(itemUnderEdit);
            lookupItems.Remove(itemUnderEdit);
        }

        if (editedRow is not null)
        {
            if (prevIndex > -1)
                lookupItems.Insert(prevIndex, editedRow);
            else
                lookupItems.Add(editedRow);
        }

        DependencyObject child = VisualTreeHelper.GetChild(e.EditingElement, 0);
        if (child is TextBox editedBox
            && valueUnderEdit != editedBox.Text)
        {
            SaveBTN.Visibility = Visibility.Visible;
            valueUnderEdit = string.Empty;
            UpdateRowCount();
        }
    }

    private void MainDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainDataGrid.ItemsSource is List<LookupItem> list
            && string.IsNullOrEmpty(SearchBox.Text)
            && list.Count < rowCount)
        {
            // A row has been deleted
            SaveBTN.Visibility = Visibility.Visible;
            UpdateRowCount();
        }
    }

    private void NewFullscreen_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab(SearchBox);
    }

    private void ParseBTN_Click(object sender, RoutedEventArgs e)
    {
        string clipboardContent = Clipboard.GetText();

        if (string.IsNullOrEmpty(clipboardContent))
            return;

        MainDataGrid.ItemsSource = null;

        ItemsDictionary.AddRange(ParseStringToRows(clipboardContent));

        MainDataGrid.ItemsSource = ItemsDictionary;

        UpdateRowCount();
        SaveBTN.Visibility = Visibility.Visible;
    }

    private async void ParseCSVFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Create OpenFileDialog 
        OpenFileDialog dlg = new()
        {
            // Set filter for file extension and default file extension 
            DefaultExt = ".csv",
            Filter = "Comma Separated Values File (.csv)|*.csv",
            CheckFileExists = true
        };

        bool? result = dlg.ShowDialog();

        if (result is false || !File.Exists(dlg.FileName))
            return;

        string csvToOpenPath = dlg.FileName;

        await LoadDataGridContent(csvToOpenPath);
        SaveBTN.Visibility = Visibility.Visible;
    }

    private void PasteToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggleButton
            && toggleButton.IsChecked is true)
            EditWindowToggleButton.IsChecked = false;
    }

    private async void PickSaveLocation_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dlg = new()
        {
            AddExtension = true,
            DefaultExt = ".csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            FileName = "QuickSimpleLookupDataFile.csv",
            OverwritePrompt = false
        };

        if (!string.IsNullOrEmpty(DefaultSettings.LookupFileLocation))
        {
            dlg.InitialDirectory = Path.GetDirectoryName(DefaultSettings.LookupFileLocation);
            dlg.FileName = Path.GetFileName(DefaultSettings.LookupFileLocation);
        }

        bool? result = dlg.ShowDialog();

        if (result is false)
            return;

        DefaultSettings.LookupFileLocation = dlg.FileName;
        DefaultSettings.Save();

        if (File.Exists(dlg.FileName))
        {
            // clear and load the new file
            ItemsDictionary.Clear();
            await LoadDataGridContent(dlg.FileName);
        }
        else
            await WriteDataToCSV();
    }

    private void PopulateSampleData()
    {
        LookupItem sampleItem1 = new("This is the key", "This is the value you want to copy quickly")
        {
            Kind = LookupItemKind.Simple
        };
        ItemsDictionary.Add(sampleItem1);

        LookupItem sampleItem2 = new("Import data", "From a copied Excel table, or import from a CSV File")
        {
            Kind = LookupItemKind.Simple
        };
        ItemsDictionary.Add(sampleItem2);

        LookupItem sampleItem3 = new("You can change save location", "Putting the data store location in OneDrive it will sync across devices")
        {
            Kind = LookupItemKind.Simple
        };
        ItemsDictionary.Add(sampleItem3);

        LookupItem sampleItem4 = new("Delete these initial rows", "and add your own manually if you like.")
        {
            Kind = LookupItemKind.Simple
        };
        ItemsDictionary.Add(sampleItem4);

        MainDataGrid.ItemsSource = null;
        MainDataGrid.ItemsSource = ItemsDictionary;
    }

    private async void PutValueIntoClipboard(KeyboardModifiersDown? keysDown = null)
    {
        if (MainDataGrid.ItemsSource is not List<LookupItem> lookUpList
            || lookUpList.FirstOrDefault() is not LookupItem firstLookupItem)
        {
            EditTextWindow etw = new(SearchBox.Text, false);
            etw.Show();
            this.Close();
            WindowUtilities.ShouldShutDown();
            return;
        }

        isPuttingValueIn = true;

        List<LookupItem> selectedLookupItems = [];

        foreach (object item in MainDataGrid.SelectedItems)
            if (item is LookupItem selectedLookupItem)
                selectedLookupItems.Add(selectedLookupItem);

        if (selectedLookupItems.Count == 0)
            selectedLookupItems.Add(firstLookupItem);

        bool openedHistoryItemOrLink = false;
        StringBuilder stringBuilder = new();

        keysDown ??= KeyboardExtensions.GetKeyboardModifiersDown();

        switch (keysDown)
        {
            case KeyboardModifiersDown.ShiftCtrlAlt:
            case KeyboardModifiersDown.ShiftCtrl:
                // Copy all of the filtered results into the clipboard
                foreach (object item in MainDataGrid.ItemsSource)
                {
                    if (item is not LookupItem luItem)
                        continue;

                    stringBuilder.AppendLine(luItem.ToString());
                }
                break;
            case KeyboardModifiersDown.CtrlAlt:
                if (selectedLookupItems.FirstOrDefault() is not LookupItem lookupItem)
                    return;

                if (Uri.TryCreate(lookupItem.LongValue, UriKind.Absolute, out Uri? uri))
                {
                    Process.Start(new ProcessStartInfo(lookupItem.LongValue) { UseShellExecute = true });
                    this.Close();
                    return;
                }
                break;
            case KeyboardModifiersDown.Ctrl:
                foreach (LookupItem lItem in selectedLookupItems)
                    stringBuilder.AppendLine(lItem.ShortValue);
                break;
            case KeyboardModifiersDown.Shift:
                foreach (LookupItem lItem in selectedLookupItems)
                    stringBuilder.AppendLine(lItem.ToString());
                break;
            default:
                foreach (LookupItem lItem in selectedLookupItems)
                {
                    switch (lItem.Kind)
                    {
                        case LookupItemKind.EditWindow when lItem.HistoryItem is not null:
                            {
                                EditTextWindow editTextWindow = new(lItem.HistoryItem);
                                editTextWindow.Show();
                                openedHistoryItemOrLink = true;
                                break;
                            }
                        case LookupItemKind.GrabFrame when lItem.HistoryItem is not null:
                            {
                                GrabFrame gf = new(lItem.HistoryItem);
                                gf.Show();
                                openedHistoryItemOrLink = true;
                                break;
                            }
                        case LookupItemKind.Link:
                            Process.Start(new ProcessStartInfo(lItem.LongValue) { UseShellExecute = true });
                            openedHistoryItemOrLink = true;
                            break;
                        default:
                            stringBuilder.AppendLine(lItem.LongValue);
                            break;
                    }
                }
                break;
        }

        if (openedHistoryItemOrLink)
        {
            this.Close();
            WindowUtilities.ShouldShutDown();
            return;
        }

        if (string.IsNullOrEmpty(stringBuilder.ToString()))
            return;

        if (stringBuilder.Length > 3 && stringBuilder.ToString().EndsWith("\r\n"))
            stringBuilder.Remove(stringBuilder.Length - 2, 2);

        if (DestinationTextBox is not null && EditWindowToggleButton.IsChecked is true)
        {
            // Do it this way instead of append text because it inserts the text at the cursor
            // Then puts the cursor at the end of the newly added text
            // AppendText() just adds the text to the end no matter what.
            DestinationTextBox.SelectedText = stringBuilder.ToString();
            DestinationTextBox.Select(DestinationTextBox.SelectionStart + stringBuilder.ToString().Length, 0);
            DestinationTextBox.Focus();
            this.Close();
            return;
        }

        try
        {
            Clipboard.SetText(stringBuilder.ToString());
            this.Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set clipboard text: {ex.Message}");
            ErrorBar.Visibility = Visibility.Visible;
        }

        if (PasteToggleButton.IsChecked is true)
            await WindowUtilities.TryInsertString(stringBuilder.ToString());

        if (EditWindowToggleButton.IsChecked is true)
        {
            EditTextWindow etw = WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
            etw.PassedTextControl.Text = stringBuilder.ToString();
            etw.Show();
        }

        WindowUtilities.ShouldShutDown();
    }

    private async void QuickSimpleLookup_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (IsEditingDataGrid)
                    return;
                e.Handled = true;
                if (SearchBox is TextBox searchTextBox && searchTextBox.Text.Contains('\t'))
                {
                    AddToLookUpResults('\t', searchTextBox.Text);
                    searchTextBox.Clear();
                    GoToEndOfMainDataGrid();
                }
                else
                    PutValueIntoClipboard();
                break;
            case Key.Escape:
                if (IsEditingDataGrid)
                    return;
                ClearOrExit();
                e.Handled = true;
                break;
            case Key.Delete:
                if (IsEditingDataGrid || SearchBox.IsFocused)
                    return;
                RowDeleted();
                e.Handled = true;
                break;
            case Key.Down:
                if (SearchBox.IsFocused)
                {
                    int selectedIndex = MainDataGrid.SelectedIndex;
                    MainDataGrid.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                    e.Handled = true;
                }
                break;
            case Key.Q:
                if (KeyboardExtensions.IsCtrlDown())
                {
                    SearchBox.Focus();
                    e.Handled = true;
                }
                break;
            case Key.S:
                if (KeyboardExtensions.IsCtrlDown())
                {
                    await WriteDataToCSV();
                    e.Handled = true;
                }
                break;
            case Key.F:
                if (KeyboardExtensions.IsCtrlDown())
                {
                    WindowUtilities.LaunchFullScreenGrab(SearchBox);
                    e.Handled = true;
                }
                break;
            case Key.I:
                if (KeyboardExtensions.IsCtrlDown() && PasteToggleButton.IsChecked is bool pasteToggle)
                {
                    PasteToggleButton.IsChecked = !pasteToggle;
                    e.Handled = true;
                }
                break;
            case Key.E:
                if (KeyboardExtensions.IsCtrlDown() && EditWindowToggleButton.IsChecked is bool etwToggle)
                {
                    EditWindowToggleButton.IsChecked = !etwToggle;
                    e.Handled = true;
                }
                break;
            case Key.End:
                GoToEndOfMainDataGrid();
                break;
            case Key.Home:
                GoToBeginningOfMainDataGrid();
                break;
            default:
                break;
        }
    }

    private async Task LoadDataGridContent(string csvToOpenPath)
    {
        string contentToParse = string.Empty;

        if (string.IsNullOrEmpty(csvToOpenPath))
            contentToParse = await FileUtilities.GetTextFileAsync(cacheFilename, FileStorageKind.WithExe);
        else
            contentToParse = await FileUtilities.GetTextFileAsync(csvToOpenPath, FileStorageKind.Absolute);

        if (string.IsNullOrWhiteSpace(contentToParse))
            PopulateSampleData();

        MainDataGrid.ItemsSource = null;

        ItemsDictionary.AddRange(ParseStringToRows(contentToParse, true));
        lookupItems = [.. ItemsDictionary];

        if (DefaultSettings.LookupSearchHistory)
        {
            AddHistoryItemsToItemsDictionary();
            SearchHistory.IsChecked = true;
        }

        MainDataGrid.ItemsSource = ItemsDictionary;

        UpdateRowCount();
    }

    private void RowDeleted()
    {
        System.Collections.IEnumerable currentItemSource = MainDataGrid.ItemsSource;
        if (currentItemSource is not List<LookupItem> filteredLookupList)
            return;

        List<LookupItem> selectedItems = GetMainDataGridSelection();

        MainDataGrid.ItemsSource = null;

        foreach (object item in selectedItems)
        {
            if (item is LookupItem selectedLookupItem)
            {
                filteredLookupList.Remove(selectedLookupItem);
                lookupItems.Remove(selectedLookupItem);
                ItemsDictionary.Remove(selectedLookupItem);

                if (selectedLookupItem.HistoryItem is null)
                {
                    SaveBTN.Visibility = Visibility.Visible;
                    continue;
                }

                if (selectedLookupItem.Kind is LookupItemKind.EditWindow)
                    Singleton<HistoryService>.Instance.RemoveTextHistoryItem(selectedLookupItem.HistoryItem);
                else if (selectedLookupItem.Kind is LookupItemKind.GrabFrame)
                    Singleton<HistoryService>.Instance.RemoveImageHistoryItem(selectedLookupItem.HistoryItem);
            }
        }

        MainDataGrid.ItemsSource = filteredLookupList;
    }

    private async void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        await WriteDataToCSV();
        SaveBTN.Visibility = Visibility.Collapsed;
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox searchingBox || !IsLoaded)
            return;

        if (string.IsNullOrEmpty(searchingBox.Text))
            SearchLabel.Visibility = Visibility.Visible;
        else
            SearchLabel.Visibility = Visibility.Collapsed;

        if (searchingBox.Text.Contains('\t'))
        {
            // a tab has been entered and this will be a new entry
            AddItemBtn.Visibility = Visibility.Visible;
        }
        else
        {
            AddItemBtn.Visibility = Visibility.Collapsed;
        }

        MainDataGrid.ItemsSource = null;

        if (string.IsNullOrEmpty(searchingBox.Text))
        {
            MainDataGrid.ItemsSource = ItemsDictionary;
            MainDataGrid.CanUserAddRows = true;
            int maxMsDelay = 300;
            if (lastSelection is not null)
            {
                int lastSelectionInt = ItemsDictionary.IndexOf(lastSelection);
                DataGridRow row = (DataGridRow)MainDataGrid.ItemContainerGenerator.ContainerFromIndex(lastSelectionInt);
                if (row is null)
                {
                    MainDataGrid.UpdateLayout();
                    MainDataGrid.ScrollIntoView(MainDataGrid.Items[lastSelectionInt]);
                    await Task.Delay(lastSelectionInt > maxMsDelay ? maxMsDelay : lastSelectionInt);
                    row = (DataGridRow)MainDataGrid.ItemContainerGenerator.ContainerFromIndex(lastSelectionInt);
                }

                if (row is not null)
                {
                    row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    await Task.Delay(lastSelectionInt > maxMsDelay ? maxMsDelay : lastSelectionInt);
                }

                MainDataGrid.SelectedIndex = lastSelectionInt;
                lastSelection = null;
                UpdateRowCount();
                SearchBox.Focus();
                return;
            }
        }
        else
            MainDataGrid.CanUserAddRows = false;

        List<string> searchArray = [.. SearchBox.Text.ToLower().Split()];
        searchArray.Sort();

        List<LookupItem> filteredList = [];

        foreach (LookupItem lItem in ItemsDictionary)
        {
            string lItemAsString = lItem.ToString().ToLower();
            bool matchAllSearchWords = true;

            foreach (string searchWord in searchArray)
            {
                if (!lItemAsString.Contains(searchWord))
                    matchAllSearchWords = false;
            }

            if (matchAllSearchWords)
                filteredList.Add(lItem);
        }

        MainDataGrid.ItemsSource = filteredList;

        if (MainDataGrid.Items.Count > 0)
            MainDataGrid.SelectedIndex = 0;

        UpdateRowCount();
    }

    private void TextGrabSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<SettingsWindow>();
    }

    private void UpdateRowCount()
    {
        if (MainDataGrid.ItemsSource is List<LookupItem> list)
        {
            rowCount = list.Count;
            RowCountTextBlock.Text = $"{rowCount} Rows";
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataGridContent(DefaultSettings.LookupFileLocation);

        if (DefaultSettings.TryInsert && !IsFromETW)
            PasteToggleButton.IsChecked = true;

        if (IsFromETW)
            EditWindowToggleButton.IsChecked = true;

        Topmost = false;
        Activate();
        SearchBox.Focus();
    }

    private void AddHistoryItemsToItemsDictionary()
    {
        List<HistoryInfo> textHistoryItems = Singleton<HistoryService>.Instance.GetEditWindows();

        foreach (HistoryInfo historyItem in textHistoryItems)
        {
            LookupItem newItem = new(historyItem);
            ItemsDictionary.Add(newItem);
        }

        List<HistoryInfo> grabFrameHistoryItems = Singleton<HistoryService>.Instance.GetRecentGrabs();

        foreach (HistoryInfo historyItem in grabFrameHistoryItems)
        {
            LookupItem newItem = new(historyItem);
            ItemsDictionary.Add(newItem);
        }
    }

    private async Task WriteDataToCSV()
    {
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            SearchBox.Clear();

        StringBuilder csvContents = new();

        foreach (LookupItem lookupItem in lookupItems)
            csvContents.AppendLine(lookupItem.ToCSVString());

        try
        {
            if (string.IsNullOrEmpty(DefaultSettings.LookupFileLocation))
                await FileUtilities.SaveTextFile(csvContents.ToString(), cacheFilename, FileStorageKind.WithExe);
            else
                await FileUtilities.SaveTextFile(csvContents.ToString(), DefaultSettings.LookupFileLocation, FileStorageKind.Absolute);

            SaveBTN.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Failed to save csv file. {ex.Message}");
        }
    }

    private void FluentWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Can't do this here because it will close the app before the text can be inserted
        // WindowUtilities.ShouldShutDown();
    }

    private async void SearchHistory_Click(object sender, RoutedEventArgs e)
    {
        DefaultSettings.LookupSearchHistory = SearchHistory.IsChecked is true;

        lookupItems.Clear();
        ItemsDictionary.Clear();
        MainDataGrid.ItemsSource = null;

        await LoadDataGridContent(DefaultSettings.LookupFileLocation);
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainDataGrid.SelectedItem is not LookupItem lookupItem)
            return;

        RowDeleted();
    }

    private void OpenInETWMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainDataGrid.SelectedItem is not LookupItem lookupItem)
            return;

        switch (lookupItem.Kind)
        {
            case LookupItemKind.Simple:
                StringBuilder sb = new();
                sb.Append(lookupItem.ShortValue);
                sb.Append(Environment.NewLine);
                sb.AppendLine(lookupItem.LongValue);
                EditTextWindow etw = new(sb.ToString(), false);
                etw.Show();
                break;
            case LookupItemKind.EditWindow:
                if (lookupItem.HistoryItem is not null)
                {
                    EditTextWindow editTextWindow = new(lookupItem.HistoryItem);
                    editTextWindow.Show();
                    return;
                }

                EditTextWindow etw2 = new(lookupItem.LongValue, false);
                etw2.Show();
                break;
            case LookupItemKind.GrabFrame:
                if (lookupItem.HistoryItem is not null)
                {
                    EditTextWindow editTextWindow = new(lookupItem.HistoryItem);
                    editTextWindow.Show();
                    return;
                }

                EditTextWindow etw3 = new(lookupItem.LongValue, false);
                etw3.Show();
                break;
            case LookupItemKind.Link:
                StringBuilder sb2 = new();
                sb2.Append(lookupItem.ShortValue);
                sb2.Append(Environment.NewLine);
                sb2.AppendLine(lookupItem.LongValue);
                EditTextWindow etw4 = new(sb2.ToString(), false);
                etw4.Show();
                break;
            default:
                break;
        }
    }
    #endregion Methods
}
