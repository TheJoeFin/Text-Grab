using Humanizer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Controls;

public partial class RegexManager : FluentWindow
{
    public EditTextWindow? SourceEditTextWindow;

    private ObservableCollection<StoredRegex> RegexPatterns { get; set; } = [];
    private ObservableCollection<PatternItem> DisplayedPatterns { get; set; } = [];
    private HashSet<string> HiddenRecognizerIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public RegexManager()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadRegexPatterns();
        HiddenRecognizerIds = [.. AppUtilities.TextGrabSettingsService.LoadHiddenSmartPatternIds()];
        RebuildDisplayedPatterns();

        RegexDataGrid.ItemsSource = DisplayedPatterns;
        RegexDataGrid.Items.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PatternItem.GroupLabel)));
    }

    private void LoadRegexPatterns()
    {
        RegexPatterns.Clear();
        StoredRegex[] loadedPatterns = AppUtilities.TextGrabSettingsService.LoadStoredRegexes();
        foreach (StoredRegex pattern in loadedPatterns)
            RegexPatterns.Add(pattern);

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
        AppUtilities.TextGrabSettingsService.SaveStoredRegexes(RegexPatterns);
    }

    private void SaveHiddenRecognizerIds()
    {
        AppUtilities.TextGrabSettingsService.SaveHiddenSmartPatternIds(HiddenRecognizerIds);
    }

    /// <summary>Rebuilds the combined saved-regex + recognizer list shown in the grid. Does not touch selection.</summary>
    private void RebuildDisplayedPatterns()
    {
        DisplayedPatterns.Clear();
        foreach (StoredRegex regex in RegexPatterns)
            DisplayedPatterns.Add(new PatternItem(regex));
        foreach (BuiltInRecognizer recognizer in BuiltInRecognizer.GetAll())
            DisplayedPatterns.Add(new PatternItem(recognizer, isHidden: HiddenRecognizerIds.Contains(recognizer.Id)));
    }

    private void SelectPatternById(string id)
    {
        RegexDataGrid.SelectedItem = DisplayedPatterns.FirstOrDefault(p => p.Id == id);
    }

    private void RegexDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        PatternItem? selected = RegexDataGrid.SelectedItem as PatternItem;
        bool isSavedRegex = selected?.Kind == PatternKind.SavedRegex;
        bool isRecognizer = selected?.Kind == PatternKind.Recognizer;

        EditButton.IsEnabled = isSavedRegex;
        UseButton.IsEnabled = isSavedRegex;
        ExplainButton.IsEnabled = selected is not null;

        DeleteButton.Visibility = isRecognizer ? Visibility.Collapsed : Visibility.Visible;
        DeleteButton.IsEnabled = isSavedRegex;

        HideButton.Visibility = isRecognizer ? Visibility.Visible : Visibility.Collapsed;
        HideButton.IsEnabled = isRecognizer;
        if (isRecognizer && selected is not null)
        {
            if (selected.IsHidden)
            {
                HideButton.Content = "Unhide";
                HideButton.Icon = new SymbolIcon(SymbolRegular.Eye24);
            }
            else
            {
                HideButton.Content = "Hide";
                HideButton.Icon = new SymbolIcon(SymbolRegular.EyeOff24);
            }
        }

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
            RebuildDisplayedPatterns();
            SelectPatternById(dialog.EditedRegex.Id);
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (RegexDataGrid.SelectedItem is not PatternItem { Kind: PatternKind.SavedRegex, SavedRegex: StoredRegex selectedRegex })
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
                RebuildDisplayedPatterns();
                SelectPatternById(dialog.EditedRegex.Id);
            }
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (RegexDataGrid.SelectedItem is not PatternItem { Kind: PatternKind.SavedRegex, SavedRegex: StoredRegex selectedRegex })
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
            RebuildDisplayedPatterns();
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        if (RegexDataGrid.SelectedItem is not PatternItem { Kind: PatternKind.Recognizer } selected)
            return;

        if (selected.IsHidden)
            HiddenRecognizerIds.Remove(selected.Id);
        else
            HiddenRecognizerIds.Add(selected.Id);

        SaveHiddenRecognizerIds();
        RebuildDisplayedPatterns();
        SelectPatternById(selected.Id);
    }

    private void UseButton_Click(object sender, RoutedEventArgs e)
    {
        if (RegexDataGrid.SelectedItem is not PatternItem { Kind: PatternKind.SavedRegex, SavedRegex: StoredRegex selectedRegex })
            return;

        // Update last used date
        selectedRegex.LastUsedDate = DateTimeOffset.Now;
        SaveRegexPatterns();

        // Open Find and Replace window with this pattern
        FindAndReplaceWindow findWindow = WindowUtilities.OpenOrActivateWindow<FindAndReplaceWindow>();
        findWindow.TextEditWindow ??= SourceEditTextWindow;
        findWindow.SetFindText(selectedRegex.Pattern, useRegex: true);
        findWindow.Show();
        findWindow.Activate();
        findWindow.SearchForText();

        // Close the Patterns Manager after opening Find and Replace
        Close();
    }

    /// <summary>
    /// Opens the Patterns Manager in "add mode" with a pre-filled pattern
    /// </summary>
    public void AddPatternFromText(string pattern, string sourceText, EditTextWindow? source = null)
    {
        SourceEditTextWindow = source;
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
            RebuildDisplayedPatterns();
            SelectPatternById(dialog.EditedRegex.Id);
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

        if (RegexDataGrid.SelectedItem is not PatternItem selected)
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

        if (selected.Kind == PatternKind.SavedRegex && !IsValidRegexPattern(selected.SavedRegex?.Pattern))
        {
            MatchCountText.Text = "Invalid Pattern";
            return;
        }

        MatchCountText.Text = PatternExecutor.GetMatches(selected, testText).Count.ToString();
    }

    private static bool IsValidRegexPattern(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return true;

        try
        {
            _ = new System.Text.RegularExpressions.Regex(pattern);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void ExplainButton_Click(object sender, RoutedEventArgs e)
    {
        if (RegexDataGrid.SelectedItem is not PatternItem selected)
            return;

        string explanation = selected.Kind == PatternKind.SavedRegex && selected.SavedRegex is not null
            ? StringMethods.ExplainRegexPattern(selected.SavedRegex.Pattern)
            : selected.Description;

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
