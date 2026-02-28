using System;
using System.Collections.Generic;
using System.Globalization;
using UnitsNet;
using UnitsNet.Units;

namespace Text_Grab.Services;

public partial class CalculationService
{
    /// <summary>
    /// Stores information about a resolved measurement unit, mapping to a UnitsNet enum value.
    /// </summary>
    private readonly record struct UnitInfo(Enum Unit, string QuantityName, string Abbreviation);

    /// <summary>
    /// Represents the result of a unit-bearing evaluation for tracking across lines.
    /// Used for operator continuation (e.g., "5 km" then "+ 3 km" or "to miles").
    /// </summary>
    public class UnitResult
    {
        /// <summary>The numeric value in the current unit.</summary>
        public double Value { get; set; }

        /// <summary>The UnitsNet unit enum value (e.g., LengthUnit.Kilometer).</summary>
        public Enum Unit { get; set; } = default!;

        /// <summary>The quantity type name (e.g., "Length", "Mass").</summary>
        public string QuantityName { get; set; } = string.Empty;

        /// <summary>The display abbreviation (e.g., "km", "lb").</summary>
        public string Abbreviation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Maps common unit names and abbreviations to UnitsNet enum values.
    /// Keys are case-insensitive. Includes singular, plural, and abbreviation forms.
    /// </summary>
    private static readonly Dictionary<string, UnitInfo> _unitLookup =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // ══════════════════════════════════════════════════════════════
        // LENGTH
        // ══════════════════════════════════════════════════════════════
        { "m",               new(LengthUnit.Meter, "Length", "m") },
        { "meter",           new(LengthUnit.Meter, "Length", "m") },
        { "meters",          new(LengthUnit.Meter, "Length", "m") },
        { "cm",              new(LengthUnit.Centimeter, "Length", "cm") },
        { "centimeter",      new(LengthUnit.Centimeter, "Length", "cm") },
        { "centimeters",     new(LengthUnit.Centimeter, "Length", "cm") },
        { "mm",              new(LengthUnit.Millimeter, "Length", "mm") },
        { "millimeter",      new(LengthUnit.Millimeter, "Length", "mm") },
        { "millimeters",     new(LengthUnit.Millimeter, "Length", "mm") },
        { "km",              new(LengthUnit.Kilometer, "Length", "km") },
        { "kilometer",       new(LengthUnit.Kilometer, "Length", "km") },
        { "kilometers",      new(LengthUnit.Kilometer, "Length", "km") },
        { "in",              new(LengthUnit.Inch, "Length", "in") },
        { "inch",            new(LengthUnit.Inch, "Length", "in") },
        { "inches",          new(LengthUnit.Inch, "Length", "in") },
        { "ft",              new(LengthUnit.Foot, "Length", "ft") },
        { "foot",            new(LengthUnit.Foot, "Length", "ft") },
        { "feet",            new(LengthUnit.Foot, "Length", "ft") },
        { "yd",              new(LengthUnit.Yard, "Length", "yd") },
        { "yard",            new(LengthUnit.Yard, "Length", "yd") },
        { "yards",           new(LengthUnit.Yard, "Length", "yd") },
        { "mi",              new(LengthUnit.Mile, "Length", "mi") },
        { "mile",            new(LengthUnit.Mile, "Length", "mi") },
        { "miles",           new(LengthUnit.Mile, "Length", "mi") },
        { "nmi",             new(LengthUnit.NauticalMile, "Length", "nmi") },
        { "nautical mile",   new(LengthUnit.NauticalMile, "Length", "nmi") },
        { "nautical miles",  new(LengthUnit.NauticalMile, "Length", "nmi") },

