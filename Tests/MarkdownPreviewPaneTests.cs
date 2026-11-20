using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab;
using Text_Grab.Controls;
using Text_Grab.Core.Actions;
using Text_Grab.Core.Editing;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;
using static Tests.CalcPaneTests;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>A delay the test finishes by hand, and a renderer that makes a plain one-paragraph document.</summary>
internal sealed class ManualPreview
{
    public sealed record Delay(TimeSpan Time, CancellationToken Token, TaskCompletionSource Completion);

    public List<Delay> Delays { get; } = [];

    public List<(string Source, bool IsLightTheme)> Renders { get; } = [];

    public Func<string, bool>? FailWhen { get; set; }

    public Task DelayAsync(TimeSpan time, CancellationToken token)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        token.Register(() => completion.TrySetCanceled(token));
        Delays.Add(new Delay(time, token, completion));
        return completion.Task;
    }

    public MarkdownPreviewRender Render(string source, bool isLightTheme)
    {
        Renders.Add((source, isLightTheme));
        if (FailWhen?.Invoke(source) == true)
            throw new InvalidOperationException("Cannot render\nthis");

        return new MarkdownPreviewRender(new FlowDocument(new Paragraph(new Run(source))), source);
    }

    public MarkdownPreviewViewModel CreateViewModel() => new(Render, DelayAsync);

    public void FinishLastDelay() => Delays[^1].Completion.TrySetResult();
}

