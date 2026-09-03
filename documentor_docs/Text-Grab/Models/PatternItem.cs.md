# Technical Documentation: `Text-Grab/Models/PatternItem.cs`

## Overview

The `PatternItem.cs` file defines a unified wrapper model for pattern matching within the Text-Grab application. It abstracts two underlying pattern implementations—user-managed regular expressions (`StoredRegex`) and built-in recognizers (`BuiltInRecognizer`)—into a single `PatternItem` type. This enables UI components (such as template pickers, edit menus, and search features) to display, group, and query all pattern types uniformly.

---

## Enumerations

### `PatternKind`

Defines the underlying backing implementation of a `PatternItem`.

| Value | Description |
| :--- | :--- |
| `SavedRegex` | Indicates the item is backed by a user-managed regular expression (`StoredRegex`). |
| `Recognizer` | Indicates the item is backed by a built-in, culture-aware recognizer (`BuiltInRecognizer`). |

---

## Class: `PatternItem`

### Constants

* `public const string SavedGroup = "Saved Patterns"`  
  The subsection header label used for grouping user-managed regex patterns in UI lists.
* `public const string SmartGroup = "Smart Patterns"`  
  The subsection header label used for grouping built-in recognizers in UI lists.

---

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| `Kind` | `PatternKind` | Identifies whether the pattern is backed by a `SavedRegex` or a `Recognizer`. Read-only. |
| `Id` | `string` | The stable identifier sourced from `StoredRegex.Id` or `BuiltInRecognizer.Id`. Read-only. |
| `Name` | `string` | The display name shown in user interface menus and pickers. Read-only. |
| `Description` | `string` | A short description explaining what the pattern matches. Read-only. |
| `GroupLabel` | `string` | Grouping header label (`SavedGroup` or `SmartGroup`). Read-only. |
| `SavedRegex` | `StoredRegex?` | Holds the backing `StoredRegex` instance if `Kind == PatternKind.SavedRegex`; otherwise, `null`. Read-only. |
| `Recognizer` | `BuiltInRecognizer?` | Holds the backing `BuiltInRecognizer` instance if `Kind == PatternKind.Recognizer`; otherwise, `null`. Read-only. |
| `IsHidden` | `bool` | Indicates whether the user has hidden the pattern from pickers. Applicable primarily to `Recognizer` items. Read-only. |
| `PatternDisplay` | `string` | Computed property. Returns the raw regex string from `SavedRegex.Pattern` if `SavedRegex` exists; otherwise returns `"(built-in)"`. |

---

### Constructors (Internal)

#### `PatternItem(StoredRegex savedRegex)`
Initializes a new `PatternItem` backed by a `StoredRegex`.
* Sets `Kind` to `PatternKind.SavedRegex`.
* Maps `Id`, `Name`, and `Description` directly from the `savedRegex` instance.
* Sets `GroupLabel` to `SavedGroup` (`"Saved Patterns"`).
* Populates the `SavedRegex` property and leaves `Recognizer` as `null`.

#### `PatternItem(BuiltInRecognizer recognizer, bool isHidden = false)`
Initializes a new `PatternItem` backed by a `BuiltInRecognizer`.
* Sets `Kind` to `PatternKind.Recognizer`.
* Maps `Id`, `Name`, and `Description` directly from the `recognizer` instance.
* Sets `GroupLabel` to `SmartGroup` (`"Smart Patterns"`).
* Populates the `Recognizer` property and leaves `SavedRegex` as `null`.
* Sets `IsHidden` based on the passed parameter (defaults to `false`).

---

### Static Methods

#### `GetAll(bool includeHidden = false)`

Retrieves a combined list of all available patterns (`SavedRegex` items followed by `BuiltInRecognizer` items).

* **Parameters:**
  * `includeHidden` (`bool`, default `false`): If `true`, includes recognizers that have been marked as hidden by the user.
* **Returns:**
  * `IReadOnlyList<PatternItem>`: The full catalog of active (or all) patterns.
* **Execution Flow:**
  1. Calls `AppUtilities.TextGrabSettingsService.LoadStoredRegexes()` to retrieve saved user regexes.
  2. If the retrieved array is empty, falls back to default patterns via `StoredRegex.GetDefaultPatterns()`.
  3. Retrieves hidden pattern IDs using `AppUtilities.TextGrabSettingsService.LoadHiddenSmartPatternIds()`.
  4. Gets all built-in recognizers via `BuiltInRecognizer.GetAll()`, wrapping each into a `PatternItem` and determining its hidden state.
  5. If `includeHidden` is `false`, filters out recognizers where `IsHidden` is `true`.
  6. Combines and returns the wrapped saved patterns followed by the recognizer patterns into a single read-only collection.

#### `GetByName(string name)`

Searches the pattern catalog for a `PatternItem` matching the specified display name using a case-insensitive comparison.

* **Parameters:**
  * `name` (`string`): The name of the pattern to search for.
* **Returns:**
  * `PatternItem?`: The matching pattern item, or `null` if no match is found.
* **Behavior:**
  * Executes `GetAll()`.
  * Because `GetAll()` orders `SavedRegex` items before `BuiltInRecognizer` items, if a saved regex and a recognizer share the exact same name, the saved regex takes precedence.