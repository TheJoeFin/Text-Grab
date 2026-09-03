# Technical Documentation: `CustomBottomBarUtilities.cs`

## Overview

The `CustomBottomBarUtilities` class (located in the `Text_Grab.Utilities` namespace) provides static helper methods to manage, persist, and construct custom bottom bar buttons for the `EditTextWindow` UI in Text-Grab. 

It handles loading bottom bar configuration settings, populating non-serialized symbol icons, dynamically mapping custom buttons to click event handlers or routed commands using reflection, and saving button configurations back to application settings.

---

## Class Architecture & Static Fields

```csharp
namespace Text_Grab.Utilities;

public class CustomBottomBarUtilities
```

### Private Fields

* **`_methodCache`** (`Dictionary<Type, List<MethodInfo>>`): A static cache storing non-public instance methods of window types to avoid redundant reflection lookups when creating delegates.
* **`_methodCacheLock`** (`Lock`): A synchronization lock object used to ensure thread-safe access and double-check locking when populating `_methodCache`.
* **`BrushConverter`** (`BrushConverter`): A static converter used for converting color string values into brush objects.

---

## Method Documentation

### Public Methods

#### `GetCustomBottomBarItemsSetting()`

Loads the user's custom bottom bar button configuration from settings and restores icon symbols.

* **Signature:**
  ```csharp
  public static List<ButtonInfo> GetCustomBottomBarItemsSetting()
  ```
* **Return Value:** `List<ButtonInfo>` — The list of configured bottom bar buttons. Returns `ButtonInfo.DefaultButtonList` if no custom settings exist.
* **How It Works:**
  1. Calls `AppUtilities.TextGrabSettingsService.LoadBottomBarButtons()`.
  2. If the loaded list is empty, returns `ButtonInfo.DefaultButtonList`.
  3. Because `SymbolIcon` is excluded from JSON serialization (`[JsonIgnore]`), it maps default symbol icons from `ButtonInfo.AllButtons` back onto the loaded `ButtonInfo` objects by matching their `ButtonText`.
  4. Returns the restored list of `ButtonInfo` items.

---

#### `SaveCustomBottomBarItemsSetting(List<CollapsibleButton>)`

Overload that converts UI `CollapsibleButton` controls into `ButtonInfo` model instances and saves them.

* **Signature:**
  ```csharp
  public static void SaveCustomBottomBarItemsSetting(List<CollapsibleButton> bottomBarButtons)
  ```
* **Parameters:**
  * `bottomBarButtons` (`List<CollapsibleButton>`): A list of active bottom bar WPF controls.
* **How It Works:**
  1. Iterates over each `CollapsibleButton` in `bottomBarButtons`.
  2. Wraps each control into a new `ButtonInfo` object (`new ButtonInfo(collapsible)`).
  3. Calls `SaveCustomBottomBarItemsSetting(List<ButtonInfo>)`.

---

#### `SaveCustomBottomBarItemsSetting(List<ButtonInfo>)`

Persists a list of `ButtonInfo` models to settings.

* **Signature:**
  ```csharp
  public static void SaveCustomBottomBarItemsSetting(List<ButtonInfo> bottomBarButtons)
  ```
* **Parameters:**
  * `bottomBarButtons` (`List<ButtonInfo>`): The list of button configuration models to save.
* **How It Works:**
  1. Calls `AppUtilities.TextGrabSettingsService.SaveBottomBarButtons(bottomBarButtons)`.

---

#### `GetBottomBarButtons(EditTextWindow)`

Constructs a list of `CollapsibleButton` WPF controls to be rendered in the specified `EditTextWindow`, wiring up properties, event handlers, and routed commands.

* **Signature:**
  ```csharp
  public static List<CollapsibleButton> GetBottomBarButtons(EditTextWindow editTextWindow)
  ```
* **Parameters:**
  * `editTextWindow` (`EditTextWindow`): The target window instance that contains the handler methods and commands for the buttons.
