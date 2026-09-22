using System;
using System.IO;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>
/// Resolves where large, non-setting on-disk data (downloaded Whisper models, debug logs) should
/// live. Normally that is <c>%LocalAppData%\Text-Grab\...</c>, matching every existing install.
/// Under Fully Portable mode (unpackaged only - a packaged install cannot write beside its own
/// read-only install directory) it moves beside the executable instead, alongside history and
/// managed settings, so nothing is left behind in AppData.
/// </summary>
public static class PortableStorageUtilities
{
    /// <summary>
    /// Returns the directory for a given AppData subfolder (e.g. "WhisperModels", "Logs"),
    /// redirected next to the executable when Fully Portable mode is active.
    /// </summary>
    public static string GetDataDirectory(string subfolderName)
    {
        if (!PackageIdentity.IsPackaged() && SettingsAccess.IsConfigured && SettingsAccess.Current.FullyPortable)
            return GetPortableDataDirectory(subfolderName);

        return GetAppDataDataDirectory(subfolderName);
    }

    /// <summary>
    /// The beside-executable location a given AppData subfolder moves to under Fully Portable
    /// mode, regardless of whether that mode is currently active. Used by the portable-mode
    /// readiness checklist to find/move data ahead of actually enabling the setting.
    /// </summary>
    public static string GetPortableDataDirectory(string subfolderName)
    {
        string? exeDirectory = Path.GetDirectoryName(FileUtilities.GetExePath());
        return Path.Combine(string.IsNullOrEmpty(exeDirectory) ? "c:\\Text-Grab" : exeDirectory, subfolderName);
    }

    /// <summary>
    /// The normal AppData location for a given subfolder, regardless of whether Fully Portable
    /// mode is currently active. Used by the portable-mode readiness checklist to find data that
    /// still needs to move.
    /// </summary>
    public static string GetAppDataDataDirectory(string subfolderName)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Text-Grab",
            subfolderName);
    }
}
