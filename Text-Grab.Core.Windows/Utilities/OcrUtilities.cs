using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>
/// Turning an OCR result into text: word- and line-level assembly, the furigana and reading-flow
/// heuristics, and the paragraph-wrap grouping.
///
/// The portable half of what used to be Text-Grab/Utilities/OcrUtilities.cs (batch 4c of the Core
/// split). It keeps the original type name because that is what nearly every call site wants -
/// Tests/OcrTests.cs alone accounts for 43 of the old file's ~80 references, all against these
/// members. The other half - screen and window capture, engine dispatch, file and BitmapSource
/// sources - stays in the app as OcrSourceUtilities: it needs WPF, and also WindowsAiUtilities
/// and LanguageUtilities, neither of which has moved yet.
/// </summary>
public static partial class OcrUtilities
{
    // Cache the SpaceJoiningWordRegex to avoid creating it on every method call
    private static readonly Regex _cachedSpaceJoiningWordRegex = SpaceJoiningWordRegex();

    public static List<WordBorderInfo> ParseOcrResultIntoWordBorderInfos(
        IOcrLinesWords ocrResult,
        bool shouldCorrectToLatin = true)
    {
        List<WordBorderInfo> infos = [];

        foreach (IOcrLine ocrLine in ocrResult.Lines)
        {
            double top = ocrLine.Words.Select(x => x.BoundingBox.Top).Min();
            double bottom = ocrLine.Words.Select(x => x.BoundingBox.Bottom).Max();
            double left = ocrLine.Words.Select(x => x.BoundingBox.Left).Min();
            double right = ocrLine.Words.Select(x => x.BoundingBox.Right).Max();

            RectangleF lineRect = new(
                (float)left,
                (float)top,
                (float)Math.Abs(right - left),
                (float)Math.Abs(bottom - top));

            StringBuilder lineText = new();
            ocrLine.GetTextFromOcrLine(true, lineText, shouldCorrectToLatin);

            WordBorderInfo info = new()
            {
                BorderRect = lineRect,
                Word = lineText.ToString().Trim(),
                ResultRowID = 0,
                ResultColumnID = 0
            };

            infos.Add(info);
        }

        return infos;
    }

    public static void GetTextFromOcrLine(
        this IOcrLine ocrLine,
        bool isSpaceJoiningOCRLang,
        StringBuilder text,
        bool shouldCorrectToLatin = true)
    {
        // (when OCR language is zh or ja)
        // matches words in a space-joining language, which contains:
        // - one letter that is not in "other letters" (CJK characters are "other letters")
        // - one number digit
        // - any words longer than one character
        // Chinese and Japanese characters are single-character words
        // when a word is one punctuation/symbol, join it without spaces

        if (isSpaceJoiningOCRLang)
        {
            text.AppendLine(ocrLine.Text);

            if (SettingsAccess.Current.CorrectErrors)
                text.TryFixEveryWordLetterNumberErrors();
        }
        else
        {
            // For CJK languages, filter out likely furigana (small ruby-text
            // characters above the main text) before merging the words. This is
            // opt-in via the RemoveFurigana setting.
            IEnumerable<IOcrWord> words = SettingsAccess.Current.RemoveFurigana
                ? FilterFurigana([.. ocrLine.Words])
                : ocrLine.Words;

            bool isFirstWord = true;
            bool isPrevWordSpaceJoining = false;

            foreach (IOcrWord ocrWord in words)
            {
                string wordString = ocrWord.Text;

                bool isThisWordSpaceJoining = _cachedSpaceJoiningWordRegex.IsMatch(wordString);

                if (SettingsAccess.Current.CorrectErrors)
                    wordString = wordString.TryFixNumberLetterErrors();

                if (isFirstWord || (!isThisWordSpaceJoining && !isPrevWordSpaceJoining))
                    _ = text.Append(wordString);
                else
                    _ = text.Append(' ').Append(wordString);

                isFirstWord = false;
                isPrevWordSpaceJoining = isThisWordSpaceJoining;
            }
        }

        if (SettingsAccess.Current.CorrectToLatin && shouldCorrectToLatin)
            text.ReplaceGreekOrCyrillicWithLatin();
    }

