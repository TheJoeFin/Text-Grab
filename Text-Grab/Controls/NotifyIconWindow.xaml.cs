using System.Windows;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Wpf.Ui.Tray.Controls;

namespace Text_Grab.Controls;
/// <summary>
/// Interaction logic for NotifyIconWindow.xaml
/// </summary>
public partial class NotifyIconWindow : Window
{
    public NotifyIconWindow()
    {
        InitializeComponent();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        App.Current.Shutdown();
    }

    private void NotifyIcon_LeftClick(NotifyIcon sender, RoutedEventArgs e)
    {
        e.Handled = true;
        App.DefaultLaunch();
    }

    private void Window_Activated(object sender, System.EventArgs e)
    {
        Hide();
        NotifyIcon.Visibility = Visibility.Visible;
    }

    private void EditWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditTextWindow etw = new(); etw.Show();
    }

    private void GrabFrameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        GrabFrame gf = new(); gf.Show();
    }

    private void FullscreenGrabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab();
    }

    private async void PreviousRegionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await OcrUtilities.GetTextFromPreviousFullscreenRegion();
    }

    private void LookupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        QuickSimpleLookup qsl = new(); qsl.Show();
    }

    private void LastGrabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Singleton<HistoryService>.Instance.GetLastHistoryAsGrabFrame();
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SettingsWindow sw = new(); sw.Show();
    }

    private void NotifyIcon_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!NotifyIcon.IsVisible)
            NotifyIcon.Visibility = Visibility.Visible;
    }
}
