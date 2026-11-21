using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Text_Grab.Core.Editing;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab.Editing.Panes;

/// <summary>
/// The calc pane: evaluates each line of the document and shows the results beside it, line for line,
/// with aggregates of the results or of the selected spreadsheet cells.
/// </summary>
/// <remarks>
/// <para>
/// The host owns whether the pane is shown (<see cref="IsOpen"/>), its width, and the Show Errors
/// choice (<see cref="ShowErrors"/>), and binds <see cref="ResultsFontSize"/> to the editor's font
/// size. The pane's Hide Pane and Show Errors items ask the host to change those
/// (<see cref="HideRequested"/>, <see cref="ShowErrorsToggleRequested"/>).
/// </para>
/// <para>
/// The results scroll with the editor by line (<see cref="IEditorSession.FirstVisibleLine"/>), so they
/// stay beside their lines in Code mode too, where the editor's lines are a different height.
/// </para>
/// </remarks>
public partial class CalcPane : UserControl, IEditorPane
{
    public const string PaneId = "textgrab.pane.calc";

    // The minimum is the results box's MinWidth.
    public static readonly PaneDescriptor Definition = new(
        PaneId, "Calculations", "Calculator24", PaneDock.Right, DefaultSize: 200, MinSize: 150);

    public static readonly DependencyProperty ResultsFontSizeProperty = DependencyProperty.Register(
        nameof(ResultsFontSize), typeof(double), typeof(CalcPane), new PropertyMetadata(16.0));

    public static readonly DependencyProperty ShowErrorsProperty = DependencyProperty.Register(
        nameof(ShowErrors), typeof(bool), typeof(CalcPane), new PropertyMetadata(false));

    private const int DEBOUNCE_DELAY_MS = 300;
    private const string PaletteGroup = "Calculations";

    // Aggregate tracking for calc pane status display
    private enum AggregateType { None, Sum, Average, Count, Min, Max, Median, Product }

    private static int attachedCount;
    private static int createdCount;

    private readonly CalculationService _calculationService = new();
    private IEditorSession? session;
    private DispatcherTimer? _debounceTimer = null;
    private CalculationResult? calculationResult;
    private AggregateType _selectedAggregate = AggregateType.None;
    private bool isOpen;
    private int evaluationGeneration;

    public CalcPane()
    {
        Interlocked.Increment(ref createdCount);
        InitializeComponent();
    }

    /// <summary>The Hide Pane item was clicked; the host decides whether the pane is shown.</summary>
    public event EventHandler? HideRequested;

    /// <summary>The Show Errors item was clicked; the host owns <see cref="ShowErrors"/>.</summary>
    public event EventHandler? ShowErrorsToggleRequested;

    public PaneDescriptor Descriptor => Definition;

    public FrameworkElement View => this;

    /// <summary>The results' font size: the editor's, so the results line up with their lines.</summary>
    public double ResultsFontSize
    {
        get => (double)GetValue(ResultsFontSizeProperty);
        set => SetValue(ResultsFontSizeProperty, value);
    }

    /// <summary>
    /// Whether a line that cannot be evaluated shows its error rather than nothing. Takes effect at the
    /// next evaluation (<see cref="RecalculateAsync"/>).
    /// </summary>
    public bool ShowErrors
    {
        get => (bool)GetValue(ShowErrorsProperty);
        set => SetValue(ShowErrorsProperty, value);
    }

