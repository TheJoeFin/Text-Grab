using Dapplo.Windows.User32;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Text_Grab.Extensions;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Windows.Media.Ocr;

namespace Text_Grab.Utilities;

public static class DiagnosticsUtilities
{
    public static async Task<string> GenerateBugReportAsync()
    {
        BugReportModel bugReport = new()
        {
            GeneratedAt = DateTimeOffset.Now,
            AppVersion = AppUtilities.GetAppVersion(),
            InstallationType = GetInstallationType(),
            WindowsVersion = GetWindowsVersion(),
            StartupDetails = GetStartupDetails(),
            SettingsInfo = GetSettingsInfo(),
            ManagedSettingsSummary = GetManagedSettingsSummary(),
            HistoryInfo = GetHistoryInfo(),
            LanguageInfo = GetLanguageInfo(),
            TesseractInfo = await GetTesseractInfoAsync(),
            Monitors = GetMonitorsInfo()
        };

        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(bugReport, options);
    }

    public static async Task<string> SaveBugReportToFileAsync()
    {
        string bugReportJson = await GenerateBugReportAsync();

        string fileName = $"TextGrab_BugReport_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string filePath = Path.Combine(documentsPath, fileName);

        await File.WriteAllTextAsync(filePath, bugReportJson);

        return filePath;
    }

    private static string GetInstallationType()
    {
        if (AppUtilities.IsPackaged())
            return "Packaged (Microsoft Store or sideloaded)";

        string baseDir = AppContext.BaseDirectory;
        bool hasCoreClr = File.Exists(Path.Combine(baseDir, "coreclr.dll"));
        bool hasHostFxr = File.Exists(Path.Combine(baseDir, "hostfxr.dll"));

        if (hasCoreClr && hasHostFxr)
            return "Self-contained executable";

        return "Framework-dependent executable";
    }

