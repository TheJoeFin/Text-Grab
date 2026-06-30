using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Controls;

/// <summary>
/// Dialog shown after the user selects a regex pattern (or recognizer) from the inline picker.
/// Lets them choose match mode (first, last, all, specific indices) and separator, and — for
/// recognizers — whether to emit the resolved value or the matched text.
/// </summary>
public partial class PatternMatchModeDialog : FluentWindow
{
    /// <summary>
    /// The configured result. Null if the user cancelled.
    /// </summary>
    public TemplatePatternMatch? Result { get; private set; }

    /// <summary>The chosen output kind (only meaningful for recognizers).</summary>
    public RecognizerOutputKind SelectedOutputKind { get; private set; } = RecognizerOutputKind.ResolvedValue;

    private readonly string _patternId;
    private readonly string _patternName;

    public PatternMatchModeDialog(string patternId, string patternName)
    {
        InitializeComponent();
        _patternId = patternId;
        _patternName = patternName;
        PatternNameLabel.Text = $"Pattern: {patternName}";
    }

    /// <summary>
    /// Creates the dialog for a recognizer, optionally showing the resolved-value /
    /// matched-text output selector.
    /// </summary>
    public PatternMatchModeDialog(string recognizerId, string recognizerName, bool isRecognizer)
        : this(recognizerId, recognizerName)
    {
        if (!isRecognizer)
            return;

        Title = "Pattern Match Options";
        DialogTitleBar.Title = "Pattern Match Options";
        PatternNameLabel.Text = $"Pattern: {recognizerName}";
        OutputPanel.Visibility = Visibility.Visible;
    }

    private void MatchModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (SeparatorPanel == null || IndicesPanel == null)
            return;

        string mode = GetSelectedMode();

        SeparatorPanel.Visibility = mode is "all" or "nth" ? Visibility.Visible : Visibility.Collapsed;
        IndicesPanel.Visibility = mode == "nth" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void IndicesTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateIndices();
    }

    private bool ValidateIndices()
    {
        if (IndicesTextBox == null)
            return true;

        string text = IndicesTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ShowIndicesError("At least one index is required.");
            return false;
        }

        string[] parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            ShowIndicesError("At least one index is required.");
            return false;
        }

        foreach (string part in parts)
        {
            if (!int.TryParse(part, out int val) || val < 1)
            {
                ShowIndicesError($"\"{part}\" is not a valid positive integer.");
                return false;
            }
        }

        HideIndicesError();
        return true;
    }

    private void ShowIndicesError(string message)
    {
        if (IndicesErrorText == null || OkButton == null)
            return;
        IndicesErrorText.Text = message;
        IndicesErrorText.Visibility = Visibility.Visible;
        OkButton.IsEnabled = false;
    }

    private void HideIndicesError()
    {
        if (IndicesErrorText == null || OkButton == null)
            return;
        IndicesErrorText.Visibility = Visibility.Collapsed;
        OkButton.IsEnabled = true;
    }

    private string GetSelectedMode()
    {
        if (MatchModePanel?.Children.OfType<RadioButton>().FirstOrDefault(button => button.IsChecked == true) is RadioButton button)
            return button.Tag?.ToString() ?? "first";

        return "first";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        string mode = GetSelectedMode();
        string separator = SeparatorTextBox.Text;

        if (mode == "nth")
        {
            if (!ValidateIndices())
                return;
            mode = IndicesTextBox.Text.Trim();
        }

        SelectedOutputKind = OutputValueRadio.IsChecked is false
            ? RecognizerOutputKind.MatchedText
            : RecognizerOutputKind.ResolvedValue;

        Result = new TemplatePatternMatch(_patternId, _patternName, mode, separator);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
