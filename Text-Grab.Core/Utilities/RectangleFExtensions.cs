using System.Drawing;

namespace Text_Grab;

/// <summary>
/// Portable geometry helpers for the Core tier.
///
/// Text-Grab's UI code works in <c>System.Windows.Rect</c>, which lives in WindowsBase.dll and
/// is therefore only available with <c>UseWPF=true</c>. Text-Grab.Core targets plain net10.0 and
/// Text-Grab.Core.Windows deliberately keeps <c>UseWPF=false</c>, so neither can use it.
///
/// <see cref="RectangleF"/> is the substitute: it lives in System.Drawing.Primitives, which is
/// part of the shared framework and genuinely cross-platform - unlike System.Drawing.Common's
/// <c>Bitmap</c>/<c>Graphics</c>, which are Windows-only and belong in Text-Grab.Core.Windows.
///
/// These mirror the WPF-typed helpers in Text-Grab/Extensions/ShapeExtensions.cs, which also
/// carries the conversions across the boundary (<c>AsRect</c> / <c>AsRectangleF</c>).
/// </summary>
public static class RectangleFExtensions
{
    /// <summary>
    /// Whether the rectangle is usable for layout or hit-testing: finite on every axis and
    /// non-degenerate. Mirrors ShapeExtensions.IsGood(Rect).
    /// </summary>
    public static bool IsGood(this RectangleF rect)
    {
        if (float.IsNaN(rect.X) || float.IsInfinity(rect.X))
            return false;

        if (float.IsNaN(rect.Y) || float.IsInfinity(rect.Y))
            return false;

        if (float.IsNaN(rect.Height) || rect.Height == 0 || float.IsInfinity(rect.Height))
            return false;

        if (float.IsNaN(rect.Width) || rect.Width == 0 || float.IsInfinity(rect.Width))
            return false;

        return true;
    }

    public static PointF CenterPoint(this RectangleF rect)
        => new(rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2));

    /// <summary>Scales position and size together, keeping the rectangle in the same relative spot.</summary>
    public static RectangleF GetScaledUpByFraction(this RectangleF rect, double scaleFactor)
        => new(
            (float)(rect.X * scaleFactor),
            (float)(rect.Y * scaleFactor),
            (float)(rect.Width * scaleFactor),
            (float)(rect.Height * scaleFactor));

    /// <summary>Scales size only, leaving the top-left corner where it is.</summary>
    public static RectangleF GetScaleSizeByFraction(this RectangleF rect, double scaleFactor)
        => new(
            rect.X,
            rect.Y,
            (float)(rect.Width * scaleFactor),
            (float)(rect.Height * scaleFactor));

    /// <summary>
    /// The smallest rectangle containing both inputs. An empty input is ignored rather than
    /// dragging the union back to the origin, so this can be folded over a sequence.
    /// </summary>
    public static RectangleF Union(this RectangleF rect, RectangleF other)
    {
        if (rect.IsEmpty)
            return other;

        if (other.IsEmpty)
            return rect;

        return RectangleF.Union(rect, other);
    }
}
