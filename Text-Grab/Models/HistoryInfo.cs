using System;
using System.Drawing;

namespace Text_Grab.Models;

public class HistoryInfo
{
    public TextGrabMode SourceMode { get; set; }

    public DateTime CaptureTime { get; set; }

    public string TextContent { get; set; }

    public string ImagePath { get; set; }

    public Bitmap? ImageContent { get; set; }
}
