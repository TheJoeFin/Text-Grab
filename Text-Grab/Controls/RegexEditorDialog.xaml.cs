using System;
using System.Collections.Generic;
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
        RegexReferenceList.ItemsSource = BuildRegexReference();
    }

    public RegexEditorDialog(StoredRegex regexToEdit)
    {
        InitializeComponent();
        RegexReferenceList.ItemsSource = BuildRegexReference();
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

    /// <summary>
    /// Inserts the clicked reference token into the pattern box at the current caret
    /// position (replacing any selection), then returns focus to the pattern box.
    /// </summary>
    private void InsertToken_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.Button { Tag: string token } || string.IsNullOrEmpty(token))
            return;

        int caret = PatternTextBox.CaretIndex;
        if (PatternTextBox.SelectionLength > 0)
        {
            caret = PatternTextBox.SelectionStart;
            PatternTextBox.SelectedText = token;
        }
        else
        {
            PatternTextBox.Text = PatternTextBox.Text.Insert(caret, token);
        }

        PatternTextBox.CaretIndex = caret + token.Length;
        PatternTextBox.Focus();
    }

    private static List<RegexReferenceCategory> BuildRegexReference() =>
    [
        new("Character classes",
        [
            new(@"\d", "Any digit (0–9)"),
            new(@"\D", "Any non-digit"),
            new(@"\w", "Word character: letter, digit, or underscore"),
            new(@"\W", "Any non-word character"),
            new(@"\s", "Any whitespace (space, tab, newline)"),
            new(@"\S", "Any non-whitespace character"),
            new(".", "Any single character (except newline)"),
            new("[abc]", "Any one of the listed characters"),
            new("[^abc]", "Any character NOT listed"),
            new("[a-z]", "Any character in the range a to z"),
        ]),
        new("Anchors & boundaries",
        [
            new("^", "Start of the line/string"),
            new("$", "End of the line/string"),
            new(@"\b", "Word boundary (edge of a word)"),
            new(@"\B", "Not a word boundary"),
        ]),
        new("Quantifiers",
        [
            new("*", "Zero or more of the preceding item"),
            new("+", "One or more of the preceding item"),
            new("?", "Zero or one (makes it optional)"),
            new("{3}", "Exactly 3 of the preceding item"),
            new("{2,}", "2 or more of the preceding item"),
            new("{2,5}", "Between 2 and 5 of the preceding item"),
            new("*?", "Lazy: as few as possible (also +? and ??)"),
        ]),
        new("Groups & alternation",
        [
            new("(...)", "Capture group — remembers the match"),
            new("(?:...)", "Group without capturing"),
            new("(?<name>...)", "Named capture group"),
            new("a|b", "Match either a or b"),
        ]),
        new("Lookaround (match context without consuming it)",
        [
            new("(?=...)", "Lookahead: followed by ... (text after a match)"),
            new("(?!...)", "Negative lookahead: NOT followed by ..."),
            new("(?<=...)", "Lookbehind: preceded by ... (text before a match)"),
            new("(?<!...)", "Negative lookbehind: NOT preceded by ..."),
        ]),
        new("Escapes & literals",
        [
            new(@"\.", "A literal dot (escape special characters with \\)"),
            new(@"\\", "A literal backslash"),
            new(@"\n", "Newline"),
            new(@"\t", "Tab"),
        ]),
    ];

    /// <summary>A named group of regex reference rows shown in the quick-reference expander.</summary>
    public sealed record RegexReferenceCategory(string CategoryName, IReadOnlyList<RegexReferenceItem> Items);

    /// <summary>A single regex token and a plain-language description of what it does.</summary>
    public sealed record RegexReferenceItem(string Token, string Description);
}
