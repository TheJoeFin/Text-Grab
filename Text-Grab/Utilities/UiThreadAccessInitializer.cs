using System.Runtime.CompilerServices;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>
/// Points Text-Grab.Core's <see cref="UiThreadAccess"/> at the WPF dispatcher.
/// </summary>
internal static class UiThreadAccessInitializer
{
    /// <summary>
    /// Same reasoning as <see cref="SettingsAccessInitializer"/>: a module initializer rather
    /// than a call in App.appStartup, so the Tests host is covered too.
    ///
    /// <c>Application.Current</c> is read inside the delegate, not here - at module-load time it
    /// is still null. When it is null at call time the post is simply dropped, which is what the
    /// code this replaced did with its <c>dispatcher is null</c> check.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
        => UiThreadAccess.SetPoster(static action =>
        {
            System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
            _ = dispatcher?.InvokeAsync(action);
        });
}
