namespace Text_Grab.Interfaces;

/// <summary>
/// The slice of Text-Grab's user settings that portable (Core / Core.Windows) code is allowed
/// to read.
///
/// The app's real settings object is <c>Text_Grab.Properties.Settings</c> - an internal, sealed,
/// generated <c>ApplicationSettingsBase</c> with 104 properties, reachable only through
/// <c>AppUtilities.TextGrabSettings</c>. That accessor is the single largest thing tying logic to
/// the app assembly. This interface breaks that tie without moving the settings machinery: the
/// generated properties already match these names and types, so the hand-written partial in
/// Text-Grab/Properties/Settings.cs satisfies the whole interface just by declaring it.
///
/// Keep this deliberately small. Add a property only when a file being moved actually reads it.
/// If a single move would need more than a handful of new members, that file probably wants the
/// facade split instead - move the pure logic to Core and leave a thin settings-reading wrapper
/// in the app, the way PatternItem / PatternItemCatalog was handled in e677b54.
///
/// Resolved through <see cref="Text_Grab.Services.SettingsAccess"/>.
/// </summary>
public interface ITextGrabSettings
{
    /// <summary>Apply the OCR error-correction pass to recognized text.</summary>
    bool CorrectErrors { get; set; }

    /// <summary>Map look-alike Greek and Cyrillic characters to Latin.</summary>
    bool CorrectToLatin { get; set; }

    /// <summary>Bypass the arm64 gate on the Windows AI feature checks.</summary>
    bool OverrideAiArchCheck { get; set; }

    /// <summary>Join OCR lines into paragraphs instead of preserving line breaks.</summary>
    bool ParagraphDetection { get; set; }

    /// <summary>Strip furigana ruby text from recognized Japanese.</summary>
    bool RemoveFurigana { get; set; }

    /// <summary>Scan captured images for barcodes and QR codes.</summary>
    bool TryToReadBarcodes { get; set; }

    /// <summary>Use the HDR-aware capture path for screen regions.</summary>
    bool HdrCaptureCorrection { get; set; }

    /// <summary>Whether the user has already granted borderless screen-capture access.</summary>
    bool HdrBorderlessGranted { get; set; }

    /// <summary>Offer UI Automation as a text source alongside the OCR engines.</summary>
    bool UiAutomationEnabled { get; set; }

    /// <summary>Offer the Windows AI image-description pseudo-language.</summary>
    bool WindowsAiDescriptionEnabled { get; set; }

    /// <summary>Fall back to OCR when UI Automation returns no text.</summary>
    bool UiAutomationFallbackToOcr { get; set; }

    /// <summary>Route OCR through Tesseract instead of the Windows engines.</summary>
    bool UseTesseract { get; set; }

    /// <summary>Cached path to the Tesseract executable; written back once discovered.</summary>
    string TesseractPath { get; set; }

    /// <summary>BCP-47 tag of the language used for the last capture.</summary>
    string LastUsedLang { get; set; }

    /// <summary>Trim spoken text to this many words; zero or negative disables the limit.</summary>
    int TtsSpeakWordLimit { get; set; }

    /// <summary>Display name of the preferred text-to-speech voice; empty selects the default.</summary>
    string TtsVoiceName { get; set; }

    /// <summary>Speaking rate passed to the TTS engine; only values in [0.5, 6.0] are applied.</summary>
    double TtsSpeakingRate { get; set; }

    /// <summary>Persist pending changes. Backed by ApplicationSettingsBase.Save().</summary>
    void Save();
}
