using System.Windows;
using System.Windows.Controls;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Text_Grab;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using static Tests.CalcPaneCharacterizationTests;
using static Tests.CalcPaneTests;
using static Tests.EditTextWindowTestHost;
using WpfGrid = System.Windows.Controls.Grid;

namespace Tests;

/// <summary>Every calc characterization test again, with the editor area rendered from a layout (E4a).</summary>
public sealed class PaneLayoutCalcPaneCharacterizationTests : CalcPaneCharacterizationTests
{
    protected override bool UsesPaneLayout => true;
}

/// <summary>Every calc pane test again, with the editor area rendered from a layout (E4a).</summary>
public sealed class PaneLayoutCalcPaneTests : CalcPaneTests
{
    protected override bool UsesPaneLayout => true;
}

/// <summary>
/// The Edit Text Window's pane layout (v5 E4a): the editor area rendered from <c>classic.json</c>
/// through AvalonDock, behind the <c>EtwPaneLayout</c> setting.
/// </summary>
public class EditTextWindowPaneLayoutTests
{
    // ---- Off ----

    [WpfFact]
    public void Off_TheWindowKeepsTheHandWrittenGridAndMakesNoDockingManager()
    {
        using IDisposable _ = UsePaneLayout(on: false);

        EditTextWindow window = CreateWindow("1 + 1");

        Assert.False(window.UsesPaneLayout);
        Assert.Null(PaneHost(window));
        Assert.Null(window.CurrentLayout);
        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Same(window.Content, ((FrameworkElement)window.FindName("EditorAreaGrid")).Parent);
    }

    // ---- The Classic layout ----

    [WpfFact]
    public void On_TheEditorAreaIsTheClassicLayoutInOneDockingManager()
    {
        using IDisposable _ = UsePaneLayout();

        EditTextWindow window = CreateWindow("1 + 1");

        DockingManager manager = Assert.IsType<DockingManager>(PaneHost(window));
        Assert.Equal(3, WpfGrid.GetRow(manager));
        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Equal([PaneIds.Editor, PaneIds.Calc], EtwLayoutValidator.PaneIdsIn(window.CurrentLayout!.Root));

        // The four editors and the bookmark moved into the layout's document as one unit, and the
        // hand-written grid, with its column, splitter and shadow, left the window.
        WpfGrid readingHost = (WpfGrid)window.FindName("ReadingEditorHost");
        Assert.Contains(readingHost, VisualDescendants<WpfGrid>(manager));
        Assert.Contains(RawTextBox(window), VisualDescendants<TextBox>(readingHost));
        Assert.Null(((FrameworkElement)window.FindName("EditorAreaGrid")).Parent);
    }

    [WpfFact]
    public void On_NoPaneCanFloatAutoHideMoveOrClose()
    {
        using IDisposable _ = UsePaneLayout();

        EditTextWindow window = CreateWindow("1 + 1");

        DockingManager manager = PaneHost(window)!;
        Assert.False(manager.ShowNavigator);
        Assert.False(manager.ShowSystemMenu);
        Assert.False(manager.AllowFloatingWindows);
        Assert.False(manager.AllowDetachedWindows);

        LayoutAnchorable calc = Assert.Single(Anchorables(manager));
        Assert.Equal((PaneIds.Calc, "Calculations"), (calc.ContentId, calc.Title));
        Assert.False(calc.CanFloat || calc.CanAutoHide || calc.CanMove || calc.CanClose || calc.CanHide || calc.CanDockAsTabbedDocument || calc.CanShowOnHover);

        LayoutDocument editor = Assert.Single(Documents(manager));
        Assert.Equal(PaneIds.Editor, editor.ContentId);
        Assert.False(editor.CanFloat || editor.CanMove || editor.CanClose);
    }

    [WpfFact]
    public void On_NothingAvalonDockDrawsIsAFocusStopButTheResizeBar()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        LayOut(window);

        DockingManager manager = PaneHost(window)!;
        List<FrameworkElement> avalonDockElements = [.. VisualDescendants<FrameworkElement>(manager)
            .Where(element => element.GetType().Namespace?.StartsWith("AvalonDock", StringComparison.Ordinal) == true || element is TabItem)];

