# Unit Conversion Tests Documentation (`Tests/UnitConversionTests.cs`)

## Overview

The `UnitConversionTests.cs` file contains a suite of automated unit tests written in C# using xUnit. Its primary purpose is to test and validate the unit conversion, parsing, calculation, and formatting functionality provided by the `CalculationService` class (from `Text_Grab.Services`).

The test suite covers a wide range of physical measurement categories (length, mass, temperature, volume, speed, area, time duration), syntax variations (explicit conversion keywords, continuation across multiple lines, operator continuations), ambiguity resolution (variable priority, date math, decimal vs. thousands separators), and formatting edge cases (feet/inches representation).

---

## Technical Details & Class Setup

* **Namespace:** `Tests`
* **Target Service:** `Text_Grab.Services.CalculationService`
* **Test Instance:** A single private, read-only field instantiates the service:
  ```csharp
  private readonly CalculationService _service = new();
  ```

---

## Test Regions and Test Methods

The test file is organized into 10 distinct `#region` blocks:

---

### 1. Explicit Conversion Tests

Tests unit conversion inputs that explicitly state a source quantity and a target unit using keywords like `to` or `in`.

| Method Name | Test Type | Description / Intent |
| :--- | :--- | :--- |
| `ExplicitConversion_ContainsTargetUnit` | `[Theory]` | Verifies that explicit conversion expressions contain the expected target unit symbol/abbreviation in the output string (e.g., `"5 miles to km"` $\rightarrow$ `"km"`) and produce zero errors. |
| `ExplicitConversion_CorrectNumericValue` | `[Theory]` | Verifies that explicit conversions calculate the numeric output value within a defined tolerance range (`expectedValue ± tolerance`). Tests include metric/imperial lengths, temperatures ($^\circ\text{C}$ to $^\circ\text{F}$), speeds ($\text{mph}$, $\text{km/h}$), and pace ($\text{min/mi}$, $\text{min/km}$). |
| `ExplicitConversion_WithShortAbbreviations` | `[Theory]` | Verifies conversions using short unit abbreviations like `"in"`, `"ft"`, `"yd"` where `"in"` could potentially conflict with the keyword `"in"`. |
| `ExplicitConversion_InKeyword_Works` | `[Fact]` | Verifies that the keyword `"in"` can be used interchangeably with `"to"` (e.g., `"5 gallons in liters"`). |
| `DurationConversion_ExplicitSyntax_Works` | `[Theory]` | Tests conversions involving time durations (years to days, hours to days, minutes to hours). |
| `DurationConversion_UsesFixedAverageMonthLength` | `[Fact]` | Validates that `"1 month to days"` evaluates using a fixed average month length of `30.44 days`. |
| `ExplicitConversion_ZeroValue_Works` | `[Fact]` | Ensures zero values convert properly (`"0 km to miles"`). |
| `ExplicitConversion_NegativeValue_Works` | `[Fact]` | Ensures negative values convert properly (`"-40 celsius to fahrenheit"` $\rightarrow$ `-40`). |
| `ExplicitConversion_IncompatibleTypes_FallsThrough` | `[Fact]` | Asserts that converting between incompatible unit categories (e.g., mass to length: `"5 kg to km"`) does not produce a valid unit result and falls through to the general calculation engine (resulting in an error or omitting `"km"` from output). |

---

### 2. Feet and Inches Tests

Tests specific formatting logic for conversions whose target unit is feet/inches.

| Method Name | Test Type | Description / Intent |
| :--- | :--- | :--- |
| `ConversionToFeet_FormatsAsFeetAndInches` | `[Theory]` | Validates that converting meters or miles to feet formats the result into standard combined feet and inches output notation (e.g., `"1.9 meters to feet"` outputs `"6 ft 3 in"`). |
| `ConversionToFeet_StillTracksNumericValue` | `[Fact]` | Confirms that while the text output is formatted as `"6 ft 3 in"`, the underlying `OutputNumbers` collection still tracks the precise decimal value in feet (~`6.23`–`6.24`). |
| `ContinuationConversionToFeet_FormatsAsFeetAndInches` | `[Fact]` | Ensures multiline continuation input (`"1.9 meters\nto feet"`) properly formats the conversion output as feet and inches on the second line. |

