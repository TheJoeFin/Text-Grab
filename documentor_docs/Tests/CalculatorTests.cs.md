# CalculatorTests.cs Technical Documentation Guide

## Overview

The `CalculatorTests.cs` file contains comprehensive unit and integration tests for expression evaluation capabilities within the application (specifically for `Text_Grab.Services.CalculationService` and the underlying `NCalc` library). 

The test suite validates:
1. Native `NCalc` behavior and parameter resolution for mathematical constants.
2. The core functionality of `CalculationService` (variable assignment, expression parsing, comment handling, error reporting, and parameter management).
3. Custom aggregate functions (`Sum`).
4. English quantity word parsing (e.g., "5 million", "3 dozen", "10 k").
5. Percentage calculation syntax (e.g., "100 * 15%").
6. DateTime arithmetic and duration operations (e.g., "March 10, 2026 + 10 days", date subtraction, target unit conversion).
7. Operator continuation across multi-line inputs (e.g., chaining expressions starting with `+`, `-`, `*`, `/`).

---

## File & Environment Information

* **File Path:** `Tests/CalculatorTests.cs`
* **Namespace:** `Tests`
* **Target Assembly Under Test:** `Text_Grab.Services`
* **Test Framework:** xUnit (`[Fact]`, `[Theory]`, `[InlineData]`)
* **Dependencies:**
  * `NCalc` & `NCalc.Exceptions`
  * `System.Globalization`
  * `Text_Grab.Services` (`CalculationService`, `CalculationResult`)

---

## Test Categories & Functional Breakdown

### 1. NCalc Engine & Math Constant Tests

These tests evaluate how the `NCalc` expression engine operates directly, confirming built-in capabilities and proper handling of custom math parameters via the `EvaluateParameter` event handler.

* **Constant Absence Verification:** Tests like `NCalc_HasBuiltInPi_ReturnsFalse` and `NCalc_HasBuiltInE_ReturnsFalse` ensure that raw `NCalc` throws `NCalcParameterNotDefinedException` when constant names `Pi` or `E` are not explicitly defined.
* **Math Functions Support:** `NCalc_SupportsBasicMathFunctions` tests built-in trigonometric, exponential, absolute, square root, and logarithm functions (`Sin`, `Cos`, `Tan`, `Sqrt`, `Abs`, `Log`, `Exp`).
* **Custom Parameter Injection:** Validates dynamic assignment of `Math.PI`, `Math.E`, `Math.Tau`, and other constants via the `EvaluateParameter` event (`NCalc_WithCustomPiParameter_Works`, `NCalc_WithMultipleMathConstants_Works`, `AsyncNCalc_WithMathConstants_Works`).
* **Case Insensitivity:** `NCalc_CaseInsensitive_MathConstants` verifies that expressions using `ExpressionOptions.IgnoreCaseAtBuiltInFunctions` process constant names regardless of casing (e.g., `pi`, `PI`, `Pi`).
* **Integration Math Constant Verification:** `MathConstants_Integration_Test` tests predefined fallback math constants including `Pi`, `E`, `Tau`, `phi`, `sqrt2`, `sqrt3`, `sqrt5`, `ln2`, `ln10`, `log2e`, and `log10e`.

---

### 2. CalculationService Core Tests

This region tests the core text processing and multi-line evaluation engine provided by `CalculationService`.

* **Basic & Multi-Line Expressions:**
  * `CalculationService_BasicExpression_ReturnsCorrectResult` ensures `"2 + 2"` evaluates to `"4"`.
  * `CalculationService_MultipleExpressions_ReturnsMultipleResults` ensures newline-separated expressions evaluate line-by-line.
* **Variable Assignments & Scope:**
  * Tests variable storage and retrieval across lines (`a = 5`, `b = 10`, `a + b`).
  * `CalculationService_VariableReassignment_UpdatesValue` verifies re-assigning existing variables updates state.
  * `CalculationService_ClearParameters_ResetsState` and `CalculationService_GetParameters_ReturnsStoredVariables` test public parameter management methods.
* **Comments and Blank Lines:**
  * `CalculationService_CommentsAndEmptyLines_ArePreserved` verifies lines starting with `//` or `#` as well as empty lines are preserved in output without generating errors.
