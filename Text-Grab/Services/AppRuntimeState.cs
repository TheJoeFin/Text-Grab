using System.Collections.Generic;
using Text_Grab.Controls;

namespace Text_Grab.Services;

/// <summary>
/// Process-wide state that used to live on <see cref="App"/> and was reached through
/// <c>(App)App.Current</c> casts. A container singleton: resolve it with
/// <c>AppServices.Provider.GetRequiredService&lt;AppRuntimeState&gt;()</c>. <see cref="App"/>'s
/// properties of the same names forward here.
/// </summary>
internal sealed class AppRuntimeState
{
    /// <summary>IDs of the global hotkeys currently registered with <c>HotKeyManager</c>.</summary>
    public List<int> HotKeyIds { get; set; } = [];

    /// <summary>Text-Grab processes running when this one started, this one included.</summary>
    public int NumberOfRunningInstances { get; set; }

    /// <summary>The tray icon window, while the app is running in the background.</summary>
    public NotifyIconWindow? TextGrabIcon { get; set; }
}
