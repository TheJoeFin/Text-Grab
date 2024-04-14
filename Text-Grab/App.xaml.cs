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
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace Text_Grab;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    #region Fields

    readonly static Settings _defaultSettings = AppUtilities.TextGrabSettings;

    #endregion Fields

    #region Properties

    public List<int> HotKeyIds { get; set; } = new();
    public int NumberOfRunningInstances { get; set; } = 0;
    public NotifyIcon? TextGrabIcon { get; set; }
    #endregion Properties

    #region Methods

    public static void DefaultLaunch()
    {
        TextGrabMode defaultLaunchSetting = Enum.Parse<TextGrabMode>(_defaultSettings.DefaultLaunch, true);

        switch (defaultLaunchSetting)
        {
            case TextGrabMode.Fullscreen:
                WindowUtilities.LaunchFullScreenGrab();
                break;
            case TextGrabMode.GrabFrame:
                GrabFrame gf = new();
                gf.Show();
                break;
            case TextGrabMode.EditText:
                EditTextWindow manipulateTextWindow = new();
                manipulateTextWindow.Show();
                break;
            case TextGrabMode.QuickLookup:
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
        bool gotTheme = Enum.TryParse(_defaultSettings.AppTheme.ToString(), true, out AppTheme currentAppTheme);

        if (!gotTheme)
            return;

        ThemeService themeService = new();
        try
        {
            switch (currentAppTheme)
            {
                case AppTheme.System:
                    if (SystemThemeUtility.IsLightTheme())
                        themeService.SetTheme(ApplicationTheme.Light);
                    else
                        themeService.SetTheme(ApplicationTheme.Dark);
                    break;
                case AppTheme.Dark:
                    themeService.SetTheme(ApplicationTheme.Dark);
                    break;
                case AppTheme.Light:
                    themeService.SetTheme(ApplicationTheme.Light);
                    break;
                default:
                    themeService.SetTheme(ApplicationTheme.Dark);
                    break;
            }
        }
        catch (Exception)
        {
#if DEBUG
            throw;
#endif
        }

        Color teal = (Color)ColorConverter.ConvertFromString("#308E98");
        ApplicationAccentColorManager.Apply(teal);
    }

    public static void WatchTheme()
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
        OcrDirectoryOptions options = new();
        await manipulateTextWindow.OcrAllImagesInFolder(currentArgument, options);
        return true;
    }

    private static async Task<bool> HandleStartupArgs(string[] args)
    {
        string currentArgument = args[0];

        bool isQuiet = false;

        foreach (string arg in args)
            if (arg == "--windowless")
            {
                isQuiet = true;
                _defaultSettings.FirstRun = false;
                _defaultSettings.Save();
            }

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

        bool isStandardMode = Enum.TryParse(currentArgument, true, out TextGrabMode launchMode);

        if (isStandardMode)
        {
            LaunchStandardMode(launchMode);
            return true;
        }

        bool openedFile = await TryToOpenFile(currentArgument, isQuiet);
        if (openedFile)
            return true;

        return await CheckForOcringFolder(currentArgument);
    }

    private static void LaunchStandardMode(TextGrabMode launchMode)
    {
        switch (launchMode)
        {
            case TextGrabMode.EditText:
                EditTextWindow manipulateTextWindow = new();
                manipulateTextWindow.Show();
                break;
            case TextGrabMode.GrabFrame:
                GrabFrame gf = new();
                gf.Show();
                break;
            case TextGrabMode.Fullscreen:
                WindowUtilities.LaunchFullScreenGrab();
                break;
            case TextGrabMode.QuickLookup:
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

        _defaultSettings.FirstRun = false;
        _defaultSettings.Save();
    }

    private static async Task<bool> TryToOpenFile(string possiblePath, bool isQuiet)
    {
        if (!File.Exists(possiblePath))
            return false;


        if (isQuiet)
        {
            (string pathContent, _) = await IoUtilities.GetContentFromPath(possiblePath);
            OutputUtilities.HandleTextFromOcr(
                pathContent,
                false,
                false);
        }
        else
        {
            EditTextWindow manipulateTextWindow = new();
            manipulateTextWindow.OpenPath(possiblePath);
            manipulateTextWindow.Show();
        }
        return true;
    }

    private void appExit(object sender, ExitEventArgs e)
    {
        TextGrabIcon?.Dispose();
        Singleton<HistoryService>.Instance.WriteHistory();
    }

    async void appStartup(object sender, StartupEventArgs e)
    {
        NumberOfRunningInstances = Process.GetProcessesByName("Text-Grab").Length;
        Current.DispatcherUnhandledException += CurrentDispatcherUnhandledException;

        // Register COM server and activator type
        bool handledArgument = false;

        await Singleton<HistoryService>.Instance.LoadHistories();

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
            _defaultSettings.FirstRun = false;
            _defaultSettings.Save();
            return;
        }

        if (_defaultSettings.FirstRun)
        {
            _defaultSettings.CorrectToLatin = LanguageUtilities.IsCurrentLanguageLatinBased();
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
        if (_defaultSettings.RunInTheBackground && NumberOfRunningInstances < 2)
        {
            NotifyIconUtilities.SetupNotifyIcon();

            if (_defaultSettings.StartupOnLogin)
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
        Dispatcher.BeginInvoke(() =>
        {
            EditTextWindow mtw = new(argsInvoked);
            mtw.Show();
        });
    }
    #endregion Methods
}
