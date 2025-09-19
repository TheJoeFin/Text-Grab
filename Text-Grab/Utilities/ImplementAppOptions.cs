using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace Text_Grab.Utilities;

internal class ImplementAppOptions
{
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
            if (app.TextGrabIcon != null)
            {
                app.TextGrabIcon.Close();
                app.TextGrabIcon = null;
            }
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
