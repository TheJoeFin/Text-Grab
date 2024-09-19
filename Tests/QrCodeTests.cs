using Text_Grab.Utilities;
using ZXing.QrCode.Internal;

namespace Tests;

public class QrCodeTests
{
    [Fact]
    public void generateSvgImage()
    {
        string testString = "This is only a test";
        ZXing.Rendering.SvgRenderer.SvgImage svg = BarcodeUtilities.GetSvgQrCodeForText(testString, ErrorCorrectionLevel.L);

        Assert.NotNull(svg);
    }
}
