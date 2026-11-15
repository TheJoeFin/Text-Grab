using System;
using System.IO;
using Text_Grab.Interfaces;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>Which Tesseract engine Text Grab runs.</summary>
public enum TesseractEngineMode
{
    /// <summary>Not decided yet; resolved once by <see cref="TesseractEngineModeResolver"/>.</summary>
    Undecided,

    /// <summary>The in-process Tesseract that ships with Text Grab (new users).</summary>
    Bundled,

    /// <summary>A user-installed tesseract.exe driven through the command line (existing setups).</summary>
    External,
}

/// <summary>
/// Makes the one-time Bundled/External decision and persists it.
///
/// The decision deliberately reads only the saved <c>TesseractPath</c> setting. It must never run
/// the install probe in <c>ExternalTesseractBackend</c>, which looks in the usual install folders and
/// writes what it finds into <c>TesseractPath</c>: that would classify every user who merely has
/// Tesseract installed as an existing user.
/// </summary>
public static class TesseractEngineModeResolver
{
    public static TesseractEngineMode Parse(string? value) =>
        Enum.TryParse(value, true, out TesseractEngineMode mode) && mode != TesseractEngineMode.Undecided
            ? mode
            : TesseractEngineMode.Undecided;

    /// <summary>The persisted mode, deciding and saving it first if this is the first look.</summary>
    public static TesseractEngineMode Current => EnsureDecided(SettingsAccess.Current);

    public static TesseractEngineMode EnsureDecided(ITextGrabSettings settings, Func<string, bool>? fileExists = null)
    {
        TesseractEngineMode saved = Parse(settings.TesseractEngineMode);
        if (saved != TesseractEngineMode.Undecided)
            return saved;

        fileExists ??= File.Exists;
        bool hasUsableExternalPath = !string.IsNullOrWhiteSpace(settings.TesseractPath)
            && fileExists(settings.TesseractPath);

        // Automation/UI-test profiles are isolated from the real user's setup.
        TesseractEngineMode decided = hasUsableExternalPath && AutomationProfile.Current is null
            ? TesseractEngineMode.External
            : TesseractEngineMode.Bundled;

        Set(settings, decided);
        return decided;
    }

    public static void Set(ITextGrabSettings settings, TesseractEngineMode mode)
    {
        settings.TesseractEngineMode = mode == TesseractEngineMode.Undecided ? string.Empty : mode.ToString();
        settings.Save();
    }

    /// <summary>
    /// Switching back to External is offered only while the saved path still points at a real file;
    /// a Bundled user who never had an external install can never reach it.
    /// </summary>
    public static bool CanSwitchToExternal(ITextGrabSettings settings, Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        return !string.IsNullOrWhiteSpace(settings.TesseractPath) && fileExists(settings.TesseractPath);
    }
}
