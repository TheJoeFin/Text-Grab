using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.Foundation;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// Explicit <see cref="OcrOptions"/> must win over whatever the settings say, and must not touch
/// the settings. Each test installs settings that are the opposite of the options it passes.
/// </summary>
[Collection(OcrSettingsCollectionDefinition.Name)]
public class OcrOptionsOverrideTests
{
    private const string RawText = "αbc He1lo";

    private static OcrOptions Options(
        bool correctErrors = false,
        bool correctToLatin = false,
        bool removeFurigana = false,
        bool paragraphDetection = false)
        => new(correctErrors, correctToLatin, removeFurigana, paragraphDetection, false, "en-US");

    [Fact]
    public void CleanOutput_OptionsOff_BeatSettingsOn()
    {
        using OcrSettingsScope _ = new(s => { s.CorrectToLatin = true; s.CorrectErrors = true; });

        OcrOutput output = new() { RawOutput = RawText, Language = new GlobalLang("en-US") };
        output.CleanOutput(Options());

        Assert.Equal(RawText, output.CleanedOutput);
    }

    [Fact]
    public void CleanOutput_OptionsOn_BeatSettingsOff()
    {
        using OcrSettingsScope _ = new(s => { s.CorrectToLatin = false; s.CorrectErrors = false; });

        OcrOutput output = new() { RawOutput = RawText, Language = new GlobalLang("en-US") };
        output.CleanOutput(Options(correctErrors: true, correctToLatin: true));

        Assert.Equal("abc Hello", output.CleanedOutput);
    }

    [Fact]
    public void CleanOutput_NullOptions_UsesSettings()
    {
        using OcrSettingsScope _ = new(s => { s.CorrectToLatin = true; s.CorrectErrors = true; });

        OcrOutput output = new() { RawOutput = RawText, Language = new GlobalLang("en-US") };
        output.CleanOutput(null);

        Assert.Equal("abc Hello", output.CleanedOutput);
    }

    [Fact]
    public void CleanOutput_DoesNotChangeSettings()
    {
        using OcrSettingsScope _ = new(s => { s.CorrectErrors = true; s.CorrectToLatin = true; });

        OcrOutput output = new() { RawOutput = RawText, Language = new GlobalLang("en-US") };
        output.CleanOutput(Options());

        Assert.True(SettingsAccess.Current.CorrectErrors);
        Assert.True(SettingsAccess.Current.CorrectToLatin);
    }

    [Fact]
    public void GetStringFromOcrOutputs_UsesSuppliedOptionsForEveryOutput()
    {
        using OcrSettingsScope _ = new(s => { s.CorrectToLatin = true; s.CorrectErrors = true; });

        List<OcrOutput> outputs =
        [
            new() { RawOutput = RawText, Language = new GlobalLang("en-US") },
            new() { RawOutput = "|" + RawText, Language = new GlobalLang("en-US") },
        ];

        Assert.Equal(RawText + "|" + RawText, OcrUtilities.GetStringFromOcrOutputs(outputs, Options()));
    }

    [Fact]
    public void BuildTextFromOcrLines_ParagraphDetectionOverride_WinsBothWays()
    {
        FakeResult result = new()
        {
            Lines =
            [
                new FakeLine("first line", new Rect(0, 0, 100, 10)),
                new FakeLine("second line", new Rect(0, 12, 100, 10)),
            ]
        };

        using (new OcrSettingsScope(s => s.ParagraphDetection = true))
        {
            Assert.Equal(
                "first line\r\nsecond line\r\n",
                OcrUtilities.BuildTextFromOcrLines(new GlobalLang("en-US"), result, Options(paragraphDetection: false)));
        }

        using (new OcrSettingsScope(s => s.ParagraphDetection = false))
        {
            Assert.Equal(
                "first line second line",
                OcrUtilities.BuildTextFromOcrLines(new GlobalLang("en-US"), result, Options(paragraphDetection: true)));
        }
    }

