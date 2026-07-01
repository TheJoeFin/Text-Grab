using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Controls;

public partial class TextOnlyTemplateDialog : FluentWindow
{
    /// <summary>When set, Save updates this template instead of creating a new one.</summary>
    public GrabTemplate? EditingTemplate { get; set; }

    public TextOnlyTemplateDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Activated += OnActivated;
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        // Refresh the pattern picker each time the dialog regains focus so patterns
        // created in the Regex Manager become available without reopening this dialog.
        if (IsLoaded)
            LoadPatternItems();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (EditingTemplate is not null)
        {
            Title = "Edit Text-Only Template";
            TitleBarControl.Title = "Edit Text-Only Template";
        }

        TemplateNameBox.Focus();
        LoadPatternItems();
        OutputTemplateBox.PatternItemSelected = OnPatternItemSelected;
    }

    private void LoadPatternItems()
    {
        StoredRegex[] patterns = AppUtilities.TextGrabSettingsService.LoadStoredRegexes();
        if (patterns.Length == 0)
            patterns = StoredRegex.GetDefaultPatterns();

        OutputTemplateBox.ItemsSource = [.. patterns.Select(p =>
            new InlinePickerItem(p.Name, $"{{p:{p.Name}:first}}", "Patterns"))];
    }

    private TemplatePatternMatch? OnPatternItemSelected(InlinePickerItem item)
    {
        StoredRegex[] patterns = AppUtilities.TextGrabSettingsService.LoadStoredRegexes();
        if (patterns.Length == 0)
            patterns = StoredRegex.GetDefaultPatterns();

        StoredRegex? storedRegex = patterns.FirstOrDefault(
            p => p.Name.Equals(item.DisplayName, StringComparison.OrdinalIgnoreCase));

        PatternMatchModeDialog dialog = new(storedRegex?.Id ?? string.Empty, item.DisplayName)
        {
            Owner = this,
        };

        return dialog.ShowDialog() is true ? dialog.Result : null;
    }

    private void ManagePatternsButton_Click(object sender, RoutedEventArgs e)
    {
        // Open the Regex Manager so the user can create a new pattern. When they return
        // focus to this dialog, OnActivated reloads the picker so the new pattern is usable.
        RegexManager regexManager = WindowUtilities.OpenOrActivateWindow<RegexManager>();
        regexManager.Show();
        regexManager.Activate();
    }

    private void ValidateInput(object sender, TextChangedEventArgs e) => UpdateSaveButton();

    private void OutputTemplateBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateSaveButton();

    private void UpdateSaveButton()
    {
        if (SaveButton is null)
            return;

        bool nameOk = !string.IsNullOrWhiteSpace(TemplateNameBox.Text);
        bool templateOk = !string.IsNullOrWhiteSpace(OutputTemplateBox.GetSerializedText());
        SaveButton.IsEnabled = nameOk && templateOk;

        if (ErrorText is not null)
            ErrorText.Visibility = Visibility.Collapsed;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string name = TemplateNameBox.Text.Trim();
        string outputTemplate = OutputTemplateBox.GetSerializedText();

        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorText.Text = "Template name is required.";
            ErrorText.Visibility = Visibility.Visible;
            TemplateNameBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(outputTemplate))
        {
            ErrorText.Text = "Output template is required.";
            ErrorText.Visibility = Visibility.Visible;
            OutputTemplateBox.Focus();
            return;
        }

        GrabTemplate newTemplate = EditingTemplate ?? new();
        newTemplate.Name = name;
        newTemplate.OutputTemplate = outputTemplate;
        newTemplate.PatternMatches = GrabTemplateExecutor.ParsePatternMatchesFromOutputTemplate(outputTemplate);

        GrabTemplateManager.AddOrUpdateTemplate(newTemplate);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
