using System;
using System.Drawing;
using System.IO;
using Text_Grab.Extensions;
using Text_Grab.Services;
using Text_Grab.Utilities.Hdr;
using Windows.Storage.Streams;

namespace Text_Grab;

public static class BitmapUtilities
{
    public static Bitmap PadImage(Bitmap image, int minW = 64, int minH = 64)
    {
        if (image.Height >= minH && image.Width >= minW)
            return image;

        int width = Math.Max(image.Width + 16, minW + 16);
        int height = Math.Max(image.Height + 16, minH + 16);

        // Create a compatible bitmap
        Bitmap destination = new(width, height, image.PixelFormat);
        using Graphics gd = Graphics.FromImage(destination);

        gd.Clear(image.GetPixel(0, 0));
        gd.DrawImageUnscaled(image, 8, 8);

        return destination;
    }

    public static Bitmap GetBitmapFromIRandomAccessStream(IRandomAccessStream stream)
    {
        Stream managedStream = stream.AsStream();
        if (managedStream.CanSeek)
            managedStream.Position = 0;

        using Bitmap bitmap = new(managedStream);
        return new Bitmap(bitmap);
    }

    internal static RotateFlipType GetRotateFlipType(string path)
    {
        using Image img = Image.FromFile(path);
        RotateFlipType rotateFlipType = img.GetRotateFlipType();
        return rotateFlipType;
    }

    /// <summary>
    /// Grabs a virtual-desktop region as a bitmap, preferring the HDR-aware capture path when the
    /// user has enabled it. Internal rather than public: its only callers are ImageMethods'
    /// GetRegionOfScreenAsBitmap and GetWindowsBoundsBitmap, both of which stay in the app -
    /// GetRegionOfScreenAsBitmap because it writes to HistoryService, GetWindowsBoundsBitmap
    /// because it pattern-matches on the GrabFrame view. It was private to ImageMethods before
    /// batch 5b unblocked it by moving HdrScreenCapture into this assembly.
    /// </summary>
    internal static Bitmap CaptureScreenRegion(Rectangle region)
    {
        if (SettingsAccess.Current.HdrCaptureCorrection)
        {
            Bitmap? hdrBitmap = HdrScreenCapture.TryCaptureRegion(region);
            if (hdrBitmap is not null)
                return hdrBitmap;
        }

        Bitmap bmp = new(region.Width, region.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(region.Left, region.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }
}
