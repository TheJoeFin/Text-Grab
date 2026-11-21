using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Layout;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using Text_Grab.Services;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Text_Grab;

/// <summary>
/// The layout picker (v5 E6 <c>picker</c>): the Layouts and Panes submenus of the Window menu, and what
/// they do to the window's layout. Each action is an ordinary click handler until E2 moves them onto
/// the registry (<see cref="LayoutPickerActions"/> pins their IDs).
/// </summary>
/// <remarks>
/// Switching layouts re-renders the dock area in place (<see cref="ApplyLayout"/>): the document,
/// selection and file state live on the session, and the editor area is moved, not rebuilt, so each
/// editor's own undo history survives. The calc pane's open state and width belong to the window and
/// carry over (<see cref="ApplyLayout"/>). The Panes menu is the other way to arrange panes: the calc item
/// goes through the same toggle path as Ctrl+P, so the layout, the menu item, the toolbar button and
/// the settings agree.
/// </remarks>
public partial class EditTextWindow
{
    /// <summary>Marks the menu items the picker fills in, so that a refill replaces exactly those.</summary>
    private static readonly object PickerItemMarker = new();

    private int layoutMenuVersion;

    /// <summary>Asks for the name of a layout to save; null cancels. For tests, which cannot answer a dialog.</summary>
    internal static Func<EditTextWindow, Task<string?>>? LayoutNamePromptOverride { get; set; }

    /// <summary>Asks whether to delete a layout. For tests.</summary>
    internal static Func<EditTextWindow, string, Task<bool>>? DeleteLayoutConfirmOverride { get; set; }

    /// <summary>Shows the Layouts and Panes submenus: with the pane layout off they stay out of the Window menu.</summary>
    private void ShowPickerMenus()
    {
        LayoutsMenuItem.Visibility = Visibility.Visible;
        PanesMenuItem.Visibility = Visibility.Visible;

        // A submenu with no items does not open, so it would never raise SubmenuOpened to be filled.
        LoadPaneMenuItems();
    }

    // ---- The menus ----

