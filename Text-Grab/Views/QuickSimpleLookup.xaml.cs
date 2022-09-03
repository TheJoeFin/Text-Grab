using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

    public QuickSimpleLookup()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ItemsDictionary.Add(new LookupItem("T1234-55", "hello there"));
        ItemsDictionary.Add(new LookupItem("T2345-66", "it me"));
        ItemsDictionary.Add(new LookupItem("T3456-77", "here we go"));

        MainDataGrid.ItemsSource = null;
        MainDataGrid.ItemsSource = ItemsDictionary;

        SearchBox.Focus();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox searchingBox || !IsLoaded) return;

        MainDataGrid.ItemsSource = null;

        if (string.IsNullOrEmpty(searchingBox.Text))
            MainDataGrid.ItemsSource = ItemsDictionary;


        List<LookupItem> filteredList = new List<LookupItem>();

        foreach (LookupItem lItem in ItemsDictionary)
        {
            if (lItem.shortValue.ToLower().Contains(searchingBox.Text.ToLower())
                || lItem.longValue.ToLower().Contains(searchingBox.Text.ToLower()))
                filteredList.Add(lItem);
        }

        MainDataGrid.ItemsSource = filteredList;
    }

    private void ParseBTN_Click(object sender, RoutedEventArgs e)
    {
        string clipboardContent = Clipboard.GetText();

        if (string.IsNullOrEmpty(clipboardContent)) return;

        MainDataGrid.ItemsSource = null;

        List<string> rows = clipboardContent.Split(Environment.NewLine).ToList();

        foreach (string row in rows)
        {
            List<string> cells = row.Split('\t').ToList();
            LookupItem newRow = new LookupItem();
            if (cells.FirstOrDefault() is String firstCell)
                newRow.shortValue = firstCell;

            newRow.longValue = "";
            if (cells.Count > 1 && cells[1] is String)
                newRow.longValue = String.Join(" ", cells.Skip(1).ToArray());

            ItemsDictionary.Add(newRow);
        }

        MainDataGrid.ItemsSource = ItemsDictionary;
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && sender is TextBox searchbox)
        {
            e.Handled = true;

            if (string.IsNullOrEmpty(searchbox.Text))
            {
                this.Close();
                return;
            }

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
    }
}
