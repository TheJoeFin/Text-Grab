using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab.Controls;

public partial class BottomBarSettings : Window
{
    private ObservableCollection<ButtonInfo> ButtonsInRightList { get; set; }
    private ObservableCollection<ButtonInfo> ButtonsInLeftList { get; set; }

    public BottomBarSettings()
    {
        InitializeComponent();

        List<ButtonInfo> allBtns = new(ButtonInfo.AllButtons);

        ButtonsInRightList = new(CustomBottomBarUtilities.GetCustomBottomBarItemsSetting());
        RightListBox.ItemsSource = ButtonsInRightList;
        foreach (ButtonInfo cbutton in ButtonsInRightList)
        {
            allBtns.Remove(cbutton);
        }

        ButtonsInLeftList = new(allBtns);
        LeftListBox.ItemsSource = ButtonsInLeftList;

        ShowCursorTextCheckBox.IsChecked = Settings.Default.ShowCursorText;
        ShowScrollbarCheckBox.IsChecked = Settings.Default.ScrollBottomBar;
    }

    private void MoveRightButton_Click(object sender, RoutedEventArgs e)
    {
        if (LeftListBox.SelectedItem is not ButtonInfo customButton)
            return;

        ButtonsInRightList.Add(customButton);
        ButtonsInLeftList.Remove(customButton);
    }

    private void MoveLeftButton_Click(object sender, RoutedEventArgs e)
    {
        if (RightListBox.SelectedItem is not ButtonInfo customButton)
            return;

        // ButtonsInLeftList.Add(customButton);
        InsertSorted(ButtonsInLeftList, customButton, p => p.OrderNumber);
        ButtonsInRightList.Remove(customButton);
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        int newIndex = MoveUp(ButtonsInRightList, RightListBox.SelectedIndex);
        RightListBox.SelectedIndex = newIndex;
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        int newIndex = MoveDown(ButtonsInRightList, RightListBox.SelectedIndex);
        RightListBox.SelectedIndex = newIndex;
    }

    private void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        Settings.Default.ShowCursorText = ShowCursorTextCheckBox.IsChecked ?? true;
        Settings.Default.ScrollBottomBar = ShowScrollbarCheckBox.IsChecked ?? true;
        Settings.Default.Save();

        CustomBottomBarUtilities.SaveCustomBottomBarItemsSetting(ButtonsInRightList.ToList());
        if (Owner is EditTextWindow etw)
            etw.SetBottomBarButtons();

        this.Close();
    }


    private void CloseBTN_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    public static void InsertSorted<T>(ObservableCollection<T> collection, T item, Func<T, double> propertySelector)
    {
        int index = 0;
        while (index < collection.Count && propertySelector(collection[index]) < propertySelector(item))
        {
            index++;
        }
        collection.Insert(index, item);
    }

    public static int MoveUp<T>(ObservableCollection<T> collection, int index)
    {
        if (index > 0 && index < collection.Count)
        {
            T item = collection[index];
            collection.RemoveAt(index);
            collection.Insert(index - 1, item);
            return index - 1;
        }
        return 0;
    }

    public static int MoveDown<T>(ObservableCollection<T> collection, int index)
    {
        if (index >= 0 && index < collection.Count - 1)
        {
            T item = collection[index];
            collection.RemoveAt(index);
            collection.Insert(index + 1, item);
            return index + 1;
        }
        return collection.Count;
    }


}