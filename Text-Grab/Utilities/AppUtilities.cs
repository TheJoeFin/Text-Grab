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

    internal static Settings TextGrabSettings => Singleton<SettingsService>.Instance.ClassicSettings;

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
