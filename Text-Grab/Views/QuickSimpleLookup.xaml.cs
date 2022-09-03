using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
            if (lItem.shortValue.Contains(searchingBox.Text) || lItem.longValue.Contains(searchingBox.Text))
                filteredList.Add(lItem);
        }

        MainDataGrid.ItemsSource = filteredList;
    }
}


public class LookupItem
{
    public string shortValue { get; set; } = string.Empty;
    public string longValue { get; set; } = string.Empty;

    public LookupItem()
    {

    }

    public LookupItem(string sv, string lv)
    {
        shortValue = sv;
        longValue = lv;
    }
}