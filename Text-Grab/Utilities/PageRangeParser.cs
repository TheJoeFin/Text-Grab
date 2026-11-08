using System;
using System.Collections.Generic;

namespace Text_Grab.Utilities;

/// <summary>
/// Parses print-dialog style page selections such as "4-9,12", "5-" (page 5 to the end)
/// or "-3" (the first three pages). Page numbers in the text are 1-based; results are
/// 0-based, sorted and de-duplicated, like a printer's "Pages" box.
/// </summary>
public static class PageRangeParser
{
    /// <summary>
    /// Returns true with the 0-based page indices when <paramref name="text"/> is valid.
    /// Blank text selects every page. On failure <paramref name="error"/> says what is wrong.
    /// </summary>
    public static bool TryParse(string? text, int pageCount, out List<int> pageIndices, out string error)
    {
        pageIndices = [];
        error = string.Empty;

        if (pageCount < 1)
        {
            error = "The document has no pages.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            for (int i = 0; i < pageCount; i++)
                pageIndices.Add(i);
            return true;
        }

        SortedSet<int> selected = [];
        string[] parts = text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            error = "Enter page numbers, like 4-9,12.";
            return false;
        }

        foreach (string part in parts)
        {
            int dash = part.IndexOf('-');
            int first;
            int last;

            if (dash < 0)
            {
                if (!TryParsePage(part, out first))
                {
                    error = $"\"{part}\" is not a page number.";
                    return false;
                }

                last = first;
            }
            else
            {
                string left = part[..dash].Trim();
                string right = part[(dash + 1)..].Trim();

                if (left.Length == 0 && right.Length == 0)
                {
                    error = "A range needs a start or an end page.";
                    return false;
                }

                first = 1;
                last = pageCount;

                if (left.Length > 0 && !TryParsePage(left, out first))
                {
                    error = $"\"{left}\" is not a page number.";
                    return false;
                }

                if (right.Length > 0 && !TryParsePage(right, out last))
                {
                    error = $"\"{right}\" is not a page number.";
                    return false;
                }
            }

            if (first < 1 || last < 1 || first > pageCount || last > pageCount)
            {
                error = $"Pages must be between 1 and {pageCount}.";
                return false;
            }

            if (first > last)
            {
                error = $"\"{part}\" runs backwards. Put the lower page first.";
                return false;
            }

            for (int page = first; page <= last; page++)
                selected.Add(page - 1);
        }

        pageIndices = [.. selected];
        return true;
    }

    private static bool TryParsePage(string text, out int page) =>
        int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out page);
}
