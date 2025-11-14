using NCalc;
using NCalc.Parser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Text_Grab.Services;

/// <summary>
/// Service for evaluating mathematical expressions from text input.
/// Supports variable assignments, mathematical constants, and multi-line expressions.
/// </summary>
public class CalculationService
{
    private readonly Dictionary<string, object> _parameters = [];

    /// <summary>
    /// Gets the culture info used for number formatting and parsing.
    /// </summary>
    public CultureInfo CultureInfo { get; set; } = CultureInfo.CurrentCulture;

    /// <summary>
    /// Gets or sets whether to show error messages in results.
    /// </summary>
    public bool ShowErrors { get; set; } = true;

    /// <summary>
    /// Evaluates a multi-line text containing expressions and returns formatted results.
    /// </summary>
    /// <param name="input">The multi-line input text containing expressions</param>
    /// <returns>A calculation result containing the evaluated output and any errors</returns>
    public async Task<CalculationResult> EvaluateExpressionsAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            _parameters.Clear();
            return new CalculationResult { Output = string.Empty, ErrorCount = 0 };
        }

        string[] lines = input.Split('\n');
        List<string> results = [];
        int errorCount = 0;

        // Clear parameters and rebuild from scratch for each evaluation
        _parameters.Clear();

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("//") || trimmedLine.StartsWith('#'))
            {
                results.Add(""); // Preserve line structure for comments/empty lines
                continue;
            }

            try
            {
                if (IsParameterAssignment(trimmedLine))
                {
                    string resultLine = await HandleParameterAssignmentAsync(trimmedLine);
                    results.Add(resultLine);
                }
                else
                {
                    string resultLine = await EvaluateStandardExpressionAsync(trimmedLine);
                    results.Add(resultLine);
                }
            }
            catch (Exception ex)
            {
                if (ShowErrors)
                {
                    results.Add($"Error: {ex.Message}");
                }
                else
                {
                    results.Add(""); // Empty line when errors are hidden
                }
                errorCount++;
            }
        }

        return new CalculationResult
        {
            Output = string.Join("\n", results),
            ErrorCount = errorCount
        };
    }

    /// <summary>
    /// Checks if a line represents a parameter assignment (variable = expression).
    /// </summary>
    public bool IsParameterAssignment(string line)
    {
        // Check for assignment pattern: variable = expression
        // Avoid matching comparison operators (==, !=, <=, >=)
        return line.Contains('=') &&
               !line.Contains("==") &&
               !line.Contains("!=") &&
               !line.Contains("<=") &&
               !line.Contains(">=") &&
               line.IndexOf('=') == line.LastIndexOf('='); // Ensure single '='
    }

    /// <summary>
    /// Standardizes decimal and group separators based on the current culture.
    /// </summary>
    public string StandardizeDecimalAndGroupSeparators(string expression)
    {
        if (CultureInfo is null)
            return expression;

        string decimalSep = CultureInfo.NumberFormat.NumberDecimalSeparator;
        string groupSep = CultureInfo.NumberFormat.NumberGroupSeparator;

        if (!string.IsNullOrEmpty(groupSep))
            expression = expression.Replace(groupSep, "");
        if (decimalSep != ".")
            expression = expression.Replace(".", decimalSep);

        return expression;
    }

    /// <summary>
    /// Parses quantity words and abbreviations in expressions, converting them to numeric values.
    /// Examples: "5 million" -> "5000000.0", "3 dozen" -> "36", "2.5 k" -> "2500"
    /// </summary>
    /// <param name="expression">The expression to parse</param>
    /// <returns>The expression with quantity words replaced by numeric values</returns>
    public static string ParseQuantityWords(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return expression;

        // Dictionary of quantity words and their multipliers
        // Note: Using decimal for precision, max value is ~7.9 x 10^28
        Dictionary<string, decimal> quantityMultipliers = new(StringComparer.OrdinalIgnoreCase)
        {
            // Large orders of magnitude (within decimal range)
            { "octillion", 1_000_000_000_000_000_000_000_000_000M },              // 10^27
            { "septillion", 1_000_000_000_000_000_000_000_000M },                 // 10^24
            { "sextillion", 1_000_000_000_000_000_000_000M },                     // 10^21
            { "quintillion", 1_000_000_000_000_000_000M },                        // 10^18
            { "quadrillion", 1_000_000_000_000_000M },                            // 10^15
            { "trillion", 1_000_000_000_000M },                                   // 10^12
            { "billion", 1_000_000_000M },                                        // 10^9
            { "million", 1_000_000M },                                            // 10^6
            { "thousand", 1_000M },                                               // 10^3
            { "hundred", 100M },                                                  // 10^2
            // Special quantities
            { "dozen", 12M },
            { "score", 20M },
            { "gross", 144M },
            // Abbreviations
            { "k", 1_000M },
            { "m", 1_000_000M },
            { "b", 1_000_000_000M },
            { "t", 1_000_000_000_000M },
            { "q", 1_000_000_000_000_000M }  // Quadrillion
        };

        // Process each quantity word
        foreach ((string? word, decimal multiplier) in quantityMultipliers)
        {
            // Use regex to find patterns like "5 million", "2.5 thousand", "-3 dozen", etc.
            // Pattern matches: optional negative sign, digits with optional decimal point, whitespace, and the quantity word
            string pattern = @"(-?\d+\.?\d*)\s+" + System.Text.RegularExpressions.Regex.Escape(word) + @"\b";

            expression = System.Text.RegularExpressions.Regex.Replace(
                expression,
                pattern,
                match =>
                {
                    string numberStr = match.Groups[1].Value;
                    if (decimal.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal number))
                    {
                        decimal result = number * multiplier;
                        // For large numbers (> 100k), add .0 to ensure double/decimal evaluation in NCalc
                        // This prevents int32 overflow issues
                        if (Math.Abs(result) > 100000 && result % 1 == 0)
                        {
                            return result.ToString("F1", CultureInfo.InvariantCulture);
                        }
                        // Format without decimal places if it's a whole number and small
                        return result % 1 == 0
                            ? result.ToString("F0", CultureInfo.InvariantCulture)
                            : result.ToString(CultureInfo.InvariantCulture);
                    }
                    return match.Value;
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return expression;
    }

    /// <summary>
    /// Handles parameter assignment (variable = expression).
    /// </summary>
    private async Task<string> HandleParameterAssignmentAsync(string line)
    {
        int equalIndex = line.IndexOf('=');
        string variableName = line[..equalIndex].Trim();
        string expression = line[(equalIndex + 1)..].Trim();

        // Validate variable name (simple validation)
        if (!IsValidVariableName(variableName))
        {
            throw new ArgumentException($"Invalid variable name: {variableName}");
        }

        // Parse quantity words first
        expression = ParseQuantityWords(expression);

        // Evaluate the expression to get the value
        expression = StandardizeDecimalAndGroupSeparators(expression);
        ExpressionOptions option = ExpressionOptions.IgnoreCaseAtBuiltInFunctions;
        AsyncExpression expr = new(expression, option)
        {
            CultureInfo = CultureInfo ?? CultureInfo.CurrentCulture
        };

        // Set up parameter handler for existing parameters
        expr.EvaluateParameterAsync += (name, args) =>
        {
            if (_parameters.ContainsKey(name))
            {
                args.Result = _parameters[name];
            }
            else if (TryGetMathConstant(name, out double constantValue))
            {
                args.Result = constantValue;
            }
            else
            {
                args.Result = null; // Default to null if parameter not found
            }
            return ValueTask.CompletedTask;
        };

        // Register custom functions
        RegisterCustomFunctions(expr);

        object? result = await expr.EvaluateAsync();

        if (result != null)
        {
            _parameters[variableName] = result;
            return $"> {variableName} = {FormatResult(result)}";
        }
        else
        {
            return $"> {variableName} = null";
        }
    }

    /// <summary>
    /// Evaluates a standard expression (not an assignment).
    /// </summary>
    private async Task<string> EvaluateStandardExpressionAsync(string line)
    {
        // Parse quantity words first
        line = ParseQuantityWords(line);

        ExpressionOptions option = ExpressionOptions.IgnoreCaseAtBuiltInFunctions;
        line = StandardizeDecimalAndGroupSeparators(line);
        AsyncExpression expression = new(line, option)
        {
            CultureInfo = CultureInfo ?? CultureInfo.CurrentCulture,
        };

        // Set up parameter handler
        expression.EvaluateParameterAsync += (name, args) =>
        {
            if (_parameters.ContainsKey(name))
            {
                args.Result = _parameters[name];
            }
            else if (TryGetMathConstant(name, out double constantValue))
            {
                args.Result = constantValue;
            }
            return ValueTask.CompletedTask;
        };

        // Register custom functions
        RegisterCustomFunctions(expression);

        object? result = await expression.EvaluateAsync();

        if (result != null)
        {
            string formattedResult = FormatResult(result);
            return formattedResult;
        }
        else
        {
            return "null";
        }
    }

    /// <summary>
    /// Validates if a string is a valid variable name.
    /// </summary>
    public bool IsValidVariableName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        // Must start with letter or underscore
        if (!char.IsLetter(name[0]) && name[0] != '_') return false;

        // Rest must be letters, digits, or underscores
        return name.Skip(1).All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    /// <summary>
    /// Attempts to get a built-in math constant value for the given parameter name.
    /// Supports case-insensitive matching for common mathematical constants.
    /// </summary>
    /// <param name="name">The parameter name to check</param>
    /// <param name="value">The constant value if found</param>
    /// <returns>True if the parameter is a recognized math constant</returns>
    public static bool TryGetMathConstant(string name, out double value)
    {
        value = name.ToLowerInvariant() switch
        {
            "pi" => Math.PI,                    // π ≈ 3.14159265359
            "e" => Math.E,                      // e ≈ 2.71828182846
            "tau" => Math.Tau,                  // τ = 2π ≈ 6.28318530718
            "phi" => (1.0 + Math.Sqrt(5.0)) / 2.0, // Golden ratio ≈ 1.61803398875
            "sqrt2" => Math.Sqrt(2.0),          // √2 ≈ 1.41421356237
            "sqrt3" => Math.Sqrt(3.0),          // √3 ≈ 1.73205080757
            "sqrt5" => Math.Sqrt(5.0),          // √5 ≈ 2.23606797750
            "ln2" => Math.Log(2.0),             // ln(2) ≈ 0.69314718056
            "ln10" => Math.Log(10.0),           // ln(10) ≈ 2.30258509299
            "log2e" => Math.Log2(Math.E),       // log₂(e) ≈ 1.44269504089
            "log10e" => Math.Log10(Math.E),     // log₁₀(e) ≈ 0.43429448190
            _ => double.NaN
        };

        return !double.IsNaN(value);
    }

    /// <summary>
    /// Formats a calculation result for display.
    /// </summary>
    public string FormatResult(object? result)
    {
        if (result is null)
            return "null";

        return result switch
        {
            double d when double.IsNaN(d) => "NaN",
            double d when double.IsPositiveInfinity(d) => "∞",
            double d when double.IsNegativeInfinity(d) => "-∞",
            double d => Math.Abs(d % 1) < double.Epsilon ? d.ToString("N0") : d.ToString("#,##0.###"),
            decimal m => m.ToString("#,##0.###"),
            int i => i.ToString("N0"),
            long l => l.ToString("N0"),
            float f => f.ToString("#,##0.###"),
            bool b => b.ToString().ToLower(),
            _ => result.ToString() ?? "null"
        };
    }

    /// <summary>
    /// Clears all stored parameters.
    /// </summary>
    public void ClearParameters()
    {
        _parameters.Clear();
    }

    /// <summary>
    /// Gets the current stored parameters.
    /// </summary>
    public IReadOnlyDictionary<string, object> GetParameters()
    {
        return _parameters;
    }

    /// <summary>
    /// Registers custom functions for the expression evaluator.
    /// </summary>
    private static void RegisterCustomFunctions(AsyncExpression expression)
    {
        // Register Sum function
        expression.EvaluateFunctionAsync += async (name, args) =>
        {
            if (name.Equals("Sum", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Parameters.Length == 0)
                {
                    args.Result = 0;
                    return;
                }

                decimal sum = 0m;
                foreach (AsyncExpression parameter in args.Parameters)
                {
                    object? value = await parameter.EvaluateAsync();

                    // Handle different numeric types
                    if (value is null)
                        continue;

                    try
                    {
                        sum += Convert.ToDecimal(value);
                    }
                    catch
                    {
                        // If conversion fails, skip this value
                        continue;
                    }
                }

                args.Result = sum;
            }
        };
    }
}

/// <summary>
/// Represents the result of a calculation evaluation.
/// </summary>
public class CalculationResult
{
    /// <summary>
    /// Gets or sets the formatted output text.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of errors encountered.
    /// </summary>
    public int ErrorCount { get; set; }
}
