# Technical Documentation: `NumberExtensions.cs`

## Overview

The `NumberExtensions.cs` file provides utility extension methods for numerical data types and collections within the `Text_Grab` namespace. Currently, it contains a static extension method for `IEnumerable<double>` that calculates the sample standard deviation of a collection of numbers using a single-pass algorithm.

---

## File Metadata

* **File Path:** `Text-Grab/Extensions/NumberExtensions.cs`
* **Namespace:** `Text_Grab`
* **Dependencies:**
  * `System`
  * `System.Collections.Generic`

---

## Class Architecture

### `NumberExtensions`

```csharp
public static class NumberExtensions
```

A `public static` utility class designed to hold extension methods operating on numerical types and collections.

---

## Methods

### `StdDev`

```csharp
public static double StdDev(this IEnumerable<double> values)
```

Calculates the sample standard deviation of an `IEnumerable<double>` sequence using a numerically stable, single-pass algorithm (Welford's algorithm).

#### Parameters

| Parameter | Type | Description |
| :--- | :--- | :--- |
| `values` | `this IEnumerable<double>` | The sequence of `double` floating-point numbers to calculate standard deviation for. |

#### Return Value

* **Type:** `double`
* **Returns:** The sample standard deviation of the input sequence. Returns `0.0` if the collection contains 1 or fewer elements.

---

## Detailed Logic Breakdown

The `StdDev` method processes the collection in a single pass to compute the mean and sum of squared differences from the mean incrementally:

1. **Initialization:**
   * `mean` (`double`): Tracks the running mean, initialized to `0.0`.
   * `sum` (`double`): Tracks the running sum of squared differences from the mean, initialized to `0.0`.
   * `stdDev` (`double`): Holds the computed standard deviation, initialized to `0.0`.
   * `n` (`int`): Counter for the total number of elements processed, initialized to `0`.

2. **Iteration Loop:**
   For each value `val` in `values`:
   * Increment `n` by `1`.
   * Calculate `delta = val - mean`.
   * Update running mean: `mean += delta / n`.
   * Update squared distance sum: `sum += delta * (val - mean)` *(Note: `val - mean` uses the updated mean value)*.

3. **Standard Deviation Calculation:**
   * Checks if `1 < n` (i.e., the collection contains 2 or more elements).
   * If true, calculates sample standard deviation using Bessel's correction:
     $$\text{stdDev} = \sqrt{\frac{\text{sum}}{n - 1}}$$
     Implemented as: `stdDev = Math.Sqrt(sum / (n - 1));`
   * If `n <= 1`, the standard deviation cannot be computed for a sample size of 0 or 1, so `stdDev` remains `0.0`.

4. **Return:**
   * Returns `stdDev`.

---

## External References Cited in Code

The implementation includes the following inline source citations:
* Stack Overflow Answer: `https://stackoverflow.com/a/2878000/7438031` (Read on 1/27/2022)
* Algorithm Article: `http://warrenseen.com/blog/2006/03/13/how-to-calculate-standard-deviation/`