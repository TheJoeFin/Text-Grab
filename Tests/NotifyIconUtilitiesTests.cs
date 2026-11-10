using Microsoft.Extensions.DependencyInjection;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Tests;

public class NotifyIconUtilitiesTests
{
    [Fact]
    public void UnregisterHotkeys_ForgetsTheIdsItUnregistered()
    {
        // Settings unregisters on open and registers on close. If the IDs are not dropped here,
        // every open/close cycle appends a fresh set and the stale ones are unregistered again
        // on every later call. HotKeyManager hands out increasing IDs and never reuses them, so
        // these values are not registered in this process and unregistering them is a no-op.
        AppRuntimeState state = AppServices.Provider.GetRequiredService<AppRuntimeState>();
        List<int> original = state.HotKeyIds;

        try
        {
            state.HotKeyIds = [int.MaxValue - 2, int.MaxValue - 1];

            NotifyIconUtilities.UnregisterHotkeys();

            Assert.Empty(state.HotKeyIds);
        }
        finally
        {
            state.HotKeyIds = original;
        }
    }
}
