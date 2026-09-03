# Technical Documentation: `Text-Grab/Utilities/NumericUtilities.cs`

## Overview

The `NumericUtilities` static class provides helper methods for parsing, formatting, comparing, and analyzing numeric values within text strings. It handles international number formatting quirks (such as differing thousands separators and decimal points), flexible floating-point conversions, token extraction from unformatted text, statistical median calculation, and floating-point value comparisons.

---

## Class Signature

```csharp
namespace Text_Grab.Utilities;

public static partial class NumericUtilities
```

---

## Key Components & Methods

### 1. Statistical Operations

#### `CalculateMedian(List<double> numbers)`

Calculates the statistical median from a list of `double` precision numbers.

* **Parameters:**
  * `numbers` (`List<double>`): A list of double-precision floating-point numbers.
* **Returns:** `double` — The calculated median value.
* **Logic:**
  1. Checks if `numbers` is empty (`Count == 0`). Returns `0` if empty.
  2. Sorts the list in ascending order using `.OrderBy(n => n)`.
  3. Evaluates the count of elements:
     * **Odd count:** Returns the middle element at index `count / 2`.
     * **Even count:** Calculates and returns the arithmetic mean of the two middle elements (`sorted[count / 2 - 1]` and `sorted[count / 2]`).

---

### 2. Formatting

#### `FormatNumber(double value)`

Formats a `double` value into a standardized string representation based on its magnitude, special state (NaN/Infinity), and floating-point precision.

* **Parameters:**
  * `value` (`double`): The number to format.
* **Returns:** `string` — A formatted text representation of the number.
* **Logic:**
  1. **Special Floating-Point Values:**
     * `double.IsNaN(value)` $\rightarrow$ returns `"NaN"`
     * `double.IsPositiveInfinity(value)` $\rightarrow$ returns `"∞"`
     * `double.IsNegativeInfinity(value)` $\rightarrow$ returns `"-∞"`
  2. **Scientific Notation:**
     * Evaluates `absValue = Math.Abs(value)`.
     * If `absValue >= 1e15` OR (`absValue < 1e-4` AND `absValue > 0`), returns scientific notation formatted using `value.ToString("E6", CultureInfo.CurrentCulture)`.
  3. **Integer Approximation Check:**
     * Calculates `fractionalPart = Math.Abs(value - Math.Round(value))`.
     * Flag `isEffectivelyInteger` is `true` if `fractionalPart < 1e-10` and `absValue < 1e10`.
     * If `isEffectivelyInteger` is `true`: Rounds `value` and returns string formatted with standard integer separator format (`"N0"`, using `CultureInfo.CurrentCulture`).
     * Otherwise: Returns standard number format with decimal places (`"N"`, using `CultureInfo.CurrentCulture`).

---

### 3. Precision & Comparison

#### `AreClose(double a, double b, double epsilon = 0.25)`

Determines if two `double` values are approximately equal within a specified tolerance threshold.

* **Parameters:**
  * `a` (`double`): First value.
  * `b` (`double`): Second value.
  * `epsilon` (`double`, optional): Maximum allowed absolute difference. Default is `0.25`.
* **Returns:** `bool` — `true` if `Math.Abs(a - b) < epsilon`; otherwise `false`.

---

### 4. String Parsing & Extraction

#### `TryExtractFirstDouble(string input, out double value)`

Attempts to extract and parse the first valid double-precision floating-point number from a given input string.

* **Parameters:**
  * `input` (`string`): The raw string containing text or numbers.
  * `value` (`out double`): Extracted numeric value if successful; otherwise `0`.
* **Returns:** `bool` — `true` if a number could be successfully extracted and parsed; otherwise `false`.
* **Logic:**
  1. Attempts `TryParseFlexibleDouble(input, out value)` directly on the full input string. If successful, returns `true`.
  2. If direct parsing fails, uses `FirstNumericTokenRegex` to iterate through all substring matches conforming to numeric patterns in `input`.
  3. Passes each regex match to `TryParseFlexibleDouble`. Returns `true` on the first successfully parsed match.
  4. If no valid numeric token is parsed, sets `value = 0` and returns `false`.

#### `TryParseFlexibleDouble(string input, out double value)`

Attempts to parse a string into a `double` after applying normalization rules to handle varied punctuation and spaces.

* **Parameters:**
  * `input` (`string`): Input text string.
  * `value` (`out double`): Parsed result output.
* **Returns:** `bool` — `true` if successfully parsed; otherwise `false`.
* **Logic:**
  1. Sets `value = 0`.
  2. Returns `false` if `input` is `null`, empty, or whitespace.
  3. Calls `NormalizeNumberString(input)`. If normalization returns an empty string, returns `false`.
  4. Calls `double.TryParse` using `NumberStyles.Float | NumberStyles.AllowLeadingSign` and `CultureInfo.InvariantCulture`.

---

### 5. Private Helper Methods & Regular Expressions

#### `NormalizeNumberString(string input)`

Internal helper method that cleans string representations of numbers, normalizing spaces, underscores, thousand separators, and decimal delimiters.

* **Parameters:**
  * `input` (`string`): The raw string to normalize.
* **Returns:** `string` — A normalized, invariant-culture compatible number string.
* **Normalization Logic:**
  1. Trims input and strips out all space (`' '`) and underscore (`'_'`) characters.
  2. Analyzes occurrences of comma (`,`) and dot (`.`) characters:
     * **Both `,` and `.` present:**
       * If `commaIndex > dotIndex` (e.g., European style `1.234,56`): removes all dots and replaces commas with dots (`1234.56`).
       * If `dotIndex > commaIndex` (e.g., US style `1,234.56`): removes all commas (`1234.56`).
     * **Only `,` present:**
       * If there are multiple commas OR exactly 3 digits after the final comma (e.g., thousands separators like `1,000` or `1,000,000`): removes all commas (`1000`).
       * Otherwise (e.g., `1,5`): replaces comma with dot (`1.5`).
     * **Only `.` present:**
       * Determines `digitsAfterDot` and evaluates if `beforeDot` starts with `'0'`.
       * If multiple dots exist OR (there are 3 digits after the dot AND the string before the dot is non-empty and does not start with `'0'`): treats dots as thousands separators and removes them.

#### `FirstNumericToken()`

Generated compiled regular expression pattern used to detect numeric tokens within arbitrary string inputs.

```csharp
[GeneratedRegex(@"[-+]?(?:(?:\d[\d\s_,.]*)?\d)(?:[eE][-+]?\d+)?", RegexOptions.Compiled)]
private static partial Regex FirstNumericToken();
```

* **Regex Pattern Details:**
  * `[-+]?`: Optional leading positive or negative sign.
  * `(?:(?:\d[\d\s_,.]*)?\d)`: Sequence of digits mixed with acceptable formatting characters (spaces, underscores, commas, dots), ending with a digit.
  * `(?:[eE][-+]?\d+)?`: Optional scientific/exponential notation match (e.g., `e+10`, `E-3`).