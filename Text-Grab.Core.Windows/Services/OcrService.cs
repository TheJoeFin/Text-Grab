using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Core.Abstractions;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Services;

/// <summary>
/// <see cref="IOcrService"/> over <see cref="OcrEngineUtilities"/>, the headless OCR dispatch
/// (v5 F3). Stateless: one instance can serve concurrent calls.
/// </summary>
public sealed class OcrService : IOcrService
{
    /// <inheritdoc/>
    public async Task<string> OcrFileAsync(string path, OcrOptions? options = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        if (IoUtilities.IsPdfFileExtension(Path.GetExtension(fullPath)))
            throw new NotSupportedException("IOcrService does not read PDFs yet: PDF pages are rendered by app-side code.");

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Cannot OCR a file that does not exist.", fullPath);

        ct.ThrowIfCancellationRequested();
        options ??= OcrOptions.FromSettings(SettingsAccess.Current);
        ILanguage language = await ResolveLanguageAsync(options.LanguageTag);

        ct.ThrowIfCancellationRequested();
        return await OcrEngineUtilities.OcrImageFileAsync(fullPath, language, options);
    }

    /// <inheritdoc/>
    public async Task<string> OcrImageAsync(Stream image, OcrOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        // GDI+ needs a seekable stream that outlives the decoded image; the caller's may be neither.
        using MemoryStream contents = new();
        await image.CopyToAsync(contents, ct);
        contents.Position = 0;

        options ??= OcrOptions.FromSettings(SettingsAccess.Current);
        ILanguage language = await ResolveLanguageAsync(options.LanguageTag);

        // Same decode as the pasted-image path (BitmapUtilities.GetBitmapFromIRandomAccessStream):
        // the copy detaches the bitmap from the stream and makes it 32bpp ARGB.
        using Bitmap decoded = new(contents);
        using Bitmap bitmap = new(decoded);

        ct.ThrowIfCancellationRequested();
        List<OcrOutput> outputs = await OcrEngineUtilities.GetTextFromImageAsync(bitmap, language, options);
        return OcrUtilities.GetStringFromOcrOutputs(outputs, options);
    }

    /// <inheritdoc/>
    public async Task<string> OcrScreenRegionAsync(RectangleF screenRegion, OcrOptions? options = null, CancellationToken ct = default)
    {
        // Truncation matches ShapeExtensions.AsRectangle, which the app's grabs convert through.
        Rectangle region = Rectangle.Truncate(screenRegion);
        if (region.Width <= 0 || region.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(screenRegion), screenRegion, "The region must be at least one pixel wide and tall.");

        ct.ThrowIfCancellationRequested();
        options ??= OcrOptions.FromSettings(SettingsAccess.Current);
        ILanguage language = await ResolveLanguageAsync(options.LanguageTag);

        ct.ThrowIfCancellationRequested();

        // ImageMethods.GetRegionOfScreenAsBitmap without its history cache: capture, then pad
        // small captures so the engine has room to find text.
        Bitmap captured = BitmapUtilities.CaptureScreenRegion(region);
        Bitmap padded = BitmapUtilities.PadImage(captured);
        try
        {
            List<OcrOutput> outputs = await OcrEngineUtilities.GetTextFromImageAsync(padded, language, options);
            return OcrUtilities.GetStringFromOcrOutputs(outputs, options);
        }
        finally
        {
            if (!ReferenceEquals(padded, captured))
                captured.Dispose();
            padded.Dispose();
        }
    }

    /// <summary>
    /// The language for <paramref name="languageTag"/>. An empty tag, or the persisted
    /// <see cref="ITextGrabSettings.LastUsedLang"/> itself, resolves exactly as the app resolves the
    /// user's OCR language, availability fallbacks included. Any other tag is matched against every
    /// language Text Grab offers, Tesseract included, and otherwise taken as a Windows OCR language
    /// tag.
    /// </summary>
    internal static async Task<ILanguage> ResolveLanguageAsync(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag)
            || string.Equals(languageTag, SettingsAccess.Current.LastUsedLang, StringComparison.OrdinalIgnoreCase))
        {
            return LanguageUtilities.GetOCRLanguage();
        }

        List<ILanguage> languages = await CaptureLanguageUtilities.GetCaptureLanguagesAsync(includeTesseract: true);
        return languages.FirstOrDefault(language => CaptureLanguageUtilities.MatchesPersistedLanguage(language, languageTag))
            ?? new GlobalLang(languageTag);
    }
}
