using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Controls;

/// <summary>Which pages within a range a multi-page grab should visit.</summary>
public enum GrabPageParity
{
    All,
    OddOnly,
    EvenOnly,
}

/// <summary>
/// Asks which pages of the loaded PDF a multi-page grab should cover. Page numbers are
/// 1-based in the UI; the resulting indices are 0-based to match the PDF renderer.
/// </summary>
public partial class GrabPageRangeDialog : FluentWindow
{
    private readonly int _pageCount;

    /// <summary>
    /// 0-based indices of the pages to grab, ascending, after the parity filter.
    /// Only meaningful when the dialog returned true.
    /// </summary>
    public List<int> PageIndices { get; private set; } = [];

    /// <summary>Whether the text from each page should be separated by an empty line.</summary>
    public bool InsertBlankLineBetweenPages => BlankLineBetweenPagesCheckBox.IsChecked is true;

    /// <summary>Restricts the range to odd- or even-numbered pages (1-based page numbers, as printed).</summary>
    public GrabPageParity PageParity =>
        PageParityPanel.Children.OfType<RadioButton>().FirstOrDefault(button => button.IsChecked == true)?.Tag switch
        {
            "odd" => GrabPageParity.OddOnly,
            "even" => GrabPageParity.EvenOnly,
            _ => GrabPageParity.All,
        };

    /// <summary>
    /// Keeps the 0-based page indices whose printed (1-based) page number matches the parity choice.
    /// </summary>
    public static List<int> FilterByParity(IEnumerable<int> pageIndices, GrabPageParity parity)
    {
        return [.. pageIndices.Where(pageIndex =>
        {
            int pageNumber = pageIndex + 1;
            return parity switch
            {
                GrabPageParity.OddOnly => pageNumber % 2 == 1,
                GrabPageParity.EvenOnly => pageNumber % 2 == 0,
                _ => true,
            };
        })];
    }

    public GrabPageRangeDialog(int currentPageIndex, int pageCount, bool isTableMode)
    {
        InitializeComponent();

        _pageCount = Math.Max(1, pageCount);
        int currentPageNumber = Math.Clamp(currentPageIndex + 1, 1, _pageCount);

        DescriptionTextBlock.Text = isTableMode
            ? "The current table boundary and settings are applied to every page in the range, and the results are joined into a new spreadsheet in an Edit Text Window."
            : "The current Grab Frame settings are applied to every page in the range, and the results are joined into a new Edit Text Window.";

        PagesTextBox.Text = currentPageNumber == _pageCount
            ? currentPageNumber.ToString()
            : $"{currentPageNumber}-{_pageCount}";
        AllPagesRadioButton.Content = $"All pages (1-{_pageCount})";
        PagesTextBox.SelectAll();
        PagesTextBox.Focus();

        foreach (RadioButton button in PageParityPanel.Children.OfType<RadioButton>())
        {
            button.Checked += (_, _) => ValidateRange();
        }
        // Tables usually want their rows to run together across pages; plain text reads
        // better with a gap between pages.
        BlankLineBetweenPagesCheckBox.IsChecked = !isTableMode;

        ValidateRange();
    }

    private void PagesTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateRange();
    }

    private void PagesTextBox_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        // Clicking into the range box means the user wants a range.
        RangeRadioButton.IsChecked = true;
    }

    private void PageScopeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (PagesTextBox is null || RangeRadioButton is null)
            return;

        PagesTextBox.IsEnabled = RangeRadioButton.IsChecked is true;
        if (PagesTextBox.IsEnabled)
            PagesTextBox.Focus();

        ValidateRange();
    }

    private bool ValidateRange()
    {
        if (PagesTextBox is null || PageParityPanel is null || RangeRadioButton is null)
            return false;

        List<int> parsed;
        if (RangeRadioButton.IsChecked is true)
        {
            if (!PageRangeParser.TryParse(PagesTextBox.Text, _pageCount, out parsed, out string error))
            {
                ShowRangeError(string.IsNullOrWhiteSpace(PagesTextBox.Text) ? "Enter a page range, like 4-9,12." : error);
                return false;
            }
        }
        else
        {
            parsed = [.. Enumerable.Range(0, _pageCount)];
        }

        List<int> filtered = FilterByParity(parsed, PageParity);
        if (filtered.Count == 0)
        {
            ShowRangeError("No pages match the selected range and odd/even choice.");
            return false;
        }

        HideRangeError();
        PageIndices = filtered;
        return true;
    }

    private void ShowRangeError(string message)
    {
        if (RangeErrorText is null || OkButton is null)
            return;

        RangeErrorText.Text = message;
        RangeErrorText.Visibility = Visibility.Visible;
        OkButton.IsEnabled = false;
    }

    private void HideRangeError()
    {
        if (RangeErrorText is null || OkButton is null)
            return;

        // Hidden (not Collapsed) keeps the space reserved so the window doesn't resize.
        RangeErrorText.Visibility = Visibility.Hidden;
        OkButton.IsEnabled = true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateRange())
            return;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