    private static string GetWindowsVersion()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is not null)
            {
                string? productName = key.GetValue("ProductName")?.ToString();
                string? displayVersion = key.GetValue("DisplayVersion")?.ToString();
                string? buildLabEx = key.GetValue("BuildLabEx")?.ToString();

                return $"{productName} {displayVersion} (Build: {buildLabEx})";
            }
        }
        catch (Exception ex)
        {
            return $"Unable to determine Windows version: {ex.Message}";
        }

        return $"Windows {Environment.OSVersion.Version}";
    }

    private static StartupDetailsModel GetStartupDetails()
    {
        StartupDetailsModel details = new()
        {
            IsPackaged = AppUtilities.IsPackaged(),
            // Sanitize: only include the executable filename, not the full path (which contains username)
            ExecutableFileName = Path.GetFileName(Environment.ProcessPath ?? "Text-Grab.exe")
        };

        if (AppUtilities.IsPackaged())
        {
            details.StartupMethod = "StartupTask API (packaged apps)";
            details.RegistryPath = "N/A (uses StartupTask)";
            details.RegistryValueStatus = "N/A (uses StartupTask)";
        }
        else
        {
            details.StartupMethod = "Registry Run key (unpackaged apps)";
            details.RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                string? actualValue = key?.GetValue("Text-Grab")?.ToString();
                string expectedExeName = Path.GetFileName(FileUtilities.GetExePath());

                if (actualValue is null)
                    details.RegistryValueStatus = "Not set";
                else if (Path.GetFileName(actualValue.Trim('"')) == expectedExeName)
                    details.RegistryValueStatus = "Configured correctly";
                else
                    details.RegistryValueStatus = "Mismatch (points to different executable)";
            }
            catch (Exception ex)
            {
                details.RegistryValueStatus = $"Error reading registry: {ex.Message}";
            }
        }

        return details;
    }

    private static SettingsInfoModel GetSettingsInfo()
    {
        Settings s = AppUtilities.TextGrabSettings;

        return new SettingsInfoModel
        {
            // Core behavior
            FirstRun = s.FirstRun,
            ShowToast = s.ShowToast,
            StartupOnLogin = s.StartupOnLogin,
            RunInBackground = s.RunInTheBackground,
            NeverAutoUseClipboard = s.NeverAutoUseClipboard,
            UseHistory = s.UseHistory,
            AddToContextMenu = s.AddToContextMenu,
            RegisterOpenWith = s.RegisterOpenWith,

            // Grab behavior
            DefaultLaunch = s.DefaultLaunch ?? "Unknown",
            TryInsert = s.TryInsert,
            InsertDelay = s.InsertDelay,
            CloseFrameOnGrab = s.CloseFrameOnGrab,
            PostGrabStayOpen = s.PostGrabStayOpen,

            // OCR / error correction
            CorrectErrors = s.CorrectErrors,
            CorrectToLatin = s.CorrectToLatin,
            ParagraphDetection = s.ParagraphDetection,
            TryToReadBarcodes = s.TryToReadBarcodes,
            UseTesseract = s.UseTesseract,
            TesseractPathConfigured = !string.IsNullOrWhiteSpace(s.TesseractPath),
            WindowsAiAvailable = WindowsAiUtilities.CanDeviceUseWinAI(),
            LastUsedLang = s.LastUsedLang ?? string.Empty,

            // Global hotkeys
            GlobalHotkeysEnabled = s.GlobalHotkeysEnabled,
            FullscreenGrabHotKey = s.FullscreenGrabHotKey ?? string.Empty,
            GrabFrameHotkey = s.GrabFrameHotkey ?? string.Empty,
            EditWindowHotKey = s.EditWindowHotKey ?? string.Empty,
            LookupHotKey = s.LookupHotKey ?? string.Empty,

            // Lookup tool
            LookupSearchHistory = s.LookupSearchHistory,
            LookupFileConfigured = !string.IsNullOrWhiteSpace(s.LookupFileLocation),

            // Display / font
            AppTheme = s.AppTheme ?? "System",
            FontFamilySetting = s.FontFamilySetting ?? "Default",
            FontSizeSetting = s.FontSizeSetting,
            IsFontBold = s.IsFontBold,
            IsFontItalic = s.IsFontItalic,
            IsFontUnderline = s.IsFontUnderline,
            IsFontStrikeout = s.IsFontStrikeout,

            // Grab Frame
            GrabFrameAutoOcr = s.GrabFrameAutoOcr,
            GrabFrameUpdateEtw = s.GrabFrameUpdateEtw,
            GrabFrameScrollBehavior = s.GrabFrameScrollBehavior ?? string.Empty,
            GrabFrameReadBarcodes = s.GrabFrameReadBarcodes,
            GrabFrameWordGrouping = s.GrabFrameWordGrouping ?? string.Empty,
            GrabFrameTranslationEnabled = s.GrabFrameTranslationEnabled,
            GrabFrameTranslationLanguage = s.GrabFrameTranslationLanguage ?? string.Empty,

            // Fullscreen grab
            FSGMakeSingleLineToggle = s.FSGMakeSingleLineToggle,
            FsgDefaultMode = s.FsgDefaultMode ?? string.Empty,
            FsgSelectionStyle = s.FsgSelectionStyle ?? string.Empty,
            FsgShadeOverlay = s.FsgShadeOverlay,
            FsgSendEtwToggle = s.FsgSendEtwToggle,

            // Edit Text Window
            EditWindowIsWordWrapOn = s.EditWindowIsWordWrapOn,
            EditWindowIsOnTop = s.EditWindowIsOnTop,
            EditWindowBottomBarIsHidden = s.EditWindowBottomBarIsHidden,
            EditWindowStartFullscreen = s.EditWindowStartFullscreen,
            RestoreEtwPositions = s.RestoreEtwPositions,
            EtwUseMargins = s.EtwUseMargins,
            EtwSpellCheckMode = s.EtwSpellCheckMode ?? string.Empty,
            ShowCursorText = s.ShowCursorText,
            ScrollBottomBar = s.ScrollBottomBar,
            EtwShowLangPicker = s.EtwShowLangPicker,
            EtwShowWordCount = s.EtwShowWordCount,
            EtwShowCharDetails = s.EtwShowCharDetails,
            EtwShowMatchCount = s.EtwShowMatchCount,
            EtwShowRegexPattern = s.EtwShowRegexPattern,
            EtwShowSimilarMatches = s.EtwShowSimilarMatches,

            // Calculator pane
            CalcShowErrors = s.CalcShowErrors,
            CalcShowPane = s.CalcShowPane,
            CalcPaneWidth = s.CalcPaneWidth,

            // Web search (name only, not URLs)
            DefaultWebSearch = s.DefaultWebSearch ?? string.Empty,

            // UI Automation
            UiAutomationEnabled = s.UiAutomationEnabled,
            UiAutomationFallbackToOcr = s.UiAutomationFallbackToOcr,
            UiAutomationTraversalMode = s.UiAutomationTraversalMode ?? string.Empty,
            UiAutomationIncludeOffscreen = s.UiAutomationIncludeOffscreen,
            UiAutomationPreferFocusedElement = s.UiAutomationPreferFocusedElement,

            // Advanced
            OverrideAiArchCheck = s.OverrideAiArchCheck,
            EnableFileBackedManagedSettings = s.EnableFileBackedManagedSettings,
        };
    }

    private static ManagedSettingsSummaryModel GetManagedSettingsSummary()
    {
        try
        {
            SettingsService svc = AppUtilities.TextGrabSettingsService;

            StoredRegex[] regexes = svc.LoadStoredRegexes();
            StoredRegex[] customRegexes = [.. regexes.Where(r => !r.IsDefault)];

            List<Models.ButtonInfo> postGrabActions = svc.LoadPostGrabActions();
            Dictionary<string, bool> postGrabCheckStates = svc.LoadPostGrabCheckStates();
            int enabledPostGrabCount = postGrabCheckStates.Values.Count(v => v);

            List<Models.ShortcutKeySet> shortcuts = svc.LoadShortcutKeySets();
            int enabledShortcutCount = shortcuts.Count(s => s.IsEnabled);

            List<Models.ButtonInfo> bottomButtons = svc.LoadBottomBarButtons();

            List<Models.WebSearchUrlModel> webSearchUrls = svc.LoadWebSearchUrls();

            List<GrabTemplate> templates = GrabTemplateManager.GetAllTemplates();

            return new ManagedSettingsSummaryModel
            {
                RegexPatternCount = regexes.Length,
                RegexDefaultPatternCount = regexes.Length - customRegexes.Length,
                RegexCustomPatternCount = customRegexes.Length,
                RegexCustomPatternNames = [.. customRegexes.Select(r => r.Name)],

                PostGrabActionCount = postGrabActions.Count,
                PostGrabActionNames = [.. postGrabActions.Select(a => a.ButtonText)],
                PostGrabEnabledCount = enabledPostGrabCount,

                ShortcutKeySetCount = shortcuts.Count,
                EnabledShortcutKeySetCount = enabledShortcutCount,

                BottomBarButtonCount = bottomButtons.Count,

                WebSearchUrlCount = webSearchUrls.Count,

                GrabTemplateCount = templates.Count,
            };
        }
        catch (Exception ex)
        {
            return new ManagedSettingsSummaryModel
            {
                ErrorMessage = $"Error reading managed settings: {ex.Message}"
            };
        }
    }

    private static HistoryInfoModel GetHistoryInfo()
    {
        try
        {
            HistoryService historyService = Singleton<HistoryService>.Instance;
            List<HistoryInfo>? imageHistory = historyService.GetRecentGrabs();

            string lastTextHistory = historyService.GetLastTextHistory();
            bool hasTextHistory = !string.IsNullOrEmpty(lastTextHistory);

            return new HistoryInfoModel
            {
                TextOnlyHistoryCount = hasTextHistory ? 1 : 0,
                ImageHistoryCount = imageHistory?.Count ?? 0,
                TotalHistoryCount = (hasTextHistory ? 1 : 0) + (imageHistory?.Count ?? 0),
                OldestEntryDate = GetOldestHistoryDate(null, imageHistory),
                NewestEntryDate = GetNewestHistoryDate(null, imageHistory),
                HasLastTextHistory = hasTextHistory,
                LastTextHistoryLength = lastTextHistory?.Length ?? 0
            };
        }
        catch (Exception ex)
        {
            return new HistoryInfoModel
            {
                TextOnlyHistoryCount = -1,
                ImageHistoryCount = -1,
                TotalHistoryCount = -1,
                ErrorMessage = $"Error accessing history: {ex.Message}"
            };
        }
    }

    private static DateTimeOffset? GetOldestHistoryDate(IList<HistoryInfo>? textHistory, IList<HistoryInfo>? imageHistory)
    {
        List<DateTimeOffset> dates = [];

        if (textHistory is not null)
            dates.AddRange(textHistory.Select(h => h.CaptureDateTime));

        if (imageHistory is not null)
            dates.AddRange(imageHistory.Select(h => h.CaptureDateTime));

        return dates.Count > 0 ? dates.Min() : null;
    }

    private static DateTimeOffset? GetNewestHistoryDate(IList<HistoryInfo>? textHistory, IList<HistoryInfo>? imageHistory)
    {
        List<DateTimeOffset> dates = [];

        if (textHistory is not null)
            dates.AddRange(textHistory.Select(h => h.CaptureDateTime));

        if (imageHistory is not null)
            dates.AddRange(imageHistory.Select(h => h.CaptureDateTime));

        return dates.Count > 0 ? dates.Max() : null;
    }

    private static LanguageInfoModel GetLanguageInfo()
    {
        try
        {
            IList<Interfaces.ILanguage> availableLanguages = LanguageUtilities.GetAllLanguages();
            Interfaces.ILanguage currentLanguage = LanguageUtilities.GetCurrentInputLanguage();

            return new LanguageInfoModel
            {
                CurrentInputLanguage = currentLanguage.LanguageTag,
                AvailableOcrLanguages = [.. OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag)],
                AvailableLanguagesCount = availableLanguages.Count,
                WindowsAiAvailable = WindowsAiUtilities.CanDeviceUseWinAI(),
                TesseractLanguagesConfigured = ["Will be populated from Tesseract installation"]
            };
        }
        catch (Exception ex)
        {
            return new LanguageInfoModel
            {
                CurrentInputLanguage = "Error",
                AvailableOcrLanguages = [],
                AvailableLanguagesCount = 0,
                WindowsAiAvailable = false,
                TesseractLanguagesConfigured = [],
                ErrorMessage = $"Error accessing language info: {ex.Message}"
            };
        }
    }

    private static async Task<TesseractInfoModel> GetTesseractInfoAsync()
    {
        try
        {
            bool canLocate = TesseractHelper.CanLocateTesseractExe();
            List<string> availableLanguages = [];

            if (canLocate)
            {
                try
                {
                    availableLanguages = await TesseractHelper.TesseractLanguagesAsStrings();
                }
                catch (Exception ex)
                {
                    availableLanguages = [$"Error getting languages: {ex.Message}"];
                }
            }

            return new TesseractInfoModel
            {
                IsInstalled = canLocate,
                ExecutablePath = canLocate ? "Located (path redacted)" : "Not found",
                Version = "Version info not publicly available",
                AvailableLanguages = availableLanguages,
                ConfiguredLanguages = ["Will be populated from Tesseract installation"]
            };
        }
        catch (Exception ex)
        {
            return new TesseractInfoModel
            {
                IsInstalled = false,
                ExecutablePath = string.Empty,
                Version = string.Empty,
                AvailableLanguages = [],
                ConfiguredLanguages = [],
                ErrorMessage = $"Error accessing Tesseract info: {ex.Message}"
            };
        }
    }

    private static List<MonitorInfoModel> GetMonitorsInfo()
    {
        List<MonitorInfoModel> monitors = [];
        try
        {
            DisplayInfo[] displays = DisplayInfo.AllDisplayInfos;
            for (int i = 0; i < displays.Length; i++)
            {
                DisplayInfo di = displays[i];
                // DPI scale percent
                NativeMethods.GetScaleFactorForMonitor(di.MonitorHandle, out uint scalePercent);
                // Raw and scaled bounds
                Dapplo.Windows.Common.Structs.NativeRect raw = di.Bounds;
                Rect scaled = di.ScaledBounds();

                monitors.Add(new MonitorInfoModel
                {
                    Index = i + 1,
                    ScalePercent = scalePercent,
                    Bounds = raw,
                    ScaledBounds = scaled
                });
            }
        }
        catch (Exception ex)
        {
            monitors.Add(new MonitorInfoModel
            {
                Index = -1,
                ScalePercent = 0,
                Bounds = new Rect(),
                ScaledBounds = new Rect(),
                ErrorMessage = $"Error reading monitors: {ex.Message}"
            });
        }
        return monitors;
    }
}

