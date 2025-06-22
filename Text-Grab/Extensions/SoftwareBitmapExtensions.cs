// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Text_Grab.Extensions;

public static class SoftwareBitmapExtensions
{
    public static async Task<SoftwareBitmapSource> ToSourceAsync(this SoftwareBitmap softwareBitmap)
    {
        SoftwareBitmapSource source = new();

        if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            SoftwareBitmap convertedBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            await source.SetBitmapAsync(convertedBitmap);
        }
        else
        {
            await source.SetBitmapAsync(softwareBitmap);
        }

        return source;
    }

    public static async Task<SoftwareBitmap> FilePathToSoftwareBitmapAsync(this string filePath)
    {
        using IRandomAccessStream stream = await StorageFileExtensions.CreateStreamAsync(filePath);
        // Create the decoder from the stream
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        // Get the SoftwareBitmap representation of the file
        return await decoder.GetSoftwareBitmapAsync();
    }

    public static SoftwareBitmap CreateMaskBitmap(this SoftwareBitmap bitmap, Rect rect)
    {
        byte[] pixelData = new byte[bitmap.PixelWidth * bitmap.PixelHeight];

        // Fill the entire bitmap with black (0)
        for (int i = 0; i < pixelData.Length; i++)
        {
            pixelData[i] = 0; // Black
        }

        // Set the region bounded by the rectangle to white (255)
        for (int y = (int)rect.Y; y < (int)(rect.Y + rect.Height); y++)
        {
            for (int x = (int)rect.X; x < (int)(rect.X + rect.Width); x++)
            {
                pixelData[(y * bitmap.PixelWidth) + x] = 255; // White
            }
        }

        // Create a new SoftwareBitmap with Gray8 pixel format
        SoftwareBitmap maskBitmap = new(BitmapPixelFormat.Gray8, bitmap.PixelWidth, bitmap.PixelHeight, BitmapAlphaMode.Ignore);

        maskBitmap.CopyFromBuffer(pixelData.AsBuffer());

        return maskBitmap;
    }

    public static SoftwareBitmap ApplyMask(this SoftwareBitmap inputBitmap, SoftwareBitmap grayMask)
    {
        if (inputBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || grayMask.BitmapPixelFormat != BitmapPixelFormat.Gray8)
        {
            throw new Exception("Input bitmap must be Bgra8 and gray mask must be Gray8");
        }

        byte[] inputBuffer = new byte[4 * inputBitmap.PixelWidth * inputBitmap.PixelHeight];
        byte[] maskBuffer = new byte[grayMask.PixelWidth * grayMask.PixelHeight];
        inputBitmap.CopyToBuffer(inputBuffer.AsBuffer());
        grayMask.CopyToBuffer(maskBuffer.AsBuffer());

        for (int y = 0; y < inputBitmap.PixelHeight; y++)
        {
            for (int x = 0; x < inputBitmap.PixelWidth; x++)
            {
                int inputIndex = ((y * inputBitmap.PixelWidth) + x) * 4;
                int maskIndex = (y * grayMask.PixelWidth) + x;

                if (maskBuffer[maskIndex] == 0)
                {
                    inputBuffer[inputIndex + 3] = 0; // Set alpha to 0 for background
                }
            }
        }

        SoftwareBitmap segmentedBitmap = new(BitmapPixelFormat.Bgra8, inputBitmap.PixelWidth, inputBitmap.PixelHeight);
        segmentedBitmap.CopyFromBuffer(inputBuffer.AsBuffer());
        return segmentedBitmap;
    }

    public static async Task<SoftwareBitmap> CreateSoftwareBitmap(this System.Drawing.Bitmap bitmap)
    {
        await using MemoryStream memory = new();
        using WrappingStream wrapper = new(memory);

        bitmap.Save(wrapper, ImageFormat.Bmp);
        wrapper.Position = 0;
        BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(wrapper.AsRandomAccessStream());
        using SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();
        await wrapper.FlushAsync();

        return softwareBmp;
    }
}
