using System.Drawing;
using System.Drawing.Imaging;
using Text_Grab.Utilities;
using Xunit;

namespace Tests;

public class HdrTests
{
    [Fact]
    public void ConvertHdrToSdr_WithNullBitmap_ReturnsNull()
    {
        // Arrange
        Bitmap? nullBitmap = null;

        // Act
        Bitmap? result = HdrUtilities.ConvertHdrToSdr(nullBitmap);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ConvertHdrToSdr_WithValidBitmap_ReturnsNewBitmap()
    {
        // Arrange
        Bitmap testBitmap = new(100, 100, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(testBitmap);
        // Fill with white color (simulating HDR bright pixels)
        g.Clear(Color.White);

        // Act
        Bitmap result = HdrUtilities.ConvertHdrToSdr(testBitmap);

        // Assert
        Assert.NotNull(result);
        Assert.NotSame(testBitmap, result);
        Assert.Equal(testBitmap.Width, result.Width);
        Assert.Equal(testBitmap.Height, result.Height);

        // Cleanup
        testBitmap.Dispose();
        result.Dispose();
    }

    [Fact]
    public void ConvertHdrToSdr_WithBrightPixels_ReducesBrightness()
    {
        // Arrange
        Bitmap testBitmap = new(10, 10, PixelFormat.Format32bppArgb);
        // Fill with very bright color
        using (Graphics g = Graphics.FromImage(testBitmap))
        {
            g.Clear(Color.FromArgb(255, 255, 255, 255));
        }

        // Act
        Bitmap result = HdrUtilities.ConvertHdrToSdr(testBitmap);

        // Assert
        // The conversion should tone map bright values
        // In this case, pure white (255,255,255) should remain relatively close
        // but with tone mapping applied
        Color centerPixel = result.GetPixel(5, 5);
        
        // After tone mapping, pixels should still be bright but potentially adjusted
        Assert.True(centerPixel.R >= 200, "Red channel should remain bright");
        Assert.True(centerPixel.G >= 200, "Green channel should remain bright");
        Assert.True(centerPixel.B >= 200, "Blue channel should remain bright");

        // Cleanup
        testBitmap.Dispose();
        result.Dispose();
    }

    [Fact]
    public void ConvertHdrToSdr_WithMixedPixels_ProcessesCorrectly()
    {
        // Arrange
        Bitmap testBitmap = new(10, 10, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(testBitmap))
        {
            // Fill with different colors to test tone mapping
            using Brush darkBrush = new SolidBrush(Color.FromArgb(255, 50, 50, 50));
            using Brush brightBrush = new SolidBrush(Color.FromArgb(255, 250, 250, 250));
            
            g.FillRectangle(darkBrush, 0, 0, 5, 10);
            g.FillRectangle(brightBrush, 5, 0, 5, 10);
        }

        // Act
        Bitmap result = HdrUtilities.ConvertHdrToSdr(testBitmap);

        // Assert
        Assert.NotNull(result);
        Color darkPixel = result.GetPixel(2, 5);
        Color brightPixel = result.GetPixel(7, 5);

        // Dark pixels should remain relatively dark
        Assert.True(darkPixel.R < 100, "Dark pixel should remain dark");
        
        // Bright pixels should be tone mapped
        Assert.True(brightPixel.R > darkPixel.R, "Bright pixel should be brighter than dark pixel");

        // Cleanup
        testBitmap.Dispose();
        result.Dispose();
    }

    [Fact]
    public void ConvertHdrToSdr_PreservesAlphaChannel()
    {
        // Arrange
        Bitmap testBitmap = new(10, 10, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(testBitmap))
        {
            using Brush semiTransparentBrush = new SolidBrush(Color.FromArgb(128, 255, 255, 255));
            g.FillRectangle(semiTransparentBrush, 0, 0, 10, 10);
        }

        // Act
        Bitmap result = HdrUtilities.ConvertHdrToSdr(testBitmap);

        // Assert
        Color pixel = result.GetPixel(5, 5);
        Assert.Equal(128, pixel.A);

        // Cleanup
        testBitmap.Dispose();
        result.Dispose();
    }

    [Fact]
    public void IsHdrEnabledAtPoint_WithValidCoordinates_DoesNotThrow()
    {
        // Arrange
        int x = 100;
        int y = 100;

        // Act & Assert
        // Should not throw exception even if HDR is not available
        var exception = Record.Exception(() => HdrUtilities.IsHdrEnabledAtPoint(x, y));
        Assert.Null(exception);
    }

    [Fact]
    public void IsHdrEnabledAtPoint_WithNegativeCoordinates_ReturnsFalse()
    {
        // Arrange
        int x = -1;
        int y = -1;

        // Act
        bool result = HdrUtilities.IsHdrEnabledAtPoint(x, y);

        // Assert
        // Should return false for invalid coordinates
        Assert.False(result);
    }
}
