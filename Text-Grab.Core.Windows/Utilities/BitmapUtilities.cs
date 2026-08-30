using System;
using System.Drawing;
using System.IO;
using Text_Grab.Extensions;
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
}