* **Error Handling Modes:**
  * Evaluates `ShowErrors = true` (outputs `"Error: ..."` message) vs. `ShowErrors = false` (outputs empty string for erroneous lines).
* **Culture and Separators:**
  * `CalculationService_CultureInfo_CanBeSet` tests setting custom `CultureInfo` (e.g., `de-DE`).
  * `CalculationService_StandardizeDecimalSeparators_WorksWithDifferentCultures` verifies separator handling using `StandardizeDecimalAndGroupSeparators`.
* **Output Formatting:**
  * `CalculationService_FormatResult_HandlesSpecialValues` verifies string outputs for `double.NaN` (`"NaN"`), `double.PositiveInfinity` (`"∞"`), `double.NegativeInfinity` (`"-∞"`), booleans, and nulls.
  * Checks digit grouping and decimal rounding logic (rounds to 3 decimal places for display).

---

### 3. Custom Function Tests (`Sum`)

Tests the custom variadic `Sum(...)` function within `CalculationService`.

* **Argument Flexibility:** Handles 0 arguments (`Sum()` $\rightarrow$ `0`), single arguments (`Sum(42)` $\rightarrow$ `42`), and multiple arguments (`Sum(1, 2, 3, 4, 5)` $\rightarrow$ `15`).
* **Data Types & Signs:** Supports negative numbers, mixed signs, decimals, large numbers, and variable references inside the function arguments.
* **Nested Functions & Expressions:** Verifies evaluation of expressions or functions inside `Sum`, e.g., `Sum((2 + 3) * 2, Abs(-5), Sqrt(16))`.
* **Case Insensitivity:** Confirms `sum()`, `SUM()`, and `SuM()` resolve correctly.

---

### 4. Quantity Word Parser Tests

Tests the `ParseQuantityWords` preprocessing logic, which converts human-readable quantity words into numeric values before evaluation.

* **Supported Words:**
  * Numerical Scales: `hundred` ($10^2$), `thousand` ($10^3$), `million` ($10^6$), `billion` ($10^9$), `trillion` ($10^{12}$), `quadrillion` ($10^{15}$), `quintillion` ($10^{18}$), `sextillion` ($10^{21}$), `septillion` ($10^{24}$), `octillion` ($10^{27}$).
  * Quantities: `dozen` ($12$), `score` ($20$), `gross` ($144$).
  * Abbreviations: `k` / `K` ($10^3$).
* **Case Insensitivity & Placement:** Accepts upper, lower, and mixed casing (e.g., `5 Million`, `5 MILLION`).
* **Expressions & Variables:** Handles complex combinations (`5 million * 12 hundred`), variable assignments (`population = 5 million`), decimals (`2.5 million`), and parentheses.

---

### 5. Percentage Syntax Tests

Tests `ParsePercentages` preprocessing and evaluation logic for percentage signs (`%`).

* **Standalone Values:** Converts standalone percentages to decimals (e.g., `25%` $\rightarrow$ `0.25`, `100%` $\rightarrow$ `1`).
* **Multiplication & Arithmetic:** Tests multiplication (`4 * 25%` $\rightarrow$ `1`), addition, subtraction, division, and negative percentages (`-10%` $\rightarrow$ `-0.1`).
* **Formatting:** Works with or without whitespace around `%` (e.g., `25 %`).
* **Higher-Order Integration:** Validates usage with math functions (`Sqrt(100) * 25%`) and `Sum(...)` (`Sum(100, 200) * 10%`).

---

### 6. DateTime Arithmetic Tests

Tests natural language date and duration calculations powered by `TryEvaluateDateTimeMath`.

* **Unit Addition & Subtraction:** Supports `days`, `weeks`, `months`, `years`, `decades`, `hours`, `mins`/`minutes`, and `seconds`.
* **Keywords:** Resolves context keywords `today`, `tomorrow`, and `yesterday`.
* **Date Formats:** Accepts long date strings (`March 10, 2026`), abbreviated months (`Jan 1, 2026`), numeric dates (`3/10/2026`, `3/10/26`), and ordinal suffixes (`March 10th, 2026`, `1st`, `2nd`, `3rd`, `4th`).
* **Time Component Handling:**
  * Preserves or displays time when times are present (e.g., `1/1/2026 2:00pm + 5 hours` $\rightarrow$ outputs date + `7:00pm`).
  * Displays time output when adding minute/hour durations to dates.
  * Returns date-only output when result lands on midnight.
