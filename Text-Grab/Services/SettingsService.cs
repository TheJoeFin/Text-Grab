using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Windows.Storage;

namespace Text_Grab.Services;

internal class SettingsService : IDisposable
{
    private const string ManagedJsonSettingsFolderName = "settings-data";

    private static readonly Dictionary<string, string> ManagedJsonSettingFiles = new(StringComparer.Ordinal)
    {
        [nameof(Properties.Settings.RegexList)] = "RegexList.json",
        [nameof(Properties.Settings.ShortcutKeySets)] = "ShortcutKeySets.json",
        [nameof(Properties.Settings.BottomButtonsJson)] = "BottomButtons.json",
        [nameof(Properties.Settings.WebSearchItemsJson)] = "WebSearchItems.json",
        [nameof(Properties.Settings.PostGrabJSON)] = "PostGrabActions.json",
        [nameof(Properties.Settings.PostGrabCheckStates)] = "PostGrabCheckStates.json",
    };

    private readonly ApplicationDataContainer? _localSettings;
    private readonly string _managedJsonSettingsFolderPath;
    private readonly bool _saveClassicSettingsChanges;
    private readonly Lock _managedJsonLock = new();
    private bool _suppressManagedJsonPropertyChanged;
    private StoredRegex[]? _cachedRegexPatterns;
    private List<ShortcutKeySet>? _cachedShortcutKeySets;
    private List<ButtonInfo>? _cachedBottomBarButtons;
    private List<WebSearchUrlModel>? _cachedWebSearchUrls;
    private List<ButtonInfo>? _cachedPostGrabActions;
    private Dictionary<string, bool>? _cachedPostGrabCheckStates;
    // relevant discussion https://github.com/microsoft/WindowsAppSDK/discussions/1478

    public Properties.Settings ClassicSettings;

    public SettingsService()
        : this(
            Properties.Settings.Default,
            AppUtilities.IsPackaged() ? ApplicationData.Current.LocalSettings : null)
    {
    }

    internal SettingsService(
        Properties.Settings classicSettings,
        ApplicationDataContainer? localSettings,
        string? managedJsonSettingsFolderPath = null,
        bool saveClassicSettingsChanges = true)
    {
        ClassicSettings = classicSettings;
        _localSettings = localSettings;
        _managedJsonSettingsFolderPath = managedJsonSettingsFolderPath ?? GetManagedJsonSettingsFolderPath();
        _saveClassicSettingsChanges = saveClassicSettingsChanges;

        if (ClassicSettings.FirstRun && _localSettings is not null && _localSettings.Values.Count > 0)
            MigrateLocalSettingsToClassic();

        // copy settings from classic to local settings
        // so that when app updates they can be copied forward
        ClassicSettings.PropertyChanged -= ClassicSettings_PropertyChanged;
        ClassicSettings.PropertyChanged += ClassicSettings_PropertyChanged;

        MigrateManagedJsonSettingsToFiles();
        RemoveManagedJsonSettingsFromContainer();
    }

