using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Core.Editing;
using Text_Grab.Core.Layouts;

namespace Text_Grab.Editing.Panes;

/// <summary>
/// The table of contents pane: the document's headings as a tree. Clicking one, or Enter on it, moves
/// the caret in the editor to that heading and scrolls it into view.
/// </summary>
/// <remarks>
/// <para>
/// The headings come from <see cref="MarkdownHeadings"/>, read from the session's text in every mode, so
/// the pane works in a layout that has it beside a Code or Text editor too. The tree is made again only
/// when the headings' levels or titles change, so typing in a section leaves what the person has
/// collapsed and selected alone. Changes are read after a short typing delay; a pane that is not
/// visible reads nothing and catches up when it is shown.
/// </para>
/// <para>
/// The pane keeps no offsets. A click reads the document again and finds the heading in that, so a
/// heading that moved since the tree was made is still the one that is shown. Nothing here is tied to
/// <c>Loaded</c> or <c>Unloaded</c>, because AvalonDock raises both again whenever it re-parents the view.
/// </para>
/// </remarks>
public partial class TocPane : UserControl, IEditorPane
{
    public const string PaneId = PaneIds.Toc;

    public static readonly PaneDescriptor Definition = new(
        PaneId, "Contents", "TextBulletListTree24", PaneDock.Left, DefaultSize: 240, MinSize: 160);

    private static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(250);

    private static int attachedCount;
    private static int createdCount;

    private DispatcherTimer? delayTimer;
    private IEditorSession? session;
    private IReadOnlyList<MarkdownHeading> headings = [];
    private IReadOnlyList<MarkdownOutlineNode> nodes = [];
    private bool isActive = true;
    private bool isStale;

    public TocPane()
    {
        Interlocked.Increment(ref createdCount);
        InitializeComponent();

        // Every branch starts open. The style builds on the app's themed TreeViewItem (WPF-UI's Fluent
        // one) when there is one; an item style set without BasedOn would drop it for the plain WPF look.
        Style itemStyle = new(typeof(TreeViewItem), TryFindResource(typeof(TreeViewItem)) as Style);
        itemStyle.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, true));
        Tree.ItemContainerStyle = itemStyle;
    }

    public PaneDescriptor Descriptor => Definition;

    public FrameworkElement View => this;

    /// <summary>How many table of contents panes are attached to a window. For tests that check a pane is not made or kept needlessly.</summary>
    internal static int AttachedCount => Volatile.Read(ref attachedCount);

    /// <summary>How many table of contents panes have been made, attached or not.</summary>
    internal static int CreatedCount => Volatile.Read(ref createdCount);

    internal TreeView TreeControl => Tree;

    internal TextBlock Placeholder => PlaceholderText;

    /// <summary>The headings the tree was last made from.</summary>
    internal IReadOnlyList<MarkdownHeading> Headings => headings;

    /// <summary>The tree's top-level nodes.</summary>
    internal IReadOnlyList<MarkdownOutlineNode> Nodes => nodes;

    /// <summary>True while a change is waiting to be read, or the pane is hidden and has missed one.</summary>
    internal bool IsStale => isStale;

    public void Attach(IEditorSession session, JsonElement? config)
    {
        if (this.session is not null)
            throw new InvalidOperationException("The table of contents pane is already attached.");

        this.session = session;
        Interlocked.Increment(ref attachedCount);

        session.TextChanged += Session_Changed;
        session.ModeChanged += Session_Changed;
        Refresh();
    }

    public void Detach()
    {
        if (session is null)
            return;

        session.TextChanged -= Session_Changed;
        session.ModeChanged -= Session_Changed;
        delayTimer?.Stop();

        headings = [];
        nodes = [];
        Tree.ItemsSource = null;
        isStale = false;
        session = null;
        Interlocked.Decrement(ref attachedCount);
    }

    /// <summary>Nothing: the pane's width and whether it is shown are the host's, and the tree follows the document.</summary>
    public JsonElement? SaveConfig() => null;

    /// <summary>A hidden pane reads nothing; a shown one catches up with what it missed.</summary>
    public void OnVisibilityChanged(bool visible)
    {
        isActive = visible;
        if (!visible)
        {
            delayTimer?.Stop();
            return;
        }

        if (isStale)
            Refresh();
    }

    /// <summary>Reads the headings from the document now and shows them.</summary>
    internal void Refresh()
    {
        delayTimer?.Stop();
        if (session is null)
            return;

        if (!isActive)
        {
            isStale = true;
            return;
        }

        isStale = false;
        IReadOnlyList<MarkdownHeading> current = MarkdownHeadings.Parse(session.Text);
        bool sameShape = MarkdownOutline.HaveSameShape(headings, current);
        headings = current;

        if (!sameShape)
        {
            nodes = MarkdownOutline.Build(current);
            Tree.ItemsSource = nodes;
        }

        PlaceholderText.Visibility = current.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Moves the caret in the editor to the start of the heading <paramref name="node"/> stands for. The
    /// document is read again, so the heading is found where it is now. False when it is gone.
    /// </summary>
    internal bool GoToHeading(MarkdownOutlineNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (session is null)
            return false;

        IReadOnlyList<MarkdownHeading> current = MarkdownHeadings.Parse(session.Text);
        MarkdownHeading? target = node.Index < current.Count && Matches(current[node.Index], node.Heading)
            ? current[node.Index]
            : current.FirstOrDefault(heading => Matches(heading, node.Heading));

        if (target is null)
        {
            // What the tree shows is out of date; bring it back in line.
            Refresh();
            return false;
        }

        session.SelectInEditor(new TextSpan(target.Offset, 0));
        return true;
    }

    private static bool Matches(MarkdownHeading heading, MarkdownHeading wanted)
        => heading.Level == wanted.Level && string.Equals(heading.Title, wanted.Title, StringComparison.Ordinal);

    private void Session_Changed(object? sender, EventArgs e)
    {
        if (!isActive)
        {
            isStale = true;
            return;
        }

        isStale = true;
        delayTimer ??= CreateTimer();
        delayTimer.Stop();
        delayTimer.Start();
    }

    private DispatcherTimer CreateTimer()
    {
        DispatcherTimer timer = new(DispatcherPriority.Background, Dispatcher) { Interval = TypingDelay };
        timer.Tick += (_, _) => Refresh();
        return timer;
    }

    private void Tree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // The expander arrow opens and closes a branch; it does not go anywhere.
        if (FindAncestor<ToggleButton>(e.OriginalSource) is not null || FindAncestor<ScrollBar>(e.OriginalSource) is not null)
            return;

        if (FindAncestor<TreeViewItem>(e.OriginalSource) is { DataContext: MarkdownOutlineNode node })
            GoToHeading(node);
    }

    private void Tree_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || Tree.SelectedItem is not MarkdownOutlineNode node)
            return;

        if (GoToHeading(node))
            e.Handled = true;
    }

    private static T? FindAncestor<T>(object? source)
        where T : DependencyObject
    {
        for (DependencyObject? current = source as DependencyObject; current is not null;)
        {
            if (current is T match)
                return match;

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
