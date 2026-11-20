using System;

namespace Text_Grab.Editing.Panes;

/// <summary>Which side of the editor a pane sits on when a layout does not say.</summary>
public enum PaneDock
{
    Left,
    Right,
    Top,
    Bottom,
    Center,
}

/// <summary>What a kind of pane is, apart from any one instance of it.</summary>
/// <param name="Id">
/// The pane's stable ID, such as <c>textgrab.pane.calc</c>. Layout files name panes by it, and from E4a
/// it is also the pane's <c>ContentId</c> in AvalonDock.
/// </param>
/// <param name="Title">The pane's name as people see it.</param>
/// <param name="IconKey">A <c>Wpf.Ui.Controls.SymbolRegular</c> name for the pane's icon.</param>
/// <param name="DefaultDock">Where the pane goes when a layout does not place it.</param>
/// <param name="DefaultSize">
/// The pane's width, or its height when docked at the top or bottom, in device-independent pixels.
/// </param>
/// <param name="ConfigType">The type the pane's layout config reads into, if it has one.</param>
public sealed record PaneDescriptor(
    string Id,
    string Title,
    string IconKey,
    PaneDock DefaultDock,
    double DefaultSize,
    Type? ConfigType = null);
