using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace Text_Grab.Utilities;

/// <summary>
/// Registers per-user file associations for unpackaged installs. Packaged installs declare
/// their associations through the MSIX manifest instead. Currently registers the Grab Frame
/// file type (<c>.tggf</c>) so a saved Grab Frame can be reopened by double-clicking it.
/// </summary>
internal static class FileAssociationUtilities
{
    private const string GrabFrameProgId = "TextGrab.GrabFrame";
    private const string GrabFrameProgIdDescription = "Text Grab Frame";

    private const string ClassesRoot = @"Software\Classes\";
    private const string GrabFrameExtensionKeyPath = ClassesRoot + GrabFrameFileUtilities.GrabFrameFileExtension;
    private const string GrabFrameProgIdKeyPath = ClassesRoot + GrabFrameProgId;

    /// <summary>
    /// Registers the .tggf file association for the current user when running unpackaged.
    /// Safe to call on every startup; only writes when the registration is missing or stale.
    /// </summary>
    internal static void EnsureGrabFrameFileAssociation()
    {
        if (PackageIdentity.IsPackaged())
            return;

        string executablePath = FileUtilities.GetExePath();
        if (string.IsNullOrEmpty(executablePath))
            return;

        string expectedCommand = $"\"{executablePath}\" \"%1\"";

        try
        {
            using (RegistryKey? existingCommandKey =
                Registry.CurrentUser.OpenSubKey($@"{GrabFrameProgIdKeyPath}\shell\open\command"))
            using (RegistryKey? existingExtensionKey =
                Registry.CurrentUser.OpenSubKey(GrabFrameExtensionKeyPath))
            {
                if (existingCommandKey?.GetValue(string.Empty) as string == expectedCommand
                    && existingExtensionKey?.GetValue(string.Empty) as string == GrabFrameProgId)
                {
                    return;
                }
            }

            using (RegistryKey extensionKey = Registry.CurrentUser.CreateSubKey(GrabFrameExtensionKeyPath))
                extensionKey.SetValue(string.Empty, GrabFrameProgId);

            using RegistryKey progIdKey = Registry.CurrentUser.CreateSubKey(GrabFrameProgIdKeyPath);
            progIdKey.SetValue(string.Empty, GrabFrameProgIdDescription);

            using (RegistryKey iconKey = progIdKey.CreateSubKey("DefaultIcon"))
                iconKey.SetValue(string.Empty, $"\"{executablePath}\",0");

            using RegistryKey commandKey = progIdKey.CreateSubKey(@"shell\open\command");
            commandKey.SetValue(string.Empty, expectedCommand);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($".tggf file association registration failed: {ex.Message}");
        }
    }
}
