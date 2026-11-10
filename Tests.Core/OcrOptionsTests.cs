using Text_Grab.Interfaces;
using Text_Grab.Models;

namespace Text_Grab.Tests.Core;

public class OcrOptionsTests
{
    [Fact]
    public void FromSettings_CopiesEveryOcrSetting()
    {
        StubSettings settings = new()
        {
            CorrectErrors = true,
            CorrectToLatin = false,
            RemoveFurigana = true,
            ParagraphDetection = false,
            TryToReadBarcodes = true,
            LastUsedLang = "ja",
        };

        OcrOptions options = OcrOptions.FromSettings(settings);

        Assert.True(options.CorrectErrors);
        Assert.False(options.CorrectToLatin);
        Assert.True(options.RemoveFurigana);
        Assert.False(options.ParagraphDetection);
        Assert.True(options.TryToReadBarcodes);
        Assert.Equal("ja", options.LanguageTag);
    }

    [Fact]
    public void FromSettings_InvertedFlags_AreInverted()
    {
        StubSettings settings = new()
        {
            CorrectErrors = false,
            CorrectToLatin = true,
            RemoveFurigana = false,
            ParagraphDetection = true,
            TryToReadBarcodes = false,
        };

        OcrOptions options = OcrOptions.FromSettings(settings);

        Assert.False(options.CorrectErrors);
        Assert.True(options.CorrectToLatin);
        Assert.False(options.RemoveFurigana);
        Assert.True(options.ParagraphDetection);
        Assert.False(options.TryToReadBarcodes);
    }

    [Fact]
    public void FromSettings_ExplicitLanguageTag_OverridesLastUsedLang()
    {
        StubSettings settings = new() { LastUsedLang = "ja" };

        Assert.Equal("fr-FR", OcrOptions.FromSettings(settings, "fr-FR").LanguageTag);
    }

    [Fact]
    public void FromSettings_DoesNotMutateSettings()
    {
        StubSettings settings = new() { CorrectErrors = true };

        _ = OcrOptions.FromSettings(settings, "de");

        Assert.True(settings.CorrectErrors);
        Assert.Equal(string.Empty, settings.LastUsedLang);
        Assert.Equal(0, settings.SaveCount);
    }

    [Fact]
    public void FromSettings_NullSettings_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => OcrOptions.FromSettings(null!));
    }

    private sealed class StubSettings : ITextGrabSettings
    {
        public bool CorrectErrors { get; set; }
        public bool CorrectToLatin { get; set; }
        public bool OverrideAiArchCheck { get; set; }
        public bool ParagraphDetection { get; set; }
        public bool RemoveFurigana { get; set; }
        public bool TryToReadBarcodes { get; set; }
        public bool HdrCaptureCorrection { get; set; }
        public bool HdrBorderlessGranted { get; set; }
        public bool UiAutomationEnabled { get; set; }
        public bool WindowsAiDescriptionEnabled { get; set; }
        public bool UiAutomationFallbackToOcr { get; set; }
        public bool UseTesseract { get; set; }
        public string TesseractPath { get; set; } = string.Empty;
        public string LastUsedLang { get; set; } = string.Empty;
        public int TtsSpeakWordLimit { get; set; }
        public string TtsVoiceName { get; set; } = string.Empty;
        public double TtsSpeakingRate { get; set; }
        public string AudioTranscriptionModel { get; set; } = string.Empty;
        public string LiveTranscriptionModel { get; set; } = string.Empty;
        public bool EnableFileBackedManagedSettings { get; set; }
        public int SaveCount { get; private set; }

        public void Save() => SaveCount++;
    }
}
