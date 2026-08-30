using System;

namespace Text_Grab.Services;

/// <summary>
/// How portable code gets work onto the app's UI thread.
///
/// The same shape as <see cref="SettingsAccess"/>, and for the same reason: Core cannot see
/// <c>System.Windows.Application.Current.Dispatcher</c>, which lives in WindowsBase. The app
/// registers a poster at module load and Core code calls <see cref="TryPost"/>.
///
/// A delegate rather than a stored dispatcher because <c>Application.Current</c> is null at
/// module-initializer time and only becomes non-null once WPF starts. Resolving it inside the
/// registered delegate keeps the original late-bound behaviour: code that runs with no WPF
/// application - the test host, most obviously - simply finds nothing to post to.
/// </summary>
public static class UiThreadAccess
{
    private static Action<Action>? _poster;

    /// <summary>
    /// Registers how to run an action on the UI thread. The app calls this from a module
    /// initializer. Tests may call it again to substitute a synchronous fake.
    /// </summary>
    public static void SetPoster(Action<Action> poster)
        => _poster = poster ?? throw new ArgumentNullException(nameof(poster));

    /// <summary>Drops the registered poster. For tests that need to restore a clean slate.</summary>
    public static void ClearPoster() => _poster = null;

    /// <summary>Whether a poster has been registered.</summary>
    public static bool IsConfigured => _poster is not null;

    /// <summary>
    /// Queues <paramref name="action"/> to run on the UI thread and returns true, or returns
    /// false when there is no UI thread to post to. Callers are expected to treat false as
    /// "nothing to do" rather than an error - it is the ordinary case in a headless process.
    /// </summary>
    public static bool TryPost(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Action<Action>? poster = _poster;
        if (poster is null)
            return false;

        poster(action);
        return true;
    }
}
