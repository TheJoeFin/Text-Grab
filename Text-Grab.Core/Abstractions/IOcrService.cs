using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Models;

namespace Text_Grab.Core.Abstractions;

/// <summary>
/// Headless OCR: give it a file, an image stream or a screen rectangle and get text back. No
/// windows, no WPF, no history entry and no clipboard write - callers decide what to do with the
/// text.
///
/// Every method takes optional <see cref="OcrOptions"/>. <c>null</c> means the user's saved
/// settings, including their selected OCR language. <see cref="OcrOptions.LanguageTag"/> picks the
/// engine and language: a Windows OCR language tag such as <c>en-US</c>, or the tag of any other
/// language Text Grab offers (Windows AI, Tesseract). An empty tag means the user's selected OCR
/// language.
///
/// Cancellation is checked between stages. A recognition already handed to an engine runs to
/// completion.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// OCRs an image file, applying its EXIF rotation first.
    /// </summary>
    /// <exception cref="System.NotSupportedException">
    /// <paramref name="path"/> is a PDF. PDF pages are rendered by app-side code that is not yet
    /// headless.
    /// </exception>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    Task<string> OcrFileAsync(string path, OcrOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// OCRs an encoded image (PNG, JPEG, BMP, ...). The stream is read from its current position to
    /// the end and is not disposed. EXIF orientation is not applied, matching how Text Grab OCRs
    /// pasted and dropped images.
    /// </summary>
    Task<string> OcrImageAsync(Stream image, OcrOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Captures and OCRs a rectangle of the virtual desktop, in physical pixels. Fractional
    /// coordinates are truncated, as the app's own capture does. Unlike an interactive grab, the
    /// capture is not cached for history. The UI Automation language falls back to OCR when its
    /// fallback setting is on and returns empty text otherwise; reading UI Automation text needs
    /// the app.
    /// </summary>
    Task<string> OcrScreenRegionAsync(RectangleF screenRegion, OcrOptions? options = null, CancellationToken ct = default);
}
