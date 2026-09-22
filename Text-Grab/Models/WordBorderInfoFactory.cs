using System;
using Text_Grab.Controls;

namespace Text_Grab.Models;

/// <summary>
/// Builds a pure <see cref="WordBorderInfo"/> projection from the WPF <see cref="WordBorder"/>
/// control. Split out of <see cref="WordBorderInfo"/> so that class could move to
/// Text-Grab.Core — <see cref="WordBorder"/> is a WPF control and can never follow it there.
/// </summary>
public static class WordBorderInfoFactory
{
    public static WordBorderInfo Create(WordBorder wordBorder)
    {
        return new WordBorderInfo
        {
            Word = wordBorder.Word,
            DisplayText = wordBorder.KeepSingleLineOutput || !string.Equals(wordBorder.DisplayText, wordBorder.Word, StringComparison.Ordinal)
                ? wordBorder.DisplayText
                : string.Empty,
            DisplayLineHeight = wordBorder.DisplayLineHeight,
            KeepSingleLineOutput = wordBorder.KeepSingleLineOutput,
            LineNumber = wordBorder.LineNumber,
            ResultColumnID = wordBorder.ResultColumnID,
            ResultRowID = wordBorder.ResultRowID,
            MatchingBackground = wordBorder.MatchingBackground.ToString(),
            IsBarcode = wordBorder.IsBarcode,
            BorderRect = new()
            {
                X = (float)wordBorder.Left,
                Y = (float)wordBorder.Top,
                Width = (float)wordBorder.Width,
                Height = (float)wordBorder.Height
            }
        };
    }
}
