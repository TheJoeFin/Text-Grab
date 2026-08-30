using System.Drawing;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

// WPF half of the original Tests/BarcodeUtilitiesTests.cs (batch 7a). [WpfFact] needs
// Xunit.StaFact, which cannot be referenced from Tests.Core.Windows (it pulls in WindowsBase,
// which TierBoundaryTests bans), so this one test stayed behind while the rest moved to
// Tests.Core.Windows/BarcodeUtilitiesTests.cs, which kept the original name.
public class BarcodeUtilitiesImageTests
{
    [WpfFact]
    public void ReadTestSingleQRCode()
    {
        string expectedOutput = "This is a test of the QR Code system";
        string testFilePath = FileUtilities.GetPathToLocalFile(@".\Images\QrCodeTestImage.png");

        Bitmap testBmp = new(testFilePath);

        List<OcrOutput> result = BarcodeUtilities.TryToReadBarcodes(testBmp);

        Assert.Single(result);
        Assert.Equal(expectedOutput, result[0].RawOutput);
    }
}
