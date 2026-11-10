using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Tests;

/// <summary>
/// Guards the app's composition root and the property that makes the Singleton&lt;T&gt; migration
/// safe: Singleton&lt;T&gt;.Instance and the container hand out the same object, whichever is
/// asked first.
///
/// In the "Settings isolation" collection because these tests swap the process-global
/// SettingsAccess and ServiceAccess resolvers. Every swap is restored through the app's own
/// module initializer, so later tests see exactly what the app installs.
/// </summary>
[Collection("Settings isolation")]
public class AppServicesTests
{
    // Probe types: Singleton<T> state is per closed type and lives for the process, so these give
    // each ordering test an instance that has never been created. The real services may already
    // have been created by earlier tests.
    private sealed class SingletonFirstProbe
    {
        public static int Constructions;
        public SingletonFirstProbe() => Interlocked.Increment(ref Constructions);
    }

    private sealed class ContainerFirstProbe
    {
        public static int Constructions;
        public ContainerFirstProbe() => Interlocked.Increment(ref Constructions);
    }

    [Fact]
    public void ModuleInitializer_ConfiguresServiceAccessWithoutAnyStartupCall()
    {
        // The test host never raises App's WPF Startup event, so this passing is the proof that
        // the wiring lives in a module initializer and not in appStartup.
        Assert.True(ServiceAccess.IsConfigured);
    }

    [WpfFact]
    public void Provider_ResolvesEveryRegisteredService()
    {
        IServiceProvider provider = AppServices.Provider;

        Assert.NotNull(provider.GetRequiredService<SettingsService>());
        Assert.NotNull(provider.GetRequiredService<HistoryService>());
        Assert.NotNull(provider.GetRequiredService<TtsService>());
        Assert.NotNull(provider.GetRequiredService<LanguageService>());
        Assert.NotNull(provider.GetRequiredService<WebSearchUrlCatalog>());
        Assert.NotNull(provider.GetRequiredService<ITextGrabSettings>());
        Assert.NotNull(provider.GetRequiredService<HttpClient>());
        Assert.NotNull(provider.GetRequiredService<AppRuntimeState>());
    }

    [WpfFact]
    public void Singleton_ThenProvider_ReturnTheSameReference()
    {
        AssertSameReferenceSingletonFirst<SettingsService>();
        AssertSameReferenceSingletonFirst<HistoryService>();
        AssertSameReferenceSingletonFirst<TtsService>();
        AssertSameReferenceSingletonFirst<LanguageService>();
        AssertSameReferenceSingletonFirst<WebSearchUrlCatalog>();
    }

    [WpfFact]
    public void Provider_ThenSingleton_ReturnTheSameReference()
    {
        AssertSameReferenceProviderFirst<SettingsService>();
        AssertSameReferenceProviderFirst<HistoryService>();
        AssertSameReferenceProviderFirst<TtsService>();
        AssertSameReferenceProviderFirst<LanguageService>();
        AssertSameReferenceProviderFirst<WebSearchUrlCatalog>();
    }

    [WpfFact]
    public void Singleton_ResolvesThroughTheContainerAndServiceAccess()
    {
        // TryGet is non-null only if the type is registered, so this proves Instance is not just
        // falling back for the five Singleton-backed services.
        Assert.Same(Singleton<SettingsService>.Instance, ServiceAccess.TryGet<SettingsService>());
        Assert.Same(Singleton<HistoryService>.Instance, ServiceAccess.TryGet<HistoryService>());
        Assert.Same(Singleton<TtsService>.Instance, ServiceAccess.TryGet<TtsService>());
        Assert.Same(Singleton<LanguageService>.Instance, ServiceAccess.TryGet<LanguageService>());
        Assert.Same(Singleton<WebSearchUrlCatalog>.Instance, ServiceAccess.TryGet<WebSearchUrlCatalog>());
    }

