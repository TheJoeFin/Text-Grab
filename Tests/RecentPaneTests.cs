using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab;
using Text_Grab.Core.Actions;
using Text_Grab.Core.Editing;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using Text_Grab.Services;
using static Tests.CalcPaneTests;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// The recent items every <see cref="RecentPane"/> made in these tests reads, in place of the person's
/// history. Empty unless a test sets <see cref="Items"/>; the value flows with the test's own async
/// context, so tests that run side by side do not see each other's items.
/// </summary>
internal sealed class FakeRecentItemsSource : IRecentItemsSource
{
    private static readonly AsyncLocal<IReadOnlyList<TrayFlyoutItem>?> items = new();

    [ModuleInitializer]
    internal static void ReplaceTheHistoryForTheWholeAssembly() => RecentItemsSources.Override = new FakeRecentItemsSource();

    public static IReadOnlyList<TrayFlyoutItem> Items
    {
        get => items.Value ?? [];
        set => items.Value = value;
    }

    public int LoadCount { get; private set; }

    public IReadOnlyList<TrayFlyoutItem> Load()
    {
        LoadCount++;
        return Items;
    }
}

/// <summary>A source a test owns, which counts its loads and changes between them.</summary>
internal sealed class ListRecentItemsSource(params TrayFlyoutItem[] initial) : IRecentItemsSource
{
    public List<TrayFlyoutItem> Items { get; } = [.. initial];

    public int LoadCount { get; private set; }

    public IReadOnlyList<TrayFlyoutItem> Load()
    {
        LoadCount++;

        // As the real source does: newest first.
        return [.. Items.OrderByDescending(item => item.Timestamp)];
    }
}

