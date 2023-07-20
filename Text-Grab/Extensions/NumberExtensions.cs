using System;
using System.Collections.Generic;

namespace Text_Grab;

public static class NumberExtensions
{
    // https://stackoverflow.com/a/2878000/7438031 
    // Read on 1/27/2022
    public static double StdDev(this IEnumerable<double> values)
    {
        // ref: http://warrenseen.com/blog/2006/03/13/how-to-calculate-standard-deviation/
        double mean = 0.0;
        double sum = 0.0;
        double stdDev = 0.0;
        int n = 0;
        foreach (double val in values)
        {
            n++;
            double delta = val - mean;
            mean += delta / n;
            sum += delta * (val - mean);
        }
        if (1 < n)
            stdDev = Math.Sqrt(sum / (n - 1));

        return stdDev;
    }
}