    [Fact]
    public void SingletonBeforeTheContainerExists_ThenContainer_SameReference()
    {
        try
        {
            // Code that runs before the container exists takes the fallback path.
            ServiceAccess.ClearProvider();
            SingletonFirstProbe early = Singleton<SingletonFirstProbe>.Instance;

            using ServiceProvider provider = BuildProviderWith<SingletonFirstProbe>();
            ServiceAccess.SetProvider(() => provider);

            Assert.Same(early, provider.GetRequiredService<SingletonFirstProbe>());
            Assert.Same(early, Singleton<SingletonFirstProbe>.Instance);
            Assert.Same(early, ServiceAccess.GetRequired<SingletonFirstProbe>());
            Assert.Equal(1, SingletonFirstProbe.Constructions);
        }
        finally
        {
            ServiceAccessInitializer.Initialize();
        }
    }

    [Fact]
    public void ContainerFirst_ThenSingleton_SameReference()
    {
        try
        {
            using ServiceProvider provider = BuildProviderWith<ContainerFirstProbe>();
            ServiceAccess.SetProvider(() => provider);

            // Building the container constructs nothing; the factory runs on first resolve.
            Assert.Equal(0, ContainerFirstProbe.Constructions);

            ContainerFirstProbe fromContainer = provider.GetRequiredService<ContainerFirstProbe>();

            Assert.Same(fromContainer, Singleton<ContainerFirstProbe>.Instance);

            // And code that bypasses the container still gets the same object.
            ServiceAccess.ClearProvider();
            Assert.Same(fromContainer, Singleton<ContainerFirstProbe>.Instance);
            Assert.Equal(1, ContainerFirstProbe.Constructions);
        }
        finally
        {
            ServiceAccessInitializer.Initialize();
        }
    }

    [Fact]
    public void TextGrabSettings_FollowsTheSettingsAccessResolverAndIsNotCached()
    {
        Settings substitute = new();

        try
        {
            SettingsAccess.SetResolver(() => substitute);

            Assert.Same(substitute, AppServices.Provider.GetRequiredService<ITextGrabSettings>());
        }
        finally
        {
            SettingsAccessInitializer.Initialize();
        }

        ITextGrabSettings restored = AppServices.Provider.GetRequiredService<ITextGrabSettings>();
        Assert.NotSame(substitute, restored);
        Assert.Same(AppUtilities.TextGrabSettings, restored);
    }

    [Fact]
    public void HttpClient_IsOneSharedInstance()
    {
        Assert.Same(
            AppServices.Provider.GetRequiredService<HttpClient>(),
            AppServices.Provider.GetRequiredService<HttpClient>());
    }

    [Fact]
    public void AppRuntimeState_IsOneSharedInstance()
    {
        // App.HotKeyIds, NumberOfRunningInstances and TextGrabIcon forward here, and the tray and
        // hotkey code reads it from the container; both must see the same state.
        Assert.Same(
            AppServices.Provider.GetRequiredService<AppRuntimeState>(),
            AppServices.Provider.GetRequiredService<AppRuntimeState>());
    }

    private static ServiceProvider BuildProviderWith<T>() where T : class, new()
    {
        ServiceCollection services = new();
        services.AddSingletonBackedByFallback<T>();
        return services.BuildServiceProvider();
    }

    private static void AssertSameReferenceSingletonFirst<T>() where T : class, new()
    {
        T fromSingleton = Singleton<T>.Instance;
        T fromProvider = AppServices.Provider.GetRequiredService<T>();

        Assert.Same(fromSingleton, fromProvider);
        Assert.Same(Singleton<T>.FallbackInstance, fromProvider);
    }

    private static void AssertSameReferenceProviderFirst<T>() where T : class, new()
    {
        T fromProvider = AppServices.Provider.GetRequiredService<T>();
        T fromSingleton = Singleton<T>.Instance;

        Assert.Same(fromProvider, fromSingleton);
        Assert.Same(Singleton<T>.FallbackInstance, fromSingleton);
    }
}
