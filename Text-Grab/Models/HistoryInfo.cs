using System;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Text_Grab.Models;

public class HistoryInfo
{
    public TextGrabMode SourceMode { get; set; }

    public DateTime CaptureTime { get; set; }

    public string TextContent { get; set; }

    public string TextInFrame { get; set; }

    public string ImagePath { get; set; }

    [JsonIgnore]
    public Bitmap? ImageContent { get; set; }
}
