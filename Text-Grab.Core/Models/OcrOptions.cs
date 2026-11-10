using Text_Grab.Interfaces;

namespace Text_Grab.Models;

/// <summary>
/// Per-call OCR options. Lets a caller OCR with options that differ from the user's saved
/// settings without mutating those settings. Passing <c>null</c> to a method that accepts
/// <see cref="OcrOptions"/> means "use <see cref="FromSettings"/> on the current settings".
/// </summary>
public sealed record OcrOptions(
    bool CorrectErrors,
    bool CorrectToLatin,
    bool RemoveFurigana,
    bool ParagraphDetection,
    bool TryToReadBarcodes,
    string LanguageTag)
{
    /// <summary>
    /// Snapshots the OCR-related settings. <paramref name="languageTag"/> overrides the
    /// persisted <see cref="ITextGrabSettings.LastUsedLang"/> when supplied.
    /// </summary>
    public static OcrOptions FromSettings(ITextGrabSettings settings, string? languageTag = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new OcrOptions(
            settings.CorrectErrors,
            settings.CorrectToLatin,
            settings.RemoveFurigana,
            settings.ParagraphDetection,
            settings.TryToReadBarcodes,
            languageTag ?? settings.LastUsedLang ?? string.Empty);
    }
}
