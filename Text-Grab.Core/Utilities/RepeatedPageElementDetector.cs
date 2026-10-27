using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace Text_Grab.Utilities;

/// <summary>
/// One piece of text (a word border or a native-PDF text line) on a page, in page coordinates.
/// </summary>
public readonly record struct PageTextElement(string Text, RectangleF Bounds);

/// <summary>
/// A page's text elements plus the size of the page they were laid out on.
/// </summary>
public sealed record PageTextSnapshot(IReadOnlyList<PageTextElement> Elements, SizeF PageSize);

/// <summary>
/// Finds running page headers and footers across a set of pages: elements that sit in the top
/// or bottom margin of the page, appear at nearly the same position on other pages, and carry
/// the same text — or nearly the same, so "Page 3 of 12" matches "Page 4 of 12" and OCR noise
/// ("Confidentia1") still matches. Repeated elements that butt directly against the page body
/// (a table's column-header row, say) are left alone: a header or footer is separated from the
/// content by a margin gap, a table header is not.
/// </summary>
public static class RepeatedPageElementDetector
{
    /// <summary>Only elements whose center falls within this fraction of the page height from the top or bottom edge are candidates.</summary>
    public const float MarginBandFraction = 0.2f;

    /// <summary>A candidate must match on at least this fraction of the other pages (rounded up, minimum one page).</summary>
    public const double RequiredMatchFraction = 1.0 / 3.0;

    /// <summary>Two elements are "in the same place" when their centers differ by no more than this many element heights (with a small absolute floor).</summary>
    private const float PositionToleranceHeightFactor = 1.0f;
    private const float PositionToleranceFloor = 6f;

    /// <summary>Normalized texts this similar (0..1) are treated as the same text, to absorb OCR noise.</summary>
    private const double FuzzyTextSimilarityThreshold = 0.85;

    /// <summary>A repeated element closer than this many of its own heights to non-repeated content is attached to the body, not a header/footer.</summary>
    private const float BodyGapHeightFactor = 1.5f;

    private static readonly Regex DigitRunRegex = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRunRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Returns, for each page in <paramref name="pages"/>, the indices into that page's
    /// <see cref="PageTextSnapshot.Elements"/> that are repeated headers or footers and should be
    /// ignored. Every returned set is empty when there are fewer than two pages.
    /// </summary>
    public static List<HashSet<int>> FindRepeatedHeaderFooterElements(IReadOnlyList<PageTextSnapshot> pages)
    {
        List<HashSet<int>> ignoredPerPage = [.. pages.Select(_ => new HashSet<int>())];

        if (pages.Count < 2)
            return ignoredPerPage;

        List<List<Candidate>> candidatesPerPage = [.. pages.Select(CollectMarginCandidates)];
        int requiredMatches = Math.Max(1, (int)Math.Ceiling((pages.Count - 1) * RequiredMatchFraction));

        for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            foreach (Candidate candidate in candidatesPerPage[pageIndex])
            {
                int matchingPages = 0;

                for (int otherIndex = 0; otherIndex < pages.Count && matchingPages < requiredMatches; otherIndex++)
                {
                    if (otherIndex == pageIndex)
                        continue;

                    if (candidatesPerPage[otherIndex].Any(other => IsSameElement(candidate, other)))
                        matchingPages++;
                }

                if (matchingPages >= requiredMatches)
                    ignoredPerPage[pageIndex].Add(candidate.Index);
            }
        }

        for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            RemoveElementsAttachedToBody(pages[pageIndex], ignoredPerPage[pageIndex]);