        Assert.NotEmpty(avalonDockElements);
        Assert.All(avalonDockElements.Where(element => element is not LayoutGridResizerControl), element => Assert.False(element.Focusable, element.GetType().Name));
        Assert.All(VisualDescendants<TabItem>(manager), tab => Assert.Equal(Visibility.Collapsed, tab.Visibility));
        LayoutGridResizerControl resizer = Assert.Single(VisualDescendants<LayoutGridResizerControl>(manager));
        Assert.Equal("TextBoxSplitter", System.Windows.Automation.AutomationProperties.GetAutomationId(resizer));
    }

    // ---- The calc pane through the layout ----

    [WpfFact]
    public void On_OpeningAndClosingTheCalcPane_ShowsAndHidesItsAnchorable()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");
        InitializeCalcPane(window);
        LayoutAnchorable calc = Assert.Single(Anchorables(PaneHost(window)!));

        window.SetCalcPaneOpen(true);
        Assert.False(calc.IsHidden);
        Assert.False(CalcChild(window).Collapsed);

        window.SetCalcPaneOpen(false);
        Assert.True(calc.IsHidden);
        Assert.Contains(calc, PaneHost(window)!.Layout.Hidden);
        Assert.True(CalcChild(window).Collapsed);
    }

    [WpfFact]
    public void On_TheSavedOpenState_BecomesTheLiveLayouts()
    {
        // Step 8: existing users' calc state carries over. Reads the person's setting; never writes it.
        using IDisposable _ = UsePaneLayout();
        bool savedOpen = Text_Grab.Utilities.AppUtilities.TextGrabSettings.CalcShowPane;
        EditTextWindow window = CreateWindow("1 + 1");

        InitializeCalcPane(window);

        Assert.Equal(!savedOpen, CalcChild(window).Collapsed);
        Assert.Equal(savedOpen, !Assert.Single(Anchorables(PaneHost(window)!)).IsHidden);
    }

    [WpfFact]
    public void On_ResizingWritesBackToTheLiveLayoutAndTheHistoryItem()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        LayOut(window);

        // What AvalonDock does when a drag on the resize bar ends.
        AnchorablePane(window).DockWidth = new GridLength(310);

        Assert.Equal((310d, DockSizeUnit.Pixel), (CalcChild(window).Size, CalcChild(window).Unit));
        Assert.Equal(310, window.AsHistoryItem().CalcPaneWidth);
    }

    [WpfFact]
    public void On_ResizingAStarSplit_CapturesBothEditorAndSiblingStarWeights()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("# Heading");
        Assert.True(BuiltInLayouts.TryGet(LayoutIds.MarkdownPreview, out EtwLayoutDefinition markdownPreview));
        Assert.Null(window.ApplyLayout(markdownPreview));
        LayOut(window);

        DocumentPane(window).DockWidth = new GridLength(1.6, GridUnitType.Star);
        AnchorablePane(window).DockWidth = new GridLength(0.4, GridUnitType.Star);

        SplitNode root = Assert.IsType<SplitNode>(window.CurrentLayout!.Root);
        DockChild editor = Assert.Single(root.Children, child => child.Node is PaneNode { PaneId: PaneIds.Editor });
        DockChild preview = Assert.Single(root.Children, child => child.Node is PaneNode { PaneId: PaneIds.MarkdownPreview });
        Assert.Equal((1.6d, DockSizeUnit.Star), (editor.Size, editor.Unit));
        Assert.Equal((0.4d, DockSizeUnit.Star), (preview.Size, preview.Unit));
    }

    [WpfFact]
    public void On_ResizingANestedSplit_CapturesTheNestedSplitAndItsChildSizes()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        EtwLayoutDefinition nested = new(
            "1.0",
            "nested-split",
            "Nested split",
            new SplitNode(DockOrientation.Horizontal,
            [
                new DockChild(new PaneNode(PaneIds.Editor), 1, DockSizeUnit.Star),
                new DockChild(
                    new SplitNode(DockOrientation.Vertical,
                    [
                        new DockChild(new PaneNode(PaneIds.Calc), 1, DockSizeUnit.Star),
                        new DockChild(new PaneNode(PaneIds.MarkdownPreview), 1, DockSizeUnit.Star),
                    ]),
                    1,
                    DockSizeUnit.Star),
            ]));
        Assert.Null(window.ApplyLayout(nested));
        LayOut(window);

        LayoutPanel rootPanel = PaneHost(window)!.Layout.RootPanel;
        LayoutDocumentPane documentPane = DocumentPane(window);
        LayoutPanel nestedPanel = Assert.Single(rootPanel.Children.OfType<LayoutPanel>());
        LayoutAnchorablePane calcPane = Assert.Single(nestedPanel.Children.OfType<LayoutAnchorablePane>(), pane => pane.Children.Any(c => c.ContentId == PaneIds.Calc));
        LayoutAnchorablePane previewPane = Assert.Single(nestedPanel.Children.OfType<LayoutAnchorablePane>(), pane => pane.Children.Any(c => c.ContentId == PaneIds.MarkdownPreview));

        documentPane.DockWidth = new GridLength(1.4, GridUnitType.Star);
        nestedPanel.DockWidth = new GridLength(0.6, GridUnitType.Star);
        calcPane.DockHeight = new GridLength(0.7, GridUnitType.Star);
        previewPane.DockHeight = new GridLength(1.3, GridUnitType.Star);

        SplitNode capturedRoot = Assert.IsType<SplitNode>(window.CurrentLayout!.Root);
        DockChild capturedEditor = Assert.Single(capturedRoot.Children, child => child.Node is PaneNode { PaneId: PaneIds.Editor });
        DockChild capturedNestedChild = Assert.Single(capturedRoot.Children, child => child.Node is SplitNode);
        SplitNode capturedNestedSplit = Assert.IsType<SplitNode>(capturedNestedChild.Node);
        DockChild capturedCalc = Assert.Single(capturedNestedSplit.Children, child => child.Node is PaneNode { PaneId: PaneIds.Calc });
        DockChild capturedPreview = Assert.Single(capturedNestedSplit.Children, child => child.Node is PaneNode { PaneId: PaneIds.MarkdownPreview });

        Assert.Equal((1.4d, DockSizeUnit.Star), (capturedEditor.Size, capturedEditor.Unit));
        Assert.Equal((0.6d, DockSizeUnit.Star), (capturedNestedChild.Size, capturedNestedChild.Unit));
        Assert.Equal((0.7d, DockSizeUnit.Star), (capturedCalc.Size, capturedCalc.Unit));
        Assert.Equal((1.3d, DockSizeUnit.Star), (capturedPreview.Size, capturedPreview.Unit));
    }

    [WpfFact]
    public void On_ClosingWithCalcPaneOpen_SavesCalcPaneWidthAndDetachesPanes()
    {
        using IDisposable _ = UsePaneLayout();
        var settings = Text_Grab.Utilities.AppUtilities.TextGrabSettings;
        int originalCalcPaneWidth = settings.CalcPaneWidth;
        try
        {
            settings.CalcPaneWidth = 0;

            EditTextWindow window = CreateWindow("1 + 1");
            InitializeCalcPane(window);
            window.SetCalcPaneOpen(true);
            LayOut(window);
            AnchorablePane(window).DockWidth = new GridLength(310);
            int attachedBeforeClose = CalcPane.AttachedCount;

            typeof(EditTextWindow)
                .GetMethod("OnClosed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(window, [EventArgs.Empty]);

            Assert.Equal(310, settings.CalcPaneWidth);
            Assert.Equal(attachedBeforeClose - 1, CalcPane.AttachedCount);
        }
        finally
        {
            settings.CalcPaneWidth = originalCalcPaneWidth;
            settings.Save();
        }
    }

    [WpfFact]
    public void On_TheCalcPaneKeepsAMinimumWidth()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");

        LayoutAnchorablePane calcPane = AnchorablePane(window);

        Assert.Equal(CalcPane.Definition.MinSize, calcPane.DockMinWidth);
        Assert.Equal(150, calcPane.DockMinWidth);
    }

    [WpfFact]
    public async Task On_SwitchingModes_LeavesThePanesAlone()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("2 + 2");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        CalcPane pane = (CalcPane)window.FindName("CalcPaneControl");
        int attached = CalcPane.AttachedCount;

        foreach (EtwEditorMode mode in new[] { EtwEditorMode.Markdown, EtwEditorMode.Spreadsheet, EtwEditorMode.Code, EtwEditorMode.Text })
        {
            SwitchMode(window, mode);
            Assert.Same(pane, window.FindName("CalcPaneControl"));
            Assert.False(Assert.Single(Anchorables(PaneHost(window)!)).IsHidden);
        }

        await window.EvaluateCalcPaneAsync();
        Assert.Equal(attached, CalcPane.AttachedCount);
        Assert.Equal("4", CalcResults(window).Text);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheEditorsFontSizeStillReachesTheOtherEditorsAndTheResults(bool paneLayout)
    {
        // The code editor binds to the text box by ElementName, which must still resolve once the
        // editors live inside AvalonDock's containers. (The Markdown editor's binding is replaced by a
        // local value when the window applies the font settings, on either path.)
        using IDisposable _ = UsePaneLayout(paneLayout);
        EditTextWindow window = CreateWindow("1 + 1");

        RawTextBox(window).FontSize = 27;
        LayOut(window);

        Assert.Equal(27, CodeEditor(window).FontSize);
        Assert.Equal(27, CalcResults(window).FontSize);
    }

    // ---- Other layouts ----

    [WpfFact]
    public void On_ALayoutWithoutTheCalcPane_DetachesItAndTurnsTheCalcTogglesOff()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");
        InitializeCalcPane(window);
        window.SetCalcPaneOpen(true);
        int attached = CalcPane.AttachedCount;

        string? problem = window.ApplyLayout(new EtwLayoutDefinition("1.0", "bare", "Bare", new PaneNode(PaneIds.Editor)));
        LayOut(window);

        Assert.Null(problem);
        Assert.Equal("bare", window.LayoutId);
        Assert.Equal(attached - 1, CalcPane.AttachedCount);
        Assert.Null(window.FindName("CalcPaneControl"));
        Assert.Empty(ResultsBoxes(window));
        Assert.Empty(Anchorables(PaneHost(window)!));
        Assert.False(((MenuItem)window.FindName("ShowCalcPaneMenuItem")).IsEnabled);
        Assert.False(((Button)window.FindName("CalcToggleButton")).IsEnabled);
        // The wrapping the calc pane turned off comes back with it gone.
        Assert.NotEqual(TextWrapping.NoWrap, RawTextBox(window).TextWrapping);
    }

    [WpfFact]
    public void On_ApplyingClassicAgain_KeepsTheSamePaneItsWidthAndWhetherItIsShown()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = new(new HistoryInfo { TextContent = "1 + 1", HasCalcPaneOpen = true, CalcPaneWidth = 260 });
        LayOut(window);
        InitializeCalcPane(window);
        CalcPane pane = (CalcPane)window.FindName("CalcPaneControl");
        int attached = CalcPane.AttachedCount;

        Assert.Null(window.ApplyLayout(BuiltInLayouts.Classic));
        LayOut(window);

        Assert.Same(pane, window.FindName("CalcPaneControl"));
        Assert.Equal(attached, CalcPane.AttachedCount);
        Assert.Equal(new GridLength(260), CalcSpace(window));
        Assert.Equal(260, window.AsHistoryItem().CalcPaneWidth);
    }

    [WpfFact]
    public void On_ALayoutWithAnUnknownPane_IsRefusedWithAReasonAndTheWindowKeepsItsLayout()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");
        EtwLayoutDefinition unknown = BuiltInLayouts.Classic with
        {
            Id = "telescope",
            Root = new SplitNode(DockOrientation.Horizontal,
            [
                new DockChild(new PaneNode(PaneIds.Editor), 1, DockSizeUnit.Star),
                new DockChild(new PaneNode("textgrab.pane.telescope"), 200, DockSizeUnit.Pixel),
            ]),
        };

        string? problem = window.ApplyLayout(unknown);

        Assert.Contains("textgrab.pane.telescope", problem);
        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.NotNull(window.FindName("CalcPaneControl"));
    }

    [WpfFact]
    public void On_ALayoutWithNoEditor_GetsOneAnyway()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");

        Assert.Null(window.ApplyLayout(new EtwLayoutDefinition("1.0", "calc-only", "Calc only", new PaneNode(PaneIds.Calc))));
        LayOut(window);

        Assert.Single(Documents(PaneHost(window)!));
        Assert.Contains(RawTextBox(window), VisualDescendants<TextBox>(PaneHost(window)!));
    }

    [WpfFact]
    public void On_UnusableSizes_FallBackToThePanesDefault()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");
        EtwLayoutDefinition broken = BuiltInLayouts.Classic with
        {
            Id = "zero",
            Root = new SplitNode(DockOrientation.Horizontal,
            [
                new DockChild(new PaneNode(PaneIds.Editor), double.NaN, DockSizeUnit.Star),
                new DockChild(new PaneNode(PaneIds.Calc), -40, DockSizeUnit.Pixel),
            ]),
        };

        Assert.Null(window.ApplyLayout(broken));

        Assert.Equal(new GridLength(CalcPane.Definition.DefaultSize), AnchorablePane(window).DockWidth);
    }

    /// <summary>
    /// The one anchorable pane, which keeps the calc pane's place and size even while the calc pane is
    /// hidden (and parented by the layout root's hidden list).
    /// </summary>
    private static LayoutAnchorablePane AnchorablePane(EditTextWindow window)
        => Assert.Single(LayoutElements(PaneHost(window)!.Layout).OfType<LayoutAnchorablePane>());

    private static LayoutDocumentPane DocumentPane(EditTextWindow window)
        => Assert.Single(LayoutElements(PaneHost(window)!.Layout).OfType<LayoutDocumentPane>());

    // ---- History and requested layouts (steps 10-11) ----

    [WpfFact]
    public void History_AClassicWindowSavesNoLayout()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");

        Assert.Null(window.AsHistoryItem().LayoutId);
    }

    [WpfFact]
    public void History_ALayoutTheWindowShowsIsSavedWithItsItem()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");

        Assert.Null(window.ApplyLayout(new EtwLayoutDefinition("1.0", "bare", "Bare", new PaneNode(PaneIds.Editor))));

        Assert.Equal("bare", window.AsHistoryItem().LayoutId);
    }

    [WpfFact]
    public void History_AnItemsLayoutThatCannotBeHad_OpensClassicAndSaysWhyOnceShown()
    {
        using IDisposable _ = UsePaneLayout();

        EditTextWindow window = new(new HistoryInfo { TextContent = "1 + 1", LayoutId = "not-a-saved-layout" });
        LayOut(window);

        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Contains("not-a-saved-layout", PendingLayoutNotice(window));
        Assert.NotNull(window.FindName("CalcPaneControl"));
    }

    [WpfFact]
    public void History_WithThePaneLayoutOff_AnItemKeepsTheLayoutItWasSavedWith()
    {
        using IDisposable _ = UsePaneLayout(on: false);

        EditTextWindow window = new(new HistoryInfo { TextContent = "1 + 1", LayoutId = "wide-calc" });

        Assert.Equal("wide-calc", window.AsHistoryItem().LayoutId);
        Assert.Null(PendingLayoutNotice(window));
    }

    [WpfFact]
    public async Task RequestLayout_Classic_IsClassicWithNoNotice()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");

        await window.RequestLayoutAsync(LayoutIds.Classic);

        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Null(PendingLayoutNotice(window));
    }

    [WpfFact]
    public async Task RequestLayout_WithThePaneLayoutOff_SaysWhy()
    {
        using IDisposable _ = UsePaneLayout(on: false);
        EditTextWindow window = CreateWindow("1 + 1");

        await window.RequestLayoutAsync("wide-calc");

        Assert.Contains("pane layout", PendingLayoutNotice(window));
        Assert.Null(PaneHost(window));
    }

    private static string? PendingLayoutNotice(EditTextWindow window)
        => (string?)typeof(EditTextWindow)
            .GetField("pendingLayoutNotice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(window);

    private static DockChild CalcChild(EditTextWindow window)
        => Assert.Single(((SplitNode)window.CurrentLayout!.Root).Children, child => child.Node is PaneNode { PaneId: PaneIds.Calc });

    private static IEnumerable<LayoutAnchorable> Anchorables(DockingManager manager)
        => LayoutElements(manager.Layout).OfType<LayoutAnchorable>().Distinct();

    private static IEnumerable<LayoutDocument> Documents(DockingManager manager)
        => LayoutElements(manager.Layout).OfType<LayoutDocument>();

    private static IEnumerable<ILayoutElement> LayoutElements(ILayoutElement element)
    {
        yield return element;
        if (element is ILayoutContainer container)
        {
            foreach (ILayoutElement child in container.Children)
                foreach (ILayoutElement descendant in LayoutElements(child))
                    yield return descendant;
        }
    }
}
