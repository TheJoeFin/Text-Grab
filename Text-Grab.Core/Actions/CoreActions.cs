using System;

namespace Text_Grab.Core.Actions;

/// <summary>
/// Registers the actions Text-Grab.Core implements: the pure text transforms. Core.Windows adds the
/// Windows-backed ones through its own <c>WindowsActions.RegisterAll</c>.
/// </summary>
public static class CoreActions
{
    /// <summary>Registers every Core action. Call once per registry; a second call throws on the first duplicate ID.</summary>
    public static void RegisterAll(IActionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
    }
}
