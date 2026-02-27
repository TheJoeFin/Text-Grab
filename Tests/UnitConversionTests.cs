using System.Globalization;
using Text_Grab.Services;

namespace Tests;

public class UnitConversionTests
{
    private readonly CalculationService _service = new();

    #region Explicit Conversion Tests

    [Theory]
    [InlineData("5 miles to km", "km")]
    [InlineData("100 fahrenheit to celsius", "°C")]
    [InlineData("1 kg to pounds", "lb")]
    [InlineData("3.5 gallons to liters", "L")]
    [InlineData("60 mph to km/h", "km/h")]
    [InlineData("1 acre to sq m", "m²")]
    [InlineData("12 inches to feet", "ft")]
    [InlineData("1000 grams to kg", "kg")]
    [InlineData("50 celsius to fahrenheit", "°F")]
    [InlineData("1 nautical mile to km", "km")]
    public async Task ExplicitConversion_ContainsTargetUnit(string input, string expectedUnit)
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Contains(expectedUnit, result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Theory]
    [InlineData("5 miles to km", 8.047, 0.01)]
    [InlineData("1 kg to pounds", 2.205, 0.01)]
    [InlineData("100 fahrenheit to celsius", 37.778, 0.01)]
    [InlineData("0 celsius to fahrenheit", 32, 0.01)]
    [InlineData("1 foot to inches", 12, 0.01)]
    [InlineData("1 mile to feet", 5280, 1)]
    [InlineData("1 gallon to liters", 3.785, 0.01)]
    [InlineData("1 kg to grams", 1000, 0.01)]
    [InlineData("100 cm to meters", 1, 0.01)]
    [InlineData("1 tonne to kg", 1000, 0.01)]
    public async Task ExplicitConversion_CorrectNumericValue(string input, double expectedValue, double tolerance)
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Single(result.OutputNumbers);
        Assert.InRange(result.OutputNumbers[0], expectedValue - tolerance, expectedValue + tolerance);
    }

    [Theory]
    [InlineData("5 in to cm")]       // "in" is both inches and keyword — "to" takes priority
    [InlineData("10 ft to m")]
    [InlineData("3 yd to meters")]
    public async Task ExplicitConversion_WithShortAbbreviations(string input)
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(0, result.ErrorCount);
        Assert.Single(result.OutputNumbers);
    }

    [Fact]
    public async Task ExplicitConversion_InKeyword_Works()
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync("5 gallons in liters");

        Assert.Contains("L", result.Output);
        Assert.Equal(0, result.ErrorCount);
        Assert.Single(result.OutputNumbers);
        Assert.InRange(result.OutputNumbers[0], 18.9, 18.95);
    }

    [Fact]
    public async Task ExplicitConversion_ZeroValue_Works()
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync("0 km to miles");

        Assert.Contains("mi", result.Output);
        Assert.Single(result.OutputNumbers);
        Assert.Equal(0, result.OutputNumbers[0], 3);
    }

    [Fact]
    public async Task ExplicitConversion_NegativeValue_Works()
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync("-40 celsius to fahrenheit");

        Assert.Contains("°F", result.Output);
        Assert.Single(result.OutputNumbers);
        Assert.Equal(-40, result.OutputNumbers[0], 1);
    }

    [Fact]
    public async Task ExplicitConversion_IncompatibleTypes_FallsThrough()
    {
        // "5 kg to km" — mass to length should not convert
        CalculationResult result = await _service.EvaluateExpressionsAsync("5 kg to km");

        // Should not produce a clean unit result — falls through to NCalc (which will error)
        Assert.True(result.ErrorCount > 0 || !result.Output.Contains("km"));
    }

    #endregion Explicit Conversion Tests

    #region Continuation Conversion Tests

    [Fact]
    public async Task ContinuationConversion_ToKeyword()
    {
        string input = "5 miles\nto km";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        string[] lines = result.Output.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Contains("mi", lines[0]);
        Assert.Contains("km", lines[1]);
    }

    [Fact]
    public async Task ContinuationConversion_CorrectValue()
    {
        string input = "100 celsius\nto fahrenheit";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(2, result.OutputNumbers.Count);
        Assert.Equal(100, result.OutputNumbers[0], 1);
        Assert.Equal(212, result.OutputNumbers[1], 1);
    }

    [Fact]
    public async Task ContinuationConversion_ChainedConversions()
    {
        string input = "1 mile\nto km\nto meters";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(3, result.OutputNumbers.Count);
        // 1 mile → 1.609 km → 1609.34 m
        Assert.InRange(result.OutputNumbers[2], 1609, 1610);
    }

    #endregion Continuation Conversion Tests

    #region Operator Continuation Tests

    [Fact]
    public async Task OperatorContinuation_AddSameUnit()
    {
        string input = "5 km\n+ 3 km";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(2, result.OutputNumbers.Count);
        Assert.Equal(8, result.OutputNumbers[1], 1);
        Assert.Contains("km", result.Output.Split('\n')[1]);
    }

    [Fact]
    public async Task OperatorContinuation_SubtractSameUnit()
    {
        string input = "10 kg\n- 3 kg";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(2, result.OutputNumbers.Count);
        Assert.Equal(7, result.OutputNumbers[1], 1);
    }

    [Fact]
    public async Task OperatorContinuation_AddDifferentUnit_SameType()
    {
        string input = "5 km\n+ 3 miles";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(2, result.OutputNumbers.Count);
        // 3 miles ≈ 4.828 km, so 5 + 4.828 ≈ 9.828 km
        Assert.InRange(result.OutputNumbers[1], 9.8, 9.9);
        Assert.Contains("km", result.Output.Split('\n')[1]);
    }

    [Fact]
    public async Task ScaleOperator_Multiply()
    {
        string input = "5 km\n* 3";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(2, result.OutputNumbers.Count);
        Assert.Equal(15, result.OutputNumbers[1], 1);
        Assert.Contains("km", result.Output.Split('\n')[1]);
    }

    [Fact]
    public async Task ScaleOperator_Divide()
    {
        string input = "10 meters\n/ 2";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(2, result.OutputNumbers.Count);
        Assert.Equal(5, result.OutputNumbers[1], 1);
        Assert.Contains("m", result.Output.Split('\n')[1]);
    }

    [Fact]
    public async Task OperatorContinuation_ThenConvert()
    {
        string input = "5 km\n+ 3 km\nto miles";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(3, result.OutputNumbers.Count);
        // 8 km ≈ 4.971 miles
        Assert.InRange(result.OutputNumbers[2], 4.9, 5.0);
        Assert.Contains("mi", result.Output.Split('\n')[2]);
    }

    #endregion Operator Continuation Tests

    #region Standalone Unit Tests

    [Theory]
    [InlineData("5 meters", "m")]
    [InlineData("100 kg", "kg")]
    [InlineData("3.5 gallons", "gal")]
    [InlineData("10 miles", "mi")]
    [InlineData("25 mph", "mph")]
    public async Task StandaloneUnit_DetectedAndDisplayed(string input, string expectedAbbrev)
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Contains(expectedAbbrev, result.Output);
        Assert.Single(result.OutputNumbers);
        Assert.Equal(0, result.ErrorCount);
    }

    [Theory]
    [InlineData("5 meters", 5)]
    [InlineData("100 kg", 100)]
    [InlineData("3.5 gallons", 3.5)]
    public async Task StandaloneUnit_CorrectNumericValue(string input, double expected)
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Single(result.OutputNumbers);
        Assert.Equal(expected, result.OutputNumbers[0], 3);
    }

    #endregion Standalone Unit Tests

    #region Unit Category Tests

    [Theory]
    // Length
    [InlineData("1 meter to feet", "ft")]
    [InlineData("1 km to miles", "mi")]
    [InlineData("1 inch to cm", "cm")]
    [InlineData("1 yard to meters", "m")]
    [InlineData("1 nautical mile to km", "km")]
    // Mass
    [InlineData("1 kg to pounds", "lb")]
    [InlineData("1 ounce to grams", "g")]
    [InlineData("1 stone to kg", "kg")]
    [InlineData("1 ton to kg", "kg")]
    [InlineData("1 tonne to pounds", "lb")]
    // Temperature
    [InlineData("100 celsius to fahrenheit", "°F")]
    [InlineData("212 fahrenheit to celsius", "°C")]
    [InlineData("0 celsius to kelvin", "K")]
    // Volume
    [InlineData("1 gallon to liters", "L")]
    [InlineData("1 cup to mL", "mL")]
    [InlineData("1 tablespoon to teaspoons", "tsp")]
    [InlineData("1 pint to cups", "cup")]
    [InlineData("1 quart to pints", "pt")]
    [InlineData("1 fl oz to mL", "mL")]
    // Speed
    [InlineData("60 mph to km/h", "km/h")]
    [InlineData("100 km/h to mph", "mph")]
    [InlineData("1 m/s to km/h", "km/h")]
    [InlineData("1 knot to mph", "mph")]
    // Area
    [InlineData("1 acre to sq m", "m²")]
    [InlineData("1 hectare to acres", "ac")]
    [InlineData("1 sq mi to sq km", "km²")]
    [InlineData("1 sq ft to sq m", "m²")]
    public async Task AllUnitCategories_ConvertSuccessfully(string input, string expectedUnit)
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Contains(expectedUnit, result.Output);
        Assert.Equal(0, result.ErrorCount);
        Assert.Single(result.OutputNumbers);
    }

    #endregion Unit Category Tests

    #region Ambiguity & Edge Case Tests

    [Fact]
    public async Task VariableTakesPriorityOverUnit()
    {
        // When a variable "km" is defined, "5 km" should use the variable, not the unit
        string input = "km = 10\n5 * km";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        // 5 * 10 = 50 (not "5 km")
        Assert.Equal(2, result.OutputNumbers.Count);
        Assert.Equal(50, result.OutputNumbers[1], 1);
    }

    [Fact]
    public async Task QuantityWords_StillWork()
    {
        // "5 million" should still be handled by quantity words, not units
        CalculationResult result = await _service.EvaluateExpressionsAsync("5 million");

        Assert.Single(result.OutputNumbers);
        Assert.Equal(5_000_000, result.OutputNumbers[0], 0);
    }

    [Fact]
    public async Task DateTimeMath_TakesPriority()
    {
        // Date math should still work as before — "today + 5 days" is not a unit expression
        CalculationResult result = await _service.EvaluateExpressionsAsync("today + 5 days");

        // Should produce a date, not a unit result
        Assert.Equal(0, result.ErrorCount);
        Assert.DoesNotContain("days", result.Output.ToLowerInvariant().Split('\n')[0].Split("days")[0]);
    }

    [Fact]
    public async Task PlainNumbersStillWork()
    {
        // Regular math should be unaffected
        CalculationResult result = await _service.EvaluateExpressionsAsync("2 + 3");

        Assert.Single(result.OutputNumbers);
        Assert.Equal(5, result.OutputNumbers[0], 1);
    }

    [Fact]
    public async Task MultipleConversions_TracksOutputNumbers()
    {
        string input = "5 miles to km\n10 kg to pounds\n100 celsius to fahrenheit";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(3, result.OutputNumbers.Count);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task DominantUnit_SetCorrectly()
    {
        string input = "5 km\n+ 3 km\n+ 2 km";
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal("km", result.DominantUnit);
    }

    [Fact]
    public async Task DominantUnit_NullForPlainMath()
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync("2 + 3");

        Assert.Null(result.DominantUnit);
    }

    #endregion Ambiguity & Edge Case Tests

    #region TryEvaluateUnitConversion Direct Tests

    [Theory]
    [InlineData("5 miles to km", true)]
    [InlineData("100 fahrenheit to celsius", true)]
    [InlineData("2 + 3", false)]
    [InlineData("hello world", false)]
    [InlineData("x = 10", false)]
    [InlineData("", false)]
    public void TryEvaluateUnitConversion_DetectsCorrectly(string input, bool expected)
    {
        bool result = _service.TryEvaluateUnitConversion(
            input, out _, out _, null);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryEvaluateUnitConversion_ContinuationWithoutPrevious_ReturnsFalse()
    {
        // "to km" without a previous unit result should not match
        bool result = _service.TryEvaluateUnitConversion(
            "to km", out _, out _, null);

        Assert.False(result);
    }

    [Fact]
    public void TryEvaluateUnitConversion_ContinuationWithPrevious_ReturnsTrue()
    {
        var previous = new CalculationService.UnitResult
        {
            Value = 5,
            Unit = UnitsNet.Units.LengthUnit.Mile,
            QuantityName = "Length",
            Abbreviation = "mi"
        };

        bool result = _service.TryEvaluateUnitConversion(
            "to km", out string output, out _, previous);

        Assert.True(result);
        Assert.Contains("km", output);
    }

    #endregion TryEvaluateUnitConversion Direct Tests

    #region Plural & Alias Tests

    [Theory]
    [InlineData("1 meter to feet")]
    [InlineData("1 meters to feet")]
    [InlineData("1 m to ft")]
    [InlineData("1 foot to meters")]
    [InlineData("1 feet to meters")]
    [InlineData("1 ft to m")]
    public async Task UnitAliases_AllResolveCorrectly(string input)
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(0, result.ErrorCount);
        Assert.Single(result.OutputNumbers);
    }

    [Theory]
    [InlineData("1 liter to mL")]
    [InlineData("1 litre to mL")]
    [InlineData("1 liters to mL")]
    [InlineData("1 litres to mL")]
    public async Task BritishSpellings_Work(string input)
    {
        CalculationResult result = await _service.EvaluateExpressionsAsync(input);

        Assert.Equal(0, result.ErrorCount);
        Assert.Single(result.OutputNumbers);
        Assert.Equal(1000, result.OutputNumbers[0], 1);
    }

    #endregion Plural & Alias Tests
}
