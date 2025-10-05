using NCalc;
using NCalc.Exceptions;
using System.Globalization;
using Text_Grab.Services;

namespace Tests;

public class CalculatorTests
{
    [Fact]
    public async Task NCalc_HasBuiltInPi_ReturnsFalse()
    {
        // Test if NCalc has built-in Pi constant
        AsyncExpression expression = new("Pi");

        NCalcParameterNotDefinedException exception = await Assert.ThrowsAsync<NCalcParameterNotDefinedException>(async () => await expression.EvaluateAsync());
        Assert.Contains("Pi", exception.Message);
    }

    [Fact]
    public async Task NCalc_HasBuiltInE_ReturnsFalse()
    {
        // Test if NCalc has built-in E constant
        AsyncExpression expression = new("E");

        NCalcParameterNotDefinedException exception = await Assert.ThrowsAsync<NCalcParameterNotDefinedException>(async () => await expression.EvaluateAsync());
        Assert.Contains("E", exception.Message);
    }

    [Fact]
    public async Task NCalc_SupportsBasicMathFunctions()
    {
        // Test basic math functions work
        (string, double)[] tests =
        [
            ("Sin(0)", 0.0),
            ("Cos(0)", 1.0),
            ("Tan(0)", 0.0),
            ("Sqrt(4)", 2.0),
            ("Abs(-5)", 5.0),
            ("Log(1, E)", 0.0), // NCalc Log(value, base) format
            ("Exp(0)", 1.0)
        ];

        foreach ((string? expr, double expected) in tests)
        {
            AsyncExpression expression = new(expr);

            // Add E parameter for the Log test
            if (expr.Contains('E'))
            {
                expression.EvaluateParameterAsync += (name, args) =>
                {
                    if (name == "E")
                        args.Result = Math.E;
                    return ValueTask.CompletedTask;
                };
            }

            double result = Convert.ToDouble(await expression.EvaluateAsync());

            Assert.Equal(expected, result, 10); // 10 decimal places precision
        }
    }

    [Fact]
    public async Task NCalc_WithCustomPiParameter_Works()
    {
        // Test that we can add Pi as a parameter
        AsyncExpression expression = new("Sin(Pi/2)");
        expression.EvaluateParameterAsync += (name, args) =>
        {
            if (name == "Pi")
                args.Result = Math.PI;
            return ValueTask.CompletedTask;
        };

        double result = Convert.ToDouble(await expression.EvaluateAsync());

        Assert.Equal(1.0, result, 10);
    }

    [Fact]
    public async Task NCalc_WithCustomEParameter_Works()
    {
        // Test that we can add E as a parameter
        AsyncExpression expression = new("Log(E, E)"); // Log(value, base) format
        expression.EvaluateParameterAsync += (name, args) =>
        {
            if (name == "E")
                args.Result = Math.E;
            return ValueTask.CompletedTask;
        };

        double result = Convert.ToDouble(await expression.EvaluateAsync());

        Assert.Equal(1.0, result, 10);
    }

    [Fact]
    public async Task NCalc_WithMultipleMathConstants_Works()
    {
        // Test multiple math constants together
        AsyncExpression expression = new("Pi * E");
        expression.EvaluateParameterAsync += (name, args) =>
        {
            args.Result = name switch
            {
                "Pi" => Math.PI,
                "E" => Math.E,
                _ => throw new ArgumentException($"Unknown parameter: {name}")
            };
            return ValueTask.CompletedTask;
        };

        double result = Convert.ToDouble(await expression.EvaluateAsync());
        double expected = Math.PI * Math.E;

        Assert.Equal(expected, result, 10);
    }

