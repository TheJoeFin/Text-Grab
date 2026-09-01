using System;

namespace Text_Grab.Services;

/// <summary>
/// How portable code reads the user's current keyboard input language.
///
/// The same delegate-resolver shape as <see cref="SettingsAccess"/> and
/// <see cref="UiThreadAccess"/>. WPF's <c>System.Windows.Input.InputLanguageManager</c> is the
/// only source for this and lives in PresentationCore, which Core cannot see; it also throws
/// <see cref="NullReferenceException"/> from its own internals in some hosts, so the app's
/// resolver owns that catch and simply returns null.
///
/// Null - whether because nothing is registered or because the host has no input language - is a
/// normal answer, not an error. <c>LanguageService</c> falls back to
/// <c>CultureInfo.CurrentUICulture</c> and then to en-US, exactly as it did when it read
/// InputLanguageManager directly.
/// </summary>
public static class InputLanguageAccess
{
    private static Func<string?>? _resolver;

    /// <summary>
    /// Registers the source of the current input-language tag. The app calls this from a module
    /// initializer. Tests may call it again to substitute a fake.
    /// </summary>
    public static void SetResolver(Func<string?> resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>Drops the registered resolver. For tests that need to restore a clean slate.</summary>
    public static void ClearResolver() => _resolver = null;

    /// <summary>Whether a resolver has been registered.</summary>
    public static bool IsConfigured => _resolver is not null;

    /// <summary>
    /// The current input-language tag, or null when there is no resolver or no input language.
    /// </summary>
    public static string? CurrentTag => _resolver?.Invoke();
}
