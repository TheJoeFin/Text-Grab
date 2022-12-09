using System.Drawing;
using ZXing.Windows.Compatibility;

namespace Text_Grab.Utilities;

public static class BarcodeUtilities
{

    public static string TryToReadBarcodes(Bitmap bitmap)
    {
        BarcodeReader barcodeReader = new()
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions { TryHarder = true }
        };

        ZXing.Result result = barcodeReader.Decode(bitmap);

        if (result is null)
            return string.Empty;
        return result.Text;
    }
}