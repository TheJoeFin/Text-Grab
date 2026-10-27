using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Extensions;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Text_Grab.Utilities.Hdr;
using Text_Grab.Views;
using Windows.Storage.Streams;
using BitmapEncoder = System.Windows.Media.Imaging.BitmapEncoder;
using BitmapFrame = System.Windows.Media.Imaging.BitmapFrame;
using Point = System.Windows.Point;

namespace Text_Grab;

public static class ImageMethods
{
    public static Bitmap BitmapImageToBitmap(BitmapImage bitmapImage)
    {
        using MemoryStream outStream = new();
        using WrappingStream wrapper = new(outStream);

        BitmapEncoder enc = new BmpBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bitmapImage));
        enc.Save(wrapper);
        using Bitmap bitmap = new(wrapper);
        wrapper.Flush();

        return new Bitmap(bitmap);
    }

    public static BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using MemoryStream memory = new();
        using WrappingStream wrapper = new(memory);

        bitmap.Save(wrapper, ImageFormat.Bmp);
        wrapper.Position = 0;
        BitmapImage bitmapImage = new();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = wrapper;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.StreamSource = null;
        bitmapImage.Freeze();

        memory.Flush();
        wrapper.Flush();

        return bitmapImage;
    }

    public static BitmapImage CachedBitmapToBitmapImage(System.Windows.Media.Imaging.CachedBitmap cachedBitmap)
    {
        BitmapImage bitmapImage = new();
        using (MemoryStream memoryStream = new())
        {
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(cachedBitmap));
            encoder.Save(memoryStream);
            memoryStream.Position = 0;

            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = memoryStream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
        }
        return bitmapImage;
    }

    /// <summary>
    /// Captures a virtual-desktop region to a bitmap. When the region lives on an HDR display
    /// and HDR correction is enabled, this uses Windows.Graphics.Capture to grab the frame at
    /// full precision and tone-map it back to SDR so the result isn't washed out (issue #111).
    /// Falls back to a plain GDI screen copy otherwise or if HDR capture fails.
    /// </summary>
    public static Bitmap GetRegionOfScreenAsBitmap(Rectangle region, bool cacheResult = true)
    {
        Bitmap bmp = BitmapUtilities.CaptureScreenRegion(region);
        bmp = BitmapUtilities.PadImage(bmp);

        if (cacheResult)
            Singleton<HistoryService>.Instance.CacheLastBitmap(bmp);

        return bmp;
    }

    public static Bitmap GetWindowsBoundsBitmap(Window passedWindow)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
        int windowWidth = (int)(passedWindow.ActualWidth * dpi.DpiScaleX);
        int windowHeight = (int)(passedWindow.ActualHeight * dpi.DpiScaleY);

        Point absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X;
        int thisCorrectedTop = (int)absPosPoint.Y;

        if (passedWindow is GrabFrame grabFrame)
        {
            Rect imageRect = grabFrame.GetImageContentRect();

            if (imageRect == Rect.Empty)
            {
                // Ask WPF's layout engine for the exact physical-pixel bounds of the
                // transparent content area. This is always correct regardless of DPI,
                // border thickness, or title/bottom bar heights.
                Rectangle contentRect = grabFrame.GetContentAreaScreenRect();
                if (contentRect == Rectangle.Empty)
                    return new Bitmap(1, 1);
                thisCorrectedLeft = contentRect.X;
                thisCorrectedTop = contentRect.Y;
                windowWidth = contentRect.Width;
                windowHeight = contentRect.Height;
            }
            else
            {
                thisCorrectedLeft = (int)imageRect.Left;
                thisCorrectedTop = (int)imageRect.Top;
                windowWidth = (int)imageRect.Width;
                windowHeight = (int)imageRect.Height;
            }
        }

        Rectangle windowRegion = new(thisCorrectedLeft, thisCorrectedTop, windowWidth, windowHeight);
        return BitmapUtilities.CaptureScreenRegion(windowRegion);
    }

    public static ImageSource GetWindowBoundsImage(Window passedWindow)
    {
        Bitmap bmp = GetWindowsBoundsBitmap(passedWindow);
        ImageSource imageSource = BitmapToImageSource(bmp);
        bmp.Dispose();
        return imageSource;
    }

    public static Bitmap ScaleBitmapUniform(Bitmap passedBitmap, double scale)
    {
        using MemoryStream memory = new();
        using WrappingStream wrapper = new(memory);

        passedBitmap.Save(wrapper, ImageFormat.Bmp);
        wrapper.Position = 0;
        BitmapImage bitmapImage = new();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = wrapper;
        bitmapImage.CacheOption = BitmapCacheOption.None;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        wrapper.Flush();

        TransformedBitmap transformedBitmap = new();
        transformedBitmap.BeginInit();
        transformedBitmap.Source = bitmapImage;
        transformedBitmap.Transform = new ScaleTransform(scale, scale);
        transformedBitmap.EndInit();
        transformedBitmap.Freeze();
        return BitmapSourceToBitmap(transformedBitmap);

    }

    public static Bitmap InteropBitmapToBitmap(System.Windows.Interop.InteropBitmap source)
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

    public static Bitmap? ImageSourceToBitmap(ImageSource? source)
    {
        return source switch
        {
            BitmapSource bitmapSource => BitmapSourceToBitmap(bitmapSource),
            _ => null
        };
    }

    public static BitmapImage GetBitmapImageFromIRandomAccessStream(IRandomAccessStream stream)
    {
        BitmapImage bmp = new();
        Stream ioStream = stream.AsStream();
        // Create a new BitmapImage and use the SetSourceAsync method to 
        // initialize it from the given IRandomAccessStream.
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.None;
        bmp.StreamSource = ioStream;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    internal static void RotateImage(BitmapImage droppedImage, RotateFlipType rotateFlipType)
    {
        // Only consider basic rotation for now
        switch ((int)rotateFlipType)
        {
            case 1:
                droppedImage.Rotation = Rotation.Rotate90;
                break;
            case 2:
                droppedImage.Rotation = Rotation.Rotate180;
                break;
            case 3:
                droppedImage.Rotation = Rotation.Rotate270;
                break;
            default:
                break;
        }
    }
}
