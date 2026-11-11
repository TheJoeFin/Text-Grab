using System;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;
using Text_Grab.Core.Abstractions;

namespace Text_Grab.Services;

/// <summary>
/// <see cref="IToastService"/> over the toast plumbing the app already uses (v5 F4):
/// <c>ToastContentBuilder.Show()</c>, which goes through <c>ToastNotificationManagerCompat</c> and
/// its AUMID and COM-activator registration for unpackaged installs. Packaged installs are
/// registered by their manifest.
///
/// Display only. The toast carries no activation argument, and both of the app's toast-click entry
/// points (<c>App.LaunchFromToast</c> and <c>TextGrabNotificationActivator</c>) ignore a click
/// without one, so what happens when an existing Text Grab toast is clicked does not change.
/// </summary>
public sealed class ToastService : IToastService
{
    /// <summary>Longest title or body shown, matching the grab toast's visible-text limit.</summary>
    internal const int MaxTextLength = 150;

    /// <inheritdoc/>
    public void Show(string title, string body)
    {
        try
        {
            BuildToast(title, body).Show();
        }
        catch (Exception ex)
        {
            // No package identity and no registrable AUMID, notifications disabled by policy, or
            // an oversized payload: none is the caller's problem. The text is not logged.
            Debug.WriteLine($"Toast not shown: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>The toast <see cref="Show"/> would display. Separate so tests can check it without showing it.</summary>
    internal static ToastContentBuilder BuildToast(string? title, string? body)
        => new ToastContentBuilder()
            .AddText(Truncate(title))
            .AddText(Truncate(body));

    private static string Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length <= MaxTextLength)
            return text;

        // Do not cut a surrogate pair in half.
        int length = char.IsHighSurrogate(text[MaxTextLength - 1]) ? MaxTextLength - 1 : MaxTextLength;
        return text[..length] + "...";
    }
}
