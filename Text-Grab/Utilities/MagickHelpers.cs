using ImageMagick;
using ImageMagick.Factories;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Text_Grab.Utilities;

public class MagickHelpers
{
    public static ImageSource? Contrast(ImageSource? source)
    {
        BitmapImage? bitmapImage = null;
        if (source is CachedBitmap cachedBitmap)
        {
            bitmapImage = ImageMethods.CachedBitmapToBitmapImage(cachedBitmap);
        }
        else if (source is BitmapImage)
        {
            bitmapImage = source as BitmapImage;
        }

        if (bitmapImage is null)
            return null;

        Bitmap bitmap = ImageMethods.BitmapImageToBitmap(bitmapImage);

        MagickImageFactory imageFactory = new();
        if (imageFactory.Create(bitmap) is not MagickImage magickImage)
            return null;

        magickImage.SigmoidalContrast(10);

        return magickImage.ToBitmapSource();
    }

    public static ImageSource? Grayscale(ImageSource? source)
    {
        BitmapImage? bitmapImage = null;
        if (source is CachedBitmap cachedBitmap)
        {
            bitmapImage = ImageMethods.CachedBitmapToBitmapImage(cachedBitmap);
        }
        else if (source is BitmapImage)
        {
            bitmapImage = source as BitmapImage;
        }

        if (bitmapImage is null)
            return null;

        Bitmap bitmap = ImageMethods.BitmapImageToBitmap(bitmapImage);

        MagickImageFactory imageFactory = new();
        if (imageFactory.Create(bitmap) is not MagickImage magickImage)
            return null;

        magickImage.Grayscale();

        return magickImage.ToBitmapSource();
    }

    public static ImageSource? Invert(ImageSource? source)
    {
        BitmapImage? bitmapImage = null;
        if (source is CachedBitmap cachedBitmap)
        {
           bitmapImage = ImageMethods.CachedBitmapToBitmapImage(cachedBitmap);
        }
        else if (source is BitmapImage)
        {
            bitmapImage = source as BitmapImage;
        }

        if (bitmapImage is null)
            return null;

        // Calculate stride of source
        int stride = (bitmapImage.PixelWidth * bitmapImage.Format.BitsPerPixel + 7) / 8;

        // Create data array to hold bitmapImage pixel data
        int length = stride * bitmapImage.PixelHeight;
        byte[] data = new byte[length];

        // Copy bitmapImage image pixels to the data array
        bitmapImage.CopyPixels(data, stride, 0);

        // Change this loop for other formats
        for (int i = 0; i < length; i += 4)
        {
            data[i] = (byte)(255 - data[i]); //R
            data[i + 1] = (byte)(255 - data[i + 1]); //G
            data[i + 2] = (byte)(255 - data[i + 2]); //B
                                                     //data[i + 3] = (byte)(255 - data[i + 3]); //A
        }

        // Create a new BitmapSource from the inverted pixel buffer
        return BitmapSource.Create(
            bitmapImage.PixelWidth, bitmapImage.PixelHeight,
            bitmapImage.DpiX, bitmapImage.DpiY, bitmapImage.Format,
            null, data, stride);
    }
}
