using System;
using System.Collections.Generic;
using Text_Grab.Services;

namespace Text_Grab.Tests.Core;

/// <summary>
/// ServiceAccess and Singleton&lt;T&gt; share one process-global resolver. Every test that sets or
/// clears it runs in this collection, which never runs in parallel with anything else.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ServiceAccessCollectionDefinition
{
    public const string Name = "Service access";
}

/// <summary>A minimal IServiceProvider over a type-to-instance map.</summary>
internal sealed class FakeServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = [];

    public int Requests { get; private set; }

    public FakeServiceProvider With<T>(T instance) where T : class
    {
        _services[typeof(T)] = instance;
        return this;
    }

    public object? GetService(Type serviceType)
    {
        Requests++;
        return _services.GetValueOrDefault(serviceType);
    }
}

[Collection(ServiceAccessCollectionDefinition.Name)]
public sealed class ServiceAccessTests : IDisposable
{
    private sealed class Greeter { }

    private sealed class Unregistered { }

    public ServiceAccessTests() => ServiceAccess.ClearProvider();

    // Tests.Core does not load the app assembly, so nothing else sets a resolver in this process.
    public void Dispose() => ServiceAccess.ClearProvider();

    [Fact]
    public void WithNoProvider_IsNotConfigured()
    {
        Assert.False(ServiceAccess.IsConfigured);
    }

    [Fact]
    public void WithNoProvider_TryGetReturnsNull()
    {
        Assert.Null(ServiceAccess.TryGet<Greeter>());
    }

    [Fact]
    public void WithNoProvider_GetRequiredThrowsAClearError()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(ServiceAccess.GetRequired<Greeter>);

        Assert.Contains(nameof(ServiceAccess.SetProvider), ex.Message);
        Assert.Contains(typeof(Greeter).FullName!, ex.Message);
    }

    [Fact]
    public void WithFakeProvider_TryGetAndGetRequiredResolveTheRegisteredInstance()
    {
        Greeter greeter = new();
        ServiceAccess.SetProvider(() => new FakeServiceProvider().With(greeter));

        Assert.True(ServiceAccess.IsConfigured);
        Assert.Same(greeter, ServiceAccess.TryGet<Greeter>());
        Assert.Same(greeter, ServiceAccess.GetRequired<Greeter>());
    }

    [Fact]
    public void WithFakeProvider_UnregisteredTypeIsNullOrThrows()
    {
        ServiceAccess.SetProvider(() => new FakeServiceProvider().With(new Greeter()));

        Assert.Null(ServiceAccess.TryGet<Unregistered>());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(ServiceAccess.GetRequired<Unregistered>);
        Assert.Contains(typeof(Unregistered).FullName!, ex.Message);
    }

    [Fact]
    public void ResolverReturningNoProvider_TryGetReturnsNullAndGetRequiredThrows()
    {
        ServiceAccess.SetProvider(static () => null);

        Assert.True(ServiceAccess.IsConfigured);
        Assert.Null(ServiceAccess.TryGet<Greeter>());
        Assert.Throws<InvalidOperationException>(ServiceAccess.GetRequired<Greeter>);
    }

    [Fact]
    public void Resolver_IsInvokedOnEachCallRatherThanCaptured()
    {
        // The app hands over a delegate so its container is built lazily, on first resolve.
        int invocations = 0;
        FakeServiceProvider provider = new FakeServiceProvider().With(new Greeter());
        ServiceAccess.SetProvider(() =>
        {
            invocations++;
            return provider;
        });

        Assert.Equal(0, invocations);

        ServiceAccess.TryGet<Greeter>();
        ServiceAccess.GetRequired<Greeter>();

        Assert.Equal(2, invocations);
    }

    [Fact]
    public void SetProvider_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceAccess.SetProvider(null!));
    }

    [Fact]
    public void ClearProvider_ReturnsToUnconfigured()
    {
        ServiceAccess.SetProvider(() => new FakeServiceProvider().With(new Greeter()));

        ServiceAccess.ClearProvider();

        Assert.False(ServiceAccess.IsConfigured);
        Assert.Null(ServiceAccess.TryGet<Greeter>());
    }

    [Fact]
    public void TryGet_DoesNotSwallowConstructionFailures()
    {
        ServiceAccess.SetProvider(static () => new ThrowingServiceProvider());

        Assert.Throws<InvalidOperationException>(ServiceAccess.TryGet<Greeter>);
    }

    private sealed class ThrowingServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => throw new InvalidOperationException("factory failed");
    }
}
