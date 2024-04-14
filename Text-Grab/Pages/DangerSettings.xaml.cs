using System.Windows;
using System.Windows.Controls;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for DangerSettings.xaml
/// </summary>
public partial class DangerSettings : Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    public DangerSettings()
    {
        InitializeComponent();
    }

    private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult areYouSure = MessageBox.Show("Are you sure you want to reset all settings to default and delete all history?", "Reset Settings to Default", MessageBoxButton.YesNo);

        if (areYouSure != MessageBoxResult.Yes)
            return;

        DefaultSettings.Reset();
        Singleton<HistoryService>.Instance.DeleteHistory();
        App.Current.Shutdown();
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult areYouSure = MessageBox.Show("Are you sure you want to delete all history?", "Reset Settings to Default", MessageBoxButton.YesNo);

        if (areYouSure != MessageBoxResult.Yes)
            return;

        Singleton<HistoryService>.Instance.DeleteHistory();
    }
}
