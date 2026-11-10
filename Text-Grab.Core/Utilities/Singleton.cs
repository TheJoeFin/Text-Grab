using System;
using System.Collections.Concurrent;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

public static class Singleton<T> where T : class, new()
{
    private static ConcurrentDictionary<Type, T> _instances = new();

    /// <summary>
    /// The container's instance when one is registered through <see cref="ServiceAccess"/>,
    /// otherwise <see cref="FallbackInstance"/>. The app registers each Singleton-backed type with
    /// a factory that returns <see cref="FallbackInstance"/>, so both paths yield the same object
    /// whichever runs first.
    /// </summary>
    public static T Instance => ServiceAccess.TryGet<T>() ?? FallbackInstance;

    /// <summary>The lazily created process-wide instance. Constructed on first read.</summary>
    internal static T FallbackInstance => _instances.GetOrAdd(typeof(T), (t) => new T());
}
