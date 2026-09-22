using System;
using System.Collections.Concurrent;

namespace Text_Grab.Utilities;

public static class Singleton<T> where T : new()
{
    private static ConcurrentDictionary<Type, T> _instances = new();

    public static T Instance => _instances.GetOrAdd(typeof(T), (t) => new T());
}
