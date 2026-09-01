using System.Runtime.CompilerServices;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>
/// Points Text-Grab.Core's <see cref="TtsEngineAccess"/> at the app's WinRT speech engine.
/// </summary>
internal static class TtsEngineAccessInitializer
{
    /// <summary>
    /// Same reasoning as <see cref="SettingsAccessInitializer"/>: a module initializer rather
    /// than a call in App.appStartup, so the Tests host is covered too.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
        => TtsEngineAccess.SetResolver(static () => new WindowsSpeechEngine());
}
