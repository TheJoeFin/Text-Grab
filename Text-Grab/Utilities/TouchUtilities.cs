using System;
using System.Diagnostics;
using System.Windows.Input;

namespace Text_Grab.Utilities;

internal static class TouchUtilities
{
    /// <summary>
    /// Has WPF find the touch screen and pen before any window exists. Call once, before the first window.
    /// </summary>
    /// <remarks>
    /// WPF looks for touch and pen devices the first time a window needs them. When a text box with spell
    /// check on was created before that (the Edit Text Window's editors are), the lookup finds no devices
    /// at all. WPF then gets no touch input for the life of the process and Windows hands it mouse clicks
    /// instead: a finger drag selects text rather than scrolling, and no touch or manipulation event fires.
    /// Reproduced in a bare WPF app (a spell-checked RichTextBox drops <see cref="Tablet.TabletDevices"/>
    /// from 4 to 0); looking first keeps them, and windows created afterwards still get touch.
    /// </remarks>
    internal static void EnsureTouchDevicesEnumerated()
    {
        try
        {
            _ = Tablet.TabletDevices.Count;
        }
        catch (Exception ex)
        {
            // Touch is a nicety; never let the lookup stop the app from starting.
            Debug.WriteLine($"Touch device enumeration failed: {ex.Message}");
        }
    }
}
