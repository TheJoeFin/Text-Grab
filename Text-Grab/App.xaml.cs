using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using RegistryUtils;
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
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls.Window;

namespace Text_Grab;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    #region Properties

    public List<int> HotKeyIds { get; set; } = new();
    public int NumberOfRunningInstances { get; set; } = 0;
    public NotifyIcon? TextGrabIcon { get; set; }
    #endregion Properties

    #region Methods

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
    public static void SetTheme(object? sender = null, EventArgs? e = null)
    {
        bool gotTheme = Enum.TryParse<AppTheme>(Settings.Default.AppTheme.ToString(), true, out AppTheme currentAppTheme);

        if (!gotTheme)
            return;

        try
        {
            switch (currentAppTheme)
            {
                case AppTheme.System:
                    if (SystemThemeUtility.IsLightTheme())
                        Theme.Apply(ThemeType.Light, WindowBackdropType.None);
                    else
                        Theme.Apply(ThemeType.Dark, WindowBackdropType.None);
                    break;
                case AppTheme.Dark:
                    Theme.Apply(ThemeType.Dark, WindowBackdropType.None);
                    break;
                case AppTheme.Light:
                    Theme.Apply(ThemeType.Light, WindowBackdropType.None);
                    break;
                default:
                    Theme.Apply(ThemeType.Dark, WindowBackdropType.None);
                    break;
            }
        }
        catch (Exception)
        {
#if DEBUG
            throw;
#endif
        }
    }

    public void WatchTheme()
    {
        if (Registry.CurrentUser.OpenSubKey(SystemThemeUtility.themeKeyPath) is not RegistryKey key)
            return;

        RegistryMonitor monitor = new(key);
        monitor.RegChanged += new EventHandler(SetTheme);
        monitor.Start();
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

    private static void ShowAndSetFirstRun()
    {
        FirstRunWindow frw = new();
        frw.Show();

        Settings.Default.FirstRun = false;
        Settings.Default.Save();
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

    private void appExit(object sender, ExitEventArgs e)
    {
        TextGrabIcon?.Dispose();
    }

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

        WatchTheme();

        if (handledArgument)
        {
            // arguments were passed, so don't show firstRun dialog
            Settings.Default.FirstRun = false;
            Settings.Default.Save();
            return;
        }

        if (Settings.Default.FirstRun)
        {
            Settings.Default.CorrectToLatin = LanguageUtilities.IsCurrentLanguageLatinBased();
            ShowAndSetFirstRun();
            return;
        }

        DefaultLaunch();
    }

    private void CurrentDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // unhandled exceptions thrown from UI thread
        Debug.WriteLine($"Unhandled exception: {e.Exception}");
        e.Handled = true;
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
    #endregion Methods
}
