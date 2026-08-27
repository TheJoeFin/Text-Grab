using Windows.ApplicationModel;

namespace Text_Grab.Utilities;

/// <summary>
/// Packaging identity checks that need only <see cref="Package"/>. Split out of
/// Text-Grab/Utilities/AppUtilities.cs so this piece can live in Core.Windows while
/// TextGrabSettings/TextGrabSettingsService stay in the app; AppUtilities.IsPackaged()
/// and GetAppVersion() forward here.
/// </summary>
public static class PackageIdentity
{
    public static bool IsPackaged()
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

    public static string GetAppVersion()
    {
        if (IsPackaged())
        {
            PackageVersion version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}" ?? "unknown error reading package version";
        }

        // Deliberately the ENTRY assembly, not the executing one. This method used to live in
        // Text-Grab/Utilities/AppUtilities.cs, where "executing" meant Text-Grab.exe and its
        // <Version> property. From here, "executing" would mean Text-Grab.Core.Windows, which
        // carries no version and would report 1.0.0.0 to the settings page and diagnostics.
        System.Reflection.Assembly versionSource =
            System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetExecutingAssembly();

        return versionSource.GetName().Version?.ToString() ?? "unknown error reading assembly version";
    }
}
