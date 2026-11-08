using Text_Grab.Models;
using Text_Grab.Views;
using Windows.Foundation;

namespace Tests;

public class FullscreenGrabFindOnScreenTests
{
    [Fact]
    public void FindOcrMatches_MapsPhraseAcrossWordsToSingleBounds()
    {
        GeneratedOcrLinesWords ocrResult = CreateOcrResult(
            new GeneratedOcrLine
            {
                Words =
                [
                    new GeneratedOcrWord { Text = "Call", BoundingBox = new Rect(10, 20, 30, 12) },
                    new GeneratedOcrWord { Text = "555", BoundingBox = new Rect(45, 20, 25, 12) },
                    new GeneratedOcrWord { Text = "1234", BoundingBox = new Rect(75, 20, 35, 12) }
                ]
            });

        FullscreenFindOnScreenResult result = FullscreenGrab.FindOcrMatches(
            ocrResult,
            "call 555",
            isSpaceJoining: true,
            isRightToLeft: false);

        Assert.Equal(1, result.MatchCount);
        Assert.Equal(new Rect(10, 20, 60, 12), Assert.Single(result.Bounds));
    }

    [Fact]
    public void FindOcrMatches_IsCaseInsensitiveAndCountsEachLineOccurrence()
    {
        GeneratedOcrLinesWords ocrResult = CreateOcrResult(
            GeneratedOcrLine.FromText("Quick brown", new Rect(0, 0, 100, 12)),
            GeneratedOcrLine.FromText("a QUICK search", new Rect(0, 20, 120, 12)));

        FullscreenFindOnScreenResult result = FullscreenGrab.FindOcrMatches(
            ocrResult,
            "quick",
            isSpaceJoining: true,
            isRightToLeft: false);

        Assert.Equal(2, result.MatchCount);
        Assert.Equal(2, result.Bounds.Count);
    }

    [Fact]
    public void FindOcrMatches_UsesNoSeparatorForNonSpaceJoiningLanguages()
    {
        GeneratedOcrLinesWords ocrResult = CreateOcrResult(
            new GeneratedOcrLine
            {
                Words =
                [
                    new GeneratedOcrWord { Text = "画", BoundingBox = new Rect(10, 10, 12, 12) },
                    new GeneratedOcrWord { Text = "面", BoundingBox = new Rect(22, 10, 12, 12) }
                ]
            });

        FullscreenFindOnScreenResult result = FullscreenGrab.FindOcrMatches(
            ocrResult,
            "画面",
            isSpaceJoining: false,
            isRightToLeft: false);

        Assert.Equal(1, result.MatchCount);
        Assert.Equal(new Rect(10, 10, 24, 12), Assert.Single(result.Bounds));
    }

    [Fact]
    public void ScaleOcrBoundsToOverlay_AccountsForOcrAndDpiScale()
    {
        System.Windows.Rect result = FullscreenGrab.ScaleOcrBoundsToOverlay(
            new Rect(60, 40, 80, 20),
            ocrScale: 2,
            dpiScaleX: 1.5,
            dpiScaleY: 1.25);

        Assert.Equal(new System.Windows.Rect(20, 16, 80 / 3d, 8), result);
    }

    [Fact]
    public void BuildFindOnScreenCopyText_CopiesEveryMatchedValue()
    {
        FullscreenFindOnScreenDisplayMatch[] matches =
        [
            new("Quick", "Quick brown", new System.Windows.Rect(0, 0, 10, 10), new System.Windows.Rect(0, 0, 100, 10)),
            new("quick", "The quick search", new System.Windows.Rect(0, 20, 10, 10), new System.Windows.Rect(0, 20, 100, 10))
        ];

        string text = FullscreenGrab.BuildFindOnScreenCopyText(
            matches,
            FsgFindEnterAction.CopyMatches);

        Assert.Equal($"Quick{Environment.NewLine}quick", text);
    }

    [Fact]
    public void BuildFindOnScreenCopyText_DeduplicatesMatchedLines()
    {
        FullscreenFindOnScreenDisplayMatch[] matches =
        [
            new("quick", "quick quick", new System.Windows.Rect(0, 0, 10, 10), new System.Windows.Rect(0, 0, 40, 10)),
            new("quick", "quick quick", new System.Windows.Rect(20, 0, 10, 10), new System.Windows.Rect(0, 0, 40, 10)),
            new("quick", "another quick", new System.Windows.Rect(0, 20, 10, 10), new System.Windows.Rect(0, 20, 60, 10)),
            new("quick", "quick quick", new System.Windows.Rect(0, 40, 10, 10), new System.Windows.Rect(0, 40, 40, 10))
        ];

        string text = FullscreenGrab.BuildFindOnScreenCopyText(
            matches,
            FsgFindEnterAction.CopyMatchedLines);

        Assert.Equal($"quick quick{Environment.NewLine}another quick{Environment.NewLine}quick quick", text);
    }

    [Fact]
    public void GetFindOnScreenRegion_UnionsAndPadsAllMatches()
    {
        System.Windows.Rect region = FullscreenGrab.GetFindOnScreenRegion(
        [
            new System.Windows.Rect(10, 20, 30, 10),
            new System.Windows.Rect(100, 80, 20, 10)
        ]);

        Assert.Equal(new System.Windows.Rect(7, 17, 116, 76), region);
    }

    private static GeneratedOcrLinesWords CreateOcrResult(params GeneratedOcrLine[] lines)
    {
        return new GeneratedOcrLinesWords
        {
            Lines = lines,
            Text = string.Join(Environment.NewLine, lines.Select(line => line.Text))
        };
    }
}
