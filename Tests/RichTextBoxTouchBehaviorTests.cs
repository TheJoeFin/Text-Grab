using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Text_Grab.Utilities;

namespace Tests;

public class RichTextBoxTouchBehaviorTests
{
    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnablingAndDisablingPreservePenGestureSettings(bool enabled)
    {
        RichTextBox box = new();
        Stylus.SetIsPressAndHoldEnabled(box, enabled);
        Stylus.SetIsFlicksEnabled(box, enabled);

        RichTextBoxTouchBehavior.SetIsEnabled(box, true);
        Assert.Equal(enabled, Stylus.GetIsPressAndHoldEnabled(box));
        Assert.Equal(enabled, Stylus.GetIsFlicksEnabled(box));

        RichTextBoxTouchBehavior.SetIsEnabled(box, false);
        Assert.Equal(enabled, box.ReadLocalValue(Stylus.IsPressAndHoldEnabledProperty));
        Assert.Equal(enabled, box.ReadLocalValue(Stylus.IsFlicksEnabledProperty));
    }

    [WpfFact]
    public void EnablingAndDisablingDoNotOverrideDefaultPenGestures()
    {
        RichTextBox box = new();
        bool pressAndHold = Stylus.GetIsPressAndHoldEnabled(box);
        bool flicks = Stylus.GetIsFlicksEnabled(box);

        RichTextBoxTouchBehavior.SetIsEnabled(box, true);
        Assert.Equal(pressAndHold, Stylus.GetIsPressAndHoldEnabled(box));
        Assert.Equal(flicks, Stylus.GetIsFlicksEnabled(box));
        Assert.Same(DependencyProperty.UnsetValue, box.ReadLocalValue(Stylus.IsPressAndHoldEnabledProperty));
        Assert.Same(DependencyProperty.UnsetValue, box.ReadLocalValue(Stylus.IsFlicksEnabledProperty));

        RichTextBoxTouchBehavior.SetIsEnabled(box, false);
        Assert.Same(DependencyProperty.UnsetValue, box.ReadLocalValue(Stylus.IsPressAndHoldEnabledProperty));
        Assert.Same(DependencyProperty.UnsetValue, box.ReadLocalValue(Stylus.IsFlicksEnabledProperty));
    }

    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TouchPansWithoutNativeScrollingOrSelectionRegardlessOfEnableOrder(bool touchFirst)
    {
        using TouchHost host = new(touchFirst);
        int manipulations = 0;
        Vector? nativeInertiaVelocity = null;
        host.Box.AddHandler(UIElement.ManipulationStartingEvent,
            new EventHandler<ManipulationStartingEventArgs>((_, _) => manipulations++), true);
        host.Box.AddHandler(UIElement.ManipulationInertiaStartingEvent,
            new EventHandler<ManipulationInertiaStartingEventArgs>((_, e) => nativeInertiaVelocity = e.TranslationBehavior.InitialVelocity), true);
        FakeTouch touch = host.CreateTouch(new Point(120, 180));
        TextPointer caret = host.Box.CaretPosition;

        touch.Down();
        try
        {
            for (int i = 1; i <= 5; i++)
            {
                touch.Move(new Point(120, 180 - 15 * i));
                DoEvents();
            }

            Assert.True(touch.WasPromotedToManipulation);
            Assert.True(manipulations > 0);
            Assert.True(Manipulation.IsManipulationActive(host.Box));
            Assert.Equal(60, host.ScrollViewer.VerticalOffset, 2);
            Assert.True(SmoothScrollBehavior.GetIsEnabled(host.Box));
            Assert.True(host.Box.Selection.IsEmpty);
        }
        finally
        {
            touch.Up();
        }

        Assert.Equal(default(Vector), nativeInertiaVelocity);
        Assert.Equal(0, caret.CompareTo(host.Box.CaretPosition));
        Assert.True(host.Box.Selection.IsEmpty);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReleasingOutsideTapSlopDoesNotPlaceTheCaret(bool hasSmallMove)
    {
        using TouchHost host = new();
        int taps = 0;
        RichTextBoxTouchBehavior.AddTappedHandler(host.Box, (_, _) => taps++);
        TextPointer caret = host.Box.CaretPosition;
        FakeTouch touch = host.CreateTouch(new Point(120, 180));

        touch.Down();
        if (hasSmallMove)
            touch.Move(new Point(120, 177));
        touch.Up(new Point(120, 100));
        DoEvents();

        Assert.Equal(0, taps);
        Assert.Equal(0, caret.CompareTo(host.Box.CaretPosition));
        Assert.True(host.Box.Selection.IsEmpty);
    }

    [WpfFact]
    public void LongPressDragExtendsSelectionWithoutPanningAwayFromEdges()
    {
        using TouchHost host = new();
        host.ScrollViewer.ScrollToVerticalOffset(300);
        host.Box.UpdateLayout();
        FakeTouch touch = host.CreateTouch(new Point(120, 180));

        touch.Down();
        try
        {
            // Fire the hold timer deterministically, without depending on wall-clock scheduling.
            Invoke(host.Controller, "HoldTimer_Tick", null, EventArgs.Empty);
            string word = host.Box.Selection.Text;
            Assert.NotEmpty(word);
            double offset = host.ScrollViewer.VerticalOffset;

            for (int i = 1; i <= 4; i++)
            {
                touch.Move(new Point(120, 180 - 20 * i));
                DoEvents();
            }

            Assert.Equal(offset, host.ScrollViewer.VerticalOffset, 2);
            Assert.Contains(word, host.Box.Selection.Text);
            Assert.NotEqual(word, host.Box.Selection.Text);
        }
        finally
        {
            touch.Up();
        }
    }

    [WpfFact]
    public void TouchingTheScrollbarStopsFlingBeforeItCanOverwriteTheDrag()
    {
        using TouchHost host = new();
        Invoke(host.Controller, "BeginScroll");
        Invoke(host.Controller, "StartFling", new Vector(0, -1));
        ScrollBar bar = FindVerticalScrollBar(host.ScrollViewer)!;
        Assert.NotNull(bar);
        FakeTouch touch = host.CreateTouch(bar.TranslatePoint(new Point(bar.ActualWidth / 2, bar.ActualHeight / 2), host.Box));

        touch.Down();
        try
        {
            host.ScrollViewer.ScrollToVerticalOffset(500);
            host.Box.UpdateLayout();
            Invoke(host.Controller, "CompositionTarget_Rendering", null, EventArgs.Empty);
            host.Box.UpdateLayout();

            Assert.Equal(500, host.ScrollViewer.VerticalOffset, 2);
        }
        finally
        {
            touch.Up();
        }
    }

    [WpfFact]
    public void FollowingALinkNavigatesToItsAddress()
    {
        Hyperlink link = new(new Run("example")) { NavigateUri = new Uri("https://example.com/") };
        new FlowDocument(new Paragraph(link));
        Uri? navigated = null;
        link.RequestNavigate += (_, e) => navigated = e.Uri;

        RichTextBoxTouchController.FollowLink(link);
        DoEvents();

        Assert.Equal(new Uri("https://example.com/"), navigated);
    }

    [WpfFact]
    public void FollowingAFootnoteReferenceRunsItsClick()
    {
        // Footnote references have no address; a click handler jumps to the note.
        Hyperlink link = new(new Run("1"));
        new FlowDocument(new Paragraph(link));
        int clicks = 0;
        link.Click += (_, _) => clicks++;

        RichTextBoxTouchController.FollowLink(link);
        DoEvents();

        Assert.Equal(1, clicks);
    }

    private static void DoEvents() =>
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.Background);

