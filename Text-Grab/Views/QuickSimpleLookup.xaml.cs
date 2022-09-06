using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
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
        SearchBox.Focus();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox searchingBox || !IsLoaded) return;

        MainDataGrid.ItemsSource = null;

        if (string.IsNullOrEmpty(searchingBox.Text))
        {
            MainDataGrid.ItemsSource = ItemsDictionary;
            MainDataGrid.CanUserAddRows = true;
        }
        else
        {
            MainDataGrid.CanUserAddRows = false;
        }


        List<LookupItem> filteredList = new List<LookupItem>();

        foreach (LookupItem lItem in ItemsDictionary)
        {
            if (lItem.shortValue.ToLower().Contains(searchingBox.Text.ToLower())
                || lItem.longValue.ToLower().Contains(searchingBox.Text.ToLower()))
                filteredList.Add(lItem);
        }

        MainDataGrid.ItemsSource = filteredList;

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

        if (string.IsNullOrEmpty(clipboardContent)) return;

        MainDataGrid.ItemsSource = null;

        ItemsDictionary.AddRange(ParseStringToRows(clipboardContent));

        MainDataGrid.ItemsSource = ItemsDictionary;
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
        switch(e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                PutValueIntoClipboard();
                break;
            case Key.Escape:
                if (sender is TextBox searchBox)
                    ClearOrExit(searchBox);
                break;
            default:
                break;
        }
    }

    private void ClearOrExit(TextBox searchBox)
    {
        if (string.IsNullOrEmpty(searchBox.Text))
        {
            this.Close();
            return;
        }
        else
        {
            searchBox.Text = "";
        }
    }

    private void PutValueIntoClipboard()
    {
        if (MainDataGrid.ItemsSource is List<LookupItem> lookUpList
                        && lookUpList.FirstOrDefault() is LookupItem firstLookupItem)
        {
            string textVal = firstLookupItem.longValue as string;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                textVal = firstLookupItem.shortValue as string;

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                textVal = String.Join(" ", new string[] { firstLookupItem.shortValue as string, firstLookupItem.longValue as string });

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

        if (MainDataGrid.ItemsSource is not List<LookupItem> itemsToSave) return;

        foreach (LookupItem lookupItem in itemsToSave)
            csvContents.AppendLine(lookupItem.ToCSVString());

        await File.WriteAllTextAsync($"{exePath}\\{cacheFilename}", csvContents.ToString());
    }
}
