using System.Reflection;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

// These tests exercise the real lifetime gate without creating a native model or requiring a
// Copilot+ PC. The collection isolates the shared gate and the shutdown flag from other tests.
[Collection("Windows AI model lifetime")]
public sealed class WinAiLanguageModelLifetimeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ReleaseModelAsync_WaitsForActiveInference()
    {
        using CancellationTokenSource timeout = new(TestTimeout);
        using IDisposable lease = await WinAiLanguageModel.AcquireInferenceAsync(timeout.Token);

        Task release = WinAiLanguageModel.ReleaseModelAsync(timeout.Token);
        try
        {
            Assert.False(release.IsCompleted);
        }
        finally
        {
            lease.Dispose();
            await release.WaitAsync(TestTimeout);
        }

        using IDisposable nextLease = await WinAiLanguageModel.AcquireInferenceAsync(timeout.Token);
    }

    [Fact]
    public async Task ReleaseModelAsync_DoesNotResumeOnCallerSynchronizationContext()
    {
        using CancellationTokenSource timeout = new(TestTimeout);
        using IDisposable lease = await WinAiLanguageModel.AcquireInferenceAsync(timeout.Token);
        RecordingSynchronizationContext context = new();
        SynchronizationContext? originalContext = SynchronizationContext.Current;

        Task release;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            release = WinAiLanguageModel.ReleaseModelAsync(timeout.Token);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        lease.Dispose();
        await release.WaitAsync(TestTimeout);

        Assert.Equal(0, context.PostCount);
    }

    [Theory]
    [InlineData("release")]
    [InlineData("restart")]
    [InlineData("warm-up")]
    public async Task ExternalLifetimeOperation_CancellationDoesNotReleaseActiveInference(string operationName)
    {
        using CancellationTokenSource timeout = new(TestTimeout);
        using CancellationTokenSource cancellation = new();
        using IDisposable lease = await WinAiLanguageModel.AcquireInferenceAsync(timeout.Token);

        Task operation = operationName switch
        {
            "release" => WinAiLanguageModel.ReleaseModelAsync(cancellation.Token),
            "restart" => WinAiLanguageModel.RestartModelAsync(cancellation.Token),
            "warm-up" => WinAiLanguageModel.EnsureModelAsync(cancellation.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(operationName)),
        };

        try
        {
            Assert.False(operation.IsCompleted);
        }
        finally
        {
            cancellation.Cancel();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TestTimeout));

        Task<IDisposable> next = WinAiLanguageModel.AcquireInferenceAsync(timeout.Token);
        try
        {
            Assert.False(next.IsCompleted);
        }
        finally
        {
            lease.Dispose();
            using IDisposable nextLease = await next.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task Recovery_DoesNotReacquireOrReleaseActiveInference()
    {
        using CancellationTokenSource timeout = new(TestTimeout);
        using IDisposable lease = await WinAiLanguageModel.AcquireInferenceAsync(timeout.Token);

        Action<CancellationToken> restart = GetRecoveryMethod();
        await Task.Run(() => restart(timeout.Token), timeout.Token).WaitAsync(TestTimeout);

        Task release = WinAiLanguageModel.ReleaseModelAsync(timeout.Token);
        try
        {
            Assert.False(release.IsCompleted);
        }
        finally
        {
            lease.Dispose();
            await release.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task Recovery_CancellationIsPropagated()
    {
        using CancellationTokenSource timeout = new(TestTimeout);
        using CancellationTokenSource cancellation = new();
        using IDisposable lease = await WinAiLanguageModel.AcquireInferenceAsync(timeout.Token);
        cancellation.Cancel();

        Action<CancellationToken> restart = GetRecoveryMethod();
        OperationCanceledException exception =
            Assert.Throws<OperationCanceledException>(() => restart(cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task Cleanup_ActiveLeaseCanFinishAndModelCannotBeRecreated()
    {
        FieldInfo disposed = typeof(WinAiLanguageModel).GetField("_disposed", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The language model shutdown flag was not found.");
        object? originalDisposed = disposed.GetValue(null);

        using CancellationTokenSource timeout = new(TestTimeout);
        using IDisposable lease = await WinAiLanguageModel.AcquireInferenceAsync(timeout.Token);
        try
        {
            WinAiLanguageModel.Cleanup();
            lease.Dispose();

            await WinAiLanguageModel.ReleaseModelAsync(timeout.Token).WaitAsync(TestTimeout);
            (bool ready, string? error) = await WinAiLanguageModel.EnsureModelAsync(timeout.Token);

            Assert.False(ready);
            Assert.NotNull(error);
            Assert.Contains("shut down", error);
        }
        finally
        {
            disposed.SetValue(null, originalDisposed);
        }
    }

    [Fact]
    public async Task InferenceLease_DisposeIsIdempotent()
    {
        using CancellationTokenSource timeout = new(TestTimeout);
        IDisposable lease = await WinAiLanguageModel.AcquireInferenceAsync(timeout.Token);

        lease.Dispose();
        lease.Dispose();

        using IDisposable nextLease = await WinAiLanguageModel.AcquireInferenceAsync(timeout.Token);
    }

    private static Action<CancellationToken> GetRecoveryMethod()
    {
        // Exercise the same private recovery path used by GenerateAsync and RunWithModelAsync
        // without adding model factories or other production-only-for-testing abstractions.
        MethodInfo method = typeof(WinAiLanguageModel).GetMethod(
            "RestartModelUnderInferenceLease", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The language model recovery method was not found.");

        return method.CreateDelegate<Action<CancellationToken>>();
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        internal int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            Interlocked.Increment(ref _postCount);
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
    }
}

[CollectionDefinition("Windows AI model lifetime", DisableParallelization = true)]
public sealed class WinAiLanguageModelLifetimeCollection
{
}
