using System.Reflection;
using System.Windows.Controls.Primitives;
using Text_Grab;
using Text_Grab.Controls;
using Text_Grab.Core.Layouts;
using Text_Grab.Models;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>The contents button on the Markdown bar: when it is there, and what it does to the table of contents pane.</summary>
public class EditTextWindowTocToggleTests
{
    private const string WithHeadings = "# Title\n\ntext\n\n## Section\n";

    private static MarkdownFormattingBar Bar(EditTextWindow window) => (MarkdownFormattingBar)window.FindName("MarkdownToolbar");

    private static EditTextWindow MarkdownWindow(string text)
    {
        EditTextWindow window = CreateWindow(text);
        SwitchMode(window, EtwEditorMode.Markdown);
        return window;
    }

    private static void Click(MarkdownFormattingBar bar)
    {
        // A real click flips IsChecked, then raises Click; the button's own OnClick does both.
        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(bar.TocToggle, null);
        DoEvents();
    }

    [WpfFact]
    public void TheButtonIsThereOnlyWhileTheMarkdownDocumentHasAHeading()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = MarkdownWindow("just text");

        Assert.False(Bar(window).IsTocToggleVisible);

        window.Session.Text = WithHeadings;

        Assert.True(Bar(window).IsTocToggleVisible);
        Assert.False(Bar(window).IsTocToggleChecked);

        window.Session.Text = "text again";

        Assert.False(Bar(window).IsTocToggleVisible);
    }

    [WpfFact]
    public void OutsideMarkdownMode_ThereIsNoButton()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = MarkdownWindow(WithHeadings);
        Assert.True(Bar(window).IsTocToggleVisible);

        SwitchMode(window, EtwEditorMode.Text);

        Assert.False(Bar(window).IsTocToggleVisible);
    }

    [WpfFact]
    public void WithoutThePaneLayout_ThereIsNoButton_BecauseThereIsNowhereToShowThePane()
    {
        using IDisposable _ = UsePaneLayout(on: false);
        EditTextWindow window = MarkdownWindow(WithHeadings);

        Assert.False(Bar(window).IsTocToggleVisible);
    }

    [WpfFact]
    public void TheButtonShowsAndHidesThePane_AndReadsAsOnWhileItIsShown()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = MarkdownWindow(WithHeadings);
        MarkdownFormattingBar bar = Bar(window);

        Click(bar);
        LayOut(window);

        Assert.Contains(PaneIds.Toc, EtwLayoutValidator.PaneIdsIn(window.CurrentLayout!.Root));
        Assert.True(bar.IsTocToggleChecked);
        TocPaneOf(window, out Text_Grab.Editing.Panes.TocPane? pane);
        Assert.Equal(["Title"], pane!.Nodes.Select(node => node.Title));

        Click(bar);
        LayOut(window);

        Assert.DoesNotContain(PaneIds.Toc, EtwLayoutValidator.PaneIdsIn(window.CurrentLayout!.Root));
        Assert.False(bar.IsTocToggleChecked);
        Assert.True(bar.IsTocToggleVisible);
    }

    [WpfFact]
    public void AnOpenPaneKeepsItsButton_EvenWhenTheLastHeadingIsDeleted()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = MarkdownWindow(WithHeadings);
        MarkdownFormattingBar bar = Bar(window);
        Click(bar);

        window.Session.Text = "no headings left";

        Assert.True(bar.IsTocToggleVisible);
        Assert.True(bar.IsTocToggleChecked);

        Click(bar);

        Assert.False(bar.IsTocToggleVisible);
    }

    [WpfFact]
    public void ThePanesMenuAndTheButtonAgree()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = MarkdownWindow(WithHeadings);
        MarkdownFormattingBar bar = Bar(window);

        window.SetPaneShown(PaneIds.Toc, true);
        LayOut(window);

        Assert.True(bar.IsTocToggleChecked);

        window.SetPaneShown(PaneIds.Toc, false);
        LayOut(window);

        Assert.False(bar.IsTocToggleChecked);
    }

    private static void TocPaneOf(EditTextWindow window, out Text_Grab.Editing.Panes.TocPane? pane)
        => pane = ((System.Collections.Generic.IEnumerable<Text_Grab.Editing.Panes.IEditorPane>)typeof(EditTextWindow)
            .GetProperty("Panes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!)
            .OfType<Text_Grab.Editing.Panes.TocPane>()
            .SingleOrDefault();
}
