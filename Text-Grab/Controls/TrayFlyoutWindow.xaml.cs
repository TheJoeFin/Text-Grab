using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab.Controls;

public partial class TrayFlyoutWindow : Window
{
    private const int ButtonColumns = 4;
    private const int ButtonRows = 2;

    private readonly Button[] buttons;
    private List<TrayFlyoutItem> allItems = [];
    private int maxCount = TrayFlyoutItemProvider.DefaultItemCount;

    public TrayFlyoutWindow()
    {
        InitializeComponent();

        // Row-major: top row is "open last", bottom row launches a mode.
        buttons =
        [
            PreviousRegionButton, LastGrabFrameButton, LastEditTextButton, ClipboardButton,
            FullscreenButton, GrabFrameButton, EditTextButton, LookupButton,
        ];

        IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>Reloads the items and button states. Call right before showing.</summary>
    public void PrepareForShow()
    {
        Properties.Settings settings = AppUtilities.TextGrabSettings;
        maxCount = TrayFlyoutItemProvider.ClampItemCount(settings.TrayFlyoutItemCount);
        TrayFlyoutSources sources = TrayFlyoutItemProvider.ParseSources(settings.TrayFlyoutSources);

        HistoryService history = Singleton<HistoryService>.Instance;
        allItems = TrayFlyoutItemProvider.LoadAll(history, sources);

        PreviousRegionButton.IsEnabled = history.HasAnyFullscreenHistory();
        LastGrabFrameButton.IsEnabled = history.HasAnyRecentGrabs();
        LastEditTextButton.IsEnabled = history.GetEditWindows().Count > 0;
        ClipboardButton.IsEnabled = TrayActions.ClipboardHasContent();

        SearchBox.Text = string.Empty;
        RefreshList();
    }

    public void FocusSearch()
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    private void RefreshList()
    {
        if (ItemsList is null)
            return;

        List<TrayFlyoutItem> shown = TrayFlyoutItemProvider.Query(allItems, SearchBox.Text, maxCount);
        ItemsList.ItemsSource = shown;
        ItemsList.SelectedIndex = shown.Count > 0 ? 0 : -1;
        if (shown.Count > 0)
            ItemsList.ScrollIntoView(shown[0]);

        EmptyText.Text = allItems.Count == 0 ? "Nothing here yet" : "No matches";
        EmptyText.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            return;

        // Don't hold history rows (and their images) while the flyout is idle.
        allItems = [];
        ItemsList.ItemsSource = null;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        nint handle = new WindowInteropHelper(this).Handle;
        int roundedCorners = DwmWindowCornerRound;
        _ = DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref roundedCorners, sizeof(int));
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (IsVisible)
            Hide();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        bool isEmpty = string.IsNullOrEmpty(SearchBox.Text);
        SearchPlaceholder.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        ClearSearchButton.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;

        RefreshList();
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        FocusSearch();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        TrayFlyoutController.CloseThenRun(TrayActions.OpenSettings);
    }

    #region Mouse and buttons

    private void ItemsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;