    [Fact]
    public void BuildTextFromOcrLines_CorrectToLatinOverride_WinsBothWays()
    {
        FakeResult result = new() { Lines = [new FakeLine(RawText, new Rect(0, 0, 100, 10))] };

        using (new OcrSettingsScope(s => { s.CorrectToLatin = true; s.ParagraphDetection = false; }))
        {
            Assert.Equal(
                RawText + Environment.NewLine,
                OcrUtilities.BuildTextFromOcrLines(new GlobalLang("en-US"), result, Options()));
        }

        using (new OcrSettingsScope(s => { s.CorrectToLatin = false; s.ParagraphDetection = false; }))
        {
            Assert.Equal(
                "abc He1lo" + Environment.NewLine,
                OcrUtilities.BuildTextFromOcrLines(new GlobalLang("en-US"), result, Options(correctToLatin: true)));
        }
    }

    [Fact]
    public void BuildTextFromOcrLines_WordPathCorrectErrorsOverride_WinsBothWays()
    {
        FakeLine line = new("He1lo", new Rect(0, 0, 50, 10))
        {
            Words = [new FakeWord("He1lo", new Rect(0, 0, 50, 10))]
        };
        FakeResult result = new() { Lines = [line] };

        using (new OcrSettingsScope(s => s.CorrectErrors = true))
            Assert.Equal("He1lo", OcrUtilities.BuildTextFromOcrLines(new GlobalLang("ja"), result, Options()));

        using (new OcrSettingsScope(s => s.CorrectErrors = false))
            Assert.Equal("Hello", OcrUtilities.BuildTextFromOcrLines(new GlobalLang("ja"), result, Options(correctErrors: true)));
    }

    [Fact]
    public void BuildTextFromOcrLines_RemoveFuriganaOverride_WinsBothWays()
    {
        FakeLine line = new("くろ黒ごま", new Rect(0, 0, 60, 30))
        {
            Words =
            [
                new FakeWord("くろ", new Rect(0, 0, 16, 8)),
                new FakeWord("黒", new Rect(0, 10, 20, 20)),
                new FakeWord("ご", new Rect(20, 10, 20, 20)),
                new FakeWord("ま", new Rect(40, 10, 20, 20)),
            ]
        };
        FakeResult result = new() { Lines = [line] };

        using (new OcrSettingsScope(s => s.RemoveFurigana = true))
            Assert.Equal("くろ 黒ごま", OcrUtilities.BuildTextFromOcrLines(new GlobalLang("ja"), result, Options()));

        using (new OcrSettingsScope(s => s.RemoveFurigana = false))
            Assert.Equal("黒ごま", OcrUtilities.BuildTextFromOcrLines(new GlobalLang("ja"), result, Options(removeFurigana: true)));
    }

    [Fact]
    public void ShouldUseParagraphDetection_OptionsWinOverSettings()
    {
        using (new OcrSettingsScope(s => s.ParagraphDetection = true))
            Assert.False(OcrUtilities.ShouldUseParagraphDetection(true, options: Options(paragraphDetection: false)));

        using (new OcrSettingsScope(s => s.ParagraphDetection = false))
            Assert.True(OcrUtilities.ShouldUseParagraphDetection(true, options: Options(paragraphDetection: true)));
    }

    [Fact]
    public void ParseOcrResultIntoWordBorderInfos_UsesOptions()
    {
        FakeLine line = new(RawText, new Rect(0, 0, 100, 10))
        {
            Words = [new FakeWord(RawText, new Rect(0, 0, 100, 10))]
        };
        FakeResult result = new() { Lines = [line] };

        using OcrSettingsScope _ = new(s => s.CorrectToLatin = true);

        List<WordBorderInfo> infos = OcrUtilities.ParseOcrResultIntoWordBorderInfos(result, true, Options(correctToLatin: false));

        Assert.Equal(RawText, Assert.Single(infos).Word);
    }

    private sealed class FakeResult : IOcrLinesWords
    {
        public string Text { get; set; } = string.Empty;
        public IOcrLine[] Lines { get; set; } = [];
        public float Angle { get; set; }
    }

    private sealed class FakeLine(string text, Rect boundingBox) : IOcrLine
    {
        public string Text { get; set; } = text;
        public IOcrWord[] Words { get; set; } = [];
        public Rect BoundingBox { get; set; } = boundingBox;
    }

    private sealed class FakeWord(string text, Rect boundingBox) : IOcrWord
    {
        public string Text { get; set; } = text;
        public Rect BoundingBox { get; set; } = boundingBox;
    }
}
