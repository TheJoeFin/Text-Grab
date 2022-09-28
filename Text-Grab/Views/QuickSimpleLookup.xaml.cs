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

namespace Text_Grab.Views;

/// <summary>
/// Interaction logic for QuickSimpleLookup.xaml
/// </summary>
public partial class QuickSimpleLookup : Window
{
    public List<LookupItem> ItemsDictionary { get; set; } = new List<LookupItem>();

    public bool IsEditingDataGrid { get; set; } = false;

    string cacheFilename = "QuickSimpleLookupCache.csv";

    public QuickSimpleLookup()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        string? exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location);
        string cachePath = $"{exePath}\\{cacheFilename}";
        if (File.Exists(cachePath))
        {
            string cacheRAW = await File.ReadAllTextAsync(cachePath);
            ItemsDictionary.AddRange(ParseStringToRows(cacheRAW, true));
        }

        MainDataGrid.ItemsSource = null;
        MainDataGrid.ItemsSource = ItemsDictionary;

        UpdateRowCount();
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

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (IsEditingDataGrid) 
                    return;
                e.Handled = true;
                PutValueIntoClipboard();
                break;
            case Key.Escape:
                ClearOrExit();
                break;
            case Key.Down:
                if (SearchBox.IsFocused)
                    MainDataGrid.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
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
            SearchBox.Text = "";
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

        try
        {
            Clipboard.SetText(textVal);
            this.Close();
        }
        catch (Exception)
        {
            Debug.WriteLine("Failed to set clipboard text");
        }
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        await WriteDataToCSV();
    }

    private async Task WriteDataToCSV()
    {
        if (SearchBox.Text is string text && !string.IsNullOrEmpty(text))
            return;

        string? exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location);

        StringBuilder csvContents = new();

        if (MainDataGrid.ItemsSource is not List<LookupItem> itemsToSave) 
            return;

        foreach (LookupItem lookupItem in itemsToSave)
            csvContents.AppendLine(lookupItem.ToCSVString());

        await File.WriteAllTextAsync($"{exePath}\\{cacheFilename}", csvContents.ToString());
    }

    private void MainDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        SaveBTN.Visibility = Visibility.Visible;
        IsEditingDataGrid = false;
    }

    private async void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        await WriteDataToCSV();
        SaveBTN.Visibility = Visibility.Collapsed;
    }

    private void MainDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        IsEditingDataGrid = true;
    }
}
