using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab.Core.Abstractions;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Services;

/// <summary>
/// The app's composition root: the one dependency-injection container in the process.
///
/// The container is built on first use of <see cref="Provider"/>, not at startup.
/// <see cref="ServiceAccessInitializer"/> only stores a delegate that reads <see cref="Provider"/>.
/// In practice the first <c>Singleton&lt;T&gt;.Instance</c> read builds it. Building it constructs
/// no service: every service is still constructed when it is first resolved, exactly when it was
/// constructed before the container existed. Do not resolve anything eagerly "to warm up";
/// <see cref="SettingsService"/> in particular runs first-run migration in its constructor.
///
/// The provider is never disposed. The container owns the disposal of anything it creates from a
/// factory, and the <c>Singleton&lt;T&gt;</c>-backed services are disposed explicitly by the app
/// on exit, as they were before the container existed.
/// </summary>
internal static class AppServices
{
    private static readonly Lazy<ServiceProvider> _provider = new(Build);

    public static IServiceProvider Provider => _provider.Value;

    internal static ServiceProvider Build()
    {
        ServiceCollection services = new();
        ConfigureServices(services);
        return services.BuildServiceProvider();
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        // Types already reached through Singleton<T>.Instance. The container hands out the same
        // object Singleton<T> does, whichever is asked first.
        services.AddSingletonBackedByFallback<SettingsService>();
        services.AddSingletonBackedByFallback<HistoryService>();
        services.AddSingletonBackedByFallback<TtsService>();
        services.AddSingletonBackedByFallback<LanguageService>();
        services.AddSingletonBackedByFallback<WebSearchUrlCatalog>();

        // Transient on purpose: tests swap settings through SettingsAccess.SetResolver, and a
        // singleton here would keep handing out whichever settings object was current first.
        services.AddTransient<ITextGrabSettings>(static _ => SettingsAccess.Current);

        // One client for the process. A long-lived client must recycle pooled connections or it
        // never sees DNS changes. Do not change its DefaultRequestHeaders; set headers per request.
        services.AddSingleton(static _ => new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        }));

        services.AddSingleton<AppRuntimeState>();

        // Headless OCR (v5 F3). Stateless, so one instance serves every caller.
        services.AddSingleton<IOcrService, OcrService>();
    }

    /// <summary>
    /// Registers <typeparamref name="T"/> as a singleton whose instance is
    /// <c>Singleton&lt;T&gt;.FallbackInstance</c>. The factory runs on first resolve, so the
    /// instance is still created lazily, on the thread that first asks for it.
    /// </summary>
    internal static IServiceCollection AddSingletonBackedByFallback<T>(this IServiceCollection services)
        where T : class, new()
        => services.AddSingleton(static _ => Singleton<T>.FallbackInstance);
}