        // ══════════════════════════════════════════════════════════════
        // MASS
        // ══════════════════════════════════════════════════════════════
        { "g",               new(MassUnit.Gram, "Mass", "g") },
        { "gram",            new(MassUnit.Gram, "Mass", "g") },
        { "grams",           new(MassUnit.Gram, "Mass", "g") },
        { "kg",              new(MassUnit.Kilogram, "Mass", "kg") },
        { "kilogram",        new(MassUnit.Kilogram, "Mass", "kg") },
        { "kilograms",       new(MassUnit.Kilogram, "Mass", "kg") },
        { "mg",              new(MassUnit.Milligram, "Mass", "mg") },
        { "milligram",       new(MassUnit.Milligram, "Mass", "mg") },
        { "milligrams",      new(MassUnit.Milligram, "Mass", "mg") },
        { "lb",              new(MassUnit.Pound, "Mass", "lb") },
        { "lbs",             new(MassUnit.Pound, "Mass", "lb") },
        { "pound",           new(MassUnit.Pound, "Mass", "lb") },
        { "pounds",          new(MassUnit.Pound, "Mass", "lb") },
        { "oz",              new(MassUnit.Ounce, "Mass", "oz") },
        { "ounce",           new(MassUnit.Ounce, "Mass", "oz") },
        { "ounces",          new(MassUnit.Ounce, "Mass", "oz") },
        { "ton",             new(MassUnit.ShortTon, "Mass", "short tn") },
        { "tons",            new(MassUnit.ShortTon, "Mass", "short tn") },
        { "short ton",       new(MassUnit.ShortTon, "Mass", "short tn") },
        { "short tons",      new(MassUnit.ShortTon, "Mass", "short tn") },
        { "tonne",           new(MassUnit.Tonne, "Mass", "t") },
        { "tonnes",          new(MassUnit.Tonne, "Mass", "t") },
        { "metric ton",      new(MassUnit.Tonne, "Mass", "t") },
        { "metric tons",     new(MassUnit.Tonne, "Mass", "t") },
        { "st",              new(MassUnit.Stone, "Mass", "st") },
        { "stone",           new(MassUnit.Stone, "Mass", "st") },
        { "stones",          new(MassUnit.Stone, "Mass", "st") },

        // ══════════════════════════════════════════════════════════════
        // TEMPERATURE
        // ══════════════════════════════════════════════════════════════
        { "celsius",         new(TemperatureUnit.DegreeCelsius, "Temperature", "°C") },
        { "°C",              new(TemperatureUnit.DegreeCelsius, "Temperature", "°C") },
        { "degC",            new(TemperatureUnit.DegreeCelsius, "Temperature", "°C") },
        { "C",               new(TemperatureUnit.DegreeCelsius, "Temperature", "°C") },
        { "fahrenheit",      new(TemperatureUnit.DegreeFahrenheit, "Temperature", "°F") },
        { "°F",              new(TemperatureUnit.DegreeFahrenheit, "Temperature", "°F") },
        { "degF",            new(TemperatureUnit.DegreeFahrenheit, "Temperature", "°F") },
        { "F",               new(TemperatureUnit.DegreeFahrenheit, "Temperature", "°F") },
        { "kelvin",          new(TemperatureUnit.Kelvin, "Temperature", "K") },

        // ══════════════════════════════════════════════════════════════
        // VOLUME
        // ══════════════════════════════════════════════════════════════
        { "liter",           new(VolumeUnit.Liter, "Volume", "L") },
        { "liters",          new(VolumeUnit.Liter, "Volume", "L") },
        { "litre",           new(VolumeUnit.Liter, "Volume", "L") },
        { "litres",          new(VolumeUnit.Liter, "Volume", "L") },
        { "L",               new(VolumeUnit.Liter, "Volume", "L") },
        { "mL",              new(VolumeUnit.Milliliter, "Volume", "mL") },
        { "milliliter",      new(VolumeUnit.Milliliter, "Volume", "mL") },
        { "milliliters",     new(VolumeUnit.Milliliter, "Volume", "mL") },
        { "millilitre",      new(VolumeUnit.Milliliter, "Volume", "mL") },
        { "millilitres",     new(VolumeUnit.Milliliter, "Volume", "mL") },
        { "gal",             new(VolumeUnit.UsGallon, "Volume", "gal") },
        { "gallon",          new(VolumeUnit.UsGallon, "Volume", "gal") },
        { "gallons",         new(VolumeUnit.UsGallon, "Volume", "gal") },
        { "qt",              new(VolumeUnit.UsQuart, "Volume", "qt") },
        { "quart",           new(VolumeUnit.UsQuart, "Volume", "qt") },
        { "quarts",          new(VolumeUnit.UsQuart, "Volume", "qt") },
        { "pt",              new(VolumeUnit.UsPint, "Volume", "pt") },
        { "pint",            new(VolumeUnit.UsPint, "Volume", "pt") },
        { "pints",           new(VolumeUnit.UsPint, "Volume", "pt") },
        { "cup",             new(VolumeUnit.UsCustomaryCup, "Volume", "cup") },
        { "cups",            new(VolumeUnit.UsCustomaryCup, "Volume", "cup") },
        { "fl oz",           new(VolumeUnit.UsOunce, "Volume", "fl oz") },
        { "floz",            new(VolumeUnit.UsOunce, "Volume", "fl oz") },
        { "fluid ounce",     new(VolumeUnit.UsOunce, "Volume", "fl oz") },
        { "fluid ounces",    new(VolumeUnit.UsOunce, "Volume", "fl oz") },
        { "tbsp",            new(VolumeUnit.UsTablespoon, "Volume", "tbsp") },
        { "tablespoon",      new(VolumeUnit.UsTablespoon, "Volume", "tbsp") },
        { "tablespoons",     new(VolumeUnit.UsTablespoon, "Volume", "tbsp") },
        { "tsp",             new(VolumeUnit.UsTeaspoon, "Volume", "tsp") },
        { "teaspoon",        new(VolumeUnit.UsTeaspoon, "Volume", "tsp") },
        { "teaspoons",       new(VolumeUnit.UsTeaspoon, "Volume", "tsp") },

