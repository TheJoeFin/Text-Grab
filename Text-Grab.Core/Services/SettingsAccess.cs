using System;
using Text_Grab.Interfaces;

namespace Text_Grab.Services;

/// <summary>
/// How portable code reaches user settings.
///
/// Core cannot call <c>AppUtilities.TextGrabSettings</c> - that lives in the app assembly and
/// returns an internal type. Instead the app registers a resolver at module load and Core reads
/// through <see cref="Current"/>.
///
/// The resolver is a delegate rather than a stored instance on purpose: the app's settings object
/// hangs off <c>Singleton&lt;SettingsService&gt;.Instance</c>, which is lazy and does real work on
/// first touch (reads user.config, seeds automation profiles, loads JSON sidecars). Registering a
/// delegate keeps module initialization free of that; the settings object is still built on first
/// read, exactly as it is today.
/// </summary>
public static class SettingsAccess
{
    private static Func<ITextGrabSettings>? _resolver;

    /// <summary>
    /// Registers the source of settings. The app calls this from a module initializer, so it is
    /// in place for any entry point into the app assembly - including the test host, which never
    /// runs App.appStartup. Tests may call it again to substitute a fake.
    /// </summary>
    public static void SetResolver(Func<ITextGrabSettings> resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>Drops the registered resolver. For tests that need to restore a clean slate.</summary>
    public static void ClearResolver() => _resolver = null;

    /// <summary>Whether a resolver has been registered.</summary>
    public static bool IsConfigured => _resolver is not null;

    /// <summary>
    /// The active settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No resolver was registered. In the app this cannot happen - the module initializer covers
    /// it. It means Core code is running with neither the app assembly loaded nor a test fake
    /// installed, so the caller has to supply one.
    /// </exception>
    public static ITextGrabSettings Current
        => _resolver?.Invoke()
            ?? throw new InvalidOperationException(
                $"No settings resolver registered. Call {nameof(SettingsAccess)}.{nameof(SetResolver)} " +
                "before reading settings from Core code.");
}
