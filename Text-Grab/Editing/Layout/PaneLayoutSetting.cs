using System;

namespace Text_Grab.Editing.Layout;

/// <summary>
/// Reads the <c>EtwPaneLayout</c> setting: whether Edit Text Windows render their editor area from a
/// layout (v5 E4a) or use the hand-written grid of 4.16.
/// </summary>
/// <remarks>
/// The setting is a string, not a bool, so that changing the default reaches everyone who has not
/// chosen. A bool's default can end up saved: the file-backed settings sidecar writes every setting,
/// chosen or not, so a saved "False" would keep a person on the old path after the default changed.
/// Empty means "this build's default"; only "On" and "Off" are choices.
/// </remarks>
internal static class PaneLayoutSetting
{
    public const string On = "On";
    public const string Off = "Off";

    /// <summary>
    /// The pane layout for people who have not chosen. On from v5 E4a step 9; the hand-written editor grid
    /// stays one release as the way back (the Edit Text settings page writes Off).
    /// </summary>
    public const bool DefaultIsOn = true;

    public static bool IsOn(string? value)
    {
        if (string.Equals(value, On, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(value, Off, StringComparison.OrdinalIgnoreCase))
            return false;

        return DefaultIsOn;
    }
}
