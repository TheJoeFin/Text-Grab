using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Panes;

namespace Text_Grab.Editing.Layout;

/// <summary>
/// Shows an Edit Text Window's dock tree (<see cref="EtwLayoutDefinition.Root"/>) through AvalonDock
/// (decision D1): the editor area as the one document, and each other pane as an anchorable around it.
/// </summary>
/// <remarks>
/// <para>
/// In 4.17 AvalonDock only does what the hand-written window did. Panes do not float, auto-hide, move
/// or close, and have no title bars or tabs (<see cref="PaneHostResources"/>). Only code hides and
/// shows a pane (<see cref="SetPaneVisible"/>): AvalonDock's hide path does not check
/// <c>CanHide</c>, which is off so that people cannot.
/// </para>
/// <para>
/// The window holds it as an <see cref="IPaneHost"/>: this class's fields are AvalonDock types, so
/// merely loading it loads AvalonDock, which the window must not do with the pane layout off.
/// </para>
/// <para>
/// Panes are made through their factories, only for panes in the layout, and attached once. AvalonDock
/// re-parents their views when a pane is hidden or shown, which raises <c>Unloaded</c> and
/// <c>Loaded</c> again; this class never ties anything to those events.
/// </para>
/// </remarks>
internal sealed class LayoutRenderer : IPaneHost
{
    /// <summary>How far one arrow key moves a resize bar, as GridSplitter's KeyboardIncrement.</summary>
    private const double KeyboardIncrement = 10;

    private readonly DockingManager manager;
    private readonly FrameworkElement editorContent;
    private readonly Func<string, IEditorPane?> createPane;
    private readonly Action<IEditorPane, JsonElement?> attachPane;
    private readonly Action<IEditorPane> detachPane;
    private readonly Dictionary<string, RenderedPane> panes = new(StringComparer.Ordinal);
    private readonly Dictionary<DockChild, RenderedChild> renderedChildren = new(System.Collections.Generic.ReferenceEqualityComparer.Instance);
    private LayoutDocument editorDocument = new();
    private EtwLayoutDefinition definition;
    private bool isHidingFromCode;