    private void MigrateLocalSettingsToClassic()
    {
        if (_localSettings is null)
            return;

        foreach (KeyValuePair<string, object> localSetting in _localSettings.Values)
        {
            try { ClassicSettings[localSetting.Key] = localSetting.Value; }
            catch (SettingsPropertyNotFoundException ex) { Debug.WriteLine($"Failed to migrate {localSetting.Key} of value {localSetting.Value}, exception message: {ex.Message}"); } // continue, just skip the setting
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to migrate setting {localSetting.Key} from ApplicationDataContainer {ex.Message}");
#if DEBUG
                throw;
#endif
            }

        }
    }

    private void ClassicSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not string propertyName)
            return;

        if (IsManagedJsonSetting(propertyName))
        {
            if (_suppressManagedJsonPropertyChanged)
                return;

            HandleManagedJsonSettingChanged(propertyName);
            return;
        }

        SaveSettingInContainer(propertyName, ClassicSettings[propertyName]);
    }

    public void Dispose()
    {
        ClassicSettings.PropertyChanged -= ClassicSettings_PropertyChanged;
    }

    internal static bool IsManagedJsonSetting(string propertyName) =>
        ManagedJsonSettingFiles.ContainsKey(propertyName);

    internal string GetManagedJsonSettingValueForExport(string propertyName)
    {
        if (!IsManagedJsonSetting(propertyName))
            return ClassicSettings[propertyName] as string ?? string.Empty;

        return ReadManagedJsonSettingText(propertyName);
    }

    public T? GetSettingFromContainer<T>(string name)
    {
        // if running as packaged try to get from local settings
        if (_localSettings is null)
            return default;

        try
        {
            _localSettings.Values.TryGetValue(name, out object? obj);

            if (obj is not null) // not saved into local settings, get default from classic settings
                return (T)Convert.ChangeType(obj, typeof(T));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to Get setting from ApplicationDataContainer {ex.Message}");

#if DEBUG
            throw;
#endif
        }

        return default;
    }

    public void SaveSettingInContainer<T>(string name, T value)
    {
        if (_localSettings is null)
            return;

        try
        {
            _localSettings.Values[name] = value;
        }
        catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x80073DC8))
        {
            // The value exceeds the ApplicationDataContainer size limit (8 KB).
            // Large data should be stored in a file instead.
            Debug.WriteLine($"Setting '{name}' exceeds ApplicationDataContainer size limit: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to Save setting in ApplicationDataContainer: {ex.Message}");
#if DEBUG
            throw;
#endif
        }
    }

    public StoredRegex[] LoadStoredRegexes() =>
        LoadManagedJson(
            nameof(Properties.Settings.RegexList),
            static () => [],
            CloneStoredRegexes,
            ref _cachedRegexPatterns);

    public void SaveStoredRegexes(IEnumerable<StoredRegex> storedRegexes)
    {
        StoredRegex[] materialized = CloneStoredRegexes(storedRegexes);
        SaveManagedJson(
            nameof(Properties.Settings.RegexList),
            materialized,
            CloneStoredRegexes,
            ref _cachedRegexPatterns);
    }

    public List<ShortcutKeySet> LoadShortcutKeySets() =>
        LoadManagedJson(
            nameof(Properties.Settings.ShortcutKeySets),
            static () => [],
            CloneShortcutKeySets,
            ref _cachedShortcutKeySets);

    public void SaveShortcutKeySets(IEnumerable<ShortcutKeySet> shortcutKeySets)
    {
        List<ShortcutKeySet> materialized = CloneShortcutKeySets(shortcutKeySets);
        SaveManagedJson(
            nameof(Properties.Settings.ShortcutKeySets),
            materialized,
            CloneShortcutKeySets,
            ref _cachedShortcutKeySets);
    }

    public List<ButtonInfo> LoadBottomBarButtons() =>
        LoadManagedJson(
            nameof(Properties.Settings.BottomButtonsJson),
            static () => [],
            CloneButtonInfos,
            ref _cachedBottomBarButtons);

    public void SaveBottomBarButtons(IEnumerable<ButtonInfo> buttonInfos)
    {
        List<ButtonInfo> materialized = CloneButtonInfos(buttonInfos);
        SaveManagedJson(
            nameof(Properties.Settings.BottomButtonsJson),
            materialized,
            CloneButtonInfos,
            ref _cachedBottomBarButtons);
    }

    public List<WebSearchUrlModel> LoadWebSearchUrls() =>
        LoadManagedJson(
            nameof(Properties.Settings.WebSearchItemsJson),
            static () => [],
            CloneWebSearchUrls,
            ref _cachedWebSearchUrls);

    public void SaveWebSearchUrls(IEnumerable<WebSearchUrlModel> webSearchUrls)
    {
        List<WebSearchUrlModel> materialized = CloneWebSearchUrls(webSearchUrls);
        SaveManagedJson(
            nameof(Properties.Settings.WebSearchItemsJson),
            materialized,
            CloneWebSearchUrls,
            ref _cachedWebSearchUrls);
    }

    public List<ButtonInfo> LoadPostGrabActions() =>
        LoadManagedJson(
            nameof(Properties.Settings.PostGrabJSON),
            static () => [],
            CloneButtonInfos,
            ref _cachedPostGrabActions);

    public void SavePostGrabActions(IEnumerable<ButtonInfo> actions)
    {
        List<ButtonInfo> materialized = CloneButtonInfos(actions);
        SaveManagedJson(
            nameof(Properties.Settings.PostGrabJSON),
            materialized,
            CloneButtonInfos,
            ref _cachedPostGrabActions);
    }

    public Dictionary<string, bool> LoadPostGrabCheckStates() =>
        LoadManagedJson(
            nameof(Properties.Settings.PostGrabCheckStates),
            static () => [],
            CloneCheckStates,
            ref _cachedPostGrabCheckStates);

    public void SavePostGrabCheckStates(IReadOnlyDictionary<string, bool> checkStates)
    {
        Dictionary<string, bool> materialized = CloneCheckStates(checkStates);
        SaveManagedJson(
            nameof(Properties.Settings.PostGrabCheckStates),
            materialized,
            CloneCheckStates,
            ref _cachedPostGrabCheckStates);
    }

    private void HandleManagedJsonSettingChanged(string propertyName)
    {
        InvalidateManagedJsonCache(propertyName);

        string managedJsonValue = ClassicSettings[propertyName] as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(managedJsonValue))
        {
            DeleteManagedJsonSettingFile(propertyName);
            RemoveSettingFromContainer(propertyName);
            return;
        }

        if (TryWriteManagedJsonSettingText(propertyName, managedJsonValue))
        {
            ClearManagedJsonSetting(propertyName);
            return;
        }

        SaveSettingInContainer(propertyName, managedJsonValue);
    }

    private void MigrateManagedJsonSettingsToFiles()
    {
        bool migratedAnySettings = false;

        foreach (string propertyName in ManagedJsonSettingFiles.Keys)
        {
            string managedJsonValue = ClassicSettings[propertyName] as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(managedJsonValue))
                continue;

            if (!TryWriteManagedJsonSettingText(propertyName, managedJsonValue))
                continue;

            ClearManagedJsonSetting(propertyName);
            migratedAnySettings = true;
        }

        if (migratedAnySettings && _saveClassicSettingsChanges)
            ClassicSettings.Save();
    }

    private void RemoveManagedJsonSettingsFromContainer()
    {
        if (_localSettings is null)
            return;

        foreach (string propertyName in ManagedJsonSettingFiles.Keys)
        {
            string filePath = GetManagedJsonSettingFilePath(propertyName);
            string classicValue = ClassicSettings[propertyName] as string ?? string.Empty;

            if (File.Exists(filePath) || string.IsNullOrWhiteSpace(classicValue))
                RemoveSettingFromContainer(propertyName);
        }
    }

    private T LoadManagedJson<T>(
        string propertyName,
        Func<T> emptyFactory,
        Func<T, T> clone,
        ref T? cachedValue)
        where T : class
    {
        lock (_managedJsonLock)
        {
            if (cachedValue is not null)
                return clone(cachedValue);
        }

        T loadedValue = emptyFactory();
        string json = ReadManagedJsonSettingText(propertyName);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                loadedValue = JsonSerializer.Deserialize<T>(json) ?? emptyFactory();
            }
            catch (JsonException)
            {
                loadedValue = emptyFactory();
            }
        }

        T cachedCopy = clone(loadedValue);
        lock (_managedJsonLock)
        {
            cachedValue = cachedCopy;
            return clone(cachedCopy);
        }
    }

    private void SaveManagedJson<T>(
        string propertyName,
        T value,
        Func<T, T> clone,
        ref T? cachedValue)
        where T : class
    {
        T cachedCopy = clone(value);
        string json = JsonSerializer.Serialize(cachedCopy);
        bool persistedToFile = TryWriteManagedJsonSettingText(propertyName, json);

        lock (_managedJsonLock)
        {
            cachedValue = clone(cachedCopy);
        }

        if (persistedToFile)
        {
            ClearManagedJsonSetting(propertyName);
        }
        else
        {
            SetManagedJsonSettingValue(propertyName, json);
            SaveSettingInContainer(propertyName, json);
        }

        if (_saveClassicSettingsChanges)
            ClassicSettings.Save();
    }

    private string ReadManagedJsonSettingText(string propertyName)
    {
        string filePath = GetManagedJsonSettingFilePath(propertyName);
        if (File.Exists(filePath))
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Failed to read managed setting file '{propertyName}': {ex.Message}");
            }
        }

        string managedJsonValue = ClassicSettings[propertyName] as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(managedJsonValue))
            return string.Empty;

        if (TryWriteManagedJsonSettingText(propertyName, managedJsonValue))
        {
            ClearManagedJsonSetting(propertyName);

            if (_saveClassicSettingsChanges)
                ClassicSettings.Save();
        }

        return managedJsonValue;
    }

    private bool TryWriteManagedJsonSettingText(string propertyName, string value)
    {
        try
        {
            Directory.CreateDirectory(_managedJsonSettingsFolderPath);
            File.WriteAllText(GetManagedJsonSettingFilePath(propertyName), value);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to persist managed setting '{propertyName}' to disk: {ex.Message}");
            return false;
        }
    }

    private void DeleteManagedJsonSettingFile(string propertyName)
    {
        string filePath = GetManagedJsonSettingFilePath(propertyName);
        if (!File.Exists(filePath))
            return;

        try
        {
            File.Delete(filePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete managed setting file '{propertyName}': {ex.Message}");
        }
    }

    private void ClearManagedJsonSetting(string propertyName)
    {
        SetManagedJsonSettingValue(propertyName, string.Empty);
        RemoveSettingFromContainer(propertyName);
    }

    private void SetManagedJsonSettingValue(string propertyName, string value)
    {
        _suppressManagedJsonPropertyChanged = true;
        try
        {
            ClassicSettings[propertyName] = value;
        }
        finally
        {
            _suppressManagedJsonPropertyChanged = false;
        }
    }

    private void RemoveSettingFromContainer(string name)
    {
        if (_localSettings is null)
            return;

        try
        {
            _localSettings.Values.Remove(name);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to remove setting from ApplicationDataContainer: {ex.Message}");
        }
    }

    private string GetManagedJsonSettingFilePath(string propertyName) =>
        Path.Combine(_managedJsonSettingsFolderPath, ManagedJsonSettingFiles[propertyName]);

    private static string GetManagedJsonSettingsFolderPath()
    {
        if (AppUtilities.IsPackaged())
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, ManagedJsonSettingsFolderName);

        string? exeDir = Path.GetDirectoryName(FileUtilities.GetExePath());
        return Path.Combine(exeDir ?? "c:\\Text-Grab", ManagedJsonSettingsFolderName);
    }

    private void InvalidateManagedJsonCache(string propertyName)
    {
        lock (_managedJsonLock)
        {
            switch (propertyName)
            {
                case nameof(Properties.Settings.RegexList):
                    _cachedRegexPatterns = null;
                    break;
                case nameof(Properties.Settings.ShortcutKeySets):
                    _cachedShortcutKeySets = null;
                    break;
                case nameof(Properties.Settings.BottomButtonsJson):
                    _cachedBottomBarButtons = null;
                    break;
                case nameof(Properties.Settings.WebSearchItemsJson):
                    _cachedWebSearchUrls = null;
                    break;
                case nameof(Properties.Settings.PostGrabJSON):
                    _cachedPostGrabActions = null;
                    break;
                case nameof(Properties.Settings.PostGrabCheckStates):
                    _cachedPostGrabCheckStates = null;
                    break;
            }
        }
    }

    private static StoredRegex[] CloneStoredRegexes(IEnumerable<StoredRegex> storedRegexes) =>
        [.. storedRegexes.Select(static regex => new StoredRegex
        {
            Id = regex.Id,
            Name = regex.Name,
            Pattern = regex.Pattern,
            IsDefault = regex.IsDefault,
            Description = regex.Description,
            CreatedDate = regex.CreatedDate,
            LastUsedDate = regex.LastUsedDate,
        })];

    private static List<ShortcutKeySet> CloneShortcutKeySets(IEnumerable<ShortcutKeySet> shortcutKeySets) =>
        [.. shortcutKeySets.Select(static shortcut => new ShortcutKeySet
        {
            Modifiers = [.. shortcut.Modifiers],
            NonModifierKey = shortcut.NonModifierKey,
            IsEnabled = shortcut.IsEnabled,
            Name = shortcut.Name,
            Action = shortcut.Action,
        })];

    private static List<ButtonInfo> CloneButtonInfos(IEnumerable<ButtonInfo> buttonInfos) =>
        [.. buttonInfos.Select(static button => new ButtonInfo
        {
            OrderNumber = button.OrderNumber,
            ButtonText = button.ButtonText,
            SymbolText = button.SymbolText,
            Background = button.Background,
            Command = button.Command,
            ClickEvent = button.ClickEvent,
            IsSymbol = button.IsSymbol,
            SymbolIcon = button.SymbolIcon,
            IsRelevantForFullscreenGrab = button.IsRelevantForFullscreenGrab,
            IsRelevantForEditWindow = button.IsRelevantForEditWindow,
            DefaultCheckState = button.DefaultCheckState,
            TemplateId = button.TemplateId,
        })];

    private static List<WebSearchUrlModel> CloneWebSearchUrls(IEnumerable<WebSearchUrlModel> webSearchUrls) =>
        [.. webSearchUrls.Select(static url => new WebSearchUrlModel
        {
            Name = url.Name,
            Url = url.Url,
        })];

    private static Dictionary<string, bool> CloneCheckStates(IReadOnlyDictionary<string, bool> checkStates) =>
        checkStates.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value, StringComparer.Ordinal);
}
