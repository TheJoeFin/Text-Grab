using System;
using Text_Grab.Interfaces;

namespace Text_Grab.Services;

/// <summary>
/// How <see cref="TtsService"/> gets its default speech engine.
///
/// The same delegate-resolver shape as <see cref="SettingsAccess"/>, <see cref="UiThreadAccess"/>
/// and <see cref="InputLanguageAccess"/>. <c>TtsService</c> used to construct its default engine
/// with a field initializer - <c>private ITtsEngine _engine = new WindowsSpeechEngine();</c> -
/// but <c>WindowsSpeechEngine</c> is WinRT-only and belongs in Core.Windows, which Core cannot
/// name. The app registers a factory at module load and <see cref="TtsService"/> calls
/// <see cref="CreateDefault"/> from its own constructor, so the engine is still built at the same
/// moment it always was: when a <c>TtsService</c> is constructed, not lazily on first
/// <c>Speak</c>.
/// </summary>
public static class TtsEngineAccess
{
    private static Func<ITtsEngine>? _resolver;

    /// <summary>
    /// Registers the factory for the default TTS engine. The app calls this from a module
    /// initializer, so it is in place for any entry point into the app assembly - including the
    /// test host. Tests may call it again to substitute a fake.
    /// </summary>
    public static void SetResolver(Func<ITtsEngine> resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>Drops the registered resolver. For tests that need to restore a clean slate.</summary>
    public static void ClearResolver() => _resolver = null;

    /// <summary>Whether a resolver has been registered.</summary>
    public static bool IsConfigured => _resolver is not null;

    /// <summary>
    /// Builds a new default engine.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No resolver was registered. In the app this cannot happen - the module initializer covers
    /// it. It means Core code is constructing a <see cref="TtsService"/> with neither the app
    /// assembly loaded nor a test fake installed, so the caller has to supply one.
    /// </exception>
    public static ITtsEngine CreateDefault()
        => _resolver is not null
            ? _resolver()
            : throw new InvalidOperationException(
                $"No TTS engine resolver registered. Call {nameof(TtsEngineAccess)}.{nameof(SetResolver)} " +
                "before constructing TtsService from Core code.");
}
