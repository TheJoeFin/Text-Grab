using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Text_Grab.Utilities;

internal static class AutomationDiagnostics
{
    private static readonly object SyncRoot = new();
    private static readonly HashSet<Window> TrackedWindows = [];
    private static AutomationProfile? _profile;

    internal static void Initialize(AutomationProfile profile)
    {
        _profile = profile;
        Directory.CreateDirectory(profile.DiagnosticsDirectory);
        Record("startup", new { profile.RootPath, profile.AllowsSystemIntegration });
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(WindowLoaded));
    }

    internal static void RecordReady(bool handledArgument, bool suppressDefaultLaunch) =>
        Record("ready", new { handledArgument, suppressDefaultLaunch });

    internal static void RecordUnhandledException(string source, Exception exception)
    {
        Record("unhandled-exception", new
        {
            source,
            exceptionType = exception.GetType().FullName,
            exception.Message,
            exception.StackTrace
        });

        AutomationProfile? profile = _profile;
        if (profile is null)
            return;

        File.WriteAllText(
            profile.FailureSentinelPath,
            JsonSerializer.Serialize(new
            {
                timestampUtc = DateTimeOffset.UtcNow,
                source,
                exceptionType = exception.GetType().FullName,
                exception.Message
            }));
    }

    internal static void Record(string eventName, object? details = null)
    {
        AutomationProfile? profile = _profile;
        if (profile is null)
            return;

        string eventJson = JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            eventName,
            details
        });

        lock (SyncRoot)
        {
            using FileStream stream = new(profile.DiagnosticsLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using StreamWriter writer = new(stream);
            writer.WriteLine(eventJson);
        }
    }

    private static void WindowLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not Window window)
            return;

        lock (SyncRoot)
        {
            if (!TrackedWindows.Add(window))
                return;
        }

        Record("window-loaded", Describe(window));
        window.Activated += WindowActivated;
        window.Deactivated += WindowDeactivated;
        window.Closed += WindowClosed;
    }

    private static void WindowActivated(object? sender, EventArgs args)
    {
        if (sender is Window window)
            Record("window-activated", Describe(window));
    }

    private static void WindowDeactivated(object? sender, EventArgs args)
    {
        if (sender is Window window)
            Record("window-deactivated", Describe(window));
    }

    private static void WindowClosed(object? sender, EventArgs args)
    {
        if (sender is not Window window)
            return;

        window.Activated -= WindowActivated;
        window.Deactivated -= WindowDeactivated;
        window.Closed -= WindowClosed;
        lock (SyncRoot)
            TrackedWindows.Remove(window);

        Record("window-closed", Describe(window));
    }

    private static object Describe(Window window) => new
    {
        windowType = window.GetType().FullName,
        window.Title,
        window.IsVisible,
        window.Left,
        window.Top,
        window.Width,
        window.Height
    };
}
