using ImageMagick;
using ImageMagick.Factories;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Text_Grab.Utilities;

/// <summary>
/// Detects whether successive captures of the same screen region differ by
/// running a Magick.NET Compare on small downscaled copies, so each check is
/// fast and allocates very little. The first capture after construction or
/// Reset() becomes a fixed baseline; later captures are judged against it.
/// Holds two small images between checks; dispose to release them.
/// </summary>
public sealed partial class ImageChangeDetector : IDisposable
{
    // Comparing fixed-size thumbnails keeps Compare cheap regardless of how
    // large the captured region is, while word-sized changes still register.
    private const int ComparisonSize = 96;

    // NormalizedMeanError at or below this is treated as noise, such as a
    // blinking caret or antialiasing differences between captures.
    private const double ChangeThreshold = 0.001;

    private readonly MagickImageFactory imageFactory = new();
    private MagickImage? baselineImage;
    private MagickImage? previousImage;

    /// <summary>
    /// Compares the capture against the fixed baseline. Returns true only
    /// when the capture differs from the baseline AND matches the previous
    /// capture, so a transient state (an indicator flash, a half-rendered
    /// frame) never triggers; the content must hold for two checks. The
    /// first capture after construction or Reset() establishes the baseline
    /// and returns false.
    /// </summary>
    public bool CheckForChangeAndUpdate(Bitmap capture)
    {
        using Bitmap thumbnail = CreateThumbnail(capture);

        if (imageFactory.Create(thumbnail) is not MagickImage currentImage)
            return false;

        if (baselineImage is null)
        {
            baselineImage = currentImage;
            previousImage?.Dispose();
            previousImage = null;
            return false;
        }

        bool differsFromBaseline = baselineImage.Compare(currentImage).NormalizedMeanError > ChangeThreshold;
        bool isStable = previousImage is not null
            && previousImage.Compare(currentImage).NormalizedMeanError <= ChangeThreshold;

        previousImage?.Dispose();
        previousImage = currentImage;

        return differsFromBaseline && isStable;
    }

    /// <summary>
    /// Drops the baseline so the next capture starts a fresh comparison.
    /// </summary>
    public void Reset()
    {
        baselineImage?.Dispose();
        baselineImage = null;
        previousImage?.Dispose();
        previousImage = null;
    }

    public void Dispose() => Reset();

    private static Bitmap CreateThumbnail(Bitmap source)
    {
        Bitmap thumbnail = new(ComparisonSize, ComparisonSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(thumbnail);
        // HighQualityBilinear prefilters when shrinking, so small on-screen
        // changes still influence the thumbnail instead of being skipped over.
        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        graphics.DrawImage(source, 0, 0, ComparisonSize, ComparisonSize);
        return thumbnail;
    }
}
