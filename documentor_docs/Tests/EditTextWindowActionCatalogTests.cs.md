# Technical Documentation: `Tests/EditTextWindowActionCatalogTests.cs`

## Overview

The `EditTextWindowActionCatalogTests.cs` file contains a unit test suite designed to validate the integrity of button action mappings within the Text-Grab application. Specifically, it ensures that all buttons defined in `ButtonInfo.AllButtons` refer to valid, resolvable commands or click event handlers implemented in the `EditTextWindow` class.

---

## Namespaces & Imports

| Namespace / Import | Purpose |
| :--- | :--- |
| `System.Reflection` | Provides reflection capabilities to inspect private instance methods of `EditTextWindow`. |
| `Text_Grab` | Contains the `EditTextWindow` class and its associated static/instance methods. |
| `Text_Grab.Models` | Contains the `ButtonInfo` model and its `AllButtons` collection. |
| `Tests` | The target namespace for this unit test class. |

---

## Class & Data Structures

### `EditTextWindowActionCatalogTests`

* **Type**: `public class`
* **Purpose**: Serves as the test container for verifying command and click event bindings for UI buttons.

#### Nested Structures

##### `ExpectedButtonAction`
* **Type**: `private readonly record struct`
* **Parameters**:
  * `string ButtonText`: The display text of the expected button.
  * `string? Command`: The optional command name associated with the button (defaults to `null`).
  * `string? ClickEvent`: The optional click event handler name associated with the button (defaults to `null`).
* **Purpose**: An internal record struct defined to represent expected button action mappings.

---

## Test Methods

### `AllButtons_UsesResolvableEditTextCommandsAndClickEvents()`

* **Attribute**: `[Fact]` (xUnit unit test)
* **Access Level**: `public void`
* **Purpose**: Verifies that every button in `ButtonInfo.AllButtons` points to a command key or event handler method that actually exists on `EditTextWindow`.

#### Execution Workflow

1. **Retrieve Commands**:
   * Calls `EditTextWindow.GetRoutedCommands().Keys` to obtain all registered routed command names.
   * Stores these command names in a `HashSet<string>` named `commandNames` for fast lookup.

2. **Retrieve Non-Public Instance Methods**:
   * Uses Reflection (`typeof(EditTextWindow).GetMethods(...)`) with flags `BindingFlags.Instance | BindingFlags.NonPublic`.
   * Projects the resulting method reflection objects into string method names.
   * Stores these method names in a `HashSet<string>` named `methodNames`.

3. **Validate Button Definitions**:
   * Iterates through each `ButtonInfo` item inside `ButtonInfo.AllButtons`.
   * **Command Validation**:
     * Checks if `button.Command` is not null or whitespace.
     * Asserts that `commandNames` contains `button.Command` via `Assert.Contains`.
   * **Click Event Validation**:
     * Checks if `button.ClickEvent` is not null or whitespace.
     * Asserts that `methodNames` contains `button.ClickEvent` via `Assert.Contains`.

---

## Code Summary

```csharp
namespace Tests;

public class EditTextWindowActionCatalogTests
{
    // Private record struct definition for tracking expected actions
    private readonly record struct ExpectedButtonAction(string ButtonText, string? Command = null, string? ClickEvent = null);

    [Fact]
    public void AllButtons_UsesResolvableEditTextCommandsAndClickEvents()
    {
        // 1. Gather routed commands from EditTextWindow
        HashSet<string> commandNames = [.. EditTextWindow.GetRoutedCommands().Keys];

        // 2. Gather non-public instance methods from EditTextWindow via reflection
        HashSet<string> methodNames = [.. typeof(EditTextWindow)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(method => method.Name)];

        // 3. Assert all buttons reference valid commands or methods
        foreach (ButtonInfo button in ButtonInfo.AllButtons)
        {
            if (!string.IsNullOrWhiteSpace(button.Command))
                Assert.Contains(button.Command, commandNames);

            if (!string.IsNullOrWhiteSpace(button.ClickEvent))
                Assert.Contains(button.ClickEvent, methodNames);
        }
    }
}
```