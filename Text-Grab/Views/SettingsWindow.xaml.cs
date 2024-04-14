using System;
using System.Windows;
using Text_Grab.Pages;
using Text_Grab.Utilities;

namespace Text_Grab;

/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    #region Constructors

    public SettingsWindow()
    {
        InitializeComponent();
        App.SetTheme();
    }

    #endregion Constructors

    #region Methods

    private void Window_Closed(object? sender, EventArgs e)
    {
        AppUtilities.TextGrabSettings.Save();

        if (App.Current is App app)
            NotifyIconUtilities.RegisterHotKeys(app);

        WindowUtilities.ShouldShutDown();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SettingsNavView.Navigate(typeof(GeneralSettings));

        if (App.Current is App app)
            NotifyIconUtilities.UnregisterHotkeys(app);
    }

    #endregion Methods
}
