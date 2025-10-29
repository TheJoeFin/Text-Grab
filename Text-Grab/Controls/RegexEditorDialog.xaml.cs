using System;
using System.Text.RegularExpressions;
using System.Windows;
using Text_Grab.Models;
using Wpf.Ui.Controls;

namespace Text_Grab.Controls;

public partial class RegexEditorDialog : FluentWindow
{
    public StoredRegex? EditedRegex { get; private set; }
    private StoredRegex? _originalRegex;

    public RegexEditorDialog()
    {
 InitializeComponent();
   _originalRegex = null;
    }

    public RegexEditorDialog(StoredRegex regexToEdit)
    {
 InitializeComponent();
        _originalRegex = regexToEdit;

     // Populate fields
        NameTextBox.Text = regexToEdit.Name;
        PatternTextBox.Text = regexToEdit.Pattern;
        DescriptionTextBox.Text = regexToEdit.Description;

        Title = "Edit Regex Pattern";
        ValidateInput(null, null);
    }

    private void ValidateInput(object? sender, System.Windows.Controls.TextChangedEventArgs? e)
    {
        bool isValid = true;
        string errorMessage = string.Empty;

        // Validate name
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
 isValid = false;
          errorMessage = "Name is required";
        }
        // Validate pattern
        else if (string.IsNullOrWhiteSpace(PatternTextBox.Text))
        {
            isValid = false;
            errorMessage = "Pattern is required";
   }
        else
        {
            // Test if pattern is valid regex
    try
            {
   _ = new Regex(PatternTextBox.Text);
  }
     catch (ArgumentException)
            {
  isValid = false;
    errorMessage = "Invalid regular expression pattern";
   }
        }

     SaveButton.IsEnabled = isValid;

    if (!isValid && !string.IsNullOrEmpty(errorMessage))
        {
            ErrorText.Text = errorMessage;
       ErrorText.Visibility = Visibility.Visible;
  }
        else
        {
         ErrorText.Visibility = Visibility.Collapsed;
        }
    }

  private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_originalRegex is not null)
        {
      // Editing existing pattern
      EditedRegex = new StoredRegex
          {
   Id = _originalRegex.Id,
         Name = NameTextBox.Text.Trim(),
            Pattern = PatternTextBox.Text.Trim(),
        Description = DescriptionTextBox.Text.Trim(),
          IsDefault = _originalRegex.IsDefault,
     CreatedDate = _originalRegex.CreatedDate,
         LastUsedDate = _originalRegex.LastUsedDate
        };
}
     else
     {
            // Creating new pattern
            EditedRegex = new StoredRegex
         {
       Name = NameTextBox.Text.Trim(),
      Pattern = PatternTextBox.Text.Trim(),
    Description = DescriptionTextBox.Text.Trim(),
    IsDefault = false
     };
        }

        DialogResult = true;
     Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
