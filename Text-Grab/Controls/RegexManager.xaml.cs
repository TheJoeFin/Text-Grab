using Humanizer;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Controls;

public partial class RegexManager : FluentWindow
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private ObservableCollection<StoredRegex> RegexPatterns { get; set; } = [];

    public RegexManager()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadRegexPatterns();
        RegexDataGrid.ItemsSource = RegexPatterns;
    }

    private void LoadRegexPatterns()
    {
        RegexPatterns.Clear();

        // Load from settings
        string regexListJson = DefaultSettings.RegexList;

        if (!string.IsNullOrWhiteSpace(regexListJson))
        {
            try
            {
                StoredRegex[]? loadedPatterns = JsonSerializer.Deserialize<StoredRegex[]>(regexListJson);
                if (loadedPatterns is not null)
                {
                    foreach (StoredRegex pattern in loadedPatterns)
                        RegexPatterns.Add(pattern);
                }
            }
            catch (JsonException)
            {
                // If deserialization fails, start fresh
            }
        }

        // Add default patterns if list is empty
        if (RegexPatterns.Count == 0)
        {
            foreach (StoredRegex defaultPattern in StoredRegex.GetDefaultPatterns())
                RegexPatterns.Add(defaultPattern);

            SaveRegexPatterns();
        }
    }

    private void SaveRegexPatterns()
    {
        try
        {
            string json = JsonSerializer.Serialize(RegexPatterns.ToArray());
            DefaultSettings.RegexList = json;
            DefaultSettings.Save();
        }
        catch (Exception)
        {
            // Handle save error silently or show message
        }
    }

    private void RegexDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        bool hasSelection = RegexDataGrid.SelectedItem is not null;

        EditButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
        ExplainButton.IsEnabled = hasSelection;
        UseButton.IsEnabled = hasSelection;

        if (hasSelection)
            TestPattern();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        RegexEditorDialog dialog = new()
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.EditedRegex is not null)
        {
            RegexPatterns.Add(dialog.EditedRegex);
            SaveRegexPatterns();
            RegexDataGrid.SelectedItem = dialog.EditedRegex;
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (RegexDataGrid.SelectedItem is not StoredRegex selectedRegex)
            return;

        RegexEditorDialog dialog = new(selectedRegex)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.EditedRegex is not null)
        {
            int index = RegexPatterns.IndexOf(selectedRegex);
            if (index >= 0)
            {
                RegexPatterns[index] = dialog.EditedRegex;
                SaveRegexPatterns();
                RegexDataGrid.Items.Refresh();
            }
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (RegexDataGrid.SelectedItem is not StoredRegex selectedRegex)
            return;

        Wpf.Ui.Controls.MessageBoxResult result = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Delete Pattern",
            Content = $"Are you sure you want to delete the pattern '{selectedRegex.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel"
        }.ShowDialogAsync().Result;

        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            RegexPatterns.Remove(selectedRegex);
            SaveRegexPatterns();
        }
    }

    private void UseButton_Click(object sender, RoutedEventArgs e)
    {
        if (RegexDataGrid.SelectedItem is not StoredRegex selectedRegex)
            return;

        // Update last used date
        selectedRegex.LastUsedDate = DateTimeOffset.Now;
        SaveRegexPatterns();

        // Open Find and Replace window with this pattern
        FindAndReplaceWindow findWindow = WindowUtilities.OpenOrActivateWindow<FindAndReplaceWindow>();
        findWindow.FindTextBox.Text = selectedRegex.Pattern;
        findWindow.UsePaternCheckBox.IsChecked = true;
        findWindow.Show();
        findWindow.Activate();
        findWindow.SearchForText();
    }

    /// <summary>
    /// Opens the Regex Manager in "add mode" with a pre-filled pattern
    /// </summary>
    public void AddPatternFromText(string pattern, string sourceText)
    {
        RegexEditorDialog dialog = new()
        {
            Owner = this
        };

        // Pre-fill the pattern field
        dialog.PatternTextBox.Text = pattern;
        dialog.NameTextBox.Text = $"Pattern from '{sourceText.MakeStringSingleLine().Truncate(30)}'";

        if (dialog.ShowDialog() == true && dialog.EditedRegex is not null)
        {
            RegexPatterns.Add(dialog.EditedRegex);
            SaveRegexPatterns();
            RegexDataGrid.SelectedItem = dialog.EditedRegex;
        }
    }

    private void TestTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // Hide placeholder when there's text
        if (TestTextBox.Text.Length > 0)
            TestTextPlaceholder.Visibility = Visibility.Collapsed;
        else
            TestTextPlaceholder.Visibility = Visibility.Visible;

        TestPattern();
    }

    private void TestPattern()
    {
        if (!IsLoaded)
            return;

        if (RegexDataGrid.SelectedItem is not StoredRegex selectedRegex)
        {
            MatchCountText.Text = "0";
            return;
        }

        string testText = TestTextBox.Text;
        if (string.IsNullOrEmpty(testText))
        {
            MatchCountText.Text = "0";
            return;
        }

        try
        {
            MatchCollection matches = Regex.Matches(testText, selectedRegex.Pattern, RegexOptions.Multiline);
            MatchCountText.Text = matches.Count.ToString();
        }
        catch (ArgumentException)
        {
            MatchCountText.Text = "Invalid Pattern";
        }
    }

    private void ExplainButton_Click(object sender, RoutedEventArgs e)
    {
        if (RegexDataGrid.SelectedItem is not StoredRegex selectedRegex)
            return;

        string explanation = StringMethods.ExplainRegexPattern(selectedRegex.Pattern);

        Wpf.Ui.Controls.MessageBox messageBox = new()
        {
            Title = "Regex Pattern Explanation",
            Content = explanation,
            CloseButtonText = "Close"
        };
        _ = messageBox.ShowDialogAsync();
    }

    private void ShowTestToggle_Click(object sender, RoutedEventArgs e)
    {
        if (ShowTestToggle.IsChecked == true)
        {
            TestPanel.Visibility = Visibility.Visible;
            ShowTestToggle.Content = "Hide Test";
        }
        else
        {
            TestPanel.Visibility = Visibility.Collapsed;
            ShowTestToggle.Content = "Show Test";
        }
    }

    private void FluentWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveRegexPatterns();
    }
}
