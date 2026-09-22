using Text_Grab.Properties;
using Text_Grab.Services;

namespace Text_Grab.Utilities;
internal class AppUtilities
{
    internal static bool IsPackaged() => PackageIdentity.IsPackaged();

    internal static SettingsService TextGrabSettingsService => Singleton<SettingsService>.Instance;

    internal static Settings TextGrabSettings => TextGrabSettingsService.ClassicSettings;

    /// <summary>
    /// Whether look-alike Greek and Cyrillic characters should be mapped to Latin.
    /// Honors the CorrectToLatin setting and only applies when the current input language is
    /// Latin-based, so the mapping never mangles text the user actually types in that script.
    /// </summary>
    internal static bool ShouldCorrectToLatin()
        => TextGrabSettings is Settings settings
            && settings.CorrectToLatin
            && LanguageUtilities.IsCurrentLanguageLatinBased();

    internal static string GetAppVersion() => PackageIdentity.GetAppVersion();
}
