using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Text_Grab;
using Text_Grab.Core.Layouts;
using Text_Grab.Models;
using Text_Grab.Utilities;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// Characterization tests for the Edit Text Window's calc pane, written against the code before E3
/// moved the pane out of the window. They pin what the pane does today, so the extraction can be
/// checked against it. A failure here means behavior changed.
/// </summary>
/// <remarks>
/// <para>
/// The tests reach the pane only through what survives the extraction: the results box by its UI
/// Automation ID, <c>CalcColumn</c> and the text box by name, the history constructor and
/// <see cref="EditTextWindow.AsHistoryItem"/>, and two internal members of the window,
/// <see cref="EditTextWindow.SetCalcPaneOpen"/> and <see cref="EditTextWindow.EvaluateCalcPaneAsync"/>.
/// </para>
/// <para>
/// The pane's toggles and context items save their state to the real settings, so no test clicks them.
/// <c>Window_Loaded</c> never runs here either (see <see cref="EditTextWindowTestHost"/>), so
/// <see cref="InitializeCalcPane"/> runs just the calc pane's part of it.
/// </para>
/// <para>
/// From E4a they run twice: here against the hand-written grid, and in
/// <see cref="PaneLayoutCalcPaneCharacterizationTests"/> against the pane layout. The width assertions
/// go through <see cref="CalcWidth"/> and <see cref="CalcSpace"/>, which read the grid's column or the
/// live layout.
/// </para>
/// </remarks>
public class CalcPaneCharacterizationTests : IDisposable
{
    private const string ResultsAutomationId = "EditText.CalculatorResults";

    private readonly IDisposable paneLayoutScope;

    public CalcPaneCharacterizationTests() => paneLayoutScope = UsePaneLayout(UsesPaneLayout);

    /// <summary>Whether these tests' windows render their editor area from a layout (E4a).</summary>
    protected virtual bool UsesPaneLayout => false;

    public void Dispose()
    {
        paneLayoutScope.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---- Evaluation ----

    [WpfFact]
    public async Task OpenPane_ShowsAResultForEachLine()
    {
        EditTextWindow window = CreateWindow("2 + 3\r\n10 * 4");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);

        await window.EvaluateCalcPaneAsync();

        Assert.Equal("5\n40", CalcResults(window).Text);
    }

    [WpfFact]
    public async Task OpenPane_ReevaluatesAfterTheTypingDelay()
    {
        EditTextWindow window = CreateWindow("2 + 2");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);

        await WaitForResults(window, "4");
        RawTextBox(window).Text = "3 * 3";

