using System;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Windows;

namespace Text_Grab.Models;

public class HistoryInfo
{
    public TextGrabMode SourceMode { get; set; }

    public DateTimeOffset CaptureDateTime { get; set; }

    public string TextContent { get; set; } = string.Empty;

    public string WordBorderInfoJson { get; set; } = string.Empty;

    public string ImagePath { get; set; } = string.Empty;

    [JsonIgnore]
    public Bitmap? ImageContent { get; set; }

    [JsonIgnore]
    public Rect PositionRect
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RectAsString))
                return Rect.Empty;

            return Rect.Parse(RectAsString);
        }

        set
        {
            RectAsString = value.ToString();
        }
    }

    private string RectAsString { get; set; } = string.Empty;

    public bool IsTable { get; set; } = false;
}
