using System;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>
/// Points Text-Grab.Core's <see cref="InputLanguageAccess"/> at WPF's InputLanguageManager.
/// </summary>
internal static class InputLanguageAccessInitializer
{
    /// <summary>
    /// Same reasoning as <see cref="SettingsAccessInitializer"/>: a module initializer rather
    /// than a call in App.appStartup, so the Tests host is covered too.
    ///
    /// The NullReferenceException catch came with the code from LanguageService - the manager
    /// throws it from its own internals in some hosts - and stays on this side of the seam,
    /// because this is the only side that knows InputLanguageManager exists.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
        => InputLanguageAccess.SetResolver(static () =>
        {
            try
            {
                return InputLanguageManager.Current?.CurrentInputLanguage?.Name;
            }
            catch (NullReferenceException)
            {
                return null;
            }
        });
}
