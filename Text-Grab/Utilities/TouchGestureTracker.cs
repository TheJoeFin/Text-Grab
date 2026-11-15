using System;
using System.Collections.Generic;
using System.Windows;

namespace Text_Grab.Utilities;

internal enum TouchGestureState
{
    Idle,
    /// <summary>Down, but neither moved past the slop nor held long enough to mean anything yet.</summary>
    Pending,
    Panning,
    LongPressed,
}

/// <summary>What a finished touch turned out to be.</summary>
internal enum TouchGestureOutcome
{
    None,
    Tap,
    PanEnded,
    LongPressEnded,
}

/// <summary>Which way a pan may move, settled by its first movement so a vertical swipe doesn't drift sideways.</summary>
[Flags]
internal enum PanAxes
{
    None = 0,
    Horizontal = 1,
    Vertical = 2,
    Both = Horizontal | Vertical,
}

/// <summary>
/// Classifies one finger: a tap, a pan once it moves past the slop, or a long press once it holds still
/// for the hold time. Pure state fed positions and input event timestamps (milliseconds), so it tests
/// without a touch screen.
/// </summary>
internal sealed class TouchGestureTracker
{
    /// <summary>How far (DIPs) a finger may wander and still be a tap or a long press.</summary>
    public const double Slop = 8;

    public const int HoldMilliseconds = 500;

    // A pan sticks to one axis when its first movement leans this strongly towards it.
    private const double AxisLockRatio = 2;

    // Release velocity is measured over the last stretch of movement, so a finger that slows
    // or stops before lifting doesn't fling.
    private const int VelocityWindowMilliseconds = 80;

    private readonly List<(int Time, Point Position)> samples = [];

    public TouchGestureState State { get; private set; } = TouchGestureState.Idle;

    public Point StartPosition { get; private set; }

    public Point LastPosition { get; private set; }

    public PanAxes Axes { get; private set; } = PanAxes.None;

    private int startTime;

    public void Down(Point position, int time)
    {
        State = TouchGestureState.Pending;
        StartPosition = position;
        LastPosition = position;
        Axes = PanAxes.None;
        startTime = time;
        samples.Clear();
        samples.Add((time, position));
    }

    /// <summary>
    /// Feeds a move and returns how far the content should follow it. That is zero until a pan starts,
    /// and zero on the move that starts it, so the content doesn't jump by the slop.
    /// </summary>
    public Vector Move(Point position, int time)
    {
        if (State == TouchGestureState.Idle)
            return default;

        Point previous = LastPosition;
        LastPosition = position;
        AddSample(position, time);

        switch (State)
        {
            case TouchGestureState.Pending:
                Vector travelled = position - StartPosition;
                if (travelled.Length <= Slop)
                    return default;

                State = TouchGestureState.Panning;
                Axes = Math.Abs(travelled.Y) > Math.Abs(travelled.X) * AxisLockRatio ? PanAxes.Vertical
                    : Math.Abs(travelled.X) > Math.Abs(travelled.Y) * AxisLockRatio ? PanAxes.Horizontal
                    : PanAxes.Both;
                return default;

            case TouchGestureState.Panning:
                return Constrain(position - previous);

            default:
                return default;
        }
    }

    /// <summary>
    /// Turns a still-pending touch into a long press. The caller's hold timer decides when: input timestamps
    /// aren't guaranteed to share a clock with timers, so this doesn't compare the two.
    /// </summary>
    public bool TryLongPress()
    {
        if (State != TouchGestureState.Pending)
            return false;

        State = TouchGestureState.LongPressed;
        return true;
    }

    /// <summary>
    /// Ends the touch, checking the release position against tap slop even if move events were missed.
    /// <paramref name="releaseVelocity"/> is in DIPs per millisecond, along the pan's axes.
    /// </summary>
    public TouchGestureOutcome Up(Point position, int time, out Vector releaseVelocity)
    {
        releaseVelocity = default;
        TouchGestureState state = State;
        State = TouchGestureState.Idle;

        switch (state)
        {
            case TouchGestureState.Pending:
                if ((position - StartPosition).Length > Slop)
                    return TouchGestureOutcome.PanEnded;

                // The hold timer may not have had its turn on a busy dispatcher; the clock decides.
                return unchecked(time - startTime) >= HoldMilliseconds
                    ? TouchGestureOutcome.LongPressEnded
                    : TouchGestureOutcome.Tap;

            case TouchGestureState.Panning:
                AddSample(position, time);
                releaseVelocity = Constrain(GetVelocity());
                return TouchGestureOutcome.PanEnded;

            case TouchGestureState.LongPressed:
                return TouchGestureOutcome.LongPressEnded;

            default:
                return TouchGestureOutcome.None;
        }
    }

    public void Cancel()
    {
        State = TouchGestureState.Idle;
        samples.Clear();
    }

    private void AddSample(Point position, int time)
    {
        // Input timestamps can repeat or arrive out of order; never let time run backwards.
        if (samples.Count > 0 && unchecked(time - samples[^1].Time) < 0)
            time = samples[^1].Time;

        samples.Add((time, position));
        while (samples.Count > 2 && unchecked(time - samples[1].Time) > VelocityWindowMilliseconds)
            samples.RemoveAt(0);
    }

    private Vector GetVelocity()
    {
        // Oldest sample still inside the window that ends at release, so a pause before lifting reads as no velocity.
        int now = samples[^1].Time;
        int first = samples.FindIndex(sample => unchecked(now - sample.Time) <= VelocityWindowMilliseconds);
        if (first < 0 || first >= samples.Count - 1)
            return default;

        (int startTime, Point startPosition) = samples[first];
        (int endTime, Point endPosition) = samples[^1];
        int elapsed = unchecked(endTime - startTime);
        return elapsed <= 0 ? default : (endPosition - startPosition) / elapsed;
    }

    private Vector Constrain(Vector delta) => new(
        Axes.HasFlag(PanAxes.Horizontal) ? delta.X : 0,
        Axes.HasFlag(PanAxes.Vertical) ? delta.Y : 0);
}
