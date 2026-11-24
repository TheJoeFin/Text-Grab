using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using Text_Grab.Core.Actions;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab;

public partial class EditTextWindow
{
    private sealed record PaletteEntry(string Name, string Group, string Gesture, string Alias, Action Execute, bool IsAvailable = true)
    {
        public double ItemOpacity => IsAvailable ? 1.0 : 0.5;

        public string SearchText { get; } = $"{Name} {Group} {Alias} {Gesture} {Gesture.Replace(" ", "")}";
    }

    public static readonly DependencyProperty PaletteActionIdProperty = DependencyProperty.RegisterAttached(
        "PaletteActionId", typeof(string), typeof(EditTextWindow),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static string? GetPaletteActionId(DependencyObject element) => (string?)element.GetValue(PaletteActionIdProperty);

    public static void SetPaletteActionId(DependencyObject element, string? value) => element.SetValue(PaletteActionIdProperty, value);

    private IReadOnlyList<PaletteEntry> paletteEntries = [];
    private IInputElement? palettePreviousFocus;
    private int paletteOpening;
    private bool IsCommandPaletteOpen => CommandPaletteOverlay.Visibility == Visibility.Visible;

    private async void CommandPaletteMenuItem_Click(object sender, RoutedEventArgs e) => await OpenCommandPaletteAsync();

    private async void ToggleCommandPalette()
    {
        if (IsCommandPaletteOpen)
            CloseCommandPalette();
        else
            await OpenCommandPaletteAsync();
    }

    private async Task OpenCommandPaletteAsync()
    {
        if (_isClosed || IsCommandPaletteOpen || LoadingStack.Visibility == Visibility.Visible)
            return;

        int opening = ++paletteOpening;
        palettePreviousFocus = Keyboard.FocusedElement;
        CommandPaletteSearch.Clear();
        CommandPaletteOverlay.Visibility = Visibility.Visible;
        CommandPaletteSearch.Focus();

        ToggleCaseMenuItem.InputGestureText = ToggleCaseGestureText;
        RefreshApplyMenus();
        LoadGrabTemplateMenuItems(GrabTemplateMenuItem);
        LoadRecentTextHistory();
        Singleton<HistoryService>.Instance.PopulateMenuItemWithRecentFiles(OpenRecentPdfsMenuItem, this);
        RefreshEditLastGrabAvailability();
        if (TranscriptionOptionsMenuItem.Visibility == Visibility.Visible)
            PopulateTranscriptionModelMenu(CaptureTranscriptionModelMenuItem);

        Task loadLanguages = LoadLanguageMenuItemsAsync(LanguageMenuItem);
        Task loadGrabs = RefreshRecentGrabsMenuAsync();
        Task loadLayouts = RefreshPickerMenusAsync();
        RefreshCommandPaletteEntries();
        await Task.WhenAll(loadLanguages, loadGrabs, loadLayouts);
        if (!_isClosed && IsCommandPaletteOpen && opening == paletteOpening)
            RefreshCommandPaletteEntries(preserveSelection: true);
    }

    private void RefreshCommandPaletteEntries(bool preserveSelection = false)
    {
        IActionRegistry registry = ServiceAccess.GetRequired<IActionRegistry>();
        IInputElement target = GetCommandPaletteTarget();
        List<PaletteEntry> entries = [];
        AddMenuEntries(EditorMenu, "");

        Add("Insert Line Above", "Selection", "Ctrl + Enter",
            () => AddedLineAboveCommand(this, new RoutedEventArgs()));
        Add("Duplicate Line", "Selection", "Ctrl + D",
            () => DuplicateSelectedLine(this, new RoutedEventArgs()));
        Add("Reopen Last Edit Text Window", "File", "Ctrl + Shift + T",
            () => OpenRecentEditWindowExecuted(this, new RoutedEventArgs()));
        AddMenuEntries(BottomBar.ContextMenu, "Bottom Bar");

        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            if (FindResource("SpreadsheetContextMenu") is ContextMenu cells)
            {
                UpdateSpreadsheetWrapMenuItem(cells);
                AddMenuEntries(cells, "Spreadsheet", skipClipboardCommands: true);
            }

            if (SpreadsheetDataGrid.CurrentCell.IsValid)
            {
                AddMenuEntries((ContextMenu)FindResource("SpreadsheetRowHeaderContextMenu"), "Spreadsheet");
                AddMenuEntries((ContextMenu)FindResource("SpreadsheetColumnHeaderContextMenu"), "Spreadsheet");
            }

            if (selectedSpreadsheetCellCoordinates.Count > 0)
                Add("Copy Selected Spreadsheet Cells", "Spreadsheet", "",
                    () => CopySpreadsheetSelectionMenuItem_Click(this, new RoutedEventArgs()));
        }

        if (MarkdownToolbar.Visibility == Visibility.Visible)
        {
            foreach (Button button in MarkdownToolbar.FormattingCommandButtons.Where(button => button.IsEnabled))
            {
                if (button.ContextMenu is ContextMenu options)
                    AddMenuEntries(options, "Markdown / Heading");
                else
                    AddButton(button, AutomationProperties.GetName(button), "Markdown");
            }
        }

        foreach (IEditorPane pane in Panes)
        {
            foreach (PaneCommand command in pane.GetPaletteCommands())
                Add(command.Name, command.Group, "", command.Execute);
        }

        if (BottomBar.Visibility == Visibility.Visible)
        {
            if (LiveTranscriptionToggleButton.Visibility == Visibility.Visible)
                AddMenuEntry(TranscribeJustIconMenuItem, "Transcription Options", "Show Only the Transcription Icon");
            if (SimilarMatchesButton.Visibility == Visibility.Visible)
                AddButton(SimilarMatchesButton, "Find Similar Matches", "Selection");
            if (RegexPatternButton.Visibility == Visibility.Visible)
                AddButton(RegexPatternButton, "Find with Extracted Regex", "Selection");
            if (CharDetailsButton.Visibility == Visibility.Visible)
                AddButton(CharDetailsButton, "Show Character Details", "Selection");
            if (SimilarMatchesButton.Visibility == Visibility.Visible || RegexPatternButton.Visibility == Visibility.Visible)
            {
                ContextMenu patternMenu = (RegexPatternButton.Visibility == Visibility.Visible ? RegexPatternButton : SimilarMatchesButton).ContextMenu;
                PopulatePatternContextMenu(patternMenu);
                AddMenuEntries(patternMenu, "Selection");
            }
        }

        paletteEntries = entries;
        FilterCommandPalette(preserveSelection);

        void Add(string name, string group, string gesture, Action execute, string alias = "", bool isAvailable = true)
            => entries.Add(new(name, group, gesture, alias, execute, isAvailable));

        void AddButton(Button button, string name, string group)
        {
            if (button.IsEnabled)
                Add(name, group, "", () => ((IInvokeProvider)new ButtonAutomationPeer(button)).Invoke());
        }

        void AddMenuEntries(ItemsControl parent, string group, bool skipClipboardCommands = false)
        {
            foreach (MenuItem item in parent.Items.OfType<MenuItem>())
            {
                // The palette lists commands only; recent items are data, not commands.
                if (item.Visibility != Visibility.Visible || IsRecentItemsMenu(item))
                    continue;

                string name = new MenuItemAutomationPeer(item).GetName();
                if (item.HasItems)
                {
                    if (item.IsEnabled)
                        AddMenuEntries(item, group.Length == 0 ? name : $"{group} / {name}", skipClipboardCommands);
                }
                else if (item != CommandPaletteMenuItem && item != LanguageMenuItem
                         && item != WebSearchCollection
                         && (!skipClipboardCommands || item.Command is null))
                {
                    AddMenuEntry(item, group, name);
                }
            }
        }

        void AddMenuEntry(MenuItem item, string group, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            // Commands that don't apply right now (e.g. Unstack without a multi-line selection)
            // stay listed so they can be found, but are shown dimmed and do nothing.
            bool isAvailable = CanInvokePaletteMenuItem(item, target);

            ActionDescriptor? descriptor = null;
            if (GetPaletteActionId(item) is string id && !registry.TryGet(id, out descriptor))
                Debug.WriteLine($"No command palette action registered for '{id}'.");

            string gesture = string.IsNullOrWhiteSpace(item.InputGestureText)
                ? descriptor?.DefaultGesture ?? ""
                : item.InputGestureText;

            // The registry supplies metadata, but the menu owns editor-aware behavior and arguments.
            string alias = descriptor is null ? "" : $"{descriptor.DisplayName} {descriptor.Category}";
            Add(name, group, gesture, () => InvokePaletteMenuItem(item), alias, isAvailable);
        }
    }

