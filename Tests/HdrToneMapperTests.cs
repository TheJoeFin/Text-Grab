using Text_Grab.Utilities.Hdr;

namespace Tests;

public class HdrToneMapperTests
{
    [Fact]
    public void SdrWhiteScaleFromNits_ReferenceWhite_IsOne()
    {
        Assert.Equal(1.0, HdrToneMapper.SdrWhiteScaleFromNits(80.0), 5);
    }

    [Theory]
    [InlineData(200.0, 2.5)]
    [InlineData(160.0, 2.0)]
    [InlineData(480.0, 6.0)]
    public void SdrWhiteScaleFromNits_ScalesRelativeTo80Nits(double nits, double expectedScale)
    {
        Assert.Equal(expectedScale, HdrToneMapper.SdrWhiteScaleFromNits(nits), 5);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(40.0)]
    public void SdrWhiteScaleFromNits_NeverBrightens(double nits)
    {
        // Values at or below the 80-nit reference must not produce a scale below 1.0,
        // which would brighten the image instead of correcting the HDR boost.
        Assert.Equal(1.0, HdrToneMapper.SdrWhiteScaleFromNits(nits), 5);
    }

    [Fact]
    public void LinearToSrgb_Endpoints()
    {
        Assert.Equal(0.0, HdrToneMapper.LinearToSrgb(0.0), 5);
        Assert.Equal(1.0, HdrToneMapper.LinearToSrgb(1.0), 5);
    }

    [Fact]
    public void ScRgbChannelToSrgbByte_SdrWhiteMapsToFullWhite()
    {
        // On a display with SDR white at 200 nits, SDR white sits at scRGB 2.5.
        double scale = HdrToneMapper.SdrWhiteScaleFromNits(200.0);

        Assert.Equal(255, HdrToneMapper.ScRgbChannelToSrgbByte(2.5, scale));
    }

    [Fact]
    public void ScRgbChannelToSrgbByte_UndoesHdrBrightnessBoost()
    {
        // The washout bug: SDR content lands above scRGB 1.0 on HDR displays. Normalizing by
        // the SDR white level must pull mid-gray back to a mid sRGB value rather than near-white.
        double scale = HdrToneMapper.SdrWhiteScaleFromNits(200.0);

        // Half of SDR white in linear light -> sRGB ~0.735 -> ~188.
        byte midGray = HdrToneMapper.ScRgbChannelToSrgbByte(1.25, scale);
        Assert.InRange(midGray, 186, 190);
    }

    [Fact]
    public void ScRgbChannelToSrgbByte_HighlightsAboveSdrWhiteClipToWhite()
    {
        double scale = HdrToneMapper.SdrWhiteScaleFromNits(200.0);

        // A specular highlight well above SDR white clamps to white rather than overflowing.
        Assert.Equal(255, HdrToneMapper.ScRgbChannelToSrgbByte(10.0, scale));
    }

    [Fact]
    public void ScRgbChannelToSrgbByte_NegativeWideGamutClampsToBlack()
    {
        double scale = HdrToneMapper.SdrWhiteScaleFromNits(200.0);

        Assert.Equal(0, HdrToneMapper.ScRgbChannelToSrgbByte(-0.5, scale));
    }

    [Fact]
    public void ScRgbChannelToSrgbByte_IsMonotonic()
    {
        double scale = HdrToneMapper.SdrWhiteScaleFromNits(200.0);
        int previous = -1;

        for (double channel = 0.0; channel <= 2.5; channel += 0.05)
        {
            int value = HdrToneMapper.ScRgbChannelToSrgbByte(channel, scale);
            Assert.True(value >= previous, $"Value dropped at channel {channel}");
            previous = value;
        }
    }
}
