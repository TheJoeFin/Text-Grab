using System.Runtime.CompilerServices;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>
/// Points Text-Grab.Core's <see cref="SettingsAccess"/> at the app's real settings object.
/// </summary>
internal static class SettingsAccessInitializer
{
    /// <summary>
    /// Runs when the Text-Grab assembly loads, before any of its code executes. A module
    /// initializer rather than a call in App.appStartup because the Tests host loads this
    /// assembly and exercises its code without ever raising the WPF Startup event - wiring it
    /// here means both paths are covered by construction.
    ///
    /// This only stores the delegate. AppUtilities.TextGrabSettings still resolves lazily on
    /// first read, so nothing forces SettingsService to be built at load time.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
        => SettingsAccess.SetResolver(static () => AppUtilities.TextGrabSettings);
}
