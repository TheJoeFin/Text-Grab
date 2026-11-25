using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Layout;
using Text_Grab.Models;
using Text_Grab.Editing.Panes;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab;

/// <summary>
/// The pane layout (v5 E4a): the editor area rendered from a layout definition through
/// <see cref="LayoutRenderer"/> instead of the hand-written grid. Behind the <c>EtwPaneLayout</c>
/// setting; with it off, nothing here runs and AvalonDock is never loaded.
/// </summary>
public partial class EditTextWindow
{
    /// <summary>The name the pane host is registered under, for <see cref="System.Windows.FrameworkElement.FindName"/>.</summary>
    private const string PaneLayoutHostName = "PaneLayoutHost";

    /// <summary>Whether this window renders its editor area from a layout. Fixed when the window is made.</summary>
    private readonly bool usesPaneLayout = PaneLayoutOverride ?? PaneLayoutSetting.IsOn(AppUtilities.TextGrabSettings.EtwPaneLayout);

    /// <summary>The layout a caller asked for (<c>--layout</c>, a link, code), or null.</summary>
    private string? requestedLayoutId;

    /// <summary>The layout the history item this window reopened was saved with, or null.</summary>
    private string? historyLayoutId;

    /// <summary>
    /// What the window was made to open: set by the constructor before <c>InitializeComponent</c>, which
    /// is when the first layout is rendered, so that the layout it names is rendered first and no other
    /// layout's panes are made on the way (<see cref="TakeInitialLayoutRequest"/>).
    /// </summary>
    private readonly LayoutRequest? initialLayout;

    /// <summary>The file the window was made to open, which <see cref="OpenedFilePath"/> does not hold until it is read.</summary>
    private string? initialFilePath;

    /// <summary>True for a window that restores a history item, whose saved mode and code language beat a layout's defaults.</summary>
    private readonly bool modeFromHistory;

    /// <summary>Counts layout resolutions, so that only the latest one applies.</summary>
    private int layoutResolution;

    /// <summary>
    /// The layout as it was last shown by a switch or a load, before the Panes menu changed anything. A
    /// pane in it is collapsed when unchecked; a pane the Panes menu added is removed.
    /// </summary>
    private EtwLayoutDefinition? appliedLayout;

    /// <summary>The layout the person set as the default, as this window last read it; null for Classic.</summary>
    private string? knownGlobalDefault;

    /// <summary>
    /// The layout store, for tests to replace so that they never read or write the person's layouts. Null
    /// means the app's own (<see cref="ILayoutStore"/> in the service provider).
    /// </summary>
    internal static ILayoutStore? LayoutStoreOverride { get; set; }

    private static ILayoutStore LayoutStore => LayoutStoreOverride ?? AppServices.Provider.GetRequiredService<ILayoutStore>();

    /// <summary>Why a layout could not be used, waiting for the window to be shown.</summary>
    private string? pendingLayoutNotice;

    /// <summary>
    /// The window's pane host, when <see cref="usesPaneLayout"/>; otherwise null and never made. Held as
    /// <see cref="IPaneHost"/> so that the window never loads AvalonDock with the pane layout off.
    /// </summary>
    private IPaneHost? paneLayout;

    /// <summary>
    /// Turns the pane layout on or off for windows made while it is set, whatever the setting says. For
    /// tests, which must not write the person's settings. Null means the setting decides.
    /// </summary>
    internal static bool? PaneLayoutOverride { get; set; }

    /// <summary>Whether this window's editor area is rendered from a layout.</summary>
    internal bool UsesPaneLayout => usesPaneLayout;

    /// <summary>The ID of the layout this window shows; Classic when the pane layout is off.</summary>
    internal string LayoutId => paneLayout?.Layout.Id ?? LayoutIds.Classic;

    /// <summary>The window's layout as it is now, sizes included, or null when the pane layout is off.</summary>
    internal EtwLayoutDefinition? CurrentLayout => paneLayout?.Layout;

