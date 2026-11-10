using System.Collections.Generic;
using System.Threading;

namespace Text_Grab.Utilities;

/// <summary>
/// Hands out Win32 hot key IDs so freed IDs are reused.
///
/// RegisterHotKey only accepts application IDs from 0x0000 to 0xBFFF, and an ID must not be reused
/// while it is still registered on the same window. A counter that only grows runs out after enough
/// register/unregister cycles (every Settings open and close is one), so IDs come back here when a
/// hot key is unregistered or fails to register.
/// </summary>
internal sealed class HotKeyIdPool(int firstId, int lastId)
{
    private readonly Lock _lock = new();
    private readonly HashSet<int> _inUse = [];

    /// <summary>The lowest free ID, or <see langword="null"/> when every ID in range is in use.</summary>
    public int? Reserve()
    {
        lock (_lock)
        {
            for (int id = firstId; id <= lastId; id++)
            {
                if (_inUse.Add(id))
                    return id;
            }

            return null;
        }
    }

    /// <summary>Returns <paramref name="id"/> to the pool. Releasing an ID that is not in use does nothing.</summary>
    public void Release(int id)
    {
        lock (_lock)
            _inUse.Remove(id);
    }
}
