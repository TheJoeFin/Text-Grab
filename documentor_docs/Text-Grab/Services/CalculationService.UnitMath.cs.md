# Technical Documentation: `CalculationService.UnitMath.cs`

## Overview

The `CalculationService.UnitMath.cs` file is a partial class implementation of `CalculationService` within the `Text-Grab.Services` namespace. Its primary purpose is to process line-based unit conversions, unit-bearing arithmetic expressions, scaling operations, and running pace calculations. 

It leverages the **UnitsNet** library for general physical quantities and extends support for custom pace units (e.g., `min/mi`, `min/km`) and formatted output (e.g., feet and inches representation).

---

## Data Structures & Internal Types

### `UnitResult` (Public Class)
Represents the result of evaluating a unit-bearing line. Used to pass unit state between multi-line calculations.

| Property | Type | Description |
| :--- | :--- | :--- |
| `Value` | `double` | The numeric scalar value in terms of the current unit. |
| `Unit` | `System.Enum` | The UnitsNet or custom enum value representing the unit (e.g., `LengthUnit.Kilometer`). |
| `QuantityName` | `string` | The unit category (e.g., `"Length"`, `"Mass"`, `"Speed"`). |
| `Abbreviation` | `string` | Display abbreviation for output formatting (e.g., `"km"`, `"lb"`). |

### `UnitInfo` (Private Record Struct)
`private readonly record struct UnitInfo(Enum Unit, string QuantityName, string Abbreviation)`
Stores static metadata for a resolved unit lookup mapping.

### `PaceUnit` (Private Enum)
An internal enumeration representing runner pace units not natively covered by basic `UnitsNet` speed units:
* `MinutePerMile`
* `MinutePerKilometer`

### Constants
* `KilometersPerMile` (`double = 1.609344`): Exact conversion factor used for speed and pace conversions.

---

## Unit Lookup Table (`_unitLookup`)

A static, case-insensitive `Dictionary<string, UnitInfo>` (`StringComparer.OrdinalIgnoreCase`) mapping common unit names, plurals, and abbreviations to `UnitInfo` instances.

Supported Quantity Categories:
* **Length**: `m`, `meter(s)`, `cm`, `centimeter(s)`, `mm`, `millimeter(s)`, `km`, `kilometer(s)`, `in`, `inch(es)`, `ft`, `foot`, `feet`, `yd`, `yard(s)`, `mi`, `mile(s)`, `nmi`, `nautical mile(s)`.
* **Mass**: `g`, `gram(s)`, `kg`, `kilogram(s)`, `mg`, `milligram(s)`, `lb(s)`, `pound(s)`, `oz`, `ounce(s)`, `ton(s)`, `short ton(s)`, `tonne(s)`, `metric ton(s)`, `st`, `stone(s)`.
* **Temperature**: `celsius`, `°C`, `degC`, `C`, `fahrenheit`, `°F`, `degF`, `F`, `kelvin`.
* **Volume**: `liter(s)`, `litre(s)`, `L`, `mL`, `milliliter(s)`, `millilitre(s)`, `gal`, `gallon(s)`, `qt`, `quart(s)`, `pt`, `pint(s)`, `cup(s)`, `fl oz`, `floz`, `fluid ounce(s)`, `tbsp`, `tablespoon(s)`, `tsp`, `teaspoon(s)`.
* **Speed & Pace**: `mph`, `miles per hour`, `km/h`, `km/hr`, `kph`, `kilometers per hour`, `m/s`, `meters per second`, `knot(s)`, `kn`, `min/mi`, `min/mile`, `minute(s) per mile`, `min/km`, `minute(s) per kilometer`, `minute(s) per kilometre`.
* **Area**: `m²`, `sq m`, `square meter(s)`, `km²`, `sq km`, `square kilometer(s)`, `ft²`, `sq ft`, `square foot/feet`, `mi²`, `sq mi`, `square mile(s)`, `in²`, `sq in`, `square inch(es)`, `yd²`, `sq yd`, `square yard(s)`, `cm²`, `sq cm`, `acre(s)`, `ac`, `hectare(s)`, `ha`.

---

## Core Pipeline: `TryEvaluateUnitConversion`

```csharp
public bool TryEvaluateUnitConversion(
    string line,
    out string result,
    out UnitResult? unitResult,
    UnitResult? previousUnitResult)
```

Evaluates a single line of input text. It executes through a prioritized evaluation chain. If any stage succeeds, it returns `true` with the formatted string `result` and population of `unitResult`.

### Priority Execution Chain

```
Input Line
   │
   ├── 1. Previous result exists? ──> TryContinuationConversion ("to km", "in feet")
   │                                   │ (Success -> Return)
   │
   ├── 2. Previous result exists? ──> TryOperatorWithUnit ("+ 3 km", "- 5 miles")
   │                                   │ (Success -> Return)
   │
   ├── 3. Previous result exists? ──> TryScaleOperator ("* 2", "/ 3")
   │                                   │ (Success -> Return)
   │
   ├── 4. Explicit Conversion? ─────> TryExplicitConversion ("5 miles to km", "100°F in celsius")
   │                                   │ (Success -> Return)
   │
   └── 5. Standalone Unit? ─────────> TryStandaloneUnit ("5 meters")
                                       │ (Success -> Return)
                                       └── Failure -> Return false
```

---

## Execution Mechanics & Helper Methods

### 1. Continuation Conversion
* **Method**: `TryContinuationConversion`
* **Pattern**: `ContinuationConversionPattern` (`^(?:to|in)\s+(.+)$`)
* **Behavior**: Takes the `Value` and `Unit` from `previousUnitResult` and converts it to the target unit specified in the line.