    /// <summary>
    /// Replaces the hand-written editor grid with the pane host and renders Classic in it. Called once,
    /// from <c>Window_Initialized</c>, in place of <see cref="AttachCalcPane"/>.
    /// </summary>
    /// <remarks>
    /// <c>ReadingEditorHost</c> (the four editors and the reading bookmark) moves into the layout's
    /// editor document as one unit. The grid it leaves, with its calc column, splitter and shadow, is
    /// taken out of the window.
    /// </remarks>
    private void InitializePaneLayout()
    {
        Grid root = (Grid)EditorAreaGrid.Parent;
        int index = root.Children.IndexOf(EditorAreaGrid);
        EditorAreaGrid.Children.Remove(ReadingEditorHost);
        root.Children.Remove(EditorAreaGrid);

        // Inside AvalonDock's templates a name lookup would stop at a template's name scope, so the
        // editors' ElementName bindings (the Markdown and code editors follow the text box's font) would
        // find nothing. The editor area takes the window's own name scope with it.
        NameScope.SetNameScope(ReadingEditorHost, NameScope.GetNameScope(this));

        paneLayout = CreatePaneHost();
        paneLayout.PaneLayoutChanged += (_, _) => UpdateTocToggle();
        Grid.SetRow(paneLayout.View, Grid.GetRow(EditorAreaGrid));
        root.Children.Insert(index, paneLayout.View);
        RegisterName(PaneLayoutHostName, paneLayout.View);

        // The layout the window was made for, when it is built in and usable, is the first one rendered:
        // rendering Classic first would make its calc pane, and its calculation service, just to detach it.
        (EtwLayoutDefinition? firstLayout, bool resolveAfter) = TakeInitialLayoutRequest();
        EtwLayoutDefinition first = firstLayout ?? BuiltInLayouts.Classic;
        paneLayout.Render(first);
        appliedLayout = first;
        ShowPickerMenus();
        UpdateCalcToggles();
        if (first.Id != LayoutIds.Classic)
            ApplyLayoutDefaultMode(first);

        if (resolveAfter)
            _ = ResolveLayoutAsync();
    }

    /// <summary>
    /// Reads what the constructor was asked to open. <c>First</c> is the built-in layout to render first, or
    /// null to render Classic. A layout that is not built in (one saved in the store), or that cannot be
    /// shown, still renders Classic first and is then resolved the usual way (<c>ResolveAfter</c>), which
    /// loads it or says why it cannot.
    /// </summary>
    /// <param name="readStore">
    /// Whether to read the layout store (the person's default layout, and a saved layout the window is
    /// to open in), which is one small file each and is done here, synchronously, so that the layout the
    /// window ends in is the first one it renders. False with the pane layout off, which has no layouts.
    /// </param>
    private (EtwLayoutDefinition? First, bool ResolveAfter) TakeInitialLayoutRequest(bool readStore = true)
    {
        LayoutRequest request = initialLayout ?? new LayoutRequest();
        requestedLayoutId = string.IsNullOrWhiteSpace(request.RequestedId) ? null : request.RequestedId.Trim();
        historyLayoutId = string.IsNullOrWhiteSpace(request.HistoryId) ? null : request.HistoryId;
        initialFilePath = request.FilePath;

        LayoutStoreDefaults defaults = readStore ? LayoutStore.ReadDefaults() : new LayoutStoreDefaults(null);
        knownGlobalDefault = defaults.GlobalDefaultId;

        string? id = LayoutResolver.ChooseId(new LayoutRequest(requestedLayoutId, historyLayoutId, initialFilePath, defaults.GlobalDefaultId), layoutForFile: readStore ? LayoutForFile : null);
        if (id is null && defaults.Error is not null)
            ShowLayoutNotice($"{defaults.Error} Text Grab opened the Classic layout instead.");

        if (id is null || id == LayoutIds.Classic || !readStore)
            return (null, false);

        if (BuiltInLayouts.TryGet(id, out EtwLayoutDefinition layout) && EtwLayoutValidator.FindProblem(layout, IsKnownPane) is null)
            return (layout, false);

        // A layout from the store: loaded now, so that no other layout's panes are made on the way.
        // One that does not load or cannot be shown renders Classic and is then resolved, which says why.
        if (!BuiltInLayouts.IsBuiltIn(id))
        {
            LayoutReadResult read = LayoutStore.Load(id);
            if (read.Layout is not null && EtwLayoutValidator.FindProblem(read.Layout, IsKnownPane) is null)
                return (read.Layout, false);
        }

        return (null, true);
    }

