using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

public sealed class AudioModelDownloadTests : IDisposable
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private readonly string _directory = Path.Combine(Environment.CurrentDirectory, $"{nameof(AudioModelDownloadTests)}-{Guid.NewGuid():N}");

    public AudioModelDownloadTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task SameModel_ConcurrentCallersShareOneDownload()
    {
        using CancellationTokenSource timeout = new(TestTimeout);
        ControlledDownloadStream stream = new();
        int opens = 0;
        string path = ModelPath("shared");
        Task<Stream> Open(CancellationToken _)
        {
            Interlocked.Increment(ref opens);
            return Task.FromResult<Stream>(stream);
        }

        Task<string> first = AudioTranscriptionUtilities.EnsureModelDownloadedAsync(path, Open, timeout.Token);
        Task<string>[] others = [];
        string[] results = [];
        try
        {
            await stream.CopyStarted.Task.WaitAsync(timeout.Token);
            others = Enumerable.Range(0, 4)
                .Select(_ => AudioTranscriptionUtilities.EnsureModelDownloadedAsync(path, Open, timeout.Token))
                .ToArray();

            Assert.All(others, task => Assert.False(task.IsCompleted));
            Assert.Equal(1, opens);
        }
        finally
        {
            stream.FinishCopy.TrySetResult();
            await first.WaitAsync(timeout.Token);
            results = await Task.WhenAll(others).WaitAsync(timeout.Token);
        }

        Assert.All(results, result => Assert.Equal(path, result));
        Assert.Equal(1, opens);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(path, timeout.Token));
        Assert.Empty(Directory.GetFiles(_directory, "*.download"));
    }

    [Fact]
    public async Task DifferentModels_DownloadIndependently()
    {
        using CancellationTokenSource timeout = new(TestTimeout);
        ControlledDownloadStream firstStream = new();
        ControlledDownloadStream secondStream = new();
        Task<string> first = AudioTranscriptionUtilities.EnsureModelDownloadedAsync(
            ModelPath("first"), _ => Task.FromResult<Stream>(firstStream), timeout.Token);
        Task<string> second = AudioTranscriptionUtilities.EnsureModelDownloadedAsync(
            ModelPath("second"), _ => Task.FromResult<Stream>(secondStream), timeout.Token);
        try
        {
            await Task.WhenAll(firstStream.CopyStarted.Task, secondStream.CopyStarted.Task).WaitAsync(timeout.Token);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            firstStream.FinishCopy.TrySetResult();
            secondStream.FinishCopy.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(timeout.Token);
        }
    }

    [Fact]
    public async Task CancelledWaiter_DoesNotTouchOwnersStagingFile()
    {
        using CancellationTokenSource timeout = new(TestTimeout);
        using CancellationTokenSource cancellation = new();
        ControlledDownloadStream stream = new();
        string path = ModelPath("cancelled-waiter");
        string unrelatedStagingPath = path + ".download";
        await File.WriteAllTextAsync(unrelatedStagingPath, "another download", timeout.Token);

        Task<string> owner = AudioTranscriptionUtilities.EnsureModelDownloadedAsync(
            path, _ => Task.FromResult<Stream>(stream), timeout.Token);
        try
        {
            await stream.CopyStarted.Task.WaitAsync(timeout.Token);
            string ownersStagingPath = Assert.Single(
                Directory.GetFiles(_directory, "*.download"), file => file != unrelatedStagingPath);
            Task<string> waiter = AudioTranscriptionUtilities.EnsureModelDownloadedAsync(
                path, _ => throw new InvalidOperationException("A waiting caller must not open a stream."), cancellation.Token);

            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter.WaitAsync(timeout.Token));

            Assert.True(File.Exists(ownersStagingPath));
            Assert.False(owner.IsCompleted);
        }
        finally
        {
            stream.FinishCopy.TrySetResult();
            await owner.WaitAsync(timeout.Token);
        }

        Assert.Equal("another download", await File.ReadAllTextAsync(unrelatedStagingPath, timeout.Token));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(path, timeout.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCancelledOwner_QueuedCallerRetries(bool cancelOwner)
    {
        using CancellationTokenSource timeout = new(TestTimeout);
        using CancellationTokenSource ownerCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        ControlledDownloadStream stream = new() { FailAfterCopy = !cancelOwner };
        string path = ModelPath("retry");
        int opens = 0;
        Task<Stream> Open(CancellationToken _) =>
            Task.FromResult<Stream>(Interlocked.Increment(ref opens) == 1 ? stream : new MemoryStream([5, 6, 7]));

        Task<string> owner = AudioTranscriptionUtilities.EnsureModelDownloadedAsync(path, Open, ownerCancellation.Token);
        await stream.CopyStarted.Task.WaitAsync(timeout.Token);
        Task<string> retry = AudioTranscriptionUtilities.EnsureModelDownloadedAsync(path, Open, timeout.Token);
        Assert.False(retry.IsCompleted);

        if (cancelOwner)
            ownerCancellation.Cancel();
        stream.FinishCopy.TrySetResult();

        if (cancelOwner)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.WaitAsync(timeout.Token));
        else
            await Assert.ThrowsAsync<IOException>(() => owner.WaitAsync(timeout.Token));

        Assert.Equal(path, await retry.WaitAsync(timeout.Token));
        Assert.Equal(2, opens);
        Assert.Equal(new byte[] { 5, 6, 7 }, await File.ReadAllBytesAsync(path, timeout.Token));
        Assert.Empty(Directory.GetFiles(_directory, "*.download"));
    }

    [Fact]
    public async Task ExistingModel_IsNotOverwritten()
    {
        using CancellationTokenSource timeout = new(TestTimeout);
        string path = ModelPath("existing");
        await File.WriteAllBytesAsync(path, [8, 9], timeout.Token);

        string result = await AudioTranscriptionUtilities.EnsureModelDownloadedAsync(
            path, _ => throw new InvalidOperationException("An existing model must not be downloaded."), timeout.Token);

        Assert.Equal(path, result);
        Assert.Equal(new byte[] { 8, 9 }, await File.ReadAllBytesAsync(path, timeout.Token));
        Assert.Empty(Directory.GetFiles(_directory, "*.download"));
    }

    private string ModelPath(string name) => Path.Combine(_directory, name + ".bin");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class ControlledDownloadStream : MemoryStream
    {
        internal TaskCompletionSource CopyStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource FinishCopy { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool FailAfterCopy { get; init; }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await destination.WriteAsync(new byte[] { 1, 2 }, cancellationToken);
            CopyStarted.TrySetResult();
            await FinishCopy.Task.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (FailAfterCopy)
                throw new IOException("Simulated download failure.");
            await destination.WriteAsync(new byte[] { 3, 4 }, cancellationToken);
        }
    }
}