---

### 3. Decimal Parsing Tests

Focuses on edge cases involving decimal points vs. thousands separators in numerical inputs.

| Method Name | Test Type | Description / Intent |
| :--- | :--- | :--- |
| `DecimalWithThreeDigits_ParsedCorrectly` | `[Theory]` | Regression test ensuring 3-digit decimals with leading zeros (e.g., `0.345`, `0.100`, `0.500`, `0.125`) are recognized as decimal fractions rather than European thousands separators. |
| `DecimalVsThousandsSeparator_CorrectBehavior` | `[Theory]` | Confirms that numbers like `"1.000"` or `"2.000"` (where the integer portion does not start with `0`) are interpreted as thousands separators (`1,000` km, `2,000` meters) and converted accordingly. |

---

### 4. Continuation Conversion Tests

Tests multiline conversions where a line depends on the unit/value context from the preceding line.

| Method Name | Test Type | Description / Intent |
| :--- | :--- | :--- |
| `ContinuationConversion_ToKeyword` | `[Fact]` | Verifies input like `"5 miles\nto km"` splits into a 2-line result containing miles on line 1 and km on line 2. |
| `ContinuationConversion_CorrectValue` | `[Fact]` | Verifies correct output numbers across lines (`"100 celsius\nto fahrenheit"` yields `[100, 212]`). |
| `ContinuationConversion_ChainedConversions` | `[Fact]` | Tests chaining conversions across multiple lines (`"1 mile\nto km\nto meters"` yields `1609.34` meters on line 3). |
| `ContinuationConversion_PaceToSpeed` | `[Fact]` | Tests multiline conversions between running pace and speed (`"8 min/mi\nto km/h"`). |
| `ContinuationConversion_PaceTimeToSpeed` | `[Fact]` | Tests converting time-formatted pace inputs (`"9:30 min/mi\nto mph"` yields numeric pace `9.5` and speed `~6.316`). |

---

### 5. Operator Continuation Tests

Tests performing arithmetic operations on unit values across multiple lines.

| Method Name | Test Type | Description / Intent |
| :--- | :--- | :--- |
| `OperatorContinuation_AddSameUnit` | `[Fact]` | Tests multiline addition with identical units (`"5 km\n+ 3 km"` $\rightarrow$ `8 km`). |
| `OperatorContinuation_SubtractSameUnit` | `[Fact]` | Tests multiline subtraction with identical units (`"10 kg\n- 3 kg"` $\rightarrow$ `7 kg`). |
| `OperatorContinuation_AddDifferentUnit_SameType` | `[Fact]` | Tests adding different units of the same category (`"5 km\n+ 3 miles"` $\rightarrow$ `~9.828 km`). |
| `ScaleOperator_Multiply` | `[Fact]` | Tests scaling a unit value using multiplication (`"5 km\n* 3"` $\rightarrow$ `15 km`). |
| `ScaleOperator_Divide` | `[Fact]` | Tests scaling a unit value using division (`"10 meters\n/ 2"` $\rightarrow$ `5 m`). |
| `OperatorContinuation_ThenConvert` | `[Fact]` | Tests performing math on units and then converting on a subsequent line (`"5 km\n+ 3 km\nto miles"` $\rightarrow$ `~4.97 miles`). |

---

### 6. Standalone Unit Tests

Tests detection and extraction of values from inputs that specify a value and unit without explicit conversion keywords.

| Method Name | Test Type | Description / Intent |
| :--- | :--- | :--- |
| `StandaloneUnit_DetectedAndDisplayed` | `[Theory]` | Verifies standalone strings like `"5 meters"`, `"100 kg"`, `"3.5 gallons"`, and `"9:30 min/mi"` correctly output their standardized unit symbols (`m`, `kg`, `gal`, `min/mi`). |
| `StandaloneUnit_CorrectNumericValue` | `[Theory]` | Validates numeric extraction accuracy for standalone unit inputs. |

---

### 7. Unit Category Tests

Broad validation across physical dimension types supported by `CalculationService`.

