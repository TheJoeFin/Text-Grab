using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Text_Grab.Utilities;

/// <summary>
/// Split out of FreeformCaptureUtilities: the one member of that file with no WPF rendering
/// type in its signature. GetBounds and BuildGeometry return System.Windows.Rect/PathGeometry
/// and stay in the app.
/// </summary>
public static class BitmapMaskUtilities
{
    public static Bitmap CreateMaskedBitmap(Bitmap sourceBitmap, IReadOnlyList<PointF> pointsRelativeToBounds)
    {
        ArgumentNullException.ThrowIfNull(sourceBitmap);

        if (pointsRelativeToBounds is null || pointsRelativeToBounds.Count < 3)
            return new Bitmap(sourceBitmap);

        Bitmap maskedBitmap = new(sourceBitmap.Width, sourceBitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(maskedBitmap);
        using GraphicsPath graphicsPath = new();

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(System.Drawing.Color.Gray);

        graphicsPath.AddPolygon([.. pointsRelativeToBounds]);
        graphics.SetClip(graphicsPath);
        graphics.DrawImage(sourceBitmap, new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height));

        return maskedBitmap;
    }
}
