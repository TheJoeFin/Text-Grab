using Text_Grab.Utilities;
using ZXing.QrCode.Internal;

namespace Tests;

public class QrCodeTests
{
    [Fact]
    public void generateSvgImage()
    {
        string testString = "This is only a test";
        var svg = BarcodeUtilities.GetSvgQrCodeForText(testString, ErrorCorrectionLevel.L);

        Assert.NotNull(svg);
    }
}
