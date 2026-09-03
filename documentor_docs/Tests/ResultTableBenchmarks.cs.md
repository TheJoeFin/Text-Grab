# Technical Documentation: `Tests/ResultTableBenchmarks.cs`

## Overview

The `ResultTableBenchmarks` class is a BenchmarkDotNet performance test suite located in the `Tests.Benchmarks` namespace. Its primary purpose is to measure the execution performance and memory allocations of table structure analysis and text extraction algorithms provided by the `ResultTable` model in the `Text_Grab` project.

## Class Metadata

* **File Path:** `Tests/ResultTableBenchmarks.cs`
* **Namespace:** `Tests.Benchmarks`
* **Attributes:**
  * `[MemoryDiagnoser]`: Enables BenchmarkDotNet to track and report memory allocations and garbage collection activity alongside execution time.

---

## Key Components

### Fields

| Field Name | Type | Description |
| :--- | :--- | :--- |
| `_syntheticBorders` | `List<WordBorderInfo>` | Stores a generated list of synthetic bounding box and text word information representing OCR outputs. |
| `_resultTable` | `ResultTable` | An instance of `ResultTable` pre-analyzed during benchmark setup. |
| `_canvas` | `Rectangle` | A `System.Drawing.Rectangle` representing the total bounds containing the generated table content. |
| `_sb` | `StringBuilder` | A reusable `StringBuilder` instance used to accumulate text output during benchmarks without extra allocations. |

---

## Setup Logic (`[GlobalSetup]`)

### `Setup()` Method

The `Setup` method is executed once prior to benchmark executions. It constructs a deterministic synthetic layout that mimics an OCR text result organized in a grid.

#### Layout Generation Parameters:
* **Grid Dimensions:** 50 rows $\times$ 6 columns.
* **Cell Metrics:**
  * Average word width (`colW`): `110`
  * Average word height (`rowH`): `24`
  * Token gap within cells (`gapX`): `16`
  * Column gap (`gapCol`): `60`
  * Row gap (`gapRow`): `14`
  * Initial offset (`x0`, `y0`): `10, 10`
* **Random Seed:** Uses `new Random(42)` for repeatable results across test runs.

#### Workflow:
1. **Word Generation:**
   * Iterates through 50 rows and 6 columns.
   * Creates contextual strings depending on column position:
     * Last column ($c = 5$): Formats numerical values/percentages (e.g., `"55 %"`, `"48%"`, or integer strings).
     * First column ($c = 0$): Formats row labels (e.g., `"Row00"`).
     * Other columns: Formats cell value identifiers (e.g., `"Val00_01"`).
2. **Tokenization and Rect Construction:**
   * Splits each string on space characters to simulate multi-token grid cells.
   * Calculates a `System.Windows.Rect` for each token, assigning coordinates based on current row/column offsets and calculated widths based on character length (`Math.Max(12, token.Length * 7)`).
   * Populates `_syntheticBorders` with `WordBorderInfo` instances containing the token string and its bounding `BorderRect`.
3. **Canvas Setup:**
   * Calculates total canvas dimensions based on grid parameters with extra padding and assigns it to `_canvas`.
4. **Warm-up Analysis:**
   * Initializes `_resultTable` and executes `AnalyzeAsTable(_syntheticBorders, _canvas, drawTable: false)` to warm up execution paths.

---

## Benchmark Methods

### 1. `AnalyzeAsTable_Baseline()`

* **Attribute:** `[Benchmark]`
* **Return Type:** `int`

#### Description
Measures the full performance pipeline of table detection/analysis on an un-analyzed input dataset.

#### Execution Process:
1. Performs a shallow copy of `_syntheticBorders` into a new `List<WordBorderInfo>` (`copy`), duplicating `WordBorderInfo` objects with identical `Word` and `BorderRect` properties to simulate newly generated OCR output without sharing references.
2. Instantiates a new `ResultTable` object `rt`.
3. Calls `rt.AnalyzeAsTable(copy, _canvas, drawTable: false)`.
4. Returns the sum of detected rows and columns (`rt.Rows.Count + rt.Columns.Count`).

---

### 2. `GetTextFromTabledWordBorders_Baseline()`

* **Attribute:** `[Benchmark]`
* **Return Type:** `int`

#### Description
Measures the baseline performance of extracting formatted text from structured table word borders.

#### Execution Process:
1. Clears the instance `StringBuilder` (`_sb.Clear()`).
2. Invokes the static method `ResultTable.GetTextFromTabledWordBorders(_sb, _syntheticBorders, isSpaceJoining: true)`.
3. Returns the length of the string populated within `_sb` (`_sb.Length`).