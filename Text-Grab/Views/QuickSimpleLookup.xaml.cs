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
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab.Views;

/// <summary>
/// Interaction logic for QuickSimpleLookup.xaml
/// </summary>
public partial class QuickSimpleLookup : Window
{
    public List<LookupItem> ItemsDictionary { get; set; } = new();

    public bool IsEditingDataGrid { get; set; } = false;

    public TextBox? DestinationTextBox;

    private string cacheFilename = "QuickSimpleLookupCache.csv";

    private int rowCount = 0;

    private string valueUnderEdit = string.Empty;

    private LookupItem? lastSelection;

    public QuickSimpleLookup()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        string? exePath = Path.GetDirectoryName(System.AppContext.BaseDirectory);
        string cachePath = $"{exePath}\\{cacheFilename}";

        if (!string.IsNullOrEmpty(Settings.Default.LookupFileLocation)
            && File.Exists(Settings.Default.LookupFileLocation))
            cachePath = Settings.Default.LookupFileLocation;

        if (File.Exists(cachePath))
            await ReadCsvFileIntoQuickSimpleLookup(cachePath);

        Topmost = false;
        Activate();
        SearchBox.Focus();

        if (MainDataGrid.Items.Count > 0)
            MainDataGrid.SelectedIndex = 0;
        else
            PopulateSampleData();
    }

    private void PopulateSampleData()
    {
        LookupItem sampleItem1 = new("This is the key", "This is the value you want to copy quickly");
        ItemsDictionary.Add(sampleItem1);

        LookupItem sampleItem2 = new("Import data", "From a copied Excel table, or import from a CSV File");
        ItemsDictionary.Add(sampleItem2);

        LookupItem sampleItem3 = new("You can change save location", "Putting the data store location in OneDrive it will sync across devices");
        ItemsDictionary.Add(sampleItem3);

        LookupItem sampleItem4 = new("Delete these initial rows", "and add your own manually if you like.");
        ItemsDictionary.Add(sampleItem4);

        MainDataGrid.ItemsSource = null;
        MainDataGrid.ItemsSource = ItemsDictionary;
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox searchingBox || !IsLoaded)
            return;

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

        List<string> searchArray = SearchBox.Text.ToLower().Split().ToList();
        searchArray.Sort();

        List<LookupItem> filteredList = new List<LookupItem>();

        foreach (LookupItem lItem in ItemsDictionary)
        {
            string lItemAsString = lItem.ToString().ToLower();
            bool matchAllSearchWords = true;

            foreach (var searchWord in searchArray)
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

    private void UpdateRowCount()
    {
        if (MainDataGrid.ItemsSource is List<LookupItem> list)
        {
            rowCount = list.Count;
            RowCountTextBlock.Text = $"{rowCount} Rows";
        }
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

    private static IEnumerable<LookupItem> ParseStringToRows(string clipboardContent, bool isCSV = false)
    {
        List<string> rows = clipboardContent.Split(Environment.NewLine).ToList();

        char splitChar = isCSV ? ',' : '\t';

        foreach (string row in rows)
        {
            LookupItem newRow = ParseStringToLookupItem(splitChar, row);

            if (!string.IsNullOrWhiteSpace(newRow.ToString()))
                yield return newRow;
        }
    }

    private static LookupItem ParseStringToLookupItem(char splitChar, string row)
    {
        List<string> cells = row.Split(splitChar).ToList();
        LookupItem newRow = new LookupItem();
        if (cells.FirstOrDefault() is String firstCell)
            newRow.shortValue = firstCell;

        newRow.longValue = "";
        if (cells.Count > 1 && cells[1] is String)
            newRow.longValue = String.Join(" ", cells.Skip(1).ToArray());
        return newRow;
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
                if (IsEditingDataGrid)
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
            case Key.E:
                if (Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    SearchBox.Focus();
                    e.Handled = true;
                }
                break;
            case Key.S:
                if (Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    await WriteDataToCSV();
                    SaveBTN.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                }
                break;
            case Key.F:
                if (Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    WindowUtilities.LaunchFullScreenGrab(SearchBox);
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

    private List<LookupItem> GetMainDataGridSelection()
    {
        var selectedItems = MainDataGrid.SelectedItems as List<LookupItem>;

        if (selectedItems is null || selectedItems.Count == 0)
        {
            selectedItems = new List<LookupItem>();
            if (MainDataGrid.SelectedItem is not LookupItem selectedLookupItem)
                return selectedItems;

            selectedItems.Add(selectedLookupItem);
        }

        return selectedItems;
    }

    private void RowDeleted()
    {
        var currentItemSource = MainDataGrid.ItemsSource;
        if (currentItemSource is not List<LookupItem> filteredLookupList)
            return;

        List<LookupItem> selectedItems = GetMainDataGridSelection();

        MainDataGrid.ItemsSource = null;

        foreach (object item in selectedItems)
        {
            if (item is LookupItem selectedLookupItem)
            {
                filteredLookupList.Remove(selectedLookupItem);
                ItemsDictionary.Remove(selectedLookupItem);
                SaveBTN.Visibility = Visibility.Visible;
            }
        }

        MainDataGrid.ItemsSource = filteredLookupList;
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

    private void AddToLookUpResults(char splitChar, string text)
    {
        LookupItem newItem = ParseStringToLookupItem(splitChar, text);

        MainDataGrid.ItemsSource = null;
        ItemsDictionary.Add(newItem);
        MainDataGrid.ItemsSource = ItemsDictionary;

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

    private void PutValueIntoClipboard()
    {
        if (MainDataGrid.ItemsSource is not List<LookupItem> lookUpList
            || lookUpList.FirstOrDefault() is not LookupItem firstLookupItem)
            return;

        List<LookupItem> selectedLookupItems = new();

        foreach (object item in MainDataGrid.SelectedItems)
            if (item is LookupItem selectedLookupItem)
                selectedLookupItems.Add(selectedLookupItem);

        if (selectedLookupItems.Count == 0)
            selectedLookupItems.Add(firstLookupItem);

        StringBuilder stringBuilder = new();

        switch (KeyboardExtensions.GetKeyboardModifiersDown())
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

                if (Uri.TryCreate(lookupItem.longValue, UriKind.Absolute, out var uri))
                {
                    Process.Start(new ProcessStartInfo(lookupItem.longValue) { UseShellExecute = true });
                    this.Close();
                    return;
                }
                break;
            case KeyboardModifiersDown.Ctrl:
                foreach (LookupItem lItem in selectedLookupItems)
                    stringBuilder.AppendLine(lItem.shortValue);
                break;
            case KeyboardModifiersDown.Shift:
                foreach (LookupItem lItem in selectedLookupItems)
                    stringBuilder.AppendLine(lItem.ToString());
                break;
            default:
                foreach (LookupItem lItem in selectedLookupItems)
                    stringBuilder.AppendLine(lItem.longValue);
                break;
        }

        if (string.IsNullOrEmpty(stringBuilder.ToString()))
            return;

        if (DestinationTextBox is not null)
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
        catch (Exception)
        {
            Debug.WriteLine("Failed to set clipboard text");
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {

    }

    private async Task WriteDataToCSV()
    {
        if (SearchBox.Text is string text && !string.IsNullOrEmpty(text))
            return;

        string saveLookupFilePath = $"C:\\{cacheFilename}";
        if (string.IsNullOrEmpty(Settings.Default.LookupFileLocation))
        {
            string? exePath = Path.GetDirectoryName(System.AppContext.BaseDirectory);
            saveLookupFilePath = $"{exePath}\\{cacheFilename}";
        }
        else
        {
            saveLookupFilePath = Settings.Default.LookupFileLocation;
        }

        StringBuilder csvContents = new();

        if (MainDataGrid.ItemsSource is not List<LookupItem> itemsToSave)
            return;

        foreach (LookupItem lookupItem in itemsToSave)
            csvContents.AppendLine(lookupItem.ToCSVString());

        await File.WriteAllTextAsync(saveLookupFilePath, csvContents.ToString());
    }

    private void MainDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        IsEditingDataGrid = false;

        if (e.EditAction == DataGridEditAction.Cancel)
            return;

        var child = VisualTreeHelper.GetChild(e.EditingElement, 0);
        if (child is TextBox editedBox
            && valueUnderEdit != editedBox.Text)
        {
            SaveBTN.Visibility = Visibility.Visible;
            valueUnderEdit = string.Empty;
            UpdateRowCount();
        }
    }

    private async void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            SearchBox.Clear();

        await WriteDataToCSV();
        SaveBTN.Visibility = Visibility.Collapsed;
    }

    private void MainDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        IsEditingDataGrid = true;
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

    private void EditingTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb)
            return;

        valueUnderEdit = tb.Text;

        tb.Focus();
        tb.SelectAll();
    }

    private void TextGrabSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<SettingsWindow>();
    }

    private async void ParseCSVFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Create OpenFileDialog 
        Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

        // Set filter for file extension and default file extension 
        dlg.DefaultExt = ".csv";
        dlg.Filter = "Comma Separated Values File (.csv)|*.csv";
        dlg.CheckFileExists = true;

        bool? result = dlg.ShowDialog();

        if (result is false || !File.Exists(dlg.FileName))
            return;

        string csvToOpenPath = dlg.FileName;

        await ReadCsvFileIntoQuickSimpleLookup(csvToOpenPath);
        SaveBTN.Visibility = Visibility.Visible;
    }

    private async Task ReadCsvFileIntoQuickSimpleLookup(string csvToOpenPath)
    {
        try
        {
            using FileStream fs = new(csvToOpenPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader sr = new(fs, Encoding.Default);
            string cacheRAW = await sr.ReadToEndAsync();

            ItemsDictionary.AddRange(ParseStringToRows(cacheRAW, true));
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Failed to read csv file. {ex.Message}");
        }

        MainDataGrid.ItemsSource = null;
        MainDataGrid.ItemsSource = ItemsDictionary;

        UpdateRowCount();
    }

    private async void PickSaveLocation_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dlg = new();

        dlg.AddExtension = true;
        dlg.DefaultExt = ".csv";
        dlg.InitialDirectory = "C:\\";
        dlg.FileName = "QuickSimpleLookupDataFile.csv";
        dlg.OverwritePrompt = false;

        if (!string.IsNullOrEmpty(Settings.Default.LookupFileLocation))
        {
            dlg.InitialDirectory = Settings.Default.LookupFileLocation;
            dlg.FileName = Path.GetFileName(Settings.Default.LookupFileLocation);
        }

        var result = dlg.ShowDialog();

        if (result is false)
            return;

        Settings.Default.LookupFileLocation = dlg.FileName;
        Settings.Default.Save();

        if (File.Exists(dlg.FileName))
        {
            // clear and load the new file
            ItemsDictionary.Clear();
            await ReadCsvFileIntoQuickSimpleLookup(dlg.FileName);
        }
        else
            await WriteDataToCSV();
    }

    private void NewFullscreen_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab(SearchBox);
    }

    private void AddItemBtn_Click(object sender, RoutedEventArgs e)
    {
        if (SearchBox is not TextBox searchTextBox)
            return;

        AddToLookUpResults('\t', searchTextBox.Text);
        searchTextBox.Clear();
        GoToEndOfMainDataGrid();
    }
}
