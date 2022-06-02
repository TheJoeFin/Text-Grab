using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for AddOrRemoveWindow.xaml
/// </summary>
public partial class AddOrRemoveWindow : Window
{
    public static RoutedCommand AddRemoveCmd = new();

    public int? LengthToChange { get; set; }

    public string TextToAdd { get; set; } = "";

    public SpotInLine SpotInLine { get; set; } = SpotInLine.Beginning;

    public AddRemove AddRemove { get; set; } = AddRemove.Add;

    public AddOrRemoveWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner is not Window etw)
            return;

        double etwMidTop = etw.Top + (etw.Height / 2);
        double etwMidLeft = etw.Left + (etw.Width / 2);

        double thisMidTop = etwMidTop - (this.Height / 2);
        double thisMidLeft = etwMidLeft - (this.Width / 2);

        this.Top = thisMidTop;
        this.Left = thisMidLeft;
    }

    private void AddRemove_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (AddRadioButton.IsChecked == true && string.IsNullOrEmpty(TextToAddTextBox.Text) == false)
            e.CanExecute = true;
        else if (RemoveRadioButton.IsChecked == true && LengthToChange is not null)
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void AddRemove_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (Owner is not EditTextWindow etwOwner)
            return;

        if (AddRadioButton.IsChecked == true)
        {
            if (BeginningRDBTN.IsChecked == true)
            {
                etwOwner.AddCharsToEachLine(TextToAddTextBox.Text, SpotInLine.Beginning);
            }
            else
            {
                etwOwner.AddCharsToEachLine(TextToAddTextBox.Text, SpotInLine.End);
            }
        }
        else
        {
            if (LengthToChange is null)
                return;

            if (BeginningRDBTN.IsChecked == true)
            {
                etwOwner.RemoveCharsFromEachLine(LengthToChange.Value, SpotInLine.Beginning);
            }
            else
            {
                etwOwner.RemoveCharsFromEachLine(LengthToChange.Value, SpotInLine.End);
            }
        }

        Close();
    }

    private void RemoveRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton removeRadioButton)
            return;

        if (removeRadioButton.IsChecked == true)
        {
            TextToAddTextBox.IsEnabled = false;
            LengthTextBox.IsEnabled = true;
        }
        else
        {
            TextToAddTextBox.IsEnabled = true;
            LengthTextBox.IsEnabled = false;
        }
    }

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox addTextTextBox)
            return;

        if (AddRadioButton.IsChecked == true)
        {
            if (addTextTextBox.Text is String textFromBox)
                TextToAdd = textFromBox;

        }
        else
        {
            if (LengthTextBox.Text is String textFromBox)
            {
                bool success = Int32.TryParse(textFromBox, out int lengthString);

                if (success == false)
                    LengthToChange = null;
                else
                    LengthToChange = lengthString;
            }
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (string.IsNullOrWhiteSpace(TextToAddTextBox.Text) == false || string.IsNullOrEmpty(LengthTextBox.Text) == false)
            {
                LengthTextBox.Clear();
                TextToAddTextBox.Clear();
            }
            else
                this.Close();
        }
    }
}
