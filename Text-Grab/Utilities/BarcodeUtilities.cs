using System;
using System.Drawing;
using Text_Grab.Models;
using ZXing.Common;
using ZXing.QrCode.Internal;
using ZXing.Rendering;
using ZXing.Windows.Compatibility;

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

    public static Bitmap GetQrCodeForText(string text)
    {
        BarcodeWriter barcodeWriter = new();

        barcodeWriter.Format = ZXing.BarcodeFormat.QR_CODE;
        barcodeWriter.Renderer = new BitmapRenderer();

        EncodingOptions encodingOptions = new()
        {
            Width = 500,
            Height = 500,
            Margin = 5,
        };
        encodingOptions.Hints.Add(ZXing.EncodeHintType.ERROR_CORRECTION, ErrorCorrectionLevel.L);

        barcodeWriter.Options = encodingOptions;

        Bitmap bitmap = barcodeWriter.Write(text);

        return bitmap;
    }
}