using System.Drawing;

namespace Text_Grab.Models;

/// <summary>
/// Defines a named, numbered capture region within a GrabTemplate.
/// Positions are stored as ratios (0.0–1.0) of the reference image dimensions
/// so the template scales to any screen size or DPI.
/// </summary>
public class TemplateRegion
{
    /// <summary>
    /// 1-based number shown on the region border and used in the output template as {RegionNumber}.
    /// </summary>
    public int RegionNumber { get; set; } = 1;

    /// <summary>
    /// Optional friendly label for this region (e.g. "Name", "Email").
    /// Displayed on the border in the designer.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Position and size as ratios of the reference image dimensions (each value 0.0–1.0).
    /// X, Y, Width, Height correspond to left, top, width, height proportions.
    /// </summary>
    public double RatioLeft { get; set; } = 0;
    public double RatioTop { get; set; } = 0;
    public double RatioWidth { get; set; } = 0;
    public double RatioHeight { get; set; } = 0;

    /// <summary>
    /// Optional default/fallback value used when OCR returns empty for this region.
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    public TemplateRegion() { }

    /// <summary>
    /// Returns the absolute pixel rect for this region given the canvas/image dimensions.
    /// </summary>
    public RectangleF ToAbsoluteRect(double imageWidth, double imageHeight)
    {
        return new RectangleF(
            x: (float)(RatioLeft * imageWidth),
            y: (float)(RatioTop * imageHeight),
            width: (float)(RatioWidth * imageWidth),
            height: (float)(RatioHeight * imageHeight));
    }

    /// <summary>
    /// Sets ratio values from an absolute rect and canvas dimensions.
    /// </summary>
    public static TemplateRegion FromAbsoluteRect(RectangleF rect, double imageWidth, double imageHeight, int regionNumber, string label = "")
    {
        return new TemplateRegion
        {
            RegionNumber = regionNumber,
            Label = label,
            RatioLeft = imageWidth > 0 ? rect.X / imageWidth : 0,
            RatioTop = imageHeight > 0 ? rect.Y / imageHeight : 0,
            RatioWidth = imageWidth > 0 ? rect.Width / imageWidth : 0,
            RatioHeight = imageHeight > 0 ? rect.Height / imageHeight : 0,
        };
    }
}
