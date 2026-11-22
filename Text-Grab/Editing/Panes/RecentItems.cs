using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab.Editing.Panes;

/// <summary>Where the recent items pane gets its items. Tests give it a fake, so they never read a person's history.</summary>
internal interface IRecentItemsSource
{
    /// <summary>
    /// The recent Edit Text Window items, grabs and files, newest first. Reads the app's history, which
    /// <see cref="HistoryService"/> wants on the UI thread, so callers are on it.
    /// </summary>
    IReadOnlyList<TrayFlyoutItem> Load();
}

/// <summary>The items the tray flyout shows: <see cref="TrayFlyoutItemProvider.LoadAll"/> over every source.</summary>
internal sealed class HistoryRecentItemsSource : IRecentItemsSource
{
    public IReadOnlyList<TrayFlyoutItem> Load()
        => TrayFlyoutItemProvider.LoadAll(Singleton<HistoryService>.Instance, TrayFlyoutSources.All);
}

/// <summary>Chooses the source a new recent pane reads, so tests can replace it for the whole assembly.</summary>
internal static class RecentItemsSources
{
    /// <summary>The source new panes read from when set; null means the person's history. For tests.</summary>
    internal static IRecentItemsSource? Override { get; set; }

    public static IRecentItemsSource Create() => Override ?? new HistoryRecentItemsSource();
}

/// <summary>Asks the window to open a recent item.</summary>
/// <param name="Item">The item to open.</param>
/// <param name="InNewWindow">True to open it in a new window even if this one could show it.</param>
internal sealed record RecentItemRequest(TrayFlyoutItem Item, bool InNewWindow);

/// <summary>Where a recent item opens.</summary>
public enum RecentOpenTarget
{
    /// <summary>In the window that holds the pane, replacing its (blank) document.</summary>
    ThisWindow,

    /// <summary>In a new Edit Text Window.</summary>
    NewEditTextWindow,

    /// <summary>In a Grab Frame.</summary>
    GrabFrame,

    /// <summary>The item is a file that no longer exists; nothing opens.</summary>
    MissingFile,
}

/// <summary>Decides where a recent item opens. Pure, so the rules can be tested without a window.</summary>
internal static class RecentItemOpener
{
    /// <summary>A PDF that was opened in a Grab Frame, which is where it opens again.</summary>
    public static bool IsGrabFramePdf(HistoryInfo history)
        => history.IsPdfDocument && !string.IsNullOrWhiteSpace(history.ImagePath);

    /// <param name="kind">What kind of item it is.</param>
    /// <param name="history">The item's history entry.</param>
    /// <param name="fileExists">For a file item, whether the file is still on disk.</param>
    /// <param name="inNewWindow">The person asked for a new window.</param>
    /// <param name="windowIsBlank">
    /// The pane's window has nothing in it to lose: no text and no file open. Showing an item there
    /// leaves the window's own file state alone, so a window with a file open is never reused.
    /// </param>
    public static RecentOpenTarget Decide(TrayFlyoutItemKind kind, HistoryInfo history, bool fileExists, bool inNewWindow, bool windowIsBlank)
    {
        switch (kind)
        {
            case TrayFlyoutItemKind.GrabFrame:
                return RecentOpenTarget.GrabFrame;

            case TrayFlyoutItemKind.File when !fileExists:
                return RecentOpenTarget.MissingFile;

            case TrayFlyoutItemKind.File when IsGrabFramePdf(history):
                return RecentOpenTarget.GrabFrame;

            default:
                return windowIsBlank && !inNewWindow ? RecentOpenTarget.ThisWindow : RecentOpenTarget.NewEditTextWindow;
        }
    }

    public static bool FileExists(HistoryInfo history)
        => !string.IsNullOrWhiteSpace(history.SourcePath) && File.Exists(history.SourcePath);
}

