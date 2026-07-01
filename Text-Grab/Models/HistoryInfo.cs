using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Windows;
using Text_Grab.Interfaces;
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

    public FsgSelectionStyle SelectionStyle { get; set; } = FsgSelectionStyle.Region;

    public string LanguageTag { get; set; } = string.Empty;

    public LanguageKind LanguageKind { get; set; } = LanguageKind.Global;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool UsedUiAutomation { get; set; }

    public bool HasCalcPaneOpen { get; set; } = false;

    public int CalcPaneWidth { get; set; } = 0;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? ManualTableColumnSeparators { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? ManualTableRowSeparators { get; set; }

    public EtwEditorMode EditorMode { get; set; } = EtwEditorMode.Text;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EditTextTableDocumentJson { get; set; }

    [JsonIgnore]
    public ILanguage OcrLanguage
    {
        get
        {
            (string normalizedLanguageTag, LanguageKind normalizedLanguageKind, _) =
                LanguageUtilities.NormalizePersistedLanguageIdentity(LanguageKind, LanguageTag, UsedUiAutomation);

            if (string.IsNullOrWhiteSpace(normalizedLanguageTag))
                return new GlobalLang(LanguageUtilities.GetCurrentInputLanguage().AsLanguage() ?? new Language("en-US"));

            return normalizedLanguageKind switch
            {
                LanguageKind.Global => new GlobalLang(new Language(normalizedLanguageTag)),
                LanguageKind.Tesseract => new TessLang(normalizedLanguageTag),
                LanguageKind.WindowsAi => new WindowsAiLang(),
                LanguageKind.UiAutomation => CaptureLanguageUtilities.GetUiAutomationFallbackLanguage(),
                _ => new GlobalLang(LanguageUtilities.GetCurrentInputLanguage().AsLanguage() ?? new Language("en-US")),
            };
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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WordBorderInfoJson { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WordBorderInfoFileName { get; set; }

    public string RectAsString { get; set; } = string.Empty;

    #endregion Properties

    #region Public Methods

    public void ClearTransientImage()
    {
        // Do not Dispose() here — the bitmap may still be in use by a
        // fire-and-forget SaveImageFile task (the packaged path is async).
        // Nulling the reference lets the GC collect once all consumers finish.
        // The HistoryService.DisposeCachedBitmap() path handles deterministic
        // cleanup of the captured fullscreen bitmap via its GDI handle.
        ImageContent = null;
    }

    public void ClearTransientWordBorderData()
    {
        WordBorderInfoJson = null;
    }

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
