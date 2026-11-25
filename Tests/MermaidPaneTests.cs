using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab;
using Text_Grab.Core.Actions;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Layout;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using Text_Grab.Services;
using static Tests.CalcPaneTests;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// A delay and a renderer the test finishes by hand, so debouncing and cancellation are exercised without
/// waiting and without drawing anything.
/// </summary>
internal sealed class ManualMermaid
{
    public sealed record Delay(TimeSpan Time, CancellationToken Token, TaskCompletionSource Completion);

    public sealed record Render(string Source, bool IsLightTheme, CancellationToken Token, TaskCompletionSource<(BitmapSource Bitmap, string Svg)> Completion);

    public List<Delay> Delays { get; } = [];

    public List<Render> Renders { get; } = [];

    public Task DelayAsync(TimeSpan time, CancellationToken token)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        token.Register(() => completion.TrySetCanceled(token));
        Delays.Add(new Delay(time, token, completion));
        return completion.Task;
    }

    public Task<(BitmapSource Bitmap, string Svg)> RenderAsync(string source, bool isLightTheme, CancellationToken token)
    {
        TaskCompletionSource<(BitmapSource, string)> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        token.Register(() => completion.TrySetCanceled(token));
        Renders.Add(new Render(source, isLightTheme, token, completion));
        return completion.Task;
    }

    public MermaidPaneViewModel CreateViewModel() => new(RenderAsync, DelayAsync);

    /// <summary>Ends the most recent delay, as if the typing delay had passed.</summary>
    public void FinishLastDelay() => Delays[^1].Completion.TrySetResult();

    /// <summary>Finishes the most recent render with a picture of <paramref name="source"/>.</summary>
    public BitmapSource FinishLastRender(string? svg = null)
    {
        Render render = Renders[^1];
        BitmapSource bitmap = Picture();
        render.Completion.TrySetResult((bitmap, svg ?? $"<svg>{render.Source}</svg>"));
        return bitmap;
    }

    public void FailLastRender(string message) => Renders[^1].Completion.TrySetException(new InvalidOperationException(message));

    public static BitmapSource Picture()
    {
        BitmapSource bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8);
        bitmap.Freeze();
        return bitmap;
    }
}

/// <summary>The Mermaid pane's state, its debounce and its cancellation (v5 E5 <c>mermaid</c>).</summary>
public class MermaidPaneViewModelTests
{
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was never met.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public void TheTypingDelayIsAboutThreeHundredMilliseconds()
        => Assert.Equal(TimeSpan.FromMilliseconds(300), MermaidPaneViewModel.DebounceDelay);

    [WpfFact]
    public async Task EachChangeRestartsTheDelay_AndOnlyTheLastOneIsDrawn()
    {
        ManualMermaid fake = new();
        MermaidPaneViewModel viewModel = fake.CreateViewModel();

        viewModel.SetSource("flowchart LR\nA");
        viewModel.SetSource("flowchart LR\nA --> B");

        Assert.Equal(2, fake.Delays.Count);
        Assert.All(fake.Delays, delay => Assert.Equal(MermaidPaneViewModel.DebounceDelay, delay.Time));
        Assert.True(fake.Delays[0].Token.IsCancellationRequested, "The first delay should be cancelled by the second change.");
        Assert.Empty(fake.Renders);

        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);

