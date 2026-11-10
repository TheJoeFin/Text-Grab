using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Threading;
using Text_Grab.Core.Abstractions;

namespace Text_Grab.Services;

/// <summary>
/// Raises <see cref="Triggered"/> once per file that is created or changed in a folder, after the
/// file has been quiet for the debounce interval (v5 F5).
///
/// <see cref="FileSystemWatcher"/> reports one save as several events, Created then one or more
/// Changed. Those coalesce per path: every new event restarts that path's quiet period, and the
/// first kind seen wins, so a new file is reported as <see cref="TriggerKind.FileCreated"/> even
/// though Changed events followed. A rename onto a matching name is reported as
/// <see cref="TriggerKind.FileCreated"/>: that is how browsers finish a download
/// (<c>.crdownload</c> → final name) and how many apps save.
///
/// Raised on a timer thread. The file may still be open in the writer when it fires; open it with
/// retry. Deletions and directories are not reported. If the watcher reports an error (its buffer
/// overflowed), nothing is raised for it and a fresh watcher takes over; events lost in the
/// overflow are not recovered.
/// </summary>
public sealed class FolderWatcherService : ITriggerSource
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(500);

    private readonly string _path;
    private readonly string _filter;
    private readonly bool _includeSubdirectories;
    private readonly TimeSpan _debounce;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Dictionary<string, Pending> _pending = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;
    private bool _disposed;
    private int _observedEvents;

    /// <param name="path">The absolute path of an existing folder.</param>
    /// <param name="filter">A file-name pattern such as <c>*.png</c>; <c>*</c> matches everything.</param>
    /// <param name="includeSubdirectories">Also watch every folder below <paramref name="path"/>.</param>
    /// <param name="debounce">How long a file must be quiet before it is reported.</param>
    /// <param name="timeProvider">The clock for the quiet period. Tests pass a fake one.</param>
    public FolderWatcherService(
        string path,
        string filter = "*",
        bool includeSubdirectories = false,
        TimeSpan? debounce = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException("The folder path must be absolute.", nameof(path));

        _path = Path.GetFullPath(path);
        _filter = string.IsNullOrWhiteSpace(filter) ? "*" : filter;
        _includeSubdirectories = includeSubdirectories;
        _debounce = debounce ?? DefaultDebounce;
        _time = timeProvider ?? TimeProvider.System;

        if (_debounce < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(debounce), _debounce, "The debounce interval cannot be negative.");
    }

    public event EventHandler<TriggerPayload>? Triggered;

    /// <summary>Paths waiting out their quiet period. For tests.</summary>
    internal int PendingCount
    {
        get { lock (_gate) return _pending.Count; }
    }

    /// <summary>Matching events seen since construction. For tests, to know the file system has reported.</summary>
    internal int ObservedEventCount => Volatile.Read(ref _observedEvents);

    /// <summary>The live watcher, or null when stopped. For tests.</summary>
    internal FileSystemWatcher? CurrentWatcher
    {
        get { lock (_gate) return _watcher; }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The folder does not exist.</exception>
    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _watcher ??= CreateWatcher();
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_gate)
        {
            DisposeWatcher();

            foreach (Pending pending in _pending.Values)
                pending.Timer?.Dispose();
            _pending.Clear();
        }
    }

    public void Dispose()
    {
        lock (_gate)
            _disposed = true;

        Stop();
    }

    /// <summary>The watcher's Error event, reachable for tests.</summary>
    internal void OnWatcherError(object? sender)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(sender, _watcher))
                return;

            DisposeWatcher();
            try
            {
                _watcher = CreateWatcher();
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // The folder is gone or no longer readable; stay stopped.
            }
        }
    }

    private FileSystemWatcher CreateWatcher()
    {
        FileSystemWatcher watcher = new(_path)
        {
            Filter = _filter,
            IncludeSubdirectories = _includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };

        watcher.Created += (sender, e) => Observe(sender, e.FullPath, TriggerKind.FileCreated);
        watcher.Changed += (sender, e) => Observe(sender, e.FullPath, TriggerKind.FileChanged);
        watcher.Renamed += (sender, e) => Observe(sender, e.FullPath, TriggerKind.FileCreated);
        watcher.Error += (sender, _) => OnWatcherError(sender);
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void DisposeWatcher()
    {
        if (_watcher is null)
            return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    private void Observe(object? sender, string fullPath, TriggerKind kind)
    {
        // The watcher's own filter is applied to both names of a rename; this applies it to the
        // name the file has now.
        if (!FileSystemName.MatchesSimpleExpression(_filter, Path.GetFileName(fullPath)))
            return;

        if (Directory.Exists(fullPath))
            return;

        lock (_gate)
        {
            // A late event from a watcher that has been stopped or replaced.
            if (_watcher is null || !ReferenceEquals(sender, _watcher))
                return;

            Interlocked.Increment(ref _observedEvents);
            DateTimeOffset now = _time.GetUtcNow();

            if (_pending.TryGetValue(fullPath, out Pending? pending))
            {
                pending.LastEventAt = now;
                pending.Timer?.Change(_debounce, Timeout.InfiniteTimeSpan);
                return;
            }

            pending = new Pending(fullPath, kind, now);
            _pending[fullPath] = pending;
            pending.Timer = _time.CreateTimer(OnQuiet, pending, _debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnQuiet(object? state)
    {
        Pending pending = (Pending)state!;

        lock (_gate)
        {
            if (!_pending.TryGetValue(pending.Path, out Pending? current) || !ReferenceEquals(current, pending))
                return;

            // An event re-armed the timer just as it fired; the re-armed timer will report it.
            if (_time.GetUtcNow() - pending.LastEventAt < _debounce)
                return;

            _pending.Remove(pending.Path);
            pending.Timer?.Dispose();
        }

        Triggered?.Invoke(this, new TriggerPayload(pending.Kind, FilePath: pending.Path));
    }

    private sealed class Pending(string path, TriggerKind kind, DateTimeOffset lastEventAt)
    {
        public string Path { get; } = path;
        public TriggerKind Kind { get; } = kind;
        public DateTimeOffset LastEventAt { get; set; } = lastEventAt;
        public ITimer? Timer { get; set; }
    }
}
