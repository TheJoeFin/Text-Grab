using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Views;
using BitmapEncoder = System.Windows.Media.Imaging.BitmapEncoder;
using BitmapFrame = System.Windows.Media.Imaging.BitmapFrame;
using Point = System.Windows.Point;

namespace Text_Grab;

public static class ImageMethods
{
    public static Bitmap PadImage(Bitmap image, int minW = 64, int minH = 64)
    {
        if (image.Height >= minH && image.Width >= minW)
            return image;

        int width = Math.Max(image.Width + 16, minW + 16);
        int height = Math.Max(image.Height + 16, minH + 16);

        // Create a compatible bitmap
        Bitmap dest = new(width, height, image.PixelFormat);
        using Graphics gd = Graphics.FromImage(dest);

        gd.Clear(image.GetPixel(0, 0));
        gd.DrawImageUnscaled(image, 8, 8);

        return dest;
    }

    public static Bitmap BitmapImageToBitmap(BitmapImage bitmapImage)
    {
        using MemoryStream outStream = new();

        BitmapEncoder enc = new BmpBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bitmapImage));
        enc.Save(outStream);
        using Bitmap bitmap = new(outStream);
        outStream.Flush();

        return new Bitmap(bitmap);
    }

    public static BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using MemoryStream memory = new();

        bitmap.Save(memory, ImageFormat.Bmp);
        memory.Position = 0;
        BitmapImage bitmapimage = new();
        bitmapimage.BeginInit();
        bitmapimage.StreamSource = memory;
        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapimage.EndInit();
        bitmapimage.Freeze();

        memory.Flush();

        return bitmapimage;
    }

    public static Bitmap GetRegionOfScreenAsBitmap(Rectangle region)
    {
        Bitmap bmp = new(region.Width, region.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(region.Left, region.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        bmp = PadImage(bmp);
        return bmp;
    }

    public static Bitmap GetWindowsBoundsBitmap(Window passedWindow)
    {
        bool isGrabFrame = false;
        if (passedWindow is GrabFrame)
            isGrabFrame = true;

        DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
        int windowWidth = (int)(passedWindow.ActualWidth * dpi.DpiScaleX);
        int windowHeight = (int)(passedWindow.ActualHeight * dpi.DpiScaleY);

        Point absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)(absPosPoint.X);
        int thisCorrectedTop = (int)(absPosPoint.Y);

        if (isGrabFrame)
        {
            thisCorrectedLeft = (int)((absPosPoint.X + 2) * dpi.DpiScaleX);
            thisCorrectedTop = (int)((absPosPoint.Y + 26) * dpi.DpiScaleY);
            windowWidth -= (int)(4 * dpi.DpiScaleX);
            windowHeight -= (int)(70 * dpi.DpiScaleY);
        }

        Bitmap bmp = new(windowWidth, windowHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }

    public static ImageSource GetWindowBoundsImage(Window passedWindow)
    {
        Bitmap bmp = GetWindowsBoundsBitmap(passedWindow);
        return BitmapToImageSource(bmp);
    }

    public static Bitmap ScaleBitmapUniform(Bitmap passedBitmap, double scale)
    {
        using MemoryStream memory = new();

        passedBitmap.Save(memory, ImageFormat.Bmp);
        memory.Position = 0;
        BitmapImage bitmapimage = new();
        bitmapimage.BeginInit();
        bitmapimage.StreamSource = memory;
        bitmapimage.CacheOption = BitmapCacheOption.None;
        bitmapimage.EndInit();
        bitmapimage.Freeze();

        memory.Flush();

        TransformedBitmap tbmpImg = new();
        tbmpImg.BeginInit();
        tbmpImg.Source = bitmapimage;
        tbmpImg.Transform = new ScaleTransform(scale, scale);
        tbmpImg.EndInit();
        tbmpImg.Freeze();
        return BitmapSourceToBitmap(tbmpImg);

    }

    public static Bitmap BitmapSourceToBitmap(BitmapSource source)
    {
        Bitmap bmp = new(
          source.PixelWidth,
          source.PixelHeight,
          System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        BitmapData data = bmp.LockBits(
          new Rectangle(System.Drawing.Point.Empty, bmp.Size),
          ImageLockMode.WriteOnly,
          System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        source.CopyPixels(
          Int32Rect.Empty,
          data.Scan0,
          data.Height * data.Stride,
          data.Stride);
        bmp.UnlockBits(data);
        return bmp;
    }
}