        Assert.Equal("flowchart LR\nA --> B", fake.Renders[0].Source);
        Assert.Equal(1, viewModel.RenderCalls);
    }

    [WpfFact]
    public async Task ANewChangeCancelsTheRenderUnderWay_AndAnOldResultNeverReplacesTheNewOne()
    {
        ManualMermaid fake = new();
        MermaidPaneViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("first");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);
        Assert.True(viewModel.IsRendering);

        viewModel.SetSource("second");

        Assert.True(fake.Renders[0].Token.IsCancellationRequested, "The render under way should be cancelled.");
        Assert.False(viewModel.IsRendering);

        // Even a renderer that ignores cancellation and finishes anyway changes nothing.
        fake.Renders[0].Completion.TrySetResult((ManualMermaid.Picture(), "<svg>first</svg>"));
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 2);
        fake.FinishLastRender();
        await WaitUntilAsync(() => viewModel.RenderedSource == "second");

        Assert.Equal("second", viewModel.RenderedSource);
        Assert.Equal("<svg>second</svg>", viewModel.RenderedSvg);
        Assert.False(viewModel.IsRendering);
    }

    [WpfFact]
    public async Task AFailedRender_KeepsTheLastGoodDiagramAndShowsTheError_AndTheNextGoodRenderClearsIt()
    {
        ManualMermaid fake = new();
        MermaidPaneViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("good");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);
        BitmapSource good = fake.FinishLastRender();
        await WaitUntilAsync(() => viewModel.HasDiagram);

        viewModel.SetSource("good plus a typo");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 2);
        fake.FailLastRender("Unexpected token\nat line 2");
        await WaitUntilAsync(() => viewModel.HasError);

        Assert.Same(good, viewModel.DiagramImage);
        Assert.Equal("good", viewModel.RenderedSource);
        Assert.Equal("Unexpected token", viewModel.ErrorMessage);
        Assert.Equal(string.Empty, viewModel.PlaceholderText);

        viewModel.SetSource("good plus a fix");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 3);
        fake.FinishLastRender();
        await WaitUntilAsync(() => !viewModel.HasError);

        Assert.Equal("good plus a fix", viewModel.RenderedSource);
        Assert.NotSame(good, viewModel.DiagramImage);
    }

    [WpfFact]
    public async Task GoingBackToTheSourceAlreadyDrawn_ClearsTheErrorWithoutRenderingAgain()
    {
        ManualMermaid fake = new();
        MermaidPaneViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("good");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);
        fake.FinishLastRender();
        await WaitUntilAsync(() => viewModel.HasDiagram);
        viewModel.SetSource("bad");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 2);
        fake.FailLastRender("nope");
        await WaitUntilAsync(() => viewModel.HasError);

        viewModel.SetSource("good");

        Assert.False(viewModel.HasError);
        Assert.Equal(2, fake.Renders.Count);
        Assert.Equal("good", viewModel.RenderedSource);
    }

    [WpfFact]
    public async Task AFirstRenderThatFails_ShowsTheErrorAndNoDiagram()
    {
        ManualMermaid fake = new();
        MermaidPaneViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("bad");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);
        fake.FailLastRender("Not a diagram");
        await WaitUntilAsync(() => viewModel.HasError);

        Assert.False(viewModel.HasDiagram);
        Assert.Equal(MermaidPaneViewModel.NothingDrawnText, viewModel.PlaceholderText);
    }

    [WpfTheory]
    [InlineData(null, MermaidPaneViewModel.NoBlockText)]
    [InlineData("", MermaidPaneViewModel.EmptySourceText)]
    [InlineData("  \n ", MermaidPaneViewModel.EmptySourceText)]
    public async Task NothingToDraw_ClearsTheDiagramAtOnceAndSaysWhy(string? source, string expectedText)
    {
        ManualMermaid fake = new();
        MermaidPaneViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("good");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);
        fake.FinishLastRender();
        await WaitUntilAsync(() => viewModel.HasDiagram);

        viewModel.SetSource(source);

        Assert.False(viewModel.HasDiagram);
        Assert.Null(viewModel.RenderedSource);
        Assert.False(viewModel.HasError);
        Assert.Equal(expectedText, viewModel.PlaceholderText);
        Assert.Single(fake.Renders);
    }

    [WpfFact]
    public async Task ChangingTheTheme_RedrawsAtOnceInTheNewColors()
    {
        ManualMermaid fake = new();
        MermaidPaneViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("good");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);
        Assert.True(fake.Renders[0].IsLightTheme);
        fake.FinishLastRender();
        await WaitUntilAsync(() => viewModel.HasDiagram);

        viewModel.SetTheme(lightTheme: false);
        await WaitUntilAsync(() => fake.Renders.Count == 2);

        Assert.False(fake.Renders[1].IsLightTheme);
        Assert.Single(fake.Delays);

        viewModel.SetTheme(lightTheme: false);
        Assert.Equal(2, fake.Renders.Count);
    }

    [WpfFact]
    public async Task AHiddenPaneDrawsNothing_AndCatchesUpWhenShown()
    {
        ManualMermaid fake = new();
        MermaidPaneViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("one");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);

        viewModel.SetActive(false);

        Assert.True(fake.Renders[0].Token.IsCancellationRequested);
        Assert.False(viewModel.IsRendering);

        viewModel.SetSource("two");
        viewModel.SetTheme(lightTheme: false);

        Assert.Single(fake.Renders);
        Assert.Single(fake.Delays);

        viewModel.SetActive(true);
        await WaitUntilAsync(() => fake.Renders.Count == 2);

        Assert.Equal("two", fake.Renders[1].Source);
        Assert.False(fake.Renders[1].IsLightTheme);
        Assert.Single(fake.Delays);
    }

    [WpfFact]
    public async Task Release_StopsTheRenderAndLetsGoOfTheDiagram()
    {
        ManualMermaid fake = new();
        MermaidPaneViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("good");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);
        fake.FinishLastRender();
        await WaitUntilAsync(() => viewModel.HasDiagram);
        viewModel.SetSource("good, changed");

        viewModel.Release();

        Assert.False(viewModel.HasDiagram);
        Assert.Null(viewModel.RenderedSvg);
        Assert.Null(viewModel.RenderedSource);
        Assert.Null(viewModel.WantedSource);
        Assert.True(fake.Delays[^1].Token.IsCancellationRequested);
        Assert.False(viewModel.IsRendering);
    }

    [WpfFact]
    public void ThePropertiesTheViewBindsToRaiseChanged()
    {
        ManualMermaid fake = new();
        MermaidPaneViewModel viewModel = fake.CreateViewModel();
        List<string?> changed = [];
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.SetSource(" ");
        viewModel.SetSource("x");

        Assert.Contains(nameof(MermaidPaneViewModel.PlaceholderText), changed);
    }

    // ---- With the real renderer ----

    [WpfFact]
    public async Task TheRealRenderer_DrawsAGoodDiagramAndKeepsItWhenTheSourceBreaks()
    {
        MermaidPaneViewModel viewModel = new(delay: (_, _) => Task.CompletedTask);

        viewModel.SetSource("flowchart LR\nA[Start] --> B[End]");
        await WaitUntilAsync(() => viewModel.HasDiagram || viewModel.HasError);

        Assert.False(viewModel.HasError, viewModel.ErrorMessage);
        BitmapSource drawn = Assert.IsAssignableFrom<BitmapSource>(viewModel.DiagramImage);
        Assert.True(drawn.PixelWidth > 10);
        Assert.StartsWith("<svg", viewModel.RenderedSvg!.TrimStart(), StringComparison.Ordinal);

        viewModel.SetSource("this is not a diagram");
        await WaitUntilAsync(() => viewModel.HasError);

        Assert.Same(drawn, viewModel.DiagramImage);
        Assert.Equal("flowchart LR\nA[Start] --> B[End]", viewModel.RenderedSource);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }
}

