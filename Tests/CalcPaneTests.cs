using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab;
using Text_Grab.Editing;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;
using static Tests.CalcPaneCharacterizationTests;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// <see cref="CalcPane"/> as a pane: what changed or is new since it left the window. What it kept is in
/// <see cref="CalcPaneCharacterizationTests"/>.
/// </summary>
/// <remarks>
/// From E4a these run twice: here against the hand-written grid, and in
/// <see cref="PaneLayoutCalcPaneTests"/> against the pane layout.
/// </remarks>
public class CalcPaneTests : IDisposable
{
    private static readonly string TwoHundredLines = string.Join("\r\n", Enumerable.Range(1, 200));

    private readonly IDisposable paneLayoutScope;

    public CalcPaneTests() => paneLayoutScope = UsePaneLayout(UsesPaneLayout);

    /// <summary>Whether these tests' windows render their editor area from a layout (E4a).</summary>
    protected virtual bool UsesPaneLayout => false;

    public void Dispose()
    {
        paneLayoutScope.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---- Code mode (new: the results used to stay put while the code editor scrolled) ----

    [WpfFact]
    public async Task Code_ResultsScrollWithTheCodeEditorLineForLine()
    {
        EditTextWindow window = CreateWindow(TwoHundredLines);
        SwitchMode(window, EtwEditorMode.Code);
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        await window.EvaluateCalcPaneAsync();
        Settle(window);

        TextBox results = CalcResults(window);
        ScrollViewer calc = WindowUtilities.GetScrollViewer(results)!;
        TextView view = CodeEditor(window).TextArea.TextView;
        CodeEditor(window).ScrollToVerticalOffset(view.GetVisualTopByDocumentLine(41));
        Settle(window, calc);

        // Line 41 is at the top of the code editor, so result 41 is at the top of the results.
        Assert.Equal(40 * TextBoxSurface.LineHeight(results), calc.VerticalOffset, precision: 1);
    }

    [WpfFact]
    public async Task Code_WheelOverTheResultsScrollsTheCodeEditor()
    {
        EditTextWindow window = CreateWindow(TwoHundredLines);
        SwitchMode(window, EtwEditorMode.Code);
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        await window.EvaluateCalcPaneAsync();
        Settle(window);

        MouseWheelEventArgs wheel = new(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        CalcResults(window).RaiseEvent(wheel);

        Assert.True(wheel.Handled);
    }

    [WpfFact]
    public async Task EnteringCodeMode_ResultsFollowTheCodeEditorsPosition()
    {
        EditTextWindow window = CreateWindow(TwoHundredLines);
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        await window.EvaluateCalcPaneAsync();
        TextBox textBox = RawTextBox(window);
        WindowUtilities.GetScrollViewer(textBox)!.ScrollToVerticalOffset(TextBoxSurface.LineHeight(textBox) * 50);
        Settle(window, WindowUtilities.GetScrollViewer(CalcResults(window))!);

        SwitchMode(window, EtwEditorMode.Code);
        Settle(window, WindowUtilities.GetScrollViewer(CalcResults(window))!);

        Assert.Equal(window.Session.FirstVisibleLine * TextBoxSurface.LineHeight(CalcResults(window)),
            WindowUtilities.GetScrollViewer(CalcResults(window))!.VerticalOffset, precision: 1);
    }

    // ---- Show Errors ----

    [WpfFact]
    public async Task ShowErrors_FollowsTheWindowsMenuItem()
    {
        EditTextWindow window = CreateWindow("2 + \r\n1 + 1");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        MenuItem showErrors = (MenuItem)window.FindName("ShowErrorsMenuItem");

        showErrors.IsChecked = false;
        await window.EvaluateCalcPaneAsync();
        Assert.Equal("\n2", CalcResults(window).Text);

        showErrors.IsChecked = true;
        await window.EvaluateCalcPaneAsync();
        Assert.StartsWith("Error:", CalcResults(window).Text);
        Assert.EndsWith("\n2", CalcResults(window).Text);
    }

    // ---- Font size ----

    [WpfFact]
    public void Results_UseTheEditorsFontSize()
    {
        EditTextWindow window = CreateWindow("1");

        RawTextBox(window).FontSize = 31;

        Assert.Equal(31, CalcResults(window).FontSize);
    }

    // ---- Command palette ----

    [WpfFact]
    public void PaletteCommands_AreOfferedOnlyWhileThePaneIsOpen()
    {
        EditTextWindow window = CreateWindow("1");
        InitializeCalcPane(window);
        IEditorPane pane = Pane(window);

        window.SetCalcPaneOpen(true);
        Assert.Equal(
            [("Copy All Results", "Calculations"), ("Calculation Pane Help", "Calculations")],
            pane.GetPaletteCommands().Select(command => (command.Name, command.Group)));

        window.SetCalcPaneOpen(false);
        Assert.Empty(pane.GetPaletteCommands());
    }

    // ---- One pane per window (E4a: the pane is made by its factory, not declared in XAML) ----

    [WpfFact]
    public void Window_AttachesExactlyOneCalcPane()
    {
        int before = CalcPane.AttachedCount;

        EditTextWindow window = CreateWindow("1 + 1");
        Assert.Equal(before + 1, CalcPane.AttachedCount);

        // Open, so the pane is on screen in both editor areas: the pane layout takes a hidden pane out
        // of the visual tree, where the hand-written grid leaves it in a column 0 wide.
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        DoEvents();
        LayOut(window);

        Assert.Equal(before + 1, CalcPane.AttachedCount);
        CalcPane shown = Assert.Single(VisualDescendants<CalcPane>(WindowContent(window)));
        Assert.Same(Pane(window), shown);
        Assert.Single(ResultsBoxes(window));
    }

    [WpfFact]
    public async Task Window_OnlyItsOwnPaneEvaluates()
    {
        EditTextWindow window = CreateWindow("2 + 2");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);

        await window.EvaluateCalcPaneAsync();

        Assert.Equal(["4"], ResultsBoxes(window).Select(box => box.Text));
    }

    // ---- Attach and Detach ----

    [WpfFact]
    public async Task Detach_StopsEvaluatingAndFollowing()
    {
        EditTextWindow window = CreateWindow("2 + 2");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        await window.EvaluateCalcPaneAsync();
        Assert.Equal("4", CalcResults(window).Text);

        Pane(window).Detach();
        RawTextBox(window).Text = "3 * 3";
        await Task.Delay(500);
        await window.EvaluateCalcPaneAsync();

        Assert.Equal("4", CalcResults(window).Text);
    }

    [WpfFact]
    public async Task ClosingOrDetaching_CancelsQueuedAndInFlightEvaluations()
    {
        EditTextWindow window = CreateWindow("2 + 2");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        await window.EvaluateCalcPaneAsync();
        Assert.Equal("4", CalcResults(window).Text);

        // Queue a debounced evaluation, then close before it fires.
        RawTextBox(window).Text = "3 * 3";
        window.SetCalcPaneOpen(false);
        await Task.Delay(450);
        DoEvents();
        Assert.Equal("4", CalcResults(window).Text);

        // Reopen, start an evaluation, and detach while it is in flight.
        window.SetCalcPaneOpen(true);
        RawTextBox(window).Text = "5 * 5";
        Task inFlight = window.EvaluateCalcPaneAsync();
        Pane(window).Detach();
        await inFlight;
        DoEvents();
    }

    [WpfFact]
    public void Attach_TwiceIsAnError()
    {
        EditTextWindow window = CreateWindow("1");

        Assert.Throws<InvalidOperationException>(() => Pane(window).Attach(window.Session, config: null));
    }

    [WpfFact]
    public void Factory_IsRegisteredAndMakesUnattachedCalcPanes()
    {
        IEditorPaneFactory factory = Assert.Single(
            AppServices.Provider.GetServices<IEditorPaneFactory>(),
            candidate => candidate.Descriptor.Id == CalcPane.PaneId);

        IEditorPane pane = factory.Create();

        CalcPane calc = Assert.IsType<CalcPane>(pane);
        Assert.Same(calc, pane.View);
        Assert.Equal(CalcPane.Definition, pane.Descriptor);
        Assert.Equal(PaneDock.Right, pane.Descriptor.DefaultDock);
        Assert.Null(pane.SaveConfig());
        Assert.NotSame(pane, factory.Create());
    }

    // ---- Aggregate toggle ----

    [WpfFact]
    public async Task AggregateMenuItem_ClickingActiveAggregateAgain_UnchecksAndClearsBadge()
    {
        EditTextWindow window = CreateWindow("1 + 1\r\n2 + 3");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        await window.EvaluateCalcPaneAsync();

        _ = OpenAggregateMenu(window);

        CalcPane pane = Pane(window);
        MenuItem showSum = (MenuItem)pane.FindName("ShowSumContextItem");
        MenuItem aggregateSum = (MenuItem)pane.FindName("AggregateSumContextItem");
        Border badge = (Border)pane.FindName("CalcAggregateStatusBorder");
        TextBlock badgeText = (TextBlock)pane.FindName("CalcAggregateStatusText");

        // Simulate WPF's checkable MenuItem click sequence: toggle IsChecked, then raise Click.
        static void ClickCheckableMenuItem(MenuItem item)
        {
            item.IsChecked = !item.IsChecked;
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
        }

        ClickCheckableMenuItem(showSum);

        Assert.True(showSum.IsChecked);
        Assert.True(aggregateSum.IsChecked);
        Assert.Equal(Visibility.Visible, badge.Visibility);
        Assert.Equal($"Sum: {NumericUtilities.FormatNumber(7)}", badgeText.Text);

        ClickCheckableMenuItem(showSum);

        Assert.False(showSum.IsChecked);
        Assert.False(aggregateSum.IsChecked);
        Assert.Equal(Visibility.Collapsed, badge.Visibility);
    }

    private static CalcPane Pane(EditTextWindow window) => (CalcPane)window.FindName("CalcPaneControl");

    /// <summary>The window's content: an unshown window applies no template of its own, so its visual tree starts here.</summary>
    internal static DependencyObject WindowContent(EditTextWindow window) => (DependencyObject)window.Content;

    /// <summary>Every calc results box in the window's visual tree.</summary>
    internal static IEnumerable<TextBox> ResultsBoxes(EditTextWindow window)
        => VisualDescendants<TextBox>(WindowContent(window))
            .Where(box => System.Windows.Automation.AutomationProperties.GetAutomationId(box) == "EditText.CalculatorResults");

    internal static IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;

            foreach (T descendant in VisualDescendants<T>(child))
                yield return descendant;
        }
    }
}
