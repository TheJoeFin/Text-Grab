# Technical Documentation: `Text-Grab/Services/CalculationService.cs`

## Overview

The `CalculationService` class provides a multi-line mathematical expression evaluation engine for the Text-Grab application. Built on top of the `NCalc` library, it evaluates standard arithmetic expressions, handles variable definitions, parses percentages and natural quantity words, formats output, and coordinates date/time calculations and unit conversions across partial class files.

---

## Data Models

### `CalculationResult`

Represents the output state following the evaluation of one or more expressions.

| Property | Type | Description |
| :--- | :--- | :--- |
| `Output` | `string` | The formatted multi-line result text corresponding to each evaluated input line. |
| `ErrorCount` | `int` | The total number of line evaluation errors encountered during processing. |
| `OutputNumbers` | `List<double>` | A list containing all valid double-precision numeric results produced by expressions or parameter assignments. |
| `DominantUnit` | `string?` | The most frequently occurring unit abbreviation identified across unit-conversion line results. `null` if no units were processed. |

---

## State and Configuration

### `CalculationService` Properties and Fields

* **`CultureInfo CultureInfo`** (Property)
  * **Type:** `System.Globalization.CultureInfo`
  * **Default:** `CultureInfo.InvariantCulture`
  * **Description:** Controls number parsing and string formatting. By default, uses invariant culture (`.` as decimal separator, `,` as function argument separator).
* **`ShowErrors`** (Property)
  * **Type:** `bool`
  * **Default:** `true`
  * **Description:** Determines whether evaluation errors return formatted error messages (`Error: <message>`) or blank lines in the output string.
* **`_parameters`** (Private Field)
  * **Type:** `Dictionary<string, object>`
  * **Description:** Stores active variable names and their evaluated values during execution. Cleared prior to each multi-line evaluation session.
* **`_quantityMultipliers`** (Private Static Field)
  * **Type:** `Dictionary<string, double>`
  * **Description:** A case-insensitive dictionary mapping word names and abbreviations to numeric multipliers (e.g., `"thousand"` $\rightarrow 10^3$, `"million"` $\rightarrow 10^6$, `"dozen"` $\rightarrow 12$).

---

## Key Features & Syntactical Support

### 1. Line Filtering & Comments
* Lines starting with `//` or `#`, as well as empty or whitespace-only lines, are ignored during evaluation. Line spacing in the output is preserved as blank lines.

### 2. Multi-Line Continuation Operators
* If a line begins with a binary operator and a numeric result exists from the preceding non-empty line, the previous result is automatically prepended to the current line.
* **Operators supported:** `+`, `-`, `*`, `/`.
* **Rules:**
  * `*` and `/`: Match when followed by a space, digit, parenthesis `(`, or letter.
  * `+` and `-`: Match **only** when followed by a space (to distinguish continuous operators from signed values like `-3`).

### 3. Quantity Words and Multipliers
`ParseQuantityWords` automatically expands natural-language quantity terms preceding numbers:
* **Orders of Magnitude / Prefixes:** `deca`/`deka`, `hecto`, `kilo`/`thousand`/`k`, `mega`/`million`, `giga`/`billion`, `tera`/`trillion`, `peta`/`quadrillion`, `exa`/`quintillion`, `zetta`/`septillion`, `yotta`/`octillion`, `nonillion`, `decillion`, `googol`.
* **Special Quantities:** `dozen` ($12$), `score` ($20$), `gross` ($144$).

### 4. Percentage Parsing
`ParsePercentages` converts percentage strings into equivalent decimal representations prior to expression evaluation (e.g., `25%` $\rightarrow$ `0.25`).

### 5. Mathematical Constants
Built-in mathematical constants can be referenced directly in expressions via case-insensitive parameter evaluation:

| Constant Key | Value | Description |
| :--- | :--- | :--- |
| `pi` | $\pi \approx 3.141592653589793$ | Ratio of a circle's circumference to its diameter |
| `e` | $e \approx 2.718281828459045$ | Euler's number |
| `tau` | $\tau = 2\pi \approx 6.283185307179586$ | Circle constant |
| `phi` | $\phi \approx 1.618033988749895$ | Golden ratio |
| `sqrt2` | $\sqrt{2} \approx 1.414213562373095$ | Square root of 2 |
| `sqrt3` | $\sqrt{3} \approx 1.732050807568877$ | Square root of 3 |
| `sqrt5` | $\sqrt{5} \approx 2.236067977499790$ | Square root of 5 |
| `ln2` | $\ln(2) \approx 0.693147180559945$ | Natural logarithm of 2 |
| `ln10` | $\ln(10) \approx 2.302585092994046$ | Natural logarithm of 10 |
| `log2e` | $\log_2(e) \approx 1.442695040888963$ | Base-2 logarithm of $e$ |
| `log10e` | $\log_{10}(e) \approx 0.434294481903251$ | Base-10 logarithm of $e$ |

