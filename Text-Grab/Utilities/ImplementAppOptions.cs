using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace Text_Grab.Utilities;

internal class ImplementAppOptions
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".ico"];

    public static async Task ImplementStartupOption(bool startupOnLogin)
    {
        if (startupOnLogin)
            await SetForStartup();
        else
            RemoveFromStartup();
    }

    public static void ImplementBackgroundOption(bool runInBackground)
    {
        if (runInBackground)
        {
            NotifyIconUtilities.SetupNotifyIcon();
        }
        else
        {
            App app = (App)App.Current;
            app.TextGrabIcon?.Close();
            app.TextGrabIcon = null;
        }
    }

    public static void RegisterAsImageOpenWithApp()
    {
        if (AppUtilities.IsPackaged())
            return; // Packaged apps use the appxmanifest for file associations

        string executablePath = FileUtilities.GetExePath();
        if (string.IsNullOrEmpty(executablePath))
            return;

        try
        {
            // Register the application in the App Paths registry
            string appKey = @"SOFTWARE\Classes\Text-Grab.Image";
            using (RegistryKey? key = Registry.CurrentUser.CreateSubKey(appKey))
            {
                if (key is null)
                    return;

                key.SetValue("", "Text Grab - Image OCR");
                key.SetValue("FriendlyTypeName", "Text Grab Image");

                using RegistryKey? shellKey = key.CreateSubKey(@"shell\open\command");
                shellKey?.SetValue("", $"\"{executablePath}\" \"%1\"");

                using RegistryKey? iconKey = key.CreateSubKey("DefaultIcon");
                iconKey?.SetValue("", $"\"{executablePath}\",0");
            }

            // Register Text Grab in OpenWithProgids for each image extension
            foreach (string ext in ImageExtensions)
            {
                string extKey = $@"SOFTWARE\Classes\{ext}\OpenWithProgids";
                using RegistryKey? key = Registry.CurrentUser.CreateSubKey(extKey);
                key?.SetValue("Text-Grab.Image", Array.Empty<byte>(), RegistryValueKind.None);
            }

            // Register in the Applications key so Windows recognizes it
            string appRegKey = @"SOFTWARE\Classes\Applications\Text-Grab.exe";
            using (RegistryKey? key = Registry.CurrentUser.CreateSubKey(appRegKey))
            {
                if (key is null)
                    return;

                key.SetValue("FriendlyAppName", "Text Grab");

                using RegistryKey? supportedTypes = key.CreateSubKey("SupportedTypes");
                if (supportedTypes is not null)
                {
                    foreach (string ext in ImageExtensions)
                        supportedTypes.SetValue(ext, "");
                }

                using RegistryKey? shellKey = key.CreateSubKey(@"shell\open\command");
                shellKey?.SetValue("", $"\"{executablePath}\" \"%1\"");
            }

            // Notify the shell of the change
            NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register file associations: {ex.Message}");
        }
    }

    public static void UnregisterAsImageOpenWithApp()
    {
        if (AppUtilities.IsPackaged())
            return;

        try
        {
            // Remove the ProgId
            Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\Classes\Text-Grab.Image", false);

            // Remove OpenWithProgids entries for each extension
            foreach (string ext in ImageExtensions)
            {
                string extKey = $@"SOFTWARE\Classes\{ext}\OpenWithProgids";
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(extKey, true);
                if (key is not null)
                {
                    try { key.DeleteValue("Text-Grab.Image", false); }
                    catch (Exception) { }
                }
            }

            // Remove the Applications key
            Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\Classes\Applications\Text-Grab.exe", false);

            NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unregister file associations: {ex.Message}");
        }
    }

    private static async void RemoveFromStartup()
    {
        if (AppUtilities.IsPackaged())
        {
            StartupTask startupTask = await StartupTask.GetAsync("StartTextGrab");
            startupTask.Disable();
        }
        else
        {
            string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            RegistryKey? key = Registry.CurrentUser.OpenSubKey(path, true);
            if (key is not null)
            {
                try { key.DeleteValue("Text-Grab"); }
                catch (Exception) { }
            }
        }
    }

    private static async Task SetForStartup()
    {
        if (AppUtilities.IsPackaged())
        {
            StartupTask startupTask = await StartupTask.GetAsync("StartTextGrab");
            StartupTaskState newState = await startupTask.RequestEnableAsync();
        }
        else
        {
            string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            string executablePath = FileUtilities.GetExePath();

            RegistryKey? key = Registry.CurrentUser.OpenSubKey(path, true);
            if (key is not null && !string.IsNullOrEmpty(executablePath))
            {
                key.SetValue("Text-Grab", $"\"{executablePath}\"");
            }
        }
        await Task.CompletedTask;
    }
}
