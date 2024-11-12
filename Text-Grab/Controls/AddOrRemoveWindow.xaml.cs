using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for AddOrRemoveWindow.xaml
/// </summary>
public partial class AddOrRemoveWindow : Wpf.Ui.Controls.FluentWindow
{
    #region Fields

    public static RoutedCommand AddRemoveCmd = new();
    public static RoutedCommand ApplyCmd = new();

    #endregion Fields

    #region Constructors

    public AddOrRemoveWindow()
    {
        InitializeComponent();
    }

    #endregion Constructors

    #region Properties

    public AddRemove AddRemove { get; set; } = AddRemove.Add;
    public int? LengthToChange { get; set; }

    public string SelectedTextFromEditTextWindow { get; set; } = "";
    public SpotInLine SpotInLine { get; set; } = SpotInLine.Beginning;
    public string TextToAdd { get; set; } = "";

    #endregion Properties

    #region Methods

    private void AddRemove_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (AddRadioButton.IsChecked is true && !string.IsNullOrEmpty(TextToAddTextBox.Text))
            e.CanExecute = true;
        else if ((RemoveRadioButton.IsChecked is true || LimitRadioButton.IsChecked is true)
            && LengthToChange is not null)
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void AddRemove_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (Owner is not EditTextWindow etwOwner)
            return;

        Apply(etwOwner);

        Close();
    }

    private void Apply(EditTextWindow etwOwner)
    {
        if (AddRadioButton.IsChecked is true)
            AddText(etwOwner);
        else if (RemoveRadioButton.IsChecked is true)
            RemoveText(etwOwner);
        else
            LimitText(etwOwner);
    }

    private void LimitText(EditTextWindow etwOwner)
    {
        if (LengthToChange is null)
            return;

        if (BeginningRDBTN.IsChecked is true)
            etwOwner.LimitNumberOfCharsPerLine(LengthToChange.Value, SpotInLine.Beginning);
        else
            etwOwner.LimitNumberOfCharsPerLine(LengthToChange.Value, SpotInLine.End);

    }

    private void RemoveText(EditTextWindow etwOwner)
    {
        if (LengthToChange is null)
            return;

        if (BeginningRDBTN.IsChecked is true)
            etwOwner.RemoveCharsFromEditTextWindow(LengthToChange.Value, SpotInLine.Beginning);
        else
            etwOwner.RemoveCharsFromEditTextWindow(LengthToChange.Value, SpotInLine.End);
    }

    private void AddText(EditTextWindow etwOwner)
    {
        if (BeginningRDBTN.IsChecked is true)
            etwOwner.AddCharsToEditTextWindow(TextToAddTextBox.Text, SpotInLine.Beginning);
        else
            etwOwner.AddCharsToEditTextWindow(TextToAddTextBox.Text, SpotInLine.End);
    }

    private void Apply_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (Owner is not EditTextWindow etwOwner)
            return;

        Apply(etwOwner);
    }

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox addTextTextBox)
            return;

        if (AddRadioButton.IsChecked is true && addTextTextBox.Text is String textFromBox)
            TextToAdd = textFromBox;

        if (LengthTextBox.Text is String textFromLengthBox)
        {
            bool success = Int32.TryParse(textFromLengthBox, out int lengthString);

            if (!success)
                LengthToChange = null;
            else
                LengthToChange = lengthString;
        }
    }

    private void RemoveRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton removeRadioButton)
            return;

        if (removeRadioButton.IsChecked is true)
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

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrWhiteSpace(TextToAddTextBox.Text)
                || !string.IsNullOrEmpty(LengthTextBox.Text))
            {
                LengthTextBox.Clear();
                TextToAddTextBox.Clear();
            }
            else
                this.Close();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        TextToAddTextBox.Text = SelectedTextFromEditTextWindow;
        LengthTextBox.Text = SelectedTextFromEditTextWindow.Length.ToString();
    }

    #endregion Methods
}
