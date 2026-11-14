using System.Security.Cryptography;
using System.Text;

namespace Text_Grab.Core.Editing;

/// <summary>One collapsed folding region: the line it starts on and a fingerprint of that line.</summary>
/// <param name="Line">Zero-based line the region starts on.</param>
/// <param name="LineHash">Fingerprint of the start line (and, for a bare "{", the line above it).</param>
/// <param name="Distinctive">False when the fingerprint is too generic to trust away from <paramref name="Line"/>.</param>
public sealed record FoldMark(int Line, string LineHash, bool Distinctive);

/// <summary>Which regions of a document were collapsed, and a fingerprint of the text they were collapsed in.</summary>
public sealed record FoldSnapshot(string TextHash, List<FoldMark> Marks);

/// <summary>
/// Remembers which folding regions were collapsed and finds them again when the document is reopened.
/// </summary>
/// <remarks>
/// Regions are recomputed from the text, so only the collapsed ones are saved, as the line each one
/// starts on. If the text is unchanged they're matched by line. If it changed (the file was edited
/// elsewhere), each is matched by the content of its start line, nearest first, so edits elsewhere in the
/// file only cost the folds they touched. A region that can't be matched is left open.
/// </remarks>
public static class FoldState
{
    /// <summary>The most collapsed regions remembered for one document.</summary>
    public const int MaxMarks = 200;

    private const int MinDistinctiveLength = 4;

    public static FoldSnapshot Capture(string text, IEnumerable<int> foldedRegionStarts)
    {
        int[] lineStarts = GetLineStarts(text);
        List<FoldMark> marks = [.. foldedRegionStarts
            .Select(offset => GetLine(lineStarts, offset))
            .Distinct()
            .Order()
            .Take(MaxMarks)
            .Select(line =>
            {
                (string hash, bool distinctive) = Fingerprint(text, lineStarts, line);
                return new FoldMark(line, hash, distinctive);
            })];

        return new FoldSnapshot(HashText(text), marks);
    }

    /// <summary>The start offsets, among <paramref name="regionStarts"/>, of the regions to collapse again.</summary>
    public static IReadOnlyList<int> Match(FoldSnapshot snapshot, string text, IEnumerable<int> regionStarts)
    {
        if (snapshot.Marks.Count == 0)
            return [];

        int[] lineStarts = GetLineStarts(text);
        List<(int Offset, int Line)> regions = [.. regionStarts
            .Distinct()
            .Select(offset => (Offset: offset, Line: GetLine(lineStarts, offset)))];

        if (string.Equals(snapshot.TextHash, HashText(text), StringComparison.Ordinal))
        {
            HashSet<int> lines = [.. snapshot.Marks.Select(mark => mark.Line)];
            return [.. regions.Where(region => lines.Contains(region.Line)).Select(region => region.Offset)];
        }

        List<int> matched = [];
        HashSet<int> used = [];
        foreach (FoldMark mark in snapshot.Marks)
        {
            int best = -1;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < regions.Count; i++)
            {
                if (used.Contains(i))
                    continue;

                (string hash, bool _) = Fingerprint(text, lineStarts, regions[i].Line);
                if (!string.Equals(hash, mark.LineHash, StringComparison.Ordinal))
                    continue;

                int distance = Math.Abs(regions[i].Line - mark.Line);
                if (!mark.Distinctive && distance != 0)
                    continue;

                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            if (best < 0)
                continue;

            used.Add(best);
            matched.Add(regions[best].Offset);
        }

        return matched;
    }

    internal static string HashText(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static (string Hash, bool Distinctive) Fingerprint(string text, int[] lineStarts, int line)
    {
        string current = GetLineText(text, lineStarts, line).Trim();
        bool distinctive = current.Length >= MinDistinctiveLength;
        string source = current;

        // A bare "{" says nothing; anchor it to the nearest non-blank line above (Allman style).
        if (!distinctive)
        {
            string? above = null;
            for (int i = line - 1; i >= 0 && above is null; i--)
            {
                string candidate = GetLineText(text, lineStarts, i).Trim();
                if (candidate.Length > 0)
                    above = candidate;
            }

            if (above is not null)
            {
                source = above + "\n" + current;
                distinctive = above.Length >= MinDistinctiveLength;
            }
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return (Convert.ToHexString(hash, 0, 8), distinctive);
    }

    private static int[] GetLineStarts(string text)
    {
        List<int> starts = [0];
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                starts.Add(i + 1);
        }

        return [.. starts];
    }

    private static int GetLine(int[] lineStarts, int offset)
    {
        int index = Array.BinarySearch(lineStarts, offset);
        return index >= 0 ? index : ~index - 1;
    }

    private static string GetLineText(string text, int[] lineStarts, int line)
    {
        int start = lineStarts[line];
        int end = line + 1 < lineStarts.Length ? lineStarts[line + 1] : text.Length;
        return text[start..end].TrimEnd('\r', '\n');
    }
}