public class BugReportModel
{
    public DateTimeOffset GeneratedAt { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string InstallationType { get; set; } = string.Empty;
    public string WindowsVersion { get; set; } = string.Empty;
    public StartupDetailsModel StartupDetails { get; set; } = new();
    public SettingsInfoModel SettingsInfo { get; set; } = new();
    public ManagedSettingsSummaryModel ManagedSettingsSummary { get; set; } = new();
    public HistoryInfoModel HistoryInfo { get; set; } = new();
    public LanguageInfoModel LanguageInfo { get; set; } = new();
    public TesseractInfoModel TesseractInfo { get; set; } = new();
    public List<MonitorInfoModel> Monitors { get; set; } = [];
}

public class StartupDetailsModel
{
    public bool IsPackaged { get; set; }
    public string StartupMethod { get; set; } = string.Empty;
    // Full paths are redacted to avoid exposing the local username/directory structure
    public string ExecutableFileName { get; set; } = string.Empty;
    public string RegistryPath { get; set; } = string.Empty;
    public string RegistryValueStatus { get; set; } = string.Empty;
}

public class SettingsInfoModel
{
    // Core behavior
    public bool FirstRun { get; set; }
    public bool ShowToast { get; set; }
    public bool StartupOnLogin { get; set; }
    public bool RunInBackground { get; set; }
    public bool NeverAutoUseClipboard { get; set; }
    public bool UseHistory { get; set; }
    public bool AddToContextMenu { get; set; }
    public bool RegisterOpenWith { get; set; }

