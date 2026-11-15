using System.Windows;
using Text_Grab.Utilities;

namespace Tests;

public class TouchGestureTrackerTests
{
    [Fact]
    public void QuickTouchThatBarelyMovesIsATap()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(100, 100), 1000);
        Assert.Equal(default, tracker.Move(new Point(104, 103), 1040));

        TouchGestureOutcome outcome = tracker.Up(new Point(104, 103), 1080, out Vector velocity);

        Assert.Equal(TouchGestureOutcome.Tap, outcome);
        Assert.Equal(default, velocity);
        Assert.Equal(TouchGestureState.Idle, tracker.State);
    }

    [Theory]
    [InlineData(TouchGestureTracker.Slop, true)]
    [InlineData(TouchGestureTracker.Slop + 1, false)]
    public void ReleasePositionParticipatesInTheTapSlopCheck(double distance, bool isTap)
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(100, 100), 0);

        TouchGestureOutcome outcome = tracker.Up(new Point(100, 100 + distance), 100, out Vector velocity);

        Assert.Equal(isTap ? TouchGestureOutcome.Tap : TouchGestureOutcome.PanEnded, outcome);
        Assert.Equal(default, velocity);
        Assert.Equal(TouchGestureState.Idle, tracker.State);
    }

    [Fact]
    public void PanStartsPastTheSlopWithoutJumpingThenFollowsTheFinger()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(100, 300), 0);

        // Crossing the slop starts the pan but moves nothing, so the text doesn't jump by the slop.
        Assert.Equal(default, tracker.Move(new Point(100, 300 - TouchGestureTracker.Slop - 1), 16));
        Assert.Equal(TouchGestureState.Panning, tracker.State);

        Assert.Equal(new Vector(0, -20), tracker.Move(new Point(100, 300 - TouchGestureTracker.Slop - 21), 32));
    }

    [Fact]
    public void MostlyVerticalPanIgnoresSidewaysDrift()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(100, 300), 0);
        tracker.Move(new Point(102, 280), 16);

        Assert.Equal(PanAxes.Vertical, tracker.Axes);
        Assert.Equal(new Vector(0, -30), tracker.Move(new Point(107, 250), 32));
    }

    [Fact]
    public void DiagonalPanMovesBothWays()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(100, 100), 0);
        tracker.Move(new Point(115, 115), 16);

        Assert.Equal(PanAxes.Both, tracker.Axes);
        Assert.Equal(new Vector(10, 5), tracker.Move(new Point(125, 120), 32));
    }

    [Fact]
    public void HoldingStillBecomesALongPressThatEndsAsOne()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(50, 50), 0);

        Assert.True(tracker.TryLongPress());
        Assert.Equal(TouchGestureState.LongPressed, tracker.State);

        // Dragging after the long press extends a selection; it doesn't pan.
        Assert.Equal(default, tracker.Move(new Point(150, 90), 700));
        Assert.Equal(TouchGestureState.LongPressed, tracker.State);
        Assert.Equal(TouchGestureOutcome.LongPressEnded, tracker.Up(new Point(150, 90), 800, out _));
    }

    [Fact]
    public void ALongPressCannotStartOnceThePanHas()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(50, 50), 0);
        tracker.Move(new Point(50, 80), 100);

        Assert.False(tracker.TryLongPress());
        Assert.Equal(TouchGestureState.Panning, tracker.State);
    }

    [Fact]
    public void ReleaseAfterTheHoldTimeIsALongPressEvenIfTheTimerNeverRan()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(50, 50), 1000);

        Assert.Equal(TouchGestureOutcome.LongPressEnded,
            tracker.Up(new Point(50, 50), 1000 + TouchGestureTracker.HoldMilliseconds, out _));
    }

    [Fact]
    public void TimestampsThatWrapAroundStillMeasureTheHold()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(50, 50), int.MaxValue - 100);

        Assert.Equal(TouchGestureOutcome.Tap, tracker.Up(new Point(50, 50), unchecked(int.MaxValue + 100), out _));
    }

    [Fact]
    public void FlickReleasesWithTheFingersSpeed()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(100, 500), 0);
        for (int time = 8; time <= 96; time += 8)
            tracker.Move(new Point(100, 500 - time * 2), time);

        Assert.Equal(TouchGestureOutcome.PanEnded, tracker.Up(new Point(100, 500 - 96 * 2), 96, out Vector velocity));
        Assert.Equal(0, velocity.X);
        Assert.Equal(-2, velocity.Y, 3);
    }

    [Fact]
    public void PausingBeforeLiftingReleasesWithoutSpeed()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(100, 500), 0);
        tracker.Move(new Point(100, 450), 16);
        tracker.Move(new Point(100, 400), 32);

        tracker.Up(new Point(100, 400), 400, out Vector velocity);

        Assert.Equal(default, velocity);
    }

    [Fact]
    public void OutOfOrderTimestampsNeverMakeSpeedFromNothing()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(100, 500), 100);
        tracker.Move(new Point(100, 450), 116);
        tracker.Move(new Point(100, 400), 90);

        tracker.Up(new Point(100, 400), 90, out Vector velocity);

        Assert.True(double.IsFinite(velocity.Y));
        Assert.True(velocity.Y <= 0);
    }

    [Fact]
    public void CancelForgetsTheTouch()
    {
        TouchGestureTracker tracker = new();
        tracker.Down(new Point(100, 100), 0);
        tracker.Cancel();

        Assert.Equal(TouchGestureState.Idle, tracker.State);
        Assert.Equal(TouchGestureOutcome.None, tracker.Up(new Point(100, 100), 50, out _));
    }
}
