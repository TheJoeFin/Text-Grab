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

    public Rect PositionRect { get; set; } = Rect.Empty;

    public bool IsTable { get; set; } = false;
}
