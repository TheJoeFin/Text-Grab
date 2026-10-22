using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

    /// <summary>0-based index of the first page to grab. Only meaningful when the dialog returned true.</summary>
    public int FirstPageIndex { get; private set; }

    /// <summary>0-based index of the last page to grab (inclusive). Only meaningful when the dialog returned true.</summary>
    public int LastPageIndex { get; private set; }

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
    /// The 0-based page indices the grab should visit, in order, honoring the parity choice.
    /// </summary>
    public static List<int> BuildPageIndices(int firstPageIndex, int lastPageIndex, GrabPageParity parity)
    {
        List<int> pageIndices = [];
        for (int pageIndex = firstPageIndex; pageIndex <= lastPageIndex; pageIndex++)
        {
            int pageNumber = pageIndex + 1;
            bool include = parity switch
            {
                GrabPageParity.OddOnly => pageNumber % 2 == 1,
                GrabPageParity.EvenOnly => pageNumber % 2 == 0,
                _ => true,
            };

            if (include)
                pageIndices.Add(pageIndex);
        }

        return pageIndices;
    }

    public GrabPageRangeDialog(int currentPageIndex, int pageCount, bool isTableMode)
    {
        InitializeComponent();

        _pageCount = Math.Max(1, pageCount);
        int currentPageNumber = Math.Clamp(currentPageIndex + 1, 1, _pageCount);

        DescriptionTextBlock.Text = isTableMode
            ? "The current table boundary and settings are applied to every page in the range, and the results are joined into a new spreadsheet in an Edit Text Window."
            : "The current Grab Frame settings are applied to every page in the range, and the results are joined into a new Edit Text Window.";

        FromPageTextBox.Text = currentPageNumber.ToString();
        ToPageTextBox.Text = _pageCount.ToString();
        // Tables usually want their rows to run together across pages; plain text reads
        // better with a gap between pages.
        BlankLineBetweenPagesCheckBox.IsChecked = !isTableMode;

        ValidateRange();
    }

    private void PageTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateRange();
    }

    private bool ValidateRange()
    {
        if (FromPageTextBox is null || ToPageTextBox is null)
            return false;

        if (!int.TryParse(FromPageTextBox.Text.Trim(), out int fromPage)
            || !int.TryParse(ToPageTextBox.Text.Trim(), out int toPage))
        {
            ShowRangeError("Enter whole page numbers.");
            return false;
        }

        if (fromPage < 1 || toPage < 1 || fromPage > _pageCount || toPage > _pageCount)
        {
            ShowRangeError($"Pages must be between 1 and {_pageCount}.");
            return false;
        }

        if (fromPage > toPage)
        {
            ShowRangeError("The first page must come before the last page.");
            return false;
        }

        HideRangeError();
        FirstPageIndex = fromPage - 1;
        LastPageIndex = toPage - 1;
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

        RangeErrorText.Visibility = Visibility.Collapsed;
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
