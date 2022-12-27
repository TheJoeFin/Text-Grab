using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Controls;

public partial class BottomBarSettings : Window
{
    private ObservableCollection<CustomButton> ButtonsInRightList { get; set; }
    private ObservableCollection<CustomButton> ButtonsInLeftList { get; set; }

    public BottomBarSettings()
    {
        InitializeComponent();

        List<CustomButton> allBtns = new(CustomButton.AllButtons);

        ButtonsInRightList = new(CustomBottomBarUtilities.GetCustomBottomBarItemsSetting());
        RightListBox.ItemsSource = ButtonsInRightList;
        foreach (CustomButton cbutton in ButtonsInRightList)
        {
            allBtns.Remove(cbutton);
        }

        ButtonsInLeftList = new(allBtns);
        LeftListBox.ItemsSource = ButtonsInLeftList;
    }

    private void MoveRightButton_Click(object sender, RoutedEventArgs e)
    {
        if (LeftListBox.SelectedItem is not CustomButton customButton)
            return;

        ButtonsInRightList.Add(customButton);
        ButtonsInLeftList.Remove(customButton);
    }

    private void MoveLeftButton_Click(object sender, RoutedEventArgs e)
    {
        if (RightListBox.SelectedItem is not CustomButton customButton)
            return;

        // ButtonsInLeftList.Add(customButton);
        InsertSorted<CustomButton>(ButtonsInLeftList, customButton, p => p.OrderNumber);
        ButtonsInRightList.Remove(customButton);
    }

    private void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        CustomBottomBarUtilities.SaveCustomBottomBarItemsSetting(ButtonsInRightList.ToList());
        if (Owner is EditTextWindow etw)
            etw.SetBottomBarButtons();
        this.Close();
    }


    private void CloseBTN_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    public void InsertSorted<T>(ObservableCollection<T> collection, T item, Func<T, double> propertySelector)
    {
        int index = 0;
        while (index < collection.Count && propertySelector(collection[index]) < propertySelector(item))
        {
            index++;
        }
        collection.Insert(index, item);
    }
}