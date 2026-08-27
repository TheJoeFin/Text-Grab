using Text_Grab.Utilities;

namespace Text_Grab.Models;

/// <summary>
/// Represents a reference to a built-in recognizer within a GrabTemplate.
/// During execution the recognizer is run against the source text and matches are
/// extracted according to <see cref="MatchMode"/>, emitting either the resolved value
/// or the matched text per <see cref="OutputKind"/>.
///
/// Placeholder syntax in the output template:
///   {r:RecognizerName:first}            — first match, resolved value
///   {r:RecognizerName:last}             — last match
///   {r:RecognizerName:all}              — all matches, default separator
///   {r:RecognizerName:all:text}         — all matches, matched text instead of resolved value
///   {r:RecognizerName:all:value:; }     — all matches, resolved value joined by "; "
///   {r:RecognizerName:2}                — 2nd match (1-based)
///   {r:RecognizerName:1,3}              — 1st and 3rd matches joined by separator
/// </summary>
public class TemplateRecognizerMatch
{
    /// <summary>The <see cref="BuiltInRecognizer.Id"/> of the recognizer.</summary>
    public string RecognizerId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the recognizer (mirrors <see cref="BuiltInRecognizer.Name"/>).
    /// Also used in the <c>{r:RecognizerName:...}</c> placeholder syntax.
    /// </summary>
    public string RecognizerName { get; set; } = string.Empty;

    /// <summary>
    /// How to select from the recognized matches.
    /// Values: "first", "last", "all", a single 1-based index like "2",
    /// or comma-separated indices like "1,3,5".
    /// </summary>
    public string MatchMode { get; set; } = "first";

    /// <summary>
    /// Separator string used when <see cref="MatchMode"/> is "all" or specifies
    /// multiple indices. Defaults to ", ".
    /// </summary>
    public string Separator { get; set; } = ", ";

    /// <summary>Whether to emit the normalized value or the matched text. Defaults to resolved value.</summary>
    public RecognizerOutputKind OutputKind { get; set; } = RecognizerOutputKind.ResolvedValue;

    public TemplateRecognizerMatch() { }

    public TemplateRecognizerMatch(string recognizerId, string recognizerName,
        string matchMode = "first", string separator = ", ",
        RecognizerOutputKind outputKind = RecognizerOutputKind.ResolvedValue)
    {
        RecognizerId = recognizerId;
        RecognizerName = recognizerName;
        MatchMode = matchMode;
        Separator = separator;
        OutputKind = outputKind;
    }
}
