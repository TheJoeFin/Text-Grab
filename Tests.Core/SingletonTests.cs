using System;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

/// <summary>
/// Singleton&lt;T&gt; state is per closed generic type and lives for the process, so every test
/// uses its own probe type and starts from an instance that has never been created.
/// </summary>
[Collection(ServiceAccessCollectionDefinition.Name)]
public sealed class SingletonTests : IDisposable
{
    private sealed class LazyProbe
    {
        public static int Constructions;
        public LazyProbe() => Interlocked.Increment(ref Constructions);
    }

    private sealed class RepeatProbe { }

    private sealed class ConcurrentProbe { }

    private sealed class FallbackAgreementProbe { }

    private sealed class RoutedProbe { }

    private sealed class UnregisteredProbe { }

    private sealed class SameObjectProbe { }

    private sealed class NullProviderProbe { }

    public SingletonTests() => ServiceAccess.ClearProvider();

    public void Dispose() => ServiceAccess.ClearProvider();

    [Fact]
    public void WithNoProvider_InstanceIsCreatedLazilyOnFirstRead()
    {
        Assert.Equal(0, LazyProbe.Constructions);

        LazyProbe first = Singleton<LazyProbe>.Instance;

        Assert.Equal(1, LazyProbe.Constructions);
        Assert.NotNull(first);
    }

    [Fact]
    public void WithNoProvider_InstanceReturnsTheSameObjectEveryTime()
    {
        RepeatProbe first = Singleton<RepeatProbe>.Instance;
        RepeatProbe second = Singleton<RepeatProbe>.Instance;

        Assert.Same(first, second);
    }

    [Fact]
    public async Task WithNoProvider_ConcurrentReadersSeeOneInstance()
    {
        Task<ConcurrentProbe>[] readers = new Task<ConcurrentProbe>[16];
        using ManualResetEventSlim start = new();
        for (int i = 0; i < readers.Length; i++)
        {
            readers[i] = Task.Run(() =>
            {
                start.Wait();
                return Singleton<ConcurrentProbe>.Instance;
            });
        }

        start.Set();
        ConcurrentProbe[] results = await Task.WhenAll(readers);

        Assert.All(results, result => Assert.Same(results[0], result));
    }

    [Fact]
    public void WithNoProvider_InstanceIsTheFallbackInstance()
    {
        Assert.Same(Singleton<FallbackAgreementProbe>.FallbackInstance, Singleton<FallbackAgreementProbe>.Instance);
    }

    [Fact]
    public void WithProvider_InstanceAsksTheProviderFirst()
    {
        RoutedProbe fromContainer = new();
        FakeServiceProvider provider = new FakeServiceProvider().With(fromContainer);
        ServiceAccess.SetProvider(() => provider);

        Assert.Same(fromContainer, Singleton<RoutedProbe>.Instance);
        Assert.Equal(1, provider.Requests);
    }

    [Fact]
    public void WithProviderThatLacksTheType_InstanceFallsBack()
    {
        FakeServiceProvider provider = new();
        ServiceAccess.SetProvider(() => provider);

        UnregisteredProbe instance = Singleton<UnregisteredProbe>.Instance;

        Assert.Same(Singleton<UnregisteredProbe>.FallbackInstance, instance);
        Assert.Same(instance, Singleton<UnregisteredProbe>.Instance);
    }

    [Fact]
    public void WithResolverReturningNoProvider_InstanceFallsBack()
    {
        ServiceAccess.SetProvider(static () => null);

        Assert.Same(Singleton<NullProviderProbe>.FallbackInstance, Singleton<NullProviderProbe>.Instance);
    }

    [Fact]
    public void ProviderReturningTheFallbackInstance_GivesOneObjectBeforeAndAfterTheProviderExists()
    {
        // This is the app's registration pattern: the container's factory returns FallbackInstance.
        SameObjectProbe beforeProvider = Singleton<SameObjectProbe>.Instance;

        ServiceAccess.SetProvider(static () => new FakeServiceProvider().With(Singleton<SameObjectProbe>.FallbackInstance));
        SameObjectProbe withProvider = Singleton<SameObjectProbe>.Instance;

        ServiceAccess.ClearProvider();
        SameObjectProbe afterProvider = Singleton<SameObjectProbe>.Instance;

        Assert.Same(beforeProvider, withProvider);
        Assert.Same(beforeProvider, afterProvider);
    }
}
