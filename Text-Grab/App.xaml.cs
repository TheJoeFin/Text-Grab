using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public NotifyIcon? TextGrabIcon { get; set; }

    public List<int> HotKeyIds { get; set; } = new();

    public int NumberOfRunningInstances { get; set; } = 0;

    async void appStartup(object sender, StartupEventArgs e)
    {
        NumberOfRunningInstances = Process.GetProcessesByName("Text-Grab").Length;
        Current.DispatcherUnhandledException += CurrentDispatcherUnhandledException;

        // Register COM server and activator type
        bool handledArgument = false;

        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            LaunchFromToast(toastArgs);
        };

        handledArgument = HandleNotifyIcon();

        if (!handledArgument && e.Args.Length > 0)
            handledArgument = await HandleStartupArgs(e.Args);

        if (handledArgument)
            return;

        if (Settings.Default.FirstRun)
        {
            Settings.Default.CorrectToLatin = LanguageUtilities.IsCurrentLanguageLatinBased();
            ShowAndSetFirstRun();
            return;
        }

        DefaultLaunch();
    }

    private static async Task<bool> HandleStartupArgs(string[] args)
    {
        string currentArgument = args[0];

        if (currentArgument.Contains("ToastActivated"))
        {
            Debug.WriteLine("Launched from toast");
            return true;
        }
        else if (currentArgument == "Settings")
        {
            SettingsWindow sw = new();
            sw.Show();
            return true;
        }

        bool isStandardMode = Enum.TryParse<DefaultLaunchSetting>(currentArgument, true, out DefaultLaunchSetting launchMode);

        if (isStandardMode)
        {
            LaunchStandardMode(launchMode);
            return true;
        }

        bool openedFile = TryToOpenFile(currentArgument);
        if (openedFile)
            return true;

        return await CheckForOcringFolder(currentArgument);
    }

    private static bool TryToOpenFile(string possiblePath)
    {
        if (!File.Exists(possiblePath))
            return false;

        EditTextWindow manipulateTextWindow = new();
        manipulateTextWindow.OpenThisPath(possiblePath);
        manipulateTextWindow.Show();
        return true;
    }

    private static async Task<bool> CheckForOcringFolder(string currentArgument)
    {
        if (!Directory.Exists(currentArgument))
            return false;

        EditTextWindow manipulateTextWindow = new();
        manipulateTextWindow.Show();
        await manipulateTextWindow.OcrAllImagesInFolder(currentArgument, false, false);
        return true;
    }

    private static void LaunchStandardMode(DefaultLaunchSetting launchMode)
    {
        switch (launchMode)
        {
            case DefaultLaunchSetting.EditText:
                EditTextWindow manipulateTextWindow = new();
                manipulateTextWindow.Show();
                break;
            case DefaultLaunchSetting.GrabFrame:
                GrabFrame gf = new();
                gf.Show();
                break;
            case DefaultLaunchSetting.Fullscreen:
                WindowUtilities.LaunchFullScreenGrab();
                break;
            case DefaultLaunchSetting.QuickLookup:
                QuickSimpleLookup qsl = new();
                qsl.Show();
                break;
            default:
                break;
        }
    }

    private bool HandleNotifyIcon()
    {
        if (Settings.Default.RunInTheBackground && NumberOfRunningInstances < 2)
        {
            NotifyIconUtilities.SetupNotifyIcon();

            if (Settings.Default.StartupOnLogin)
                return true;
        }

        return false;
    }

    private void LaunchFromToast(ToastNotificationActivatedEventArgsCompat toastArgs)
    {
        string argsInvoked = toastArgs.Argument;
        if (String.IsNullOrWhiteSpace(argsInvoked))
            return;

        // Need to dispatch to UI thread if performing UI operations
        Dispatcher.BeginInvoke((Action)(() =>
        {
            EditTextWindow mtw = new(argsInvoked);
            mtw.Show();
        }));
    }

    private static void ShowAndSetFirstRun()
    {
        FirstRunWindow frw = new();
        frw.Show();

        Settings.Default.FirstRun = false;
        Settings.Default.Save();
    }

    public static void DefaultLaunch()
    {
        DefaultLaunchSetting defaultLaunchSetting = Enum.Parse<DefaultLaunchSetting>(Settings.Default.DefaultLaunch, true);

        switch (defaultLaunchSetting)
        {
            case DefaultLaunchSetting.Fullscreen:
                WindowUtilities.LaunchFullScreenGrab();
                break;
            case DefaultLaunchSetting.GrabFrame:
                GrabFrame gf = new();
                gf.Show();
                break;
            case DefaultLaunchSetting.EditText:
                EditTextWindow manipulateTextWindow = new();
                manipulateTextWindow.Show();
                break;
            case DefaultLaunchSetting.QuickLookup:
                QuickSimpleLookup quickSimpleLookup = new();
                quickSimpleLookup.Show();
                break;
            default:
                EditTextWindow editTextWindow = new();
                editTextWindow.Show();
                break;
        }
    }

    private void appExit(object sender, ExitEventArgs e)
    {
        TextGrabIcon?.Dispose();
    }

    private void CurrentDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // unhandled exceptions thrown from UI thread
        Debug.WriteLine($"Unhandled exception: {e.Exception}");
        e.Handled = true;
    }
}
