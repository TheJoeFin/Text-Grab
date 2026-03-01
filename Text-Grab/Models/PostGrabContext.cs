using System.Windows;
using System.Windows.Media.Imaging;
using Text_Grab.Interfaces;

namespace Text_Grab.Models;

/// <summary>
/// Carries all context data produced by a grab action and passed through
/// the post-grab action pipeline. This allows actions that need only the
/// OCR text to ignore the extra fields, while template actions can use
/// the capture region and DPI to re-run sub-region OCR.
/// </summary>
public record PostGrabContext(
    /// <summary>The OCR text extracted from the full capture region.</summary>
    string Text,

    /// <summary>
    /// The screen rectangle (in physical pixels) that was captured.
    /// Used by template execution to derive sub-region rectangles.
    /// </summary>
    Rect CaptureRegion,

    /// <summary>The DPI scale factor at capture time.</summary>
    double DpiScale,

    /// <summary>Optional in-memory copy of the captured image.</summary>
    BitmapSource? CapturedImage,

    /// <summary>The OCR language used for the capture. Null means use the app default.</summary>
    ILanguage? Language = null
)
{
    /// <summary>Convenience factory for non-template actions that only need text.</summary>
    public static PostGrabContext TextOnly(string text) =>
        new(text, Rect.Empty, 1.0, null, null);
}
