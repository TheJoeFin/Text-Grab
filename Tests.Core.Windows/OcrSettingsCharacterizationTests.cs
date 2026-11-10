using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Windows.Foundation;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// Pins how OcrOutput.CleanOutput and OcrUtilities.BuildTextFromOcrLines respond to each OCR
/// setting, driven through FakeTextGrabSettings. Written against the pre-OcrOptions code and kept
/// unchanged afterwards: the settings-driven default path must not move.
/// </summary>
[Collection(OcrSettingsCollectionDefinition.Name)]
public class OcrSettingsCharacterizationTests
{
    // Greek alpha is mapped to Latin 'a'; "He1lo" is mostly letters, so the digit is fixed to 'l'.
    // "αbc" is shorter than five characters, so only the Latin mapping can touch it.
    private const string RawText = "αbc He1lo";

    [Theory]
    [InlineData(true, true, "abc Hello")]
    [InlineData(true, false, "abc He1lo")]
    [InlineData(false, true, "αbc Hello")]
    [InlineData(false, false, "αbc He1lo")]
    public void CleanOutput_LatinLanguage_FollowsSettings(bool correctToLatin, bool correctErrors, string expected)
    {
        using OcrSettingsScope _ = new(s =>
        {
            s.CorrectToLatin = correctToLatin;
            s.CorrectErrors = correctErrors;
        });

        OcrOutput output = new() { RawOutput = RawText, Language = new GlobalLang("en-US") };
        output.CleanOutput();

        Assert.Equal(expected, output.CleanedOutput);
    }

    [Theory]
    [InlineData(true, "αbc Hello")]
    [InlineData(false, "αbc He1lo")]
    public void CleanOutput_NonLatinLanguage_NeverMapsToLatin(bool correctErrors, string expected)
    {
        using OcrSettingsScope _ = new(s =>
        {
            s.CorrectToLatin = true;
            s.CorrectErrors = correctErrors;
        });

        OcrOutput output = new() { RawOutput = RawText, Language = new GlobalLang("ja") };
        output.CleanOutput();

        Assert.Equal(expected, output.CleanedOutput);
    }

    [Fact]
    public void CleanOutput_Barcode_IsLeftAlone()
    {
        using OcrSettingsScope _ = new(s =>
        {
            s.CorrectToLatin = true;
            s.CorrectErrors = true;
        });

        OcrOutput output = new() { Kind = OcrOutputKind.Barcode, RawOutput = RawText, Language = new GlobalLang("en-US") };
        output.CleanOutput();

        Assert.Equal(string.Empty, output.CleanedOutput);
    }

    // Quirk pinned on purpose: on the space-joining path GetTextFromOcrLine calls
    // StringBuilder.TryFixEveryWordLetterNumberErrors, which returns the fixed string and does not
    // modify the builder, and the return value is discarded. So CorrectErrors has no effect here
    // today; only CorrectToLatin does. F2 must not change this.
    [Theory]
    [InlineData(true, true, "abc He1lo")]
    [InlineData(true, false, "abc He1lo")]
    [InlineData(false, true, "αbc He1lo")]
    [InlineData(false, false, "αbc He1lo")]
    public void BuildTextFromOcrLines_SpaceJoining_CorrectionSettings(bool correctToLatin, bool correctErrors, string expected)
    {
        using OcrSettingsScope _ = new(s =>
        {
            s.CorrectToLatin = correctToLatin;
            s.CorrectErrors = correctErrors;
            s.ParagraphDetection = false;
        });

        string text = OcrUtilities.BuildTextFromOcrLines(new GlobalLang("en-US"), SingleLine(RawText));

        Assert.Equal(expected + Environment.NewLine, text);
    }

    [Theory]
    [InlineData(true, "first line second line")]
    [InlineData(false, "first line\r\nsecond line\r\n")]
    public void BuildTextFromOcrLines_ParagraphDetection_FollowsSetting(bool paragraphDetection, string expected)
    {
        using OcrSettingsScope _ = new(s =>
        {
            s.ParagraphDetection = paragraphDetection;
            s.CorrectErrors = false;
            s.CorrectToLatin = false;
        });

        FakeResult result = new()
        {
            Lines =
            [
                new FakeLine("first line", new Rect(0, 0, 100, 10)),
                new FakeLine("second line", new Rect(0, 12, 100, 10)),
            ]
        };

        string text = OcrUtilities.BuildTextFromOcrLines(new GlobalLang("en-US"), result);

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(true, "黒ごま")]
    [InlineData(false, "くろ 黒ごま")]
    public void BuildTextFromOcrLines_Japanese_FuriganaFollowsSetting(bool removeFurigana, string expected)
    {
        using OcrSettingsScope _ = new(s =>
        {
            s.RemoveFurigana = removeFurigana;
            s.CorrectErrors = false;
            s.CorrectToLatin = false;
        });

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

        string text = OcrUtilities.BuildTextFromOcrLines(new GlobalLang("ja"), new FakeResult { Lines = [line] });

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(true, "Hello")]
    [InlineData(false, "He1lo")]
    public void BuildTextFromOcrLines_WordPath_CorrectErrorsFollowsSetting(bool correctErrors, string expected)
    {
        using OcrSettingsScope _ = new(s =>
        {
            s.CorrectErrors = correctErrors;
            s.RemoveFurigana = false;
            s.CorrectToLatin = false;
        });

        FakeLine line = new("He1lo", new Rect(0, 0, 50, 10))
        {
            Words = [new FakeWord("He1lo", new Rect(0, 0, 50, 10))]
        };

        string text = OcrUtilities.BuildTextFromOcrLines(new GlobalLang("ja"), new FakeResult { Lines = [line] });

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    public void ShouldUseParagraphDetection_CombinesSettingLanguageAndTableMode(
        bool setting, bool spaceJoining, bool tableMode, bool expected)
    {
        using OcrSettingsScope _ = new(s => s.ParagraphDetection = setting);

        Assert.Equal(expected, OcrUtilities.ShouldUseParagraphDetection(spaceJoining, tableMode));
    }

    private static FakeResult SingleLine(string text)
        => new() { Lines = [new FakeLine(text, new Rect(0, 0, 100, 10))] };

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
