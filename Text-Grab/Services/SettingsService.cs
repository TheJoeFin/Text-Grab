using System;
using System.Diagnostics;
using Text_Grab.Utilities;
using Windows.Storage;

namespace Text_Grab.Services;

internal class SettingsService
{
    private ApplicationDataContainer? _localSettings;
    // relevant discussion https://github.com/microsoft/WindowsAppSDK/discussions/1478

    private Properties.Settings _classicSettings = Properties.Settings.Default;

    public SettingsService()
    {
        if (AppUtilities.IsPackaged())
            _localSettings = ApplicationData.Current.LocalSettings;
    }

    public T GetSetting<T>(string name)
    {
        // if running as packaged try to get from local settings
        if (_localSettings is not null)
        {
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
        }

        return _classicSettings[name] is T value ? value : default;
    }

    public void SaveSetting<T>(string name, T value)
    {
        if (_localSettings is not null)
        {
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
        else
            _classicSettings[name] = value;
    }
}