### 2. Operator Continuation with Units
* **Method**: `TryOperatorWithUnit`
* **Pattern**: `OperatorWithUnitPattern` (`^(?<op>[+-])\s+(?<number>\d+\.?\d*)\s+(?<unit>.+)$`)
* **Behavior**:
  1. Validates that the operand unit belongs to the same unit family (`Unit.GetType()`).
  2. Converts the operand to the `previousUnitResult.Unit`.
  3. Performs addition or subtraction with `previousUnitResult.Value`.
  4. Preserves the target unit from `previousUnitResult`.

### 3. Scaling Operators
* **Method**: `TryScaleOperator`
* **Pattern**: `ScaleOperatorPattern` (`^(?<op>[*/])\s*(?<number>\d+\.?\d*)$`)
* **Behavior**:
  1. Multiplies or divides `previousUnitResult.Value` by the operand number.
  2. Prevents division by zero (`op == "/" && number == 0`).
  3. Retains the previous unit type and abbreviation.

### 4. Explicit Conversions
* **Method**: `TryExplicitConversion`
* **Patterns**:
  * Primary: `ToConversionPattern` (`^(.+?)\s+to\s+(.+)$`)
  * Fallback: `InConversionPattern` (`^(.+?)\s+in\s+(.+)$`)
* **Behavior**: Evaluates explicit source-to-target unit requests. `to` takes precedence over `in` to prevent standard inches (`in`) from triggering false conversions.

### 5. Standalone Units
* **Method**: `TryStandaloneUnit`
* **Behavior**:
  1. Parses standalone entries like `"5 meters"`.
  2. **Disambiguation Controls**:
     * Rejects single-character unit inputs (e.g., `"5 m"`) during standalone detection to prevent collision with variables or plain text words. Single-character units remain supported in explicit conversions (`5 m to ft`).
     * Rejects inputs if the trailing text matches an existing variable key inside `_parameters`.

---

## Unit Conversion Engine & Speed/Pace Calculation

### Standard Unit Conversions
`TryConvertUnitValue` processes standard conversions using `UnitsNet.Quantity`:
```csharp
IQuantity source = Quantity.From(value, sourceUnit);
IQuantity converted = source.ToUnit(targetUnit);
convertedValue = (double)converted.Value;
```

### Pace and Speed Conversion Engine
Because running pace (`min/mi`, `min/km`) is inversely proportional to speed, custom logic routes conversions involving `PaceUnit` through kilometers per hour ($km/h$) as an intermediate standard:

1. **Conversion to $km/h$ (`TryConvertToKilometersPerHour`)**:
   * `PaceUnit.MinutePerMile`: $\text{km/h} = \frac{60 \times 1.609344}{\text{pace}}$
   * `PaceUnit.MinutePerKilometer`: $\text{km/h} = \frac{60}{\text{pace}}$
   * `SpeedUnit`: Handled via `Speed.From(value, speedUnit).KilometersPerHour`

2. **Conversion from $km/h$ (`TryConvertFromKilometersPerHour`)**:
   * `PaceUnit.MinutePerMile`: $\text{pace} = \frac{60 \times 1.609344}{\text{km/h}}$
   * `PaceUnit.MinutePerKilometer`: $\text{pace} = \frac{60}{\text{km/h}}$
   * `SpeedUnit`: Handled via `Speed.FromKilometersPerHour(km/h).ToUnit(speedUnit).Value`

3. **Time-Based Pace Parsing (`TryParsePaceTimeValue`)**:
   * Accepts clock formats like `MM:SS` or `HH:MM:SS` (e.g., `9:30 min/mi`).
   * Converts hours and seconds into decimal minutes before passing to the conversion engine.

---

## Formatting Utilities

### `FormatUnitValue`
`private string FormatUnitValue(double value, string abbreviation)`
Formats standard outputs using the base class method `FormatResult(value)` combined with the target abbreviation (e.g., `"5 km"`).

### `FormatFeetAndInches`
`private static string FormatFeetAndInches(double totalFeet)`
Used whenever the target output unit is `LengthUnit.Foot`.
* Converts decimal feet to whole feet and rounded inches.
* Handles overflow (e.g., $11.6\text{ in} \rightarrow 12\text{ in} \rightarrow +1\text{ ft}$).
* Formats output as `X ft Y in` or `X ft` (if inches equal `0`). Supports negative values (e.g., `-6 ft 3 in`).

---

## Regular Expressions

All regular expressions are generated using C# source generators (`[GeneratedRegex]`):

| Method | Pattern | Regex Options | Purpose |
| :--- | :--- | :--- | :--- |
| `ContinuationConversionPattern` | `^(?:to\|in)\s+(.+)$` | `IgnoreCase` | Matches target conversions starting with `to` or `in`. |
| `ToConversionPattern` | `^(.+?)\s+to\s+(.+)$` | `IgnoreCase` | Matches explicit conversions using `to`. |
| `InConversionPattern` | `^(.+?)\s+in\s+(.+)$` | `IgnoreCase` | Matches explicit conversions using `in`. |
| `NumberWithUnitPattern` | `^(?<number>-?(?:\d+(?::\d{1,2}){1,2}\|\d+\.?\d*))\s*(?<unit>.+)$` | None | Extracts numeric/time prefix and unit suffix. |
| `OperatorWithUnitPattern` | `^(?<op>[+-])\s+(?<number>\d+\.?\d*)\s+(?<unit>.+)$` | `IgnoreCase` | Matches `+` or `-` operator, value, and unit. |
| `ScaleOperatorPattern` | `^(?<op>[*/])\s*(?<number>\d+\.?\d*)$` | None | Matches `*` or `/` scaling operators. |