    /// <summary>
    /// With the pane layout off there is nothing to render, but a layout somebody asked for still gets its
    /// notice, as <see cref="RequestLayoutAsync"/> gives one.
    /// </summary>
    private void TakeInitialLayoutRequestWithPaneLayoutOff()
    {
        _ = TakeInitialLayoutRequest(readStore: false);
        if (requestedLayoutId is not null)
            _ = ResolveLayoutAsync();
    }

    /// <summary>
    /// Applies <paramref name="layout"/>'s <c>DefaultMode</c> and <c>DefaultCodeLanguage</c> to a window
    /// that has no file and no history item, as it first takes the layout (<see cref="LayoutDefaults"/>).
    /// A mode somebody chose is kept, and so is whatever a history item saved. A file that is opened
    /// decides its own mode in <see cref="OpenPath"/>.
    /// </summary>
    private void ApplyLayoutDefaultMode(EtwLayoutDefinition layout)
    {
        if (modeFromHistory || !string.IsNullOrEmpty(OpenedFilePath))
            return;

        EtwEditorMode mode = LayoutDefaults.ModeForBlankWindow(editorMode, layout);
        if (mode == editorMode)
            return;

        if (mode == EtwEditorMode.Code)
            SetCodeLanguageFor(LayoutDefaults.CodeLanguageFor(null, layout));

        // Before the window is loaded, Window_Loaded applies the mode, as it does for a history item's.
        if (IsLoaded)
            SetEditorMode(mode);
        else
            editorMode = mode;
    }

    /// <summary>Sets the code language a <see cref="LayoutDefaults.CodeLanguageFor"/> choice names: a language, Plain Text, or what the text looks like.</summary>
    private void SetCodeLanguageFor(CodeLanguageChoice choice)
        => SetCodeLanguage(choice.Detect ? DetectCodeLanguage() : choice.Name);

    /// <summary>
    /// Shows <paramref name="layout"/> in this window. Returns null, or, when the layout cannot be
    /// shown, why; the window then keeps what it shows.
    /// </summary>
    /// <remarks>
    /// The calc pane's width and whether it is shown are the window's, kept across layouts as they are
    /// kept in settings and history: a layout that has the calc pane gets the width it has now, and
    /// shows it if the window does.
    /// </remarks>
    internal string? ApplyLayout(EtwLayoutDefinition layout)
    {
        string? problem = ShowLayout(layout);
        if (problem is null)
            appliedLayout = layout;

        return problem;
    }

    /// <summary>
    /// <see cref="ApplyLayout"/>, except that the layout shown is not the new "applied" one: what the
    /// Panes menu does to the live layout.
    /// </summary>
    private string? ShowLayout(EtwLayoutDefinition layout)
    {
        if (paneLayout is null)
            return "The pane layout is off.";

        if (EtwLayoutValidator.FindProblem(layout, IsKnownPane) is string problem)
            return problem;

        GridLength? calcWidth = paneLayout.GetPaneSize(PaneIds.Calc);
        paneLayout.Render(layout);

        if (calcPane is not null)
        {
            if (calcWidth is GridLength width)
                paneLayout.SetPaneSize(PaneIds.Calc, width);

            SetCalcPaneVisibilityInLayout(paneLayout, calcPane, ShowCalcPaneMenuItem.IsChecked);
        }
        else if (_previousTextWrapping.HasValue)
        {
            // The calc pane left with the old layout; so does the wrapping it turned off.
            PassedTextControl.TextWrapping = _previousTextWrapping.Value;
            _previousTextWrapping = null;
        }

        UpdateCalcToggles();
        UpdateTocToggle();
        return null;
    }