    /// <summary>
    /// Removes words that are likely furigana: small ruby-text characters
    /// rendered above the main text in Japanese. A word is treated as furigana
    /// when it is noticeably shorter than the line's median word height and sits
    /// directly above a larger word that overlaps it horizontally.
    /// </summary>
    internal static List<IOcrWord> FilterFurigana(List<IOcrWord> words)
    {
        if (words.Count == 0)
            return words;

        // Furigana is typically around half the height of the main text.
        List<double> heights = [.. words.Select(w => w.BoundingBox.Height).OrderBy(h => h)];
        double medianHeight = heights[heights.Count / 2];
        double furiganaThreshold = medianHeight * 0.6;

        List<IOcrWord> filteredWords = [];

        for (int i = 0; i < words.Count; i++)
        {
            IOcrWord word = words[i];
            bool isProbablyFurigana = false;

            if (word.BoundingBox.Height < furiganaThreshold)
            {
                // Only treat it as furigana when a larger word sits below it and
                // overlaps horizontally (i.e. the kanji it annotates).
                for (int j = 0; j < words.Count; j++)
                {
                    if (i == j)
                        continue;

                    IOcrWord otherWord = words[j];

                    bool isBelow = otherWord.BoundingBox.Top > word.BoundingBox.Bottom;
                    bool overlapsHorizontally = !(otherWord.BoundingBox.Right < word.BoundingBox.Left
                        || otherWord.BoundingBox.Left > word.BoundingBox.Right);
                    bool isLarger = otherWord.BoundingBox.Height > furiganaThreshold;

                    if (isBelow && overlapsHorizontally && isLarger)
                    {
                        isProbablyFurigana = word.Text.Length <= 2;
                        break;
                    }
                }
            }

            if (!isProbablyFurigana)
                filteredWords.Add(word);
        }

        // If everything was filtered, fall back to the original words to avoid
        // dropping the whole line.
        return filteredWords.Count > 0 ? filteredWords : words;
    }

    internal readonly record struct PositionedOcrLine(int LineNumber, string Text, Windows.Foundation.Rect BoundingBox);

    internal sealed class GroupedOcrLines(IReadOnlyList<PositionedOcrLine> lines, Windows.Foundation.Rect boundingBox)
    {
        public Windows.Foundation.Rect BoundingBox { get; } = boundingBox;

        public IReadOnlyList<PositionedOcrLine> Lines { get; } = lines;

        public int StartingLineNumber => Lines.Count == 0 ? 0 : Lines[0].LineNumber;

        public string DisplayText => string.Join(Environment.NewLine, Lines.Select(static line => line.Text.MakeStringSingleLine()));

        public string SingleLineText => string.Join(" ", Lines.Select(static line => line.Text.MakeStringSingleLine()).Where(static text => !string.IsNullOrWhiteSpace(text)));
    }

    internal static string BuildTextFromOcrLines(ILanguage language, IOcrLinesWords ocrResult)
    {
        StringBuilder text = new();

        bool isSpaceJoiningOCRLang = language.IsSpaceJoining();
        IOcrLine[] lines = ocrResult.Lines;

        if (ShouldUseParagraphDetection(isSpaceJoiningOCRLang) && lines.Length > 0)
        {
            List<GroupedOcrLines> groupedLines =
            [
                .. GroupWrappedParagraphLines(
                    [.. lines.Select((line, index) => new PositionedOcrLine(index, line.Text, line.BoundingBox))])
            ];

            for (int i = 0; i < groupedLines.Count; i++)
            {
                if (i > 0)
                    text.AppendLine();

                text.Append(groupedLines[i].SingleLineText);
            }
        }
        else
        {
            // Windows OCR returns CJK lines - especially furigana ruby lines and
            // stray fragments - in an order that does not follow the page's reading
            // flow, so re-sort by geometry (top-to-bottom, then left-to-right)
            // before joining. Space-joining languages keep the engine order because
            // paragraph detection above already handles their layout.
            IReadOnlyList<IOcrLine> orderedLines = isSpaceJoiningOCRLang
                ? lines
                : OrderLinesForReadingFlow(lines);

            // Windows OCR emits furigana (Japanese ruby readings) as their own
            // short lines sitting directly above the kanji they annotate, so the
            // word-level filter above never catches them. Drop those whole lines
            // when furigana removal is enabled.
            if (!isSpaceJoiningOCRLang && SettingsAccess.Current.RemoveFurigana)
                orderedLines = FilterFuriganaLines(orderedLines);

            foreach (IOcrLine ocrLine in orderedLines)
                ocrLine.GetTextFromOcrLine(isSpaceJoiningOCRLang, text, language.IsLatinBased());
        }

        if (language.IsRightToLeft())
            text.ReverseWordsForRightToLeft();

        return text.ToString();
    }