* **Return Value:** `List<CollapsibleButton>` — Fully instantiated and configured dynamic button controls ready for display.
* **How It Works:**
  1. Fetches non-public instance methods of `editTextWindow` via `GetMethods(editTextWindow)`.
  2. Retrieves routed commands from `EditTextWindow.GetRoutedCommands()`.
  3. Loops through each `ButtonInfo` retrieved from `GetCustomBottomBarItemsSetting()` with a 1-based index counter:
     * Instantiates a `CollapsibleButton` and assigns `ButtonText`, `IsSymbol`, `CustomButton`, `ToolTip` (formatted as `"{ButtonText} (ctrl + {index})"`), and `ButtonSymbol`.
     * Evaluates `Background`: If not set to `"Transparent"`, attempts to convert the string to a `SolidColorBrush` using `BrushConverter` and applies it.
     * Binding Logic:
       * Checks if the button's `ClickEvent` name matches a non-public instance method on `editTextWindow`. If found, creates a `RoutedEventHandler` delegate bound to `editTextWindow` and attaches it to the button's `Click` event.
       * If no click event method is found, checks if `Command` matches a available `RoutedCommand`. If found, binds the `RoutedCommand` to `button.Command`.
  4. Returns the generated list of `CollapsibleButton` controls.

---

### Private Helper Methods

#### `GetMethods(object)`

Extracts non-public instance methods of an object's type utilizing double-check locking and caching.

* **Signature:**
  ```csharp
  private static List<MethodInfo> GetMethods(object obj)
  ```
* **Parameters:**
  * `obj` (`object`): The object instance whose methods will be retrieved via reflection.
* **Return Value:** `List<MethodInfo>` — Cached or newly reflected non-public instance methods.
* **How It Works:**
  1. Retrieves the object's `Type`.
  2. Checks if `_methodCache` already contains methods for that `Type`. If present, returns immediately.
  3. Enters a thread lock (`_methodCacheLock`) and performs a second check on `_methodCache`.
  4. Uses reflection (`type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)`) to retrieve non-public instance methods.
  5. Caches the result in `_methodCache` and returns the method list.

---

#### `GetMethodInfoForName(string, List<MethodInfo>)`

Finds a `MethodInfo` matching a given method name string.

* **Signature:**
  ```csharp
  private static MethodInfo? GetMethodInfoForName(string methodName, List<MethodInfo> methods)
  ```
* **Parameters:**
  * `methodName` (`string`): Target method name to locate.
  * `methods` (`List<MethodInfo>`): List of methods to search through.
* **Return Value:** `MethodInfo?` — The matching `MethodInfo`, or `null` if no match is found.

---

#### `GetCommandBinding(string, Dictionary<string, RoutedCommand>)`

Searches a dictionary of routed commands for a key matching the command name string.

* **Signature:**
  ```csharp
  private static RoutedCommand? GetCommandBinding(string commandName, Dictionary<string, RoutedCommand> routedCommands)
  ```
* **Parameters:**
  * `commandName` (`string`): Target command key to match.
  * `routedCommands` (`Dictionary<string, RoutedCommand>`): Mapping of command names to `RoutedCommand` instances.
* **Return Value:** `RoutedCommand?` — The matching `RoutedCommand`, or `null` if no match is found.

---

## Workflow Diagram

```
[Load Bottom Bar Items]
         │
         ▼
Load ButtonInfo list from Settings Service
         │
  Is List Empty? ── Yes ──> Return ButtonInfo.DefaultButtonList
         │ No
         ▼
Restore SymbolIcon from ButtonInfo.AllButtons matching ButtonText
         │
         ▼
[Construct CollapsibleButton Controls for EditTextWindow]
         │
         ├─ Assign Text, Symbols, Index-based Tooltips (ctrl + N)
         ├─ Convert and apply custom Background brush if non-transparent
         │
         ├─ Match ClickEvent to non-public instance MethodInfo via reflection?
         │      ├─ Yes ──> Create RoutedEventHandler delegate ──> Attach to button.Click
         │      └─ No  ──> Match Command to RoutedCommand?
         │                     └─ Yes ──> Assign button.Command
         │
         ▼
Return generated List<CollapsibleButton> to UI
```