    private static bool IsKnownPane(string paneId)
        => AppServices.Provider.GetServices<IEditorPaneFactory>().Any(factory => factory.Descriptor.Id == paneId);

    /// <summary>
    /// Opens this window in the layout with this ID: what <c>--layout</c>, <c>text-grab://edit-text?layout=</c>
    /// and <see cref="WindowUtilities"/> use. A layout that cannot be used leaves Classic, and the window
    /// says why once it is shown. Completes when the layout is applied.
    /// </summary>
    internal Task RequestLayoutAsync(string? layoutId)
    {
        requestedLayoutId = string.IsNullOrWhiteSpace(layoutId) ? null : layoutId.Trim();
        return ResolveLayoutAsync();
    }

    /// <summary>Remembers the layout a history item was saved with and applies it.</summary>
    private void UseHistoryLayout(string? layoutId)
    {
        historyLayoutId = string.IsNullOrWhiteSpace(layoutId) ? null : layoutId;

        // A window made for a history item was told its layout before it rendered anything.
        if (historyLayoutId is not null && initialLayout is null)
            _ = ResolveLayoutAsync();
    }

    /// <summary>
    /// A file opened into a window that was given no layout takes its file type's built-in layout, if it
    /// has one (<see cref="LayoutFileTypeDefaults"/>: <c>.mmd</c> and <c>.mermaid</c>). Completes when the
    /// layout is applied, so that <see cref="OpenPath"/> sets the file's mode from the layout it ends up in.
    /// </summary>
    private Task UseFileTypeLayoutAsync(string filePath)
        => requestedLayoutId is null && historyLayoutId is null && LayoutForFile(filePath) is not null
            ? ResolveLayoutAsync()
            : Task.CompletedTask;

    /// <summary>
    /// A file's own layout: the one it was last shown in (<see cref="FileViewStateStore"/>) if the person
    /// asked for files to be remembered and that layout still exists, else its type's built-in layout.
    /// </summary>
    private string? LayoutForFile(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && paneLayout is not null && DefaultSettings.EtwRememberFileView
            && FileViewStateStore.Get(filePath)?.LayoutId is string saved && !string.IsNullOrWhiteSpace(saved)
            && (BuiltInLayouts.IsBuiltIn(saved) || LayoutStore.Load(saved).Layout is not null))
            return saved;

