using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Text_Grab.Models;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;
using ZXing.Rendering;
using ZXing.Windows.Compatibility;
using static ZXing.Rendering.SvgRenderer;

namespace Text_Grab.Utilities;

public static class BarcodeUtilities
{
    private static OcrOutput EmptyBarcodeOutput => new() { Kind = OcrOutputKind.Barcode, RawOutput = string.Empty };

    public static OcrOutput TryToReadBarcodes(Bitmap bitmap)
    {
        if (!CanReadBitmapDimensions(bitmap))
            return EmptyBarcodeOutput;

        BarcodeReader barcodeReader = new()
        {
            AutoRotate = true,
            Options = new DecodingOptions { TryHarder = true }
        };

        Result? result = null;

        try
        {
            result = barcodeReader.Decode(bitmap);
        }
        catch (ArgumentException ex)
        {
            Debug.WriteLine($"Unable to decode barcode from bitmap: {ex.Message}");
            return EmptyBarcodeOutput;
        }
        catch (ObjectDisposedException ex)
        {
            Debug.WriteLine($"Unable to decode barcode from disposed bitmap: {ex.Message}");
            return EmptyBarcodeOutput;
        }
        catch (ExternalException ex)
        {
            Debug.WriteLine($"Unable to decode barcode from GDI+ bitmap: {ex.Message}");
            return EmptyBarcodeOutput;
        }

        string resultString = string.Empty;
        if (result is not null)
            resultString = result.Text;

        return new OcrOutput()
        {
            Kind = OcrOutputKind.Barcode,
            RawOutput = resultString,
            SourceBitmap = bitmap,
        };
    }

    private static bool CanReadBitmapDimensions(Bitmap? bitmap)
    {
        if (bitmap is null)
            return false;

        try
        {
            return bitmap.Width > 0 && bitmap.Height > 0;
        }
        catch (ArgumentException ex)
        {
            Debug.WriteLine($"Unable to read bitmap dimensions for barcode scanning: {ex.Message}");
            return false;
        }
        catch (ObjectDisposedException ex)
        {
            Debug.WriteLine($"Unable to read bitmap dimensions for disposed barcode bitmap: {ex.Message}");
            return false;
        }
        catch (ExternalException ex)
        {
            Debug.WriteLine($"Unable to read barcode bitmap dimensions due to GDI+ error: {ex.Message}");
            return false;
        }
    }

    public static Bitmap GetQrCodeForText(string text, ErrorCorrectionLevel correctionLevel)
    {
        BitmapRenderer bitmapRenderer = new()
        {
            Foreground = System.Drawing.Color.Black,
            Background = System.Drawing.Color.White
        };

        BarcodeWriter barcodeWriter = new()
        {
            Format = ZXing.BarcodeFormat.QR_CODE,
            Renderer = bitmapRenderer,
        };

        EncodingOptions encodingOptions = new()
        {
            Width = 500,
            Height = 500,
            Margin = 5,
        };
        encodingOptions.Hints.Add(ZXing.EncodeHintType.ERROR_CORRECTION, correctionLevel);
        barcodeWriter.Options = encodingOptions;

        Bitmap bitmap = barcodeWriter.Write(text);

        return bitmap;
    }

    public static SvgImage GetSvgQrCodeForText(string text, ErrorCorrectionLevel correctionLevel)
    {
        BarcodeWriterSvg barcodeWriter = new()
        {
            Format = ZXing.BarcodeFormat.QR_CODE,
            Renderer = new SvgRenderer()
        };

        EncodingOptions encodingOptions = new()
        {
            Width = 500,
            Height = 500,
            Margin = 5,
        };
        encodingOptions.Hints.Add(ZXing.EncodeHintType.ERROR_CORRECTION, correctionLevel);
        barcodeWriter.Options = encodingOptions;

        SvgImage svg = barcodeWriter.Write(text);

        return svg;
    }
}
