# Technical Documentation: `KeyboardExtensions.cs`

## Overview

The `KeyboardExtensions.cs` file provides utility methods and an enumeration to query the physical state of keyboard modifier keys (Control, Alt, and Shift) within the `Text_Grab` application. 

It abstracts WPF keyboard state checks (`System.Windows.Input.Keyboard`) into simple boolean methods and aggregates these states into a unified enum representation via `GetKeyboardModifiersDown()`.

---

## Namespace & Dependencies

- **Namespace:** `Text_Grab`
- **Dependencies:** `System.Windows.Input` (Provides the WPF `Keyboard` class and `Key` enumeration for state querying).

---

## Enumeration: `KeyboardModifiersDown`

An integer-backed enumeration representing the exact combination of active modifier keys currently pressed.

```csharp
public enum KeyboardModifiersDown
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 3,
    ShiftCtrl = 4,
    ShiftAlt = 5,
    CtrlAlt = 6,
    ShiftCtrlAlt = 7
}
```

### Enum Values
| Value Name | Integer Value | Description |
| :--- | :--- | :--- |
| `None` | `0` | No modifier keys are pressed. |
| `Shift` | `1` | Only the Shift key is pressed. |
| `Ctrl` | `2` | Only the Control key is pressed. |
| `Alt` | `3` | Only the Alt key is pressed. |
| `ShiftCtrl` | `4` | Both Shift and Control keys are pressed. |
| `ShiftAlt` | `5` | Both Shift and Alt keys are pressed. |
| `CtrlAlt` | `6` | Both Control and Alt keys are pressed. |
| `ShiftCtrlAlt` | `7` | All three modifier keys (Shift, Control, Alt) are pressed. |

---

## Class: `KeyboardExtensions`

`public static class KeyboardExtensions`

A static utility class offering static helper methods to detect modifier key states.

### Methods

#### 1. Single Modifier Methods

These methods check whether either the left or right variant of a given modifier key is currently down using `Keyboard.IsKeyDown(...)`.

* **`bool IsCtrlDown()`**
  * **Returns:** `true` if `Key.LeftCtrl` or `Key.RightCtrl` is pressed; otherwise, `false`.
* **`bool IsAltDown()`**
  * **Returns:** `true` if `Key.LeftAlt` or `Key.RightAlt` is pressed; otherwise, `false`.
* **`bool IsShiftDown()`**
  * **Returns:** `true` if `Key.LeftShift` or `Key.RightShift` is pressed; otherwise, `false`.

---

#### 2. Combined Modifier Methods

These expression-bodied methods combine the single modifier methods to check for specific key combinations.

* **`bool IsCtrlAltDown()`**
  * **Returns:** `true` if both Control and Alt are pressed (`IsCtrlDown() && IsAltDown()`).
* **`bool IsShiftCtrlDown()`**
  * **Returns:** `true` if both Control and Shift are pressed (`IsCtrlDown() && IsShiftDown()`).
* **`bool IsShiftAltDown()`**
  * **Returns:** `true` if both Shift and Alt are pressed (`IsShiftDown() && IsAltDown()`).
* **`bool IsShiftCtrlAltDown()`**
  * **Returns:** `true` if Shift, Control, and Alt are all simultaneously pressed (`IsShiftDown() && IsCtrlDown() && IsAltDown()`).

---

#### 3. State Aggregation Method

* **`KeyboardModifiersDown GetKeyboardModifiersDown()`**
  * **Returns:** A `KeyboardModifiersDown` enum value corresponding to the current combination of active modifier keys.
  * **Execution Logic:** Evaluates conditions in order from most restrictive (3 keys) down to single key checks, returning as soon as a matching condition is met:
    1. If `IsShiftCtrlAltDown()` is true $\rightarrow$ Returns `KeyboardModifiersDown.ShiftCtrlAlt`
    2. If `IsShiftAltDown()` is true $\rightarrow$ Returns `KeyboardModifiersDown.ShiftAlt`
    3. If `IsCtrlAltDown()` is true $\rightarrow$ Returns `KeyboardModifiersDown.CtrlAlt`
    4. If `IsShiftCtrlDown()` is true $\rightarrow$ Returns `KeyboardModifiersDown.ShiftCtrl`
    5. If `IsShiftDown()` is true $\rightarrow$ Returns `KeyboardModifiersDown.Shift`
    6. If `IsCtrlDown()` is true $\rightarrow$ Returns `KeyboardModifiersDown.Ctrl`
    7. If `IsAltDown()` is true $\rightarrow$ Returns `KeyboardModifiersDown.Alt`
    8. Default $\rightarrow$ Returns `KeyboardModifiersDown.None`