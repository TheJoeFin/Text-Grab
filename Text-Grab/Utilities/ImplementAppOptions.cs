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
            // Get strongly-typed current application
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
            
            // Use the correct executable path for both deployment types
            string executablePath = GetCorrectExecutablePath();
            
            RegistryKey? key = Registry.CurrentUser.OpenSubKey(path, true);
            if (key is not null && !string.IsNullOrEmpty(executablePath))
            {
                key.SetValue("Text-Grab", $"\"{executablePath}\"");
            }
        }
        await Task.CompletedTask;
    }

    private static string GetCorrectExecutablePath()
    {
        // For single-file self-contained apps, use the original executable location
        if (IsExtractedSingleFile())
        {
            // Try to get the original path from command line args or process info
            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && System.IO.File.Exists(processPath))
            {
                return processPath;
            }
        }
        
        // For framework-dependent apps, use the base directory approach
        string? baseDir = System.IO.Path.GetDirectoryName(System.AppContext.BaseDirectory);
        if (!string.IsNullOrEmpty(baseDir))
        {
            string exePath = System.IO.Path.Combine(baseDir, "Text-Grab.exe");
            if (System.IO.File.Exists(exePath))
            {
                return exePath;
            }
        }
        
        // Fallback to process path
        return Environment.ProcessPath ?? "";
    }

    private static bool IsExtractedSingleFile()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR"));
    }
}