### 6. Custom Functions
The service registers custom NCalc function evaluations:
* **`Sum(...)`**: Accepts zero or more parameters, attempts to convert each argument to `decimal`, and returns the total sum. Non-convertible or `null` values are skipped. If no arguments are passed, returns `0`.

---

## Primary Methods Reference

### Core Evaluation Pipeline

#### `Task<CalculationResult> EvaluateExpressionsAsync(string input)`
Asynchronously processes a block of text containing one or more expression lines.

**Workflow per line:**
1. Trims whitespace. If line is empty or a comment (`//` or `#`), appends empty output line.
2. Checks for continuous binary operator prefix and prepends `previousLineResult` if applicable.
3. **Evaluation Fallback Cascade:**
   * **Date/Time Math:** Calls `TryEvaluateDateTimeMath` (defined in partial class).
   * **Unit Conversion:** Calls `TryEvaluateUnitConversion` (defined in partial class).
   * **Parameter Assignment:** Evaluates lines matching `variable = expression`.
   * **Standard Expression:** Evaluates standard arithmetic expressions.
4. Updates `OutputNumbers`, error counts, and tracks unit frequencies to calculate `DominantUnit`.
5. Returns a consolidated `CalculationResult`.

---

### Variable & Expression Parsing Helpers

#### `bool IsParameterAssignment(string line)`
Checks if a line is a variable definition.
* Returns `true` if the line contains a single `=` character, is not a comparison operator (`==`, `!=`, `<=`, `>=`), and contains a non-whitespace right-hand side.

#### `bool IsValidVariableName(string name)`
Determines if a string is a valid variable identifier.
* Returns `true` if the string starts with an ASCII letter or underscore (`_`) and is followed exclusively by letters, digits, or underscores.

#### `Task<string> HandleParameterAssignmentAsync(string line)`
Evaluates a parameter assignment line (e.g., `x = 10 * 2`).
1. Extracts variable name and expression body.
2. Validates variable name via `IsValidVariableName`.
3. Pre-processes percentages and quantity words.
4. Normalizes culture separators.
5. Evaluates RHS via NCalc, assigning the result into `_parameters`.
6. Returns string formatted as `> variable = <value>`.

#### `Task<string> EvaluateStandardExpressionAsync(string line)`
Evaluates standard mathematical expressions.
1. Pre-processes percentages, quantity words, and culture separators.
2. Creates an `NCalc.Expression` object with parameter resolution and custom functions registered.
3. Evaluates expression asynchronously and returns formatted string output via `FormatResult`.

---

### Parsing and Normalization

#### `string StandardizeDecimalAndGroupSeparators(string expression)`
Adjusts thousand and decimal separators based on `CultureInfo`:
* **German / European (`de-DE`) Style (`,` decimal, `.` group):** Removes group separator periods and converts decimal periods to commas.
* **Invariant / US Style (`.` decimal, `,` group):** Uses regex to strip thousand-separator commas while retaining commas used for function arguments.

#### `static string ParsePercentages(string expression, CultureInfo? cultureInfo = null)`
Uses Regex pattern `(-?\d+\.?\d*)\s*%` to locate percentages, converts values to decimal values ($n / 100$), and returns the updated string without group separators.

#### `static string ParseQuantityWords(string expression, CultureInfo? cultureInfo = null)`
Iterates through `_quantityMultipliers`, using regular expressions to replace quantity names (e.g., `5 million`) with raw numeric values. Large numbers ($> 10^5$) append decimal points where necessary to prevent integer overflow during evaluation.

#### `static bool TryGetMathConstant(string name, out double value)`
Case-insensitive lookup for pre-defined math constants. Returns `true` along with the corresponding `double` value if found.

#### `static bool StartsWithBinaryOperator(string trimmedLine)`
Determines if `trimmedLine` starts with `*`, `/`, `+`, or `-` according to continuation rules to infer operation on previous line results.

---

### State Management & Utilities

#### `string FormatResult(object? result)`
Formats objects into human-readable strings based on type:
* `NaN` $\rightarrow$ `"NaN"`
* Positive Infinity $\rightarrow$ `"∞"`
* Negative Infinity $\rightarrow$ `"-∞"`
* Integers/Whole Doubles/Longs $\rightarrow$ Formatted using `"N0"` or current format without trailing zeros.
* Floating point / Decimal $\rightarrow$ Formatted with dynamic decimal separators (`#,##0.###`).
* `bool` $\rightarrow$ Lowercase string (`"true"` / `"false"`).
* `null` $\rightarrow$ `"null"`.

#### `void ClearParameters()`
Clears all entries inside `_parameters`.

#### `IReadOnlyDictionary<string, object> GetParameters()`
Returns a read-only view of the internal variable storage `_parameters`.