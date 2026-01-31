using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab.Pages;
/// <summary>
/// Interaction logic for FullscreenGrabSettings.xaml
/// </summary>
public partial class FullscreenGrabSettings : Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private bool _loaded = false;

    public FullscreenGrabSettings()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // hydrate controls from settings
        SendToEtwCheckBox.IsChecked = DefaultSettings.FsgSendEtwToggle;
        TryInsertCheckBox.IsChecked = DefaultSettings.TryInsert;

        ShadeOverlayCheckBox.IsChecked = DefaultSettings.FsgShadeOverlay;

        InsertDelaySlider.Value = Math.Clamp(DefaultSettings.InsertDelay, InsertDelaySlider.Minimum, InsertDelaySlider.Maximum);
        InsertDelayValueText.Text = DefaultSettings.InsertDelay.ToString("0.0", CultureInfo.InvariantCulture);

        InsertDelaySlider.IsEnabled = TryInsertCheckBox.IsChecked == true;

        // Determine default mode
        FsgDefaultMode mode = FsgDefaultMode.Default;
        if (!string.IsNullOrWhiteSpace(DefaultSettings.FsgDefaultMode))
            Enum.TryParse(DefaultSettings.FsgDefaultMode, true, out mode);

        if (mode == FsgDefaultMode.Table)
        {
            TableModeRadio.IsChecked = true;
        }
        else if (DefaultSettings.FSGMakeSingleLineToggle)
        {
            SingleLineModeRadio.IsChecked = true;
        }
        else
        {
            DefaultModeRadio.IsChecked = true;
        }

        // Update post-grab actions count
        UpdateActionsCountText();

        _loaded = true;
    }

    private void DefaultModeRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded || DefaultModeRadio.IsChecked != true) return;
        DefaultSettings.FsgDefaultMode = nameof(FsgDefaultMode.Default);
        DefaultSettings.FSGMakeSingleLineToggle = false;
        DefaultSettings.Save();
    }

    private void SingleLineModeRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded || SingleLineModeRadio.IsChecked != true) return;
        // SingleLine uses the legacy flag, FsgDefaultMode stays Default
        DefaultSettings.FSGMakeSingleLineToggle = true;
        DefaultSettings.FsgDefaultMode = nameof(FsgDefaultMode.Default);
        DefaultSettings.Save();
    }

    private void TableModeRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded || TableModeRadio.IsChecked != true) return;
        DefaultSettings.FsgDefaultMode = nameof(FsgDefaultMode.Table);
        DefaultSettings.FSGMakeSingleLineToggle = false;
        DefaultSettings.Save();
    }

    private void SendToEtwCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.FsgSendEtwToggle = SendToEtwCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void TryInsertCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        bool enabled = TryInsertCheckBox.IsChecked == true;
        DefaultSettings.TryInsert = enabled;
        DefaultSettings.Save();
        InsertDelaySlider.IsEnabled = enabled;
    }

    private void ShadeOverlayCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        bool isChecked = sender is CheckBox cb && cb.IsChecked == true;
        DefaultSettings.FsgShadeOverlay = isChecked;
        DefaultSettings.Save();
    }

    private void InsertDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        double newVal = Math.Round(InsertDelaySlider.Value, 1);
        DefaultSettings.InsertDelay = newVal;
        DefaultSettings.Save();
        InsertDelayValueText.Text = newVal.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private void CustomizeActionsButton_Click(object sender, RoutedEventArgs e)
    {
        PostGrabActionEditor editor = new()
        {
            Owner = Window.GetWindow(this)
        };

        bool? result = editor.ShowDialog();

        if (result == true)
        {
            // Update the count text after changes
            UpdateActionsCountText();
        }
    }

    private void UpdateActionsCountText()
    {
        List<ButtonInfo> enabledActions = PostGrabActionManager.GetEnabledPostGrabActions();
        int count = enabledActions.Count;

        if (count == 0)
        {
            ActionsCountText.Text = "No actions enabled";
        }
        else if (count == 1)
        {
            ActionsCountText.Text = $"1 action enabled: {enabledActions.First().ButtonText}";
        }
        else
        {
            string actionsList = string.Join(", ", enabledActions.Take(3).Select(a => a.ButtonText));
            if (count > 3)
                actionsList += $", and {count - 3} more";

            ActionsCountText.Text = $"{count} actions enabled: {actionsList}";
        }
    }
}