    /// <param name="editorContent">The editor area: the four editors and the reading bookmark.</param>
    /// <param name="createPane">Makes a pane from its factory, or null if nothing makes that ID.</param>
    /// <param name="attachPane">Connects a new pane to the window; called once per pane.</param>
    /// <param name="detachPane">Disconnects a pane that leaves the layout or the window.</param>
    public LayoutRenderer(
        FrameworkElement editorContent,
        Func<string, IEditorPane?> createPane,
        Action<IEditorPane, JsonElement?> attachPane,
        Action<IEditorPane> detachPane)
    {
        this.editorContent = editorContent;
        this.createPane = createPane;
        this.attachPane = attachPane;
        this.detachPane = detachPane;
        definition = BuiltInLayouts.Classic;

        PaneHostResources resources = new();
        manager = new DockingManager
        {
            // Ctrl+Tab would open AvalonDock's pane switcher even with focus in an editor.
            ShowNavigator = false,
            ShowSystemMenu = false,
            AllowFloatingWindows = false,
            AllowDetachedWindows = false,
            IgnoreTabControlKeyBindings = true,
            GridSplitterWidth = 5,
            GridSplitterHeight = 5,
            Template = (ControlTemplate)resources["PaneHost.ManagerTemplate"],
            DocumentPaneControlStyle = (Style)resources["PaneHost.DocumentPaneStyle"],
            AnchorablePaneControlStyle = (Style)resources["PaneHost.AnchorablePaneStyle"],
            GridSplitterVerticalStyle = (Style)resources["PaneHost.ResizerStyle"],
            GridSplitterHorizontalStyle = (Style)resources["PaneHost.ResizerStyle"],
        };
        manager.Resources.MergedDictionaries.Add(resources);
        manager.SetResourceReference(Control.BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        manager.AnchorableClosing += (_, e) => e.Cancel = true;
        manager.DocumentClosing += (_, e) => e.Cancel = true;
        manager.AnchorableHiding += (_, e) => e.Cancel = !isHidingFromCode;
        manager.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Manager_KeyDown));
    }

    /// <summary>A pane was shown or hidden, or a resize ended. The live layout (<see cref="Layout"/>) already says so.</summary>
    public event EventHandler? PaneLayoutChanged;

    /// <summary>What the window puts in its editor-area row.</summary>
    public FrameworkElement View => manager;

    /// <summary>
    /// The layout as it is now: the one last rendered, with the sizes and shown state the window has
    /// since given its panes.
    /// </summary>
    public EtwLayoutDefinition Layout => definition with { Root = Capture(definition.Root) };

    public IReadOnlyList<IEditorPane> Panes => [.. panes.Values.Select(rendered => rendered.Pane)];

    public IEditorPane? GetPane(string paneId) => panes.TryGetValue(paneId, out RenderedPane? rendered) ? rendered.Pane : null;

    /// <summary>
    /// Replaces what the window shows with <paramref name="layout"/>. Panes in both layouts keep their
    /// instance; panes only in the old one are detached; panes only in the new one are made and attached.
    /// </summary>
    /// <param name="layout">A layout <see cref="EtwLayoutValidator.FindProblem"/> found nothing wrong with.</param>
    public void Render(EtwLayoutDefinition layout)
    {
        ClearRenderedChildren();

        EtwLayoutDefinition normalized = EtwLayoutValidator.Normalize(layout);
        HashSet<string> shownIds = [.. ShownPaneIds(normalized.Root)];

        foreach (string removed in panes.Keys.Where(id => !shownIds.Contains(id)).ToList())
        {
            RenderedPane rendered = panes[removed];
            panes.Remove(removed);
            rendered.Anchorable.Content = null;
            detachPane(rendered.Pane);
        }

        // The old layout's items still hold the views; let go of them before the new items take them.
        foreach (RenderedPane kept in panes.Values)
            kept.Anchorable.Content = null;

        LayoutDocument editor = new()
        {
            ContentId = PaneIds.Editor,
            Title = "Editor",
            CanFloat = false,
            CanMove = false,
            CanClose = false,
        };
        editorDocument.Content = null;
        editorDocument = editor;

        List<RenderedPane> collapsed = [];
        List<RenderedPane> shown = [];
        LayoutPanel rootPanel = normalized.Root is SplitNode split
            ? BuildPanel(split, collapsed, shown)
            : new LayoutPanel(BuildChild(new DockChild(normalized.Root, 1, DockSizeUnit.Star), horizontal: true, collapsed, shown));

        manager.Layout = new LayoutRoot { RootPanel = rootPanel };
        editor.Content = editorContent;
        definition = normalized;

        foreach (RenderedPane rendered in collapsed)
        {
            HideFromCode(rendered.Anchorable);
            rendered.Pane.OnVisibilityChanged(false);
        }

        foreach (RenderedPane rendered in shown)
            rendered.Pane.OnVisibilityChanged(true);
    }

    public bool HasPane(string paneId) => panes.ContainsKey(paneId);

    public bool IsPaneVisible(string paneId) => panes.TryGetValue(paneId, out RenderedPane? rendered) && !rendered.Anchorable.IsHidden;

    /// <summary>
    /// Shows or hides a pane, keeping its place and size. Keyboard focus stays where it was: showing an
    /// anchorable makes it AvalonDock's active content, which would otherwise move focus into it.
    /// </summary>
    public void SetPaneVisible(string paneId, bool visible)
    {
        if (!panes.TryGetValue(paneId, out RenderedPane? rendered) || rendered.Anchorable.IsHidden != visible)
            return;

        IInputElement? focused = Keyboard.FocusedElement;
        if (visible)
            rendered.Anchorable.Show();
        else
            HideFromCode(rendered.Anchorable);

        rendered.Pane.OnVisibilityChanged(visible);

        if (focused is not null && !ReferenceEquals(Keyboard.FocusedElement, focused) && focused is UIElement { IsVisible: true })
            Keyboard.Focus(focused);

        PaneLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The pane's size along its split: its width beside the editor, its height above or below.</summary>
    public GridLength? GetPaneSize(string paneId)
    {
        if (!panes.TryGetValue(paneId, out RenderedPane? rendered))
            return null;

        return rendered.Horizontal ? rendered.Container.DockWidth : rendered.Container.DockHeight;
    }

    public void SetPaneSize(string paneId, GridLength size)
    {
        if (!panes.TryGetValue(paneId, out RenderedPane? rendered))
            return;

        if (rendered.Horizontal)
            rendered.Container.DockWidth = size;
        else
            rendered.Container.DockHeight = size;
    }

    /// <summary>The pane's laid-out size along its split, or 0 while it is hidden or not laid out.</summary>
    public double GetPaneActualSize(string paneId)
    {
        if (!panes.TryGetValue(paneId, out RenderedPane? rendered) || rendered.Anchorable.IsHidden)
            return 0;

        // The pane fills its container, whose own actual size AvalonDock does not make public.
        FrameworkElement view = rendered.Pane.View;
        return rendered.Horizontal ? view.ActualWidth : view.ActualHeight;
    }

    /// <summary>Detaches every pane. The window calls this once, when it closes.</summary>
    public void Close()
    {
        ClearRenderedChildren();

        foreach (RenderedPane rendered in panes.Values)
            detachPane(rendered.Pane);

        panes.Clear();
    }

    private static IEnumerable<string> ShownPaneIds(DockNode node) => node switch
    {
        PaneNode pane => [pane.PaneId],
        TabGroupNode tabs => tabs.Tabs.Count > 0 ? [tabs.Tabs[SelectedTabIndex(tabs)].PaneId] : [],
        SplitNode split => split.Children.SelectMany(child => ShownPaneIds(child.Node)),
        _ => [],
    };

    private static int SelectedTabIndex(TabGroupNode tabs) => Math.Clamp(tabs.SelectedIndex, 0, tabs.Tabs.Count - 1);

    private LayoutPanel BuildPanel(SplitNode split, List<RenderedPane> collapsed, List<RenderedPane> shown)
    {
        bool horizontal = split.Orientation == DockOrientation.Horizontal;
        LayoutPanel panel = new() { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical };
        foreach (DockChild child in split.Children)
            panel.Children.Add(BuildChild(child, horizontal, collapsed, shown));

        return panel;
    }

    private ILayoutPanelElement BuildChild(DockChild child, bool horizontal, List<RenderedPane> collapsed, List<RenderedPane> shown)
    {
        switch (child.Node)
        {
            case SplitNode nested:
                LayoutPanel panel = BuildPanel(nested, collapsed, shown);
                ApplySize(panel, child, horizontal, fallbackPixels: 200, minSize: 0);
                TrackChild(child, panel, horizontal);
                return panel;

            case PaneNode { PaneId: PaneIds.Editor }:
                LayoutDocumentPane documentPane = new(editorDocument) { ShowHeader = false };
                ApplySize(documentPane, child, horizontal, fallbackPixels: 400, minSize: 0);
                TrackChild(child, documentPane, horizontal);
                return documentPane;

            case PaneNode pane:
                LayoutAnchorablePane paneContainer = BuildAnchorablePane(pane, child, horizontal, collapsed, shown);
                TrackChild(child, paneContainer, horizontal);
                return paneContainer;

            case TabGroupNode tabs:
                // Only the selected tab is shown, or made: tab headers are hidden in 4.17.
                LayoutAnchorablePane tabContainer = BuildAnchorablePane(tabs.Tabs[SelectedTabIndex(tabs)], child, horizontal, collapsed, shown);
                TrackChild(child, tabContainer, horizontal);
                return tabContainer;

            default:
                throw new InvalidOperationException($"Unknown dock node {child.Node.GetType().Name}.");
        }
    }

    private LayoutAnchorablePane BuildAnchorablePane(
        PaneNode node,
        DockChild child,
        bool horizontal,
        List<RenderedPane> collapsed,
        List<RenderedPane> shown)
    {
        if (!panes.TryGetValue(node.PaneId, out RenderedPane? rendered))
        {
            IEditorPane pane = createPane(node.PaneId)
                ?? throw new InvalidOperationException($"No factory makes the pane {node.PaneId}.");
            rendered = new RenderedPane(pane);
            panes.Add(node.PaneId, rendered);
            if (child.Collapsed)
                pane.OnVisibilityChanged(false);
            attachPane(pane, node.Config);
        }
        else if (!child.Collapsed)
        {
            shown.Add(rendered);
        }

        PaneDescriptor descriptor = rendered.Pane.Descriptor;
        LayoutAnchorable anchorable = new()
        {
            ContentId = descriptor.Id,
            Title = descriptor.Title,
            Content = rendered.Pane.View,
            CanFloat = false,
            CanAutoHide = false,
            CanMove = false,
            CanClose = false,
            CanHide = false,
            CanDockAsTabbedDocument = false,
            CanShowOnHover = false,
        };
        LayoutAnchorablePane container = new(anchorable);
        ApplySize(container, child, horizontal, descriptor.DefaultSize, descriptor.MinSize);

        rendered.Anchorable = anchorable;
        rendered.Container = container;
        rendered.Horizontal = horizontal;
        rendered.Node = node;
        if (child.Collapsed)
            collapsed.Add(rendered);

        return container;
    }

    private void TrackChild<T>(DockChild child, LayoutPositionableGroup<T> element, bool horizontal)
        where T : class, ILayoutElement
    {
        element.PropertyChanged += Container_PropertyChanged;
        renderedChildren[child] = new RenderedChild(element, DockSize.For(element), horizontal);
    }

    private void ClearRenderedChildren()
    {
        foreach (RenderedChild tracked in renderedChildren.Values)
            tracked.Element.PropertyChanged -= Container_PropertyChanged;

        renderedChildren.Clear();
    }

    private static void ApplySize<T>(LayoutPositionableGroup<T> element, DockChild child, bool horizontal, double fallbackPixels, double minSize)
        where T : class, ILayoutElement
    {
        GridLength size = child.Unit == DockSizeUnit.Star
            ? new GridLength(EtwLayoutValidator.SanitizeSize(child.Size, 1), GridUnitType.Star)
            : new GridLength(EtwLayoutValidator.SanitizeSize(child.Size, fallbackPixels), GridUnitType.Pixel);

        if (horizontal)
        {
            element.DockWidth = size;
            element.DockMinWidth = minSize;
        }
        else
        {
            element.DockHeight = size;
            element.DockMinHeight = minSize;
        }
    }

    private void HideFromCode(LayoutAnchorable anchorable)
    {
        isHidingFromCode = true;
        try
        {
            anchorable.Hide();
        }
        finally
        {
            isHidingFromCode = false;
        }
    }

    // A resize ends with AvalonDock writing the new DockWidth or DockHeight.
    private void Container_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayoutAnchorablePane.DockWidth) or nameof(LayoutAnchorablePane.DockHeight))
            PaneLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The definition's tree with each shown pane's current size and hidden state.</summary>
    private DockNode Capture(DockNode node) => node switch
    {
        SplitNode split => split with { Children = [.. split.Children.Select(Capture)] },
        _ => node,
    };

    private DockChild Capture(DockChild child)
    {
        DockChild captured = child;
        if (renderedChildren.TryGetValue(child, out RenderedChild? tracked))
        {
            GridLength size = tracked.Size.Get(tracked.Horizontal);
            if (!size.IsAuto)
            {
                captured = captured with
                {
                    Size = size.Value,
                    Unit = size.IsStar ? DockSizeUnit.Star : DockSizeUnit.Pixel,
                };
            }
        }

        if (child.Node is SplitNode)
            return captured with { Node = Capture(child.Node) };

        string? paneId = child.Node switch
        {
            PaneNode pane => pane.PaneId,
            TabGroupNode tabs when tabs.Tabs.Count > 0 => tabs.Tabs[SelectedTabIndex(tabs)].PaneId,
            _ => null,
        };

        if (paneId is null || !panes.TryGetValue(paneId, out RenderedPane? rendered))
            return captured;

        JsonElement? config = rendered.Pane.SaveConfig();
        DockNode node = child.Node switch
        {
            PaneNode pane => pane with { Config = config ?? pane.Config },
            _ => child.Node,
        };

        return captured with
        {
            Node = node,
            Collapsed = rendered.Anchorable.IsHidden,
        };
    }

    // The resize bar is a Tab stop, so arrow keys move it, as they moved the GridSplitter it replaces.
    private void Manager_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not LayoutGridResizerControl resizer
            || resizer.Parent is not Grid grid
            || grid is not ILayoutControl { Model: ILayoutOrientableGroup group })
            return;

        bool horizontal = group.Orientation == Orientation.Horizontal;
        double delta = (horizontal, e.Key) switch
        {
            (true, Key.Left) => -KeyboardIncrement,
            (true, Key.Right) => KeyboardIncrement,
            (false, Key.Up) => -KeyboardIncrement,
            (false, Key.Down) => KeyboardIncrement,
            _ => 0,
        };

        if (delta == 0)
            return;

        int index = grid.Children.IndexOf(resizer);
        if (index <= 0 || index >= grid.Children.Count - 1
            || grid.Children[index - 1] is not FrameworkElement previous
            || previous is not ILayoutControl { Model: ILayoutElement previousModel }
            || grid.Children[index + 1] is not FrameworkElement next
            || next is not ILayoutControl { Model: ILayoutElement nextModel }
            || DockSize.Of(previousModel) is not DockSize previousSize
            || DockSize.Of(nextModel) is not DockSize nextSize)
            return;

        double previousActual = horizontal ? previous.ActualWidth : previous.ActualHeight;
        double nextActual = horizontal ? next.ActualWidth : next.ActualHeight;
        delta = Math.Clamp(delta, -Math.Max(0, previousActual - previousSize.Min(horizontal)), Math.Max(0, nextActual - nextSize.Min(horizontal)));
        if (delta != 0 && previousActual > 0 && nextActual > 0)
        {
            previousSize.Resize(previousActual, delta, horizontal);
            nextSize.Resize(nextActual, -delta, horizontal);
        }

        e.Handled = true;
    }

    /// <summary>
    /// The dock size of one of the layout elements a split holds. AvalonDock keeps their common
    /// interface internal, so this reaches the public members of each concrete type.
    /// </summary>
    private sealed class DockSize(Func<bool, GridLength> get, Action<bool, GridLength> set, Func<bool, double> min)
    {
        public static DockSize? Of(ILayoutElement element) => element switch
        {
            LayoutPanel panel => For(panel),
            LayoutDocumentPane documentPane => For(documentPane),
            LayoutAnchorablePane anchorablePane => For(anchorablePane),
            LayoutDocumentPaneGroup documentGroup => For(documentGroup),
            LayoutAnchorablePaneGroup anchorableGroup => For(anchorableGroup),
            _ => null,
        };

        public GridLength Get(bool horizontal) => get(horizontal);

        public double Min(bool horizontal) => min(horizontal);

        public void Resize(double actual, double delta, bool horizontal)
        {
            GridLength current = get(horizontal);
            GridLength resized = current.IsStar
                ? new GridLength(current.Value * (actual + delta) / actual, GridUnitType.Star)
                : new GridLength(Math.Max(0, (current.IsAuto ? actual : current.Value) + delta), GridUnitType.Pixel);
            set(horizontal, resized);
        }

        public static DockSize For<T>(LayoutPositionableGroup<T> group) where T : class, ILayoutElement => new(
            horizontal => horizontal ? group.DockWidth : group.DockHeight,
            (horizontal, value) =>
            {
                if (horizontal)
                    group.DockWidth = value;
                else
                    group.DockHeight = value;
            },
            horizontal => horizontal ? group.DockMinWidth : group.DockMinHeight);
    }

    private sealed record RenderedChild(INotifyPropertyChanged Element, DockSize Size, bool Horizontal);

    private sealed class RenderedPane(IEditorPane pane)
    {
        public IEditorPane Pane { get; } = pane;

        public LayoutAnchorable Anchorable { get; set; } = null!;

        /// <summary>The anchorable's pane, which keeps its size while the anchorable is hidden.</summary>
        public LayoutAnchorablePane Container { get; set; } = null!;

        /// <summary>Whether the pane's size along its split is a width.</summary>
        public bool Horizontal { get; set; }

        public PaneNode Node { get; set; } = null!;
    }
}