    private async void LayoutsMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, LayoutsMenuItem))
            return;

        await LoadLayoutMenuItemsAsync();
    }

    private void PanesMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, PanesMenuItem))
            LoadPaneMenuItems();
    }

    /// <summary>
    /// Fills the Layouts submenu from the store: the built-in layouts, then the saved ones, with the one
    /// the window shows checked, and sets which of the fixed items apply. Called when the submenu opens
    /// and by the command palette.
    /// </summary>
    internal async Task LoadLayoutMenuItemsAsync()
    {
        if (paneLayout is null)
            return;

        int version = ++layoutMenuVersion;
        ILayoutStore store = LayoutStore;
        LayoutStoreDefaults defaults = await store.ReadDefaultsAsync();
        IReadOnlyList<LayoutSummary> layouts = await store.ListAsync();
        if (version != layoutMenuVersion || _isClosed)
            return;

        knownGlobalDefault = defaults.GlobalDefaultId;
        string current = LayoutId;

        foreach (object item in LayoutsMenuItem.Items.Cast<object>().Where(IsPickerItem).ToList())
            LayoutsMenuItem.Items.Remove(item);

        int index = LayoutsMenuItem.Items.IndexOf(LayoutsListEndSeparator);
        bool separated = false;
        foreach (LayoutSummary layout in layouts)
        {
            if (!layout.IsBuiltIn && !separated)
            {
                LayoutsMenuItem.Items.Insert(index++, new Separator { Tag = PickerItemMarker });
                separated = true;
            }

            LayoutsMenuItem.Items.Insert(index++, CreateLayoutMenuItem(layout, current));
        }

        SaveLayoutAsMenuItem.IsEnabled = true;
        ResetLayoutMenuItem.IsEnabled = ResetTargetId() is not null;
        DeleteLayoutMenuItem.IsEnabled = layouts.Any(layout => layout.Id == current && !layout.IsBuiltIn);
        SetDefaultLayoutMenuItem.IsEnabled = layouts.Any(layout => layout.Id == current);
        SetDefaultLayoutMenuItem.IsChecked = (defaults.GlobalDefaultId ?? LayoutIds.Classic) == current;
    }

    private MenuItem CreateLayoutMenuItem(LayoutSummary layout, string current)
    {
        MenuItem item = new()
        {
            Header = layout.Name.Replace("_", "__", StringComparison.Ordinal),
            IsCheckable = true,
            IsChecked = layout.Id == current,
            Tag = PickerItemMarker,
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(item, $"EditText.Menu.Layouts.Item.{layout.Id}");
        item.Click += async (_, _) => await SwitchLayoutAsync(layout.Id);
        return item;
    }

    /// <summary>Fills the Panes submenu with one checkable item per pane that can be made, checked when it is shown.</summary>
    private void LoadPaneMenuItems()
    {
        if (paneLayout is null)
            return;

        // Made once; after that only which items are checked changes, as the layout does.
        if (PanesMenuItem.Items.Count > 0)
        {
            foreach (MenuItem existing in PanesMenuItem.Items.OfType<MenuItem>())
            {
                if (existing.Tag is string existingPaneId)
                    existing.IsChecked = paneLayout.IsPaneVisible(existingPaneId);
            }

            return;
        }

        foreach (PaneDescriptor descriptor in AppServices.Provider.GetServices<IEditorPaneFactory>()
            .Select(factory => factory.Descriptor)
            .OrderBy(descriptor => descriptor.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            MenuItem item = new()
            {
                Header = descriptor.Title.Replace("_", "__", StringComparison.Ordinal),
                IsCheckable = true,
                IsChecked = paneLayout.IsPaneVisible(descriptor.Id),
                Tag = descriptor.Id,
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(item, $"EditText.Menu.Panes.{descriptor.Id[(descriptor.Id.LastIndexOf('.') + 1)..]}");
            string paneId = descriptor.Id;
            item.Click += (_, _) => SetPaneShown(paneId, item.IsChecked, persistCalc: true);
            PanesMenuItem.Items.Add(item);
        }
    }

    private static bool IsPickerItem(object item) => item is FrameworkElement { Tag: { } tag } && ReferenceEquals(tag, PickerItemMarker);

    /// <summary>Fills both submenus for the command palette, which lists menu items but only sees what is already in them.</summary>
    private Task RefreshPickerMenusAsync()
    {
        LoadPaneMenuItems();
        return LoadLayoutMenuItemsAsync();
    }

    // ---- Switching ----

    /// <summary>
    /// Shows the layout with this ID in this window: built in or saved. A layout that cannot be used is
    /// left alone and the window says why. Returns null, or why.
    /// </summary>
    internal async Task<string?> SwitchLayoutAsync(string layoutId)
    {
        if (_isClosed)
            return null;

        if (paneLayout is null)
            return "The pane layout is off.";

        int resolution = ++layoutResolution;
        LayoutReadResult read = await LayoutStore.LoadAsync(layoutId);
        if (_isClosed || resolution != layoutResolution)
            return null;

        string? problem = read.Layout is null
            ? read.Error
            : EtwLayoutValidator.FindProblem(read.Layout, IsKnownPane);
        if (problem is null && read.Layout is not null)
        {
            // An explicit choice: a file opened later does not replace it, and a resolution under way is dropped.
            requestedLayoutId = layoutId;
            problem = ApplyLayout(read.Layout);
            if (problem is null)
            {
                ApplyLayoutModeOnSwitch(read.Layout);
                return null;
            }
        }

        string notice = $"The layout \"{layoutId}\" can't be shown. {problem} Text Grab kept the {LayoutId} layout.";
        ShowLayoutNotice(notice);
        return notice;
    }

    /// <summary>
    /// A switch changes the mode only if the new layout names one, and never when that would lose something
    /// (<see cref="LayoutDefaults.ModeOnSwitch"/>). Entering Code mode sets the layout's language only if the window has none.
    /// </summary>
    private void ApplyLayoutModeOnSwitch(EtwLayoutDefinition layout)
    {
        EtwEditorMode mode = LayoutDefaults.ModeOnSwitch(editorMode, layout, tableDocument?.HasViewFormatting == true);
        if (mode == editorMode)
            return;

        if (mode == EtwEditorMode.Code && LayoutDefaults.CodeLanguageOnSwitch(isCodeLanguageSet, layout) is CodeLanguageChoice choice)
            SetCodeLanguageFor(choice);

        SetEditorMode(mode);
    }

    // ---- The Panes menu ----

    /// <summary>
    /// Shows or hides a pane in this window. A pane the layout has is shown or collapsed. One it has not
    /// is added at its default side, and one added that way is removed again. The calc pane goes through
    /// the toggle path Ctrl+P uses.
    /// </summary>
    /// <param name="persistCalc">
    /// Whether to write the calc pane's open state to <c>CalcShowPane</c>, as the toggles do. False for tests.
    /// </param>
    internal void SetPaneShown(string paneId, bool shown, bool persistCalc = false)
    {
        if (paneLayout is null || paneId == PaneIds.Editor)
            return;

        if (paneId == PaneIds.Calc)
        {
            SetCalcPaneShown(shown, persistCalc);
            return;
        }

        EtwLayoutDefinition live = paneLayout.Layout;
        bool inLive = EtwLayoutValidator.ContainsPane(live.Root, paneId);
        if (shown)
        {
            if (inLive)
                paneLayout.SetPaneVisible(paneId, true);
            else if (DescriptorFor(paneId) is PaneDescriptor descriptor)
                ShowLayout(LayoutPaneEditor.AddPane(live, paneId, SideOf(descriptor), descriptor.DefaultSize));

            return;
        }

        if (!inLive)
            return;

        if (appliedLayout is not null && EtwLayoutValidator.ContainsPane(appliedLayout.Root, paneId))
            paneLayout.SetPaneVisible(paneId, false);
        else
            ShowLayout(LayoutPaneEditor.RemovePane(live, paneId));
    }

    private void SetCalcPaneShown(bool shown, bool persist)
    {
        if (calcPane is null)
        {
            if (!shown || paneLayout is null || DescriptorFor(PaneIds.Calc) is not PaneDescriptor descriptor)
                return;

            // The layout has no calc pane: add one. The window's open state decides how it shows.
            ShowCalcPaneMenuItem.IsChecked = true;
            if (persist)
                DefaultSettings.CalcShowPane = true;

            ShowLayout(LayoutPaneEditor.AddPane(paneLayout.Layout, PaneIds.Calc, SideOf(descriptor), descriptor.DefaultSize));
            return;
        }

        // What the menu item, Ctrl+P and the toolbar button do: the setting first, then the layout.
        if (persist)
        {
            ShowCalcPaneMenuItem.IsChecked = shown;
            DefaultSettings.CalcShowPane = shown;
            SetCalcPaneVisibility();
        }
        else
        {
            SetCalcPaneOpen(shown);
        }

        // A calc pane the Panes menu added leaves the layout again when it is hidden.
        if (!shown && paneLayout is not null && appliedLayout is not null && !EtwLayoutValidator.ContainsPane(appliedLayout.Root, PaneIds.Calc))
            ShowLayout(LayoutPaneEditor.RemovePane(paneLayout.Layout, PaneIds.Calc));
    }

    private static PaneDescriptor? DescriptorFor(string paneId)
        => AppServices.Provider.GetServices<IEditorPaneFactory>().FirstOrDefault(factory => factory.Descriptor.Id == paneId)?.Descriptor;

    private static PaneSide SideOf(PaneDescriptor descriptor) => descriptor.DefaultDock switch
    {
        PaneDock.Left => PaneSide.Left,
        PaneDock.Top => PaneSide.Top,
        PaneDock.Bottom => PaneSide.Bottom,
        _ => PaneSide.Right,
    };

    // ---- Save, reset, delete, default ----

    private async void SaveLayoutAsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string? name = LayoutNamePromptOverride is { } prompt ? await prompt(this) : await PromptForLayoutNameAsync();
        if (name is null)
            return;

        if (await SaveCurrentLayoutAsync(name) is string problem)
            ShowLayoutNotice(problem);
    }

    private async Task<string?> PromptForLayoutNameAsync()
    {
        TextBox box = new() { MinWidth = 280, Text = $"My {paneLayout?.Layout.Name ?? "layout"}" };
        System.Windows.Automation.AutomationProperties.SetAutomationId(box, "EditText.LayoutName");
        System.Windows.Automation.AutomationProperties.SetName(box, "Layout name");
        box.SelectAll();

        StackPanel content = new();
        content.Children.Add(new TextBlock { Text = "Name for this layout:", Margin = new Thickness(0, 0, 0, 8) });
        content.Children.Add(box);

        Wpf.Ui.Controls.MessageBoxResult result = await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Save current layout as",
            Content = content,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            Owner = this,
        }.ShowDialogAsync();

        return result == Wpf.Ui.Controls.MessageBoxResult.Primary ? box.Text : null;
    }

    /// <summary>
    /// Saves the window's layout, as it is now, as a user layout with this name; the window then shows that
    /// layout. Returns null, or why it could not be saved.
    /// </summary>
    internal async Task<string?> SaveCurrentLayoutAsync(string name)
    {
        if (_isClosed)
            return null;

        if (paneLayout is null)
            return "The pane layout is off.";

        name = name.Trim();
        if (name.Length == 0)
            return "A layout needs a name.";

        ILayoutStore store = LayoutStore;
        HashSet<string> taken = [.. (await store.ListAsync()).Select(layout => layout.Id)];
        if (_isClosed)
            return null;

        string id = LayoutIds.FromName(name, taken.Contains);

        EtwLayoutDefinition live = paneLayout.Layout;
        EtwLayoutDefinition saved = live with
        {
            Id = id,
            Name = name,
            // Reset returns to the built-in this started from, however many copies lie between.
            BasedOn = BuiltInLayouts.IsBuiltIn(live.Id) ? live.Id : live.BasedOn,
        };

        try
        {
            await store.SaveAsync(saved);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return _isClosed ? null : $"The layout could not be saved: {ex.Message}";
        }

        if (_isClosed)
            return null;

        layoutResolution++;
        requestedLayoutId = id;
        return ApplyLayout(saved);
    }

    /// <summary>
    /// The built-in layout "Reset to built-in" returns to, or null when there is nothing to return to: a
    /// built-in layout with no pane changes, or a saved layout with no recorded origin.
    /// </summary>
    internal string? ResetTargetId()
    {
        if (paneLayout is null)
            return null;

        EtwLayoutDefinition live = paneLayout.Layout;
        if (BuiltInLayouts.TryGet(live.Id, out EtwLayoutDefinition builtIn))
            return LayoutPaneEditor.HasPaneChanges(live, builtIn) ? builtIn.Id : null;

        return live.BasedOn is { } origin && BuiltInLayouts.IsBuiltIn(origin) ? origin : null;
    }

    /// <summary>
    /// Returns the window's layout to a built-in definition: this layout's own when it is built in, or the
    /// one a saved layout was copied from. Saved files are never touched. Returns null, or why not.
    /// </summary>
    internal string? ResetLayout()
    {
        if (ResetTargetId() is not string id || !BuiltInLayouts.TryGet(id, out EtwLayoutDefinition builtIn))
            return "There is nothing to reset to.";

        layoutResolution++;
        requestedLayoutId = id;
        string? problem = ApplyLayout(builtIn);
        if (problem is null)
            ApplyLayoutModeOnSwitch(builtIn);

        return problem;
    }

    private void ResetLayoutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ResetLayout() is string problem)
            ShowLayoutNotice(problem);
    }

    private async void DeleteLayoutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (await DeleteCurrentLayoutAsync() is string problem)
            ShowLayoutNotice(problem);
    }

    /// <summary>
    /// Deletes the saved layout this window shows, after asking. The window keeps its live copy, so the
    /// Layouts list then has no checked item; other windows showing it keep theirs too. A layout that was
    /// the default stops being it. Returns null, or why nothing was deleted.
    /// </summary>
    internal async Task<string?> DeleteCurrentLayoutAsync()
    {
        string id = LayoutId;
        if (paneLayout is null || BuiltInLayouts.IsBuiltIn(id))
            return "Built-in layouts can't be deleted.";

        string name = paneLayout.Layout.Name;
        bool confirmed = DeleteLayoutConfirmOverride is { } confirm
            ? await confirm(this, name)
            : await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Delete layout",
                Content = $"Delete the layout \"{name}\"? Windows that show it keep it until they close.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                Owner = this,
            }.ShowDialogAsync() is Wpf.Ui.Controls.MessageBoxResult.Primary;
        if (!confirmed)
            return null;

        ILayoutStore store = LayoutStore;
        try
        {
            if (!await store.DeleteAsync(id))
                return $"There is no saved layout \"{id}\" to delete.";

            if ((await store.ReadDefaultsAsync()).GlobalDefaultId == id)
            {
                await store.SetGlobalDefaultAsync(null);
                knownGlobalDefault = null;
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            return $"The layout could not be deleted: {ex.Message}";
        }

        return null;
    }

    private async void SetDefaultLayoutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // The item is checkable: its new state is what was asked for.
        if (await SetGlobalDefaultAsync(SetDefaultLayoutMenuItem.IsChecked) is string problem)
            ShowLayoutNotice(problem);
    }

    /// <summary>
    /// Makes this window's layout the one new windows open in, or, with false, clears the default so that
    /// they open in Classic. Returns null, or why it could not be saved.
    /// </summary>
    internal async Task<string?> SetGlobalDefaultAsync(bool makeDefault)
    {
        string id = LayoutId;
        string? stored = makeDefault && id != LayoutIds.Classic ? id : null;

        try
        {
            await LayoutStore.SetGlobalDefaultAsync(stored);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
        {
            return $"The default layout could not be saved: {ex.Message}";
        }

        knownGlobalDefault = stored;
        return null;
    }
}