    /// <summary>
    /// Re-orders OCR lines into natural reading flow: groups lines that share a
    /// horizontal row (their vertical extents overlap), orders rows top-to-bottom,
    /// and orders the lines within each row left-to-right. Windows OCR frequently
    /// returns CJK lines out of order (furigana above kanji, trailing fragments),
    /// which scrambles the concatenated text without this pass.
    /// </summary>
    internal static IReadOnlyList<IOcrLine> OrderLinesForReadingFlow(IReadOnlyList<IOcrLine> lines)
    {
        if (lines.Count <= 1)
            return lines;

        // Stable sort by the top edge so rows are discovered top-to-bottom.
        List<IOcrLine> byTop = [.. lines.OrderBy(line => line.BoundingBox.Top)];

        List<List<IOcrLine>> rows = [];
        double currentRowTop = 0;
        double currentRowBottom = 0;

        foreach (IOcrLine line in byTop)
        {
            Windows.Foundation.Rect box = line.BoundingBox;

            if (rows.Count > 0)
            {
                double overlap = Math.Min(currentRowBottom, box.Bottom) - Math.Max(currentRowTop, box.Top);
                double minHeight = Math.Min(currentRowBottom - currentRowTop, box.Height);

                // A line joins the current row when it overlaps the row's vertical
                // band by more than half of the shorter of the two heights.
                if (minHeight > 0 && overlap > minHeight * 0.5)
                {
                    rows[^1].Add(line);
                    currentRowTop = Math.Min(currentRowTop, box.Top);
                    currentRowBottom = Math.Max(currentRowBottom, box.Bottom);
                    continue;
                }
            }

            rows.Add([line]);
            currentRowTop = box.Top;
            currentRowBottom = box.Bottom;
        }

        List<IOcrLine> ordered = [];
        foreach (List<IOcrLine> row in rows)
            ordered.AddRange(row.OrderBy(line => line.BoundingBox.Left));

        return ordered;
    }

    /// <summary>
    /// Removes whole OCR lines that are likely furigana: short ruby-reading lines
    /// that sit directly above a substantially taller line overlapping them
    /// horizontally (the kanji they annotate). Windows OCR returns furigana as
    /// their own lines, so this complements the word-level <see cref="FilterFurigana"/>.
    /// The heuristic is intentionally conservative and geometry-only; it can miss
    /// mis-detected readings and is offered as an opt-in, experimental setting.
    /// </summary>
    internal static IReadOnlyList<IOcrLine> FilterFuriganaLines(IReadOnlyList<IOcrLine> lines)
    {
        if (lines.Count < 2)
            return lines;

        List<IOcrLine> kept = [];

        for (int i = 0; i < lines.Count; i++)
        {
            Windows.Foundation.Rect box = lines[i].BoundingBox;
            bool isFurigana = false;

            for (int j = 0; j < lines.Count; j++)
            {
                if (i == j)
                    continue;

                Windows.Foundation.Rect other = lines[j].BoundingBox;

                bool isBelow = other.Top >= box.Bottom;
                bool overlapsHorizontally = !(other.Right < box.Left || other.Left > box.Right);
                // The annotated kanji is markedly taller than its reading.
                bool isSubstantiallyTaller = other.Height > box.Height * 1.4;
                // Ruby text hugs the top of its character; a large vertical gap
                // means these are separate lines of body text, not a reading.
                bool isCloseAbove = other.Top - box.Bottom < box.Height;

                if (isBelow && overlapsHorizontally && isSubstantiallyTaller && isCloseAbove)
                {
                    isFurigana = true;
                    break;
                }
            }

            if (!isFurigana)
                kept.Add(lines[i]);
        }

        // Never drop everything - fall back to the input if the heuristic would
        // erase the whole result.
        return kept.Count > 0 ? kept : lines;
    }

    internal static bool ShouldUseParagraphDetection(bool isSpaceJoiningLanguage, bool isTableMode = false)
    {
        return SettingsAccess.Current.ParagraphDetection && isSpaceJoiningLanguage && !isTableMode;
    }

