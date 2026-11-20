using System.IO;
using Text_Grab;
using Text_Grab.Controls;
using Text_Grab.Core.Editing;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using Text_Grab.Utilities;
using static Tests.CalcPaneTests;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// Opening Mermaid files in the Mermaid layout (v5 E5 <c>mermaid</c>): the built-in file-type map, the
/// layout's <c>DefaultMode</c> meeting the mode a file would open in, and the window rendering the layout it
/// ends in first, so that Classic's calc pane is never made for a window that does not keep it.
/// </summary>
/// <remarks>
/// Opening a file records it in the recent files, so these run in the history service's collection with the
/// profile pointed at a temporary folder. Windows are made but never shown, so <c>Window_Loaded</c> never
/// runs: tests that need its mode switch call <see cref="EditTextWindowTestHost.SwitchMode"/> as it does.
/// </remarks>
[Collection("History service")]
public sealed class EditTextWindowMermaidLayoutTests : IDisposable
{
    private const string Diagram = "flowchart LR\r\nA[Start] --> B[End]\r\n";

    private readonly string root = Path.Combine(Path.GetTempPath(), $"text-grab-mermaid-open-{Guid.NewGuid():N}");
    private readonly IDisposable profileScope;

    public EditTextWindowMermaidLayoutTests()
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

    private static EditTextWindow WindowFor(string path)
    {
        EditTextWindow window = new(new LayoutRequest(FilePath: path));
        LayOut(window);
        return window;
    }

    private static string? PendingLayoutNotice(EditTextWindow window)
        => (string?)typeof(EditTextWindow)
            .GetField("pendingLayoutNotice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(window);

    // ---- A new window for a Mermaid file builds the Mermaid layout directly ----

    [WpfTheory]
    [InlineData("flow.mmd")]
    [InlineData("flow.mermaid")]
    [InlineData("FLOW.MMD")]
    public async Task AMermaidFileInANewWindow_OpensTheMermaidLayoutInCodeModeWithoutEverMakingTheCalcPane(string name)
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile(name, Diagram);
        int calcCreated = CalcPane.CreatedCount;
        int mermaidCreated = MermaidPane.CreatedCount;

        EditTextWindow window = WindowFor(path);

        // Rendered before any file is read: no Classic first.
        Assert.Equal(LayoutIds.Mermaid, window.LayoutId);
        Assert.Equal(calcCreated, CalcPane.CreatedCount);
        Assert.Equal(mermaidCreated + 1, MermaidPane.CreatedCount);

        await window.OpenPath(path);
        LayOut(window);

        Assert.Equal(LayoutIds.Mermaid, window.LayoutId);
        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Null(window.CodeLanguageName);
        Assert.Equal(Diagram, window.Session.Text);
        Assert.Equal(Diagram, CodeEditor(window).Text);
        Assert.Equal(calcCreated, CalcPane.CreatedCount);
        Assert.Equal(mermaidCreated + 1, MermaidPane.CreatedCount);
        Assert.Null(window.FindName("CalcPaneControl"));
        MermaidPane pane = Assert.Single(VisualDescendants<MermaidPane>(PaneHost(window)!));
        Assert.Equal(Diagram, pane.ViewModel.WantedSource);
        Assert.Empty(VisualDescendants<CalcPane>(PaneHost(window)!));
    }

    [WpfFact]
    public async Task TheLanguageStaysPlainTextWhenTheWindowIsLoadedAfterTheFile()
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile("flow.mmd", Diagram);
        EditTextWindow window = WindowFor(path);
        await window.OpenPath(path);

