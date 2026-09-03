# Technical Documentation: `Tests/BenchmarkRunnerTests.cs`

## Overview

The `Tests/BenchmarkRunnerTests.cs` file defines a unit test wrapper designed to trigger performance benchmarks using the **BenchmarkDotNet** framework across the entire `Tests` assembly. 

By default, the benchmark execution method is explicitly skipped during automated test runs and is intended to be executed manually on demand.

---

## File Details

* **File Path:** `Tests/BenchmarkRunnerTests.cs`
* **Namespace:** `Tests`
* **Class:** `BenchmarkRunnerTests`

---

## External Dependencies

* **`BenchmarkDotNet.Running`**: Provides the `BenchmarkRunner` class used to execute benchmarks against target assemblies or classes.
* **xUnit Framework** *(implied by the `[Fact]` attribute)*: Used to expose the method as a test runner entry point.

---

## Class and Member Breakdown

### `BenchmarkRunnerTests` Class

```csharp
public class BenchmarkRunnerTests
```
A public class that serves as a container for the benchmark execution test method.

---

### `RunAllBenchmarks` Method

```csharp
[Fact(Skip = "Manual: run benchmarks on demand only.")]
public void RunAllBenchmarks()
{
    // Runs all benchmarks in the Tests assembly. Intended to be invoked manually.
    _ = BenchmarkRunner.Run(typeof(BenchmarkRunnerTests).Assembly);
}
```

#### Key Components:

1. **`[Fact(Skip = "Manual: run benchmarks on demand only.")]` Attribute**
   * Marks `RunAllBenchmarks()` as an xUnit test method.
   * Sets the `Skip` property with the reason `"Manual: run benchmarks on demand only."`.
   * **Purpose:** Ensures continuous integration (CI/CD) pipelines and regular automated unit test runs skip this method, preventing long-running benchmark executions during normal testing.

2. **`typeof(BenchmarkRunnerTests).Assembly`**
   * Retrieves the `Assembly` object in which `BenchmarkRunnerTests` is defined (the `Tests` assembly).

3. **`BenchmarkRunner.Run(...)`**
   * Calls BenchmarkDotNet's static `Run` method, passing the `Tests` assembly.
   * Scans the assembly for any classes containing benchmark annotations and executes them.

4. **Discard Assignment (`_ =`)**
   * Uses the C# discard pattern (`_`) to explicitly ignore the return value (`Summary` object) returned by `BenchmarkRunner.Run`.

---

## How It Works

1. **Test Discovery:** An xUnit test runner discovers `RunAllBenchmarks()` via the `[Fact]` attribute.
2. **Execution Skipping:** The runner sees the `Skip` parameter and skips automatic execution.
3. **Manual Trigger:** When a developer manually invokes this test (overriding the skip setting in an IDE or via CLI parameters):
   * The method identifies its containing assembly (`typeof(BenchmarkRunnerTests).Assembly`).
   * `BenchmarkRunner.Run` scans the assembly for performance benchmarks and executes them.