/// <summary>
/// The recent pane's state: every recent item, the search, and the rows that match it. Reading history
/// is the pane's job (<see cref="IRecentItemsSource"/>); opening is the window's
/// (<see cref="OpenRequested"/>).
/// </summary>
internal sealed partial class RecentItemsViewModel : ObservableObject
{
    private readonly IRecentItemsSource source;
    private List<TrayFlyoutItem> allItems = [];

    public RecentItemsViewModel(IRecentItemsSource source)
    {
        this.source = source;
    }

    // Fields, not partial properties: CommunityToolkit.Mvvm 8.4.0 generates partial properties only with
    // LangVersion preview. MVVMTK0045 is about WinRT marshalling (UWP and WinUI 3), which WPF never uses.
#pragma warning disable MVVMTK0045
    /// <summary>What the person typed in the search box.</summary>
    [ObservableProperty]
    private string query = string.Empty;

    /// <summary>The highlighted row. It stays the same row across a reload.</summary>
    [ObservableProperty]
    private TrayFlyoutItem? selectedItem;
#pragma warning restore MVVMTK0045

    /// <summary>The rows that match <see cref="Query"/>, newest first, at most <see cref="TrayFlyoutItemProvider.MaxItemCount"/>.</summary>
    public ObservableCollection<TrayFlyoutItem> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;

    /// <summary>Says why there are no rows.</summary>
    public string EmptyText => allItems.Count == 0 ? "Nothing here yet" : "No matches";

    /// <summary>The window is asked to open an item.</summary>
    public event EventHandler<RecentItemRequest>? OpenRequested;

    /// <summary>Reads the history again and shows what matches the search. The search and the selected row stay.</summary>
    public void Refresh()
    {
        allItems = [.. source.Load()];
        OnPropertyChanged(nameof(EmptyText));
        ApplyQuery();
    }

    /// <summary>Forgets every item, so a pane that left its window holds no history text.</summary>
    public void Release()
    {
        allItems = [];
        SelectedItem = null;
        Items.Clear();
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    partial void OnQueryChanged(string value) => ApplyQuery();

    [RelayCommand]
    private void Open(TrayFlyoutItem? item) => Request(item ?? SelectedItem ?? Items.FirstOrDefault(), inNewWindow: false);

    [RelayCommand]
    private void OpenInNewWindow(TrayFlyoutItem? item) => Request(item ?? SelectedItem, inNewWindow: true);

    [RelayCommand]
    private void ClearQuery() => Query = string.Empty;

    [RelayCommand]
    private void Copy(TrayFlyoutItem? item)
    {
        if (item is not null)
            TrayFlyoutController.CopyItem(item);
    }

    [RelayCommand]
    private void CopyPath(TrayFlyoutItem? item)
    {
        if (item is not null)
            TrayFlyoutController.CopyItemPath(item);
    }

    [RelayCommand]
    private void OpenDirectory(TrayFlyoutItem? item)
    {
        if (item is not null)
            TrayFlyoutController.OpenItemDirectory(item);
    }

    private void Request(TrayFlyoutItem? item, bool inNewWindow)
    {
        if (item is not null)
            OpenRequested?.Invoke(this, new RecentItemRequest(item, inNewWindow));
    }

    private void ApplyQuery()
    {
        string? selectedKey = SelectedItem is null ? null : KeyOf(SelectedItem);

        List<TrayFlyoutItem> matching = TrayFlyoutItemProvider.Query(allItems, Query, TrayFlyoutItemProvider.MaxItemCount);
        Items.Clear();
        foreach (TrayFlyoutItem item in matching)
            Items.Add(item);

        SelectedItem = selectedKey is null ? null : Items.FirstOrDefault(item => KeyOf(item) == selectedKey);
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>What makes a row the same row after a reload: a file by its path, anything else by its history ID.</summary>
    internal static string KeyOf(TrayFlyoutItem item)
        => item.Kind == TrayFlyoutItemKind.File
            ? $"file:{item.History.SourcePath.ToLowerInvariant()}"
            : $"{item.Kind}:{item.History.ID}";
}
