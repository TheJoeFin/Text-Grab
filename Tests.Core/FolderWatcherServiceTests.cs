using System.Collections.Concurrent;
using System.Diagnostics;
using Text_Grab.Core.Abstractions;
using Text_Grab.Services;

namespace Text_Grab.Tests.Core;

/// <summary>
/// <see cref="FolderWatcherService"/> (v5 F5) on a real temp folder with a fake clock. The file
/// system reports asynchronously, so each test waits - in real time - until the service has seen
/// the events, then moves the fake clock to decide when the quiet period ends.
/// </summary>
public sealed class FolderWatcherServiceTests : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"tg-folder-watch-{Guid.NewGuid():N}");
    private readonly ManualTimeProvider _time = new();
    private readonly ConcurrentQueue<TriggerPayload> _raised = new();

    public FolderWatcherServiceTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A watcher handle can linger briefly; the temp folder is not worth failing a test over.
        }
    }

    [Fact]
    public void SingleWrite_RaisesOneFileCreated_AfterTheQuietPeriod()
    {
        using FolderWatcherService watcher = StartWatcher();
        string path = Path.Combine(_folder, "scan.png");

        Write(watcher, path, "one");
        _time.Advance(Debounce - TimeSpan.FromMilliseconds(1));
        Assert.Empty(_raised);

        _time.Advance(TimeSpan.FromMilliseconds(1));

        TriggerPayload payload = Assert.Single(_raised);
        Assert.Equal(new TriggerPayload(TriggerKind.FileCreated, FilePath: path), payload);
        Assert.Equal(0, watcher.PendingCount);
    }

    [Fact]
    public void BurstOfWritesToOneFile_RaisesOnce_AfterTheLastWriteIsQuiet()
    {
        using FolderWatcherService watcher = StartWatcher();
        string path = Path.Combine(_folder, "growing.txt");

        Write(watcher, path, "a");
        for (int i = 0; i < 3; i++)
        {
            _time.Advance(Debounce / 2);
            Write(watcher, path, $"more {i}", append: true);
        }

        // Each write restarted the quiet period, so half an interval after the last one is too soon.
        _time.Advance(Debounce / 2);
        Assert.Empty(_raised);

        _time.Advance(Debounce / 2);

        TriggerPayload payload = Assert.Single(_raised);
        Assert.Equal(TriggerKind.FileCreated, payload.Kind);
        Assert.Equal(path, payload.FilePath);
    }

    [Fact]
    public void TwoFiles_RaiseOneEventEach()
    {
        using FolderWatcherService watcher = StartWatcher();
        string first = Path.Combine(_folder, "first.txt");
        string second = Path.Combine(_folder, "second.txt");

        Write(watcher, first, "1");
        Write(watcher, second, "2");
        _time.Advance(Debounce);

        Assert.Equal(
            new[] { first, second }.Order(StringComparer.OrdinalIgnoreCase),
            _raised.Select(p => p.FilePath!).Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChangingAnExistingFile_RaisesFileChanged()
    {
        string path = Path.Combine(_folder, "existing.txt");
        File.WriteAllText(path, "before");
        using FolderWatcherService watcher = StartWatcher();

        Write(watcher, path, "after", append: true);
        _time.Advance(Debounce);

        Assert.Equal(new TriggerPayload(TriggerKind.FileChanged, FilePath: path), Assert.Single(_raised));
    }

    [Fact]
    public void Filter_ExcludesNonMatchingFiles()
    {
        using FolderWatcherService watcher = StartWatcher("*.png");
        string ignored = Path.Combine(_folder, "notes.txt");
        string matched = Path.Combine(_folder, "photo.png");

        File.WriteAllText(ignored, "not an image");
        Write(watcher, matched, "image bytes");
        _time.Advance(Debounce);

        Assert.Equal(matched, Assert.Single(_raised).FilePath);
    }

    [Fact]
    public void RenameOntoAMatchingName_IsReportedAsCreated()
    {
        // How a browser finishes a download: a temp name, then a rename.
        using FolderWatcherService watcher = StartWatcher("*.png");
        string partial = Path.Combine(_folder, "download.png.crdownload");
        string final = Path.Combine(_folder, "download.png");
        File.WriteAllText(partial, "bytes");

        int before = watcher.ObservedEventCount;
        File.Move(partial, final);
        WaitForEvents(watcher, before);
        _time.Advance(Debounce);

        Assert.Equal(new TriggerPayload(TriggerKind.FileCreated, FilePath: final), Assert.Single(_raised));
    }

    [Fact]
    public void Stop_DropsPendingFiles_AndIgnoresLaterWrites()
    {
        FolderWatcherService watcher = StartWatcher();
        string path = Path.Combine(_folder, "pending.txt");
        Write(watcher, path, "x");

        watcher.Stop();
        File.WriteAllText(Path.Combine(_folder, "after-stop.txt"), "y");
        Thread.Sleep(200);
        _time.Advance(Debounce * 3);

        Assert.Empty(_raised);
        Assert.Equal(0, watcher.PendingCount);
        Assert.Null(watcher.CurrentWatcher);
        watcher.Dispose();
    }

    [Fact]
    public void WatcherError_RaisesNothing_AndAFreshWatcherCarriesOn()
    {
        using FolderWatcherService watcher = StartWatcher();
        FileSystemWatcher? original = watcher.CurrentWatcher;

        watcher.OnWatcherError(original);

        Assert.NotNull(watcher.CurrentWatcher);
        Assert.NotSame(original, watcher.CurrentWatcher);
        Assert.Empty(_raised);

        string path = Path.Combine(_folder, "after-error.txt");
        Write(watcher, path, "still watching");
        _time.Advance(Debounce);
        Assert.Equal(path, Assert.Single(_raised).FilePath);
    }

    [Fact]
    public void ErrorFromAReplacedWatcher_IsIgnored()
    {
        using FolderWatcherService watcher = StartWatcher();
        FileSystemWatcher? current = watcher.CurrentWatcher;

        watcher.OnWatcherError(new object());

        Assert.Same(current, watcher.CurrentWatcher);
    }

    [Fact]
    public void Constructor_RejectsARelativePath()
    {
        Assert.Throws<ArgumentException>(() => new FolderWatcherService("relative\\folder"));
    }

    [Fact]
    public void Start_OnAMissingFolder_Throws()
    {
        using FolderWatcherService watcher = new(Path.Combine(_folder, "missing"), timeProvider: _time);

        Assert.Throws<ArgumentException>(watcher.Start);
    }

    private FolderWatcherService StartWatcher(string filter = "*")
    {
        FolderWatcherService watcher = new(_folder, filter, debounce: Debounce, timeProvider: _time);
        watcher.Triggered += (_, payload) => _raised.Enqueue(payload);
        watcher.Start();
        return watcher;
    }

    /// <summary>Writes a file and waits until the watcher has seen it, plus any trailing events.</summary>
    private static void Write(FolderWatcherService watcher, string path, string text, bool append = false)
    {
        int before = watcher.ObservedEventCount;
        if (append)
            File.AppendAllText(path, text);
        else
            File.WriteAllText(path, text);

        WaitForEvents(watcher, before);
    }

    private static void WaitForEvents(FolderWatcherService watcher, int before)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (watcher.ObservedEventCount == before)
        {
            if (elapsed.Elapsed > TimeSpan.FromSeconds(10))
                throw new TimeoutException("The file system never reported the change.");
            Thread.Sleep(10);
        }

        // One save is usually several events; let the rest land before the clock moves.
        Thread.Sleep(150);
    }
}
