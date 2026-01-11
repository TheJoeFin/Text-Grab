using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Controls;

/// <summary>
/// Converts enum values to int for ComboBox SelectedIndex binding
/// </summary>
public class EnumToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Enum enumValue)
            return (int)(object)enumValue;
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue && targetType.IsEnum)
            return Enum.ToObject(targetType, intValue);
        return DefaultCheckState.Off;
    }
}

public partial class PostGrabActionEditor : FluentWindow
{
    #region Properties

    private ObservableCollection<ButtonInfo> AvailableActions { get; set; }
    private ObservableCollection<ButtonInfo> EnabledActions { get; set; }

    #endregion Properties

    #region Constructors

    public PostGrabActionEditor()
    {
        InitializeComponent();

        // Get all available actions
        List<ButtonInfo> allActions = PostGrabActionManager.GetAvailablePostGrabActions();

        // Get currently enabled actions
        List<ButtonInfo> enabledActions = PostGrabActionManager.GetEnabledPostGrabActions();

        // Populate enabled list
        EnabledActions = new ObservableCollection<ButtonInfo>(enabledActions);
        EnabledActionsListBox.ItemsSource = EnabledActions;

        // Populate available list (actions not currently enabled) - sorted by OrderNumber
        AvailableActions = [];
        List<ButtonInfo> availableActionsList = [.. allActions
            .Where(a => !enabledActions.Any(e => e.ButtonText == a.ButtonText))
            .OrderBy(a => a.OrderNumber)];

        foreach (ButtonInfo? action in availableActionsList)
        {
            AvailableActions.Add(action);
        }
        AvailableActionsListBox.ItemsSource = AvailableActions;

        // Update empty state visibility
        UpdateEmptyStateVisibility();
    }

    #endregion Constructors

    #region Methods

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (AvailableActionsListBox.SelectedItem is not ButtonInfo selectedAction)
            return;

        EnabledActions.Add(selectedAction);
        AvailableActions.Remove(selectedAction);
        UpdateEmptyStateVisibility();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (EnabledActionsListBox.SelectedItem is not ButtonInfo selectedAction)
            return;

        AvailableActions.Add(selectedAction);
        EnabledActions.Remove(selectedAction);

        // Re-sort available actions by order number
        List<ButtonInfo> sortedAvailable = [.. AvailableActions.OrderBy(a => a.OrderNumber)];
        AvailableActions.Clear();
        foreach (ButtonInfo? action in sortedAvailable)
        {
            AvailableActions.Add(action);
        }

        UpdateEmptyStateVisibility();
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        int index = EnabledActionsListBox.SelectedIndex;
        if (index <= 0 || index >= EnabledActions.Count)
            return;

        ButtonInfo item = EnabledActions[index];
        EnabledActions.RemoveAt(index);
        EnabledActions.Insert(index - 1, item);
        EnabledActionsListBox.SelectedIndex = index - 1;
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        int index = EnabledActionsListBox.SelectedIndex;
        if (index < 0 || index >= EnabledActions.Count - 1)
            return;

        ButtonInfo item = EnabledActions[index];
        EnabledActions.RemoveAt(index);
        EnabledActions.Insert(index + 1, item);
        EnabledActionsListBox.SelectedIndex = index + 1;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show(
                "This will reset to the default post-grab actions. Continue?",
                "Reset to Defaults",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        // Get defaults
        List<ButtonInfo> defaults = PostGrabActionManager.GetDefaultPostGrabActions();

        // Clear and repopulate enabled list
        EnabledActions.Clear();
        foreach (ButtonInfo action in defaults)
        {
            EnabledActions.Add(action);
        }

        // Repopulate available list
        List<ButtonInfo> allActions = PostGrabActionManager.GetAvailablePostGrabActions();
        AvailableActions.Clear();
        List<ButtonInfo> availableActionsList = [.. allActions
            .Where(a => !defaults.Any(d => d.ButtonText == a.ButtonText))
            .OrderBy(a => a.OrderNumber)];

        foreach (ButtonInfo? action in availableActionsList)
        {
            AvailableActions.Add(action);
        }

        UpdateEmptyStateVisibility();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Save the enabled actions
        PostGrabActionManager.SavePostGrabActions([.. EnabledActions]);

        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateEmptyStateVisibility()
    {
        if (AvailableActions.Count == 0)
        {
            NoAvailableActionsText.Visibility = Visibility.Visible;
            AvailableActionsListBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            NoAvailableActionsText.Visibility = Visibility.Collapsed;
            AvailableActionsListBox.Visibility = Visibility.Visible;
        }
    }

    #endregion Methods
}
