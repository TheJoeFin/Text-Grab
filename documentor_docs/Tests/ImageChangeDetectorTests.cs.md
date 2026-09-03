# Technical Documentation: `Tests/ImageChangeDetectorTests.cs`

## Overview

The `ImageChangeDetectorTests` class is a unit test suite written in C# using xUnit. Its primary purpose is to verify the behavior and functionality of the `ImageChangeDetector` class (and its static utilities) located in the `Text_Grab.Utilities` namespace.

This test suite validates image change detection mechanics, state reset behavior, transient frame filtering, and static image comparison threshold logic.

---

## File Details

- **File Path**: `Tests/ImageChangeDetectorTests.cs`
- **Namespace**: `Tests`
- **Dependencies**:
  - `System.Drawing`
  - `Text_Grab.Utilities`
  - xUnit Testing Framework (`Fact`, `Assert`)

---

## Class Constants

The test suite relies on two local sample image relative file paths:

| Constant Name | Path | Description |
| :--- | :--- | :--- |
| `fontTestPath` | `".\\Images\\FontTest.png"` | Path to the primary sample test image. |
| `fontSamplePath` | `".\\Images\\font_sample.png"` | Path to a secondary sample test image distinct from `FontTest.png`. |

---

## Tested Functionality & Methods

The test suite exercises the following API surface of `ImageChangeDetector`:
1. `CheckForChangeAndUpdate(Bitmap image)` (Instance method)
2. `Reset()` (Instance method)
3. `ImagesDifferBeyondThreshold(Bitmap image1, Bitmap image2)` (Static method)

---

## Test Cases

### 1. `FirstCapture_EstablishesBaseline_ReportsNoChange()`

* **Purpose**: Verifies that the initial image processed by an `ImageChangeDetector` instance acts as the baseline and returns `false`.
* **Flow**:
  1. Instantiates a new `ImageChangeDetector`.
  2. Loads a `Bitmap` from `fontTestPath` using `FileUtilities.GetPathToLocalFile()`.
  3. Executes `detector.CheckForChangeAndUpdate(image)`.
* **Assertion**: Asserts that `CheckForChangeAndUpdate` returns `false`.

---

### 2. `SameCapture_ReportsNoChange()`

* **Purpose**: Verifies that submitting the same image repeatedly after setting a baseline results in no change reported.
* **Flow**:
  1. Instantiates `ImageChangeDetector`.
  2. Loads `image` from `fontTestPath`.
  3. Calls `CheckForChangeAndUpdate(image)` once to establish the baseline.
  4. Calls `CheckForChangeAndUpdate(image)` a second time.
* **Assertion**: Asserts that the second call returns `false`.

---

### 3. `DifferentCapture_ReportsChange_OnceItHoldsForTwoChecks()`

* **Purpose**: Verifies that a change is not reported immediately upon detecting a new image; it must persist across two consecutive checks to be considered stable and reported as a change.
* **Flow**:
  1. Loads `image1` (`fontTestPath`) and `image2` (`fontSamplePath`).
  2. Calls `CheckForChangeAndUpdate(image1)` (Establishes baseline).
  3. Calls `CheckForChangeAndUpdate(image2)` for the first time.
  4. Calls `CheckForChangeAndUpdate(image2)` for the second consecutive time.
* **Assertions**:
  * The first check with `image2` returns `false` (change is pending/unstable).
  * The second check with `image2` returns `true` (change is confirmed).

---

### 4. `TransientCapture_DoesNotReportChange()`

* **Purpose**: Ensures transient single-frame image differences (such as a temporary visual glitch or flash indicator) do not trigger a change notification if the detector reverts back to the baseline image on the next check.
* **Flow**:
  1. Loads `image1` (`fontTestPath`) and `image2` (`fontSamplePath`).
  2. Baseline established with `image1`.
  3. Submits `image2` once (transient change).
  4. Reverts back and submits `image1` twice.
* **Assertions**:
  * `CheckForChangeAndUpdate(image2)` returns `false`.
  * Subsequent `CheckForChangeAndUpdate(image1)` calls return `false`.

---

### 5. `Reset_NextCaptureBecomesBaseline_ReportsNoChange()`

* **Purpose**: Tests that invoking `Reset()` clears the detector state, causing the subsequent image capture to establish a new baseline rather than reporting a change.
* **Flow**:
  1. Baseline established with `image1`.
  2. Calls `detector.Reset()`.
  3. Calls `CheckForChangeAndUpdate(image2)`.
* **Assertion**: Asserts that checking `image2` immediately after a reset returns `false`.

---

### 6. `ImagesDifferBeyondThreshold_IdenticalImages_ReportsNoDifference()`

* **Purpose**: Validates the static `ImagesDifferBeyondThreshold` method when comparing two separate image instances created from the same source file.
* **Flow**:
  1. Loads two separate `Bitmap` instances from `fontTestPath`.
  2. Calls `ImageChangeDetector.ImagesDifferBeyondThreshold(image1, image2)`.
* **Assertion**: Asserts that the method returns `false`.

---

### 7. `ImagesDifferBeyondThreshold_DifferentImages_ReportsDifference()`

* **Purpose**: Validates the static `ImagesDifferBeyondThreshold` method when comparing two visually distinct images.
* **Flow**:
  1. Loads `image1` (`fontTestPath`) and `image2` (`fontSamplePath`).
  2. Calls `ImageChangeDetector.ImagesDifferBeyondThreshold(image1, image2)`.
* **Assertion**: Asserts that the method returns `true`.