    /// <summary>
    /// Whether the host shows the pane. Opening it shows the results and evaluates after the typing
    /// delay; a closed pane does not evaluate.
    /// </summary>
    public bool IsOpen
    {
        get => isOpen;
        set
        {
            isOpen = value;

            if (value)
            {
                CalcResultsTextControl.Visibility = Visibility.Visible;
                _debounceTimer?.Start();
            }
            else
            {
                evaluationGeneration++;
                _debounceTimer?.Stop();
                CalcResultsTextControl.Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// How many calc panes in the process are attached right now. Tests check that a window attaches
    /// exactly one, whichever way it places it.
    /// </summary>
    internal static int AttachedCount => Volatile.Read(ref attachedCount);

    /// <summary>
    /// How many calc panes have been made, attached or not, and with them their calculation services. Unlike
    /// <see cref="AttachedCount"/>, a pane made and then detached still counts, so a test can tell that a
    /// window never made one.
    /// </summary>
    internal static int CreatedCount => Volatile.Read(ref createdCount);

    /// <summary>The results' scroll viewer while the pointer is over the shown results, for horizontal wheel scrolling.</summary>
    internal ScrollViewer? ScrollViewerUnderMouse
        => CalcResultsTextControl.Visibility == Visibility.Visible && CalcResultsTextControl.IsMouseOver
            ? WindowUtilities.GetScrollViewer(CalcResultsTextControl)
            : null;

    public void Attach(IEditorSession session, JsonElement? config)
    {
        if (this.session is not null)
            throw new InvalidOperationException("The calc pane is already attached.");

        this.session = session;
        Interlocked.Increment(ref attachedCount);

        // Set up debounce timer to avoid excessive calculations
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DEBOUNCE_DELAY_MS)
        };
        _debounceTimer.Tick += DebounceTimer_Tick;

        session.TextChanged += Session_TextChanged;
        session.ViewportChanged += Session_ViewportChanged;
        session.ModeChanged += Session_ModeChanged;
        session.SelectedCellsChanged += Session_SelectedCellsChanged;

        HideCalcPaneContextItem.Click += HideCalcPaneContextItem_Click;
        ShowCalcErrorsContextItem.Click += ShowCalcErrorsContextItem_Click;
        CopyAllContextItem.Click += CopyAllContextItem_Click;
        CalcResultsTextControl.PreviewMouseWheel += CalcResultsTextControl_PreviewMouseWheel;
    }

    public void Detach()
    {
        if (session is null)
            return;

        evaluationGeneration++;

        if (_debounceTimer is not null)
        {
            _debounceTimer.Stop();
            _debounceTimer.Tick -= DebounceTimer_Tick;
            _debounceTimer = null;
        }

        session.TextChanged -= Session_TextChanged;
        session.ViewportChanged -= Session_ViewportChanged;
        session.ModeChanged -= Session_ModeChanged;
        session.SelectedCellsChanged -= Session_SelectedCellsChanged;

        HideCalcPaneContextItem.Click -= HideCalcPaneContextItem_Click;
        ShowCalcErrorsContextItem.Click -= ShowCalcErrorsContextItem_Click;
        CopyAllContextItem.Click -= CopyAllContextItem_Click;
        CalcResultsTextControl.PreviewMouseWheel -= CalcResultsTextControl_PreviewMouseWheel;

        session = null;
        Interlocked.Decrement(ref attachedCount);
    }

    /// <summary>Nothing: the pane's width and open state are the host's, and Show Errors is a setting.</summary>
    public JsonElement? SaveConfig() => null;

    public IReadOnlyList<PaneCommand> GetPaletteCommands()
    {
        if (CalcResultsTextControl.Visibility != Visibility.Visible)
            return [];

        List<PaneCommand> commands = [];
        AddButton(CalcCopyAllButton, "Copy All Results");
        AddButton(CalcInfoButton, "Calculation Pane Help");
        return commands;

        void AddButton(Button button, string name)
        {
            if (button.IsEnabled)
                commands.Add(new(name, PaletteGroup, () => ((IInvokeProvider)new ButtonAutomationPeer(button)).Invoke()));
        }
    }

    /// <summary>Evaluates the document now rather than after the typing delay. Does nothing while the pane is closed.</summary>
    public Task RecalculateAsync() => EvaluateExpressions();

    /// <summary>Copies all the results, as the copy button does.</summary>
    public void CopyAllResults()
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

    /// <summary>
    /// None: the pane is not a control of its own to UI Automation. Its results, buttons and aggregate
    /// appear under the window as they did before the pane was extracted, so screen readers and UI tests
    /// see the same tree.
    /// </summary>
    protected override AutomationPeer? OnCreateAutomationPeer() => null;

    private void Session_TextChanged(object? sender, EventArgs e)
    {
        // Reset the debounce timer
        if (isOpen)
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }

        // If a newline append auto-scrolls the editor, ensure calc scroll follows too
        // Schedule after layout so offsets are accurate
        Dispatcher.BeginInvoke(SyncScrollToEditor, DispatcherPriority.Background);
    }

