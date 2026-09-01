using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Text_Grab.Utilities;

public static partial class NumericUtilities
{
    private static readonly Regex FirstNumericTokenRegex = FirstNumericToken();

    public static double CalculateMedian(List<double> numbers)
    {
        if (numbers.Count == 0)
            return 0;

        List<double> sorted = [.. numbers.OrderBy(n => n)];
        int count = sorted.Count;

        if (count % 2 == 0)
        {
            // Even number of elements - average the two middle values
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
        else
        {
            // Odd number of elements - return the middle value
            return sorted[count / 2];
        }
    }

    public static string FormatNumber(double value)
    {
        // Handle special floating-point values first
        if (double.IsNaN(value))
            return "NaN";

        if (double.IsPositiveInfinity(value))
            return "∞";

        if (double.IsNegativeInfinity(value))
            return "-∞";

        double absValue = Math.Abs(value);

        // Use scientific notation for very large or very small numbers
        if (absValue is >= 1e15 or < 1e-4 and > 0)
        {
            return value.ToString("E6", CultureInfo.CurrentCulture);
        }

        // Check if value is "close enough" to an integer using epsilon comparison
        // Use a small tolerance to account for floating-point precision
        double fractionalPart = Math.Abs(value - Math.Round(value));
        bool isEffectivelyInteger = fractionalPart < 1e-10 && absValue < 1e10;

        if (isEffectivelyInteger)
        {
            return Math.Round(value).ToString("N0", CultureInfo.CurrentCulture);
        }
        else
        {
            return value.ToString("N", CultureInfo.CurrentCulture);
        }
    }

    public static bool AreClose(double a, double b, double epsilon = 0.25)
    {
        return Math.Abs(a - b) < epsilon;
    }

    public static bool TryExtractFirstDouble(string input, out double value)
    {
        if (TryParseFlexibleDouble(input, out value))
            return true;

        foreach (Match match in FirstNumericTokenRegex.Matches(input))
        {
            if (TryParseFlexibleDouble(match.Value, out value))
                return true;
        }

        value = 0;
        return false;
    }

    public static bool TryParseFlexibleDouble(string input, out double value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        string normalized = NormalizeNumberString(input);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return double.TryParse(
            normalized,
            NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string NormalizeNumberString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        StringBuilder sb = new();
        foreach (char c in input.Trim())
        {
            if (c is not ' ' and not '_')
                sb.Append(c);
        }

        string compact = sb.ToString();
        int commaIndex = compact.IndexOf(',');
        int dotIndex = compact.IndexOf('.');

        if (commaIndex >= 0 && dotIndex >= 0)
        {
            if (commaIndex > dotIndex)
            {
                compact = compact.Replace(".", string.Empty);
                compact = compact.Replace(',', '.');
            }
            else
            {
                compact = compact.Replace(",", string.Empty);
            }
        }
        else if (commaIndex >= 0)
        {
            int lastCommaIndex = compact.LastIndexOf(',');
            int digitsAfterComma = compact.Length - lastCommaIndex - 1;
            bool hasMultipleCommas = compact.Count(c => c == ',') > 1;

            if (hasMultipleCommas || digitsAfterComma == 3)
                compact = compact.Replace(",", string.Empty);
            else
                compact = compact.Replace(',', '.');
        }
        else if (dotIndex >= 0)
        {
            int lastDotIndex = compact.LastIndexOf('.');
            int digitsAfterDot = compact.Length - lastDotIndex - 1;
            bool hasMultipleDots = compact.Count(c => c == '.') > 1;
            // A leading 0 before the dot (e.g. "0.345") means it can't be a thousands separator.
            string beforeDot = compact[..lastDotIndex].TrimStart('-');
            bool couldBeThousandsSep = beforeDot.Length > 0 && !beforeDot.StartsWith('0');

            if (hasMultipleDots || (digitsAfterDot == 3 && couldBeThousandsSep))
                compact = compact.Replace(".", string.Empty);
        }

        return compact;
    }

    [GeneratedRegex(@"[-+]?(?:(?:\d[\d\s_,.]*)?\d)(?:[eE][-+]?\d+)?", RegexOptions.Compiled)]
    private static partial Regex FirstNumericToken();
}
