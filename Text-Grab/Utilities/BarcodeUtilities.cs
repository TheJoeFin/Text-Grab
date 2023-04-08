using System.Drawing;
using Text_Grab.Models;
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
}