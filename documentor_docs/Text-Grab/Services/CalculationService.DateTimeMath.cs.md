# Documentation: `CalculationService.DateTimeMath.cs`

## Overview

The `CalculationService.DateTimeMath.cs` file is a `partial class` implementation of `CalculationService` in the `Text_Grab.Services` namespace. It provides capability for parsing, evaluating, converting, and formatting date, time, and duration math expressions from text lines.

### Key Capabilities
- **Date/Time Arithmetic**: Perform additions and subtractions on dates using time units (e.g., `"March 10th + 10 days"`, `"2/25/26 11:02pm + 800 mins"`, `"today + 5 weeks 3 days 8 hours"`).
- **Sequential Line Context**: Evaluate expressions starting with arithmetic operators by passing a base `DateTime` from previous calculations (e.g., `"+ 2 weeks"`).
- **Date Subtraction**: Evaluate the difference between two dates (e.g., `"March 10th - January 1st"` or `"today - yesterday in weeks"`).
- **Duration Unit Conversions**: Convert standalone duration values into target units (e.g., `"3.6 years to days"`).

---

## Constants & Data Structures

### Constants
The class defines average unit conversion multipliers for date math estimations:

| Constant | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `AverageDaysPerMonth` | `double` | `30.44` | Average number of days per month used for fractional month calculations. |
| `AverageDaysPerYear` | `double` | `365.25` | Average number of days per year used for fractional year/decade calculations. |
| `HoursPerDay` | `double` | `24d` | Total hours in a standard day. |
| `MinutesPerDay` | `double` | `HoursPerDay * 60d` (`1440`) | Total minutes in a standard day. |
| `SecondsPerDay` | `double` | `MinutesPerDay * 60d` (`86400`) | Total seconds in a standard day. |

### Structs

#### `DurationUnitInfo`
A `readonly record struct` used to represent dynamic duration conversions:
```csharp
private readonly record struct DurationUnitInfo(string SingularName, string PluralName, double DaysPerUnit);
```
- **`SingularName`**: Display name for single-unit results (e.g., `"day"`).
- **`PluralName`**: Display name for multi-unit results (e.g., `"days"`).
- **`DaysPerUnit`**: Scaling factor defining how many days equal one unit.

---

## Public Methods

### `TryEvaluateDateTimeMath(string, out string)`
Overload that attempts to evaluate a date/time expression string.

```csharp
public static bool TryEvaluateDateTimeMath(string line, out string result)
```
- **Parameters**:
  - `line`: The raw input string expression to evaluate.
  - `result`: The evaluated and formatted result string if successful; otherwise `string.Empty`.
- **Returns**: `bool` — `true` if the line was successfully evaluated; otherwise `false`.

---

### `TryEvaluateDateTimeMath(string, out string, out DateTime?, DateTime?)`
Overload that supports contextual chained operations using a baseline `DateTime` object.

```csharp
public static bool TryEvaluateDateTimeMath(
    string line, 
    out string result, 
    out DateTime? parsedDateTime, 
    DateTime? baseDateTime)
```
- **Parameters**:
  - `line`: The raw input string expression.
  - `result`: The evaluated and formatted result string if successful.
  - `parsedDateTime`: Output parameter storing the resulting `DateTime` object (used as `baseDateTime` for subsequent line evaluation).
  - `baseDateTime`: An optional `DateTime` value to use if the expression starts directly with an arithmetic operator (e.g., `"+ 5 days"`).
- **Returns**: `bool` — `true` if evaluation succeeded; otherwise `false`.

---

## Core Evaluation Pipelines

Evaluation flows through `TryEvaluateDateTimeMath` with internal parameters:

```
                  Input String
                       │
          ┌────────────┴────────────┐
          ▼                         ▼
TryEvaluateDateSubtraction   TryEvaluateDurationConversion
 (e.g., "date1 - date2")       (e.g., "3.6 years to days")
          │                         │
     [Success?]                [Success?]
     ├── Yes ──> Return        ├── Yes ──> Return
     └── No  ──> Continue      └── No  ──> Continue
          │
          ▼
DateTime Arithmetic Pipeline
 (e.g., "March 10th + 5 days - 2 hours")
```

### 1. Date Subtraction Pipeline (`TryEvaluateDateSubtraction`)
Calculates the difference between two date instances.
1. Checks for a target requested unit specified with `to` or `in` suffix via `TryExtractRequestedDurationUnit` (e.g., `"March 10th - Jan 1st in weeks"`).
2. Splits the input line at `" - "` using `DateSubtractionSplitPattern()`.
3. Parses both operands via `TryParseFlexibleDate`.
4. **Calculations**:
   - If a target unit is specified: Converts total calculated days difference via `ConvertDurationValue` and formats numeric result using `FormatDurationValue`.
   - If no target unit is specified: Formats the duration breakdown into a human-readable string (e.g., `"2 weeks 3 days 2 hours"`) via `FormatTimeSpanHumanReadable`.

