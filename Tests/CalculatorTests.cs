using NCalc;
using NCalc.Exceptions;

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
}
