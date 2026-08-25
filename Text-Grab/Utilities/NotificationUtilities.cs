using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Text;
using System.Windows;

namespace Text_Grab.Utilities;

internal static class NotificationUtilities
{
    internal static void ShowToast(string copiedText)
    {
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(copiedText);

        // changed to using base64
        // the padding '=' will be encoded as '%3D' in the toast XML, so remove them
        string encodedString = Convert.ToBase64String(plainTextBytes).TrimEnd('=');

        // truncate toast body text first, if it is too long
        string toastBody;
        if (copiedText.Length > 150)
            toastBody = copiedText[..150] + "...";
        else
            toastBody = copiedText;

        // build the toast XML
        ToastContentBuilder toast = new ToastContentBuilder()
            .AddArgument("text", encodedString)
            .AddText("Text Grab")
            .AddText(toastBody);

        int toastSizeInBytes = Encoding.UTF8.GetByteCount(toast.Content.GetContent());
        if (toastSizeInBytes > 5000) // maximum size 5000 bytes
        {
            // the XML is still too large, the copied text itself will have to be truncated and some data will be lost

            int bytesFree = 5000 - (toastSizeInBytes - encodedString.Length); // max length for encodedString

            // 4 chars in a base64 string = 3 bytes, so convert it to max length for plainTextBytes
            int maxTextBytes = bytesFree / 4 * 3; // max length for plainTextBytes

            // as we removed the padding '='s, maybe we can fit in 2 or 3 more base64 chars, which is 1 or 2 text bytes
            if (bytesFree % 4 >= 2)
                maxTextBytes += bytesFree % 4 - 1;

            // convert only as much as bytesFree bytes

            plainTextBytes = new byte[maxTextBytes];
            int bytesUsed = 0;

            // Encoder.Convert() won't fail when the byte array is smaller than the size needed to hold the source string,
            // it will just convert as many characters as possible.
            Encoding.UTF8.GetEncoder().Convert(copiedText.AsSpan(), plainTextBytes.AsSpan(), true, out _, out bytesUsed, out _);

            encodedString = Convert.ToBase64String(plainTextBytes, 0, bytesUsed).TrimEnd('=');

            // rebuild the toast XML
            toast = new ToastContentBuilder()
                .AddArgument("text", encodedString)
                .AddText("Text Grab")
                .AddText(toastBody);
        }

        toast.Show();
    }

    /// <summary>
    /// Shows a toast for a finished audio transcription. Tapping it re-activates <paramref name="windowId"/>'s
    /// <see cref="EditTextWindow"/> — the one that actually received the transcript — via
    /// <see cref="TextGrabNotificationActivator"/>, rather than opening a new window with the text
    /// (which would also have to be truncated to fit the ~5000-byte toast payload limit).
    /// </summary>
    internal static void ShowTranscriptionCompleteToast(string fileDescription, Guid windowId)
    {
        new ToastContentBuilder()
            .AddArgument("windowId", windowId.ToString())
            .AddText("Text Grab")
            .AddText($"Transcription complete: {fileDescription}")
            .Show();
    }

    /// <summary>
    /// Shows a toast for a finished Local AI task (summarize, rewrite, translate, etc.). These run
    /// with the owning <see cref="EditTextWindow"/> disabled for the duration, so a user who has
    /// switched away benefits from the same "tap to come back" behavior as
    /// <see cref="ShowTranscriptionCompleteToast"/>.
    /// </summary>
    internal static void ShowLocalAiCompleteToast(string taskDescription, Guid windowId)
    {
        new ToastContentBuilder()
            .AddArgument("windowId", windowId.ToString())
            .AddText("Text Grab")
            .AddText($"{taskDescription} complete")
            .Show();
    }

    private const string WindowIdArgumentPrefix = "windowId=";

    /// <summary>
    /// Handles a toast's <c>windowId=</c> activation argument (see <see cref="ShowTranscriptionCompleteToast"/>)
    /// by re-activating the matching <see cref="EditTextWindow"/> — the one that actually received the
    /// transcript — instead of opening a new window. There are two toast-click entry points that both
    /// need this: <see cref="TextGrabNotificationActivator"/> (COM activation, used when the app isn't
    /// already running) and <c>App.LaunchFromToast</c> (fires in the already-running process). Returns
    /// true if the argument was a windowId (handled either by activating the window or, if it was
    /// already closed, by doing nothing) — callers should only fall back to their own "open a new
    /// window" behavior when this returns false.
    /// </summary>
    internal static bool TryActivateTranscriptionWindow(string argsInvoked)
    {
        if (!argsInvoked.StartsWith(WindowIdArgumentPrefix, StringComparison.Ordinal)
            || !Guid.TryParse(argsInvoked[WindowIdArgumentPrefix.Length..], out Guid windowId))
        {
            return false;
        }

        foreach (Window window in Application.Current.Windows)
        {
            if (window is EditTextWindow etw && etw.WindowId == windowId)
            {
                if (etw.WindowState == WindowState.Minimized)
                    etw.WindowState = WindowState.Normal;
                etw.Activate();
                break;
            }
        }

        // Handled either way: if the window was already closed there's nothing to re-activate.
        return true;
    }
}
