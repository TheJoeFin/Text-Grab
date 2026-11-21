using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Text_Grab.Utilities;

namespace Text_Grab;

/// <summary>
/// When the window is ready for typing, for UI tests and for measuring open time (v5 E4a step 9). Records
/// nothing outside an automation profile.
/// </summary>
public partial class EditTextWindow
{
    private readonly long constructedTimestamp = Stopwatch.GetTimestamp();

    /// <summary>
    /// Records <c>etw-ready</c> once the window has been rendered, has finished loading any file it was
    /// opened with, and the dispatcher has gone idle: the point at which its editor takes input. The event
    /// carries the milliseconds since the window was made, whether it uses the pane layout, the document's
    /// length, and the process's working set and private bytes. Called from <c>Window_Initialized</c>.
    /// </summary>
    private void RecordReadyWhenIdle()
    {
        if (!AutomationDiagnostics.IsEnabled)
            return;

        ContentRendered += Rendered;

        void Rendered(object? sender, EventArgs e)
        {
            ContentRendered -= Rendered;
            RecordOnceLoaded();
        }
    }

    private void RecordOnceLoaded()
    {
        if (!IsEnabled)
        {
            // A file is still loading (SetToLoading turns the window off until it is in).
            IsEnabledChanged += LoadingEnded;
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            using Process process = Process.GetCurrentProcess();
            AutomationDiagnostics.Record("etw-ready", new
            {
                elapsedMs = Stopwatch.GetElapsedTime(constructedTimestamp).TotalMilliseconds,
                paneLayout = usesPaneLayout,
                textLength = Session.Text.Length,
                workingSetMb = process.WorkingSet64 / 1048576.0,
                privateMb = process.PrivateMemorySize64 / 1048576.0,
            });
        }, DispatcherPriority.ApplicationIdle);

        void LoadingEnded(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not true)
                return;

            IsEnabledChanged -= LoadingEnded;
            RecordOnceLoaded();
        }
    }
}
