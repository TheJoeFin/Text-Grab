# Technical Documentation: `Tests/GrabFrameUnfreezeTests.cs`

## Overview

The `GrabFrameUnfreezeTests` class contains unit tests designed to verify the conditional logic of the `GrabFrame.ShouldApplyUnfreezeResult` method from the `Text_Grab.Views` namespace. 

The primary purpose of this test suite is to ensure that unfreeze results are applied only when the transition state is current, active, and valid (i.e., matching transition versions, not in freeze mode, and not cleaned up).

---

## File Details

* **File Path:** `Tests/GrabFrameUnfreezeTests.cs`
* **Namespace:** `Tests`
* **Dependencies:** `Text_Grab.Views`

---

## Class Breakdown

### `GrabFrameUnfreezeTests`

A public unit test class containing data-driven theory tests for validating frame unfreeze state evaluation.

#### Test Method

##### `ShouldApplyUnfreezeResult_RequiresCurrentLiveTransition`

```csharp
[Theory]
[InlineData(3, 3, false, false, true)]
[InlineData(3, 4, false, false, false)]
[InlineData(3, 3, true, false, false)]
[InlineData(3, 3, false, true, false)]
public void ShouldApplyUnfreezeResult_RequiresCurrentLiveTransition(
    int transitionVersion,
    int currentTransitionVersion,
    bool isFreezeMode,
    bool isCleanedUp,
    bool expected)
```

* **Type:** Parameterized xUnit Test (`[Theory]`)
* **Target Method:** `Text_Grab.Views.GrabFrame.ShouldApplyUnfreezeResult(int, int, bool, bool)`
* **Description:** Verifies that `GrabFrame.ShouldApplyUnfreezeResult` returns the expected boolean value based on different combinations of transition versions and state flags.

##### Method Parameters:

1. **`transitionVersion`** (`int`): The version identifier of the transition being evaluated.
2. **`currentTransitionVersion`** (`int`): The current live transition version identifier.
3. **`isFreezeMode`** (`bool`): Indicates whether the frame is currently in freeze mode.
4. **`isCleanedUp`** (`bool`): Indicates whether the frame state has been cleaned up.
5. **`expected`** (`bool`): The expected return value from `GrabFrame.ShouldApplyUnfreezeResult`.

---

## Test Data Matrix (`InlineData`)

The method tests four distinct state scenarios:

| Case | `transitionVersion` | `currentTransitionVersion` | `isFreezeMode` | `isCleanedUp` | `expected` | Reasoning / Condition Tested |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **1** | `3` | `3` | `false` | `false` | `true` | **Valid Unfreeze:** Versions match, freeze mode is off, and the instance is not cleaned up. |
| **2** | `3` | `4` | `false` | `false` | `false` | **Outdated Transition:** Transition version (`3`) does not match the current version (`4`). |
| **3** | `3` | `3` | `true` | `false` | `false` | **In Freeze Mode:** Cannot apply unfreeze result while `isFreezeMode` is `true`. |
| **4** | `3` | `3` | `false` | `true` | `false` | **Already Cleaned Up:** Cannot apply unfreeze result when `isCleanedUp` is `true`. |

---

## How It Works

1. The xUnit test framework executes `ShouldApplyUnfreezeResult_RequiresCurrentLiveTransition` for each `[InlineData]` attribute set.
2. The parameters are passed directly into the static method `GrabFrame.ShouldApplyUnfreezeResult(...)`.
3. `Assert.Equal(expected, ...)` compares the actual boolean output of `GrabFrame.ShouldApplyUnfreezeResult` against the `expected` parameter defined in the test case.