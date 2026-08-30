using System.Runtime.CompilerServices;
using Text_Grab.Interfaces;
using Text_Grab.Services;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// Registers a <see cref="SettingsAccess"/> resolver for this test host. Tests.Core.Windows has
/// no app assembly to supply one via a <c>[ModuleInitializer]</c> the way Tests does (see
/// SettingsAccess.Current's remarks), so any moved test whose production code path reads settings
/// - here, a handful of OcrTests methods that call into OcrUtilities.BuildTextFromOcrLines, which
/// reads ParagraphDetection/RemoveFurigana/CorrectErrors/CorrectToLatin internally - would throw
/// InvalidOperationException without one installed.
///
/// Every default below is copied from Text-Grab/Properties/Settings.settings's "(Default)"
/// profile, so a moved test that depends on a default value (e.g. RemoveFurigana=true dropping
/// furigana words) sees exactly what it saw running inside the app-hosted Tests project.
/// </summary>
internal static class TestSettingsInitializer
{
    [ModuleInitializer]
    internal static void Register() => SettingsAccess.SetResolver(() => new FakeTextGrabSettings());
}

/// <summary>Minimal ITextGrabSettings double seeded with Settings.settings's shipped defaults.</summary>
internal sealed class FakeTextGrabSettings : ITextGrabSettings
{
    public bool CorrectErrors { get; set; } = true;
    public bool CorrectToLatin { get; set; } = true;
    public bool OverrideAiArchCheck { get; set; }
    public bool ParagraphDetection { get; set; } = true;
    public bool RemoveFurigana { get; set; } = true;
    public bool TryToReadBarcodes { get; set; } = true;
    public bool HdrCaptureCorrection { get; set; }
    public bool HdrBorderlessGranted { get; set; }
    public bool UiAutomationEnabled { get; set; }
    public bool WindowsAiDescriptionEnabled { get; set; }
    public bool UiAutomationFallbackToOcr { get; set; } = true;
    public bool UseTesseract { get; set; }
    public string TesseractPath { get; set; } = string.Empty;
    public string LastUsedLang { get; set; } = string.Empty;
    public int TtsSpeakWordLimit { get; set; } = 100;
    public string TtsVoiceName { get; set; } = string.Empty;
    public double TtsSpeakingRate { get; set; } = 1;
    public string AudioTranscriptionModel { get; set; } = "BaseMultilingual";
    public bool EnableFileBackedManagedSettings { get; set; }

    public void Save() { }
}
