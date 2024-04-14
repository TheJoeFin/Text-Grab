using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Text_Grab.Utilities;
using Windows.Storage;

namespace Text_Grab.Services;

internal class SettingsService : IDisposable
{
    private readonly ApplicationDataContainer? _localSettings;
    // relevant discussion https://github.com/microsoft/WindowsAppSDK/discussions/1478

    public Properties.Settings ClassicSettings = Properties.Settings.Default;

    public SettingsService()
    {
        if (!AppUtilities.IsPackaged())
            return;

        _localSettings = ApplicationData.Current.LocalSettings;

        if (ClassicSettings.FirstRun && _localSettings.Values.Count > 0)
            MigrateLocalSettingsToClassic();

        // copy settings from classic to local settings
        // so that when app updates they can be copied forward
        ClassicSettings.PropertyChanged -= ClassicSettings_PropertyChanged;
        ClassicSettings.PropertyChanged += ClassicSettings_PropertyChanged;
    }

    private void MigrateLocalSettingsToClassic()
    {
        if (_localSettings is null)
            return;

        foreach (KeyValuePair<string, object> localSetting in _localSettings.Values)
            ClassicSettings[localSetting.Key] = localSetting.Value;
    }

    private void ClassicSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not string propertyName)
            return;

        SaveSettingInContainer(propertyName, ClassicSettings[propertyName]);
    }

    public void Dispose()
    {
        ClassicSettings.PropertyChanged -= ClassicSettings_PropertyChanged;
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
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to Save setting from ApplicationDataContainer {ex.Message}");
#if DEBUG
            throw;
#endif
        }
    }
}
