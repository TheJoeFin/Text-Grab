using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Text_Grab.Utilities;

public static class DiagnosticsUtilities
{
    public static async Task<string> GenerateBugReportAsync()
    {
        var bugReport = new BugReportModel
        {
            GeneratedAt = DateTimeOffset.Now,
            AppVersion = AppUtilities.GetAppVersion(),
            InstallationType = GetInstallationType(),
            WindowsVersion = GetWindowsVersion(),
            StartupDetails = GetStartupDetails(),
            SettingsInfo = GetSettingsInfo(),
            HistoryInfo = GetHistoryInfo(),
            LanguageInfo = GetLanguageInfo(),
            TesseractInfo = await GetTesseractInfoAsync()
        };

        var options = new JsonSerializerOptions
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
        
        // Check if it's self-contained by looking for runtime files
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
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
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
        var details = new StartupDetailsModel
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
            
            // Calculate the path that would be set in registry (using the fixed logic)
            string executablePath = Path.Combine(AppContext.BaseDirectory, "Text-Grab.exe");
            details.CalculatedRegistryValue = $"\"{executablePath}\"";
            
            // Try to read the actual registry value
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
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
        var settings = AppUtilities.TextGrabSettings;
        
        return new SettingsInfoModel
        {
            FirstRun = settings.FirstRun,
            ShowToast = settings.ShowToast,
            StartupOnLogin = settings.StartupOnLogin,
            RunInBackground = settings.RunInTheBackground,
            GlobalHotkeysEnabled = settings.GlobalHotkeysEnabled,
            TryToReadBarcodes = false, // Property doesn't exist in current settings
            CorrectErrors = settings.CorrectErrors,
            CorrectToLatin = false, // Property doesn't exist in current settings
            UseTesseract = settings.UseTesseract,
            UseWindowsOcr = true, // Assume true as it's built-in Windows OCR
            UseWindowsAi = false, // Property doesn't exist, check through utility
            DefaultLaunch = settings.DefaultLaunch?.ToString() ?? "Unknown",
            DefaultLanguage = "Not configured in settings", // Property doesn't exist in current settings
            TesseractPath = settings.TesseractPath ?? string.Empty,
            NeverAutoUseClipboard = settings.NeverAutoUseClipboard,
            FontFamilySetting = settings.FontFamilySetting ?? "Default",
            IsFontBold = settings.IsFontBold,
            ShowTooltips = false // Property doesn't exist in current settings
        };
    }

    private static HistoryInfoModel GetHistoryInfo()
    {
        try
        {
            var historyService = Singleton<HistoryService>.Instance;
            var imageHistory = historyService.GetRecentGrabs();
            
            // We can get some text history info but there's no public getter for full text history
            string lastTextHistory = historyService.GetLastTextHistory();
            bool hasTextHistory = !string.IsNullOrEmpty(lastTextHistory);
            
            return new HistoryInfoModel
            {
                TextOnlyHistoryCount = hasTextHistory ? 1 : 0, // Can only tell if there's at least one
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
        var dates = new List<DateTimeOffset>();
        
        if (textHistory != null)
            dates.AddRange(textHistory.Select(h => h.CaptureDateTime));
        
        if (imageHistory != null)
            dates.AddRange(imageHistory.Select(h => h.CaptureDateTime));
        
        return dates.Count > 0 ? dates.Min() : null;
    }

    private static DateTimeOffset? GetNewestHistoryDate(IList<HistoryInfo>? textHistory, IList<HistoryInfo>? imageHistory)
    {
        var dates = new List<DateTimeOffset>();
        
        if (textHistory != null)
            dates.AddRange(textHistory.Select(h => h.CaptureDateTime));
        
        if (imageHistory != null)
            dates.AddRange(imageHistory.Select(h => h.CaptureDateTime));
        
        return dates.Count > 0 ? dates.Max() : null;
    }

    private static LanguageInfoModel GetLanguageInfo()
    {
        try
        {
            var availableLanguages = LanguageUtilities.GetAllLanguages();
            var currentLanguage = LanguageUtilities.GetCurrentInputLanguage();
            
            return new LanguageInfoModel
            {
                CurrentInputLanguage = currentLanguage.LanguageTag,
                AvailableOcrLanguages = OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToList(),
                AvailableLanguagesCount = availableLanguages.Count,
                WindowsAiAvailable = WindowsAiUtilities.CanDeviceUseWinAI(),
                TesseractLanguagesConfigured = new List<string> { "Will be populated from Tesseract installation" }
            };
        }
        catch (Exception ex)
        {
            return new LanguageInfoModel
            {
                CurrentInputLanguage = "Error",
                AvailableOcrLanguages = new List<string>(),
                AvailableLanguagesCount = 0,
                WindowsAiAvailable = false,
                TesseractLanguagesConfigured = new List<string>(),
                ErrorMessage = $"Error accessing language info: {ex.Message}"
            };
        }
    }

    private static async Task<TesseractInfoModel> GetTesseractInfoAsync()
    {
        try
        {
            bool canLocate = TesseractHelper.CanLocateTesseractExe();
            List<string> availableLanguages = new();

            if (canLocate)
            {
                try
                {
                    availableLanguages = await TesseractHelper.TesseractLanguagesAsStrings();
                }
                catch (Exception ex)
                {
                    availableLanguages = new List<string> { $"Error getting languages: {ex.Message}" };
                }
            }

            return new TesseractInfoModel
            {
                IsInstalled = canLocate,
                ExecutablePath = canLocate ? "Located (path private)" : "Not found",
                Version = "Version info not publicly available",
                AvailableLanguages = availableLanguages,
                ConfiguredLanguages = new List<string> { "Will be populated from Tesseract installation" }
            };
        }
        catch (Exception ex)
        {
            return new TesseractInfoModel
            {
                IsInstalled = false,
                ExecutablePath = string.Empty,
                Version = string.Empty,
                AvailableLanguages = new List<string>(),
                ConfiguredLanguages = new List<string>(),
                ErrorMessage = $"Error accessing Tesseract info: {ex.Message}"
            };
        }
    }
}

// Data models for the bug report
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
    public bool UseWindowsOcr { get; set; }
    public bool UseWindowsAi { get; set; }
    public string DefaultLaunch { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = string.Empty;
    public string TesseractPath { get; set; } = string.Empty;
    public bool NeverAutoUseClipboard { get; set; }
    public string FontFamilySetting { get; set; } = string.Empty;
    public bool IsFontBold { get; set; }
    public bool ShowTooltips { get; set; }
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
    public List<string> AvailableOcrLanguages { get; set; } = new();
    public int AvailableLanguagesCount { get; set; }
    public bool WindowsAiAvailable { get; set; }
    public List<string> TesseractLanguagesConfigured { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class TesseractInfoModel
{
    public bool IsInstalled { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<string> AvailableLanguages { get; set; } = new();
    public List<string> ConfiguredLanguages { get; set; } = new();
    public string? ErrorMessage { get; set; }
}