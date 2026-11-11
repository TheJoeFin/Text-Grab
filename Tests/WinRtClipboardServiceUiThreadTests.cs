using System.Windows.Threading;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Tests;

/// <summary>
/// <see cref="WinRtClipboardService"/> (v5 F4) where a WPF UI thread exists. Its headless paths
/// are covered in Tests.Core.Windows.
///
/// In the "Settings isolation" collection because one test swaps the process-global
/// <see cref="UiThreadAccess"/> poster; it restores the app's own through its module initializer.
/// </summary>
[Collection("Settings isolation")]
public class WinRtClipboardServiceUiThreadTests
{
    [WpfFact]
    public async Task CallerOffTheUiThread_IsMarshalledOntoIt()
    {
        Dispatcher ui = Dispatcher.CurrentDispatcher;
        int uiThreadId = Environment.CurrentManagedThreadId;
        int? ranOn = null;
        using WinRtClipboardService clipboard = new(
            () =>
            {
                ranOn = Environment.CurrentManagedThreadId;
                return "from the ui thread";
            },
            _ => { },
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(1));

        try
        {
            UiThreadAccess.SetPoster(action => ui.InvokeAsync(action));

            string? text = await Task.Run(() => clipboard.GetTextAsync());

            Assert.Equal("from the ui thread", text);
            Assert.Equal(uiThreadId, ranOn);
            Assert.Null(clipboard.WorkerThreadId);
        }
        finally
        {
            UiThreadAccessInitializer.Initialize();
        }
    }

    [WpfFact]
    public async Task RealClipboard_RoundTripsOnTheUiThread()
    {
        using WinRtClipboardService clipboard = new();

        string? previous = await clipboard.GetTextAsync();
        string marker = $"Text Grab UI clipboard test {Guid.NewGuid():N}";
        try
        {
            await clipboard.SetTextAsync(marker);

            Assert.Equal(marker, await clipboard.GetTextAsync());
            // The UI thread is STA, so nothing was handed to the service's own thread.
            Assert.Null(clipboard.WorkerThreadId);
            Assert.Equal(marker, System.Windows.Clipboard.GetText());
        }
        finally
        {
            if (previous is not null)
                await clipboard.SetTextAsync(previous);
        }
    }
}
