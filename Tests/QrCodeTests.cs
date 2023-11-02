using Text_Grab.Utilities;

namespace Tests;

public class QrCodeTests
{
    [Fact]
    public void generateSvgImage()
    {
        string testString = "This is only a test";
        var svg = BarcodeUtilities.GetSvgQrCodeForText(testString);

        Assert.NotNull(svg);
    }
}