        if (ItemsControl.ContainerFromElement(ItemsList, source) is ListBoxItem { DataContext: TrayFlyoutItem item })
            TrayFlyoutController.ActivateItem(item);
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string action })
            return;

        Action? launch = action switch
        {
            "PreviousRegion" => TrayActions.CopyPreviousRegion,
            "LastGrabFrame" => TrayActions.OpenLastGrabFrame,
            "LastEditText" => TrayActions.OpenLastEditTextWindow,
            "Clipboard" => TrayActions.OpenClipboardContent,
            "Fullscreen" => TrayActions.OpenFullscreenGrab,
            "GrabFrame" => TrayActions.OpenGrabFrame,
            "EditText" => TrayActions.OpenEditTextWindow,
            "Lookup" => TrayActions.OpenLookup,
            _ => null,
        };

        if (launch is not null)
            TrayFlyoutController.CloseThenRun(launch);
    }

    #endregion Mouse and buttons

    #region Keyboard

    private enum FocusZone { Search, List, Buttons }

    private FocusZone CurrentZone()
    {
        if (SearchBox.IsKeyboardFocusWithin)
            return FocusZone.Search;

        if (Keyboard.FocusedElement is Button focused && buttons.Contains(focused))
            return FocusZone.Buttons;

        return FocusZone.List;
    }

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Type-to-search: typing anywhere in the flyout goes to the search box.
        if (CurrentZone() == FocusZone.Search || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
            return;

        SearchBox.Focus();
        SearchBox.Text += e.Text;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = string.Empty;
                FocusSearch();
            }
            else
            {
                TrayFlyoutController.Close();
            }

            e.Handled = true;
            return;
        }

        e.Handled = CurrentZone() switch
        {
            FocusZone.Search => HandleSearchKey(e),
            FocusZone.List => HandleListKey(e),
            _ => HandleButtonKey(e),
        };
    }

    private bool HandleSearchKey(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (ItemsList.Items.Count > 0)
                    FocusListItem(Math.Max(0, ItemsList.SelectedIndex));
                else
                    FocusFirstEnabledButton();
                return true;
            case Key.Enter:
                return ActivateSelectedItem(copyInstead: Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            default:
                return false;
        }
    }

    private bool HandleListKey(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up when ItemsList.SelectedIndex <= 0:
                FocusSearch();
                return true;
            case Key.Down when ItemsList.SelectedIndex >= ItemsList.Items.Count - 1:
                FocusFirstEnabledButton();
                return true;
            case Key.Enter:
                return ActivateSelectedItem(copyInstead: Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            default:
                return false;
        }
    }

    private bool HandleButtonKey(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is not Button focused)
            return false;

        int index = Array.IndexOf(buttons, focused);
        int row = index / ButtonColumns;
        int column = index % ButtonColumns;

        switch (e.Key)
        {
            case Key.Left:
                MoveInRow(row, column, -1);
                return true;
            case Key.Right:
                MoveInRow(row, column, 1);
                return true;
            case Key.Down:
                if (row < ButtonRows - 1)
                    MoveToRow(row + 1, column);
                return true;
            case Key.Up:
                if (row > 0)
                    MoveToRow(row - 1, column);
                else if (ItemsList.Items.Count > 0)
                    FocusListItem(ItemsList.Items.Count - 1);
                else
                    FocusSearch();
                return true;
            default:
                return false;
        }
    }

    private bool ActivateSelectedItem(bool copyInstead)
    {
        TrayFlyoutItem? item = ItemsList.SelectedItem as TrayFlyoutItem
            ?? ItemsList.Items.OfType<TrayFlyoutItem>().FirstOrDefault();

        if (item is null)
            return false;

        if (copyInstead)
        {
            TrayFlyoutController.CopyItem(item);
            TrayFlyoutController.Close();
        }
        else
        {
            TrayFlyoutController.ActivateItem(item);
        }

        return true;
    }

    private void FocusListItem(int index)
    {
        if (index < 0 || index >= ItemsList.Items.Count)
            return;

        ItemsList.SelectedIndex = index;
        ItemsList.ScrollIntoView(ItemsList.Items[index]);
        ItemsList.UpdateLayout();

        if (ItemsList.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem container)
            container.Focus();
        else
            Dispatcher.BeginInvoke(() => (ItemsList.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem)?.Focus(), DispatcherPriority.Loaded);
    }

    private void FocusFirstEnabledButton()
    {
        Button? first = buttons.FirstOrDefault(b => b.IsEnabled);
        first?.Focus();
    }

    private void MoveInRow(int row, int column, int step)
    {
        for (int c = column + step; c >= 0 && c < ButtonColumns; c += step)
        {
            Button candidate = buttons[(row * ButtonColumns) + c];
            if (candidate.IsEnabled)
            {
                candidate.Focus();
                return;
            }
        }
    }

    private void MoveToRow(int targetRow, int column)
    {
        // Prefer the same column, then the nearest enabled column in the target row.
        IEnumerable<int> order = Enumerable.Range(0, ButtonColumns).OrderBy(c => Math.Abs(c - column));
        foreach (int c in order)
        {
            Button candidate = buttons[(targetRow * ButtonColumns) + c];
            if (candidate.IsEnabled)
            {
                candidate.Focus();
                return;
            }
        }
    }

    #endregion Keyboard

    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