---

### 2. Duration Conversion Pipeline (`TryEvaluateDurationConversion`)
Converts standalone values from one duration unit to another.
1. Matches inputs using `ToConversionPattern()` or `InConversionPattern()`.
2. Extracts source portion (value + unit) and target unit text.
3. Parses number and unit via `TryParseDurationValueAndUnit` and `TryResolveDurationUnit`.
4. Calculates conversion using `ConvertDurationValue`:
   $$\text{ConvertedValue} = \frac{\text{Value} \times \text{SourceUnit.DaysPerUnit}}{\text{TargetUnit.DaysPerUnit}}$$
5. Formats value with appropriate singular/plural unit string using `FormatDurationValue`.

---

### 3. Date/Time Arithmetic Pipeline
Handles addition and subtraction of mixed duration units on an initial base date.

1. **Anchor Detection**: Locates the first explicit `+` or `-` operator via `DateTimeArithmeticPattern()`.
2. **Date Extraction & Parsing**:
   - Everything preceding the match anchor is treated as the baseline `datePart`.
   - If `datePart` is empty, uses `baseDateTime` if present; otherwise defaults to `DateTime.Today`.
   - If `datePart` exists, parses it via `TryParseFlexibleDate`.
3. **Segment Processing**:
   - Matches all remaining segments after the anchor via `DateTimeDurationSegmentPattern()`.
   - Operations inherit the previous operator if an explicit operator is omitted in compound expressions (e.g., `+ 5 weeks 3 days` sets operator `+` for both `5 weeks` and `3 days`).
   - Applies sign flip if operator is `-`.
4. **Time & Fractional Defaults**:
   - If non-integer day/year/month offsets exist without explicit initial time components, defaults starting time to **12:00 PM (noon)** (`dateTime.AddHours(12)`).
5. **Applying Offsets**: Loops through all extracted operations and executes `ApplyDateTimeOffset`.
6. **Formatting**: Formats output display string based on system culture and presence of time details (`FormatDateTimeResult`).

---

## Private Helper Methods

### Date & Time Parsing Helpers

#### `TryParseFlexibleDate(string input, out DateTime dateTime, out bool hasTime)`
Parses strings into a valid `DateTime` instance:
- **Keywords**: Handles `today` (`DateTime.Today`), `now` (`DateTime.Now`), `tomorrow` (`DateTime.Today.AddDays(1)`), and `yesterday` (`DateTime.Today.AddDays(-1)`).
- **Ordinal Suffix Removal**: Replaces `1st`, `2nd`, `3rd`, `4th`, etc., with digit-only values using `OrdinalSuffixPattern()`.
- **Time Component Detection**: Detects explicit times using `HasTimeComponent()`.
- **Culture Parsing**: Performs `DateTime.TryParse` using `CultureInfo.CurrentCulture`.

#### `HasTimeComponent(string input)`
Evaluates whether input text contains explicit time syntax:
- Matches AM/PM markers (`10am`, `10 am`, `10p.m.`) via `AmPmPattern()`.
- Matches colon time patterns (`11:02`, `14:30`) via `ColonTimePattern()`.

---

### Date Manipulation Helpers

#### `ApplyDateTimeOffset(DateTime dateTime, double number, string unit)`
Dispatches math operations based on the duration unit string:
- `"decade"` / `"decades"` $\rightarrow$ Adds fractional years (`number * 10`).
- `"year"` / `"years"` $\rightarrow$ Adds fractional years via `AddFractionalYears`.
- `"month"` / `"months"` $\rightarrow$ Adds fractional months via `AddFractionalMonths`.
- `"week"` / `"weeks"` $\rightarrow$ `dateTime.AddDays(number * 7)`.
- `"day"` / `"days"` $\rightarrow$ `dateTime.AddDays(number)`.
- `"hour"` / `"hours"` / `"hr"` / `"hrs"` $\rightarrow$ `dateTime.AddHours(number)`.
- `"minute"` / `"minutes"` / `"min"` / `"mins"` $\rightarrow$ `dateTime.AddMinutes(number)`.

#### `AddFractionalYears(DateTime dateTime, double years)`
Applies whole years using `DateTime.AddYears`, and adds remainder fractional years as days multiplied by `AverageDaysPerYear` (`365.25`).

