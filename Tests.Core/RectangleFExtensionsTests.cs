using System.Drawing;

namespace Text_Grab.Tests.Core;

public class RectangleFExtensionsTests
{
    [Theory]
    [InlineData(0, 0, 10, 10, true)]
    [InlineData(-5, -5, 1, 1, true)]
    [InlineData(0, 0, 0, 10, false)]     // zero width
    [InlineData(0, 0, 10, 0, false)]     // zero height
    [InlineData(float.NaN, 0, 10, 10, false)]
    [InlineData(0, float.NaN, 10, 10, false)]
    [InlineData(float.PositiveInfinity, 0, 10, 10, false)]
    [InlineData(0, 0, float.NegativeInfinity, 10, false)]
    public void IsGood_RejectsDegenerateAndNonFiniteRects(float x, float y, float w, float h, bool expected)
    {
        RectangleF rect = new(x, y, w, h);

        Assert.Equal(expected, rect.IsGood());
    }

    [Fact]
    public void CenterPoint_ReturnsMidpoint()
    {
        RectangleF rect = new(10, 20, 30, 40);

        PointF center = rect.CenterPoint();

        Assert.Equal(25f, center.X);
        Assert.Equal(40f, center.Y);
    }

    [Fact]
    public void GetScaledUpByFraction_ScalesPositionAndSize()
    {
        RectangleF scaled = new RectangleF(10, 20, 30, 40).GetScaledUpByFraction(2.0);

        Assert.Equal(new RectangleF(20, 40, 60, 80), scaled);
    }

    [Fact]
    public void GetScaleSizeByFraction_LeavesOriginInPlace()
    {
        RectangleF scaled = new RectangleF(10, 20, 30, 40).GetScaleSizeByFraction(0.5);

        Assert.Equal(new RectangleF(10, 20, 15, 20), scaled);
    }

    [Fact]
    public void Union_CombinesBothRects()
    {
        RectangleF union = new RectangleF(0, 0, 10, 10).Union(new RectangleF(20, 20, 10, 10));

        Assert.Equal(new RectangleF(0, 0, 30, 30), union);
    }

    [Fact]
    public void Union_IgnoresEmptyOperandsRatherThanPullingToOrigin()
    {
        RectangleF populated = new(50, 50, 10, 10);

        Assert.Equal(populated, populated.Union(RectangleF.Empty));
        Assert.Equal(populated, RectangleF.Empty.Union(populated));
    }
}
