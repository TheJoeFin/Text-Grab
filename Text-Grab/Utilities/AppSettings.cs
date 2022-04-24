using Windows.Storage;

namespace Text_Grab;

public class AppSettings
{
    ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

    private DefaultLaunchSetting _defaultLaunch;

    public DefaultLaunchSetting DefaultLaunch
    {
        get { return _defaultLaunch; }
        set
        {
            _defaultLaunch = value;
            localSettings.Values[nameof(DefaultLaunch)] = _defaultLaunch.ToString();
        }
    }

    private bool _showToast;

    public bool ShowToast
    {
        get { return _showToast; }
        set
        {
            _showToast = value;
            localSettings.Values[nameof(ShowToast)] = _showToast.ToString();
        }
    }

    public AppSettings()
    {
        string ActivateOnLaunchSetting = (string)localSettings.Values[nameof(DefaultLaunch)];
        if (ActivateOnLaunchSetting == null)
            ActivateOnLaunchSetting = "false";

        switch (ActivateOnLaunchSetting)
        {
            case "GrabFrame":
                _defaultLaunch = DefaultLaunchSetting.GrabFrame;
                break;
            default:
                _defaultLaunch = DefaultLaunchSetting.Fullscreen;
                break;
        }

        string ShowToastSetting = (string)localSettings.Values[nameof(ShowToast)];
        if (ShowToastSetting == null)
            ShowToastSetting = "false";
        _showToast = bool.Parse(ShowToastSetting);
    }
}