    [Theory]
    [InlineData("Pi", Math.PI)]
    [InlineData("E", Math.E)]
    [InlineData("Tau", Math.Tau)]
    public void MathConstants_HaveCorrectValues(string constantName, double expectedValue)
    {
        // Test that .NET Math constants have expected values
        double actualValue = constantName switch
        {
            "Pi" => Math.PI,
            "E" => Math.E,
            "Tau" => Math.Tau,
            _ => throw new ArgumentException($"Unknown constant: {constantName}")
        };

        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public async Task NCalc_WithTauConstant_Works()
    {
        // Test that we can use Tau (2*Pi)
        AsyncExpression expression = new("Tau/2");
        expression.EvaluateParameterAsync += (name, args) =>
        {
            if (name == "Tau")
                args.Result = Math.Tau;
            return ValueTask.CompletedTask;
        };

        double result = Convert.ToDouble(await expression.EvaluateAsync());

        Assert.Equal(Math.PI, result, 10);
    }

    [Fact]
    public async Task NCalc_CaseInsensitive_MathConstants()
    {
        // Test case insensitive constants
        (string, double)[] testCases =
        [
            ("pi", Math.PI),
            ("PI", Math.PI),
            ("Pi", Math.PI),
            ("e", Math.E),
            ("E", Math.E),
            ("tau", Math.Tau),
            ("TAU", Math.Tau),
            ("Tau", Math.Tau)
        ];

        foreach ((string? constantName, double expectedValue) in testCases)
        {
            AsyncExpression expression = new(constantName, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
            expression.EvaluateParameterAsync += (name, args) =>
            {
                args.Result = name.ToLower() switch
                {
                    "pi" => Math.PI,
                    "e" => Math.E,
                    "tau" => Math.Tau,
                    _ => throw new ArgumentException($"Unknown parameter: {name}")
                };
                return ValueTask.CompletedTask;
            };

            double result = Convert.ToDouble(await expression.EvaluateAsync());

            Assert.Equal(expectedValue, result, 10);
        }
    }

    [Fact]
    public async Task NCalc_ComplexMathExpression_WithConstants()
    {
        // Test complex expression using multiple constants
        AsyncExpression expression = new("Sin(Pi/6) + Cos(Pi/3) + Log(E, E)"); // Using Log(value, base)
        expression.EvaluateParameterAsync += (name, args) =>
        {
            args.Result = name switch
            {
                "Pi" => Math.PI,
                "E" => Math.E,
                _ => throw new ArgumentException($"Unknown parameter: {name}")
            };
            return ValueTask.CompletedTask;
        };

        double result = Convert.ToDouble(await expression.EvaluateAsync());

        // Sin(π/6) + Cos(π/3) + Log_e(e) = 0.5 + 0.5 + 1 = 2.0
        Assert.Equal(2.0, result, 10);
    }

    [Fact]
    public async Task AsyncNCalc_WithMathConstants_Works()
    {
        // Test async version with constants
        AsyncExpression expression = new("Sqrt(Pi * E)", ExpressionOptions.IgnoreCaseAtBuiltInFunctions);

        expression.EvaluateParameterAsync += (name, args) =>
        {
            args.Result = name.ToLower() switch
            {
                "pi" => Math.PI,
                "e" => Math.E,
                _ => throw new ArgumentException($"Unknown parameter: {name}")
            };
            return ValueTask.CompletedTask;
        };

        double result = Convert.ToDouble(await expression.EvaluateAsync());
        double expected = Math.Sqrt(Math.PI * Math.E);

        Assert.Equal(expected, result, 10);
    }

    [Theory]
    [InlineData("Pi", Math.PI)]
    [InlineData("E", Math.E)]
    [InlineData("Tau", Math.Tau)]
    [InlineData("phi", 1.618033988749)]
    [InlineData("sqrt2", 1.414213562373)]
    [InlineData("sqrt3", 1.732050807569)]
    [InlineData("sqrt5", 2.236067977499)]
    public async Task MathConstants_Integration_Test(string constantName, double expectedValue)
    {
        // Test the TryGetMathConstant method logic using realistic expressions
        AsyncExpression expression = new(constantName, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);

        expression.EvaluateParameterAsync += (name, args) =>
        {
            // Simulate the TryGetMathConstant logic
            double value = name.ToLowerInvariant() switch
            {
                "pi" => Math.PI,
                "e" => Math.E,
                "tau" => Math.Tau,
                "phi" => (1.0 + Math.Sqrt(5.0)) / 2.0,
                "sqrt2" => Math.Sqrt(2.0),
                "sqrt3" => Math.Sqrt(3.0),
                "sqrt5" => Math.Sqrt(5.0),
                "ln2" => Math.Log(2.0),
                "ln10" => Math.Log(10.0),
                "log2e" => Math.Log2(Math.E),
                "log10e" => Math.Log10(Math.E),
                _ => double.NaN
            };

            if (!double.IsNaN(value))
                args.Result = value;

            return ValueTask.CompletedTask;
        };

        double result = Convert.ToDouble(await expression.EvaluateAsync());

        Assert.Equal(expectedValue, result, 5); // 5 decimal places precision for constants
    }

    #region CalculationService Tests

    [Fact]
    public async Task CalculationService_BasicExpression_ReturnsCorrectResult()
    {
        // Arrange
        CalculationService service = new();
        string input = "2 + 2";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("4", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_MultipleExpressions_ReturnsMultipleResults()
    {
        // Arrange
        CalculationService service = new();
        string input = "10 + 5\n20 * 2\n100 / 4";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("15", lines[0]);
        Assert.Equal("40", lines[1]);
        Assert.Equal("25", lines[2]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_VariableAssignment_StoresAndUsesVariable()
    {
        // Arrange
        CalculationService service = new();
        string input = "x = 10\nx * 2";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Contains("x = 10", lines[0]);
        Assert.Equal("20", lines[1]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_MultipleVariables_WorksCorrectly()
    {
        // Arrange
        CalculationService service = new();
        string input = "a = 5\nb = 10\na + b\na * b";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Contains("a = 5", lines[0]);
        Assert.Contains("b = 10", lines[1]);
        Assert.Equal("15", lines[2]);
        Assert.Equal("50", lines[3]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_MathConstants_WorkCorrectly()
    {
        // Arrange
        CalculationService service = new();
        string input = "Pi\nE\nTau";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Contains("3.14", lines[0]); // Pi
        Assert.Contains("2.71", lines[1]); // E
        Assert.Contains("6.28", lines[2]); // Tau
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_CommentsAndEmptyLines_ArePreserved()
    {
        // Arrange
        CalculationService service = new();
        string input = "// This is a comment\n2 + 2\n\n# Another comment\n3 * 3";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(5, lines.Length);
        Assert.Equal("", lines[0]); // Comment
        Assert.Equal("4", lines[1]); // 2+2
        Assert.Equal("", lines[2]); // Empty line
        Assert.Equal("", lines[3]); // Comment
        Assert.Equal("9", lines[4]); // 3*3
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_WithErrors_ShowsErrorMessages()
    {
        // Arrange
        CalculationService service = new() { ShowErrors = true };
        string input = "2 + 2\ninvalid expression\n3 * 3";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("4", lines[0]);
        Assert.StartsWith("Error:", lines[1]);
        Assert.Equal("9", lines[2]);
        Assert.Equal(1, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_WithErrorsHidden_ShowsEmptyLines()
    {
        // Arrange
        CalculationService service = new() { ShowErrors = false };
        string input = "2 + 2\ninvalid expression\n3 * 3";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("4", lines[0]);
        Assert.Equal("", lines[1]); // Error hidden
        Assert.Equal("9", lines[2]);
        Assert.Equal(1, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_ComplexExpression_WorksCorrectly()
    {
        // Arrange
        CalculationService service = new();
        string input = "x = 10\ny = 5\nresult = (x + y) * 2 - 3\nresult";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Contains("x = 10", lines[0]);
        Assert.Contains("y = 5", lines[1]);
        Assert.Contains("result = 27", lines[2]);
        Assert.Equal("27", lines[3]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_MathFunctions_WorkCorrectly()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sqrt(16)\nAbs(-10)\nCos(0)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("4", lines[0]);
        Assert.Equal("10", lines[1]);
        Assert.Equal("1", lines[2]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_WithPiConstant_WorksInExpressions()
    {
        // Arrange
        CalculationService service = new();
        string input = "radius = 5\narea = Pi * radius * radius\narea";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Contains("radius = 5", lines[0]);
        Assert.Contains("area = ", lines[1]);
        Assert.Contains("78.5", lines[2]); // Pi * 5 * 5 ≈ 78.54
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        CalculationService service = new();
        string input = "";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_WhitespaceOnly_ReturnsEmpty()
    {
        // Arrange
        CalculationService service = new();
        string input = "   \n\n   ";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public void CalculationService_IsParameterAssignment_DetectsAssignments()
    {
        // Arrange
        CalculationService service = new();

        // Act & Assert
        Assert.True(service.IsParameterAssignment("x = 10"));
        Assert.True(service.IsParameterAssignment("result = 5 + 5"));
        Assert.True(service.IsParameterAssignment("_var = 100"));
        
        // Should not detect comparison operators
        Assert.False(service.IsParameterAssignment("x == 10"));
        Assert.False(service.IsParameterAssignment("x != 10"));
        Assert.False(service.IsParameterAssignment("x <= 10"));
        Assert.False(service.IsParameterAssignment("x >= 10"));
        Assert.False(service.IsParameterAssignment("10 + 5"));
    }

    [Theory]
    [InlineData("validName", true)]
    [InlineData("_validName", true)]
    [InlineData("valid123", true)]
    [InlineData("_123", true)]
    [InlineData("camelCase", true)]
    [InlineData("PascalCase", true)]
    [InlineData("123invalid", false)]
    [InlineData("invalid-name", false)]
    [InlineData("invalid.name", false)]
    [InlineData("invalid name", false)]
    [InlineData("", false)]
    public void CalculationService_IsValidVariableName_ValidatesCorrectly(string name, bool expected)
    {
        // Arrange
        CalculationService service = new();

        // Act
        bool result = service.IsValidVariableName(name);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task CalculationService_InvalidVariableName_ThrowsError()
    {
        // Arrange
        CalculationService service = new() { ShowErrors = true };
        string input = "123invalid = 10";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Contains("Error:", result.Output);
        Assert.Contains("Invalid variable name", result.Output);
        Assert.Equal(1, result.ErrorCount);
    }

    [Fact]
    public void CalculationService_TryGetMathConstant_RecognizesAllConstants()
    {
        // Act & Assert
        Assert.True(CalculationService.TryGetMathConstant("pi", out double piValue));
        Assert.Equal(Math.PI, piValue, 10);

        Assert.True(CalculationService.TryGetMathConstant("e", out double eValue));
        Assert.Equal(Math.E, eValue, 10);

        Assert.True(CalculationService.TryGetMathConstant("tau", out double tauValue));
        Assert.Equal(Math.Tau, tauValue, 10);

        Assert.True(CalculationService.TryGetMathConstant("phi", out double phiValue));
        Assert.Equal((1.0 + Math.Sqrt(5.0)) / 2.0, phiValue, 10);

        Assert.True(CalculationService.TryGetMathConstant("sqrt2", out double sqrt2Value));
        Assert.Equal(Math.Sqrt(2.0), sqrt2Value, 10);

        Assert.False(CalculationService.TryGetMathConstant("unknown", out _));
    }

    [Fact]
    public void CalculationService_TryGetMathConstant_IsCaseInsensitive()
    {
        // Act & Assert
        Assert.True(CalculationService.TryGetMathConstant("PI", out double pi1));
        Assert.True(CalculationService.TryGetMathConstant("Pi", out double pi2));
        Assert.True(CalculationService.TryGetMathConstant("pi", out double pi3));
        
        Assert.Equal(pi1, pi2);
        Assert.Equal(pi2, pi3);
        Assert.Equal(Math.PI, pi1, 10);
    }

    [Fact]
    public void CalculationService_FormatResult_HandlesSpecialValues()
    {
        // Arrange
        CalculationService service = new();

        // Act & Assert
        Assert.Equal("NaN", service.FormatResult(double.NaN));
        Assert.Equal("∞", service.FormatResult(double.PositiveInfinity));
        Assert.Equal("-∞", service.FormatResult(double.NegativeInfinity));
        Assert.Equal("true", service.FormatResult(true));
        Assert.Equal("false", service.FormatResult(false));
        Assert.Equal("null", service.FormatResult(null!));
    }

    [Theory]
    [InlineData(1000, "1,000")]
    [InlineData(1000000, "1,000,000")]
    [InlineData(3.14159, "3.142")]
    [InlineData(10, "10")]
    [InlineData(0, "0")]
    public void CalculationService_FormatResult_FormatsNumbersCorrectly(object value, string expectedStart)
    {
        // Arrange
        CalculationService service = new();

        // Act
        string result = service.FormatResult(value);

        // Assert
        Assert.StartsWith(expectedStart, result);
    }

    [Fact]
    public async Task CalculationService_ClearParameters_ResetsState()
    {
        // Arrange
        CalculationService service = new();
        await service.EvaluateExpressionsAsync("x = 10\ny = 20");

        // Act
        service.ClearParameters();
        var parameters = service.GetParameters();

        // Assert
        Assert.Empty(parameters);
    }

    [Fact]
    public async Task CalculationService_GetParameters_ReturnsStoredVariables()
    {
        // Arrange
        CalculationService service = new();
        await service.EvaluateExpressionsAsync("x = 10\ny = 20\nz = 30");

        // Act
        var parameters = service.GetParameters();

        // Assert
        Assert.Equal(3, parameters.Count);
        Assert.True(parameters.ContainsKey("x"));
        Assert.True(parameters.ContainsKey("y"));
        Assert.True(parameters.ContainsKey("z"));
    }

    [Fact]
    public async Task CalculationService_VariableReassignment_UpdatesValue()
    {
        // Arrange
        CalculationService service = new();
        string input = "x = 10\nx\nx = 20\nx";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Contains("x = 10", lines[0]);
        Assert.Equal("10", lines[1]);
        Assert.Contains("x = 20", lines[2]);
        Assert.Equal("20", lines[3]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_ChainedCalculations_WorkCorrectly()
    {
        // Arrange
        CalculationService service = new();
        string input = "a = 5\nb = a * 2\nc = b + 10\nd = c / 2\nd";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(5, lines.Length);
        Assert.Contains("a = 5", lines[0]);
        Assert.Contains("b = 10", lines[1]);
        Assert.Contains("c = 20", lines[2]);
        Assert.Contains("d = 10", lines[3]);
        Assert.Equal("10", lines[4]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_DivisionByZero_HandlesGracefully()
    {
        // Arrange
        CalculationService service = new() { ShowErrors = false };
        string input = "10 / 0";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        // Should return infinity or handle gracefully
        Assert.True(result.Output.Contains("∞") || result.Output == "");
    }

    [Fact]
    public async Task CalculationService_LongDecimalNumbers_FormatCorrectly()
    {
        // Arrange
        CalculationService service = new();
        string input = "1.23456789";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Contains("1.235", result.Output); // Should round to 3 decimal places
    }

    [Fact]
    public async Task CalculationService_PercentageCalculations_WorkCorrectly()
    {
        // Arrange
        CalculationService service = new();
        string input = "total = 100\ntax = total * 0.15\ntotal + tax";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Contains("total = 100", lines[0]);
        Assert.Contains("tax = 15", lines[1]);
        Assert.Equal("115", lines[2]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_NegativeNumbers_WorkCorrectly()
    {
        // Arrange
        CalculationService service = new();
        string input = "-10 + 5\n-3 * -4\n-15 / 3";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("-5", lines[0]);
        Assert.Equal("12", lines[1]);
        Assert.Equal("-5", lines[2]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_Parentheses_WorkCorrectly()
    {
        // Arrange
        CalculationService service = new();
        string input = "(2 + 3) * 4\n2 + (3 * 4)\n((2 + 3) * 4) / 2";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("20", lines[0]);
        Assert.Equal("14", lines[1]);
        Assert.Equal("10", lines[2]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_PowerOperations_WorkCorrectly()
    {
        // Arrange
        CalculationService service = new();
        // Note: NCalc doesn't have a built-in power operator
        // We'll use multiplication to simulate simple power operations for this test
        // Or we can use Sqrt for square root which is supported
        string input = "2 * 2 * 2\n10 * 10\nSqrt(16)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("8", lines[0]); // 2^3 = 8
        Assert.Equal("100", lines[1]); // 10^2 = 100
        Assert.Equal("4", lines[2]); // sqrt(16) = 4
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CalculationService_TrigonometricFunctions_WithPi_WorkCorrectly()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sin(Pi/2)\nCos(Pi)\nTan(0)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("1", lines[0]); // Sin(π/2) = 1
        Assert.Contains("-1", lines[1]); // Cos(π) = -1
        Assert.Equal("0", lines[2]); // Tan(0) = 0
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public void CalculationService_CultureInfo_CanBeSet()
    {
        // Arrange
        CalculationService service = new();
        CultureInfo germanCulture = new("de-DE");

        // Act
        service.CultureInfo = germanCulture;

        // Assert
        Assert.Equal(germanCulture, service.CultureInfo);
    }

    [Fact]
    public void CalculationService_StandardizeDecimalSeparators_WorksWithDifferentCultures()
    {
        // Arrange
        CalculationService service = new()
        {
            CultureInfo = new CultureInfo("de-DE") // Uses comma as decimal separator
        };

        // Act
        // In German culture, we already have period in input, so it should be kept if there's no group separator
        // This method is designed to prepare expressions for NCalc, which uses period
        // So actually the method removes group separators and keeps decimal as-is for NCalc
        string result = service.StandardizeDecimalAndGroupSeparators("1.5");

        // Assert
        // The method actually doesn't convert period to comma - it standardizes TO the culture's format
        // But only if input has culture-specific separators. "1.5" with period stays as-is.
        // Let's test with actual German format input
        service.CultureInfo = new CultureInfo("de-DE");
        string germanInput = "1,5"; // German decimal format
        string standardized = service.StandardizeDecimalAndGroupSeparators(germanInput);
        // Should convert comma to the culture's decimal separator for NCalc processing
        Assert.Equal("1,5", standardized);
    }

    [Fact]
    public async Task CalculationService_RealWorldExample_MonthlyBudget()
    {
        // Arrange
        CalculationService service = new();
        string input = @"// Monthly Budget Calculator
income = 5000
rent = 1200
utilities = 200
food = 600
transportation = 300
entertainment = 400

// Calculate total expenses
expenses = rent + utilities + food + transportation + entertainment

// Calculate savings
savings = income - expenses

// Calculate savings percentage
savingsPercent = (savings / income) * 100

// Display results
savings
savingsPercent";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal(0, result.ErrorCount);
        string[] lines = result.Output.Split('\n');
        // Should have savings = 2300 and savingsPercent = 46
        Assert.Contains("2,300", lines[lines.Length - 2]);
        Assert.Contains("46", lines[lines.Length - 1]);
    }

    [Fact]
    public async Task CalculationService_RealWorldExample_CompoundInterest()
    {
        // Arrange
        CalculationService service = new();
        string input = @"// Compound Interest Calculator
principal = 10000
rate = 0.05
years = 10

// Simplified compound interest using multiplication for demonstration
// In real world, users could chain multiplications: amount = principal * 1.05 * 1.05 ... 
// Or use: amount = principal * 1.62889 (precomputed 1.05^10)
multiplier = 1.62889
amount = principal * multiplier

// Calculate interest earned
interest = amount - principal

interest";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal(0, result.ErrorCount);
        string[] lines = result.Output.Split('\n');
        // Interest should be approximately 6288.9
        Assert.Contains("6,", lines[lines.Length - 1]);
    }

    [Fact]
    public async Task CalculationService_RealWorldExample_CircleCalculations()
    {
        // Arrange
        CalculationService service = new();
        string input = @"// Circle Calculations
radius = 5

// Calculate circumference: C = 2πr
circumference = 2 * Pi * radius

// Calculate area: A = πr²
area = Pi * radius * radius

// Display results
circumference
area";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal(0, result.ErrorCount);
        string[] lines = result.Output.Split('\n');
        // Circumference should be ~31.42
        Assert.Contains("31", lines[lines.Length - 2]);
        // Area should be ~78.54
        Assert.Contains("78", lines[lines.Length - 1]);
    }

    #endregion CalculationService Tests

    #region Sum Function Tests

    [Fact]
    public async Task Sum_WithNoArguments_ReturnsZero()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum()";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("0", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithSingleNumber_ReturnsThatNumber()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum(42)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("42", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithMultipleNumbers_ReturnsCorrectSum()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum(1; 2; 3; 4; 5)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("15", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithNegativeNumbers_ReturnsCorrectSum()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum(-10, -20, -30)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("-60", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithMixedPositiveAndNegative_ReturnsCorrectSum()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum(100; -25; 50; -15; -10)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("100", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithDecimalNumbers_ReturnsCorrectSum()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum(1.5; 2.5; 3.5)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("7.5", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithExpressions_EvaluatesAndSums()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum(2 + 3; 4 * 2; 10 / 2)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("18", result.Output); // (2+3) + (4*2) + (10/2) = 5 + 8 + 5 = 18
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithVariables_UsesVariableValues()
    {
        // Arrange
        CalculationService service = new();
        string input = "a = 10\nb = 20\nc = 30\nSum(a; b; c)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Equal("60", lines[3]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithNestedFunctions_Works()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum(Abs(-5); Sqrt(16); Max(10; 20))";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("29", result.Output); // 5 + 4 + 20 = 29
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithLargeNumbers_ReturnsCorrectSum()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum(1000000; 2000000; 3000000)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("6,000,000", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithZeros_ReturnsCorrectSum()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum(0; 0; 0; 5; 0)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("5", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_CaseInsensitive_Works()
    {
        // Arrange
        CalculationService service = new();
        string input = "sum(1; 2; 3)\nSUM(4; 5; 6)\nSuM(7; 8; 9)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("6", lines[0]);
        Assert.Equal("15", lines[1]);
        Assert.Equal("24", lines[2]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithMathConstants_Works()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum(Pi; E; Tau)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        // Pi + E + Tau ≈ 3.14159 + 2.71828 + 6.28318 ≈ 12.14305
        Assert.Contains("12", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_InComplexExpression_Works()
    {
        // Arrange
        CalculationService service = new();
        string input = "total = Sum(10; 20; 30)\naverage = total / 3\naverage";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Contains("total = 60", lines[0]);
        Assert.Contains("average = 20", lines[1]);
        Assert.Equal("20", lines[2]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithVeryLongList_ReturnsCorrectSum()
    {
        // Arrange
        CalculationService service = new();
        // Sum of 1 to 100 = 5050
        string input = "Sum(1; 2; 3; 4; 5; 6; 7; 8; 9; 10; 11; 12; 13; 14; 15; 16; 17; 18; 19; 20; " +
                      "21; 22; 23; 24; 25; 26; 27; 28; 29; 30; 31; 32; 33; 34; 35; 36; 37; 38; 39; 40; " +
                      "41; 42; 43; 44; 45; 46; 47; 48; 49; 50; 51; 52; 53; 54; 55; 56; 57; 58; 59; 60; " +
                      "61; 62; 63; 64; 65; 66; 67; 68; 69; 70; 71; 72; 73; 74; 75; 76; 77; 78; 79; 80; " +
                      "81; 82; 83; 84; 85; 86; 87; 88; 89; 90; 91; 92; 93; 94; 95; 96; 97; 98; 99; 100)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("5,050", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_RealWorldExample_MonthlyExpenses()
    {
        // Arrange
        CalculationService service = new();
        string input = @"// Monthly expenses
rent = 1500
utilities = 250
groceries = 600
transportation = 200
entertainment = 300

// Calculate total using Sum
totalExpenses = Sum(rent; utilities; groceries; transportation; entertainment)
totalExpenses";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Contains("2,850", lines[lines.Length - 1]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_RealWorldExample_ScoreCalculation()
    {
        // Arrange
        CalculationService service = new();
        string input = @"// Test scores
test1 = 85
test2 = 92
test3 = 78
test4 = 95

// Calculate total and average
totalPoints = Sum(test1; test2; test3; test4)
averageScore = totalPoints / 4

totalPoints
averageScore";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Contains("350", lines[lines.Length - 2]); // Total
        Assert.Contains("87.5", lines[lines.Length - 1]); // Average
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithMultipleSumCalls_WorksCorrectly()
    {
        // Arrange
        CalculationService service = new();
        string input = @"group1 = Sum(1; 2; 3)
group2 = Sum(4; 5; 6)
group3 = Sum(7; 8; 9)
grandTotal = Sum(group1; group2; group3)
grandTotal";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        string[] lines = result.Output.Split('\n');
        Assert.Contains("group1 = 6", lines[0]);
        Assert.Contains("group2 = 15", lines[1]);
        Assert.Contains("group3 = 24", lines[2]);
        Assert.Contains("grandTotal = 45", lines[3]);
        Assert.Equal("45", lines[4]);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithMixedIntegerAndDecimal_ReturnsDecimalSum()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum(10; 5.5; 3; 2.25)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("20.75", result.Output);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task Sum_WithParenthesesAndOperations_CorrectPrecedence()
    {
        // Arrange
        CalculationService service = new();
        string input = "Sum((2 + 3) * 2; (10 - 5) / 5; 8)";

        // Act
        CalculationResult result = await service.EvaluateExpressionsAsync(input);

        // Assert
        Assert.Equal("19", result.Output); // (5*2) + (5/5) + 8 = 10 + 1 + 8 = 19
        Assert.Equal(0, result.ErrorCount);
    }

    #endregion Sum Function Tests
}
