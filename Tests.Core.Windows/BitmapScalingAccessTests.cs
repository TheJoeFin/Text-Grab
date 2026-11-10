using System.Drawing;
using System.Drawing.Imaging;
using Text_Grab.Services;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// The scaler seam the OCR dispatch uses (v5 F3). Nothing registers a scaler in this host - there
/// is no app assembly - so OCR here runs on the GDI+ default. Tests that register one share the
/// non-parallel OCR settings collection because the seam is process-global.
/// </summary>
[Collection(OcrSettingsCollectionDefinition.Name)]
public class BitmapScalingAccessTests
{
    [Fact]
    public void NothingIsRegisteredInAHeadlessHost()
    {
        Assert.False(BitmapScalingAccess.IsConfigured);
    }

    [Theory]
    [InlineData(40, 20, 2.5, 100, 50)]
    [InlineData(64, 32, 0.5, 32, 16)]
    [InlineData(10, 10, 1.0, 10, 10)]
    [InlineData(30, 5, 0.01, 1, 1)]
    public void ScaleUniform_WithNothingRegistered_ScalesWithTheDefault(
        int width, int height, double scale, int expectedWidth, int expectedHeight)
    {
        using Bitmap source = new(width, height, PixelFormat.Format32bppArgb);

        using Bitmap scaled = BitmapScalingAccess.ScaleUniform(source, scale);

        Assert.NotSame(source, scaled);
        Assert.Equal(expectedWidth, scaled.Width);
        Assert.Equal(expectedHeight, scaled.Height);
        Assert.Equal(PixelFormat.Format32bppPArgb, scaled.PixelFormat);
    }

    [Fact]
    public void DefaultScaleUniform_KeepsContentInPlace()
    {
        using Bitmap source = new(20, 10, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(source))
        {
            g.FillRectangle(Brushes.Red, 0, 0, 10, 10);
            g.FillRectangle(Brushes.Blue, 10, 0, 10, 10);
        }

        using Bitmap scaled = BitmapScalingAccess.DefaultScaleUniform(source, 3);

        Color left = scaled.GetPixel(15, 15);
        Color right = scaled.GetPixel(45, 15);
        Assert.True(left.R > 200 && left.B < 50, $"left was {left}");
        Assert.True(right.B > 200 && right.R < 50, $"right was {right}");
        // No dark fringe from sampling past the edge.
        Assert.True(scaled.GetPixel(0, 0).R > 200, $"corner was {scaled.GetPixel(0, 0)}");
    }

    [Fact]
    public void ScaleUniform_UsesTheRegisteredScaler()
    {
        double? seenScale = null;
        try
        {
            BitmapScalingAccess.SetScaler((bitmap, scale) =>
            {
                seenScale = scale;
                return new Bitmap(7, 3);
            });

            using Bitmap source = new(40, 20);
            using Bitmap scaled = BitmapScalingAccess.ScaleUniform(source, 1.75);

            Assert.True(BitmapScalingAccess.IsConfigured);
            Assert.Equal(1.75, seenScale);
            Assert.Equal(7, scaled.Width);
        }
        finally
        {
            BitmapScalingAccess.ClearScaler();
        }

        Assert.False(BitmapScalingAccess.IsConfigured);
    }

    [Fact]
    public void SetScaler_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => BitmapScalingAccess.SetScaler(null!));
    }
}
