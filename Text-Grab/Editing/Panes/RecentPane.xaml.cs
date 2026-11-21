using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Text_Grab.Core.Editing;
using Text_Grab.Models;

namespace Text_Grab.Editing.Panes;

/// <summary>
/// The recent items pane: a searchable list of the recent Edit Text Window items, grabs and files
/// (issue #592), the same items the tray flyout shows. Double-click or Enter opens one; the window
/// decides where (<see cref="OpenRequested"/>).
/// </summary>
/// <remarks>
/// <para>
/// There is no change feed for history yet (F6 is shelved), so the list is read when the pane attaches
/// and each time its window is activated, which is when new items appear: opening an item in another
/// window or grabbing something deactivates this one. A hidden pane only notes that it is stale and reads
/// when it is shown again.
/// </para>
/// <para>
/// The view holds no logic of its own beyond bindings and key handling; the state is
/// <see cref="RecentItemsViewModel"/>. Nothing here is tied to <c>Loaded</c> or <c>Unloaded</c>, because
/// AvalonDock raises both again whenever it re-parents the view.
/// </para>
/// </remarks>
public partial class RecentPane : UserControl, IEditorPane
{
    public const string PaneId = "textgrab.pane.recent";

    public static readonly PaneDescriptor Definition = new(
        PaneId, "Recent", "History24", PaneDock.Left, DefaultSize: 260, MinSize: 180);

    private const string PaletteGroup = "Recent";

    private static int attachedCount;
    private static int createdCount;

    private readonly RecentItemsViewModel viewModel;
    private IEditorSession? session;
    private bool isStale;

    public RecentPane()
        : this(RecentItemsSources.Create())
    {
    }

    internal RecentPane(IRecentItemsSource source)
    {
        Interlocked.Increment(ref createdCount);
        viewModel = new RecentItemsViewModel(source);
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>The window is asked to open an item. The pane never opens anything itself.</summary>
    internal event EventHandler<RecentItemRequest>? OpenRequested;

    public PaneDescriptor Descriptor => Definition;

    public FrameworkElement View => this;

    /// <summary>How many recent panes are attached to a window. For tests that check a pane is not made or kept needlessly.</summary>
    internal static int AttachedCount => Volatile.Read(ref attachedCount);

    /// <summary>How many recent panes have been made, attached or not.</summary>
    internal static int CreatedCount => Volatile.Read(ref createdCount);

    internal RecentItemsViewModel ViewModel => viewModel;

    internal bool IsStale => isStale;

    public void Attach(IEditorSession session, JsonElement? config)
    {
        if (this.session is not null)
            throw new InvalidOperationException("The recent pane is already attached.");

        this.session = session;
        Interlocked.Increment(ref attachedCount);

        viewModel.OpenRequested += ViewModel_OpenRequested;
        IsVisibleChanged += RecentPane_IsVisibleChanged;
        Refresh();
    }

    public void Detach()
    {
        if (session is null)
            return;

        viewModel.OpenRequested -= ViewModel_OpenRequested;
        IsVisibleChanged -= RecentPane_IsVisibleChanged;

        // The items hold the text of recent documents; a pane that left its window has no reason to.
        viewModel.Release();
        isStale = false;
        session = null;
        Interlocked.Decrement(ref attachedCount);
    }

    /// <summary>Nothing: the pane's width and whether it is shown are the host's, and the search is not worth keeping.</summary>
    public JsonElement? SaveConfig() => null;

    public IReadOnlyList<PaneCommand> GetPaletteCommands()
    {
        if (session is null)
            return [];

        return
        [
            new("Search recent items", PaletteGroup, FocusSearch),
            new("Refresh recent items", PaletteGroup, Refresh),
        ];
    }

    public void OnWindowActivated()
    {
        if (session is null)
            return;

        if (IsVisible)
            Refresh();
        else
            isStale = true;
    }

    /// <summary>Reads the history again, now. The search and the selected row stay.</summary>
    internal void Refresh()
    {
        isStale = false;
        viewModel.Refresh();
    }

    /// <summary>Puts keyboard focus in the search box.</summary>
    internal void FocusSearch()
    {
        FocusElement(SearchBox);
        SearchBox.SelectAll();
    }

    private void RecentPane_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && isStale)
            Refresh();
    }

    private void ViewModel_OpenRequested(object? sender, RecentItemRequest request)
        => OpenRequested?.Invoke(this, request);

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when viewModel.Items.Count > 0:
                viewModel.SelectedItem ??= viewModel.Items[0];
                FocusSelectedRow();
                e.Handled = true;
                break;

            case Key.Enter:
                OpenFromKeyboard();
                e.Handled = true;
                break;

            case Key.Escape when viewModel.Query.Length > 0:
                viewModel.ClearQueryCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void ItemsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                OpenFromKeyboard();
                e.Handled = true;
                break;

            case Key.Escape:
                SearchBox.Focus();
                e.Handled = true;
                break;

            case Key.Up when ItemsList.SelectedIndex <= 0:
                SearchBox.Focus();
                e.Handled = true;
                break;
        }
    }

    private void OpenFromKeyboard()
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            viewModel.OpenInNewWindowCommand.Execute(viewModel.SelectedItem ?? (viewModel.Items.Count > 0 ? viewModel.Items[0] : null));
        else
            viewModel.OpenCommand.Execute(null);
    }

    internal void FocusSelectedRow()
    {
        if (viewModel.SelectedItem is null)
            return;

        ItemsList.ScrollIntoView(viewModel.SelectedItem);
        ItemsList.UpdateLayout();
        if (ItemsList.ItemContainerGenerator.ContainerFromItem(viewModel.SelectedItem) is ListBoxItem row)
            FocusElement(row);
    }

    private static void FocusElement(UIElement element)
    {
        if (!element.Focus()
            && element.Focusable
            && element.IsEnabled
            && FocusManager.GetFocusScope(element) is DependencyObject scope)
        {
            FocusManager.SetFocusedElement(scope, element);
        }
    }

    private void Item_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: TrayFlyoutItem item })
        {
            viewModel.OpenCommand.Execute(item);
            e.Handled = true;
        }
    }

    private void ItemMenuOpen_Click(object sender, RoutedEventArgs e)
        => viewModel.OpenCommand.Execute(RowOf(sender));

    private void ItemMenuOpenInNewWindow_Click(object sender, RoutedEventArgs e)
        => viewModel.OpenInNewWindowCommand.Execute(RowOf(sender));

    private void ItemMenuCopy_Click(object sender, RoutedEventArgs e)
        => viewModel.CopyCommand.Execute(RowOf(sender));

    private void ItemMenuCopyPath_Click(object sender, RoutedEventArgs e)
        => viewModel.CopyPathCommand.Execute(RowOf(sender));

    private void ItemMenuOpenDirectory_Click(object sender, RoutedEventArgs e)
        => viewModel.OpenDirectoryCommand.Execute(RowOf(sender));

    private static TrayFlyoutItem? RowOf(object sender)
        => sender is MenuItem { DataContext: TrayFlyoutItem item } ? item : null;
}
