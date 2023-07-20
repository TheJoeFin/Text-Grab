using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Windows;
using Text_Grab.Utilities;
using Windows.Globalization;

namespace Text_Grab.Models;

public class HistoryInfo : IEquatable<HistoryInfo>
{
    #region Constructors

    public HistoryInfo()
    {

    }

    #endregion Constructors

    #region Properties

    public DateTimeOffset CaptureDateTime { get; set; }

    public string ID { get; set; } = "";

    [JsonIgnore]
    public Bitmap? ImageContent { get; set; }

    public string ImagePath { get; set; } = string.Empty;

    public bool IsTable { get; set; } = false;

    public double DpiScaleFactor { get; set; } = 1.0;

    public string LanguageTag { get; set; } = String.Empty;

    [JsonIgnore]
    public Language OcrLanguage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LanguageTag))
                return LanguageUtilities.GetCurrentInputLanguage();

            return new Language(LanguageTag);
        }
    }

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

    public string RectAsString { get; set; } = string.Empty;

    #endregion Properties

    #region Public Methods

    public static bool operator !=(HistoryInfo? left, HistoryInfo? right)
    {
        return !(left == right);
    }

    public static bool operator ==(HistoryInfo? left, HistoryInfo? right)
    {
        return EqualityComparer<HistoryInfo>.Default.Equals(left, right);
    }

    public bool Equals(HistoryInfo? other)
    {
        if (other is null)
            return false;

        if (other.ID == this.ID)
            return true;

        return false;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as HistoryInfo);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ID);
    }

    #endregion Public Methods
}
