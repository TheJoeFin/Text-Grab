using System;
using System.Collections.Generic;
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
        if (Owner is not EditTextWindow etw)
            return;

        double etwMidTop = etw.Top + (etw.Height / 2);
        double etwMidLeft = etw.Left + (etw.Width / 2);

        double thisMidTop = etwMidTop - (this.Height / 2);
        double thisMidLeft = etwMidLeft - (this.Width / 2);

        this.Top = etwMidTop;
        this.Left = thisMidLeft;
    }

    private void AddRemove_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (LengthToChange is null)
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void AddRemove_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (AddRadioButton.IsChecked == true)
        {
            if (BeginningRDBTN.IsChecked == true)
            {

            }
            else
            {

            }
        }
        else
        {
            if (BeginningRDBTN.IsChecked == true)
            {

            }
            else
            {

            }
        }
    }

    private void RemoveRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton removeRadioButton)
            return;

        if (removeRadioButton.IsChecked == true)
            TextToAddTextBox.IsEnabled = false;
        else
            TextToAddTextBox.IsEnabled = true;
    }

    private void LengthTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox lengthTextBox)
            return;

        if (lengthTextBox.Text is String textFromBox)
        {
            bool success = Int32.TryParse(textFromBox, out int lengthString);

            if (success == false)
                LengthToChange = null;
            else
                LengthToChange = lengthString;
        }
    }

    private void TextToAddTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox addTextTextBox)
            return;

        if (addTextTextBox.Text is String textFromBox)
            TextToAdd = textFromBox;
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
