using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.Json.Serialization;
using Text_Grab.Interfaces;
using Text_Grab.Utilities;
using Windows.Globalization;

namespace Text_Grab.Models;

public class HistoryInfo : IEquatable<HistoryInfo>
{
    private static readonly NumberFormatInfo CommaDecimalFormat = new() { NumberDecimalSeparator = "," };

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

    public OpenContentKind SourceContentKind { get; set; } = OpenContentKind.Image;

    public string SourcePath { get; set; } = string.Empty;

    public int SourcePageIndex { get; set; }

    [JsonIgnore]
    public bool IsPdfDocument => SourceContentKind == OpenContentKind.PdfDocument;

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
                LanguageKind.WindowsAiDescription => new WindowsAiDescriptionLang(),
                LanguageKind.UiAutomation => CaptureLanguageUtilities.GetUiAutomationFallbackLanguage(),
                _ => new GlobalLang(LanguageUtilities.GetCurrentInputLanguage().AsLanguage() ?? new Language("en-US")),
            };
        }
    }

    /// <summary>
    /// A projection over the persisted <see cref="RectAsString"/>, not a stored field.
    /// </summary>
    /// <remarks>
    /// The on-disk format is the one <c>System.Windows.Rect</c> wrote before B2 of the Core split
    /// moved this model off WPF geometry: <c>"x,y,width,height"</c>, or the literal <c>"Empty"</c>.
    /// It is written with the invariant culture so a history file stays readable on any machine,
    /// and read back tolerating the <c>';'</c> separator and comma decimals that
    /// <c>Rect.ToString()</c> produced under cultures whose decimal separator is <c>','</c> -
    /// strings the old invariant-only <c>Rect.Parse</c> threw on.
    /// </remarks>
    [JsonIgnore]
    public RectangleF PositionRect
    {
        get => ParsePositionRect(RectAsString);
        set => RectAsString = FormatPositionRect(value);
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

    /// <summary>
    /// Returns a shallow copy of this instance. Reference-typed members (e.g.
    /// <see cref="ImageContent"/>, the separator lists) are shared, not cloned — callers
    /// that only need to tweak value/string fields without mutating the original should use this.
    /// </summary>
    public HistoryInfo ShallowCopy() => (HistoryInfo)MemberwiseClone();

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

    private static RectangleF ParsePositionRect(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return RectangleF.Empty;

        string trimmed = source.Trim();

        if (trimmed.Equals("Empty", StringComparison.OrdinalIgnoreCase))
            return RectangleF.Empty;

        // A ';' separator means the writing culture used ',' as its decimal separator.
        bool commaDecimals = trimmed.Contains(';');
        string[] parts = trimmed.Split(commaDecimals ? ';' : ',');

        if (parts.Length != 4)
            return RectangleF.Empty;

        IFormatProvider format = commaDecimals ? CommaDecimalFormat : CultureInfo.InvariantCulture;
        float[] values = new float[4];

        for (int i = 0; i < 4; i++)
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, format, out values[i]))
                return RectangleF.Empty;

        return new RectangleF(values[0], values[1], values[2], values[3]);
    }

    private static string FormatPositionRect(RectangleF rect)
    {
        if (rect == RectangleF.Empty)
            return string.Empty;

        return string.Create(CultureInfo.InvariantCulture, $"{rect.X},{rect.Y},{rect.Width},{rect.Height}");
    }

    #endregion Public Methods
}