    // Keep calc pane scroll in sync with the editor
    private void Session_ViewportChanged(object? sender, EventArgs e) => SyncScrollToEditor();

    // Another editor is showing: follow it once it has been laid out.
    private void Session_ModeChanged(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(SyncScrollToEditor, DispatcherPriority.Background);

    private void Session_SelectedCellsChanged(object? sender, EventArgs e)
    {
        if (CalcResultsTextControl.Visibility == Visibility.Visible)
        {
            UpdateCalcPaneTextDisplay();
            UpdateAggregateStatusDisplay();
        }
    }

    /// <summary>
    /// Scrolls the results so the line at the top of the editor is at the top of the results. By line,
    /// not by pixel: the results' lines are the height of their own font, which need not be the editor's.
    /// </summary>
    private void SyncScrollToEditor()
    {
        try
        {
            if (session is null || CalcResultsTextControl.Visibility != Visibility.Visible)
                return;

            if (WindowUtilities.GetScrollViewer(CalcResultsTextControl) is not ScrollViewer calcSv)
                return;

            double target = session.FirstVisibleLine * TextBoxSurface.LineHeight(CalcResultsTextControl);
            if (!NumericUtilities.AreClose(calcSv.VerticalOffset, target))
                calcSv.ScrollToVerticalOffset(target);
        }
        catch { /* no-op */ }
    }

    private async void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();

        if (CalcResultsTextControl.Visibility != Visibility.Visible)
            return;

        try
        {
            await EvaluateExpressions();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation when the dispatcher is shutting down
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to evaluate calc pane expressions: {ex.Message}");
        }
    }

    private async Task EvaluateExpressions()
    {
        // Don't waste cycles if the pane isn't visible
        if (session is null || CalcResultsTextControl.Visibility != Visibility.Visible)
            return;

        int generation = ++evaluationGeneration;

        string input = session.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            calculationResult = null;
            UpdateCalcPaneTextDisplay();
            _calculationService.ClearParameters();
            UpdateAggregateStatusDisplay();
            // Keep scrolls aligned even when clearing
            await Dispatcher.InvokeAsync(() =>
            {
                if (generation == evaluationGeneration)
                    SyncScrollToEditor();
            }, DispatcherPriority.Render);
            return;
        }

        // Update calculation service settings
        _calculationService.CultureInfo = session.Culture;
        _calculationService.ShowErrors = ShowErrors;

        // Evaluate expressions using the service
        CalculationResult result = await _calculationService.EvaluateExpressionsAsync(input);

        if (generation != evaluationGeneration || session is null || CalcResultsTextControl.Visibility != Visibility.Visible)
            return;

        calculationResult = result;

        // Update the text control with results or current spreadsheet selection preview
        UpdateCalcPaneTextDisplay();

        // Update the aggregate status display if an aggregate is selected
        UpdateAggregateStatusDisplay();

        // After updating calc text, its ScrollViewer resets; resync to the editor's scroll
        await Dispatcher.InvokeAsync(() =>
        {
            if (generation == evaluationGeneration)
                SyncScrollToEditor();
        }, DispatcherPriority.Render);

