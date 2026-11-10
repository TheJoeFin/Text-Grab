using System.Runtime.CompilerServices;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>
/// Points Text-Grab.Core's <see cref="ServiceAccess"/> at the app's container.
/// </summary>
internal static class ServiceAccessInitializer
{
    /// <summary>
    /// Runs when the Text-Grab assembly loads, so the Tests host (which never raises the WPF
    /// Startup event) is covered the same way as the app.
    ///
    /// This only stores the delegate. It must not build the container or construct any service:
    /// module initializers also run during test discovery and design-time builds. The container is
    /// built on the first resolve, by <see cref="AppServices.Provider"/>.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
        => ServiceAccess.SetProvider(static () => AppServices.Provider);
}
