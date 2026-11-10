using Text_Grab.Utilities;

namespace Tests;

public class HotKeyIdPoolTests
{
    [Fact]
    public void Reserve_HandsOutTheLowestFreeIdUntilTheRangeIsUsed()
    {
        HotKeyIdPool pool = new(firstId: 1, lastId: 3);

        Assert.Equal(1, pool.Reserve());
        Assert.Equal(2, pool.Reserve());
        Assert.Equal(3, pool.Reserve());
        Assert.Null(pool.Reserve());
    }

    [Fact]
    public void Release_MakesTheIdAvailableAgain()
    {
        HotKeyIdPool pool = new(firstId: 1, lastId: 3);
        pool.Reserve();
        pool.Reserve();

        pool.Release(1);

        Assert.Equal(1, pool.Reserve());
        Assert.Equal(3, pool.Reserve());
    }

    [Fact]
    public void RegisterUnregisterCycles_NeverExhaustTheRange()
    {
        // The old counter handed out a new ID for every registration, so a session with enough
        // Settings open/close cycles ran past the Win32 limit and every later registration failed.
        HotKeyIdPool pool = new(firstId: 1, lastId: 3);

        for (int cycle = 0; cycle < 10_000; cycle++)
        {
            int?[] ids = [pool.Reserve(), pool.Reserve(), pool.Reserve()];
            Assert.Equal([1, 2, 3], ids);

            foreach (int? id in ids)
                pool.Release(id!.Value);
        }
    }

    [Fact]
    public void Release_OfAnIdNotInUse_IsIgnored()
    {
        HotKeyIdPool pool = new(firstId: 1, lastId: 2);
        pool.Reserve();

        pool.Release(2);
        pool.Release(42);

        Assert.Equal(2, pool.Reserve());
        Assert.Null(pool.Reserve());
    }

    [Fact]
    public void HotKeyManager_UsesTheWin32ApplicationIdRange()
    {
        // RegisterHotKey: "An application must specify an id value in the range 0x0000 through 0xBFFF."
        Assert.InRange(HotKeyManager.FirstHotKeyId, 0x0000, 0xBFFF);
        Assert.Equal(0xBFFF, HotKeyManager.LastHotKeyId);
    }
}
