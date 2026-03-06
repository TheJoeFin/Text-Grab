using System.Windows;
using System.Windows.Media.Imaging;

namespace Text_Grab.Models;

public record FullscreenCaptureResult(
    FsgSelectionStyle SelectionStyle,
    Rect CaptureRegion,
    BitmapSource? CapturedImage = null,
    string? WindowTitle = null)
{
    public bool SupportsTemplateActions => SelectionStyle != FsgSelectionStyle.Freeform;

    public bool SupportsPreviousRegionReplay =>
        SelectionStyle is FsgSelectionStyle.Region or FsgSelectionStyle.AdjustAfter;

    public bool UsesCapturedImage => CapturedImage is not null;
}