        return LayoutFileTypeDefaults.LayoutIdFor(filePath);
    }

    /// <summary>
    /// Remembers the layout, mode and code language this window shows the opened file in, so that the file
    /// reopens that way. Called when the window closes and when another file replaces this one.
    /// </summary>
    private void SaveFileViewState()
    {
        if (string.IsNullOrWhiteSpace(OpenedFilePath) || !DefaultSettings.EtwRememberFileView)
            return;

        // With the pane layout off there is no layout to report, so what was remembered stays.
        string? layoutId = paneLayout is null ? FileViewStateStore.Get(OpenedFilePath)?.LayoutId : LayoutId;
        string? codeLanguage = editorMode == EtwEditorMode.Code && isCodeLanguageSet ? codeLanguageName ?? string.Empty : null;
        FileViewStateStore.Set(OpenedFilePath, layoutId, editorMode, codeLanguage);
    }

    /// <summary>The layout to save with this window's history item: null for Classic.</summary>
    private string? LayoutIdToSave()
    {
        // With the pane layout off, an item reopened from history keeps the layout it was saved with.
        if (paneLayout is null)
            return historyLayoutId;

        // Classic is null so that history files do not change, unless the person made another layout the
        // default: then a Classic item must say so, or it would reopen in the default.
        if (LayoutId == LayoutIds.Classic)
            return knownGlobalDefault is null ? null : LayoutIds.Classic;

        return LayoutId;
    }

    /// <summary>
    /// Chooses this window's layout (decision D5: the caller's, the history item's, the file type's,
    /// then Classic) and shows it. Only the latest call applies.
    /// </summary>
    private async Task ResolveLayoutAsync()
    {
        int resolution = ++layoutResolution;
        LayoutRequest request = new(requestedLayoutId, historyLayoutId, OpenedFilePath ?? initialFilePath);
        Func<string?, string?> layoutForFile = LayoutForFile;

        if (paneLayout is null)
        {
            if (requestedLayoutId is not null && requestedLayoutId != LayoutIds.Classic)
                ShowLayoutNotice($"The layout \"{requestedLayoutId}\" needs the new pane layout, which is turned off in Settings > Edit Text. Text Grab opened the previous editor instead.");
            return;
        }

        ResolvedLayout resolved;
        try
        {
            resolved = await new LayoutResolver(LayoutStore, IsKnownPane, layoutForFile).ResolveAsync(request);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            resolved = new ResolvedLayout(BuiltInLayouts.Classic, $"The layout could not be loaded: {ex.Message} Text Grab opened the Classic layout instead.");
        }

        if (resolution != layoutResolution || _isClosed)
            return;

        string? notice = resolved.Notice;
        if (resolved.Layout.Id != LayoutId)
        {
            if (ApplyLayout(resolved.Layout) is string problem)
                notice ??= $"{problem} Text Grab kept the {LayoutId} layout.";
            else
                ApplyLayoutDefaultMode(resolved.Layout);
        }

        if (notice is not null)
            ShowLayoutNotice(notice);
    }

    /// <summary>
    /// Says why a layout could not be used: logged for diagnostics and UI tests at once, and shown to the
    /// person once the window is on screen, never while it is being made.
    /// </summary>
    private void ShowLayoutNotice(string notice)
    {
        if (_isClosed)
            return;

        Debug.WriteLine($"Layout notice: {notice}");
        AutomationDiagnostics.Record("layout-notice", new { notice });

        if (!IsLoaded)
        {
            pendingLayoutNotice = notice;
            return;
        }

        // Not modal: the window behind it works as it would have in Classic.
        _ = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Layout not available",
            Content = notice,
            CloseButtonText = "OK",
            Owner = this,
        }.ShowDialogAsync(showAsDialog: false);
    }

    /// <summary>Shows the notice a layout left while the window was being made. Called from <c>Window_Loaded</c>.</summary>
    private void ShowPendingLayoutNotice()
    {
        if (_isClosed || pendingLayoutNotice is not string notice)
            return;

        pendingLayoutNotice = null;
        Dispatcher.BeginInvoke(() =>
        {
            if (!_isClosed)
                ShowLayoutNotice(notice);
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    // The one place the window names LayoutRenderer, kept out of line so that compiling any other method
    // never loads it (and with it AvalonDock).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private IPaneHost CreatePaneHost() => new LayoutRenderer(ReadingEditorHost, CreatePane, AttachLayoutPane, DetachLayoutPane);

    private void AttachLayoutPane(IEditorPane pane, JsonElement? config)
    {
        if (pane is CalcPane calc)
        {
            calcPane = calc;
            ConnectCalcPane(calc, config);
            return;
        }

        if (pane is RecentPane recent)
        {
            AttachRecentPane(recent, config);
            return;
        }

        // Relative images in the preview resolve against the file the window has open.
        if (pane is MarkdownPreviewPane preview)
            preview.FilePathProvider = () => OpenedFilePath;

        pane.Attach(Session, config);
    }

    private void DetachLayoutPane(IEditorPane pane)
    {
        if (ReferenceEquals(pane, calcPane))
        {
            DetachCalcPane();
            UnregisterName(CalcPaneName);
            calcPane = null;
            return;
        }

        if (pane is RecentPane recent)
        {
            DetachRecentPane(recent);
            return;
        }

        pane.Detach();
    }

    /// <summary>The calc toggles do nothing in a layout without the calc pane, so they are turned off there.</summary>
    private void UpdateCalcToggles()
    {
        bool hasCalcPane = calcPane is not null;
        ShowCalcPaneMenuItem.IsEnabled = hasCalcPane;
        CalcToggleButton.IsEnabled = hasCalcPane;
    }
}