        // ══════════════════════════════════════════════════════════════
        // SPEED
        // ══════════════════════════════════════════════════════════════
        { "mph",             new(SpeedUnit.MilePerHour, "Speed", "mph") },
        { "miles per hour",  new(SpeedUnit.MilePerHour, "Speed", "mph") },
        { "km/h",            new(SpeedUnit.KilometerPerHour, "Speed", "km/h") },
        { "kph",             new(SpeedUnit.KilometerPerHour, "Speed", "km/h") },
        { "kilometers per hour", new(SpeedUnit.KilometerPerHour, "Speed", "km/h") },
        { "m/s",             new(SpeedUnit.MeterPerSecond, "Speed", "m/s") },
        { "meters per second", new(SpeedUnit.MeterPerSecond, "Speed", "m/s") },
        { "knot",            new(SpeedUnit.Knot, "Speed", "kn") },
        { "knots",           new(SpeedUnit.Knot, "Speed", "kn") },
        { "kn",              new(SpeedUnit.Knot, "Speed", "kn") },

        // ══════════════════════════════════════════════════════════════
        // AREA
        // ══════════════════════════════════════════════════════════════
        { "m²",              new(AreaUnit.SquareMeter, "Area", "m²") },
        { "sq m",            new(AreaUnit.SquareMeter, "Area", "m²") },
        { "square meter",    new(AreaUnit.SquareMeter, "Area", "m²") },
        { "square meters",   new(AreaUnit.SquareMeter, "Area", "m²") },
        { "km²",             new(AreaUnit.SquareKilometer, "Area", "km²") },
        { "sq km",           new(AreaUnit.SquareKilometer, "Area", "km²") },
        { "square kilometer", new(AreaUnit.SquareKilometer, "Area", "km²") },
        { "square kilometers", new(AreaUnit.SquareKilometer, "Area", "km²") },
        { "ft²",             new(AreaUnit.SquareFoot, "Area", "ft²") },
        { "sq ft",           new(AreaUnit.SquareFoot, "Area", "ft²") },
        { "square foot",     new(AreaUnit.SquareFoot, "Area", "ft²") },
        { "square feet",     new(AreaUnit.SquareFoot, "Area", "ft²") },
        { "mi²",             new(AreaUnit.SquareMile, "Area", "mi²") },
        { "sq mi",           new(AreaUnit.SquareMile, "Area", "mi²") },
        { "square mile",     new(AreaUnit.SquareMile, "Area", "mi²") },
        { "square miles",    new(AreaUnit.SquareMile, "Area", "mi²") },
        { "in²",             new(AreaUnit.SquareInch, "Area", "in²") },
        { "sq in",           new(AreaUnit.SquareInch, "Area", "in²") },
        { "square inch",     new(AreaUnit.SquareInch, "Area", "in²") },
        { "square inches",   new(AreaUnit.SquareInch, "Area", "in²") },
        { "yd²",             new(AreaUnit.SquareYard, "Area", "yd²") },
        { "sq yd",           new(AreaUnit.SquareYard, "Area", "yd²") },
        { "square yard",     new(AreaUnit.SquareYard, "Area", "yd²") },
        { "square yards",    new(AreaUnit.SquareYard, "Area", "yd²") },
        { "cm²",             new(AreaUnit.SquareCentimeter, "Area", "cm²") },
        { "sq cm",           new(AreaUnit.SquareCentimeter, "Area", "cm²") },
        { "acre",            new(AreaUnit.Acre, "Area", "ac") },
        { "acres",           new(AreaUnit.Acre, "Area", "ac") },
        { "ac",              new(AreaUnit.Acre, "Area", "ac") },
        { "hectare",         new(AreaUnit.Hectare, "Area", "ha") },
        { "hectares",        new(AreaUnit.Hectare, "Area", "ha") },
        { "ha",              new(AreaUnit.Hectare, "Area", "ha") },
    };