/// <summary>The Markdown preview pane's state, its debounce and its last-good-document rule (v5 E5 <c>markdown-preview</c>).</summary>
public class MarkdownPreviewViewModelTests
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
        => Assert.Equal(TimeSpan.FromMilliseconds(300), MarkdownPreviewViewModel.DebounceDelay);

    [WpfFact]
    public async Task EachChangeRestartsTheDelay_AndOnlyTheLastOneIsRendered()
    {
        ManualPreview fake = new();
        MarkdownPreviewViewModel viewModel = fake.CreateViewModel();

        viewModel.SetSource("# One");
        viewModel.SetSource("# Two");

        Assert.Equal(2, fake.Delays.Count);
        Assert.All(fake.Delays, delay => Assert.Equal(MarkdownPreviewViewModel.DebounceDelay, delay.Time));
        Assert.True(fake.Delays[0].Token.IsCancellationRequested);
        Assert.Empty(fake.Renders);

        fake.FinishLastDelay();
        await WaitUntilAsync(() => viewModel.HasDocument);

        Assert.Equal([("# Two", true)], fake.Renders);
        Assert.Equal("# Two", viewModel.RenderedSource);
        Assert.Equal("# Two", viewModel.RenderedTextSummary.Trim());
        Assert.Equal(1, viewModel.RenderCalls);
    }

    [WpfFact]
    public async Task AFailedRender_KeepsTheLastGoodDocumentAndShowsTheError_AndTheNextGoodRenderClearsIt()
    {
        ManualPreview fake = new() { FailWhen = source => source.Contains("bad", StringComparison.Ordinal) };
        MarkdownPreviewViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("good");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => viewModel.HasDocument);
        FlowDocument good = viewModel.Document!;

        viewModel.SetSource("bad");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => viewModel.HasError);

        Assert.Same(good, viewModel.Document);
        Assert.Equal("good", viewModel.RenderedSource);
        Assert.Equal("Cannot render", viewModel.ErrorMessage);
        Assert.Equal(string.Empty, viewModel.PlaceholderText);

        viewModel.SetSource("good again");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => !viewModel.HasError);

        Assert.Equal("good again", viewModel.RenderedSource);
        Assert.NotSame(good, viewModel.Document);
    }

    [WpfFact]
    public async Task GoingBackToTheSourceAlreadyShown_ClearsTheErrorWithoutRenderingAgain()
    {
        ManualPreview fake = new() { FailWhen = source => source == "bad" };
        MarkdownPreviewViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("good");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => viewModel.HasDocument);
        viewModel.SetSource("bad");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => viewModel.HasError);

        viewModel.SetSource("good");

        Assert.False(viewModel.HasError);
        Assert.Equal(2, fake.Renders.Count);
    }

    [WpfTheory]
    [InlineData(null, null, MarkdownPreviewViewModel.EmptySourceText)]
    [InlineData("", null, MarkdownPreviewViewModel.EmptySourceText)]
    [InlineData("  \n ", null, MarkdownPreviewViewModel.EmptySourceText)]
    [InlineData(null, MarkdownPreviewViewModel.MarkdownModeText, MarkdownPreviewViewModel.MarkdownModeText)]
    public async Task NothingToRender_ClearsTheDocumentAtOnceAndSaysWhy(string? source, string? notice, string expectedText)
    {
        ManualPreview fake = new();
        MarkdownPreviewViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("good");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => viewModel.HasDocument);
        int cleared = 0;
        viewModel.DocumentCleared += (_, _) => cleared++;

        viewModel.SetSource(source, notice);

        Assert.False(viewModel.HasDocument);
        Assert.Null(viewModel.RenderedSource);
        Assert.False(viewModel.HasError);
        Assert.Equal(expectedText, viewModel.PlaceholderText);
        Assert.Equal(1, cleared);
        Assert.Single(fake.Renders);
    }

    [WpfFact]
    public async Task ChangingTheTheme_RendersAtOnceInTheNewColors()
    {
        ManualPreview fake = new();
        MarkdownPreviewViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("good");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => viewModel.HasDocument);

        viewModel.SetTheme(lightTheme: false);
        await WaitUntilAsync(() => fake.Renders.Count == 2);

        Assert.False(fake.Renders[1].IsLightTheme);
        Assert.Single(fake.Delays);
        viewModel.SetTheme(lightTheme: false);
        Assert.Equal(2, fake.Renders.Count);
    }

    [WpfFact]
    public async Task AHiddenPaneRendersNothing_AndCatchesUpWhenShown()
    {
        ManualPreview fake = new();
        MarkdownPreviewViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("one");
        viewModel.SetActive(false);

        Assert.True(fake.Delays[0].Token.IsCancellationRequested);

        viewModel.SetSource("two");
        viewModel.SetTheme(lightTheme: false);
        Assert.Single(fake.Delays);
        Assert.Empty(fake.Renders);

        viewModel.SetActive(true);
        await WaitUntilAsync(() => viewModel.HasDocument);

        Assert.Equal([("two", false)], fake.Renders);
        Assert.Single(fake.Delays);
    }

    [WpfFact]
    public async Task Release_StopsPendingWorkAndLetsGoOfTheDocument()
    {
        ManualPreview fake = new();
        MarkdownPreviewViewModel viewModel = fake.CreateViewModel();
        viewModel.SetSource("good");
        fake.FinishLastDelay();
        await WaitUntilAsync(() => viewModel.HasDocument);
        viewModel.SetSource("good, changed");

        viewModel.Release();

        Assert.False(viewModel.HasDocument);
        Assert.Null(viewModel.RenderedSource);
        Assert.Null(viewModel.WantedSource);
        Assert.Equal(string.Empty, viewModel.RenderedTextSummary);
        Assert.True(fake.Delays[^1].Token.IsCancellationRequested);
    }
}

/// <summary>The Markdown preview pane in an Edit Text Window, and the layout that has it (v5 E5 <c>markdown-preview</c>).</summary>
public class MarkdownPreviewPaneTests
{
    private const string Markdown = "# Title\n\nHello **bold** world\n\n- [ ] task\n";