    // Grab behavior
    public string DefaultLaunch { get; set; } = string.Empty;
    public bool TryInsert { get; set; }
    public double InsertDelay { get; set; }
    public bool CloseFrameOnGrab { get; set; }
    public bool PostGrabStayOpen { get; set; }

    // OCR / error correction
    public bool CorrectErrors { get; set; }
    public bool CorrectToLatin { get; set; }
    public bool ParagraphDetection { get; set; }
    public bool TryToReadBarcodes { get; set; }
    public bool UseTesseract { get; set; }
    public bool TesseractPathConfigured { get; set; }  // true/false only — full path is PII
    public bool WindowsAiAvailable { get; set; }
    public string LastUsedLang { get; set; } = string.Empty;

    // Global hotkeys
    public bool GlobalHotkeysEnabled { get; set; }
    public string FullscreenGrabHotKey { get; set; } = string.Empty;
    public string GrabFrameHotkey { get; set; } = string.Empty;
    public string EditWindowHotKey { get; set; } = string.Empty;
    public string LookupHotKey { get; set; } = string.Empty;

    // Lookup tool
    public bool LookupSearchHistory { get; set; }
    public bool LookupFileConfigured { get; set; }  // true/false only — full path is PII

    // Display / font
    public string AppTheme { get; set; } = string.Empty;
    public string FontFamilySetting { get; set; } = string.Empty;
    public double FontSizeSetting { get; set; }
    public bool IsFontBold { get; set; }
    public bool IsFontItalic { get; set; }
    public bool IsFontUnderline { get; set; }
    public bool IsFontStrikeout { get; set; }

