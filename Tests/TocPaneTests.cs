using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab.Core.Editing;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Panes;
using Text_Grab.Models;
using Text_Grab.Services;

namespace Tests;

/// <summary>The table of contents pane: the headings it lists, when it reads them, and where a click goes.</summary>
public class TocPaneTests
{
    private sealed class FakeSession(string text) : IEditorSession
    {
        public string Text { get => text; set { text = value; TextChanged?.Invoke(this, EventArgs.Empty); } }

        public EtwEditorMode Mode { get; set; } = EtwEditorMode.Markdown;

        public TextSpan Selection { get; set; }

        public string SelectedText => string.Empty;

        public CultureInfo Culture => CultureInfo.InvariantCulture;

        public IReadOnlyList<double>? SelectedCellNumbers => null;

        public double FirstVisibleLine => 0;

        public List<TextSpan> SelectInEditorCalls { get; } = [];

        public event EventHandler? SelectedCellsChanged { add { } remove { } }

        public event EventHandler? ViewportChanged { add { } remove { } }

        public event EventHandler? TextChanged;

        public event EventHandler? ModeChanged;

        public int TextChangedSubscribers => TextChanged?.GetInvocationList().Length ?? 0;

        public void RaiseModeChanged() => ModeChanged?.Invoke(this, EventArgs.Empty);

        public void ReplaceSelection(string replacement) { }

        public void ReplaceRange(TextSpan range, string replacement) { }

        public void AppendText(string newText) { }

        public string GetSelectedTextOrAllText() => text;

        public void ReplaceSelectedTextOrAllText(string replacement) { }

        public void SelectInEditor(TextSpan range) => SelectInEditorCalls.Add(range);

        public void ScrollToEnd() { }

        public bool ScrollByWheel(int wheelDelta) => false;
    }

    private const string Document = "# Intro\ntext\n## Setup\n## Usage\n# Reference\n";

    private static TocPane AttachedPane(FakeSession session)
    {
        TocPane pane = new();
        pane.Attach(session, null);
        return pane;
    }

    [Fact]
    public void ThePaneIsKnownToTheAppAndIdentifiedByItsPinnedId()
    {
        Assert.Equal("textgrab.pane.toc", PaneIds.Toc);
        Assert.Equal(PaneIds.Toc, TocPane.PaneId);
        Assert.Contains(AppServices.Provider.GetServices<IEditorPaneFactory>(), factory => factory.Descriptor.Id == PaneIds.Toc);
    }

    [WpfFact]
    public void AttachingListsTheHeadingsAsANestedTree()
    {
        FakeSession session = new(Document);
        TocPane pane = AttachedPane(session);

        Assert.Equal(2, pane.Nodes.Count);
        Assert.Equal(["Intro", "Reference"], pane.Nodes.Select(node => node.Title));
        Assert.Equal(["Setup", "Usage"], pane.Nodes[0].Children.Select(node => node.Title));
        Assert.Equal(Visibility.Collapsed, pane.Placeholder.Visibility);
        pane.Detach();
    }

    [WpfFact]
    public void ADocumentWithNoHeadingsShowsThePlaceholderInsteadOfATree()
    {
        FakeSession session = new("just text");
        TocPane pane = AttachedPane(session);

        Assert.Empty(pane.Nodes);
        Assert.Equal(Visibility.Visible, pane.Placeholder.Visibility);

        session.Text = "# Now one";
        pane.Refresh();

        Assert.Single(pane.Nodes);
        Assert.Equal(Visibility.Collapsed, pane.Placeholder.Visibility);
        pane.Detach();
    }

    [WpfFact]
    public void TypingInASectionKeepsTheTree_ButRenamingAHeadingMakesANewOne()
    {
        FakeSession session = new(Document);
        TocPane pane = AttachedPane(session);
        object? tree = pane.TreeControl.ItemsSource;

        session.Text = Document.Replace("text", "much more text", StringComparison.Ordinal);
        pane.Refresh();

        Assert.Same(tree, pane.TreeControl.ItemsSource);

        session.Text = Document.Replace("Usage", "Using it", StringComparison.Ordinal);
        pane.Refresh();

        Assert.NotSame(tree, pane.TreeControl.ItemsSource);
        Assert.Equal(["Setup", "Using it"], pane.Nodes[0].Children.Select(node => node.Title));
        pane.Detach();
    }

    [WpfFact]
    public void AChangeIsReadAfterTheTypingDelay_NotAtOnce()
    {
        FakeSession session = new(Document);
        TocPane pane = AttachedPane(session);

        session.Text = Document + "# Added\n";

        Assert.True(pane.IsStale);
        Assert.Equal(2, pane.Nodes.Count);

        pane.Refresh();

        Assert.False(pane.IsStale);
        Assert.Equal(3, pane.Nodes.Count);
        pane.Detach();
    }