    internal static List<GroupedOcrLines> GroupWrappedParagraphLines(IReadOnlyList<PositionedOcrLine> lines)
    {
        List<GroupedOcrLines> groupedLines = [];

        if (lines.Count == 0)
            return groupedLines;

        List<PositionedOcrLine> currentGroup = [lines[0]];
        Windows.Foundation.Rect currentBounds = lines[0].BoundingBox;

        for (int i = 1; i < lines.Count; i++)
        {
            PositionedOcrLine previousLine = currentGroup[^1];
            PositionedOcrLine currentLine = lines[i];

            if (IsWrappedParagraph(
                previousLine.BoundingBox.Y,
                previousLine.BoundingBox.Height,
                currentLine.BoundingBox.Y,
                currentLine.BoundingBox.Height))
            {
                currentGroup.Add(currentLine);
                currentBounds = UnionRectangles(currentBounds, currentLine.BoundingBox);
                continue;
            }

            groupedLines.Add(new GroupedOcrLines([.. currentGroup], currentBounds));
            currentGroup = [currentLine];
            currentBounds = currentLine.BoundingBox;
        }

        groupedLines.Add(new GroupedOcrLines([.. currentGroup], currentBounds));
        return groupedLines;
    }

    private static Windows.Foundation.Rect UnionRectangles(Windows.Foundation.Rect current, Windows.Foundation.Rect next)
    {
        if (current.IsEmpty)
            return next;

        if (next.IsEmpty)
            return current;

        double left = Math.Min(current.X, next.X);
        double top = Math.Min(current.Y, next.Y);
        double right = Math.Max(current.X + current.Width, next.X + next.Width);
        double bottom = Math.Max(current.Y + current.Height, next.Y + next.Height);
        return new Windows.Foundation.Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Determines whether two consecutive lines belong to the same wrapped paragraph
    /// by comparing the vertical gap between them relative to the average line height.
    /// Returns true if the lines should be joined with a space (same paragraph, wrapped),
    /// false if they should be separated by a newline (different paragraphs).
    /// </summary>
    internal static bool IsWrappedLine(IOcrLine currentLine, IOcrLine nextLine)
    {
        if (currentLine.BoundingBox.IsEmpty || nextLine.BoundingBox.IsEmpty)
            return false;

        return IsWrappedParagraph(
            currentLine.BoundingBox.Y,
            currentLine.BoundingBox.Height,
            nextLine.BoundingBox.Y,
            nextLine.BoundingBox.Height);
    }

    /// <summary>
    /// Core paragraph-wrap heuristic: returns true when the vertical gap between two
    /// lines is small enough (less than 60 % of the average line height) that they
    /// belong to the same wrapped paragraph, and their heights are similar (ratio ≤ 1.5).
    /// Works for any coordinate space — ratios are scale-invariant.
    /// </summary>
    internal static bool IsWrappedParagraph(
        double currentTop, double currentHeight,
        double nextTop, double nextHeight)
    {
        if (currentHeight <= 0 || nextHeight <= 0)
            return false;

        // Lines with significantly different heights are likely different content blocks
        double minHeight = Math.Min(currentHeight, nextHeight);
        double maxHeight = Math.Max(currentHeight, nextHeight);
        if (maxHeight / minHeight > 1.5)
            return false;

        // Consecutive OCR entries must advance to a distinct visual row. Without
        // this guard, duplicate or horizontally split entries on the same row have
        // a negative gap and are incorrectly merged into a one-line-tall paragraph.
        if (nextTop - currentTop < minHeight * 0.5)
            return false;

        // If the vertical gap between line bounding boxes is less than 0.6× the average line
        // height, the lines are part of the same paragraph (normal line spacing); otherwise
        // the extra whitespace signals a paragraph break.
        double gap = nextTop - (currentTop + currentHeight);
        double avgLineHeight = (currentHeight + nextHeight) / 2.0;
        return gap < avgLineHeight * 0.6;
    }

    public static string GetStringFromOcrOutputs(List<OcrOutput> outputs)
    {
        StringBuilder text = new();

        foreach (OcrOutput output in outputs)
        {
            output.CleanOutput();

            if (!string.IsNullOrWhiteSpace(output.CleanedOutput))
                text.Append(output.CleanedOutput);
            else if (!string.IsNullOrWhiteSpace(output.RawOutput))
                text.Append(output.RawOutput);
        }

        return text.ToString();
    }

    [GeneratedRegex(@"(^[\p{L}-[\p{Lo}]]|\p{Nd}$)|.{2,}")]
    private static partial Regex SpaceJoiningWordRegex();
}
