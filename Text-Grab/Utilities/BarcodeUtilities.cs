using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Media;
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

    public static OcrOutput TryToReadBarcodes(Bitmap bitmap)
    {
        BarcodeReader barcodeReader = new()
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions { TryHarder = true }
        };

        ZXing.Result result = barcodeReader.Decode(bitmap);

        string resultString = string.Empty;
        if (result is not null)
            resultString = result.Text;

        return new OcrOutput ()
        {
            Kind = OcrOutputKind.Barcode,
            RawOutput = resultString,
            SourceBitmap = bitmap,
        };
    }

    public static Bitmap GetQrCodeForText(string text, ErrorCorrectionLevel correctionLevel)
    {
        BitmapRenderer bitmapRenderer = new();
        bitmapRenderer.Foreground = System.Drawing.Color.Black;
        bitmapRenderer.Background = System.Drawing.Color.White;

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