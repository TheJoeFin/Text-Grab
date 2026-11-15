using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Documents;

namespace Text_Grab.Utilities;

/// <summary>Finds the word around a <see cref="TextPointer"/> in a FlowDocument.</summary>
internal static class TextPointerWords
{
    /// <summary>
    /// The word that <paramref name="position"/> sits in or at either edge of, or <c>null</c> when it touches
    /// no word (whitespace, punctuation, an embedded control). A word may span formatting, as in
    /// <c>ba**nana**</c>, but not a paragraph, a line break or an embedded control. Apostrophes between
    /// letters belong to the word, so "don't" is one word. Unicode letters, digits and combining marks
    /// include supplementary characters represented by surrogate pairs.
    /// </summary>
    public static (TextPointer Start, TextPointer End)? GetWordRange(TextPointer position)
    {
        TextPointer start = Scan(position, LogicalDirection.Backward);
        TextPointer end = Scan(position, LogicalDirection.Forward);
        return start.CompareTo(end) == 0 ? null : (start, end);
    }

    private static TextPointer Scan(TextPointer origin, LogicalDirection direction)
    {
        TextPointer edge = origin;
        // Starts as the character across the origin, so an apostrophe right next to it ("don'|t") can still join.
        LogicalDirection opposite = direction == LogicalDirection.Forward ? LogicalDirection.Backward : LogicalDirection.Forward;
        Rune? previous = EnumerateCharacters(origin, opposite).Select(static item => (Rune?)item.Character).FirstOrDefault();
        using IEnumerator<(Rune Character, TextPointer After)> characters = EnumerateCharacters(origin, direction).GetEnumerator();
        bool hasCurrent = characters.MoveNext();

        while (hasCurrent)
        {
            (Rune character, TextPointer after) = characters.Current;
            hasCurrent = characters.MoveNext();

            if (IsWordCharacter(character))
            {
                edge = after;
                previous = character;
                continue;
            }

            // An apostrophe only joins two word characters ("don't"), never trails one ("dogs'").
            if (IsApostrophe(character) && previous is Rune before && Rune.IsLetter(before)
                && hasCurrent && Rune.IsLetter(characters.Current.Character))
            {
                previous = character;
                continue;
            }

            break;
        }

        return edge;
    }

    /// <summary>
    /// The characters from <paramref name="origin"/> in <paramref name="direction"/>, each with the position just
    /// past it, stepping through inline formatting elements but stopping at anything that ends a run of text.
    /// </summary>
    private static IEnumerable<(Rune Character, TextPointer After)> EnumerateCharacters(TextPointer origin, LogicalDirection direction)
    {
        bool forward = direction == LogicalDirection.Forward;
        using IEnumerator<(char Character, TextPointer After)> units = EnumerateCodeUnits(origin, direction).GetEnumerator();
        bool hasCurrent = units.MoveNext();

        while (hasCurrent)
        {
            (char unit, TextPointer after) = units.Current;
            hasCurrent = units.MoveNext();

            // Decode across formatting boundaries too, in either scan direction.
            if (hasCurrent && Rune.TryCreate(forward ? unit : units.Current.Character,
                forward ? units.Current.Character : unit, out Rune character))
            {
                after = units.Current.After;
                hasCurrent = units.MoveNext();
            }
            else if (!Rune.TryCreate(unit, out character))
            {
                character = Rune.ReplacementChar;
            }

            yield return (character, after);
        }
    }

    private static IEnumerable<(char Character, TextPointer After)> EnumerateCodeUnits(TextPointer origin, LogicalDirection direction)
    {
        TextPointer? navigator = origin;
        bool forward = direction == LogicalDirection.Forward;

        while (navigator is not null)
        {
            switch (navigator.GetPointerContext(direction))
            {
                case TextPointerContext.Text:
                    string text = navigator.GetTextInRun(direction);
                    for (int i = 0; i < text.Length; i++)
                    {
                        int offset = forward ? i + 1 : -(i + 1);
                        yield return (forward ? text[i] : text[^(i + 1)], navigator.GetPositionAtOffset(offset, direction)!);
                    }

                    navigator = navigator.GetPositionAtOffset(forward ? text.Length : -text.Length, direction);
                    break;

                case TextPointerContext.ElementStart or TextPointerContext.ElementEnd
                    when navigator.GetAdjacentElement(direction) is Run or Span:
                    // Formatting (Bold, Italic, Hyperlink, plain Run) doesn't split a word.
                    navigator = navigator.GetNextContextPosition(direction);
                    break;

                default:
                    // Paragraph or list boundaries, LineBreak, InlineUIContainer, document edges.
                    yield break;
            }
        }
    }

    private static bool IsWordCharacter(Rune character) =>
        Rune.IsLetterOrDigit(character)
        || character.Value == '_'
        || Rune.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark;

    private static bool IsApostrophe(Rune character) => character.Value is '\'' or '’';
}
