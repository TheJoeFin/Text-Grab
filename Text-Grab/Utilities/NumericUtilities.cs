using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Text_Grab.Utilities;

public static class NumericUtilities
{
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
        if (absValue >= 1e15 || (absValue < 1e-4 && absValue > 0))
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
}