    /// <summary>
    /// Attempts to evaluate a line as a unit conversion or unit-bearing expression.
    /// Supports patterns:
    /// <list type="bullet">
    ///   <item>Explicit conversion: "5 miles to km", "100°F in celsius"</item>
    ///   <item>Continuation conversion: "to km" (from previous unit result)</item>
    ///   <item>Operator with units: "+ 3 km", "- 5 miles" (from previous unit result)</item>
    ///   <item>Scale operator: "* 2", "/ 3" (from previous unit result, preserves unit)</item>
    ///   <item>Standalone unit: "5 meters" (tracked for future continuation)</item>
    /// </list>
    /// </summary>
    /// <param name="line">The input line to evaluate</param>
    /// <param name="result">The formatted display result if successful</param>
    /// <param name="unitResult">The parsed unit result for tracking across lines</param>
    /// <param name="previousUnitResult">The previous line's unit result for continuation</param>
    /// <returns>True if the line was successfully evaluated as a unit expression</returns>
    public bool TryEvaluateUnitConversion(
        string line,
        out string result,
        out UnitResult? unitResult,
        UnitResult? previousUnitResult)
    {
        result = string.Empty;
        unitResult = null;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        string trimmed = line.Trim();

        // 1. Continuation conversion: "to km" / "in feet" (requires previous unit result)
        if (previousUnitResult is not null
            && TryContinuationConversion(trimmed, previousUnitResult, out result, out unitResult))
            return true;

        // 2. Operator with unit: "+ 3 km", "- 5 miles" (requires previous unit result)
        if (previousUnitResult is not null
            && TryOperatorWithUnit(trimmed, previousUnitResult, out result, out unitResult))
            return true;

        // 3. Scale operator: "* 2", "/ 3" (requires previous unit result, preserves unit)
        if (previousUnitResult is not null
            && TryScaleOperator(trimmed, previousUnitResult, out result, out unitResult))
            return true;

        // 4. Explicit conversion: "5 miles to km", "100°F in celsius"
        if (TryExplicitConversion(trimmed, out result, out unitResult))
            return true;

        // 5. Standalone unit: "5 meters" (track for future continuation)
        if (TryStandaloneUnit(trimmed, out result, out unitResult))
            return true;

        return false;
    }

    #region Unit Conversion Helpers