* **Combined Duration Segments:** Tests multiple implicit duration tokens without requiring repeated operators:
  * Example: `"January 1, 2026 + 5 weeks 3 days 8 hours"`
  * Example: `"January 1, 2020 + 1 decade 2 years 3 months 2 weeks 5 days"`
  * Inherits operator context for trailing segments (e.g., `"March 1, 2026 + 1 month 5 days - 2 hours"`).
* **Date Subtraction (`Date - Date`):**
  * Subtracting two dates produces a duration breakdown in human-readable terms (e.g., `"March 10, 2026 - January 1, 2026"` $\rightarrow$ `"2 months 1 week 2 days"`).
  * Returns `"0 seconds"` for identical dates.
  * Works across time components (`3/1/2026 10:30:45am - 3/1/2026 8:00:00am` $\rightarrow$ `"2 hours 30 minutes 45 seconds"`).
  * Target Unit Conversions: Supports conversion clauses like `in weeks` or `to days` (e.g., `"March 10, 2026 - January 1, 2026 to days"` $\rightarrow$ `"68 days"`), population of numeric results in `CalculationResult.OutputNumbers`.

---

### 7. Line & Operator Continuation Tests

Tests multi-line accumulator calculations where a line begins with a binary operator (`+`, `-`, `*`, `/`).

* **Mechanism (`StartsWithBinaryOperator`):**
  * Detects when a line starts with a binary operator.
  * Prepends the result of the previous valid expression line.
  * Examples:
    * Line 1: `2 + 3` (Result: `5`)
    * Line 2: `* 4` (Evaluates as `5 * 4` $\rightarrow$ `20`)
* **Chaining:** Allows chaining running totals across many lines (`10` $\rightarrow$ `+ 10` $\rightarrow$ `- 5` $\rightarrow$ `* 3`).
* **Date Continuation:** Continues date expressions across lines (e.g., `"March 1, 2026 + 2 weeks"` followed by `"+ 1 month"` adds 1 month to March 15).
* **Comment Handling:** Skipping comments (`//`, `#`) does not clear the previous line's stored result context.

---

## Direct Helper Methods Tested

The test file directly verifies several static and instance methods exposed by `CalculationService`:

| Method | Description Tested |
| :--- | :--- |
| `IsParameterAssignment(string)` | Detects variable assignment syntax (`x = 10`) while ignoring comparisons (`==`, `!=`, `<=`, `>=`). |
| `IsValidVariableName(string)` | Validates identifier rules (alphanumeric and underscores; cannot start with a digit or contain special characters/spaces). |
| `TryGetMathConstant(string, out double)` | Case-insensitive lookup for pre-defined mathematical constants (`pi`, `e`, `tau`, `phi`, `sqrt2`). |
| `FormatResult(object)` | Formats object outputs into localized string representations, handling nulls, booleans, infinities, and NaN. |
| `StandardizeDecimalAndGroupSeparators(string)` | Standardizes group and decimal separators for culture-aware parsing. |
| `ParseQuantityWords(string)` | Converts quantity words into numeric values within expression strings. |
| `ParsePercentages(string)` | Preprocesses `%` symbols into decimal divisions (`/ 100.0`). |
| `TryEvaluateDateTimeMath(string, out string)` | Parses and evaluates date and time math expressions, returning `false` if invalid or no time units are present. |
| `StartsWithBinaryOperator(string)` | Returns `true` if input starts with a binary operator intended for expression chaining. |

---

## Test Execution Summary

* **Precision Assertions:** Floating-point comparison tests use precision parameters with `Assert.Equal`:
  * Standard math tests check accuracy up to **10 decimal places**.
  * Dynamic constant integration tests check accuracy up to **5 decimal places**.
* **Async Pattern:** Standard `Fact` methods returning `Task` utilize `await expression.EvaluateAsync(TestContext.Current.CancellationToken)` to test non-blocking evaluation paths with cancellation token support.