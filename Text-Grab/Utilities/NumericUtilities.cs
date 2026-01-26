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
        // Use the same formatting logic as the calculation service, with group separators
        if (Math.Abs(value) >= 1e15 || (Math.Abs(value) < 1e-4 && value != 0))
        {
            return value.ToString("E6", CultureInfo.CurrentCulture);
        }
        else if (value % 1 == 0 && Math.Abs(value) < 1e10)
        {
            return value.ToString("N0", CultureInfo.CurrentCulture);  // N0 includes group separators
        }
        else
        {
            return value.ToString("N", CultureInfo.CurrentCulture);  // N includes group separators and decimals
        }
    }

    public static bool AreClose(double a, double b, double epsilon = 0.25)
    {
        return Math.Abs(a - b) < epsilon;
    }
}
