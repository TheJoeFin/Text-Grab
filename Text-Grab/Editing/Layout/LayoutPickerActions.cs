using System;
using Text_Grab.Core.Actions;

namespace Text_Grab.Editing.Layout;

/// <summary>What <see cref="LayoutPickerActions.Switch"/> takes as its config: the layout to show.</summary>
/// <param name="LayoutId">The layout's ID, built in or saved.</param>
public sealed record LayoutSwitchConfig(string LayoutId);

/// <summary>
/// The layout picker's actions as descriptors (v5 E6 <c>picker</c>), so that their IDs are pinned, a layout
/// can be bound to a hotkey or a button once E2 lands, and E2 can wire them into menus. Until then each
/// is an ordinary click handler in the Window menu's Layouts submenu and a command palette entry;
/// nothing in the registry runs them.
/// </summary>
internal static class LayoutPickerActions
{
    /// <summary>Shows a layout in this window. Config: <see cref="LayoutSwitchConfig"/>.</summary>
    public const string Switch = "textgrab.layout.switch";

    /// <summary>Saves the window's layout as a new user layout.</summary>
    public const string SaveAs = "textgrab.layout.saveAs";

    /// <summary>Returns the window's layout to a built-in definition.</summary>
    public const string Reset = "textgrab.layout.reset";

    /// <summary>Deletes a user layout.</summary>
    public const string Delete = "textgrab.layout.delete";

    /// <summary>Makes the window's layout the one new windows open in.</summary>
    public const string SetDefault = "textgrab.layout.setDefault";

    /// <summary>Registers the picker's actions. Call once per registry; a second call throws on the first duplicate ID.</summary>
    public static void RegisterAll(IActionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(Command(Switch, "Switch Layout", "WindowEdit20", typeof(LayoutSwitchConfig)));
        registry.Register(Command(SaveAs, "Save Current Layout As", "Save24"));
        registry.Register(Command(Reset, "Reset Layout", "ArrowReset24"));
        registry.Register(Command(Delete, "Delete Layout", "Delete24"));
        registry.Register(Command(SetDefault, "Set Layout as Default", "Star24"));
    }

    private static ActionDescriptor Command(string id, string displayName, string iconKey, Type? configType = null)
        => new(id, displayName, iconKey, ActionCategory.Window, ActionKind.AppCommand, ConfigType: configType);
}
