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
            BaseDirectory = AppContext.BaseDirectory,
            ExecutablePath = Environment.ProcessPath ?? "Unknown"
        };

        if (AppUtilities.IsPackaged())
        {
            details.StartupMethod = "StartupTask API (packaged apps)";
            details.RegistryPath = "N/A (uses StartupTask)";
            details.RegistryValue = "N/A (uses StartupTask)";
        }
        else
        {
            details.StartupMethod = "Registry Run key (unpackaged apps)";
            details.RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

            string executablePath = Path.Combine(AppContext.BaseDirectory, "Text-Grab.exe");
            details.CalculatedRegistryValue = $"{executablePath}";

            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                details.ActualRegistryValue = key?.GetValue("Text-Grab")?.ToString() ?? "Not set";
            }
            catch (Exception ex)
            {
                details.ActualRegistryValue = $"Error reading registry: {ex.Message}";
            }
        }

        return details;
    }

    private static SettingsInfoModel GetSettingsInfo()
    {
        Settings settings = AppUtilities.TextGrabSettings;

        return new SettingsInfoModel
        {
            FirstRun = settings.FirstRun,
            ShowToast = settings.ShowToast,
            StartupOnLogin = settings.StartupOnLogin,
            RunInBackground = settings.RunInTheBackground,
            GlobalHotkeysEnabled = settings.GlobalHotkeysEnabled,
            TryToReadBarcodes = false,
            CorrectErrors = settings.CorrectErrors,
            CorrectToLatin = false,
            UseTesseract = settings.UseTesseract,
            WindowsAiAvailable = WindowsAiUtilities.CanDeviceUseWinAI(),
            DefaultLaunch = settings.DefaultLaunch?.ToString() ?? "Unknown",
            DefaultLanguage = "Not configured in settings",
            TesseractPath = settings.TesseractPath ?? string.Empty,
            NeverAutoUseClipboard = settings.NeverAutoUseClipboard,
            FontFamilySetting = settings.FontFamilySetting ?? "Default",
            IsFontBold = settings.IsFontBold,
        };
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
                ExecutablePath = canLocate ? "Located (path private)" : "Not found",
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
    public HistoryInfoModel HistoryInfo { get; set; } = new();
    public LanguageInfoModel LanguageInfo { get; set; } = new();
    public TesseractInfoModel TesseractInfo { get; set; } = new();
    public List<MonitorInfoModel> Monitors { get; set; } = [];
}

public class StartupDetailsModel
{
    public bool IsPackaged { get; set; }
    public string StartupMethod { get; set; } = string.Empty;
    public string BaseDirectory { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string RegistryPath { get; set; } = string.Empty;
    public string CalculatedRegistryValue { get; set; } = string.Empty;
    public string ActualRegistryValue { get; set; } = string.Empty;
    public string RegistryValue { get; set; } = string.Empty;
}

public class SettingsInfoModel
{
    public bool FirstRun { get; set; }
    public bool ShowToast { get; set; }
    public bool StartupOnLogin { get; set; }
    public bool RunInBackground { get; set; }
    public bool GlobalHotkeysEnabled { get; set; }
    public bool TryToReadBarcodes { get; set; }
    public bool CorrectErrors { get; set; }
    public bool CorrectToLatin { get; set; }
    public bool UseTesseract { get; set; }
    public bool WindowsAiAvailable { get; set; }
    public string DefaultLaunch { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = string.Empty;
    public string TesseractPath { get; set; } = string.Empty;
    public bool NeverAutoUseClipboard { get; set; }
    public string FontFamilySetting { get; set; } = string.Empty;
    public bool IsFontBold { get; set; }
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
