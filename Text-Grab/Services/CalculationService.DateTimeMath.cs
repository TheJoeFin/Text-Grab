using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Text_Grab.Services;

public partial class CalculationService
{
    /// <summary>
    /// Attempts to evaluate a line as a date/time math expression.
    /// Supports expressions like "March 10th + 10 days", "2/25/26 11:02pm + 800 mins", etc.
    /// Also supports combined duration segments: "today + 5 weeks 3 days 8 hours".
    /// Supported units: days, weeks, months, years, decades, hours, minutes.
    /// </summary>
    /// <param name="line">The input line to evaluate</param>
    /// <param name="result">The formatted date/time result if successful</param>
    /// <returns>True if the line was successfully evaluated as a date/time math expression</returns>
    public static bool TryEvaluateDateTimeMath(string line, out string result)
    {
        result = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        // Find the first explicit arithmetic operation (requires +/-) to anchor where arithmetic starts
        Match anchorMatch = DateTimeArithmeticPattern().Match(line);
        if (!anchorMatch.Success)
            return false;

        // Everything before the first arithmetic match is the date part
        string datePart = line[..anchorMatch.Index].Trim();

        // Parse the base date
        DateTime dateTime;
        bool hasInputTime;

        if (string.IsNullOrEmpty(datePart))
        {
            dateTime = DateTime.Today;
            hasInputTime = false;
        }
        else if (!TryParseFlexibleDate(datePart, out dateTime, out hasInputTime))
        {
            return false;
        }

        // Parse all duration segments from the arithmetic portion.
        // Supports both explicit operators (+ 5 days - 2 hours) and combined
        // segments where implicit entries inherit the previous operator (+ 5 weeks 3 days 8 hours).
        string arithmeticPortion = line[anchorMatch.Index..];
        MatchCollection segments = DateTimeDurationSegmentPattern().Matches(arithmeticPortion);
        if (segments.Count == 0)
            return false;

        bool hasTimeUnits = false;
        bool hasFractionalDayOrLarger = false;
        List<(double Number, string Unit)> operations = [];
        string currentOp = "+";

        foreach (Match segment in segments)
        {
            string opValue = segment.Groups["op"].Value;
            if (!string.IsNullOrEmpty(opValue))
                currentOp = opValue;

            string numberStr = segment.Groups["number"].Value;
            string unit = segment.Groups["unit"].Value.ToLowerInvariant();

            if (!double.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
                return false;

            if (currentOp == "-")
                number = -number;

            bool isTimeUnit = unit is "hour" or "hours" or "hr" or "hrs"
                              or "minute" or "minutes" or "min" or "mins";
            if (isTimeUnit)
                hasTimeUnits = true;
            else if (number % 1 != 0)
                hasFractionalDayOrLarger = true;

            operations.Add((number, unit));
        }

        // If fractional day+ units and no explicit time, assume noon as starting time
        if (hasFractionalDayOrLarger && !hasInputTime && dateTime.TimeOfDay == TimeSpan.Zero)
            dateTime = dateTime.AddHours(12);

        // Apply all operations
        foreach ((double number, string unit) in operations)
            dateTime = ApplyDateTimeOffset(dateTime, number, unit);

        // Determine whether to include time in the output
        bool showTime = hasInputTime || hasTimeUnits ||
                       (hasFractionalDayOrLarger && dateTime.TimeOfDay != TimeSpan.Zero);

        result = FormatDateTimeResult(dateTime, showTime);
        return true;
    }

    /// <summary>
    /// Applies a numeric offset with a time unit to a DateTime.
    /// </summary>
    private static DateTime ApplyDateTimeOffset(DateTime dateTime, double number, string unit)
    {
        return unit switch
        {
            "decade" or "decades" => AddFractionalYears(dateTime, number * 10),
            "year" or "years" => AddFractionalYears(dateTime, number),
            "month" or "months" => AddFractionalMonths(dateTime, number),
            "week" or "weeks" => dateTime.AddDays(number * 7),
            "day" or "days" => dateTime.AddDays(number),
            "hour" or "hours" or "hr" or "hrs" => dateTime.AddHours(number),
            "minute" or "minutes" or "min" or "mins" => dateTime.AddMinutes(number),
            _ => dateTime
        };
    }

    private static DateTime AddFractionalYears(DateTime dateTime, double years)
    {
        int wholeYears = (int)years;
        double fraction = years - wholeYears;

        dateTime = dateTime.AddYears(wholeYears);
        if (Math.Abs(fraction) > double.Epsilon)
            dateTime = dateTime.AddDays(fraction * 365.25);

        return dateTime;
    }

    private static DateTime AddFractionalMonths(DateTime dateTime, double months)
    {
        int wholeMonths = (int)months;
        double fraction = months - wholeMonths;

        dateTime = dateTime.AddMonths(wholeMonths);
        if (Math.Abs(fraction) > double.Epsilon)
            dateTime = dateTime.AddDays(fraction * 30.44);

        return dateTime;
    }

    /// <summary>
    /// Attempts to parse a date string flexibly, supporting various formats
    /// including named months with ordinal suffixes, numeric dates, and special keywords.
    /// Uses the current system culture for parsing.
    /// </summary>
    private static bool TryParseFlexibleDate(string input, out DateTime dateTime, out bool hasTime)
    {
        dateTime = default;
        hasTime = false;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string cleaned = input.Trim();

        // Handle special keywords
        switch (cleaned.ToLowerInvariant())
        {
            case "today":
                dateTime = DateTime.Today;
                return true;
            case "now":
                dateTime = DateTime.Now;
                hasTime = true;
                return true;
            case "tomorrow":
                dateTime = DateTime.Today.AddDays(1);
                return true;
            case "yesterday":
                dateTime = DateTime.Today.AddDays(-1);
                return true;
        }

        // Remove ordinal suffixes (1st, 2nd, 3rd, 4th, etc.)
        cleaned = OrdinalSuffixPattern().Replace(cleaned, "$1");

        // Detect if input has a time component
        hasTime = HasTimeComponent(input);

        // Try parsing with current culture
        if (DateTime.TryParse(cleaned, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dateTime))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a string contains a time component (am/pm indicators or HH:mm patterns).
    /// </summary>
    private static bool HasTimeComponent(string input)
    {
        // Check for am/pm indicators (e.g., 10am, 10 am, 10a.m., 10pm)
        if (AmPmPattern().IsMatch(input))
            return true;

        // Check for colon-separated time (e.g., 11:02, 14:30)
        if (ColonTimePattern().IsMatch(input))
            return true;

        return false;
    }

    /// <summary>
    /// Formats a DateTime result for display.
    /// Uses the current culture's short date format for dates.
    /// When time is included, appends 12-hour time with lowercase am/pm.
    /// </summary>
    private static string FormatDateTimeResult(DateTime dateTime, bool includeTime)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;

        if (includeTime)
        {
            string datePart = dateTime.ToString("d", culture);
            string timePart = dateTime.ToString("h:mmtt", culture).ToLowerInvariant();
            return $"{datePart} {timePart}";
        }

        return dateTime.ToString("d", culture);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"(?<op>[+-])\s*(?<number>\d+\.?\d*)\s*(?<unit>decades?|years?|months?|weeks?|days?|hours?|hrs?|hr|minutes?|mins?|min)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex DateTimeArithmeticPattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"(?<op>[+-])?\s*(?<number>\d+\.?\d*)\s*(?<unit>decades?|years?|months?|weeks?|days?|hours?|hrs?|hr|minutes?|mins?|min)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex DateTimeDurationSegmentPattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"(\d+)(?:st|nd|rd|th)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex OrdinalSuffixPattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"\d\s*[aApP]\.?[mM]\.?(?:\s|$|[^a-zA-Z])")]
    private static partial System.Text.RegularExpressions.Regex AmPmPattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"\d{1,2}:\d{2}")]
    private static partial System.Text.RegularExpressions.Regex ColonTimePattern();
}
