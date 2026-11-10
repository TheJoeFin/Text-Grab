using System;
using Microsoft.Extensions.DependencyInjection;

namespace Text_Grab.Services;

/// <summary>
/// How code below the app reaches the app's dependency-injection container.
///
/// The container is built by the app (<c>Text-Grab/Services/AppServices.cs</c>), which Core cannot
/// reference. The app registers a resolver here from a module initializer and Core and
/// Core.Windows resolve through it.
///
/// The resolver is a delegate rather than a stored provider on purpose, for the same reason as
/// <see cref="SettingsAccess"/>: registering a delegate keeps module initialization free of work.
/// The app's container is built the first time something is resolved, not when the assembly loads.
/// </summary>
public static class ServiceAccess
{
    private static Func<IServiceProvider?>? _resolver;

    /// <summary>
    /// Registers the source of the service provider. The app calls this from a module initializer,
    /// so it is in place for any entry point into the app assembly, including the test host. Tests
    /// may call it again to substitute a fake.
    /// </summary>
    public static void SetProvider(Func<IServiceProvider?> resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>Drops the registered resolver. For tests that need to restore a clean slate.</summary>
    public static void ClearProvider() => _resolver = null;

    /// <summary>Whether a resolver has been registered.</summary>
    public static bool IsConfigured => _resolver is not null;

    /// <summary>
    /// The registered service of type <typeparamref name="T"/>, or <see langword="null"/> when no
    /// resolver is registered, the resolver returns no provider, or <typeparamref name="T"/> is not
    /// registered. Exceptions thrown while constructing the service are not swallowed.
    /// </summary>
    public static T? TryGet<T>() where T : class
        => _resolver?.Invoke()?.GetService<T>();

    /// <summary>The registered service of type <typeparamref name="T"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// No resolver is registered, the resolver returned no provider, or <typeparamref name="T"/> is
    /// not registered in the provider.
    /// </exception>
    public static T GetRequired<T>() where T : class
    {
        Func<IServiceProvider?> resolver = _resolver
            ?? throw new InvalidOperationException(
                $"No service provider registered. Call {nameof(ServiceAccess)}.{nameof(SetProvider)} " +
                $"before resolving {typeof(T).FullName} from Core code.");

        IServiceProvider provider = resolver()
            ?? throw new InvalidOperationException(
                $"The {nameof(ServiceAccess)} resolver returned no service provider while resolving {typeof(T).FullName}.");

        return provider.GetService<T>()
            ?? throw new InvalidOperationException(
                $"No service of type {typeof(T).FullName} is registered in the service provider.");
    }
}