        // What Window_Loaded does for a window that already holds a file in Code mode.
        SwitchMode(window, EtwEditorMode.Code);

        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Null(window.CodeLanguageName);
        Assert.Equal(Diagram, CodeEditor(window).Text);
    }

    [WpfFact]
    public async Task AMermaidFileOpenedInAWindowAlreadyShowingClassic_SwitchesToTheMermaidLayoutAndCodeMode()
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile("flow.mmd", Diagram);
        EditTextWindow window = CreateWindow(string.Empty);
        Assert.Equal(LayoutIds.Classic, window.LayoutId);

        await window.OpenPath(path);
        LayOut(window);

        Assert.Equal(LayoutIds.Mermaid, window.LayoutId);
        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Null(window.CodeLanguageName);
        Assert.Null(window.FindName("CalcPaneControl"));
        Assert.Single(VisualDescendants<MermaidPane>(PaneHost(window)!));
    }

    [WpfFact]
    public async Task AnMmdFileOpenedWhileTheWindowShowsAnotherLayout_KeepsThatLayout()
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile("flow.mmd", Diagram);
        EditTextWindow window = new(new LayoutRequest(RequestedId: LayoutIds.Explorer));
        LayOut(window);

        await window.OpenPath(path);

        Assert.Equal(LayoutIds.Explorer, window.LayoutId);
        Assert.Equal(EtwEditorMode.Text, window.Session.Mode);
    }

    // ---- Every other file stays as it was ----

    [WpfTheory]
    [InlineData("notes.txt", EtwEditorMode.Text)]
    [InlineData("readme.md", EtwEditorMode.Markdown)]
    [InlineData("Greeter.cs", EtwEditorMode.Code)]
    [InlineData("data.csv", EtwEditorMode.Spreadsheet)]
    public async Task OtherFiles_OpenInClassicAndTheModeTheirTypeImpliesAndNeverMakeTheMermaidPane(string name, EtwEditorMode expectedMode)
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile(name, "a,b\r\n1,2\r\n");
        int mermaidCreated = MermaidPane.CreatedCount;

        EditTextWindow window = WindowFor(path);
        await window.OpenPath(path);

        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Equal(expectedMode, window.Session.Mode);
        Assert.Equal(mermaidCreated, MermaidPane.CreatedCount);
        Assert.NotNull(window.FindName("CalcPaneControl"));
        Assert.Empty(VisualDescendants<MermaidPane>(PaneHost(window)!));
    }

    [WpfFact]
    public async Task AMarkdownFile_StillOpensInMarkdownModeInClassic_AndSoDoesAnExistingWindow()
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile("readme.md", "# Title\r\n");
        EditTextWindow window = CreateWindow(string.Empty);

        await window.OpenPath(path);

        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Equal(EtwEditorMode.Markdown, window.Session.Mode);
    }

    [WpfFact]
    public async Task WithThePaneLayoutOff_AnMmdFileOpensAsItAlwaysDid()
    {
        using IDisposable _ = UsePaneLayout(on: false);
        string path = WriteFile("flow.mmd", Diagram);
        int mermaidCreated = MermaidPane.CreatedCount;
        EditTextWindow window = new(new LayoutRequest(FilePath: path));
        LayOut(window);

        await window.OpenPath(path);

        Assert.False(window.UsesPaneLayout);
        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Equal(EtwEditorMode.Text, window.Session.Mode);
        Assert.Equal(mermaidCreated, MermaidPane.CreatedCount);
        Assert.Null(PendingLayoutNotice(window));
    }

    [WpfFact]
    public async Task ARequestedLayout_BeatsTheFileTypeAndAnItemsLayoutBeatsItToo()
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile("flow.mmd", Diagram);

        EditTextWindow classic = new(new LayoutRequest(RequestedId: LayoutIds.Classic, FilePath: path));
        LayOut(classic);
        await classic.OpenPath(path);

        Assert.Equal(LayoutIds.Classic, classic.LayoutId);
        Assert.Equal(EtwEditorMode.Text, classic.Session.Mode);

        EditTextWindow explorer = new(new HistoryInfo { TextContent = Diagram, LayoutId = LayoutIds.Explorer });
        LayOut(explorer);
        await explorer.OpenPath(path);

        Assert.Equal(LayoutIds.Explorer, explorer.LayoutId);
    }

    // ---- A layout asked for by name ----

    [WpfFact]
    public void LayoutMermaid_OnABlankWindow_OpensCodeModeAndBuildsNoCalcPane()
    {
        using IDisposable _ = UsePaneLayout();
        int calcCreated = CalcPane.CreatedCount;

        EditTextWindow window = new(new LayoutRequest(RequestedId: LayoutIds.Mermaid));
        LayOut(window);

        Assert.Equal(LayoutIds.Mermaid, window.LayoutId);
        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Null(window.CodeLanguageName);
        Assert.Equal(calcCreated, CalcPane.CreatedCount);
        Assert.Single(VisualDescendants<MermaidPane>(PaneHost(window)!));
        Assert.Null(PendingLayoutNotice(window));
    }

    [WpfFact]
    public void LayoutMermaid_WithTextGivenToTheWindow_OpensCodeMode()
    {
        using IDisposable _ = UsePaneLayout();
        int calcCreated = CalcPane.CreatedCount;

        EditTextWindow window = new("flowchart LR\nA --> B", isEncoded: false, layoutId: LayoutIds.Mermaid);
        LayOut(window);

        Assert.Equal(LayoutIds.Mermaid, window.LayoutId);
        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal("flowchart LR\nA --> B", window.Session.Text);
        Assert.Equal(calcCreated, CalcPane.CreatedCount);
    }

    [WpfFact]
    public async Task LayoutMermaid_AskedForAfterTheWindowIsMade_AlsoSetsCodeModeOnABlankWindow_ButNeverReplacesAChosenMode()
    {
        using IDisposable _ = UsePaneLayout();
        EditTextWindow blank = CreateWindow(string.Empty);
        await blank.RequestLayoutAsync(LayoutIds.Mermaid);

        Assert.Equal(LayoutIds.Mermaid, blank.LayoutId);
        Assert.Equal(EtwEditorMode.Code, blank.Session.Mode);

        EditTextWindow spreadsheet = CreateWindow("a\tb");
        SwitchMode(spreadsheet, EtwEditorMode.Spreadsheet);
        await spreadsheet.RequestLayoutAsync(LayoutIds.Mermaid);

        Assert.Equal(LayoutIds.Mermaid, spreadsheet.LayoutId);
        Assert.Equal(EtwEditorMode.Spreadsheet, spreadsheet.Session.Mode);
    }

    [WpfFact]
    public void LayoutMermaid_WithThePaneLayoutOff_SaysWhyAndChangesNothing()
    {
        using IDisposable _ = UsePaneLayout(on: false);
        int mermaidCreated = MermaidPane.CreatedCount;

        EditTextWindow window = new(new LayoutRequest(RequestedId: LayoutIds.Mermaid));

        Assert.Contains("pane layout", PendingLayoutNotice(window));
        Assert.Equal(EtwEditorMode.Text, window.Session.Mode);
        Assert.Equal(mermaidCreated, MermaidPane.CreatedCount);
    }

    [WpfFact]
    public void AnUnknownLayout_OpensClassicWithItsCalcPaneAndSaysWhy()
    {
        using IDisposable _ = UsePaneLayout();

        EditTextWindow window = new(new LayoutRequest(RequestedId: "no-such-layout"));
        LayOut(window);

        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Contains("no-such-layout", PendingLayoutNotice(window));
        Assert.NotNull(window.FindName("CalcPaneControl"));
    }

    // ---- History ----

    [WpfFact]
    public void AHistoryItemInTheMermaidLayout_KeepsTheModeItWasSavedWith()
    {
        using IDisposable _ = UsePaneLayout();
        int calcCreated = CalcPane.CreatedCount;

        EditTextWindow window = new(new HistoryInfo { TextContent = Diagram, EditorMode = EtwEditorMode.Text, LayoutId = LayoutIds.Mermaid });
        LayOut(window);

        Assert.Equal(LayoutIds.Mermaid, window.LayoutId);
        Assert.Equal(EtwEditorMode.Text, window.Session.Mode);
        Assert.Equal(calcCreated, CalcPane.CreatedCount);
        Assert.Equal(LayoutIds.Mermaid, window.AsHistoryItem().LayoutId);
    }

    [WpfFact]
    public void AHistoryItemInTheMermaidLayout_KeepsItsCodeLanguage()
    {
        using IDisposable _ = UsePaneLayout();

        EditTextWindow window = new(new HistoryInfo { TextContent = Diagram, EditorMode = EtwEditorMode.Code, CodeLanguage = CodeLanguages.Json, LayoutId = LayoutIds.Mermaid });
        LayOut(window);

        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal(CodeLanguages.Json, window.CodeLanguageName);
    }

    // ---- The layout's DefaultMode and DefaultCodeLanguage against a file's own mode ----

    private static EtwLayoutDefinition CodeLayout(string? language)
        => BuiltInLayouts.Classic with { Id = "code-defaults", Name = "Code defaults", DefaultMode = EtwEditorMode.Code, DefaultCodeLanguage = language };

    [WpfTheory]
    [InlineData("notes.txt", "MarkDown", EtwEditorMode.Code, "MarkDown")]
    [InlineData("readme.md", "MarkDown", EtwEditorMode.Code, "MarkDown")]
    [InlineData("readme.md", null, EtwEditorMode.Code, "MarkDown")]
    [InlineData("Greeter.cs", "MarkDown", EtwEditorMode.Code, "C#")]
    [InlineData("data.csv", "MarkDown", EtwEditorMode.Spreadsheet, null)]
    [InlineData("notes.txt", null, EtwEditorMode.Code, null)]
    public async Task ALayoutsDefaultMode_ReplacesATextOrMarkdownFilesModeButNotASpreadsheetOrASourceFilesLanguage(string name, string? layoutLanguage, EtwEditorMode expectedMode, string? expectedLanguage)
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile(name, "a,b\r\n1,2\r\n");
        EditTextWindow window = CreateWindow(string.Empty);
        Assert.Null(window.ApplyLayout(CodeLayout(layoutLanguage)));

        await window.OpenPath(path);

        Assert.Equal(expectedMode, window.Session.Mode);
        if (expectedMode == EtwEditorMode.Code)
            Assert.Equal(expectedLanguage, window.CodeLanguageName);
    }

    [WpfFact]
    public async Task AnUnknownDefaultLanguage_FallsBackToDetectingFromTheText()
    {
        using IDisposable _ = UsePaneLayout();
        const string json = "{\r\n  \"name\": \"demo\",\r\n  \"items\": [1, 2, 3]\r\n}\r\n";
        string path = WriteFile("notes.txt", json);
        EditTextWindow window = CreateWindow(string.Empty);
        Assert.Null(window.ApplyLayout(CodeLayout("Klingon")));

        await window.OpenPath(path);

        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal(CodeLanguageDetector.Detect(json), window.CodeLanguageName);
    }

    [WpfFact]
    public async Task MultipleFilesStayInTextMode_WhateverTheLayoutsDefaultIs()
    {
        using IDisposable _ = UsePaneLayout();
        string path = WriteFile("notes.txt", "one");
        EditTextWindow window = CreateWindow(string.Empty);
        Assert.Null(window.ApplyLayout(CodeLayout(null)));
        LayOut(window);

        await window.OpenPath(path, isMultipleFiles: true);

        Assert.Equal(EtwEditorMode.Text, window.Session.Mode);
    }
}