    private static EtwLayoutDefinition PreviewLayout => BuiltInLayouts.TryGet(LayoutIds.MarkdownPreview, out EtwLayoutDefinition layout)
        ? layout
        : throw new InvalidOperationException("markdown-preview is not built in.");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was never met.");
            await Task.Delay(10);
        }
    }

    private static MarkdownPreviewPane Attached(EditTextWindow window, ManualPreview fake, bool lightTheme = true)
    {
        MarkdownPreviewPane pane = new(fake.CreateViewModel(), () => lightTheme);
        pane.Attach(window.Session, config: null);
        return pane;
    }

    private static async Task<MarkdownPreviewPane> RealPaneAsync(EditTextWindow window)
    {
        MarkdownPreviewPane pane = new();
        pane.Attach(window.Session, config: null);
        await WaitUntilAsync(() => pane.ViewModel.HasDocument);
        pane.Measure(new Size(420, 400));
        pane.Arrange(new Rect(0, 0, 420, 400));
        pane.UpdateLayout();
        return pane;
    }

    /// <summary>The position in <paramref name="paragraph"/> after <paramref name="renderedOffset"/> characters of its text.</summary>
    private static TextPointer PositionIn(Paragraph paragraph, int renderedOffset)
    {
        TextPointer navigator = paragraph.ContentStart;
        while (new TextRange(paragraph.ContentStart, navigator).Text.Length < renderedOffset)
            navigator = navigator.GetNextInsertionPosition(LogicalDirection.Forward)!;

        return navigator;
    }

    private static Paragraph ParagraphWith(FlowDocument document, string text)
        => document.Blocks.OfType<Paragraph>().First(paragraph => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Contains(text, StringComparison.Ordinal));

    // ---- The pane and its factory ----

    [Fact]
    public void PaneIds_AndActionIdsArePinned()
    {
        Assert.Equal("textgrab.pane.markdownPreview", PaneIds.MarkdownPreview);
        Assert.Equal(PaneIds.MarkdownPreview, MarkdownPreviewPane.PaneId);

        ActionRegistry registry = new();
        MarkdownPreviewPaneActions.RegisterAll(registry);
        Assert.Equal(["textgrab.markdownPreview.goToSource"], registry.All.Select(descriptor => descriptor.Id));
    }

    [WpfFact]
    public void Factory_IsRegisteredAndMakesUnattachedPanes()
    {
        IEditorPaneFactory factory = Assert.Single(
            AppServices.Provider.GetServices<IEditorPaneFactory>(),
            candidate => candidate.Descriptor.Id == MarkdownPreviewPane.PaneId);
        int attached = MarkdownPreviewPane.AttachedCount;

        IEditorPane pane = factory.Create();

        MarkdownPreviewPane preview = Assert.IsType<MarkdownPreviewPane>(pane);
        Assert.Same(preview, pane.View);
        Assert.Equal(MarkdownPreviewPane.Definition, pane.Descriptor);
        Assert.Equal(("Preview", PaneDock.Right), (pane.Descriptor.Title, pane.Descriptor.DefaultDock));
        Assert.True(Enum.IsDefined(typeof(Wpf.Ui.Controls.SymbolRegular), pane.Descriptor.IconKey), pane.Descriptor.IconKey);
        Assert.Null(pane.SaveConfig());
        Assert.Equal(attached, MarkdownPreviewPane.AttachedCount);
        Assert.NotSame(pane, factory.Create());
        Assert.Empty(pane.GetPaletteCommands());
    }

    [Fact]
    public void ResolvingTheFactories_MakesNoPane()
    {
        int created = MarkdownPreviewPane.CreatedCount;

        _ = AppServices.Provider.GetServices<IEditorPaneFactory>().Select(factory => factory.Descriptor).ToList();

        Assert.Equal(created, MarkdownPreviewPane.CreatedCount);
    }

    [WpfFact]
    public void Attach_RendersTheDocument_AndTwiceIsAnError()
    {
        ManualPreview fake = new();
        EditTextWindow window = CreateWindow(Markdown);
        MarkdownPreviewPane pane = Attached(window, fake);

        Assert.Equal(Markdown, pane.ViewModel.WantedSource);
        Assert.Single(fake.Delays);
        Assert.Throws<InvalidOperationException>(() => pane.Attach(window.Session, config: null));
        pane.Detach();
    }

    [WpfFact]
    public void Attach_RendersInTheThemeTheSystemHas()
    {
        EditTextWindow window = CreateWindow(Markdown);

        MarkdownPreviewPane pane = Attached(window, new ManualPreview(), lightTheme: false);

        Assert.False(pane.ViewModel.IsLightTheme);
        pane.Detach();
    }

    [WpfFact]
    public void EditingTheSource_UpdatesWhatThePreviewWillRender()
    {
        ManualPreview fake = new();
        EditTextWindow window = CreateWindow(Markdown);
        MarkdownPreviewPane pane = Attached(window, fake);

        RawTextBox(window).Text = "# Changed";

        Assert.Equal("# Changed", pane.ViewModel.WantedSource);
        Assert.Equal(2, fake.Delays.Count);
        pane.Detach();
    }

    [WpfFact]
    public void InMarkdownMode_ThePaneShowsANoticeInsteadOfASecondCopy_AndRendersAgainOutsideIt()
    {
        EditTextWindow window = CreateWindow(Markdown);
        MarkdownPreviewPane pane = Attached(window, new ManualPreview());

        SwitchMode(window, EtwEditorMode.Markdown);

        Assert.Null(pane.ViewModel.WantedSource);
        Assert.Equal(MarkdownPreviewViewModel.MarkdownModeText, pane.ViewModel.PlaceholderText);

        SwitchMode(window, EtwEditorMode.Code);

        Assert.Equal(window.Session.Text, pane.ViewModel.WantedSource);
        pane.Detach();
    }

    [WpfFact]
    public void Detach_StopsFollowingTheDocument_ReleasesAndMayBeCalledAgain()
    {
        ManualPreview fake = new();
        EditTextWindow window = CreateWindow(Markdown);
        int attached = MarkdownPreviewPane.AttachedCount;
        MarkdownPreviewPane pane = Attached(window, fake);
        Assert.Equal(attached + 1, MarkdownPreviewPane.AttachedCount);

        pane.Detach();
        pane.Detach();
        RawTextBox(window).Text = "changed after detach";

        Assert.Equal(attached, MarkdownPreviewPane.AttachedCount);
        Assert.Null(pane.ViewModel.WantedSource);
        Assert.Single(fake.Delays);
    }

    [WpfFact]
    public async Task AHiddenPaneRendersNothingUntilItIsShown()
    {
        ManualPreview fake = new();
        EditTextWindow window = CreateWindow(Markdown);
        MarkdownPreviewPane pane = Attached(window, fake);

        pane.OnVisibilityChanged(false);
        RawTextBox(window).Text = "# Hidden change";
        Assert.Single(fake.Delays);

        pane.OnVisibilityChanged(true);
        await WaitUntilAsync(() => fake.Renders.Count == 1);

        Assert.Equal("# Hidden change", fake.Renders[0].Source);
        pane.Detach();
    }

    [WpfFact]
    public void WindowActivated_ReadsTheThemeAgain()
    {
        EditTextWindow window = CreateWindow(Markdown);
        bool light = true;
        MarkdownPreviewPane pane = new(new ManualPreview().CreateViewModel(), () => light);
        pane.Attach(window.Session, config: null);

        light = false;
        ((IEditorPane)pane).OnWindowActivated();

        Assert.False(pane.ViewModel.IsLightTheme);
        pane.Detach();
    }

    // ---- The real rendering ----

    [WpfFact]
    public async Task TheRealPane_RendersTheMarkdownReadOnlyAndHasTheAutomationIdsTheUiTestsUse()
    {
        EditTextWindow window = CreateWindow(Markdown);
        MarkdownPreviewPane pane = await RealPaneAsync(window);

        Assert.True(pane.PreviewBox.IsReadOnly);
        string text = new TextRange(pane.PreviewBox.Document.ContentStart, pane.PreviewBox.Document.ContentEnd).Text;
        Assert.Contains("Hello bold world", text, StringComparison.Ordinal);
        Assert.DoesNotContain("**", text, StringComparison.Ordinal);
        Assert.Contains("Hello bold world", pane.ViewModel.RenderedTextSummary, StringComparison.Ordinal);

        string[] ids = [.. VisualDescendants<FrameworkElement>(pane).Select(System.Windows.Automation.AutomationProperties.GetAutomationId).Where(id => id.StartsWith("EditText.MarkdownPreview", StringComparison.Ordinal))];
        foreach (string id in new[] { "Document", "Placeholder", "Error" })
            Assert.Contains($"EditText.MarkdownPreview.{id}", ids);

        FrameworkElement box = pane.PreviewBox;
        Assert.Contains("Hello bold world", System.Windows.Automation.AutomationProperties.GetHelpText(box), StringComparison.Ordinal);
        pane.Detach();
    }

    [WpfFact]
    public async Task TheRealPane_ReplacesThePreviewWhenTheSourceChanges_AndLetsGoOfItOnDetach()
    {
        EditTextWindow window = CreateWindow(Markdown);
        MarkdownPreviewPane pane = await RealPaneAsync(window);

        RawTextBox(window).Text = "Second *draft*";
        await WaitUntilAsync(() => pane.ViewModel.RenderedSource == "Second *draft*");

        Assert.Contains("Second draft", new TextRange(pane.PreviewBox.Document.ContentStart, pane.PreviewBox.Document.ContentEnd).Text, StringComparison.Ordinal);

        pane.Detach();

        Assert.DoesNotContain("Second", new TextRange(pane.PreviewBox.Document.ContentStart, pane.PreviewBox.Document.ContentEnd).Text, StringComparison.Ordinal);
    }

    [WpfFact]
    public async Task TaskListBoxesInThePreviewCannotBeToggled()
    {
        EditTextWindow window = CreateWindow(Markdown);
        MarkdownPreviewPane pane = await RealPaneAsync(window);

        List<CheckBox> boxes = [];
        void Collect(DependencyObject parent)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                if (child is InlineUIContainer { Child: CheckBox box })
                    boxes.Add(box);
                else if (child is DependencyObject element)
                    Collect(element);
            }
        }

        Collect(pane.PreviewBox.Document);

        CheckBox only = Assert.Single(boxes);
        Assert.False(only.IsHitTestVisible);
        Assert.False(only.Focusable);
        pane.Detach();
    }

    // ---- Preview position to source offset ----

    [WpfFact]
    public void TheSyncMap_PutsAClickInAWordInThatWordsPlaceInTheSource()
    {
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(Markdown, new System.Windows.Media.FontFamily("Segoe UI"), 14);
        MarkdownPreviewSync sync = new(document, Markdown);
        Paragraph hello = ParagraphWith(document, "Hello");
        string rendered = new TextRange(hello.ContentStart, hello.ContentEnd).Text;

        int? inBold = sync.RawOffsetAt(PositionIn(hello, rendered.IndexOf("bold", StringComparison.Ordinal) + 2));
        int? inWorld = sync.RawOffsetAt(PositionIn(hello, rendered.IndexOf("world", StringComparison.Ordinal) + 2));
        int? inTitle = sync.RawOffsetAt(PositionIn(ParagraphWith(document, "Title"), 2));

        int bold = Markdown.IndexOf("bold", StringComparison.Ordinal);
        int world = Markdown.IndexOf("world", StringComparison.Ordinal);
        Assert.InRange(inBold!.Value, bold - 2, bold + 6);
        Assert.InRange(inWorld!.Value, world, world + 5);
        Assert.InRange(inTitle!.Value, 0, Markdown.IndexOf('\n', StringComparison.Ordinal));
    }

    [WpfFact]
    public void TheSyncMap_NeverGoesPastTheEndOfTheSource()
    {
        const string source = "tail";
        FlowDocument document = MarkdownFlowDocumentUtilities.CreateFlowDocument(source, new System.Windows.Media.FontFamily("Segoe UI"), 14);
        MarkdownPreviewSync sync = new(document, source);

        int? end = sync.RawOffsetAt(document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward));

        Assert.InRange(end!.Value, 0, source.Length);
    }

    [WpfFact]
    public async Task GoToSource_MovesTheCaretInTheEditorToTheMatchingPlace()
    {
        EditTextWindow window = CreateWindow(Markdown);
        MarkdownPreviewPane pane = await RealPaneAsync(window);
        Paragraph hello = ParagraphWith(pane.PreviewBox.Document, "Hello");
        string rendered = new TextRange(hello.ContentStart, hello.ContentEnd).Text;

        bool moved = pane.GoToSource(PositionIn(hello, rendered.IndexOf("world", StringComparison.Ordinal) + 1));

        Assert.True(moved);
        int world = Markdown.IndexOf("world", StringComparison.Ordinal);
        Assert.InRange(window.Session.Selection.Start, world, world + 5);
        Assert.Equal(0, window.Session.Selection.Length);
        Assert.False(pane.GoToSource(null));
        pane.Detach();
    }

    [WpfFact]
    public async Task PaletteCommands_ExistOnlyWhileAttachedWithADocument()
    {
        EditTextWindow window = CreateWindow(Markdown);
        MarkdownPreviewPane pane = new();
        Assert.Empty(pane.GetPaletteCommands());
        pane.Attach(window.Session, config: null);
        Assert.Empty(pane.GetPaletteCommands());
        await WaitUntilAsync(() => pane.ViewModel.HasDocument);

        Assert.Single(pane.GetPaletteCommands());

        pane.Detach();
        Assert.Empty(pane.GetPaletteCommands());
    }

    // ---- Which windows make it ----

    [WpfFact]
    public void Classic_NeverMakesTheMarkdownPreviewPane()
    {
        using IDisposable _ = UsePaneLayout();
        int created = MarkdownPreviewPane.CreatedCount;

        EditTextWindow window = CreateWindow(Markdown);

        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Equal(created, MarkdownPreviewPane.CreatedCount);
        Assert.Empty(VisualDescendants<MarkdownPreviewPane>(PaneHost(window)!));
    }

    [WpfFact]
    public void TheHandWrittenWindow_NeverMakesTheMarkdownPreviewPane()
    {
        using IDisposable _ = UsePaneLayout(on: false);
        int created = MarkdownPreviewPane.CreatedCount;

        EditTextWindow window = CreateWindow(Markdown);

        Assert.Equal(created, MarkdownPreviewPane.CreatedCount);
        Assert.False(window.UsesPaneLayout);
    }

    [WpfFact]
    public void MarkdownPreview_MakesAndAttachesThePaneRightOfTheEditor_AndClassicTakesItAway()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow(Markdown);
        int attached = MarkdownPreviewPane.AttachedCount;

        Assert.Null(window.ApplyLayout(PreviewLayout));
        LayOut(window);

        Assert.Equal(LayoutIds.MarkdownPreview, window.LayoutId);
        Assert.Equal(attached + 1, MarkdownPreviewPane.AttachedCount);
        MarkdownPreviewPane pane = Assert.Single(VisualDescendants<MarkdownPreviewPane>(PaneHost(window)!));
        Assert.Equal(Markdown, pane.ViewModel.WantedSource);
        Assert.True(pane.TranslatePoint(new Point(0, 0), RawTextBox(window)).X > 0, "The preview should be right of the editor.");
        Assert.Equal(LayoutIds.MarkdownPreview, window.AsHistoryItem().LayoutId);

        Assert.Null(window.ApplyLayout(BuiltInLayouts.Classic));
        LayOut(window);

        Assert.Equal(attached, MarkdownPreviewPane.AttachedCount);
        Assert.Null(pane.ViewModel.WantedSource);
        Assert.Empty(VisualDescendants<MarkdownPreviewPane>(PaneHost(window)!));
    }

    [WpfFact]
    public void ThePaneReadsTheOpenFilesPathFromTheWindow()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow(Markdown);

        Assert.Null(window.ApplyLayout(PreviewLayout));
        LayOut(window);

        MarkdownPreviewPane pane = Assert.Single(VisualDescendants<MarkdownPreviewPane>(PaneHost(window)!));
        Assert.NotNull(pane.FilePathProvider);
        Assert.Null(pane.FilePathProvider!());
    }
}

