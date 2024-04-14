using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using Text_Grab.Utilities;
using Windows.Storage;

namespace Text_Grab.Services;

internal class SettingsService : IDisposable
{
    private ApplicationDataContainer? _localSettings;
    // relevant discussion https://github.com/microsoft/WindowsAppSDK/discussions/1478

    public Properties.Settings ClassicSettings = Properties.Settings.Default;

    private static SettingsPropertyCollection settingOptions = Properties.Settings.Default.Properties;

    public SettingsService()
    {
        if (AppUtilities.IsPackaged())
            _localSettings = ApplicationData.Current.LocalSettings;

        ClassicSettings.PropertyChanged -= ClassicSettings_PropertyChanged;
        ClassicSettings.PropertyChanged += ClassicSettings_PropertyChanged;
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