/// <summary>The recent items pane's state and the rules for where an item opens (v5 E5 <c>recent</c>).</summary>
public class RecentItemsViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    internal static TrayFlyoutItem Text(string text, int minutesAgo = 0, string? id = null)
        => new(TrayFlyoutItemKind.EditText, new HistoryInfo
        {
            ID = id ?? Guid.NewGuid().ToString(),
            TextContent = text,
            CaptureDateTime = Now.AddMinutes(-minutesAgo),
        });

    internal static TrayFlyoutItem Grab(string text, int minutesAgo = 0)
        => new(TrayFlyoutItemKind.GrabFrame, new HistoryInfo
        {
            ID = Guid.NewGuid().ToString(),
            TextContent = text,
            CaptureDateTime = Now.AddMinutes(-minutesAgo),
        });

    internal static TrayFlyoutItem File(string path, int minutesAgo = 0)
        => new(TrayFlyoutItemKind.File, new HistoryInfo
        {
            ID = Guid.NewGuid().ToString(),
            SourcePath = path,
            CaptureDateTime = Now.AddMinutes(-minutesAgo),
        });

    private static RecentItemsViewModel Model(ListRecentItemsSource source)
    {
        RecentItemsViewModel model = new(source);
        model.Refresh();
        return model;
    }

    [Fact]
    public void Nothing_IsLoadedUntilTheFirstRefresh()
    {
        ListRecentItemsSource source = new(Text("one"));

        RecentItemsViewModel model = new(source);

        Assert.Equal(0, source.LoadCount);
        Assert.Empty(model.Items);
        Assert.True(model.IsEmpty);
    }

    [Fact]
    public void Refresh_ShowsEveryKindNewestFirst()
    {
        ListRecentItemsSource source = new(Text("older", 30), Grab("grab", 10), File(@"C:\docs\report.txt", 5), Text("newest", 1));

        RecentItemsViewModel model = Model(source);

        Assert.Equal(["newest", "report.txt", "grab", "older"], model.Items.Select(item => item.Title));
        Assert.False(model.IsEmpty);
    }

    [Fact]
    public void Query_MatchesTheTitleOrTheTextOfEveryTermAndReachesPastTheRowCap()
    {
        ListRecentItemsSource source = new();
        source.Items.AddRange(Enumerable.Range(0, 150).Select(i => Text($"note {i}", i)));
        source.Items.Add(Text("a needle at the very end", 500));
        RecentItemsViewModel model = Model(source);

        Assert.Equal(TrayFlyoutItemProvider.MaxItemCount, model.Items.Count);

        model.Query = "NEEDLE end";

        Assert.Equal(["a needle at the very end"], model.Items.Select(item => item.Title));
    }

    [Fact]
    public void EmptyText_SaysWhetherThereIsNothingOrNothingMatches()
    {
        ListRecentItemsSource source = new();
        RecentItemsViewModel model = Model(source);
        Assert.Equal("Nothing here yet", model.EmptyText);

        source.Items.Add(Text("one"));
        model.Refresh();
        model.Query = "zzz";

        Assert.True(model.IsEmpty);
        Assert.Equal("No matches", model.EmptyText);
    }

    [Fact]
    public void Refresh_KeepsTheSearchAndTheSelectedRowAndPicksUpNewItems()
    {
        TrayFlyoutItem keep = Text("keep this one", 5, id: "keep");
        ListRecentItemsSource source = new(keep, Text("other keeper", 6));
        RecentItemsViewModel model = Model(source);
        model.Query = "keep";
        model.SelectedItem = model.Items[0];

        source.Items.Insert(0, Text("fresh keep", 0));
        source.Items.Remove(keep);
        source.Items.Add(Text("keep this one", 5, id: "keep"));
        model.Refresh();

        Assert.Equal("keep", model.Query);
        Assert.Equal(3, model.Items.Count);
        Assert.Equal("keep", model.SelectedItem?.History.ID);
    }

    [Fact]
    public void Refresh_ASelectedRowThatIsGone_LeavesNothingSelected()
    {
        TrayFlyoutItem gone = Text("gone");
        ListRecentItemsSource source = new(gone, Text("stays"));
        RecentItemsViewModel model = Model(source);
        model.SelectedItem = gone;

        source.Items.Remove(gone);
        model.Refresh();

        Assert.Null(model.SelectedItem);
    }

    [Fact]
    public void AFileRow_IsTheSameRowWhateverTheCaseOfItsPath()
        => Assert.Equal(
            RecentItemsViewModel.KeyOf(File(@"C:\Docs\Report.TXT")),
            RecentItemsViewModel.KeyOf(File(@"c:\docs\report.txt")));

    [Fact]
    public void Open_AsksTheWindowToOpenTheRowGivenTheSelectedRowOrTheFirstOne()
    {
        TrayFlyoutItem first = Text("first", 1);
        TrayFlyoutItem second = Text("second", 2);
        RecentItemsViewModel model = Model(new ListRecentItemsSource(first, second));
        List<RecentItemRequest> requests = [];
        model.OpenRequested += (_, request) => requests.Add(request);

        model.OpenCommand.Execute(second);
        model.OpenCommand.Execute(null);
        model.SelectedItem = second;
        model.OpenCommand.Execute(null);
        model.OpenInNewWindowCommand.Execute(first);

        Assert.Equal(
            [(second, false), (first, false), (second, false), (first, true)],
            requests.Select(request => (request.Item, request.InNewWindow)));
    }

    [Fact]
    public void Open_WithNothingToOpen_AsksForNothing()
    {
        RecentItemsViewModel model = Model(new ListRecentItemsSource());
        int requests = 0;
        model.OpenRequested += (_, _) => requests++;

        model.OpenCommand.Execute(null);
        model.OpenInNewWindowCommand.Execute(null);

        Assert.Equal(0, requests);
    }

    [Fact]
    public void ClearQuery_ShowsEverythingAgain()
    {
        RecentItemsViewModel model = Model(new ListRecentItemsSource(Text("alpha"), Text("beta")));
        model.Query = "alpha";
        Assert.Single(model.Items);

        model.ClearQueryCommand.Execute(null);

        Assert.Equal(2, model.Items.Count);
    }

    [Fact]
    public void Release_ForgetsEveryItem()
    {
        RecentItemsViewModel model = Model(new ListRecentItemsSource(Text("alpha")));
        model.SelectedItem = model.Items[0];

        model.Release();

        Assert.Empty(model.Items);
        Assert.Null(model.SelectedItem);
        Assert.True(model.IsEmpty);
    }

    [Fact]
    public void Query_RaisesPropertyChanged()
    {
        RecentItemsViewModel model = Model(new ListRecentItemsSource(Text("alpha")));
        List<string?> raised = [];
        model.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        model.Query = "a";

        Assert.Contains(nameof(RecentItemsViewModel.Query), raised);
        Assert.Contains(nameof(RecentItemsViewModel.IsEmpty), raised);
    }

    // ---- Where an item opens ----

    private static readonly HistoryInfo Plain = new() { TextContent = "x" };
    private static readonly HistoryInfo PdfInAGrabFrame = new() { SourcePath = @"C:\docs\a.pdf", ImagePath = "a.png", SourceContentKind = OpenContentKind.PdfDocument };

    [Theory]
    [InlineData(true, false, RecentOpenTarget.ThisWindow)]
    [InlineData(true, true, RecentOpenTarget.NewEditTextWindow)]
    [InlineData(false, false, RecentOpenTarget.NewEditTextWindow)]
    [InlineData(false, true, RecentOpenTarget.NewEditTextWindow)]
    public void AnEditTextItem_OpensHereOnlyInABlankWindow(bool windowIsBlank, bool inNewWindow, RecentOpenTarget expected)
        => Assert.Equal(expected, RecentItemOpener.Decide(TrayFlyoutItemKind.EditText, Plain, fileExists: false, inNewWindow, windowIsBlank));

    [Theory]
    [InlineData(true, false, RecentOpenTarget.ThisWindow)]
    [InlineData(false, false, RecentOpenTarget.NewEditTextWindow)]
    [InlineData(true, true, RecentOpenTarget.NewEditTextWindow)]
    public void ATextFile_OpensLikeAnEditTextItem(bool windowIsBlank, bool inNewWindow, RecentOpenTarget expected)
        => Assert.Equal(expected, RecentItemOpener.Decide(TrayFlyoutItemKind.File, Plain, fileExists: true, inNewWindow, windowIsBlank));

    [Fact]
    public void AMissingFile_OpensNothingWhateverTheWindowIs()
        => Assert.Equal(RecentOpenTarget.MissingFile, RecentItemOpener.Decide(TrayFlyoutItemKind.File, Plain, fileExists: false, inNewWindow: false, windowIsBlank: true));

    [Fact]
    public void APdfThatWasOpenedInAGrabFrame_OpensThereAgain()
    {
        Assert.True(RecentItemOpener.IsGrabFramePdf(PdfInAGrabFrame));
        Assert.Equal(RecentOpenTarget.GrabFrame, RecentItemOpener.Decide(TrayFlyoutItemKind.File, PdfInAGrabFrame, fileExists: true, inNewWindow: false, windowIsBlank: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AGrab_AlwaysOpensInAGrabFrame(bool windowIsBlank)
        => Assert.Equal(RecentOpenTarget.GrabFrame, RecentItemOpener.Decide(TrayFlyoutItemKind.GrabFrame, Plain, fileExists: false, inNewWindow: false, windowIsBlank));
}

/// <summary>The recent items pane in an Edit Text Window, and the Explorer layout that has it (v5 E5 <c>recent</c>).</summary>
public class RecentPaneTests
{
    private static EtwLayoutDefinition Explorer => BuiltInLayouts.TryGet(LayoutIds.Explorer, out EtwLayoutDefinition layout)
        ? layout
        : throw new InvalidOperationException("Explorer is not built in.");

    private static RecentPane PaneIn(EditTextWindow window)
        => Assert.Single(VisualDescendants<RecentPane>(PaneHost(window)!));

    private static ListBox ItemsListOf(RecentPane pane)
        => Assert.Single(VisualDescendants<ListBox>(pane));

    // ---- The pane and its factory ----

    [Fact]
    public void PaneIds_ArePinned()
    {
        Assert.Equal("textgrab.pane.recent", PaneIds.Recent);
        Assert.Equal(PaneIds.Recent, RecentPane.PaneId);

        string[] ids = [.. AppServices.Provider.GetServices<IEditorPaneFactory>().Select(factory => factory.Descriptor.Id).Order(StringComparer.Ordinal)];

        Assert.Equal(["textgrab.pane.calc", "textgrab.pane.recent"], ids);
    }

    [WpfFact]
    public void Factory_IsRegisteredAndMakesUnattachedRecentPanes()
    {
        IEditorPaneFactory factory = Assert.Single(
            AppServices.Provider.GetServices<IEditorPaneFactory>(),
            candidate => candidate.Descriptor.Id == RecentPane.PaneId);
        int attached = RecentPane.AttachedCount;

        IEditorPane pane = factory.Create();

        RecentPane recent = Assert.IsType<RecentPane>(pane);
        Assert.Same(recent, pane.View);
        Assert.Equal(RecentPane.Definition, pane.Descriptor);
        Assert.Equal(("Recent", PaneDock.Left), (pane.Descriptor.Title, pane.Descriptor.DefaultDock));
        Assert.True(Enum.IsDefined(typeof(Wpf.Ui.Controls.SymbolRegular), pane.Descriptor.IconKey), pane.Descriptor.IconKey);
        Assert.Null(pane.SaveConfig());
        Assert.Equal(attached, RecentPane.AttachedCount);
        Assert.NotSame(pane, factory.Create());
        Assert.Empty(pane.GetPaletteCommands());
    }

    [Fact]
    public void ResolvingTheFactories_MakesNoPane()
    {
        int created = RecentPane.CreatedCount;

        _ = AppServices.Provider.GetServices<IEditorPaneFactory>().Select(factory => factory.Descriptor).ToList();

        Assert.Equal(created, RecentPane.CreatedCount);
    }

    [WpfFact]
    public void Attach_LoadsTheItemsOnce_AndTwiceIsAnError()
    {
        FakeRecentItemsSource.Items = [RecentItemsViewModelTests.Text("one")];
        ListRecentItemsSource source = new(RecentItemsViewModelTests.Text("one"));
        EditTextWindow window = CreateWindow("x");
        RecentPane pane = new(source);

        pane.Attach(window.Session, config: null);

        Assert.Equal(1, source.LoadCount);
        Assert.Single(pane.ViewModel.Items);
        Assert.Throws<InvalidOperationException>(() => pane.Attach(window.Session, config: null));
        pane.Detach();
    }

    [WpfFact]
    public void Detach_ReleasesTheItemsAndTheCountAndMayBeCalledAgain()
    {
        EditTextWindow window = CreateWindow("x");
        RecentPane pane = new(new ListRecentItemsSource(RecentItemsViewModelTests.Text("one")));
        int attached = RecentPane.AttachedCount;
        pane.Attach(window.Session, config: null);
        Assert.Equal(attached + 1, RecentPane.AttachedCount);

        pane.Detach();
        pane.Detach();

        Assert.Equal(attached, RecentPane.AttachedCount);
        Assert.Empty(pane.ViewModel.Items);
    }

    [WpfFact]
    public void WindowActivated_AHiddenPaneOnlyNotesThatItIsStaleAndReadsWhenAskedAgain()
    {
        EditTextWindow window = CreateWindow("x");
        ListRecentItemsSource source = new(RecentItemsViewModelTests.Text("one"));
        RecentPane pane = new(source);
        pane.Attach(window.Session, config: null);
        Assert.False(pane.IsVisible);

        ((IEditorPane)pane).OnWindowActivated();

        Assert.True(pane.IsStale);
        Assert.Equal(1, source.LoadCount);

        source.Items.Add(RecentItemsViewModelTests.Text("two"));
        pane.Refresh();

        Assert.False(pane.IsStale);
        Assert.Equal(2, pane.ViewModel.Items.Count);
        pane.Detach();
    }

    [WpfFact]
    public void PaletteCommands_ExistOnlyWhileAttached()
    {
        EditTextWindow window = CreateWindow("x");
        RecentPane pane = new(new ListRecentItemsSource());
        pane.Attach(window.Session, config: null);

        Assert.Equal(["Search recent items", "Refresh recent items"], pane.GetPaletteCommands().Select(command => command.Name));

        pane.Detach();
        Assert.Empty(pane.GetPaletteCommands());
    }

    [WpfFact]
    public void TheViewHasTheAutomationIdsTheUiTestsUse()
    {
        EditTextWindow window = CreateWindow("x");
        RecentPane pane = new(new ListRecentItemsSource(RecentItemsViewModelTests.Text("alpha")));
        pane.Attach(window.Session, config: null);
        pane.Measure(new Size(260, 400));
        pane.Arrange(new Rect(0, 0, 260, 400));
        pane.UpdateLayout();

        string[] ids = [.. VisualDescendants<FrameworkElement>(pane).Select(System.Windows.Automation.AutomationProperties.GetAutomationId).Where(id => id.StartsWith("EditText.Recent", StringComparison.Ordinal))];

        Assert.Contains("EditText.Recent.Search", ids);
        Assert.Contains("EditText.Recent.Items", ids);
        Assert.Contains("EditText.Recent.Item", ids);
        pane.Detach();
    }

    // ---- Which windows make it ----

    [WpfFact]
    public void Classic_NeverMakesTheRecentPaneOrReadsHistory()
    {
        using IDisposable _ = UsePaneLayout();
        int created = RecentPane.CreatedCount;
        int attached = RecentPane.AttachedCount;

        EditTextWindow window = CreateWindow("1 + 1");

        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Equal(created, RecentPane.CreatedCount);
        Assert.Equal(attached, RecentPane.AttachedCount);
        Assert.Empty(VisualDescendants<RecentPane>(PaneHost(window)!));
    }

    [WpfFact]
    public void TheHandWrittenWindow_NeverMakesTheRecentPane()
    {
        using IDisposable _ = UsePaneLayout(on: false);
        int created = RecentPane.CreatedCount;

        EditTextWindow window = CreateWindow("1 + 1");

        Assert.Equal(created, RecentPane.CreatedCount);
        Assert.False(window.UsesPaneLayout);
    }

    [WpfFact]
    public void Explorer_MakesAndAttachesTheRecentPaneLeftOfTheEditor_AndClassicTakesItAway()
    {
        using IDisposable _ = UsePaneLayout();
        FakeRecentItemsSource.Items = [RecentItemsViewModelTests.Text("from history"), RecentItemsViewModelTests.File(@"C:\docs\notes.txt", 5)];
        EditTextWindow window = CreateWindow("1 + 1");
        int created = RecentPane.CreatedCount;
        int attached = RecentPane.AttachedCount;

        Assert.Null(window.ApplyLayout(Explorer));
        LayOut(window);

        Assert.Equal(LayoutIds.Explorer, window.LayoutId);
        Assert.Equal(created + 1, RecentPane.CreatedCount);
        Assert.Equal(attached + 1, RecentPane.AttachedCount);
        RecentPane pane = PaneIn(window);
        Assert.Equal(["from history", "notes.txt"], pane.ViewModel.Items.Select(item => item.Title));
        Assert.Equal(2, ItemsListOf(pane).Items.Count);
        Assert.True(pane.TranslatePoint(new Point(0, 0), RawTextBox(window)).X < 0, "The recent pane should be left of the editor.");

        Assert.Null(window.ApplyLayout(BuiltInLayouts.Classic));
        LayOut(window);

        Assert.Equal(attached, RecentPane.AttachedCount);
        Assert.Empty(pane.ViewModel.Items);
        Assert.Empty(VisualDescendants<RecentPane>(PaneHost(window)!));
    }

    [WpfFact]
    public void Explorer_KeepsTheCalcPaneClassicAlreadyMade()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");
        CalcPane calc = (CalcPane)window.FindName("CalcPaneControl");

        Assert.Null(window.ApplyLayout(Explorer));
        LayOut(window);

        Assert.Same(calc, window.FindName("CalcPaneControl"));
    }

    [WpfFact]
    public void Explorer_CarriesTheLayoutIdIntoTheHistoryItem()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow("1 + 1");

        Assert.Null(window.ApplyLayout(Explorer));

        Assert.Equal(LayoutIds.Explorer, window.AsHistoryItem().LayoutId);
    }

    [WpfFact]
    public void ARecentPaneInALayoutThePaneLayoutIsOffFor_IsRefusedWithAReason()
    {
        using IDisposable _ = UsePaneLayout(on: false);
        EditTextWindow window = CreateWindow("1 + 1");

        Assert.Equal("The pane layout is off.", window.ApplyLayout(Explorer));
    }

    // ---- Opening ----

    [WpfFact]
    public async Task OpeningAnEditTextItem_InABlankWindow_ShowsItHere()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow window = CreateWindow(string.Empty);
        TrayFlyoutItem item = new(TrayFlyoutItemKind.EditText, new HistoryInfo
        {
            ID = "history-1",
            TextContent = "def f():\n    return 1",
            EditorMode = EtwEditorMode.Code,
            CodeLanguage = CodeLanguages.Python,
            CaptureDateTime = DateTimeOffset.Now,
        });

        Assert.True(window.IsBlankForRecentItems);
        await window.OpenRecentItemAsync(item, inNewWindow: false);

        Assert.Equal("def f():\n    return 1", window.Session.Text);
        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal(CodeLanguages.Python, window.CodeLanguageName);
        Assert.False(window.IsBlankForRecentItems);
    }

    [WpfFact]
    public void AWindowWithText_IsNotBlank()
    {
        EditTextWindow window = CreateWindow("something");

        Assert.False(window.IsBlankForRecentItems);
    }

    [WpfFact]
    public void ARequestFromThePane_ReachesTheWindow()
    {
        using IDisposable _ = UsePaneLayout();
        TrayFlyoutItem item = RecentItemsViewModelTests.Text("from the pane", id: "pane-item");
        FakeRecentItemsSource.Items = [item];
        EditTextWindow window = CreateWindow(string.Empty);
        Assert.Null(window.ApplyLayout(Explorer));
        LayOut(window);
        RecentPane pane = PaneIn(window);

        pane.ViewModel.OpenCommand.Execute(item);

        // The window opens a blank window's item right away on the UI thread.
        DoEvents();
        Assert.Equal("from the pane", window.Session.Text);
    }

    // ---- Actions ----

    [Fact]
    public void TheActionIds_ArePinnedAndRegisteredInTheAppsRegistry()
    {
        IActionRegistry registry = AppServices.Provider.GetRequiredService<IActionRegistry>();

        Assert.Equal(
            ["textgrab.recent.open", "textgrab.recent.openInNewWindow", "textgrab.recent.refresh", "textgrab.recent.search"],
            registry.All.Select(descriptor => descriptor.Id).Where(id => id.StartsWith("textgrab.recent.", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.All(
            registry.All.Where(descriptor => descriptor.Id.StartsWith("textgrab.recent.", StringComparison.Ordinal)),
            descriptor => Assert.Equal(ActionKind.AppCommand, descriptor.Kind));
    }
}