/// <summary>
/// Opening Markdown in the Markdown preview layout (v5 E5 <c>markdown-preview</c>): the layout's
/// <c>DefaultMode</c> and <c>DefaultCodeLanguage</c> meeting what a file would open in, and the files that never reach it.
/// </summary>
[Collection("History service")]
public sealed class EditTextWindowMarkdownPreviewLayoutTests : IDisposable
{
    private const string Document = "# Title\r\n\r\nSome **bold** text\r\n";

    private readonly string root = Path.Combine(Path.GetTempPath(), $"text-grab-md-preview-{Guid.NewGuid():N}");
    private readonly IDisposable profileScope;

    public EditTextWindowMarkdownPreviewLayoutTests()
    {
        AutomationProfile profile = AutomationProfile.TryCreate(
            ["Text-Grab.exe"],
            name => name == AutomationProfile.ProfileEnvironmentVariable ? root : null)
            ?? throw new InvalidOperationException("Failed to create automation profile.");
        profileScope = AutomationProfile.OverrideCurrentForTests(profile);
        Directory.CreateDirectory(Path.Combine(root, "files"));
    }

    public void Dispose()
    {
        profileScope.Dispose();
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private string WriteFile(string name, string text)
    {
        string path = Path.Combine(root, "files", name);
        File.WriteAllText(path, text);
        return path;
    }

    private static string? PendingLayoutNotice(EditTextWindow window)
        => (string?)typeof(EditTextWindow)
            .GetField("pendingLayoutNotice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(window);

    [WpfFact]
    public async Task AMarkdownFileOpenedWithTheLayout_IsInCodeModeWithMarkdownHighlighting_AndNeverMakesTheCalcPane()
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile("notes.md", Document);
        int calcCreated = CalcPane.CreatedCount;

        EditTextWindow window = new(new LayoutRequest(RequestedId: LayoutIds.MarkdownPreview, FilePath: path));
        LayOut(window);
        Assert.Equal(LayoutIds.MarkdownPreview, window.LayoutId);
        Assert.Equal(calcCreated, CalcPane.CreatedCount);

        await window.OpenPath(path);
        LayOut(window);

        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal(CodeLanguages.Markdown, window.CodeLanguageName);
        Assert.Equal(Document, window.Session.Text);
        Assert.Equal(calcCreated, CalcPane.CreatedCount);
        Assert.Null(window.FindName("CalcPaneControl"));
        MarkdownPreviewPane pane = Assert.Single(VisualDescendants<MarkdownPreviewPane>(PaneHost(window)!));
        Assert.Equal(Document, pane.ViewModel.WantedSource);
        Assert.Equal(path, pane.FilePathProvider!());
    }

    [WpfFact]
    public async Task TheLanguageStaysMarkdownWhenTheWindowIsLoadedAfterTheFile()
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile("notes.md", Document);
        EditTextWindow window = new(new LayoutRequest(RequestedId: LayoutIds.MarkdownPreview, FilePath: path));
        LayOut(window);
        await window.OpenPath(path);

        // What Window_Loaded does for a window that already holds a file in Code mode.
        SwitchMode(window, EtwEditorMode.Code);

        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal(CodeLanguages.Markdown, window.CodeLanguageName);
    }