    // Grab Frame
    public bool GrabFrameAutoOcr { get; set; }
    public bool GrabFrameUpdateEtw { get; set; }
    public string GrabFrameScrollBehavior { get; set; } = string.Empty;
    public bool GrabFrameReadBarcodes { get; set; }
    public string GrabFrameWordGrouping { get; set; } = string.Empty;
    public bool GrabFrameTranslationEnabled { get; set; }
    public string GrabFrameTranslationLanguage { get; set; } = string.Empty;

    // Fullscreen grab
    public bool FSGMakeSingleLineToggle { get; set; }
    public string FsgDefaultMode { get; set; } = string.Empty;
    public string FsgSelectionStyle { get; set; } = string.Empty;
    public bool FsgShadeOverlay { get; set; }
    public bool FsgSendEtwToggle { get; set; }

    // Edit Text Window
    public bool EditWindowIsWordWrapOn { get; set; }
    public bool EditWindowIsOnTop { get; set; }
    public bool EditWindowBottomBarIsHidden { get; set; }
    public bool EditWindowStartFullscreen { get; set; }
    public bool RestoreEtwPositions { get; set; }
    public bool EtwUseMargins { get; set; }
    public string EtwSpellCheckMode { get; set; } = string.Empty;
    public bool ShowCursorText { get; set; }
    public bool ScrollBottomBar { get; set; }
    public bool EtwShowLangPicker { get; set; }
    public bool EtwShowWordCount { get; set; }
    public bool EtwShowCharDetails { get; set; }
    public bool EtwShowMatchCount { get; set; }
    public bool EtwShowRegexPattern { get; set; }
    public bool EtwShowSimilarMatches { get; set; }

