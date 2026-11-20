using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab;

/// <summary>
/// The window's side of the recent items pane (<see cref="RecentPane"/>): opening the item it asks for.
/// The pane reads the history and lists it; where an item opens is the window's call.
/// </summary>
public partial class EditTextWindow
{
    /// <summary>The window's recent items pane while its layout has one; null in Classic, which never makes it.</summary>
    private RecentPane? recentPane;

    private void AttachRecentPane(RecentPane pane, System.Text.Json.JsonElement? config)
    {
        recentPane = pane;
        pane.Attach(Session, config);
        pane.OpenRequested += RecentPane_OpenRequested;
    }

    private void DetachRecentPane(RecentPane pane)
    {
        pane.OpenRequested -= RecentPane_OpenRequested;
        pane.Detach();
        if (ReferenceEquals(recentPane, pane))
            recentPane = null;
    }

    private async void RecentPane_OpenRequested(object? sender, RecentItemRequest request)
    {
        try
        {
            await OpenRecentItemAsync(request.Item, request.InNewWindow);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Debug.WriteLine($"The recent pane failed to open an item: {ex}");
        }
    }

    /// <summary>
    /// Whether this window has nothing to lose by showing a recent item: no text, and no file open.
    /// <see cref="ShowHistoryItem"/> leaves the window's own file state as it is, so a window with a file
    /// open is never reused, whether or not it has unsaved edits.
    /// </summary>
    internal bool IsBlankForRecentItems => string.IsNullOrWhiteSpace(Session.Text) && string.IsNullOrEmpty(OpenedFilePath);

    /// <summary>
    /// Opens a recent item as the tray flyout does, except that an item can open in this window when
    /// it is blank (<see cref="RecentItemOpener"/>).
    /// </summary>
    internal async Task OpenRecentItemAsync(TrayFlyoutItem item, bool inNewWindow)
    {
        HistoryInfo history = item.History;
        RecentOpenTarget target = RecentItemOpener.Decide(
            item.Kind, history, RecentItemOpener.FileExists(history), inNewWindow, IsBlankForRecentItems);

        switch (target)
        {
            case RecentOpenTarget.GrabFrame:
                GrabFrame grabFrame = RecentItemOpener.IsGrabFramePdf(history)
                    ? new GrabFrame(history, history.SourcePath)
                    : new GrabFrame(history);
                grabFrame.Show();
                break;

            case RecentOpenTarget.MissingFile:
                await PromptForMissingRecentFileAsync(history.SourcePath);
                break;

            case RecentOpenTarget.ThisWindow when item.Kind == TrayFlyoutItemKind.File:
                await OpenPath(history.SourcePath);
                break;

            case RecentOpenTarget.ThisWindow:
                ShowHistoryItem(history);
                break;

            case RecentOpenTarget.NewEditTextWindow when item.Kind == TrayFlyoutItemKind.File:
                EditTextWindow fileWindow = new();
                fileWindow.Show();
                fileWindow.Activate();
                await fileWindow.OpenPath(history.SourcePath);
                break;

            default:
                EditTextWindow textWindow = new(history);
                textWindow.Show();
                textWindow.Activate();
                break;
        }

        // Opening a file records it as recent, which this window's activation will not see.
        if (!_isClosed)
            recentPane?.Refresh();
    }

    private async Task PromptForMissingRecentFileAsync(string path)
    {
        Wpf.Ui.Controls.MessageBox box = new()
        {
            Title = "File not found",
            Content = $"{path}\n\nThis file no longer exists.",
            PrimaryButtonText = "Remove recent item",
            CloseButtonText = "Cancel",
            Owner = this,
            MinWidth = 520,
        };

        if (await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary)
            Singleton<HistoryService>.Instance.RemoveRecentFile(path);
    }
}
