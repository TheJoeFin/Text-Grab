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
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab.Views;

/// <summary>
/// Interaction logic for QuickSimpleLookup.xaml
/// </summary>
public partial class QuickSimpleLookup : Window
{
    public List<LookupItem> ItemsDictionary { get; set; } = new List<LookupItem>();

    public bool IsEditingDataGrid { get; set; } = false;

    string cacheFilename = "QuickSimpleLookupCache.csv";

    public TextBox? DestinationTextBox;

    public QuickSimpleLookup()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        string? exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location);
        string cachePath = $"{exePath}\\{cacheFilename}";

        if (string.IsNullOrEmpty(Settings.Default.LookupFileLocation) == false
            && File.Exists(Settings.Default.LookupFileLocation))
            cachePath = Settings.Default.LookupFileLocation;

        if (File.Exists(cachePath))
            await ReadCsvFileIntoQuickSimpleLookup(cachePath);

        Topmost = false;
        Activate();
        SearchBox.Focus();

        if (MainDataGrid.Items.Count > 0)
            MainDataGrid.SelectedIndex = 0;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox searchingBox || !IsLoaded)
            return;

        MainDataGrid.ItemsSource = null;

        if (string.IsNullOrEmpty(searchingBox.Text))
        {
            MainDataGrid.ItemsSource = ItemsDictionary;
            MainDataGrid.CanUserAddRows = true;
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
                if (lItemAsString.Contains(searchWord) == false)
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
            RowCountTextBlock.Text = $"{list.Count} Rows";
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
            List<string> cells = row.Split(splitChar).ToList();
            LookupItem newRow = new LookupItem();
            if (cells.FirstOrDefault() is String firstCell)
                newRow.shortValue = firstCell;

            newRow.longValue = "";
            if (cells.Count > 1 && cells[1] is String)
                newRow.longValue = String.Join(" ", cells.Skip(1).ToArray());

            if (!string.IsNullOrWhiteSpace(newRow.ToString()))
                yield return newRow;
        }
    }

    private async void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (IsEditingDataGrid)
                    return;
                e.Handled = true;
                PutValueIntoClipboard();
                e.Handled = true;
                break;
            case Key.Escape:
                ClearOrExit();
                e.Handled = true;
                break;
            case Key.Down:
                if (SearchBox.IsFocused)
                {
                    MainDataGrid.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
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
                    WindowUtilities.LaunchFullScreenGrab(true, destinationTextBox: SearchBox);
                    e.Handled = true;
                }
                break;
            default:
                break;
        }
    }

    private void ClearOrExit()
    {
        if (string.IsNullOrEmpty(SearchBox.Text))
            this.Close();
        else
        {
            SearchBox.Clear();
            SearchBox.Focus();
        }
    }

    private void PutValueIntoClipboard()
    {
        if (MainDataGrid.ItemsSource is not List<LookupItem> lookUpList
            || lookUpList.FirstOrDefault() is not LookupItem firstLookupItem)
            return;

        LookupItem lookupItem = firstLookupItem;

        if (MainDataGrid.SelectedItem is LookupItem selectedLookupItem)
        {
            lookupItem = selectedLookupItem;
        }

        string textVal = lookupItem.longValue as string;

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            textVal = lookupItem.shortValue as string;

        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            textVal = String.Join(" ", new string[] { lookupItem.shortValue as string, lookupItem.longValue as string });

        if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
        {
            StringBuilder sb = new();
            // Copy all of the filtered results into the clipboard
            foreach (object item in MainDataGrid.ItemsSource)
            {
                if (item is not LookupItem luItem)
                    continue;

                sb.AppendLine(String.Join(" ", new string[] { luItem.shortValue as string, luItem.longValue as string }));
            }

            textVal = sb.ToString();
        }

        if (string.IsNullOrEmpty(textVal))
            return;

        if (DestinationTextBox is not null)
        {
            // Do it this way instead of append text because it inserts the text at the cursor
            // Then puts the cursor at the end of the newly added text
            // AppendText() just adds the text to the end no matter what.
            DestinationTextBox.SelectedText = textVal;
            DestinationTextBox.Select(DestinationTextBox.SelectionStart + textVal.Length, 0);
            DestinationTextBox.Focus();
        }
        else
        {
            try
            {
                Clipboard.SetText(textVal);
            }
            catch (Exception)
            {
                Debug.WriteLine("Failed to set clipboard text");
            }
        }

        this.Close();
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
            string? exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location);
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
        SaveBTN.Visibility = Visibility.Visible;
        IsEditingDataGrid = false;
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

    private async void ParseCSVFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Create OpenFileDialog 
        Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

        // Set filter for file extension and default file extension 
        dlg.DefaultExt = ".csv";
        dlg.Filter = "Comma Separated Values File (.csv)|*.csv";

        bool? result = dlg.ShowDialog();

        if (result == false || dlg.CheckFileExists == false)
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

        if (string.IsNullOrEmpty(Settings.Default.LookupFileLocation) == false)
        {
            dlg.InitialDirectory = Settings.Default.LookupFileLocation;
            dlg.FileName = Path.GetFileName(Settings.Default.LookupFileLocation);
        }

        var result = dlg.ShowDialog();

        if (result == false || dlg.CheckPathExists == false)
            return;

        Settings.Default.LookupFileLocation = dlg.FileName;
        Settings.Default.Save();

        if (dlg.CheckFileExists)
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
        WindowUtilities.LaunchFullScreenGrab(true, destinationTextBox: SearchBox);
    }
}
