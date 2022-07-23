using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace Text_Grab.Utilities;

internal class ImplementAppOptions
{
    public static async Task ImplementStartupOption(bool startupOption)
    {
        if (startupOption == true)
            await SetForStartup();
        else
            RemoveFromStartup();
    }

    public static void ImplementBackgroundOption(bool backgroundOption)
    {
        if (backgroundOption == true)
        {
            // Get strongly-typed current application
            NotifyIconUtilities.SetupNotifyIcon();
        }
        else
        {
            App app = (App)App.Current;
            if (app.TextGrabIcon != null)
            {
                app.TextGrabIcon.Dispose();
                app.TextGrabIcon = null;
            }
        }
    }

    internal static bool IsPackaged()
    {
        try
        {
            // If we have a package ID then we are running in a packaged context
            var dummy = Package.Current.Id;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async void RemoveFromStartup()
    {
        if (IsPackaged())
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
        if (IsPackaged())
        {
            StartupTask startupTask = await StartupTask.GetAsync("StartTextGrab");
            StartupTaskState newState = await startupTask.RequestEnableAsync();
        }
        else
        {
            string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            string? BaseDir = System.IO.Path.GetDirectoryName(System.AppContext.BaseDirectory);
            RegistryKey? key = Registry.CurrentUser.OpenSubKey(path, true);
            if (key is not null
                && BaseDir is not null)
            {
                key.SetValue("Text-Grab", $"\"{BaseDir}\\Text-Grab.exe\"");
            }
        }
        await Task.CompletedTask;
    }
}