#### `AddFractionalMonths(DateTime dateTime, double months)`
Applies whole months using `DateTime.AddMonths`, and adds remainder fractional months as days multiplied by `AverageDaysPerMonth` (`30.44`).

---

### Formatting Helpers

#### `FormatDateTimeResult(DateTime dateTime, bool includeTime)`
- Formats date according to `CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern`.
- If `includeTime` is true, appends 12-hour formatted time in lowercase (e.g., `10/24/2026 3:45pm`).

#### `FormatTimeSpanHumanReadable(DateTime earlier, DateTime later)`
Calculates precise differences between two dates and formats output down to non-zero components:
- Subtracts whole years, months, weeks, days, hours, minutes, and seconds incrementally.
- Outputs human-readable string combining units (e.g., `"1 year 2 months 3 days"`). Returns `"0 seconds"` if difference is 0.

#### `FormatDurationNumber(double value)`
- Formats integer-equivalent numbers without decimal points using format `"N0"`.
- Formats fractional numbers up to 3 decimal places using format `"#,##0.###"`.

---

### Conversion & Unit Helpers

#### `TryResolveDurationUnit(string unitText, out DurationUnitInfo unit)`
Maps recognized unit text string aliases to a `DurationUnitInfo` struct:

| Unit Alias | Singular | Plural | Days Per Unit |
| :--- | :--- | :--- | :--- |
| `decade(s)` | `decade` | `decades` | $365.25 \times 10 = 3652.5$ |
| `year(s)` | `year` | `years` | $365.25$ |
| `month(s)` | `month` | `months` | $30.44$ |
| `week(s)` | `week` | `weeks` | $7$ |
| `day(s)` | `day` | `days` | $1$ |
| `hour(s)`, `hr(s)` | `hour` | `hours` | $1 / 24 \approx 0.0416667$ |
| `minute(s)`, `min(s)`| `minute` | `minutes` | $1 / 1440 \approx 0.0006944$ |
| `second(s)`, `sec(s)`| `second` | `seconds` | $1 / 86400 \approx 0.00001157$ |

---

## Generated Regular Expressions

The source file uses C# Source Generators (`[GeneratedRegex]`) for high-performance pattern matching:

```csharp
// Matches initial arithmetic operator, number, and duration unit
[GeneratedRegex(@"(?<op>[+-])\s*(?<number>\d+\.?\d*)\s*(?<unit>decades?|years?|months?|weeks?|days?|hours?|hrs?|hr|minutes?|mins?|min)\b", RegexOptions.IgnoreCase)]
private static partial Regex DateTimeArithmeticPattern();

// Matches repeated duration segments with optional operator inheritance
[GeneratedRegex(@"(?<op>[+-])?\s*(?<number>\d+\.?\d*)\s*(?<unit>decades?|years?|months?|weeks?|days?|hours?|hrs?|hr|minutes?|mins?|min)\b", RegexOptions.IgnoreCase)]
private static partial Regex DateTimeDurationSegmentPattern();

// Matches ordinal suffixes on dates (e.g., 1st, 2nd, 3rd)
[GeneratedRegex(@"(\d+)(?:st|nd|rd|th)\b", RegexOptions.IgnoreCase)]
private static partial Regex OrdinalSuffixPattern();

// Matches AM/PM indicators in time strings
[GeneratedRegex(@"\d\s*[aApP]\.?[mM]\.?(?:\s|$|[^a-zA-Z])")]
private static partial Regex AmPmPattern();

// Matches HH:MM formatted times
[GeneratedRegex(@"\d{1,2}:\d{2}")]
private static partial Regex ColonTimePattern();

// Matches explicit subtraction delimiter (" - ") between two dates
[GeneratedRegex(@"\s+-\s+")]
private static partial Regex DateSubtractionSplitPattern();

// Matches target requested unit extensions in expressions (e.g. "... in weeks")
[GeneratedRegex(@"^(?<body>.+?)\s+(?:to|in)\s+(?<unit>decades?|years?|months?|weeks?|days?|hours?|hrs?|hr|minutes?|mins?|min|seconds?|secs?|sec)\s*$", RegexOptions.IgnoreCase)]
private static partial Regex DateSubtractionTargetUnitPattern();

// Matches single duration value-and-unit strings (e.g., "3.6 years")
[GeneratedRegex(@"^(?<number>[-+]?(?:\d[\d,._ ]*\d|\d))\s*(?<unit>decades?|years?|months?|weeks?|days?|hours?|hrs?|hr|minutes?|mins?|min|seconds?|secs?|sec)\s*$", RegexOptions.IgnoreCase)]
private static partial Regex DurationValuePattern();
```