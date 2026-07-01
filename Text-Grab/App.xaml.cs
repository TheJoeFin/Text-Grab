using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using RegistryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Text_Grab.Controls;
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
    internal readonly record struct StartupArguments(
        bool IsQuiet,
        bool OpenInGrabFrame,
        string? PrimaryArgument,
        string? GrabFramePath);

    #region Fields

    private static readonly Settings _defaultSettings = AppUtilities.TextGrabSettings;
    private static RegistryMonitor? _themeRegistryMonitor;
    private static RegistryKey? _themeRegistryKey;

    #endregion Fields

    #region Properties

    public List<int> HotKeyIds { get; set; } = [];
    public int NumberOfRunningInstances { get; set; } = 0;
    public NotifyIconWindow? TextGrabIcon { get; set; }
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
                manipulateTextWindow.Activate();
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

        SetTheme();
    }

    public static async Task OpenFileWithPickerAsync(bool isQuiet = false)
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = FileUtilities.GetOpenDocumentFilter(),
            Title = "Open File",
            CheckFileExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (openFileDialog.ShowDialog() == true)
            await TryToOpenFilePathAsync(openFileDialog.FileName, isQuiet);
    }

    public static DragDropEffects GetDroppedFileEffect(IDataObject? dataObject)
    {
        return GetDroppedFilePaths(dataObject).Any()
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    public static IReadOnlyList<string> GetDroppedFilePaths(IDataObject? dataObject)
    {
        if (dataObject is null || !dataObject.GetDataPresent(DataFormats.FileDrop, true))
            return [];


        if (dataObject.GetData(DataFormats.FileDrop, true) is not string[] paths || paths.Length == 0)
            return [];

        return [.. paths.Where(File.Exists)];
    }

    public static async Task<bool> TryToOpenDroppedFilesAsync(IDataObject? dataObject, bool isQuiet = false)
    {
        bool openedAny = false;

        foreach (string path in GetDroppedFilePaths(dataObject))
            openedAny = await TryToOpenFilePathAsync(path, isQuiet) || openedAny;

        return openedAny;
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

        // for now this is best but... not ideal
        ApplicationAccentColorManager.ApplySystemAccent();

        // TODO: try to apply the teal color again, maybe something in WPFUI is broken
        // Color teal = (Color)ColorConverter.ConvertFromString("#308E98");

        // SolidColorBrush tealBrush = new(teal);
        // themeService.SetAccent(tealBrush);

        // This does not work either
        // ApplicationAccentColorManager.Apply(teal);

        //ResourceReferenceExpressionConverter converter = new();
        //Dictionary<string, SolidColorBrush> brushes = new()
        //{
        //    ["SystemAccentColor"] = tealBrush,
        //    ["SystemAccentColorPrimary"] = tealBrush,
        //    ["SystemAccentColorSecondary"] = tealBrush,
        //    ["SystemAccentColorTertiary"] = tealBrush
        //};
        //Dictionary<string, SolidColorBrush> brushes = new()
        //{
        //    ["SystemAccentColor"] = ((Color)UiApplication.Current.Resources["SystemAccentColor"]).ToBrush(),
        //    ["SystemAccentColorPrimary"] = ((Color)UiApplication.Current.Resources["SystemAccentColorPrimary"]).ToBrush(),
        //    ["SystemAccentColorSecondary"] = ((Color)UiApplication.Current.Resources["SystemAccentColorSecondary"]).ToBrush(),
        //    ["SystemAccentColorTertiary"] = ((Color)UiApplication.Current.Resources["SystemAccentColorTertiary"]).ToBrush()
        //};
        //ResourceDictionary themeDictionary = UiApplication.Current.Resources.MergedDictionaries[0];
        //try
        //{
        //    foreach (DictionaryEntry entry in themeDictionary)
        //    {
        //        if (entry.Value is SolidColorBrush brush)
        //        {
        //            object dynamicColor = brush.ReadLocalValue(SolidColorBrush.ColorProperty);
        //            if (dynamicColor is not Color &&
        //                converter.ConvertTo(dynamicColor, typeof(MarkupExtension)) is DynamicResourceExtension dynamicResource &&
        //                brushes.ContainsKey((string)dynamicResource.ResourceKey))
        //            {
        //                themeDictionary[entry.Key] = brushes[(string)dynamicResource.ResourceKey];
        //            }
        //        }
        //    }
        //}
        //catch (Exception)
        //{
        //    Debug.WriteLine($"Failed to apply accent color");
        //}
    }

    public static void WatchTheme()
    {
        StopWatchingTheme();

        if (Registry.CurrentUser.OpenSubKey(SystemThemeUtility.themeKeyPath) is not RegistryKey key)
        {
            SetTheme();
            return;
        }

        _themeRegistryKey = key;
        _themeRegistryMonitor = new RegistryMonitor(key);
        _themeRegistryMonitor.RegChanged += new EventHandler(SetTheme);
        _themeRegistryMonitor.Start();
        SetTheme();
    }

    private static void StopWatchingTheme()
    {
        if (_themeRegistryMonitor is not null)
        {
            _themeRegistryMonitor.RegChanged -= new EventHandler(SetTheme);
            try { _themeRegistryMonitor.Dispose(); }
            catch (ObjectDisposedException) { }
            _themeRegistryMonitor = null;
        }

        _themeRegistryKey?.Dispose();
        _themeRegistryKey = null;
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

    internal static StartupArguments ParseStartupArguments(IEnumerable<string> args)
    {
        bool isQuiet = false;
        bool openInGrabFrame = false;
        string? primaryArgument = null;
        string? grabFramePath = null;

        foreach (string arg in args)
        {
            if (string.Equals(arg, "--windowless", StringComparison.OrdinalIgnoreCase))
            {
                isQuiet = true;
                continue;
            }

            if (string.Equals(arg, "--grabframe", StringComparison.OrdinalIgnoreCase))
            {
                openInGrabFrame = true;
                continue;
            }

            primaryArgument ??= arg;

            if (grabFramePath is not null)
                continue;

            try
            {
                string absolutePath = Path.GetFullPath(arg);
                if (File.Exists(absolutePath))
                    grabFramePath = absolutePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Invalid path argument: {arg}, error: {ex.Message}");
            }
        }

        return new StartupArguments(isQuiet, openInGrabFrame, primaryArgument, grabFramePath);
    }

    private static async Task<bool> HandleStartupArgs(string[] args)
    {
        // text-grab:// protocol activation (e.g. from the Text Grab browser
        // extension); short-circuit before any file-path probing of arguments.
        foreach (string arg in args)
            if (ProtocolUtilities.IsProtocolUri(arg))
                return HandleProtocolUri(arg);

        StartupArguments startupArguments = ParseStartupArguments(args);

        if (startupArguments.IsQuiet)
        {
            _defaultSettings.FirstRun = false;
            _defaultSettings.Save();
        }

        // Handle --grabframe flag: open the next argument (file path) in GrabFrame
        if (startupArguments.OpenInGrabFrame)
        {
            if (!string.IsNullOrEmpty(startupArguments.GrabFramePath))
            {
                GrabFrame gf = new(startupArguments.GrabFramePath);
                gf.Show();
                return true;
            }
            else
            {
                Debug.WriteLine("--grabframe flag specified but no valid image or PDF file path provided");
                // Fall through to default launch behavior
            }
        }

        if (string.IsNullOrWhiteSpace(startupArguments.PrimaryArgument))
            return false;

        string currentArgument = startupArguments.PrimaryArgument;

        if (currentArgument.Contains("ToastActivated", StringComparison.Ordinal))
        {
            Debug.WriteLine("Launched from toast");
            return true;
        }
        else if (string.Equals(currentArgument, "Settings", StringComparison.OrdinalIgnoreCase))
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

        bool openedFile = await TryToOpenFilePathAsync(currentArgument, startupArguments.IsQuiet);
        if (openedFile)
            return true;

        return await CheckForOcringFolder(currentArgument);
    }

    internal static bool HandleProtocolUri(string uriString)
    {
        if (!ProtocolUtilities.TryParseProtocolUri(uriString, out string command, out Dictionary<string, string> parameters))
            return false;

        switch (command)
        {
            case "paste-spreadsheet":
                {
                    EditTextWindow etw = new();
                    etw.Show();
                    etw.EnterSpreadsheetMode();
                    // Defer the paste until the window has loaded so the
                    // spreadsheet grid is ready to receive the clipboard table.
                    etw.Dispatcher.InvokeAsync(
                        etw.PasteClipboardIntoSpreadsheet,
                        DispatcherPriority.Loaded);
                    etw.Activate();
                    return true;
                }
            case "edit-text":
                {
                    EditTextWindow etw = new();
                    try
                    {
                        string clipboardText = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(clipboardText))
                            etw.AddThisText(clipboardText);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"edit-text protocol: clipboard read failed. {ex.Message}");
                    }
                    etw.Show();
                    etw.Activate();
                    return true;
                }
            case "grab-frame":
                {
                    // A path is optional. When present it is untrusted (the protocol can be
                    // launched by any web page), so it must pass the safe-path gate before we
                    // open the file; an unsafe path falls back to an empty Grab Frame.
                    if (parameters.TryGetValue("path", out string? path))
                    {
                        if (ProtocolUtilities.TryGetSafeProtocolFilePath(path, out string safePath))
                        {
                            GrabFrame gfWithFile = new(safePath);
                            gfWithFile.Show();
                            gfWithFile.Activate();
                            return true;
                        }

                        Debug.WriteLine("grab-frame protocol: rejected unsafe path; opening empty frame.");
                    }

                    GrabFrame gf = new();
                    gf.Show();
                    gf.Activate();
                    return true;
                }
            case "grab-text":
                {
                    // OCR a local image/PDF straight to the clipboard, no window. The path is
                    // untrusted; only proceed for a validated, allowed local file.
                    if (parameters.TryGetValue("path", out string? path)
                        && ProtocolUtilities.TryGetSafeProtocolFilePath(path, out string safePath))
                    {
                        _ = GrabTextFromFileAsync(safePath);
                        return true;
                    }

                    Debug.WriteLine("grab-text protocol: missing or unsafe path; ignoring.");
                    return false;
                }
            case "fullscreen":
                LaunchStandardMode(TextGrabMode.Fullscreen);
                return true;
            case "quick-lookup":
                LaunchStandardMode(TextGrabMode.QuickLookup);
                return true;
            case "settings":
                {
                    SettingsWindow sw = new();
                    sw.Show();
                    return true;
                }
            default:
                Debug.WriteLine($"Unknown text-grab:// command: {command}");
                return false;
        }
    }

    /// <summary>
    /// OCRs a local image/PDF file and routes the result to the clipboard (and
    /// a toast), the same handling as a normal grab. Used by text-grab://grab-text.
    /// </summary>
    private static async Task GrabTextFromFileAsync(string path)
    {
        try
        {
            string ocrText = await OcrUtilities.OcrAbsoluteFilePathAsync(
                path, LanguageUtilities.GetOCRLanguage());
            OutputUtilities.HandleTextFromOcr(ocrText, isSingleLine: false, isTable: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"grab-text protocol: OCR failed. {ex.Message}");
        }
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

    public static async Task<bool> TryToOpenFilePathAsync(string possiblePath, bool isQuiet = false)
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
        else if (IoUtilities.IsVisualDocumentFile(possiblePath))
        {
            GrabFrame gf = new(possiblePath);
            gf.Show();
            gf.Activate();
        }
        else
        {
            EditTextWindow manipulateTextWindow = new();
            manipulateTextWindow.OpenPath(possiblePath);
            manipulateTextWindow.Show();
            manipulateTextWindow.Activate();
        }
        return true;
    }

    private void appExit(object sender, ExitEventArgs e)
    {
        TextGrabIcon?.Close();

        NotifyIconUtilities.UnregisterHotkeys(this);
        HotKeyIds.Clear();

        StopWatchingTheme();

        HistoryService historyService = Singleton<HistoryService>.Instance;
        historyService.WriteHistory();
        historyService.Dispose();
    }

    private async void appStartup(object sender, StartupEventArgs e)
    {
        NumberOfRunningInstances = Process.GetProcessesByName("Text-Grab").Length;
        Current.DispatcherUnhandledException += CurrentDispatcherUnhandledException;

        // Per-user text-grab:// registration for unpackaged installs
        // (packaged installs register the protocol via the MSIX manifest).
        ProtocolUtilities.EnsureProtocolRegistration();

        // Register COM server and activator type
        bool handledArgument = false;

        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            LaunchFromToast(toastArgs);
        };

        handledArgument = HandleNotifyIcon();

        if (!handledArgument)
            handledArgument = await ShareTargetUtilities.HandleShareTargetActivationAsync();

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