    [WpfFact]
    public void AHiddenPaneReadsNothing_AndCatchesUpWhenShown()
    {
        FakeSession session = new(Document);
        TocPane pane = AttachedPane(session);
        pane.OnVisibilityChanged(false);

        session.Text = Document + "# Added\n";
        pane.Refresh();

        Assert.True(pane.IsStale);
        Assert.Equal(2, pane.Nodes.Count);

        pane.OnVisibilityChanged(true);

        Assert.False(pane.IsStale);
        Assert.Equal(3, pane.Nodes.Count);
        pane.Detach();
    }

    [WpfFact]
    public void GoingToAHeadingSelectsItsLineStartInTheEditor()
    {
        FakeSession session = new(Document);
        TocPane pane = AttachedPane(session);

        Assert.True(pane.GoToHeading(pane.Nodes[0].Children[1]));

        Assert.Equal([new TextSpan(Document.IndexOf("## Usage", StringComparison.Ordinal), 0)], session.SelectInEditorCalls);
        pane.Detach();
    }

    [WpfFact]
    public void AHeadingThatMovedSinceTheTreeWasMadeIsFoundWhereItIsNow()
    {
        FakeSession session = new(Document);
        TocPane pane = AttachedPane(session);
        MarkdownOutlineNode reference = pane.Nodes[1];

        // Text typed above the heading shifts its offset, and nothing has refreshed the pane yet.
        session.Text = "A new first paragraph.\n\n" + Document;

        Assert.True(pane.GoToHeading(reference));

        Assert.Equal(session.Text.IndexOf("# Reference", StringComparison.Ordinal), session.SelectInEditorCalls.Single().Start);
        pane.Detach();
    }

    [WpfFact]
    public void AHeadingThatIsGoneGoesNowhere_AndTheTreeCatchesUp()
    {
        FakeSession session = new(Document);
        TocPane pane = AttachedPane(session);
        MarkdownOutlineNode reference = pane.Nodes[1];

        session.Text = "# Intro\n";

        Assert.False(pane.GoToHeading(reference));

        Assert.Empty(session.SelectInEditorCalls);
        Assert.Single(pane.Nodes);
        pane.Detach();
    }

    [WpfFact]
    public void ClickingTheSameHeadingTwiceGoesThereTwice()
    {
        FakeSession session = new(Document);
        TocPane pane = AttachedPane(session);

        Assert.True(pane.GoToHeading(pane.Nodes[1]));
        Assert.True(pane.GoToHeading(pane.Nodes[1]));

        Assert.Equal(2, session.SelectInEditorCalls.Count);
        pane.Detach();
    }

    [WpfFact]
    public void EnterOnTheSelectedItemGoesToItsHeading()
    {
        FakeSession session = new(Document);
        TocPane pane = AttachedPane(session);
        pane.Measure(new Size(300, 300));
        pane.Arrange(new Rect(0, 0, 300, 300));
        pane.UpdateLayout();

        TreeViewItem item = (TreeViewItem)pane.TreeControl.ItemContainerGenerator.ContainerFromIndex(1);
        item.IsSelected = true;
        KeyEventArgs enter = new(Keyboard.PrimaryDevice, new TestKeySource(), 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        pane.TreeControl.RaiseEvent(enter);

        Assert.True(enter.Handled);
        Assert.Equal(Document.IndexOf("# Reference", StringComparison.Ordinal), session.SelectInEditorCalls.Single().Start);
        pane.Detach();
    }

    [WpfFact]
    public void DetachingLetsGoOfTheSession_AndTheNextAttachWorks()
    {
        FakeSession session = new(Document);
        TocPane pane = AttachedPane(session);
        int attached = TocPane.AttachedCount;

        pane.Detach();

        Assert.Equal(attached - 1, TocPane.AttachedCount);
        Assert.Equal(0, session.TextChangedSubscribers);
        Assert.Empty(pane.Nodes);
        Assert.Null(pane.TreeControl.ItemsSource);
        Assert.False(pane.GoToHeading(new MarkdownOutlineNode(new MarkdownHeading(1, "x", 0), 0, [])));

        pane.Attach(session, null);

        Assert.Equal(2, pane.Nodes.Count);
        pane.Detach();
    }

    [WpfFact]
    public void AttachingTwiceIsAnError()
    {
        FakeSession session = new(Document);
        TocPane pane = AttachedPane(session);

        Assert.Throws<InvalidOperationException>(() => pane.Attach(session, null));
        pane.Detach();
    }

    private sealed class TestKeySource : PresentationSource
    {
        public override Visual? RootVisual { get; set; }

        public override bool IsDisposed => false;

        protected override CompositionTarget? GetCompositionTargetCore() => null;
    }
}