        await WaitForResults(window, "9");
    }

    [WpfFact]
    public async Task ClosedPane_DoesNotEvaluate()
    {
        EditTextWindow window = CreateWindow("2 + 3");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(false);

        await window.EvaluateCalcPaneAsync();

        Assert.Equal(string.Empty, CalcResults(window).Text);
        Assert.Equal(Visibility.Collapsed, CalcResults(window).Visibility);
    }

    // ---- Opening and closing ----

    [WpfFact]
    public void Opening_TurnsOffWrappingInTheTextEditorAndClosingRestoresIt()
    {
        EditTextWindow window = CreateWindow("one");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(false);
        TextWrapping before = RawTextBox(window).TextWrapping;
        Assert.NotEqual(TextWrapping.NoWrap, before);

        window.SetCalcPaneOpen(true);
        Assert.Equal(TextWrapping.NoWrap, RawTextBox(window).TextWrapping);
        Assert.Equal(Visibility.Visible, CalcResults(window).Visibility);

        window.SetCalcPaneOpen(false);
        Assert.Equal(before, RawTextBox(window).TextWrapping);
        Assert.Equal(Visibility.Collapsed, CalcResults(window).Visibility);
    }

    [WpfFact]
    public void HistoryItem_RestoresTheWidthAndSavesItBack()
    {
        EditTextWindow window = new(new HistoryInfo
        {
            TextContent = "1 + 1",
            HasCalcPaneOpen = true,
            CalcPaneWidth = 260,
        });
        LayOut(window);
        Assert.Equal(new GridLength(260), CalcWidth(window));

        InitializeCalcPane(window);
        LayOut(window);

        Assert.Equal(Visibility.Visible, CalcResults(window).Visibility);
        Assert.Equal(new GridLength(260), CalcSpace(window));
        HistoryInfo saved = window.AsHistoryItem();
        Assert.True(saved.HasCalcPaneOpen);
        Assert.Equal(260, saved.CalcPaneWidth);
    }

    [WpfFact]
    public void NewWindow_OpensThePaneAtTheXamlColumnWidthNotTheSavedWidth()
    {
        // Added in E4a to pin the hand-written window before the pane layout: a window that is not
        // restoring a history item opens the pane at the column's XAML width (200). InitializeCalcPane
        // reads the saved CalcPaneWidth into the remembered width, but SetCalcPaneVisibility only uses
        // that when the column is 0 wide, and collapsing remembers 200 over it.
        EditTextWindow window = CreateWindow("1 + 1");
        InitializeCalcPane(window);

        window.SetCalcPaneOpen(true);

        Assert.Equal(new GridLength(200), CalcSpace(window));
    }

    [WpfFact]
    public void Closing_CollapsesTheColumnAndReopening_BringsBackItsWidth()
    {
        EditTextWindow window = new(new HistoryInfo
        {
            TextContent = "1 + 1",
            HasCalcPaneOpen = true,
            CalcPaneWidth = 260,
        });
        InitializeCalcPane(window);

        window.SetCalcPaneOpen(false);
        Assert.Equal(new GridLength(0), CalcSpace(window));
        HistoryInfo closed = window.AsHistoryItem();
        Assert.False(closed.HasCalcPaneOpen);
        Assert.Equal(0, closed.CalcPaneWidth);

        window.SetCalcPaneOpen(true);
        Assert.Equal(new GridLength(260), CalcSpace(window));
        Assert.Equal(260, window.AsHistoryItem().CalcPaneWidth);
    }

    // ---- Aggregates ----

    [WpfFact]
    public async Task ContextMenu_AggregatesTheResults()
    {
        EditTextWindow window = CreateWindow("2\r\n3");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        await window.EvaluateCalcPaneAsync();

        Assert.Equal(Aggregates(sum: 5, average: 2.5, median: 2.5, count: 2, min: 2, max: 3, product: 6), OpenAggregateMenu(window));
    }

    [WpfFact]
    public async Task Spreadsheet_SelectedNumbersReplaceTheResultsAndFeedTheAggregates()
    {
        EditTextWindow window = CreateWindow("apple\t1.5\r\npear\t2\r\nfig\tn/a");
        SwitchMode(window, EtwEditorMode.Spreadsheet);
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        Grid(window).SelectedCells.Clear();
        await window.EvaluateCalcPaneAsync();

        SelectCells(window, (0, 1), (1, 1));

        Assert.Equal(NumericUtilities.FormatNumber(1.5) + Environment.NewLine + "2", CalcResults(window).Text);
        Assert.Equal(Aggregates(sum: 3.5, average: 1.75, median: 1.75, count: 2, min: 1.5, max: 2, product: 3), OpenAggregateMenu(window));
    }

    [WpfFact]
    public async Task Spreadsheet_SelectingOnlyTextCells_KeepsTheResultsButEmptiesTheAggregates()
    {
        // Pins today's behavior: the results fall back to the evaluated text, but the aggregates
        // count the selected cells' numbers, of which there are none.
        EditTextWindow window = CreateWindow("apple\t1.5\r\npear\t2\r\nfig\tn/a");
        SwitchMode(window, EtwEditorMode.Spreadsheet);
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        Grid(window).SelectedCells.Clear();
        await window.EvaluateCalcPaneAsync();
        string evaluated = CalcResults(window).Text;

        SelectCells(window, (2, 1));

        Assert.Equal(evaluated, CalcResults(window).Text);
        Assert.Equal(
            ["Sum: -", "Average: -", "Median: -", "Count: 0", "Min: -", "Max: -", "Product: -"],
            OpenAggregateMenu(window));
    }

    // ---- Scrolling with the text editor ----

    [WpfFact]
    public async Task Text_ResultsScrollWithTheTextEditor()
    {
        EditTextWindow window = CreateWindow(string.Join("\r\n", Enumerable.Range(1, 200)));
        TextBox results = CalcResults(window);
        // Same font as the results, so a line is the same height in both.
        RawTextBox(window).FontFamily = results.FontFamily;
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        await window.EvaluateCalcPaneAsync();
        LayOut(window);
        DoEvents();

        ScrollViewer main = WindowUtilities.GetScrollViewer(RawTextBox(window))!;
        ScrollViewer calc = WindowUtilities.GetScrollViewer(results)!;
        Assert.True(main.ScrollableHeight > 600);

        main.ScrollToVerticalOffset(600);
        Settle(window, calc);

        Assert.Equal(600, main.VerticalOffset, precision: 1);
        Assert.Equal(main.VerticalOffset, calc.VerticalOffset, precision: 1);
    }

    [WpfFact]
    public async Task Text_WheelOverTheResultsScrollsTheTextEditor()
    {
        EditTextWindow window = CreateWindow(string.Join("\r\n", Enumerable.Range(1, 200)));
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        await window.EvaluateCalcPaneAsync();
        LayOut(window);

        MouseWheelEventArgs wheel = new(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        CalcResults(window).RaiseEvent(wheel);

        Assert.True(wheel.Handled);
    }

    // ---- Helpers ----

    /// <summary>Runs the calc pane's part of <c>Window_Loaded</c>.</summary>
    internal static void InitializeCalcPane(EditTextWindow window)
        => typeof(EditTextWindow)
            .GetMethod("InitializeCalcPane", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);

    internal static TextBox CalcResults(EditTextWindow window)
        => FindByAutomationId<TextBox>(window, ResultsAutomationId)
            ?? throw new InvalidOperationException($"No {ResultsAutomationId} in the window.");

    internal static ColumnDefinition CalcColumn(EditTextWindow window) => (ColumnDefinition)window.FindName("CalcColumn");

    /// <summary>
    /// The calc pane's width, shown or not: the hand-written grid's column, or with the pane layout,
    /// the calc pane's size in the live layout.
    /// </summary>
    internal static GridLength CalcWidth(EditTextWindow window)
        => window.CurrentLayout is EtwLayoutDefinition layout
            ? AsGridLength(CalcChild(layout))
            : CalcColumn(window).Width;

    /// <summary>
    /// The space the calc pane takes: the hand-written grid's column, which is 0 wide while the pane is
    /// closed, or with the pane layout, the calc pane's size while it is shown and 0 while it is hidden.
    /// </summary>
    internal static GridLength CalcSpace(EditTextWindow window)
    {
        if (window.CurrentLayout is not EtwLayoutDefinition layout)
            return CalcColumn(window).Width;

        DockChild calc = CalcChild(layout);
        return calc.Collapsed ? new GridLength(0) : AsGridLength(calc);
    }

    private static DockChild CalcChild(EtwLayoutDefinition layout)
        => Assert.Single(((SplitNode)layout.Root).Children, child => child.Node is PaneNode { PaneId: PaneIds.Calc });

    private static GridLength AsGridLength(DockChild child)
        => new(child.Size, child.Unit == DockSizeUnit.Star ? GridUnitType.Star : GridUnitType.Pixel);

    internal static async Task WaitForResults(EditTextWindow window, string expected)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (CalcResults(window).Text != expected && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.Equal(expected, CalcResults(window).Text);
    }

    /// <summary>
    /// Lays the window out until scrolling has settled. A scroll takes effect at the next layout pass,
    /// and a scroll asked for while a layout pass finishes (from a ScrollChanged handler) waits for the
    /// next frame, which a window that is never shown does not get. Invalidating
    /// <paramref name="followers"/> stands in for that frame.
    /// </summary>
    internal static void Settle(EditTextWindow window, params ScrollViewer[] followers)
    {
        for (int i = 0; i < 3; i++)
        {
            LayOut(window);
            DoEvents();
            foreach (ScrollViewer follower in followers)
                follower.InvalidateArrange();
        }

        LayOut(window);
    }

    /// <summary>Opens the results box's context menu as a right-click would and returns its aggregate items.</summary>
    internal static string[] OpenAggregateMenu(EditTextWindow window)
    {
        TextBox results = CalcResults(window);
        ContextMenuEventArgs opening = (ContextMenuEventArgs)Activator.CreateInstance(
            typeof(ContextMenuEventArgs),
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            args: [results, true],
            culture: null)!;
        opening.RoutedEvent = FrameworkElement.ContextMenuOpeningEvent;
        results.RaiseEvent(opening);

        return [.. results.ContextMenu.Items.OfType<MenuItem>()
            .Select(item => item.Header?.ToString() ?? string.Empty)
            .Where(header => header.Contains(':'))];
    }

    /// <summary>The aggregate items' headers, formatted as the pane formats them (in the current culture).</summary>
    internal static string[] Aggregates(double sum, double average, double median, int count, double min, double max, double product)
        => [
            $"Sum: {NumericUtilities.FormatNumber(sum)}",
            $"Average: {NumericUtilities.FormatNumber(average)}",
            $"Median: {NumericUtilities.FormatNumber(median)}",
            $"Count: {count}",
            $"Min: {NumericUtilities.FormatNumber(min)}",
            $"Max: {NumericUtilities.FormatNumber(max)}",
            $"Product: {NumericUtilities.FormatNumber(product)}",
        ];

    private static void SelectCells(EditTextWindow window, params (int Row, int Column)[] cells)
    {
        DataGrid grid = Grid(window);
        grid.SelectedCells.Clear();
        foreach ((int row, int column) in cells)
            grid.SelectedCells.Add(new DataGridCellInfo(grid.Items[row], grid.Columns[column]));
        DoEvents();
    }

    /// <summary>
    /// The element with this AutomationId: by the visual tree first, which reaches into the pane
    /// layout's AvalonDock containers, then by the logical tree, which reaches what is not laid out.
    /// </summary>
    private static T? FindByAutomationId<T>(EditTextWindow window, string automationId) where T : DependencyObject
        => CalcPaneTests.VisualDescendants<T>(CalcPaneTests.WindowContent(window))
            .FirstOrDefault(element => AutomationProperties.GetAutomationId(element) == automationId)
            ?? FindByAutomationIdInLogicalTree<T>(window, automationId);

    private static T? FindByAutomationIdInLogicalTree<T>(DependencyObject root, string automationId) where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject element)
                continue;

            if (element is T match && AutomationProperties.GetAutomationId(element) == automationId)
                return match;

            if (FindByAutomationIdInLogicalTree<T>(element, automationId) is T found)
                return found;
        }

        return null;
    }
}