/// <summary>The Mermaid pane in an Edit Text Window, and the Mermaid layout that has it (v5 E5 <c>mermaid</c>).</summary>
public class MermaidPaneTests
{
    private const string Flowchart = "flowchart LR\nA[Start] --> B[End]";

    private static EtwLayoutDefinition MermaidLayout => BuiltInLayouts.TryGet(LayoutIds.Mermaid, out EtwLayoutDefinition layout)
        ? layout
        : throw new InvalidOperationException("Mermaid is not built in.");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was never met.");
            await Task.Delay(10);
        }
    }

    private static MermaidPane PaneIn(EditTextWindow window)
        => Assert.Single(VisualDescendants<MermaidPane>(PaneHost(window)!));

    private static MermaidPane Attached(EditTextWindow window, ManualMermaid fake, bool lightTheme = true)
    {
        MermaidPane pane = new(fake.CreateViewModel(), () => lightTheme);
        pane.Attach(window.Session, config: null);
        return pane;
    }

    // ---- The pane and its factory ----

    [Fact]
    public void PaneIds_AndActionIdsArePinned()
    {
        Assert.Equal("textgrab.pane.mermaid", PaneIds.Mermaid);
        Assert.Equal(PaneIds.Mermaid, MermaidPane.PaneId);

        string[] ids = [.. AppServices.Provider.GetServices<IEditorPaneFactory>().Select(factory => factory.Descriptor.Id).Order(StringComparer.Ordinal)];
        Assert.Equal(["textgrab.pane.calc", "textgrab.pane.markdownPreview", "textgrab.pane.mermaid", "textgrab.pane.recent", "textgrab.pane.toc"], ids);

        ActionRegistry registry = new();
        MermaidPaneActions.RegisterAll(registry);
        Assert.Equal(
            ["textgrab.mermaid.copyImage", "textgrab.mermaid.exportPng", "textgrab.mermaid.exportSvg", "textgrab.mermaid.openFullScreen"],
            registry.All.Select(descriptor => descriptor.Id).Order(StringComparer.Ordinal));
    }

    [WpfFact]
    public void Factory_IsRegisteredAndMakesUnattachedMermaidPanes()
    {
        IEditorPaneFactory factory = Assert.Single(
            AppServices.Provider.GetServices<IEditorPaneFactory>(),
            candidate => candidate.Descriptor.Id == MermaidPane.PaneId);
        int attached = MermaidPane.AttachedCount;

        IEditorPane pane = factory.Create();

        MermaidPane mermaid = Assert.IsType<MermaidPane>(pane);
        Assert.Same(mermaid, pane.View);
        Assert.Equal(MermaidPane.Definition, pane.Descriptor);
        Assert.Equal(("Diagram", PaneDock.Right), (pane.Descriptor.Title, pane.Descriptor.DefaultDock));
        Assert.True(Enum.IsDefined(typeof(Wpf.Ui.Controls.SymbolRegular), pane.Descriptor.IconKey), pane.Descriptor.IconKey);
        Assert.Null(pane.SaveConfig());
        Assert.Equal(attached, MermaidPane.AttachedCount);
        Assert.NotSame(pane, factory.Create());
        Assert.Empty(pane.GetPaletteCommands());
    }

    [Fact]
    public void ResolvingTheFactories_MakesNoPane()
    {
        int created = MermaidPane.CreatedCount;

        _ = AppServices.Provider.GetServices<IEditorPaneFactory>().Select(factory => factory.Descriptor).ToList();

        Assert.Equal(created, MermaidPane.CreatedCount);
    }

    [WpfFact]
    public void Attach_DrawsTheDocument_AndTwiceIsAnError()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        MermaidPane pane = Attached(window, fake);

        Assert.Equal(Flowchart, pane.ViewModel.WantedSource);
        Assert.Single(fake.Delays);
        Assert.Throws<InvalidOperationException>(() => pane.Attach(window.Session, config: null));
        pane.Detach();
    }

    [WpfFact]
    public void Attach_DrawsInTheThemeTheSystemHas()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);

        MermaidPane pane = Attached(window, fake, lightTheme: false);

        Assert.False(pane.ViewModel.IsLightTheme);
        pane.Detach();
    }

    [WpfFact]
    public void EditingTheSource_UpdatesWhatTheDiagramWillDraw()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        MermaidPane pane = Attached(window, fake);

        RawTextBox(window).Text = "flowchart LR\nA --> C";

        Assert.Equal("flowchart LR\nA --> C", pane.ViewModel.WantedSource);
        Assert.Equal(2, fake.Delays.Count);
        pane.Detach();
    }

    [WpfFact]
    public void InMarkdownMode_TheBlockUnderTheCaretIsDrawn_AndFirstWhenTheCaretCannotBeKnown()
    {
        const string markdown = "# Notes\n\n```mermaid\nflowchart LR\nA --> B\n```\n\n```mermaid\nsequenceDiagram\nAlice->>Bob: Hi\n```\n";
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(markdown);
        MermaidPane pane = Attached(window, fake);

        SwitchMode(window, EtwEditorMode.Markdown);
        pane.Refresh();

        // The rendered Markdown editor does not report its caret as a raw offset, so the first block wins.
        Assert.Equal("flowchart LR\nA --> B", pane.ViewModel.WantedSource);

        window.Session.Selection = new Text_Grab.Core.Editing.TextSpan(markdown.IndexOf("Alice", StringComparison.Ordinal), 0);
        pane.Refresh();

        Assert.Equal("sequenceDiagram\nAlice->>Bob: Hi", pane.ViewModel.WantedSource);
        pane.Detach();
    }

    [WpfFact]
    public void SwitchingModes_DrawsTheWholeDocumentAgainOutsideMarkdown()
    {
        const string markdown = "# Notes\n\n```mermaid\nflowchart LR\nA --> B\n```\n";
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(markdown);
        MermaidPane pane = Attached(window, fake);
        SwitchMode(window, EtwEditorMode.Markdown);
        Assert.Equal("flowchart LR\nA --> B", pane.ViewModel.WantedSource);

        SwitchMode(window, EtwEditorMode.Code);

        Assert.Equal(window.Session.Text, pane.ViewModel.WantedSource);
        pane.Detach();
    }

    [WpfFact]
    public void Detach_StopsFollowingTheDocument_ReleasesAndMayBeCalledAgain()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        int attached = MermaidPane.AttachedCount;
        MermaidPane pane = Attached(window, fake);
        Assert.Equal(attached + 1, MermaidPane.AttachedCount);

        pane.Detach();
        pane.Detach();
        RawTextBox(window).Text = "changed after detach";

        Assert.Equal(attached, MermaidPane.AttachedCount);
        Assert.Null(pane.ViewModel.WantedSource);
        Assert.Single(fake.Delays);
    }

    [WpfFact]
    public async Task AHiddenPaneDrawsNothingUntilItIsShown()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        MermaidPane pane = Attached(window, fake);

        pane.OnVisibilityChanged(false);
        RawTextBox(window).Text = "flowchart LR\nA --> C";
        Assert.Single(fake.Delays);

        pane.OnVisibilityChanged(true);
        await WaitUntilAsync(() => fake.Renders.Count == 1);

        Assert.Equal("flowchart LR\nA --> C", fake.Renders[0].Source);
        pane.Detach();
    }

    [WpfFact]
    public void WindowActivated_ReadsTheThemeAgain()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        bool light = true;
        MermaidPane pane = new(fake.CreateViewModel(), () => light);
        pane.Attach(window.Session, config: null);
        Assert.True(pane.ViewModel.IsLightTheme);

        light = false;
        ((IEditorPane)pane).OnWindowActivated();

        Assert.False(pane.ViewModel.IsLightTheme);
        pane.Detach();
    }

    // ---- Actions ----

    private static async Task<MermaidPane> PaneWithADiagramAsync(EditTextWindow window, ManualMermaid fake)
    {
        MermaidPane pane = Attached(window, fake);
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);
        fake.FinishLastRender("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        await WaitUntilAsync(() => pane.ViewModel.HasDiagram);
        return pane;
    }

    [WpfFact]
    public async Task TheContextMenu_IsMarkdownModesMenuPlusCopyImage_AndCopyingUsesTheClipboardSeam()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        MermaidPane pane = Attached(window, fake);
        Assert.Null(pane.CreateContextMenu());

        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);
        BitmapSource drawn = fake.FinishLastRender();
        await WaitUntilAsync(() => pane.ViewModel.HasDiagram);

        ContextMenu menu = Assert.IsType<ContextMenu>(pane.CreateContextMenu());
        string[] headers = [.. menu.Items.OfType<MenuItem>().Select(item => item.Header?.ToString() ?? string.Empty)];
        Assert.Equal(["View full screen", "Copy image", "Save as text...", "Save as PNG...", "Save as SVG..."], headers);

        Action<BitmapSource> original = MermaidPane.ImageClipboard;
        BitmapSource? copied = null;
        MermaidPane.ImageClipboard = image => copied = image;
        try
        {
            pane.CopyImage();
        }
        finally
        {
            MermaidPane.ImageClipboard = original;
        }

        Assert.Same(drawn, copied);
        pane.Detach();
    }

    [WpfFact]
    public async Task ZoomContextMenu_DropsTheDiagramsMenu_WhenDiagramIsCleared_WhenOpeningWithoutDiagram_AndOnDetach()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        MermaidPane pane = await PaneWithADiagramAsync(window, fake);
        Text_Grab.Controls.ZoomBorder zoom = Assert.IsType<Text_Grab.Controls.ZoomBorder>(pane.FindName("Zoom"));

        Assert.True(pane.UpdateContextMenu());
        ContextMenu built = Assert.IsType<ContextMenu>(zoom.ContextMenu);
        Assert.NotEmpty(built.Items);

        // Clearing the diagram drops the menu that references the old bitmap/SVG, for an empty placeholder
        // (a null menu would make WPF swallow the next right-click).
        pane.ViewModel.SetSource(null);
        Assert.NotSame(built, zoom.ContextMenu);
        Assert.Empty(Assert.IsType<ContextMenu>(zoom.ContextMenu).Items);

        // Opening the context menu with no diagram drops any stale menu and reports nothing to open.
        ContextMenu stale = new();
        stale.Items.Add(new MenuItem { Header = "Stale" });
        zoom.ContextMenu = stale;
        Assert.False(pane.UpdateContextMenu());
        Assert.NotSame(stale, zoom.ContextMenu);
        Assert.Empty(Assert.IsType<ContextMenu>(zoom.ContextMenu).Items);

        // Re-render, populate the menu, and verify Detach clears it.
        pane.ViewModel.SetSource(Flowchart);
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 2);
        fake.FinishLastRender();
        await WaitUntilAsync(() => pane.ViewModel.HasDiagram);

        Assert.True(pane.UpdateContextMenu());
        Assert.NotNull(zoom.ContextMenu);

        pane.Detach();
        Assert.Null(zoom.ContextMenu);
    }

    [WpfFact]
    public async Task RightClickingTheDiagram_OpensItsMenuOnTheFirstClick_AndAfterARedraw()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        MermaidPane pane = await PaneWithADiagramAsync(window, fake);
        Text_Grab.Controls.ZoomBorder zoom = Assert.IsType<Text_Grab.Controls.ZoomBorder>(pane.FindName("Zoom"));

        // The first right-click on a new diagram.
        Assert.Contains("View full screen", MenuHeaders(MenuWpfOpensOnRightClick(zoom)));

        // A redraw, as after an edit, then the first right-click after it.
        object? before = pane.ViewModel.DiagramImage;
        pane.ViewModel.SetSource("flowchart LR\nA --> C");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 2);
        fake.FinishLastRender();
        await WaitUntilAsync(() => pane.ViewModel.DiagramImage is not null && !ReferenceEquals(pane.ViewModel.DiagramImage, before));

        Assert.Contains("View full screen", MenuHeaders(MenuWpfOpensOnRightClick(zoom)));

        // With nothing drawn there is no menu to open.
        pane.ViewModel.SetSource(null);
        Assert.Null(MenuWpfOpensOnRightClick(zoom));

        pane.Detach();
    }

    /// <summary>
    /// The menu WPF opens for a right-click on <paramref name="element"/>, or null for none. WPF raises
    /// ContextMenuOpening and then opens the menu of the element its class handler picked as the event
    /// routed: the first that already had a menu before any instance handler ran. That target and the
    /// event arguments' constructor are internal to WPF, so they are reached by reflection.
    /// </summary>
    private static ContextMenu? MenuWpfOpensOnRightClick(UIElement element)
    {
        ContextMenuEventArgs args = (ContextMenuEventArgs)Activator.CreateInstance(
            typeof(ContextMenuEventArgs),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            [element, true],
            null)!;
        element.RaiseEvent(args);
        if (args.Handled)
            return null;

        DependencyObject? target = (DependencyObject?)typeof(ContextMenuEventArgs)
            .GetProperty("TargetElement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(args);
        return target is null ? null : ContextMenuService.GetContextMenu(target);
    }

    private static string[] MenuHeaders(ContextMenu? menu)
        => menu is null ? [] : [.. menu.Items.OfType<MenuItem>().Select(item => item.Header?.ToString() ?? string.Empty)];

    [WpfFact]
    public async Task ContextMenuViewFullScreen_OpensViewerOwnedByHostingWindow()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        MermaidPane pane = await PaneWithADiagramAsync(window, fake);
        Window host = new()
        {
            Content = pane,
            Left = 120,
            Top = 80,
            Width = 500,
            Height = 400,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Opacity = 0,
        };

        Window? viewerOwner = null;
        WindowState? viewerState = null;
        Rect viewerBounds = default;
        try
        {
            host.Show();
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            ContextMenu menu = Assert.IsType<ContextMenu>(pane.CreateContextMenu());
            MenuItem fullScreenItem = Assert.Single(
                menu.Items.OfType<MenuItem>(),
                item => string.Equals(item.Header?.ToString(), "View full screen", StringComparison.Ordinal));

            _ = host.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () =>
            {
                Text_Grab.Controls.MermaidViewerWindow? viewer = PresentationSource.CurrentSources
                    .OfType<PresentationSource>()
                    .Select(source => source.RootVisual)
                    .OfType<Text_Grab.Controls.MermaidViewerWindow>()
                    .FirstOrDefault();

                if (viewer is not null)
                {
                    viewerOwner = viewer.Owner;
                    viewerState = viewer.WindowState;
                    viewerBounds = new Rect(viewer.Left, viewer.Top, viewer.Width, viewer.Height);
                    viewer.Close();
                }
            });

            fullScreenItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Same(host, viewerOwner);
            Assert.Equal(WindowState.Normal, viewerState);
            Assert.Equal(host.Left, viewerBounds.Left, 1);
            Assert.Equal(host.Top, viewerBounds.Top, 1);
            Assert.Equal(host.ActualWidth, viewerBounds.Width, 1);
            Assert.Equal(host.ActualHeight, viewerBounds.Height, 1);
        }
        finally
        {
            foreach (Text_Grab.Controls.MermaidViewerWindow viewer in PresentationSource.CurrentSources
                .OfType<PresentationSource>()
                .Select(source => source.RootVisual)
                .OfType<Text_Grab.Controls.MermaidViewerWindow>()
                .ToList())
            {
                viewer.Close();
            }

            host.Close();
            pane.Detach();
        }
    }

    [WpfFact]
    public async Task PaletteCommands_ExistOnlyWhileAttachedWithADiagram()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        MermaidPane pane = Attached(window, fake);
        Assert.Empty(pane.GetPaletteCommands());

        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 1);
        fake.FinishLastRender();
        await WaitUntilAsync(() => pane.ViewModel.HasDiagram);

        Assert.Equal(
            ["Open diagram full screen", "Export diagram as SVG", "Export diagram as PNG", "Copy diagram image"],
            pane.GetPaletteCommands().Select(command => command.Name));

        pane.Detach();
        Assert.Empty(pane.GetPaletteCommands());
    }

    [WpfFact]
    public async Task TheViewHasTheAutomationIdsTheUiTestsUse_AndShowsTheDiagramAndTheError()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        MermaidPane pane = await PaneWithADiagramAsync(window, fake);
        pane.Measure(new Size(420, 400));
        pane.Arrange(new Rect(0, 0, 420, 400));
        pane.UpdateLayout();

        FrameworkElement[] elements = [.. VisualDescendants<FrameworkElement>(pane)];
        string[] ids = [.. elements.Select(System.Windows.Automation.AutomationProperties.GetAutomationId).Where(id => id.StartsWith("EditText.Mermaid", StringComparison.Ordinal))];
        foreach (string id in new[] { "OpenFullScreen", "ExportSvg", "ExportPng", "CopyImage", "ZoomIn", "ZoomOut", "ResetZoom", "Diagram", "Placeholder", "Error" })
            Assert.Contains($"EditText.Mermaid.{id}", ids);

        FrameworkElement diagram = elements.First(element => System.Windows.Automation.AutomationProperties.GetAutomationId(element) == "EditText.Mermaid.Diagram");
        Assert.Equal(Visibility.Visible, diagram.Visibility);
        Assert.Equal(Flowchart, System.Windows.Automation.AutomationProperties.GetHelpText(diagram));
        FrameworkElement strip = elements.First(element => element is Border && element.Name == "ErrorStrip");
        Assert.Equal(Visibility.Collapsed, strip.Visibility);

        pane.ViewModel.SetSource("broken");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => fake.Renders.Count == 2);
        fake.FailLastRender("Syntax error");
        await WaitUntilAsync(() => pane.ViewModel.HasError);
        pane.UpdateLayout();

        Assert.Equal(Visibility.Visible, strip.Visibility);
        Assert.Equal(Flowchart, System.Windows.Automation.AutomationProperties.GetHelpText(diagram));
        TextBlock error = elements.OfType<TextBlock>().First(block => System.Windows.Automation.AutomationProperties.GetAutomationId(block) == "EditText.Mermaid.Error");
        Assert.Equal("Syntax error", error.Text);
        pane.Detach();
    }

    [WpfFact]
    public async Task Zoom_StepsInAndOutAndResets()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        MermaidPane pane = await PaneWithADiagramAsync(window, fake);
        pane.Measure(new Size(420, 400));
        pane.Arrange(new Rect(0, 0, 420, 400));
        pane.UpdateLayout();

        pane.ZoomIn();
        pane.ZoomIn();
        double zoomed = VisualDescendants<Text_Grab.Controls.ZoomBorder>(pane).Single().GetScale();
        pane.ZoomOut();
        double out1 = VisualDescendants<Text_Grab.Controls.ZoomBorder>(pane).Single().GetScale();
        pane.ResetZoom();

        Assert.Equal(1.4, zoomed, 3);
        Assert.Equal(1.2, out1, 3);
        Assert.Equal(1.0, VisualDescendants<Text_Grab.Controls.ZoomBorder>(pane).Single().GetScale(), 3);
        pane.Detach();
    }

    // ---- Which windows make it ----

    [WpfFact]
    public void Classic_NeverMakesTheMermaidPaneOrItsRenderer()
    {
        using IDisposable _ = UsePaneLayout();
        int created = MermaidPane.CreatedCount;
        int attached = MermaidPane.AttachedCount;

        EditTextWindow window = CreateWindow("1 + 1");

        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Equal(created, MermaidPane.CreatedCount);
        Assert.Equal(attached, MermaidPane.AttachedCount);
        Assert.Empty(VisualDescendants<MermaidPane>(PaneHost(window)!));
    }

    [WpfFact]
    public void TheHandWrittenWindow_NeverMakesTheMermaidPane()
    {
        using IDisposable _ = UsePaneLayout(on: false);
        int created = MermaidPane.CreatedCount;

        EditTextWindow window = CreateWindow("1 + 1");

        Assert.Equal(created, MermaidPane.CreatedCount);
        Assert.False(window.UsesPaneLayout);
    }

    [WpfFact]
    public void Mermaid_MakesAndAttachesThePaneRightOfTheEditor_AndClassicTakesItAway()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow(Flowchart);
        int created = MermaidPane.CreatedCount;
        int attached = MermaidPane.AttachedCount;

        Assert.Null(window.ApplyLayout(MermaidLayout));
        LayOut(window);

        Assert.Equal(LayoutIds.Mermaid, window.LayoutId);
        Assert.Equal(created + 1, MermaidPane.CreatedCount);
        Assert.Equal(attached + 1, MermaidPane.AttachedCount);
        MermaidPane pane = PaneIn(window);
        Assert.Equal(Flowchart, pane.ViewModel.WantedSource);
        Assert.True(pane.TranslatePoint(new Point(0, 0), RawTextBox(window)).X > 0, "The diagram pane should be right of the editor.");

        Assert.Null(window.ApplyLayout(BuiltInLayouts.Classic));
        LayOut(window);

        Assert.Equal(attached, MermaidPane.AttachedCount);
        Assert.Null(pane.ViewModel.WantedSource);
        Assert.Empty(VisualDescendants<MermaidPane>(PaneHost(window)!));
    }

    [WpfFact]
    public void Mermaid_CarriesTheLayoutIdIntoTheHistoryItem()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow(Flowchart);

        Assert.Null(window.ApplyLayout(MermaidLayout));

        Assert.Equal(LayoutIds.Mermaid, window.AsHistoryItem().LayoutId);
    }

    [WpfFact]
    public void ApplyingMermaid_KeepsTheModeTheWindowHas_BecauseOnlyOpeningAWindowSetsIt()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow(Flowchart);

        Assert.Null(window.ApplyLayout(MermaidLayout));

        Assert.Equal(EtwEditorMode.Text, window.Session.Mode);
    }

    [WpfFact]
    public async Task AnInitiallyCollapsedMermaidPaneInALayout_DrawsNothingUntilShown()
    {
        ManualMermaid fake = new();
        EditTextWindow window = CreateWindow(Flowchart);
        MermaidPane pane = new(fake.CreateViewModel(), () => true);

        EtwLayoutDefinition collapsedLayout = MermaidLayout with
        {
            Root = new SplitNode(
                DockOrientation.Horizontal,
                [
                    new DockChild(new PaneNode(PaneIds.Editor), 1, DockSizeUnit.Star),
                    new DockChild(new PaneNode(PaneIds.Mermaid), 420, DockSizeUnit.Pixel, Collapsed: true),
                ]),
        };

        LayoutRenderer renderer = new(
            new Border(),
            paneId => paneId == PaneIds.Mermaid ? pane : null,
            (created, config) => created.Attach(window.Session, config),
            created => created.Detach());

        renderer.Render(collapsedLayout);

        Assert.False(renderer.IsPaneVisible(PaneIds.Mermaid));
        Assert.Equal(Flowchart, pane.ViewModel.WantedSource);
        Assert.Empty(fake.Delays);
        Assert.Empty(fake.Renders);

        RawTextBox(window).Text = "flowchart LR\nA --> C";
        Assert.Equal("flowchart LR\nA --> C", pane.ViewModel.WantedSource);
        Assert.Empty(fake.Delays);
        Assert.Empty(fake.Renders);

        renderer.SetPaneVisible(PaneIds.Mermaid, true);
        await WaitUntilAsync(() => fake.Renders.Count == 1);

        Assert.True(renderer.IsPaneVisible(PaneIds.Mermaid));
        Assert.Equal("flowchart LR\nA --> C", fake.Renders[0].Source);

        renderer.Close();
    }
}
