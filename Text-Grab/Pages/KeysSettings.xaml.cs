using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for KeysSettings.xaml
/// </summary>
public partial class KeysSettings : Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private bool settingsSet = false;

    public KeysSettings()
    {
        InitializeComponent();
    }

    private void ShortcutControl_Recording(object sender, EventArgs e)
    {
        if (!settingsSet)
            return;

        foreach (UIElement child in ShortcutsStackPanel.Children)
            if (child is ShortcutControl shortcutControl
                && sender is ShortcutControl senderShortcut
                && shortcutControl != senderShortcut)
                shortcutControl.StopRecording(sender);
    }

    private void ShortcutControl_KeySetChanged(object sender, EventArgs e)
    {
        if (!settingsSet)
            return;

        if (HotKeysAllDifferent())
        {
            List<ShortcutKeySet> shortcutKeys = [];

            foreach (UIElement child in ShortcutsStackPanel.Children)
                if (child is ShortcutControl control)
                    shortcutKeys.Add(control.KeySet);

            ShortcutKeysUtilities.SaveShortcutKeySetSettings(shortcutKeys);
        }
    }

    private bool HotKeysAllDifferent()
    {
        bool anyMatchingKeys = false;

        HashSet<ShortcutControl> shortcuts = [];

        foreach (UIElement child in ShortcutsStackPanel.Children)
            if (child is ShortcutControl shortcutControl)
                shortcuts.Add(shortcutControl);

        if (shortcuts.Count == 0)
            return false;

        foreach (ShortcutControl shortcut in shortcuts)
        {
            ShortcutKeySet keySet = shortcut.KeySet;
            bool isThisShortcutGood = true;

            foreach (ShortcutControl shortcut2 in shortcuts)
            {
                if (shortcut == shortcut2)
                    continue;

                if (keySet.AreKeysEqual(shortcut2.KeySet) && (shortcut.KeySet.IsEnabled && keySet.IsEnabled))
                {
                    shortcut.HasConflictingError = true;
                    shortcut2.HasConflictingError = true;
                    shortcut2.GoIntoErrorMode("Cannot have two shortcuts that are the same");
                    anyMatchingKeys = true;
                    isThisShortcutGood = false;
                }
            }

            if (isThisShortcutGood)
                shortcut.HasConflictingError = false;

            shortcut.CheckForErrors();
        }

        if (anyMatchingKeys)
            return false;

        return true;
    }   

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.Current is App app)
            NotifyIconUtilities.UnregisterHotkeys(app);
        // registering of hotkeys is done when the settings window closes

        RunInBackgroundChkBx.IsChecked = DefaultSettings.RunInTheBackground;
        GlobalHotkeysCheckbox.IsChecked = DefaultSettings.GlobalHotkeysEnabled;

        IEnumerable<ShortcutKeySet> shortcutKeySets = ShortcutKeysUtilities.GetShortcutKeySetsFromSettings();

        foreach (ShortcutKeySet keySet in shortcutKeySets)
        {
            switch (keySet.Action)
            {
                case ShortcutKeyActions.None:
                    break;
                case ShortcutKeyActions.Settings:
                    break;
                case ShortcutKeyActions.Fullscreen:
                    FsgShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.GrabFrame:
                    GfShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.Lookup:
                    QslShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.EditWindow:
                    EtwShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.PreviousRegionGrab:
                    GlrShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.PreviousEditWindow:
                    LetwShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.PreviousGrabFrame:
                    LgfShortcutControl.KeySet = keySet;
                    break;
                default:
                    break;
            }
        }

        settingsSet = true;
    }

    private void RunInBackgroundChkBx_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.RunInTheBackground = true;
        ImplementAppOptions.ImplementBackgroundOption(DefaultSettings.RunInTheBackground);
        DefaultSettings.Save();
    }

    private void RunInBackgroundChkBx_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.RunInTheBackground = false;
        ImplementAppOptions.ImplementBackgroundOption(DefaultSettings.RunInTheBackground);
        GlobalHotkeysCheckbox.IsChecked = false;
        DefaultSettings.Save();
    }

    private void GlobalHotkeysCheckbox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.GlobalHotkeysEnabled = true;
    }

    private void GlobalHotkeysCheckbox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.GlobalHotkeysEnabled = false;
    }
}
