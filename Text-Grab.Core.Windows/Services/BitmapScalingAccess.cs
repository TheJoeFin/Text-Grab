using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Text_Grab.Services;

/// <summary>
/// How OCR code scales a bitmap before it is recognized.
///
/// The same delegate-resolver shape as <see cref="SettingsAccess"/> and
/// <see cref="InputLanguageAccess"/>. The app's scaler, <c>ImageMethods.ScaleBitmapUniform</c>,
/// scales through a WPF <c>TransformedBitmap</c>, which Core.Windows cannot reference. Every
/// Windows-OCR call scales its input to an ideal line height first, so the engine dispatch could not
/// leave the app without this seam (v5 F3).
///
/// The app registers its WPF scaler from a module initializer, so every grab in the app scales
/// exactly as it did before. A host with nothing registered - <c>Tests.Core.Windows</c>, or any
/// future headless runner - gets <see cref="DefaultScaleUniform"/>, a GDI+ bicubic scale. Not
/// registered is a normal state here, not an error.
/// </summary>
public static class BitmapScalingAccess
{
    private static Func<Bitmap, double, Bitmap>? _scaler;

    /// <summary>
    /// Registers the scaler. The app calls this from a module initializer. Tests may call it
    /// again to substitute a fake.
    /// </summary>
    public static void SetScaler(Func<Bitmap, double, Bitmap> scaler)
        => _scaler = scaler ?? throw new ArgumentNullException(nameof(scaler));

    /// <summary>Drops the registered scaler. For tests that need to restore a clean slate.</summary>
    public static void ClearScaler() => _scaler = null;

    /// <summary>Whether a scaler has been registered.</summary>
    public static bool IsConfigured => _scaler is not null;

    /// <summary>
    /// Returns a new bitmap scaled by <paramref name="scale"/> on both axes. The caller owns and
    /// disposes the result; <paramref name="bitmap"/> is not modified.
    /// </summary>
    public static Bitmap ScaleUniform(Bitmap bitmap, double scale)
        => (_scaler ?? DefaultScaleUniform)(bitmap, scale);

    /// <summary>
    /// The scaler used when none is registered: GDI+ high-quality bicubic into a 32bpp PARGB
    /// bitmap, the pixel format the WPF scaler returns.
    /// </summary>
    internal static Bitmap DefaultScaleUniform(Bitmap bitmap, double scale)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        int width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        int height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

        Bitmap scaled = new(width, height, PixelFormat.Format32bppPArgb);
        using Graphics graphics = Graphics.FromImage(scaled);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // TileFlipXY stops the bicubic kernel sampling transparent pixels past the edges, which
        // would otherwise leave a faint dark border.
        using ImageAttributes attributes = new();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        graphics.DrawImage(
            bitmap,
            new Rectangle(0, 0, width, height),
            0, 0, bitmap.Width, bitmap.Height,
            GraphicsUnit.Pixel,
            attributes);

        return scaled;
    }
}