        // Optional status (kept commented)
        // if (result.ErrorCount == 0) { } else { }
    }

    private void HideCalcPaneContextItem_Click(object sender, RoutedEventArgs e)
        => HideRequested?.Invoke(this, EventArgs.Empty);

    private void ShowCalcErrorsContextItem_Click(object sender, RoutedEventArgs e)
        => ShowErrorsToggleRequested?.Invoke(this, EventArgs.Empty);

    private void CopyAllContextItem_Click(object sender, RoutedEventArgs e) => CopyAllResults();

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
            SyncAggregateMenuCheckedStates();
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

            // WPF toggles IsChecked on a checkable MenuItem before raising Click, so clicking
            // the active aggregate arrives with IsChecked == false.
            if (!menuItem.IsChecked && _selectedAggregate == aggregateType)
            {
                _selectedAggregate = AggregateType.None;
                SyncAggregateMenuCheckedStates();
                UpdateAggregateStatusDisplay();
                return;
            }

            // Store the selected aggregate type and sync both context menus
            _selectedAggregate = aggregateType;
            SyncAggregateMenuCheckedStates();

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

    private void SyncAggregateMenuCheckedStates()
    {
        ShowSumContextItem.IsChecked = _selectedAggregate == AggregateType.Sum;
        ShowAverageContextItem.IsChecked = _selectedAggregate == AggregateType.Average;
        ShowMedianContextItem.IsChecked = _selectedAggregate == AggregateType.Median;
        ShowCountContextItem.IsChecked = _selectedAggregate == AggregateType.Count;
        ShowMinContextItem.IsChecked = _selectedAggregate == AggregateType.Min;
        ShowMaxContextItem.IsChecked = _selectedAggregate == AggregateType.Max;
        ShowProductContextItem.IsChecked = _selectedAggregate == AggregateType.Product;

        AggregateSumContextItem.IsChecked = _selectedAggregate == AggregateType.Sum;
        AggregateAverageContextItem.IsChecked = _selectedAggregate == AggregateType.Average;
        AggregateMedianContextItem.IsChecked = _selectedAggregate == AggregateType.Median;
        AggregateCountContextItem.IsChecked = _selectedAggregate == AggregateType.Count;
        AggregateMinContextItem.IsChecked = _selectedAggregate == AggregateType.Min;
        AggregateMaxContextItem.IsChecked = _selectedAggregate == AggregateType.Max;
        AggregateProductContextItem.IsChecked = _selectedAggregate == AggregateType.Product;
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
        // The selected spreadsheet cells' numbers, when there are any, take the results' place.
        if (session?.SelectedCellNumbers is { Count: > 0 } selectedNumbers)
        {
            CalcResultsTextControl.Text = string.Join(
                Environment.NewLine,
                selectedNumbers.Select(NumericUtilities.FormatNumber));
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
            SyncAggregateMenuCheckedStates();
        }
    }

    /// <summary>
    /// The selected spreadsheet cells' numbers while cells are selected, even if none holds a number;
    /// otherwise the results' numbers.
    /// </summary>
    private List<double> GetCurrentAggregateNumbers()
    {
        if (session?.SelectedCellNumbers is IReadOnlyList<double> selectedNumbers)
            return [.. selectedNumbers];

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

    private void CalcCopyAllButton_Click(object sender, RoutedEventArgs e) => CopyAllResults();

    private void CalcInfoButton_Click(object sender, RoutedEventArgs e)
    {
        CalcInfoPopup.IsOpen = !CalcInfoPopup.IsOpen;
    }

    private void CalcResultsTextControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Forward scrolling intent to the editor so both stay aligned
        if (Keyboard.Modifiers == ModifierKeys.Control)
            return; // let zoom handler take it

        // Drive the editor with the same smooth scrolling; the results mirror it via ViewportChanged
        if (session?.ScrollByWheel(e.Delta) == true)
            e.Handled = true;
    }
}
