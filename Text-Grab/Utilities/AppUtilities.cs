using Text_Grab.Properties;
using Text_Grab.Services;
using Windows.ApplicationModel;

namespace Text_Grab.Utilities;
internal class AppUtilities
{
    internal static bool IsPackaged()
    {
        try
        {
            // If we have a package ID then we are running in a packaged context
            PackageId dummy = Package.Current.Id;
            return true;
        }
        catch
        {
            return false;
        }
    }

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

    internal static string GetAppVersion()
    {
        if (IsPackaged())
        {
            PackageVersion version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}" ?? "unknown error reading package version";
        }

        
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown error reading assembly version";
    }
}