    private bool IsRecentItemsMenu(MenuItem item)
        => item == OpenRecentMenuItem || item == OpenRecentGrabsMenuItem || item == OpenRecentPdfsMenuItem;

    private IInputElement GetCommandPaletteTarget() => editorMode switch
    {
        EtwEditorMode.Markdown => MarkdownEditorControl,
        EtwEditorMode.Code => CodeEditorControl.TextArea,
        EtwEditorMode.Spreadsheet when (palettePreviousFocus ?? Keyboard.FocusedElement) is TextBox editor
            && ReferenceEquals(editor, activeSpreadsheetCellEditor) => editor,
        EtwEditorMode.Spreadsheet => SpreadsheetDataGrid,
        _ => PassedTextControl,
    };

    private static bool CanInvokePaletteMenuItem(MenuItem item, IInputElement target)
        => item.Command is RoutedCommand routed
            ? !Equals(item.ReadLocalValue(IsEnabledProperty), false) && routed.CanExecute(item.CommandParameter, item.CommandTarget ?? target)
            : item.IsEnabled;

    private void InvokePaletteMenuItem(MenuItem item)
    {
        IInputElement target = GetCommandPaletteTarget();
        if (!CanInvokePaletteMenuItem(item, target))
            return;

        if (item.Command is RoutedCommand routed)
            routed.Execute(item.CommandParameter, item.CommandTarget ?? target);
        else
            ((IInvokeProvider)new MenuItemAutomationPeer(item)).Invoke();
    }

