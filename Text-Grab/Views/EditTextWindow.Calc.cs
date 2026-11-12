using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Text_Grab;

public partial class EditTextWindow
{
    private void SyncCalcScrollToMain()
    {
        try
        {
            if (CalcResultsTextControl.Visibility != Visibility.Visible)
                return;

            ScrollViewer? mainSv = WindowUtilities.GetScrollViewer(PassedTextControl);
            ScrollViewer? calcSv = WindowUtilities.GetScrollViewer(CalcResultsTextControl);
            if (mainSv is null || calcSv is null)
                return;

            if (!NumericUtilities.AreClose(calcSv.VerticalOffset, mainSv.VerticalOffset))
                calcSv.ScrollToVerticalOffset(mainSv.VerticalOffset);
        }
        catch { /* no-op */ }
    }

    // Aggregate tracking for calc pane status display
    private enum AggregateType { None, Sum, Average, Count, Min, Max, Median, Product }

    private void InitializeExpressionEvaluator()
    {
        // Set up debounce timer to avoid excessive calculations
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DEBOUNCE_DELAY_MS)
        };
        _debounceTimer.Tick += DebounceTimer_Tick;
    }

    private async void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();

        if (CalcResultsTextControl.Visibility != Visibility.Visible)
            return;

        await EvaluateExpressions();
    }

    private async Task EvaluateExpressions()
    {
        // Don't waste cycles if the pane isn't visible
        if (CalcResultsTextControl.Visibility != Visibility.Visible)
            return;

        string input = Session.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            calculationResult = null;
            UpdateCalcPaneTextDisplay();
            _calculationService.ClearParameters();
            UpdateAggregateStatusDisplay();
            // Keep scrolls aligned even when clearing
            await Dispatcher.InvokeAsync(SyncCalcScrollToMain, DispatcherPriority.Render);
            return;
        }

        // Update calculation service settings
        _calculationService.CultureInfo = selectedCultureInfo ?? CultureInfo.CurrentCulture;
        _calculationService.ShowErrors = ShowErrorsMenuItem.IsChecked == true;

        // Evaluate expressions using the service
        calculationResult = await _calculationService.EvaluateExpressionsAsync(input);

        // Update the text control with results or current spreadsheet selection preview
        UpdateCalcPaneTextDisplay();

        // Update the aggregate status display if an aggregate is selected
        UpdateAggregateStatusDisplay();

        // After updating calc text, its ScrollViewer resets; resync to main scroll
        await Dispatcher.InvokeAsync(SyncCalcScrollToMain, DispatcherPriority.Render);

        // Optional status (kept commented)
        // if (result.ErrorCount == 0) { } else { }
    }

    private async void ShowErrorsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Re-evaluate expressions when toggle changes
        await EvaluateExpressions();
    }

    private void HideCalcPaneContextItem_Click(object sender, RoutedEventArgs e)
    {
        ShowCalcPaneMenuItem.IsChecked = false;
        DefaultSettings.CalcShowPane = false;
        SetCalcPaneVisibility();
    }

    private void ShowCalcErrorsContextItem_Click(object sender, RoutedEventArgs e)
    {
        ShowErrorsMenuItem.IsChecked = !ShowErrorsMenuItem.IsChecked;
        DefaultSettings.CalcShowErrors = ShowErrorsMenuItem.IsChecked;
        _ = EvaluateExpressions();
    }

    private void CopyAllContextItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CalcResultsTextControl.Text))
            return;

        try
        {
            System.Windows.Clipboard.SetDataObject(CalcResultsTextControl.Text, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy calc results to clipboard: {ex.Message}");
        }
    }

    private void CalcResultsTextControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        UpdateCalcAggregates();
    }

    private void UpdateCalcAggregates()
    {
        List<double> numbers = GetCurrentAggregateNumbers();

        // Update menu items based on whether we have numbers
        if (numbers.Count == 0)
        {
            ShowSumContextItem.Header = "Sum: -";
            ShowAverageContextItem.Header = "Average: -";
            ShowMedianContextItem.Header = "Median: -";
            ShowCountContextItem.Header = "Count: 0";
            ShowMinContextItem.Header = "Min: -";
            ShowMaxContextItem.Header = "Max: -";
            ShowProductContextItem.Header = "Product: -";

            ShowSumContextItem.IsEnabled = false;
            ShowAverageContextItem.IsEnabled = false;
            ShowMedianContextItem.IsEnabled = false;
            ShowCountContextItem.IsEnabled = false;
            ShowMinContextItem.IsEnabled = false;
            ShowMaxContextItem.IsEnabled = false;
            ShowProductContextItem.IsEnabled = false;
        }
        else
        {
            double sum = numbers.Sum();
            double average = numbers.Average();
            double median = NumericUtilities.CalculateMedian(numbers);
            int count = numbers.Count;
            double min = numbers.Min();
            double max = numbers.Max();
            double product = numbers.Aggregate(1.0, (acc, val) => acc * val);

            ShowSumContextItem.Header = $"Sum: {NumericUtilities.FormatNumber(sum)}";
            ShowAverageContextItem.Header = $"Average: {NumericUtilities.FormatNumber(average)}";
            ShowMedianContextItem.Header = $"Median: {NumericUtilities.FormatNumber(median)}";
            ShowCountContextItem.Header = $"Count: {count}";
            ShowMinContextItem.Header = $"Min: {NumericUtilities.FormatNumber(min)}";
            ShowMaxContextItem.Header = $"Max: {NumericUtilities.FormatNumber(max)}";
            ShowProductContextItem.Header = $"Product: {NumericUtilities.FormatNumber(product)}";

            ShowSumContextItem.IsEnabled = true;
            ShowAverageContextItem.IsEnabled = true;
            ShowMedianContextItem.IsEnabled = true;
            ShowCountContextItem.IsEnabled = true;
            ShowMinContextItem.IsEnabled = true;
            ShowMaxContextItem.IsEnabled = true;
            ShowProductContextItem.IsEnabled = true;

            // Wire up click handlers to copy values and track selection
            ShowSumContextItem.Click -= SelectAggregate_Click;
            ShowSumContextItem.Click += SelectAggregate_Click;
            ShowSumContextItem.Tag = (AggregateType.Sum, sum);

            ShowAverageContextItem.Click -= SelectAggregate_Click;
            ShowAverageContextItem.Click += SelectAggregate_Click;
            ShowAverageContextItem.Tag = (AggregateType.Average, average);

            ShowMedianContextItem.Click -= SelectAggregate_Click;
            ShowMedianContextItem.Click += SelectAggregate_Click;
            ShowMedianContextItem.Tag = (AggregateType.Median, median);

            ShowCountContextItem.Click -= SelectAggregate_Click;
            ShowCountContextItem.Click += SelectAggregate_Click;
            ShowCountContextItem.Tag = (AggregateType.Count, (double)count);

            ShowMinContextItem.Click -= SelectAggregate_Click;
            ShowMinContextItem.Click += SelectAggregate_Click;
            ShowMinContextItem.Tag = (AggregateType.Min, min);

            ShowMaxContextItem.Click -= SelectAggregate_Click;
            ShowMaxContextItem.Click += SelectAggregate_Click;
            ShowMaxContextItem.Tag = (AggregateType.Max, max);

            ShowProductContextItem.Click -= SelectAggregate_Click;
            ShowProductContextItem.Click += SelectAggregate_Click;
            ShowProductContextItem.Tag = (AggregateType.Product, product);

            // Update checked states based on current selection
            ShowSumContextItem.IsChecked = _selectedAggregate == AggregateType.Sum;
            ShowAverageContextItem.IsChecked = _selectedAggregate == AggregateType.Average;
            ShowMedianContextItem.IsChecked = _selectedAggregate == AggregateType.Median;
            ShowCountContextItem.IsChecked = _selectedAggregate == AggregateType.Count;
            ShowMinContextItem.IsChecked = _selectedAggregate == AggregateType.Min;
            ShowMaxContextItem.IsChecked = _selectedAggregate == AggregateType.Max;
            ShowProductContextItem.IsChecked = _selectedAggregate == AggregateType.Product;
        }
    }

    private void SelectAggregate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not ValueTuple<AggregateType, double> tagData)
        {
            return;
        }

        try
        {
            (AggregateType aggregateType, double value) = tagData;

            // If clicking a checked item, uncheck it and clear selection
            if (menuItem.IsChecked && _selectedAggregate == aggregateType)
            {
                menuItem.IsChecked = false;
                _selectedAggregate = AggregateType.None;
                UpdateAggregateStatusDisplay();
                return;
            }

            // Uncheck all other aggregate menu items (both context menus)
            ShowSumContextItem.IsChecked = false;
            ShowAverageContextItem.IsChecked = false;
            ShowMedianContextItem.IsChecked = false;
            ShowCountContextItem.IsChecked = false;
            ShowMinContextItem.IsChecked = false;
            ShowMaxContextItem.IsChecked = false;
            ShowProductContextItem.IsChecked = false;

            AggregateSumContextItem.IsChecked = false;
            AggregateAverageContextItem.IsChecked = false;
            AggregateMedianContextItem.IsChecked = false;
            AggregateCountContextItem.IsChecked = false;
            AggregateMinContextItem.IsChecked = false;
            AggregateMaxContextItem.IsChecked = false;
            AggregateProductContextItem.IsChecked = false;

            // Check the clicked item
            menuItem.IsChecked = true;

            // Store the selected aggregate type
            _selectedAggregate = aggregateType;

            // Copy value to clipboard
            string valueToCopy = NumericUtilities.FormatNumber(value);
            System.Windows.Clipboard.SetDataObject(valueToCopy, true);

            // Update the status display
            UpdateAggregateStatusDisplay();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to process aggregate selection: {ex.Message}");
        }
    }

    private void UpdateAggregateStatusDisplay()
    {
        if (_selectedAggregate == AggregateType.None)
        {
            CalcAggregateStatusBorder.Visibility = Visibility.Collapsed;
            return;
        }

        List<double> numbers = GetCurrentAggregateNumbers();

        if (numbers.Count == 0)
        {
            CalcAggregateStatusBorder.Visibility = Visibility.Collapsed;
            return;
        }

        // Calculate the selected aggregate
        double value;
        string aggregateName;

        switch (_selectedAggregate)
        {
            case AggregateType.Sum:
                value = numbers.Sum();
                aggregateName = "Sum";
                break;
            case AggregateType.Average:
                value = numbers.Average();
                aggregateName = "Average";
                break;
            case AggregateType.Median:
                value = NumericUtilities.CalculateMedian(numbers);
                aggregateName = "Median";
                break;
            case AggregateType.Count:
                value = numbers.Count;
                aggregateName = "Count";
                break;
            case AggregateType.Min:
                value = numbers.Min();
                aggregateName = "Min";
                break;
            case AggregateType.Max:
                value = numbers.Max();
                aggregateName = "Max";
                break;
            case AggregateType.Product:
                value = numbers.Aggregate(1.0, (acc, val) => acc * val);
                aggregateName = "Product";
                break;
            default:
                CalcAggregateStatusBorder.Visibility = Visibility.Collapsed;
                return;
        }

        // Update the status text
        CalcAggregateStatusText.Text = $"{aggregateName}: {NumericUtilities.FormatNumber(value)}";
        CalcAggregateStatusBorder.Visibility = Visibility.Visible;
    }

    private void UpdateCalcPaneTextDisplay()
    {
        if (TryGetSpreadsheetSelectionNumbersPreviewText(out string selectionPreviewText))
        {
            CalcResultsTextControl.Text = selectionPreviewText;
            return;
        }

        CalcResultsTextControl.Text = calculationResult?.Output ?? string.Empty;
    }

    private void CalcAggregateStatusBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CalcAggregateStatusText.Text))
            return;

        try
        {
            // Extract just the numeric value from the text (e.g., "Sum: 123.45" -> "123.45")
            string fullText = CalcAggregateStatusText.Text;
            int colonIndex = fullText.IndexOf(':');
            if (colonIndex >= 0 && colonIndex < fullText.Length - 1)
            {
                string valueToCopy = fullText[(colonIndex + 1)..].Trim();
                System.Windows.Clipboard.SetDataObject(valueToCopy, true);
            }
            else
            {
                System.Windows.Clipboard.SetDataObject(fullText, true);
            }

            // Animate the copy action
            AnimateAggregateCopy();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy aggregate value: {ex.Message}");
        }

        e.Handled = true;
    }

    private void CalcAggregateStatusBorder_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        UpdateAggregateContextMenu();
    }

    private void UpdateAggregateContextMenu()
    {
        List<double> numbers = GetCurrentAggregateNumbers();

        // Update menu items based on whether we have numbers
        if (numbers.Count == 0)
        {
            AggregateSumContextItem.Header = "Sum: -";
            AggregateAverageContextItem.Header = "Average: -";
            AggregateMedianContextItem.Header = "Median: -";
            AggregateCountContextItem.Header = "Count: 0";
            AggregateMinContextItem.Header = "Min: -";
            AggregateMaxContextItem.Header = "Max: -";
            AggregateProductContextItem.Header = "Product: -";

            AggregateSumContextItem.IsEnabled = false;
            AggregateAverageContextItem.IsEnabled = false;
            AggregateMedianContextItem.IsEnabled = false;
            AggregateCountContextItem.IsEnabled = false;
            AggregateMinContextItem.IsEnabled = false;
            AggregateMaxContextItem.IsEnabled = false;
            AggregateProductContextItem.IsEnabled = false;
        }
        else
        {
            double sum = numbers.Sum();
            double average = numbers.Average();
            double median = NumericUtilities.CalculateMedian(numbers);
            int count = numbers.Count;
            double min = numbers.Min();
            double max = numbers.Max();
            double product = numbers.Aggregate(1.0, (acc, val) => acc * val);

            AggregateSumContextItem.Header = $"Sum: {NumericUtilities.FormatNumber(sum)}";
            AggregateAverageContextItem.Header = $"Average: {NumericUtilities.FormatNumber(average)}";
            AggregateMedianContextItem.Header = $"Median: {NumericUtilities.FormatNumber(median)}";
            AggregateCountContextItem.Header = $"Count: {count}";
            AggregateMinContextItem.Header = $"Min: {NumericUtilities.FormatNumber(min)}";
            AggregateMaxContextItem.Header = $"Max: {NumericUtilities.FormatNumber(max)}";
            AggregateProductContextItem.Header = $"Product: {NumericUtilities.FormatNumber(product)}";

            AggregateSumContextItem.IsEnabled = true;
            AggregateAverageContextItem.IsEnabled = true;
            AggregateMedianContextItem.IsEnabled = true;
            AggregateCountContextItem.IsEnabled = true;
            AggregateMinContextItem.IsEnabled = true;
            AggregateMaxContextItem.IsEnabled = true;
            AggregateProductContextItem.IsEnabled = true;

            // Wire up click handlers
            AggregateSumContextItem.Click -= SelectAggregate_Click;
            AggregateSumContextItem.Click += SelectAggregate_Click;
            AggregateSumContextItem.Tag = (AggregateType.Sum, sum);

            AggregateAverageContextItem.Click -= SelectAggregate_Click;
            AggregateAverageContextItem.Click += SelectAggregate_Click;
            AggregateAverageContextItem.Tag = (AggregateType.Average, average);

            AggregateMedianContextItem.Click -= SelectAggregate_Click;
            AggregateMedianContextItem.Click += SelectAggregate_Click;
            AggregateMedianContextItem.Tag = (AggregateType.Median, median);

            AggregateCountContextItem.Click -= SelectAggregate_Click;
            AggregateCountContextItem.Click += SelectAggregate_Click;
            AggregateCountContextItem.Tag = (AggregateType.Count, (double)count);

            AggregateMinContextItem.Click -= SelectAggregate_Click;
            AggregateMinContextItem.Click += SelectAggregate_Click;
            AggregateMinContextItem.Tag = (AggregateType.Min, min);

            AggregateMaxContextItem.Click -= SelectAggregate_Click;
            AggregateMaxContextItem.Click += SelectAggregate_Click;
            AggregateMaxContextItem.Tag = (AggregateType.Max, max);

            AggregateProductContextItem.Click -= SelectAggregate_Click;
            AggregateProductContextItem.Click += SelectAggregate_Click;
            AggregateProductContextItem.Tag = (AggregateType.Product, product);

            // Update checked states based on current selection
            AggregateSumContextItem.IsChecked = _selectedAggregate == AggregateType.Sum;
            AggregateAverageContextItem.IsChecked = _selectedAggregate == AggregateType.Average;
            AggregateMedianContextItem.IsChecked = _selectedAggregate == AggregateType.Median;
            AggregateCountContextItem.IsChecked = _selectedAggregate == AggregateType.Count;
            AggregateMinContextItem.IsChecked = _selectedAggregate == AggregateType.Min;
            AggregateMaxContextItem.IsChecked = _selectedAggregate == AggregateType.Max;
            AggregateProductContextItem.IsChecked = _selectedAggregate == AggregateType.Product;
        }
    }

    private List<double> GetCurrentAggregateNumbers()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            List<(int RowIndex, int ColumnIndex)> selectedCellCoordinates = GetSelectedSpreadsheetCellCoordinates();
            if (selectedCellCoordinates.Count > 0)
                return ExtractSpreadsheetSelectionNumbers(spreadsheetTable, selectedCellCoordinates);
        }

        return calculationResult?.OutputNumbers ?? [];
    }

    private void AnimateAggregateCopy()
    {
        // Flash the copy icon to indicate the copy action
        DoubleAnimation fadeInOutAnimation = new()
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(300),
            AutoReverse = true
        };

        CalcAggregateCopyIcon.BeginAnimation(OpacityProperty, fadeInOutAnimation);
    }

    private void CalcCopyAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CalcResultsTextControl.Text))
            return;

        try
        {
            System.Windows.Clipboard.SetDataObject(CalcResultsTextControl.Text, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy calc results to clipboard: {ex.Message}");
        }
    }

    private void CalcInfoButton_Click(object sender, RoutedEventArgs e)
    {
        CalcInfoPopup.IsOpen = !CalcInfoPopup.IsOpen;
    }

    private void CalcResultsTextControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Forward scrolling intent to the main text box so both stay aligned
        if (Keyboard.Modifiers == ModifierKeys.Control)
            return; // let zoom handler take it

        // Drive the main pane with the same smooth scrolling; the calc pane mirrors it via ScrollChanged
        if (SmoothScrollBehavior.ScrollBy(PassedTextControl, e.Delta))
            e.Handled = true;
    }

    private void ShowCalcPaneMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;

        DefaultSettings.CalcShowPane = menuItem.IsChecked;

        SetCalcPaneVisibility();
    }

    private void CalcToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCalcPaneMenuItem.IsChecked = !ShowCalcPaneMenuItem.IsChecked;
        DefaultSettings.CalcShowPane = ShowCalcPaneMenuItem.IsChecked;
        SetCalcPaneVisibility();
    }

    private void ToggleCalcPaneExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ShowCalcPaneMenuItem.IsChecked = !ShowCalcPaneMenuItem.IsChecked;
        DefaultSettings.CalcShowPane = ShowCalcPaneMenuItem.IsChecked;
        SetCalcPaneVisibility();
    }

    private void SetCalcPaneVisibility()
    {
        // Check if we're loading from history and should ignore default settings
        if (ShowCalcPaneMenuItem.Tag is bool fromHistory && fromHistory)
        {
            ShowCalcPaneMenuItem.Tag = null; // Clear the flag after first use
            // Use ShowCalcPaneMenuItem.IsChecked which was set from history
        }
        else
        {
            // Not from history, apply user's default setting
            ShowCalcPaneMenuItem.IsChecked = DefaultSettings.CalcShowPane;
        }

        if (ShowCalcPaneMenuItem.IsChecked)
        {
            CalcResultsTextControl.Visibility = Visibility.Visible;
            TextBoxSplitter.Visibility = Visibility.Visible;
            CalcPaneShadow.Visibility = Visibility.Visible;

            // Restore previous width if it was collapsed
            if (CalcColumn.Width.Value == 0)
                CalcColumn.Width = _lastCalcColumnWidth;

            // Disable text wrapping when calc pane is visible to maintain vertical alignment
            // Store the previous wrapping state to restore later
            if (PassedTextControl.TextWrapping != TextWrapping.NoWrap)
            {
                _previousTextWrapping = PassedTextControl.TextWrapping;
                PassedTextControl.TextWrapping = TextWrapping.NoWrap;
            }

            _debounceTimer?.Start();
        }
        else
        {
            CalcResultsTextControl.Visibility = Visibility.Collapsed;
            TextBoxSplitter.Visibility = Visibility.Collapsed;
            CalcPaneShadow.Visibility = Visibility.Collapsed;

            // Remember current width, then collapse column to remove pane area
            if (CalcColumn.Width.Value > 0)
                _lastCalcColumnWidth = CalcColumn.Width;
            CalcColumn.Width = new GridLength(0);

            // Restore previous text wrapping setting when calc pane is hidden
            if (_previousTextWrapping.HasValue)
            {
                PassedTextControl.TextWrapping = _previousTextWrapping.Value;
                _previousTextWrapping = null;
            }
        }
    }

    private void ShowErrorsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;

        DefaultSettings.CalcShowErrors = menuItem.IsChecked;
        _ = EvaluateExpressions();
    }

    private void TextBoxSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Toggle between equal split (star) and collapsed
        if (CalcColumn.Width.IsStar)
        {
            // If already star-sized, collapse the pane
            ShowCalcPaneMenuItem.IsChecked = false;
            DefaultSettings.CalcShowPane = false;
            SetCalcPaneVisibility();
        }
        else
        {
            // If collapsed or pixel-sized, set to equal split (1 star = 50% of available space)
            CalcColumn.Width = new GridLength(1, GridUnitType.Star);
            _lastCalcColumnWidth = new GridLength(1, GridUnitType.Star);
        }
    }
}