| Method Name | Test Type | Description / Intent |
| :--- | :--- | :--- |
| `AllUnitCategories_ConvertSuccessfully` | `[Theory]` | Tests successful evaluation across six unit categories: **Length** (meters, feet, miles, nautical miles), **Mass** (kg, lb, ounces, stone, ton, tonne), **Temperature** (Celsius, Fahrenheit, Kelvin), **Volume** (gallons, liters, cups, teaspoons, pints, quarts, fl oz), **Speed/Pace** (mph, km/h, m/s, knots, min/mi, min/km), and **Area** (acre, sq m, hectare, sq mi, sq ft). |

---

### 8. Ambiguity & Edge Case Tests

Tests precedence rules and potential naming collisions.

| Method Name | Test Type | Description / Intent |
| :--- | :--- | :--- |
| `VariableTakesPriorityOverUnit` | `[Fact]` | Ensures user-defined variables take priority over unit names (`"km = 10\n5 * km"` evaluates to `50`, not `"5 km"`). |
| `QuantityWords_StillWork` | `[Fact]` | Ensures text multiplier words are handled as numeric scales (`"5 million"` $\rightarrow$ `5,000,000`). |
| `DateTimeMath_TakesPriority` | `[Fact]` | Validates that expressions like `"today + 5 days"` prioritize date math over unit conversion engines. |
| `PlainNumbersStillWork` | `[Fact]` | Ensures plain math expressions (`"2 + 3"`) evaluate normally without unit side effects. |
| `MultipleConversions_TracksOutputNumbers` | `[Fact]` | Tests tracking of numeric outputs across multiple distinct single-line conversions in one execution. |
| `DominantUnit_SetCorrectly` | `[Fact]` | Verifies that `result.DominantUnit` is set to `"km"` when operating repeatedly on kilometers. |
| `DominantUnit_NullForPlainMath` | `[Fact]` | Verifies that `result.DominantUnit` is `null` when performing plain math without units. |

---

### 9. TryEvaluateUnitConversion Direct Tests

Tests the lower-level public method `CalculationService.TryEvaluateUnitConversion` directly rather than invoking `EvaluateExpressionsAsync`.

| Method Name | Test Type | Description / Intent |
| :--- | :--- | :--- |
| `TryEvaluateUnitConversion_DetectsCorrectly` | `[Theory]` | Asserts return value (`true` for conversion expressions like `"5 miles to km"`, `false` for non-conversion expressions like `"2 + 3"` or `"hello world"`). |
| `TryEvaluateUnitConversion_ContinuationWithoutPrevious_ReturnsFalse` | `[Fact]` | Asserts that `"to km"` returns `false` if `previous` unit context is `null`. |
| `TryEvaluateUnitConversion_ContinuationWithPrevious_ReturnsTrue` | `[Fact]` | Asserts that passing an explicit `CalculationService.UnitResult` object as context to `TryEvaluateUnitConversion` allows `"to km"` to succeed and produce converted output. |

---

### 10. Plural & Alias Tests

Validates naming flexibility and regional spelling variations.

| Method Name | Test Type | Description / Intent |
| :--- | :--- | :--- |
| `UnitAliases_AllResolveCorrectly` | `[Theory]` | Ensures singular, plural, and symbol forms resolve identically (e.g., `"meter"`, `"meters"`, `"m"`, `"foot"`, `"feet"`, `"ft"`). |
| `BritishSpellings_Work` | `[Theory]` | Confirms support for UK/International spellings (e.g., `"liter"`, `"litre"`, `"liters"`, `"litres"` all convert to `1000 mL`). |

---

## Dependencies & Key Types Tested

* **`CalculationService`**: Core service under test (`EvaluateExpressionsAsync`, `TryEvaluateUnitConversion`).
* **`CalculationResult`**: The return payload containing properties such as:
  * `Output` (`string`): The formatted calculation result string.
  * `OutputNumbers` (`List<double>`): Numeric values computed during evaluation.
  * `ErrorCount` (`int`): Count of parsing/evaluation errors.
  * `DominantUnit` (`string?`): Primary unit detected across expressions.
* **`CalculationService.UnitResult`**: Context structure passed to `TryEvaluateUnitConversion` containing unit metadata (`Value`, `Unit`, `QuantityName`, `Abbreviation`).