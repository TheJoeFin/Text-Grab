using System.Drawing;
using Text_Grab.Utilities.Hdr;

namespace Tests;

public class HdrScreenCaptureTests
{
    [Fact]
    public void BuildCaptureSegments_MapsCrossMonitorRegionsToCompositeCoordinates()
    {
        Rectangle region = new(-200, 100, 500, 300);
        MonitorHdrInfo[] monitors =
        [
            new((IntPtr)1, new Rectangle(-1920, 0, 1920, 1080), true, 200),
            new((IntPtr)2, new Rectangle(0, 0, 2560, 1440), true, 160),
        ];

        HdrScreenCapture.HdrCaptureSegment[] segments =
            HdrScreenCapture.BuildCaptureSegments(region, monitors);

        Assert.Collection(
            segments,
            left =>
            {
                Assert.Equal(new Rectangle(-200, 100, 200, 300), left.CaptureRegion);
                Assert.Equal(new Point(0, 0), left.Destination);
            },
            right =>
            {
                Assert.Equal(new Rectangle(0, 100, 300, 300), right.CaptureRegion);
                Assert.Equal(new Point(200, 0), right.Destination);
            });
    }

    [Fact]
    public void BuildCaptureSegments_ExcludesSdrAndNonIntersectingMonitors()
    {
        Rectangle region = new(100, 100, 200, 200);
        MonitorHdrInfo[] monitors =
        [
            new((IntPtr)1, new Rectangle(0, 0, 500, 500), false, 0),
            new((IntPtr)2, new Rectangle(500, 0, 500, 500), true, 200),
        ];

        Assert.Empty(HdrScreenCapture.BuildCaptureSegments(region, monitors));
    }
}
