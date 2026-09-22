using System.Drawing;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

public class ImageChangeDetectorTests
{
    private const string fontTestPath = @".\Images\FontTest.png";
    private const string fontSamplePath = @".\Images\font_sample.png";

    [Fact]
    public void FirstCapture_EstablishesBaseline_ReportsNoChange()
    {
        using ImageChangeDetector detector = new();
        using Bitmap image = new(FileUtilities.GetPathToLocalFile(fontTestPath));

        Assert.False(detector.CheckForChangeAndUpdate(image));
    }

    [Fact]
    public void SameCapture_ReportsNoChange()
    {
        using ImageChangeDetector detector = new();
        using Bitmap image = new(FileUtilities.GetPathToLocalFile(fontTestPath));

        _ = detector.CheckForChangeAndUpdate(image);

        Assert.False(detector.CheckForChangeAndUpdate(image));
    }

    [Fact]
    public void DifferentCapture_ReportsChange_OnceItHoldsForTwoChecks()
    {
        using ImageChangeDetector detector = new();
        using Bitmap image1 = new(FileUtilities.GetPathToLocalFile(fontTestPath));
        using Bitmap image2 = new(FileUtilities.GetPathToLocalFile(fontSamplePath));

        _ = detector.CheckForChangeAndUpdate(image1);

        // First differing capture is not yet stable, so no change is reported.
        Assert.False(detector.CheckForChangeAndUpdate(image2));
        Assert.True(detector.CheckForChangeAndUpdate(image2));
    }

    [Fact]
    public void TransientCapture_DoesNotReportChange()
    {
        using ImageChangeDetector detector = new();
        using Bitmap image1 = new(FileUtilities.GetPathToLocalFile(fontTestPath));
        using Bitmap image2 = new(FileUtilities.GetPathToLocalFile(fontSamplePath));

        _ = detector.CheckForChangeAndUpdate(image1);

        // A one-check blip (flash indicator, half-rendered frame) that
        // reverts to the baseline never reports a change.
        Assert.False(detector.CheckForChangeAndUpdate(image2));
        Assert.False(detector.CheckForChangeAndUpdate(image1));
        Assert.False(detector.CheckForChangeAndUpdate(image1));
    }

    [Fact]
    public void Reset_NextCaptureBecomesBaseline_ReportsNoChange()
    {
        using ImageChangeDetector detector = new();
        using Bitmap image1 = new(FileUtilities.GetPathToLocalFile(fontTestPath));
        using Bitmap image2 = new(FileUtilities.GetPathToLocalFile(fontSamplePath));

        _ = detector.CheckForChangeAndUpdate(image1);
        detector.Reset();

        Assert.False(detector.CheckForChangeAndUpdate(image2));
    }

    [Fact]
    public void ImagesDifferBeyondThreshold_IdenticalImages_ReportsNoDifference()
    {
        using Bitmap image1 = new(FileUtilities.GetPathToLocalFile(fontTestPath));
        using Bitmap image2 = new(FileUtilities.GetPathToLocalFile(fontTestPath));

        Assert.False(ImageChangeDetector.ImagesDifferBeyondThreshold(image1, image2));
    }

    [Fact]
    public void ImagesDifferBeyondThreshold_DifferentImages_ReportsDifference()
    {
        using Bitmap image1 = new(FileUtilities.GetPathToLocalFile(fontTestPath));
        using Bitmap image2 = new(FileUtilities.GetPathToLocalFile(fontSamplePath));

        Assert.True(ImageChangeDetector.ImagesDifferBeyondThreshold(image1, image2));
    }
}
