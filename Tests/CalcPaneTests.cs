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
public class CalcPaneTests
{
    private static readonly string TwoHundredLines = string.Join("\r\n", Enumerable.Range(1, 200));

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

    private static CalcPane Pane(EditTextWindow window) => (CalcPane)window.FindName("CalcPaneControl");
}
