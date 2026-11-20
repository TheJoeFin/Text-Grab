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

    /// <summary>Counts layout resolutions, so that only the latest one applies.</summary>
    private int layoutResolution;

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
        Grid.SetRow(paneLayout.View, Grid.GetRow(EditorAreaGrid));
        root.Children.Insert(index, paneLayout.View);
        RegisterName(PaneLayoutHostName, paneLayout.View);
        paneLayout.Render(BuiltInLayouts.Classic);
        UpdateCalcToggles();
    }

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
        if (historyLayoutId is not null)
            _ = ResolveLayoutAsync();
    }

    /// <summary>
    /// A file opened into a window that was given no layout takes its file type's built-in layout, if it
    /// has one (<see cref="LayoutFileTypeDefaults"/>, empty in E4a).
    /// </summary>
    private void UseFileTypeLayout(string filePath)
    {
        if (requestedLayoutId is null && historyLayoutId is null && LayoutFileTypeDefaults.LayoutIdFor(filePath) is not null)
            _ = ResolveLayoutAsync();
    }

    /// <summary>The layout to save with this window's history item: null for Classic.</summary>
    private string? LayoutIdToSave()
    {
        // With the pane layout off, an item reopened from history keeps the layout it was saved with.
        if (paneLayout is null)
            return historyLayoutId;

        return LayoutId == LayoutIds.Classic ? null : LayoutId;
    }

    /// <summary>
    /// Chooses this window's layout (decision D5: the caller's, the history item's, the file type's,
    /// then Classic) and shows it. Only the latest call applies.
    /// </summary>
    private async Task ResolveLayoutAsync()
    {
        int resolution = ++layoutResolution;
        LayoutRequest request = new(requestedLayoutId, historyLayoutId, OpenedFilePath);

        if (paneLayout is null)
        {
            if (requestedLayoutId is not null && requestedLayoutId != LayoutIds.Classic)
                ShowLayoutNotice($"The layout \"{requestedLayoutId}\" needs the new pane layout, which is turned off in Settings > Edit Text. Text Grab opened the previous editor instead.");
            return;
        }

        ResolvedLayout resolved;
        try
        {
            resolved = await new LayoutResolver(AppServices.Provider.GetRequiredService<ILayoutStore>(), IsKnownPane).ResolveAsync(request);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            resolved = new ResolvedLayout(BuiltInLayouts.Classic, $"The layout could not be loaded: {ex.Message} Text Grab opened the Classic layout instead.");
        }

        if (resolution != layoutResolution || _isClosed)
            return;

        string? notice = resolved.Notice;
        if (resolved.Layout.Id != LayoutId && ApplyLayout(resolved.Layout) is string problem)
            notice ??= $"{problem} Text Grab kept the {LayoutId} layout.";

        if (notice is not null)
            ShowLayoutNotice(notice);
    }

    /// <summary>
    /// Says why a layout could not be used: logged for diagnostics and UI tests at once, and shown to the
    /// person once the window is on screen, never while it is being made.
    /// </summary>
    private void ShowLayoutNotice(string notice)
    {
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
        if (pendingLayoutNotice is not string notice)
            return;

        pendingLayoutNotice = null;
        Dispatcher.BeginInvoke(() => ShowLayoutNotice(notice), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
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
