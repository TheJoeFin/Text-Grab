using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using Text_Grab.Core.Editing;

namespace Text_Grab.Editing.Panes;

/// <summary>
/// A component of the Edit Text Window that has a region of its own beside the editor, such as the
/// calc pane. It knows the document only through <see cref="IEditorSession"/>.
/// </summary>
/// <remarks>
/// <para>
/// From E4a, AvalonDock hosts panes. It re-parents <see cref="View"/> when the layout changes and when
/// the pane is hidden or shown, which raises the view's <c>Unloaded</c> and <c>Loaded</c> again. So
/// nothing that should happen once, such as <see cref="Attach"/>, <see cref="Detach"/> or subscribing
/// to the session, may be tied to those events.
/// </para>
/// <para>
/// Hiding a pane is not detaching it. A hidden pane stays attached and should go idle: stop work
/// whose results nobody can see. <see cref="Detach"/> runs when the pane leaves the layout or the
/// window closes.
/// </para>
/// </remarks>
public interface IEditorPane
{
    PaneDescriptor Descriptor { get; }

    /// <summary>What the host puts on screen for this pane.</summary>
    FrameworkElement View { get; }

    /// <summary>
    /// Connects the pane to the window's document. <paramref name="config"/> is what
    /// <see cref="SaveConfig"/> returned when the layout was saved, or null.
    /// </summary>
    void Attach(IEditorSession session, JsonElement? config);

    /// <summary>Disconnects from the session: unsubscribes, stops timers and releases anything held.</summary>
    void Detach();

    /// <summary>
    /// What a layout should remember about this pane, or null for nothing. The pane's size and whether
    /// it is shown are the host's to remember.
    /// </summary>
    JsonElement? SaveConfig();

    /// <summary>The commands this pane adds to the Edit Text Window's command palette right now.</summary>
    IReadOnlyList<PaneCommand> GetPaletteCommands() => [];

    /// <summary>
    /// The window the pane is in was activated: a chance for a pane that shows data from outside the
    /// window, such as history, to look again. A pane that is hidden should only note that it is stale.
    /// </summary>
    void OnWindowActivated()
    {
    }

    /// <summary>
    /// The host showed or hid the pane. A hidden pane should stop work whose results nobody can see
    /// and catch up when shown again. Called by the host even when WPF's <c>IsVisibleChanged</c> does
    /// not fire, such as when a layout opens with the pane collapsed.
    /// </summary>
    void OnVisibilityChanged(bool visible)
    {
    }
}

/// <summary>One entry a pane adds to the command palette.</summary>
/// <param name="Name">The entry's name, as the palette lists it.</param>
/// <param name="Group">The group the palette shows beside the name.</param>
/// <param name="Execute">Runs the command.</param>
public sealed record PaneCommand(string Name, string Group, Action Execute);