    // Calculator pane
    public bool CalcShowErrors { get; set; }
    public bool CalcShowPane { get; set; }
    public int CalcPaneWidth { get; set; }

    // Web search (name of default search only — URLs are not included)
    public string DefaultWebSearch { get; set; } = string.Empty;

    // UI Automation
    public bool UiAutomationEnabled { get; set; }
    public bool UiAutomationFallbackToOcr { get; set; }
    public string UiAutomationTraversalMode { get; set; } = string.Empty;
    public bool UiAutomationIncludeOffscreen { get; set; }
    public bool UiAutomationPreferFocusedElement { get; set; }

    // Advanced
    public bool OverrideAiArchCheck { get; set; }
    public bool EnableFileBackedManagedSettings { get; set; }
}

public class ManagedSettingsSummaryModel
{
    // Regex patterns
    public int RegexPatternCount { get; set; }
    public int RegexDefaultPatternCount { get; set; }
    public int RegexCustomPatternCount { get; set; }
    // Names only — actual pattern strings are omitted as they may reveal sensitive data domains
    public List<string> RegexCustomPatternNames { get; set; } = [];

    // Post-grab actions
    public int PostGrabActionCount { get; set; }
    public List<string> PostGrabActionNames { get; set; } = [];
    public int PostGrabEnabledCount { get; set; }

    // Shortcut key sets
    public int ShortcutKeySetCount { get; set; }
    public int EnabledShortcutKeySetCount { get; set; }

    // Bottom bar buttons
    public int BottomBarButtonCount { get; set; }

    // Web search URLs (count only — URLs are not included as they may reveal research interests)
    public int WebSearchUrlCount { get; set; }

    // Grab templates
    public int GrabTemplateCount { get; set; }

    public string? ErrorMessage { get; set; }
}

public class HistoryInfoModel
{
    public int TextOnlyHistoryCount { get; set; }
    public int ImageHistoryCount { get; set; }
    public int TotalHistoryCount { get; set; }
    public DateTimeOffset? OldestEntryDate { get; set; }
    public DateTimeOffset? NewestEntryDate { get; set; }
    public bool HasLastTextHistory { get; set; }
    public int LastTextHistoryLength { get; set; }
    public string? ErrorMessage { get; set; }
}

public class LanguageInfoModel
{
    public string CurrentInputLanguage { get; set; } = string.Empty;
    public List<string> AvailableOcrLanguages { get; set; } = [];
    public int AvailableLanguagesCount { get; set; }
    public bool WindowsAiAvailable { get; set; }
    public List<string> TesseractLanguagesConfigured { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public class TesseractInfoModel
{
    public bool IsInstalled { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<string> AvailableLanguages { get; set; } = [];
    public List<string> ConfiguredLanguages { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public class MonitorInfoModel
{
    public int Index { get; set; }
    public uint ScalePercent { get; set; }
    public Rect Bounds { get; set; } = new();
    public Rect ScaledBounds { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