        return ignoredPerPage;
    }

    /// <summary>
    /// Collapses whitespace, ignores case, and replaces every run of digits with a placeholder
    /// so texts that differ only by a number ("Page 3", "Page 4") compare equal.
    /// </summary>
    public static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string collapsed = WhitespaceRunRegex.Replace(text.Trim(), " ");
        return DigitRunRegex.Replace(collapsed, "#").ToUpperInvariant();
    }

    /// <summary>
    /// Similarity of two strings in 0..1 based on edit distance (1 = identical).
    /// </summary>
    public static double TextSimilarity(string first, string second)
    {
        if (first.Length == 0 && second.Length == 0)
            return 1;

        int longest = Math.Max(first.Length, second.Length);
        return 1 - ((double)LevenshteinDistance(first, second) / longest);
    }

    private sealed record Candidate(int Index, PageTextElement Element, string NormalizedText, PointF Center, bool IsTopBand);

    private static List<Candidate> CollectMarginCandidates(PageTextSnapshot page)
    {
        List<Candidate> candidates = [];
        float pageHeight = page.PageSize.Height;

        if (pageHeight <= 0)
            return candidates;

        float topBandLimit = pageHeight * MarginBandFraction;
        float bottomBandStart = pageHeight * (1 - MarginBandFraction);

        for (int index = 0; index < page.Elements.Count; index++)
        {
            PageTextElement element = page.Elements[index];
            string normalized = NormalizeText(element.Text);
            if (normalized.Length == 0)
                continue;

            PointF center = CenterOf(element.Bounds);
            bool isTopBand = center.Y <= topBandLimit;
            bool isBottomBand = center.Y >= bottomBandStart;

            if (isTopBand || isBottomBand)
                candidates.Add(new Candidate(index, element, normalized, center, isTopBand));
        }

        return candidates;
    }

    private static bool IsSameElement(Candidate first, Candidate second)
    {
        if (first.IsTopBand != second.IsTopBand)
            return false;

        float tolerance = Math.Max(
            PositionToleranceFloor,
            PositionToleranceHeightFactor * Math.Max(first.Element.Bounds.Height, second.Element.Bounds.Height));

        if (Math.Abs(first.Center.X - second.Center.X) > tolerance
            || Math.Abs(first.Center.Y - second.Center.Y) > tolerance)
        {
            return false;
        }

        if (first.NormalizedText == second.NormalizedText)
            return true;

        // Fuzzy matching only earns its keep on text long enough that a one-character OCR
        // slip is not most of the string.
        if (Math.Min(first.NormalizedText.Length, second.NormalizedText.Length) < 4)
            return false;

        return TextSimilarity(first.NormalizedText, second.NormalizedText) >= FuzzyTextSimilarityThreshold;
    }

    /// <summary>
    /// Un-flags elements that are attached to page content that is being kept: a repeated table
    /// column-header row has data rows immediately below it, and a numeric cell that happens to
    /// match across pages ("4" vs "7" both normalize to "#") shares its line with cells that
    /// differ. A running page header or footer has neither — it sits alone in the margin with a
    /// gap before the body. The check is transitive: once a cell is kept, its line-mates are
    /// kept, then the header row directly above them, and so on until nothing changes.
    /// </summary>
    private static void RemoveElementsAttachedToBody(PageTextSnapshot page, HashSet<int> flagged)
    {
        if (flagged.Count == 0)
            return;

        List<int> keptIndices = [];
        for (int index = 0; index < page.Elements.Count; index++)
        {
            if (!flagged.Contains(index) && !string.IsNullOrWhiteSpace(page.Elements[index].Text))
                keptIndices.Add(index);
        }

        bool changed = true;
        while (changed && flagged.Count > 0)
        {
            changed = false;

            foreach (int index in flagged.ToList())
            {
                RectangleF bounds = page.Elements[index].Bounds;
                bool isAttached = keptIndices.Any(keptIndex => IsAttached(bounds, page.Elements[keptIndex].Bounds));
                if (!isAttached)
                    continue;

                flagged.Remove(index);
                keptIndices.Add(index);
                changed = true;
            }
        }
    }

    /// <summary>
    /// True when <paramref name="candidate"/> shares a text line with <paramref name="kept"/>, or
    /// sits directly above or below it (within a line-and-a-half) in the same column.
    /// </summary>
    private static bool IsAttached(RectangleF candidate, RectangleF kept)
    {
        float minHeight = Math.Max(1, Math.Min(candidate.Height, kept.Height));
        float verticalOverlap = Math.Min(candidate.Bottom, kept.Bottom) - Math.Max(candidate.Top, kept.Top);
        if (verticalOverlap >= minHeight * 0.5f)
            return true;

        if (!HorizontallyOverlapOrNear(candidate, kept))
            return false;

        float gap = candidate.Top < kept.Top
            ? kept.Top - candidate.Bottom
            : candidate.Top - kept.Bottom;

        return gap < BodyGapHeightFactor * Math.Max(1, candidate.Height);
    }

    private static bool HorizontallyOverlapOrNear(RectangleF first, RectangleF second)
    {
        // Column headers line up over their column; a page number in the corner has nothing
        // beneath it. Allow a little slack so slightly offset columns still count.
        float slack = Math.Max(first.Height, second.Height);
        return first.Left - slack < second.Right && second.Left - slack < first.Right;
    }

    private static PointF CenterOf(RectangleF rect) => new(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));

    private static int LevenshteinDistance(string first, string second)
    {
        if (first.Length == 0)
            return second.Length;
        if (second.Length == 0)
            return first.Length;

        int[] previous = new int[second.Length + 1];
        int[] current = new int[second.Length + 1];

        for (int j = 0; j <= second.Length; j++)
            previous[j] = j;

        for (int i = 1; i <= first.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= second.Length; j++)
            {
                int substitutionCost = first[i - 1] == second[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[second.Length];
    }
}
