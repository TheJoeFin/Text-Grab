using System;

namespace Text_Grab.Utilities.Hdr;

/// <summary>
/// Pure, side-effect-free helpers that convert HDR (scRGB, FP16) pixel values captured
/// from an HDR display down to standard 8-bit sRGB values.
///
/// Windows composites the desktop in scRGB when HDR is enabled: values are linear and
/// unbounded, where 1.0 corresponds to the sRGB reference white of 80 nits. The user's
/// "SDR content brightness" slider pushes SDR white well above 80 nits, so ordinary
/// (non-HDR) content lands at scRGB values greater than 1.0. A naive capture keeps that
/// boost, which is why HDR screenshots come out washed out / too bright (see issue #111).
///
/// The fix is to divide by the display's actual SDR white level so SDR content maps back
/// to 1.0, then encode with the sRGB transfer function. HDR highlights above SDR white
/// clip to white, matching how the same content looks with HDR turned off.
/// </summary>
public static class HdrToneMapper
{
    /// <summary>scRGB value 1.0 equals the sRGB reference white of 80 nits.</summary>
    public const double SdrReferenceWhiteNits = 80.0;

    /// <summary>
    /// Converts a display SDR white level (in nits) to the scRGB value at which SDR white sits.
    /// Clamped so the scale is never below 1.0 (80 nits), which would brighten rather than
    /// correct the image.
    /// </summary>
    public static double SdrWhiteScaleFromNits(double sdrWhiteNits)
    {
        double nits = sdrWhiteNits > 0 ? sdrWhiteNits : SdrReferenceWhiteNits;
        return Math.Max(nits, SdrReferenceWhiteNits) / SdrReferenceWhiteNits;
    }

    /// <summary>
    /// Maps a single linear scRGB channel value to an 8-bit sRGB value.
    /// </summary>
    /// <param name="channel">Linear scRGB channel value (may be negative for wide-gamut colors or above 1.0 for HDR highlights).</param>
    /// <param name="sdrWhiteScale">scRGB value of SDR white, from <see cref="SdrWhiteScaleFromNits"/>.</param>
    public static byte ScRgbChannelToSrgbByte(double channel, double sdrWhiteScale)
    {
        if (sdrWhiteScale <= 0)
            sdrWhiteScale = 1.0;

        // Normalize so SDR white becomes 1.0, then clamp: negatives (outside sRGB gamut)
        // go to 0 and HDR highlights above SDR white clip to white.
        double normalized = Math.Clamp(channel / sdrWhiteScale, 0.0, 1.0);
        double srgb = LinearToSrgb(normalized);
        return (byte)Math.Clamp((int)(srgb * 255.0 + 0.5), 0, 255);
    }

    /// <summary>
    /// Applies the sRGB transfer function (opto-electronic) to a linear value in [0, 1].
    /// </summary>
    public static double LinearToSrgb(double value)
    {
        if (value <= 0.0031308)
            return 12.92 * value;

        return 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055;
    }
}
