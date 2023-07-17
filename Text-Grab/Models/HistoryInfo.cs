using System;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Windows;

namespace Text_Grab.Models;

public class HistoryInfo : IEquatable<HistoryInfo>
{
    public HistoryInfo()
    {

    }

    public DateTimeOffset CaptureDateTime { get; set; }

    public string ID { get; set; } = "";

    [JsonIgnore]
    public Bitmap? ImageContent { get; set; }

    public string ImagePath { get; set; } = string.Empty;

    public bool IsTable { get; set; } = false;

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

    public TextGrabMode SourceMode { get; set; }

    public string TextContent { get; set; } = string.Empty;

    public string WordBorderInfoJson { get; set; } = string.Empty;

    private string RectAsString { get; set; } = string.Empty;

    public bool Equals(HistoryInfo? other)
    {
        if (other is null)
            return false;

        if (other.ID == this.ID)
            return true;

        return false;
    }
}
