namespace Text_Grab.Tests.Core;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock only moves when a test calls <see cref="Advance"/>.
/// Timers that come due run synchronously on the advancing thread, outside the provider's lock.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
            return _now;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(this, callback, state);
        lock (_gate)
            _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _now += by;
            due = [.. _timers.Where(t => t.DueAt is DateTimeOffset at && at <= _now)];
            foreach (ManualTimer timer in due)
                timer.DueAt = timer.Period == Timeout.InfiniteTimeSpan ? null : _now + timer.Period;
        }

        foreach (ManualTimer timer in due)
            timer.Fire();
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; set; }
        public TimeSpan Period { get; private set; } = Timeout.InfiniteTimeSpan;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                Period = period;
                DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
            }
            return true;
        }

        public void Fire() => callback(state);

        public void Dispose()
        {
            lock (owner._gate)
            {
                DueAt = null;
                owner._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
