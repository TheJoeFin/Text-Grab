using System.IO;
using System.Reflection;
using System.Windows.Controls;
using Text_Grab;
using Text_Grab.Core.Editing;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using static Tests.CalcPaneCharacterizationTests;
using static Tests.EditTextWindowTestHost;
using RoutedEventArgs = System.Windows.RoutedEventArgs;

namespace Tests;

[CollectionDefinition("Layout store", DisableParallelization = true)]
public class LayoutStoreCollectionDefinition
{
}

/// <summary>
/// The layout picker (v5 E6 <c>picker</c>): switching layouts, the Panes menu, saving, resetting, deleting
/// and the default layout, against a layout store in a folder of its own.
/// </summary>
/// <remarks>
/// Windows are made but never shown. Nothing here calls a handler that writes <c>DefaultSettings</c> or
/// history: the calc pane is toggled through the non-persisting seam, and the store is a temporary folder.
/// </remarks>
[Collection("Layout store")]
public sealed class EditTextWindowLayoutPickerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "TextGrabPickerTests", Guid.NewGuid().ToString("N"));
    private readonly FileLayoutStore store;
    private readonly ILayoutStore? previousStore = EditTextWindow.LayoutStoreOverride;
    private readonly IDisposable paneLayout = UsePaneLayout();

    public EditTextWindowLayoutPickerTests()
    {
        store = new FileLayoutStore(directory);
        EditTextWindow.LayoutStoreOverride = store;
        EditTextWindow.DeleteLayoutConfirmOverride = (_, _) => Task.FromResult(true);
    }

    public void Dispose()
    {
        EditTextWindow.DeleteLayoutConfirmOverride = null;
        EditTextWindow.LayoutStoreOverride = previousStore;
        paneLayout.Dispose();
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private static T Field<T>(EditTextWindow window, string name)
        => (T)typeof(EditTextWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;

    private static string? PendingNotice(EditTextWindow window)
        => (string?)typeof(EditTextWindow).GetField("pendingLayoutNotice", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window);

    private static string[] PaneIdsOf(EditTextWindow window) => [.. EtwLayoutValidator.PaneIdsIn(window.CurrentLayout!.Root)];

    // ---- The round trip the phase file asks for ----

    [WpfFact]
    public async Task Switching_ClassicToExplorerAndBack_InTextMode_KeepsTextSelectionDirtyFlagAndUndo()
    {
        string path = Path.Combine(directory, "notes.txt");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "hello");
        EditTextWindow window = CreateWindow(string.Empty);
        await window.OpenPath(path);
        LayOut(window);
        TextBox box = RawTextBox(window);
        box.AppendText(" world");
        DoEvents();
        LayOut(window);
        box.Select(2, 3);
        bool dirty = Field<bool>(window, "hasPendingFileEdits");
        TextSpan selection = window.Session.Selection;
        Assert.True(box.CanUndo);

        Assert.Null(await window.SwitchLayoutAsync(LayoutIds.Explorer));
        LayOut(window);
        Assert.Equal(LayoutIds.Explorer, window.LayoutId);
        Assert.Null(await window.SwitchLayoutAsync(LayoutIds.Classic));
        LayOut(window);

        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Equal("hello world", box.Text);
        Assert.Equal((2, 3), (box.SelectionStart, box.SelectionLength));
        Assert.Equal(selection, window.Session.Selection);
        Assert.Equal(dirty, Field<bool>(window, "hasPendingFileEdits"));
        Assert.Equal(EtwEditorMode.Text, window.Session.Mode);
        Assert.True(box.CanUndo);
        box.Undo();
        Assert.Equal("hello", box.Text);
    }

    [WpfFact]
    public async Task Switching_ClassicToMermaidAndBack_InCodeMode_KeepsTextSelectionLanguageAndUndo()
    {
        EditTextWindow window = CreateWindow("let a = 1;\nlet b = 2;\n");
        SwitchMode(window, EtwEditorMode.Code);
        window.SetCodeLanguage(CodeLanguages.CSharp);
        Text_Grab.Controls.CodeEditor editor = CodeEditor(window);
        editor.Document.Insert(editor.Document.TextLength, "let c = 3;\n");
        DoEvents();
        LayOut(window);
        window.Session.Selection = new TextSpan(4, 1);
        string text = window.Session.Text;
        TextSpan selection = window.Session.Selection;
        Assert.True(editor.Document.UndoStack.CanUndo);

        Assert.Null(await window.SwitchLayoutAsync(LayoutIds.Mermaid));
        LayOut(window);
        Assert.Equal(LayoutIds.Mermaid, window.LayoutId);
        Assert.Null(await window.SwitchLayoutAsync(LayoutIds.Classic));
        LayOut(window);

        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal(text, window.Session.Text);
        Assert.Equal(text, editor.Document.Text);
        Assert.Equal(selection, window.Session.Selection);
        Assert.Equal(CodeLanguages.CSharp, window.CodeLanguageName);
        Assert.True(editor.Document.UndoStack.CanUndo);
        editor.Document.UndoStack.Undo();
        Assert.DoesNotContain("let c", editor.Document.Text);
    }

    // ---- The mode on a switch ----

    [WpfFact]
    public async Task ASwitchToACodeLayout_TakesTextModeToCodeWithPlainTextWhenTheWindowHasNoLanguage()
    {
        EditTextWindow window = CreateWindow("graph TD\nA-->B");

        Assert.Null(await window.SwitchLayoutAsync(LayoutIds.Mermaid));
        LayOut(window);

        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Null(window.CodeLanguageName);
    }

    [WpfFact]
    public async Task ASwitchToExplorer_ChangesNoMode()
    {
        EditTextWindow window = CreateWindow("one\ttwo\n1\t2");
        SwitchMode(window, EtwEditorMode.Spreadsheet);

        Assert.Null(await window.SwitchLayoutAsync(LayoutIds.Explorer));

        Assert.Equal(EtwEditorMode.Spreadsheet, window.Session.Mode);
    }

    [WpfFact]
    public async Task ASpreadsheetWithFormatting_StaysASpreadsheetButAPlainOneBecomesCode()
    {
        EditTextWindow formatted = CreateWindow("one\ttwo\n1\t2");
        SwitchMode(formatted, EtwEditorMode.Spreadsheet);
        Field<EditTextTableDocument>(formatted, "tableDocument").SetCellWrap(0, 0, true);

        EditTextWindow plain = CreateWindow("one\ttwo\n1\t2");
        SwitchMode(plain, EtwEditorMode.Spreadsheet);

        Assert.Null(await formatted.SwitchLayoutAsync(LayoutIds.Mermaid));
        Assert.Null(await plain.SwitchLayoutAsync(LayoutIds.Mermaid));

        Assert.Equal(EtwEditorMode.Spreadsheet, formatted.Session.Mode);
        Assert.Equal(EtwEditorMode.Code, plain.Session.Mode);
    }

    [WpfFact]
    public async Task AMarkdownWindow_StaysMarkdownOnASwitchToTheMarkdownPreviewLayout()
    {
        EditTextWindow window = CreateWindow("# Title\n\ntext");
        SwitchMode(window, EtwEditorMode.Markdown);

        Assert.Null(await window.SwitchLayoutAsync(LayoutIds.MarkdownPreview));

        Assert.Equal(EtwEditorMode.Markdown, window.Session.Mode);
    }

    // ---- A layout that cannot be shown ----

    [WpfFact]
    public async Task AnUnknownLayout_LeavesTheWindowAloneAndSaysWhy()
    {
        EditTextWindow window = CreateWindow("text");

        string? problem = await window.SwitchLayoutAsync("no-such-layout");

        Assert.Contains("no-such-layout", problem);
        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Contains("kept the classic layout", PendingNotice(window));
    }

    // ---- The Panes menu ----

    [WpfFact]
    public void APaneTheLayoutLacks_IsAddedAtItsDefaultSideAndRemovedAgain()
    {
        EditTextWindow window = CreateWindow("text");
        int created = RecentPane.CreatedCount;

        window.SetPaneShown(PaneIds.Recent, true);
        LayOut(window);

        Assert.Equal([PaneIds.Recent, PaneIds.Editor, PaneIds.Calc], PaneIdsOf(window));
        Assert.Equal(created + 1, RecentPane.CreatedCount);

        window.SetPaneShown(PaneIds.Recent, false);
        LayOut(window);

        Assert.Equal([PaneIds.Editor, PaneIds.Calc], PaneIdsOf(window));
    }

    [WpfFact]
    public async Task APaneInTheSavedLayout_IsCollapsedAndShownAgainNotRemoved()
    {
        EditTextWindow window = CreateWindow("text");
        Assert.Null(await window.SwitchLayoutAsync(LayoutIds.Explorer));
        LayOut(window);
        string[] before = PaneIdsOf(window);

        window.SetPaneShown(PaneIds.Recent, false);
        LayOut(window);

        Assert.Equal(before, PaneIdsOf(window));
        Assert.True(Collapsed(window, PaneIds.Recent));

        window.SetPaneShown(PaneIds.Recent, true);
        Assert.False(Collapsed(window, PaneIds.Recent));
    }

    private static bool Collapsed(EditTextWindow window, string paneId)
        => ((SplitNode)window.CurrentLayout!.Root).Children.Single(child => child.Node is PaneNode pane && pane.PaneId == paneId).Collapsed;

    [WpfFact]
    public void TheCalcPanesItem_AgreesWithTheCalcMenuItemAndTheLayout()
    {
        EditTextWindow window = CreateWindow("1 + 1");
        InitializeCalcPane(window);
        MenuItem calcItem = (MenuItem)window.FindName("ShowCalcPaneMenuItem");

        window.SetPaneShown(PaneIds.Calc, true);
        Assert.True(calcItem.IsChecked);
        Assert.False(Collapsed(window, PaneIds.Calc));

        // The Panes submenu reports what the layout says, as it opens.
        MenuItem panes = (MenuItem)window.FindName("PanesMenuItem");
        panes.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent, panes));
        MenuItem calcPaneItem = panes.Items.OfType<MenuItem>().Single(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == "EditText.Menu.Panes.calc");
        Assert.True(calcPaneItem.IsChecked);

        window.SetPaneShown(PaneIds.Calc, false);
        Assert.False(calcItem.IsChecked);
        Assert.True(Collapsed(window, PaneIds.Calc));
        panes.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent, panes));
        Assert.False(panes.Items.OfType<MenuItem>().Single(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == "EditText.Menu.Panes.calc").IsChecked);
    }

    [WpfFact]
    public async Task TheCalcPane_CanBeAddedToALayoutWithoutItAndLeavesAgain()
    {
        EditTextWindow window = CreateWindow("1 + 1");
        Assert.Null(await window.SwitchLayoutAsync(LayoutIds.Mermaid));
        LayOut(window);
        Assert.DoesNotContain(PaneIds.Calc, PaneIdsOf(window));
        Assert.False(((Button)window.FindName("CalcToggleButton")).IsEnabled);

        window.SetPaneShown(PaneIds.Calc, true);
        LayOut(window);

        Assert.Contains(PaneIds.Calc, PaneIdsOf(window));
        Assert.True(((Button)window.FindName("CalcToggleButton")).IsEnabled);
        Assert.False(Collapsed(window, PaneIds.Calc));

        window.SetPaneShown(PaneIds.Calc, false);
        LayOut(window);

        Assert.DoesNotContain(PaneIds.Calc, PaneIdsOf(window));
        Assert.False(((Button)window.FindName("CalcToggleButton")).IsEnabled);
    }

    [WpfFact]
    public void TheEditorPane_CannotBeToggled()
    {
        EditTextWindow window = CreateWindow("text");

        window.SetPaneShown(PaneIds.Editor, false);

        Assert.Contains(PaneIds.Editor, PaneIdsOf(window));
    }

    // ---- The menus ----

    [WpfFact]
    public async Task TheLayoutsMenu_ListsBuiltInAndSavedLayoutsWithTheCurrentOneChecked()
    {
        EditTextWindow window = CreateWindow("text");
        Assert.Null(await window.SaveCurrentLayoutAsync("My layout"));
        await window.SwitchLayoutAsync(LayoutIds.Explorer);

        MenuItem layouts = (MenuItem)window.FindName("LayoutsMenuItem");
        Assert.Equal(System.Windows.Visibility.Visible, layouts.Visibility);
        await InvokeLoadLayoutMenuItems(window);

        Dictionary<string, MenuItem> byId = layouts.Items.OfType<MenuItem>()
            .Where(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item).StartsWith("EditText.Menu.Layouts.Item.", StringComparison.Ordinal))
            .ToDictionary(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item)["EditText.Menu.Layouts.Item.".Length..]);
        Assert.Equal(["classic", "explorer", "markdown-preview", "mermaid", "my-layout"], byId.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["explorer"], byId.Where(pair => pair.Value.IsChecked).Select(pair => pair.Key));
        Assert.Equal("Explorer", byId["explorer"].Header);
    }

    private static Task InvokeLoadLayoutMenuItems(EditTextWindow window)
        => (Task)typeof(EditTextWindow).GetMethod("LoadLayoutMenuItemsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null)!;

    [WpfFact]
    public void WithThePaneLayoutOff_TheLayoutsAndPanesMenusAreNotInTheWindowMenu()
    {
        using IDisposable off = UsePaneLayout(on: false);

        EditTextWindow window = CreateWindow("text");

        Assert.Equal(System.Windows.Visibility.Collapsed, ((MenuItem)window.FindName("LayoutsMenuItem")).Visibility);
        Assert.Equal(System.Windows.Visibility.Collapsed, ((MenuItem)window.FindName("PanesMenuItem")).Visibility);
    }

    // ---- Save, reset, delete ----

    [WpfFact]
    public async Task SaveAs_WritesTheLiveLayoutToTheStoreAndTheWindowShowsIt()
    {
        EditTextWindow window = CreateWindow("text");
        window.SetPaneShown(PaneIds.Recent, true);

        Assert.Null(await window.SaveCurrentLayoutAsync("Wide Recent"));

        LayoutReadResult read = await store.LoadAsync("wide-recent", TestContext.Current.CancellationToken);
        Assert.True(read.Succeeded, read.Error);
        Assert.Equal("Wide Recent", read.Layout!.Name);
        Assert.Equal(LayoutIds.Classic, read.Layout.BasedOn);
        Assert.Equal([PaneIds.Recent, PaneIds.Editor, PaneIds.Calc], EtwLayoutValidator.PaneIdsIn(read.Layout.Root));
        Assert.Equal("wide-recent", window.LayoutId);
        Assert.Equal(["classic", "explorer", "markdown-preview", "mermaid", "wide-recent"],
            (await store.ListAsync(TestContext.Current.CancellationToken)).Select(summary => summary.Id).Order(StringComparer.Ordinal));
    }

    [WpfFact]
    public async Task SaveAs_NeverReplacesABuiltInOrAnEarlierLayoutAndKeepsTheOriginAcrossCopies()
    {
        EditTextWindow window = CreateWindow("text");

        Assert.Null(await window.SaveCurrentLayoutAsync("Classic"));
        Assert.Equal("classic-2", window.LayoutId);
        Assert.Null(await window.SaveCurrentLayoutAsync("Classic"));
        Assert.Equal("classic-3", window.LayoutId);

        // A copy of a copy still resets to the built-in.
        Assert.Equal(LayoutIds.Classic, (await store.LoadAsync("classic-3", TestContext.Current.CancellationToken)).Layout!.BasedOn);
        Assert.Equal(LayoutIds.Classic, window.ResetTargetId());
    }

    [WpfFact]
    public async Task SaveAs_WithABlankName_IsRefused()
    {
        EditTextWindow window = CreateWindow("text");

        Assert.NotNull(await window.SaveCurrentLayoutAsync("   "));
        Assert.False(Directory.Exists(directory));
    }

    [WpfFact]
    public async Task ResetToBuiltIn_IsOnlyOfferedWhenThereIsSomethingToReturnTo()
    {
        EditTextWindow window = CreateWindow("text");
        Assert.Null(window.ResetTargetId());
        Assert.NotNull(window.ResetLayout());

        // Panes toggled on a built-in layout: back to its definition.
        window.SetPaneShown(PaneIds.Recent, true);
        Assert.Equal(LayoutIds.Classic, window.ResetTargetId());
        Assert.Null(window.ResetLayout());
        LayOut(window);
        Assert.Equal([PaneIds.Editor, PaneIds.Calc], PaneIdsOf(window));
        Assert.Null(window.ResetTargetId());

        // A saved layout: back to the built-in it was copied from, and its file is left as it was.
        window.SetPaneShown(PaneIds.Recent, true);
        Assert.Null(await window.SaveCurrentLayoutAsync("Mine"));
        string before = await File.ReadAllTextAsync(Path.Combine(directory, "mine.json"), TestContext.Current.CancellationToken);
        Assert.Equal(LayoutIds.Classic, window.ResetTargetId());
        Assert.Null(window.ResetLayout());
        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Equal(before, await File.ReadAllTextAsync(Path.Combine(directory, "mine.json"), TestContext.Current.CancellationToken));
    }

    [WpfFact]
    public async Task ASavedLayoutWithNoOrigin_HasNothingToResetTo()
    {
        await store.SaveAsync(BuiltInLayouts.Classic with { Id = "plain", Name = "Plain" }, TestContext.Current.CancellationToken);
        EditTextWindow window = CreateWindow("text");
        Assert.Null(await window.SwitchLayoutAsync("plain"));

        Assert.Null(window.ResetTargetId());
    }

    [WpfFact]
    public async Task Delete_RemovesTheFileAndTheWindowKeepsItsLiveCopy()
    {
        EditTextWindow window = CreateWindow("text");
        Assert.Null(await window.SaveCurrentLayoutAsync("Doomed"));
        EditTextWindow other = CreateWindow("other");
        Assert.Null(await other.SwitchLayoutAsync("doomed"));

        Assert.Null(await window.DeleteCurrentLayoutAsync());

        Assert.False(File.Exists(Path.Combine(directory, "doomed.json")));
        Assert.Equal("doomed", window.LayoutId);
        Assert.Equal("doomed", other.LayoutId);
        await InvokeLoadLayoutMenuItems(window);
        MenuItem layouts = (MenuItem)window.FindName("LayoutsMenuItem");
        Assert.DoesNotContain(layouts.Items.OfType<MenuItem>(), item => item.IsChecked && item.Tag is not null);
        Assert.False(((MenuItem)window.FindName("DeleteLayoutMenuItem")).IsEnabled);
    }

    [WpfFact]
    public async Task Delete_IsRefusedForABuiltInLayoutAndDoesNothingWhenCancelled()
    {
        EditTextWindow window = CreateWindow("text");
        Assert.NotNull(await window.DeleteCurrentLayoutAsync());

        Assert.Null(await window.SaveCurrentLayoutAsync("Keeper"));
        EditTextWindow.DeleteLayoutConfirmOverride = (_, _) => Task.FromResult(false);

        Assert.Null(await window.DeleteCurrentLayoutAsync());
        Assert.True(File.Exists(Path.Combine(directory, "keeper.json")));
    }

    // ---- The default layout ----

    [WpfFact]
    public async Task SetAsDefault_IsUsedByTheNextWindowAndClearedByUnchecking()
    {
        EditTextWindow window = CreateWindow("text");
        Assert.Null(await window.SwitchLayoutAsync(LayoutIds.Explorer));

        Assert.Null(await window.SetGlobalDefaultAsync(makeDefault: true));
        Assert.Equal(new LayoutStoreDefaults("explorer"), await store.ReadDefaultsAsync(TestContext.Current.CancellationToken));

        EditTextWindow next = new();
        LayOut(next);
        Assert.Equal(LayoutIds.Explorer, next.LayoutId);

        Assert.Null(await window.SetGlobalDefaultAsync(makeDefault: false));
        EditTextWindow last = new();
        LayOut(last);
        Assert.Equal(LayoutIds.Classic, last.LayoutId);
        Assert.Equal(new LayoutStoreDefaults(null), store.ReadDefaults());
    }

    [WpfFact]
    public async Task AClassicItemSavedWhileAnotherLayoutIsTheDefault_SaysSoInHistory()
    {
        EditTextWindow explorer = CreateWindow("text");
        Assert.Null(await explorer.SwitchLayoutAsync(LayoutIds.Explorer));
        Assert.Null(await explorer.SetGlobalDefaultAsync(true));

        EditTextWindow classic = new(new LayoutRequest(RequestedId: LayoutIds.Classic));
        LayOut(classic);

        Assert.Equal(LayoutIds.Classic, classic.AsHistoryItem().LayoutId);
        Assert.Null(CreateWindowWithoutDefault().AsHistoryItem().LayoutId);

        EditTextWindow CreateWindowWithoutDefault()
        {
            store.SetGlobalDefaultAsync(null, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            EditTextWindow window = new();
            LayOut(window);
            return window;
        }
    }

    [WpfFact]
    public async Task DeletingTheDefaultLayout_ClearsTheDefault()
    {
        EditTextWindow window = CreateWindow("text");
        Assert.Null(await window.SaveCurrentLayoutAsync("Fav"));
        Assert.Null(await window.SetGlobalDefaultAsync(true));

        Assert.Null(await window.DeleteCurrentLayoutAsync());

        Assert.Equal(new LayoutStoreDefaults(null), store.ReadDefaults());
    }

    [WpfFact]
    public async Task AMissingOrMalformedDefault_OpensClassicAndSaysWhy()
    {
        await store.SetGlobalDefaultAsync("gone", TestContext.Current.CancellationToken);
        EditTextWindow missing = new();
        LayOut(missing);
        // A layout from the store is resolved after the window is made, which says why it can't be had.
        for (int attempt = 0; attempt < 100 && PendingNotice(missing) is null; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
            DoEvents();
        }

        Assert.Equal(LayoutIds.Classic, missing.LayoutId);
        Assert.Contains("gone", PendingNotice(missing));

        await File.WriteAllTextAsync(Path.Combine(directory, FileLayoutStore.MetadataFileName), "{ not json", TestContext.Current.CancellationToken);
        EditTextWindow malformed = new();
        LayOut(malformed);

        Assert.Equal(LayoutIds.Classic, malformed.LayoutId);
        Assert.Contains("not valid", PendingNotice(malformed));
    }

    // ---- A saved layout is the first one rendered ----

    [WpfFact]
    public async Task ASavedLayoutWithoutTheCalcPane_NeverMakesTheCalcPaneWhetherRequestedOrTheDefault()
    {
        await store.SaveAsync(
            LayoutPaneEditor.RemovePane(BuiltInLayouts.Classic, PaneIds.Calc) with { Id = "no-calc", Name = "No calc" },
            TestContext.Current.CancellationToken);
        int created = CalcPane.CreatedCount;

        EditTextWindow requested = new(new LayoutRequest(RequestedId: "no-calc"));
        LayOut(requested);

        Assert.Equal("no-calc", requested.LayoutId);
        Assert.Equal(created, CalcPane.CreatedCount);

        await store.SetGlobalDefaultAsync("no-calc", TestContext.Current.CancellationToken);
        EditTextWindow byDefault = new();
        LayOut(byDefault);

        Assert.Equal("no-calc", byDefault.LayoutId);
        Assert.Equal(created, CalcPane.CreatedCount);
    }

    [WpfFact]
    public void WithNoDefaultAndNoRequest_TheWindowIsClassicWithNoNotice()
    {
        EditTextWindow window = new();
        LayOut(window);

        Assert.Equal(LayoutIds.Classic, window.LayoutId);
        Assert.Null(PendingNotice(window));
        Assert.Null(window.AsHistoryItem().LayoutId);
        Assert.False(Directory.Exists(directory));
    }
}
