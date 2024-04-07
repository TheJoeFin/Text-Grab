using Text_Grab.Helpers;
using Windows.Storage;

namespace Text_Grab.Services;
internal class SettingsService
{
    private ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;
    // relevant discussion https://github.com/microsoft/WindowsAppSDK/discussions/1478
    public SettingsService()
    {
        if (!_localSettings.Values.ContainsKey("IsFirstRun"))
        {
            _localSettings.Values["IsFirstRun"] = true;
        }
    }

    private bool? testSetting;

    public bool TestSetting
    {
        get
        {
            testSetting ??= _localSettings.ReadAsync<bool>(nameof(TestSetting)).Result;
            testSetting ??= false;

            return testSetting.Value;
        }
        set
        {
            testSetting = value;
            _localSettings.SaveAsync(nameof(TestSetting), value);
        }
    }

    private double? testDoubleSetting;
    public double? TestDoubleSetting
    {
        get
        {
            testDoubleSetting ??= _localSettings.ReadAsync<double>(nameof(TestDoubleSetting)).Result;
            testDoubleSetting ??= 2;
            return testDoubleSetting;
        }
        set
        {
            testDoubleSetting = value;
            _localSettings.SaveAsync(nameof(TestDoubleSetting), value);
        }
    }

    private string? testStringSetting;
    public string? TestStringSetting
    {
        get
        {
            testStringSetting ??= _localSettings.ReadAsync<string>(nameof(TestStringSetting)).Result;
            testStringSetting ??= "Hello, World!";
            return testStringSetting;
        }
        set
        {
            testStringSetting = value;
            _localSettings.SaveAsync(nameof(TestStringSetting), value);
        }
    }
}