    private static void Invoke(object target, string method, params object?[] arguments) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, arguments);

    private static ScrollBar? FindVerticalScrollBar(DependencyObject element)
    {
        if (element is ScrollBar { Orientation: Orientation.Vertical, IsVisible: true } bar)
            return bar;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            if (FindVerticalScrollBar(VisualTreeHelper.GetChild(element, i)) is ScrollBar found)
                return found;
        }

        return null;
    }

    private sealed class TouchHost : IDisposable
    {
        private readonly HwndSource source;

        public RichTextBox Box { get; } = new() { Width = 400, Height = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        public ScrollViewer ScrollViewer { get; }
        public RichTextBoxTouchController Controller { get; }

        public TouchHost(bool touchFirst = true)
        {
            for (int i = 0; i < 100; i++)
                Box.Document.Blocks.Add(new Paragraph(new Run($"Line {i}: hello brave world")));

            if (touchFirst)
                RichTextBoxTouchBehavior.SetIsEnabled(Box, true);
            SmoothScrollBehavior.SetIsEnabled(Box, true);
            RichTextBoxTouchBehavior.SetIsEnabled(Box, true);

            source = new HwndSource(new HwndSourceParameters("Touch behavior test")
            {
                Width = 400,
                Height = 300,
                PositionX = -20000,
                PositionY = -20000,
                WindowStyle = unchecked((int)0x80000000),
            });
            source.RootVisual = Box;
            Box.Measure(new Size(400, 300));
            Box.Arrange(new Rect(0, 0, 400, 300));
            Box.UpdateLayout();
            DoEvents();

            ScrollViewer = WindowUtilities.GetScrollViewer(Box)!;
            DependencyProperty controllerProperty = (DependencyProperty)typeof(RichTextBoxTouchBehavior)
                .GetField("ControllerProperty", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            Controller = (RichTextBoxTouchController)Box.GetValue(controllerProperty);
        }

        public FakeTouch CreateTouch(Point position) => new(source, Box, position);

        public void Dispose()
        {
            RichTextBoxTouchBehavior.SetIsEnabled(Box, false);
            SmoothScrollBehavior.SetIsEnabled(Box, false);
            source.Dispose();
        }
    }

    private sealed class FakeTouch : TouchDevice
    {
        private static int nextId;
        private readonly UIElement owner;
        private Point position;
        private TouchAction action;

        public bool WasPromotedToManipulation { get; private set; }

        public FakeTouch(PresentationSource source, UIElement owner, Point position) : base(Interlocked.Increment(ref nextId))
        {
            this.owner = owner;
            this.position = position;
            SetActiveSource(source);
        }

        public override TouchPoint GetTouchPoint(IInputElement relativeTo) => new(this,
            owner.TranslatePoint(position, relativeTo as UIElement), new Rect(0, 0, 1, 1), action);

        public override TouchPointCollection GetIntermediateTouchPoints(IInputElement relativeTo) => [GetTouchPoint(relativeTo)];

        protected override void OnManipulationStarted() => WasPromotedToManipulation = true;

        public void Down()
        {
            action = TouchAction.Down;
            Activate();
            ReportDown();
        }

        public void Move(Point point)
        {
            position = point;
            action = TouchAction.Move;
            ReportMove();
        }

        public void Up(Point? point = null)
        {
            if (point is Point releasedAt)
                position = releasedAt;
            action = TouchAction.Up;
            ReportUp();
            Deactivate();
        }
    }
}