    private void CommandPaletteSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (CommandPaletteResults is not null && IsCommandPaletteOpen)
            FilterCommandPalette();
    }

    private void FilterCommandPalette(bool preserveSelection = false)
    {
        PaletteEntry? previous = preserveSelection ? CommandPaletteResults.SelectedItem as PaletteEntry : null;
        string query = CommandPaletteSearch.Text.Trim();
        string[] words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        const StringComparison comparison = StringComparison.CurrentCultureIgnoreCase;
        PaletteEntry[] results = [.. paletteEntries
            .Where(entry => words.All(word => entry.SearchText.Contains(word, comparison)))
            .OrderBy(entry => entry.Name.Equals(query, comparison) ? 0
                : entry.Name.StartsWith(query, comparison) ? 1 : 2)
            .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.Group, StringComparer.CurrentCultureIgnoreCase)];

        CommandPaletteResults.ItemsSource = results;
        CommandPaletteNoResults.Visibility = results.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        MovePaletteSelection(previous is null ? 0 : Array.FindIndex(results,
            entry => entry.Name == previous.Name && entry.Group == previous.Group));
    }

    private void MovePaletteSelection(int index)
    {
        if (CommandPaletteResults.Items.Count == 0)
            return;

        CommandPaletteResults.SelectedIndex = Math.Clamp(index, 0, CommandPaletteResults.Items.Count - 1);
        CommandPaletteResults.ScrollIntoView(CommandPaletteResults.SelectedItem);
    }

    private bool HandleCommandPaletteKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.P && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (!e.IsRepeat)
                ToggleCommandPalette();
            e.Handled = true;
            return true;
        }

        if (!IsCommandPaletteOpen)
            return false;

        switch (e.Key)
        {
            case Key.Escape:
                CloseCommandPalette();
                break;
            case Key.Up:
                MovePaletteSelection(CommandPaletteResults.SelectedIndex - 1);
                break;
            case Key.Down:
                MovePaletteSelection(CommandPaletteResults.SelectedIndex + 1);
                break;
            case Key.Enter:
                ExecuteSelectedCommandPaletteEntry();
                break;
            default:
                return true; // Let the palette handle the key; skip the window's editor shortcuts.
        }

        e.Handled = true;
        return true;
    }

    private void CommandPaletteOverlay_KeyDown(object sender, KeyEventArgs e)
    {
        // TextBox has already handled its own editing shortcuts. Do not bubble the rest to the editor.
        ModifierKeys modifiers = Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt);
        if (modifiers is ModifierKeys.Control or ModifierKeys.Alt || e.Key is >= Key.F1 and <= Key.F24)
            e.Handled = true;
    }

    private void CommandPaletteOverlay_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, CommandPaletteOverlay))
        {
            CloseCommandPalette();
            e.Handled = true;
        }
    }

    private void CommandPaletteResults_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(CommandPaletteResults, source)
            is ListBoxItem { Content: PaletteEntry entry })
        {
            CommandPaletteResults.SelectedItem = entry;
            e.Handled = true;
            ExecuteSelectedCommandPaletteEntry();
        }
    }

    private void ExecuteSelectedCommandPaletteEntry()
    {
        if (CommandPaletteResults.SelectedItem is not PaletteEntry { IsAvailable: true } entry)
            return;

        CloseCommandPalette();
        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            // Header actions invoked from the palette should use the current cell, not a prior right-click.
            spreadsheetContextRowIndex = null;
            spreadsheetContextColumnIndex = null;
        }
        entry.Execute();
    }

    private void EditTextWindow_Deactivated(object? sender, EventArgs e)
    {
        if (IsCommandPaletteOpen)
            CloseCommandPalette(restoreFocus: false);
    }

    private void CloseCommandPalette(bool restoreFocus = true)
    {
        IInputElement target = GetCommandPaletteTarget();
        IInputElement? previousFocus = palettePreviousFocus;
        palettePreviousFocus = null;
        CommandPaletteOverlay.Visibility = Visibility.Collapsed;
        CommandPaletteResults.ItemsSource = null;
        paletteEntries = [];

        if (restoreFocus && (previousFocus is null || Keyboard.Focus(previousFocus) != previousFocus))
            Keyboard.Focus(target);
    }
}
