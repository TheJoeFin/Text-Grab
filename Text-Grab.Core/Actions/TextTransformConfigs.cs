using Text_Grab.Utilities;

namespace Text_Grab.Core.Actions;

/// <summary>Config for <c>textgrab.text.cleanUp</c>.</summary>
/// <param name="CorrectToLatin">
/// Also map look-alike Greek and Cyrillic characters to Latin ones. The Edit Text Window turns this
/// on when the Correct To Latin setting is on and the input language is Latin-based.
/// </param>
public sealed record CleanUpTextConfig(bool CorrectToLatin = false);

/// <summary>Config for <c>textgrab.text.unstack</c>.</summary>
/// <param name="Columns">How many lines go side by side in each row. At least 1.</param>
public sealed record UnstackConfig(int Columns);

/// <summary>Config for <c>textgrab.text.unstackGroups</c>.</summary>
/// <param name="Rows">How many rows the lines are dealt into. At least 1.</param>
public sealed record UnstackGroupsConfig(int Rows);

/// <summary>Config for <c>textgrab.text.applyPattern</c>.</summary>
/// <param name="Pattern">The .NET regular expression to run, in multiline mode.</param>
/// <param name="Mode">Which matches to keep: <c>first</c>, <c>last</c>, <c>all</c>, or 1-based indices such as <c>2</c> or <c>1,3</c>.</param>
/// <param name="Separator">Joins the kept matches.</param>
public sealed record ApplyPatternConfig(string Pattern, string Mode = "all", string Separator = ", ");

/// <summary>Config for <c>textgrab.text.applyRecognizer</c>.</summary>
/// <param name="RecognizerId">A built-in recognizer's ID, such as <c>datetime</c> or <c>email</c>.</param>
/// <param name="Mode">Which matches to keep, as in <see cref="ApplyPatternConfig.Mode"/>.</param>
/// <param name="Separator">Joins the kept matches.</param>
/// <param name="Output">Whether each match gives its resolved value or the text as written.</param>
public sealed record ApplyRecognizerConfig(
    string RecognizerId,
    string Mode = "all",
    string Separator = ", ",
    RecognizerOutputKind Output = RecognizerOutputKind.ResolvedValue);
