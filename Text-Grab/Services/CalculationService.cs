using NCalc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Text_Grab.Services;

/// <summary>
/// Service for evaluating mathematical expressions from text input.
/// Supports variable assignments, mathematical constants, and multi-line expressions.
/// </summary>
public partial class CalculationService
{
    private readonly Dictionary<string, object> _parameters = [];

    /// <summary>
    /// Gets the culture info used for number formatting and parsing.
    /// Defaults to InvariantCulture for consistent parsing with period (.) as decimal separator
    /// and comma (,) as function argument separator in NCalc.
    /// Change to de-DE if you prefer comma (,) for decimal and semicolon (;) for arguments.
    /// </summary>
    public CultureInfo CultureInfo { get; set; } = CultureInfo.InvariantCulture;

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
            return new CalculationResult { Output = string.Empty, ErrorCount = 0, OutputNumbers = [] };
        }

        string[] lines = input.Split('\n');
        List<string> results = [];
        List<double> outputNumbers = [];
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
                    
                    // Extract variable name and add its value to output numbers
                    int equalIndex = trimmedLine.IndexOf('=');
                    string variableName = trimmedLine[..equalIndex].Trim();
                    if (_parameters.TryGetValue(variableName, out object? value))
                    {
                        try
                        {
                            double numValue = Convert.ToDouble(value);
                            outputNumbers.Add(numValue);
                        }
                        catch
                        {
                            // Skip non-numeric values
                        }
                    }
                }
                else
                {
                    string resultLine = await EvaluateStandardExpressionAsync(trimmedLine);
                    results.Add(resultLine);
                    
                    // Try to parse the result as a number and add to output numbers
                    // Remove formatting characters before parsing
                    string cleanedResult = resultLine.Replace(",", "").Replace(" ", "").Trim();
                    if (double.TryParse(cleanedResult, NumberStyles.Any, CultureInfo.InvariantCulture, out double numValue))
                    {
                        outputNumbers.Add(numValue);
                    }
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
            ErrorCount = errorCount,
            OutputNumbers = outputNumbers
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
    /// Handles user input that may contain thousand separators (like "1,000,000")
    /// and converts them to the format expected by NCalc for the current culture.
    /// </summary>
    public string StandardizeDecimalAndGroupSeparators(string expression)
    {
        if (CultureInfo is null)
            return expression;

        string decimalSep = CultureInfo.NumberFormat.NumberDecimalSeparator;
        string groupSep = CultureInfo.NumberFormat.NumberGroupSeparator;

        // For cultures like de-DE where comma is decimal and period is thousands
        if (decimalSep == "," && groupSep == ".")
        {
            // Remove periods that are clearly thousand separators (followed by exactly 3 digits and another separator or digit group)
            expression = DigitGroupSeparator().Replace(expression, "$1");

            // Convert remaining periods to commas (these are decimal points)
            expression = expression.Replace(".", ",");
        }
        // For InvariantCulture and en-US where comma is used for thousands (like "1,000,000")
        // Remove commas that are thousand separators, but NOT commas used as function argument separators
        else if (decimalSep == "." && groupSep == ",")
        {
            // Remove commas that are clearly thousand separators (between digits, followed by exactly 3 digits)
            // Pattern: digit, comma, exactly 3 digits, then either another comma, non-digit, or end
            expression = CommaGroupSeparator().Replace(expression, "$1");
        }

        return expression;
    }

    /// <summary>
    /// Parses quantity words and abbreviations in expressions, converting them to numeric values.
    /// Examples: "5 million" -> "5000000.0", "3 dozen" -> "36", "2.5 k" -> "2500"
    /// </summary>
    /// <param name="expression">The expression to parse</param>
    /// <param name="cultureInfo">The culture to use for formatting output numbers (default: InvariantCulture)</param>
    /// <returns>The expression with quantity words replaced by numeric values</returns>
    public static string ParseQuantityWords(string expression, CultureInfo? cultureInfo = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return expression;

        cultureInfo ??= CultureInfo.InvariantCulture;

        // Dictionary of quantity words and their multipliers
        // Note: Using double for range, supports up to ~10^308
        Dictionary<string, double> quantityMultipliers = new(StringComparer.OrdinalIgnoreCase)
        {
            // Extremely large orders of magnitude
            { "googol", 1e100 },                                                  // 10^100
            { "decillion", 1e33 },                                                // 10^33
            { "nonillion", 1e30 },                                                // 10^30
            { "octillion", 1e27 },                                                // 10^27
            { "yotta", 1e24 },                                                    // 10^24 (SI prefix)
            { "septillion", 1e24 },                                               // 10^24
            { "zetta", 1e21 },                                                    // 10^21 (SI prefix)
            { "sextillion", 1e21 },                                               // 10^21
            { "exa", 1e18 },                                                      // 10^18 (SI prefix)
            { "quintillion", 1e18 },                                              // 10^18
            { "peta", 1e15 },                                                     // 10^15 (SI prefix)
            { "quadrillion", 1e15 },                                              // 10^15
            { "tera", 1e12 },                                                     // 10^12 (SI prefix)
            { "trillion", 1e12 },                                                 // 10^12
            { "giga", 1e9 },                                                      // 10^9 (SI prefix)
            { "billion", 1e9 },                                                   // 10^9
            { "mega", 1e6 },                                                      // 10^6 (SI prefix)
            { "million", 1e6 },                                                   // 10^6
            { "kilo", 1e3 },                                                      // 10^3 (SI prefix)
            { "thousand", 1e3 },                                                  // 10^3
            { "hecto", 1e2 },                                                     // 10^2 (SI prefix)
            { "hundred", 1e2 },                                                   // 10^2
            { "deca", 1e1 },                                                      // 10^1 (SI prefix)
            { "deka", 1e1 },                                                      // 10^1 (SI prefix, alternate spelling)
            // Special quantities
            { "dozen", 12.0 },
            { "score", 20.0 },
            { "gross", 144.0 },
            // Abbreviations
            { "k", 1e3 }
            // { "m", 1e6 }, // Ambiguous: could be meter or million
            // { "b", 1e9 },
            // { "t", 1e12 },
            // { "q", 1e15 }  // Quadrillion
        };

        // Process each quantity word
        foreach ((string? word, double multiplier) in quantityMultipliers)
        {
            // Use regex to find patterns like "5 million", "2.5 thousand", "-3 dozen", "5million", etc.
            // Pattern matches: optional negative sign, digits with optional decimal point, optional whitespace, and the quantity word
            string pattern = @"(-?\d+\.?\d*)\s*" + System.Text.RegularExpressions.Regex.Escape(word) + @"\b";

            expression = System.Text.RegularExpressions.Regex.Replace(
                expression,
                pattern,
                match =>
                {
                    string numberStr = match.Groups[1].Value;
                    if (double.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
                    {
                        double result = number * multiplier;

                        // Create a NumberFormatInfo without group separators to avoid thousand separators
                        NumberFormatInfo nfi = (NumberFormatInfo)cultureInfo.NumberFormat.Clone();
                        nfi.NumberGroupSeparator = "";

                        // For large numbers (> 100k), add decimal separator to ensure double evaluation in NCalc
                        // This prevents int32 overflow issues. Use "G" format for very large numbers to preserve precision.
                        if (Math.Abs(result) > 100000 && result % 1 == 0)
                        {
                            // For very large numbers (>10^15), use "G" format to preserve precision
                            if (Math.Abs(result) > 1e15)
                            {
                                string resultStr = result.ToString("G17", CultureInfo.InvariantCulture);
                                // Add decimal point if not already present
                                if (!resultStr.Contains('.') && !resultStr.Contains('E'))
                                    resultStr += nfi.NumberDecimalSeparator + "0";
                                return resultStr;
                            }
                            return result.ToString("F1", nfi);
                        }
                        // Format without decimal places if it's a whole number and small
                        return result % 1 == 0
                            ? result.ToString("F0", nfi)
                            : result.ToString(nfi);
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
        expression = ParseQuantityWords(expression, CultureInfo);

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
        line = ParseQuantityWords(line, CultureInfo);

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


        object? result = null;

        try
        {
            result = await expression.EvaluateAsync();
        }
        catch (Exception)
        {
            throw new Exception($"Error evaluating expression: {line}");
        }

        if (result is null)
            return "Error: result is null";

        string formattedResult = FormatResult(result);
        return formattedResult;
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

    [System.Text.RegularExpressions.GeneratedRegex(@"(\d)\.(?=\d{3}(?:\.|,|\D|$))")]
    private static partial System.Text.RegularExpressions.Regex DigitGroupSeparator();
    [System.Text.RegularExpressions.GeneratedRegex(@"(\d),(?=\d{3}(?:,|\D|$))")]
    private static partial System.Text.RegularExpressions.Regex CommaGroupSeparator();
}

/// <summary>
/// Represents the result of a calculation evaluation
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

    /// <summary>
    /// Gets or sets the list of numeric output values from evaluated expressions.
    /// Includes both direct expression results and variable assignment values.
    /// </summary>
    public List<double> OutputNumbers { get; set; } = [];
}