    [WpfFact]
    public async Task ATextFileOpenedWithTheLayout_AlsoGetsMarkdownHighlighting()
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile("notes.txt", Document);
        EditTextWindow window = new(new LayoutRequest(RequestedId: LayoutIds.MarkdownPreview, FilePath: path));
        LayOut(window);

        await window.OpenPath(path);

        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal(CodeLanguages.Markdown, window.CodeLanguageName);
    }

    [WpfFact]
    public void LayoutMarkdownPreview_OnABlankWindow_OpensCodeModeWithMarkdownHighlighting()
    {
        using IDisposable _ = UsePaneLayout();
        int calcCreated = CalcPane.CreatedCount;

        EditTextWindow window = new(new LayoutRequest(RequestedId: LayoutIds.MarkdownPreview));
        LayOut(window);

        Assert.Equal(LayoutIds.MarkdownPreview, window.LayoutId);
        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal(CodeLanguages.Markdown, window.CodeLanguageName);
        Assert.Equal(calcCreated, CalcPane.CreatedCount);
        Assert.Single(VisualDescendants<MarkdownPreviewPane>(PaneHost(window)!));
        Assert.Null(PendingLayoutNotice(window));
    }

    [WpfFact]
    public void LayoutMarkdownPreview_WithTextGivenToTheWindow_OpensCodeMode()
    {
        using IDisposable _ = UsePaneLayout();

        EditTextWindow window = new("# Hi", isEncoded: false, layoutId: LayoutIds.MarkdownPreview);
        LayOut(window);

        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal(CodeLanguages.Markdown, window.CodeLanguageName);
        Assert.Equal("# Hi", window.Session.Text);
    }

    [WpfFact]
    public async Task AMarkdownFileWithNoLayout_StillOpensInClassicInMarkdownMode_AndNeverMakesThePane()
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile("readme.md", Document);
        int created = MarkdownPreviewPane.CreatedCount;

        EditTextWindow fresh = new(new LayoutRequest(FilePath: path));
        LayOut(fresh);
        await fresh.OpenPath(path);
        EditTextWindow existing = CreateWindow(string.Empty);
        await existing.OpenPath(path);

        Assert.Equal(LayoutIds.Classic, fresh.LayoutId);
        Assert.Equal(EtwEditorMode.Markdown, fresh.Session.Mode);
        Assert.Equal(LayoutIds.Classic, existing.LayoutId);
        Assert.Equal(EtwEditorMode.Markdown, existing.Session.Mode);
        Assert.Equal(created, MarkdownPreviewPane.CreatedCount);
        Assert.NotNull(fresh.FindName("CalcPaneControl"));
    }

    [WpfFact]
    public async Task AHistoryItemInTheLayout_KeepsTheModeAndLanguageItWasSavedWith()
    {
        using IDisposable _ = UsePaneLayout();

        EditTextWindow window = new(new HistoryInfo { TextContent = Document, EditorMode = EtwEditorMode.Text, LayoutId = LayoutIds.MarkdownPreview });
        LayOut(window);
        await Task.CompletedTask;

        Assert.Equal(LayoutIds.MarkdownPreview, window.LayoutId);
        Assert.Equal(EtwEditorMode.Text, window.Session.Mode);
    }

    [WpfFact]
    public void LayoutMarkdownPreview_WithThePaneLayoutOff_SaysWhyAndChangesNothing()
    {
        using IDisposable _ = UsePaneLayout(on: false);
        int created = MarkdownPreviewPane.CreatedCount;

        EditTextWindow window = new(new LayoutRequest(RequestedId: LayoutIds.MarkdownPreview));

        Assert.Contains("pane layout", PendingLayoutNotice(window));
        Assert.Equal(EtwEditorMode.Text, window.Session.Mode);
        Assert.Equal(created, MarkdownPreviewPane.CreatedCount);
    }
}