    /// <summary>
    /// Handles "to km" or "in feet" when there is a previous unit result to convert from.
    /// </summary>
    private bool TryContinuationConversion(
        string trimmed,
        UnitResult previous,
        out string result,
        out UnitResult? unitResult)
    {
        result = string.Empty;
        unitResult = null;

        System.Text.RegularExpressions.Match match = ContinuationConversionPattern().Match(trimmed);
        if (!match.Success)
            return false;

        string targetStr = match.Groups[1].Value.Trim();
        if (!TryResolveUnit(targetStr, out UnitInfo target))
            return false;

        // Ensure compatible quantity types (e.g., both are Length)
        if (previous.Unit.GetType() != target.Unit.GetType())
            return false;

        try
        {
            IQuantity source = Quantity.From(previous.Value, previous.Unit);
            IQuantity converted = source.ToUnit(target.Unit);
            double convertedValue = (double)converted.Value;

            unitResult = new UnitResult
            {
                Value = convertedValue,
                Unit = target.Unit,
                QuantityName = target.QuantityName,
                Abbreviation = target.Abbreviation
            };
            result = FormatUnitValue(convertedValue, target.Abbreviation);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Handles "+ 3 km" or "- 5 miles" operator continuation with units.
    /// Converts the operand to the previous unit before adding/subtracting.
    /// </summary>
    private bool TryOperatorWithUnit(
        string trimmed,
        UnitResult previous,
        out string result,
        out UnitResult? unitResult)
    {
        result = string.Empty;
        unitResult = null;

        System.Text.RegularExpressions.Match match = OperatorWithUnitPattern().Match(trimmed);
        if (!match.Success)
            return false;

        string op = match.Groups["op"].Value;
        string numberStr = match.Groups["number"].Value;
        string unitStr = match.Groups["unit"].Value.Trim();

        if (!double.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
            return false;

        if (!TryResolveUnit(unitStr, out UnitInfo operandUnit))
            return false;

        // Must be same quantity type
        if (previous.Unit.GetType() != operandUnit.Unit.GetType())
            return false;

        try
        {
            // Convert operand to the previous result's unit
            double operandInPreviousUnit;
            if (operandUnit.Unit.Equals(previous.Unit))
            {
                operandInPreviousUnit = number;
            }
            else
            {
                IQuantity operandQuantity = Quantity.From(number, operandUnit.Unit);
                IQuantity converted = operandQuantity.ToUnit(previous.Unit);
                operandInPreviousUnit = (double)converted.Value;
            }

            double newValue = op == "+"
                ? previous.Value + operandInPreviousUnit
                : previous.Value - operandInPreviousUnit;

            unitResult = new UnitResult
            {
                Value = newValue,
                Unit = previous.Unit,
                QuantityName = previous.QuantityName,
                Abbreviation = previous.Abbreviation
            };
            result = FormatUnitValue(newValue, previous.Abbreviation);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Handles "* 2" or "/ 3" scaling operators that preserve the previous unit.
    /// </summary>
    private bool TryScaleOperator(
        string trimmed,
        UnitResult previous,
        out string result,
        out UnitResult? unitResult)
    {
        result = string.Empty;
        unitResult = null;

        System.Text.RegularExpressions.Match match = ScaleOperatorPattern().Match(trimmed);
        if (!match.Success)
            return false;

        string op = match.Groups["op"].Value;
        string numberStr = match.Groups["number"].Value;

        if (!double.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
            return false;

        if (op == "/" && number == 0)
            return false; // Avoid division by zero

        double newValue = op == "*"
            ? previous.Value * number
            : previous.Value / number;

        unitResult = new UnitResult
        {
            Value = newValue,
            Unit = previous.Unit,
            QuantityName = previous.QuantityName,
            Abbreviation = previous.Abbreviation
        };
        result = FormatUnitValue(newValue, previous.Abbreviation);
        return true;
    }

    /// <summary>
    /// Handles explicit "5 miles to km" or "100°F in celsius" conversion expressions.
    /// Tries the "to" keyword first (unambiguous), then "in" as fallback.
    /// </summary>
    private bool TryExplicitConversion(
        string trimmed,
        out string result,
        out UnitResult? unitResult)
    {
        result = string.Empty;
        unitResult = null;

        // Try "to" keyword first (unambiguous, avoids conflict with "in" as inches)
        System.Text.RegularExpressions.Match match = ToConversionPattern().Match(trimmed);
        if (!match.Success)
        {
            // Fallback to "in" keyword
            match = InConversionPattern().Match(trimmed);
        }

        if (!match.Success)
            return false;

        string sourcePart = match.Groups[1].Value.Trim();
        string targetStr = match.Groups[2].Value.Trim();

        if (!TryExtractValueAndUnit(sourcePart, out double value, out UnitInfo sourceUnit))
            return false;

        if (!TryResolveUnit(targetStr, out UnitInfo targetUnit))
            return false;

        // Ensure compatible quantity types
        if (sourceUnit.Unit.GetType() != targetUnit.Unit.GetType())
            return false;

        try
        {
            IQuantity source = Quantity.From(value, sourceUnit.Unit);
            IQuantity converted = source.ToUnit(targetUnit.Unit);
            double convertedValue = (double)converted.Value;

            unitResult = new UnitResult
            {
                Value = convertedValue,
                Unit = targetUnit.Unit,
                QuantityName = targetUnit.QuantityName,
                Abbreviation = targetUnit.Abbreviation
            };
            result = FormatUnitValue(convertedValue, targetUnit.Abbreviation);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Handles standalone unit expressions like "5 meters" — tracks the unit for future
    /// continuation but does not convert. Requires multi-character unit abbreviations
    /// to avoid conflicts with single-letter variable names and quantity words.
    /// </summary>
    private bool TryStandaloneUnit(
        string trimmed,
        out string result,
        out UnitResult? unitResult)
    {
        result = string.Empty;
        unitResult = null;

        if (!TryExtractValueAndUnit(trimmed, out double value, out UnitInfo unit))
            return false;

        // For standalone detection, skip single-char unit text that could conflict
        // with variable names or quantity words (e.g., "5 m" where m could be a variable).
        // Multi-char input like "meters" is unambiguous even if the abbreviation is "m".
        // Single-char units still work in explicit conversions with "to"/"in" keyword.
        System.Text.RegularExpressions.Match unitMatch = NumberWithUnitPattern().Match(trimmed);
        string inputUnitText = unitMatch.Success ? unitMatch.Groups["unit"].Value.Trim() : string.Empty;
        if (inputUnitText.Length <= 1)
            return false;

        // Don't capture if the unit text matches a defined variable name
        string[] words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
        {
            string unitText = string.Join(" ", words[1..]);
            if (_parameters.ContainsKey(unitText))
                return false;
        }

        unitResult = new UnitResult
        {
            Value = value,
            Unit = unit.Unit,
            QuantityName = unit.QuantityName,
            Abbreviation = unit.Abbreviation
        };
        result = FormatUnitValue(value, unit.Abbreviation);
        return true;
    }

    /// <summary>
    /// Extracts a numeric value and unit from a string like "5 miles", "100°F", "3.5 gallons".
    /// The number must appear at the beginning and the unit at the end.
    /// </summary>
    private static bool TryExtractValueAndUnit(string input, out double value, out UnitInfo unitInfo)
    {
        value = 0;
        unitInfo = default;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        System.Text.RegularExpressions.Match match = NumberWithUnitPattern().Match(input.Trim());
        if (!match.Success)
            return false;

        string numberStr = match.Groups["number"].Value;
        string unitStr = match.Groups["unit"].Value.Trim();

        if (!double.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            return false;

        return TryResolveUnit(unitStr, out unitInfo);
    }

    /// <summary>
    /// Looks up a unit string in the unit dictionary.
    /// </summary>
    private static bool TryResolveUnit(string unitString, out UnitInfo entry)
    {
        return _unitLookup.TryGetValue(unitString.Trim(), out entry);
    }

    /// <summary>
    /// Formats a numeric value with a unit abbreviation for display.
    /// Uses the standard FormatResult for the number portion.
    /// </summary>
    private string FormatUnitValue(double value, string abbreviation)
    {
        string formatted = FormatResult(value);
        return $"{formatted} {abbreviation}";
    }

    #endregion Unit Conversion Helpers

    #region Unit Math Regex Patterns

    /// <summary>
    /// Matches "to km" or "in feet" — continuation conversion from previous unit result.
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^(?:to|in)\s+(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex ContinuationConversionPattern();

    /// <summary>
    /// Matches "5 miles to km" — explicit conversion with "to" keyword.
    /// Uses lazy matching on the source to prefer the earliest split point.
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^(.+?)\s+to\s+(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex ToConversionPattern();

    /// <summary>
    /// Matches "5 gallons in liters" — explicit conversion with "in" keyword.
    /// Used as fallback after "to" pattern to avoid conflicts with "in" as inches.
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^(.+?)\s+in\s+(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex InConversionPattern();

    /// <summary>
    /// Matches a number followed by a unit: "5 miles", "100°F", "3.5 gallons".
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^(?<number>-?\d+\.?\d*)\s*(?<unit>.+)$")]
    private static partial System.Text.RegularExpressions.Regex NumberWithUnitPattern();

    /// <summary>
    /// Matches operator continuation with unit: "+ 3 km", "- 5 miles".
    /// Requires whitespace between operator, number, and unit.
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^(?<op>[+-])\s+(?<number>\d+\.?\d*)\s+(?<unit>.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex OperatorWithUnitPattern();

    /// <summary>
    /// Matches scale operators without units: "* 2", "/ 3", "* 0.5".
    /// Only used when there is a previous unit result to preserve.
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^(?<op>[*/])\s*(?<number>\d+\.?\d*)$")]
    private static partial System.Text.RegularExpressions.Regex ScaleOperatorPattern();

    #endregion Unit Math Regex Patterns
}
