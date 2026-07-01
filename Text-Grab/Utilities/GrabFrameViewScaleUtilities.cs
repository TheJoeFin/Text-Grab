using System;
using System.Windows;
using Text_Grab;

namespace Text_Grab.Utilities;

public static class GrabFrameViewScaleUtilities
{
    public const double MaximumLoadedDocumentScale = 5.0;
    public const double MinimumLoadedDocumentScale = 0.5;
    public const double MinimumLoadedDocumentWindowHeight = 450;
    public const double MinimumLoadedDocumentWindowWidth = 800;
    public const double ScaleStep = 0.25;

    public static double CoerceScale(double scale)
    {
        if (!double.IsFinite(scale))
            return 1.0;

        return Math.Clamp(scale, MinimumLoadedDocumentScale, MaximumLoadedDocumentScale);
    }

    public static Rect GetMinimumWindowRect(Rect currentWindowRect, Size minimumWindowSize, Rect workArea)
    {
        if (!currentWindowRect.IsGood())
            return currentWindowRect;

        double targetWidth = Math.Max(currentWindowRect.Width, minimumWindowSize.Width);
        double targetHeight = Math.Max(currentWindowRect.Height, minimumWindowSize.Height);

        double centerX = currentWindowRect.Left + (currentWindowRect.Width / 2.0);
        double centerY = currentWindowRect.Top + (currentWindowRect.Height / 2.0);

        Rect desiredRect = new(
            centerX - (targetWidth / 2.0),
            centerY - (targetHeight / 2.0),
            targetWidth,
            targetHeight);

        if (!workArea.IsGood())
            return desiredRect;

        double width = Math.Min(desiredRect.Width, workArea.Width);
        double height = Math.Min(desiredRect.Height, workArea.Height);
        double left = Math.Clamp(desiredRect.Left, workArea.Left, workArea.Right - width);
        double top = Math.Clamp(desiredRect.Top, workArea.Top, workArea.Bottom - height);

        return new Rect(left, top, width, height);
    }

    public static double StepScale(double currentScale, int direction)
    {
        double coercedScale = CoerceScale(currentScale);
        int normalizedDirection = direction switch
        {
            < 0 => -1,
            > 0 => 1,
            _ => 0
        };

        if (normalizedDirection == 0)
            return coercedScale;

        return CoerceScale(coercedScale + (normalizedDirection * ScaleStep));
    }
}
