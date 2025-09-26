using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Diagnostics;
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

    private async void ExportBugReportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string filePath = await DiagnosticsUtilities.SaveBugReportToFileAsync();
            
            MessageBoxResult result = MessageBox.Show(
                $"Bug report saved to:\n{filePath}\n\nWould you like to open the file location?", 
                "Bug Report Generated", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Information);
            
            if (result == MessageBoxResult.Yes)
            {
                // Open the file location in File Explorer
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(
                $"Failed to generate bug report:\n{ex.Message}", 
                "Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
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
