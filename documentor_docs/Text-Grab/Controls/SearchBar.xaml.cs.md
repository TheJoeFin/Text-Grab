# Documentation: `Text-Grab/Controls/SearchBar.xaml.cs`

## Overview

The `SearchBar` class is a custom WPF `UserControl` in the `Text_Grab.Controls` namespace. It serves as a unified search input component across multiple features in Text-Grab (such as Quick Simple Lookup, Find & Replace, and Grab Frame). 

The control encapsulates the user input interface, including:
- A free-text input box with placeholder text.
- Regular Expression (Regex) toggling and validation styling.
- An optional exact-match toggle.
- A removable smart-pattern "chip" interface for active recognizers.
- A pattern picker menu generated dynamically from `PatternItem` instances.

The `SearchBar` control manages input state and UI adornments, delegating the actual searching, filtering, and debouncing logic to the host component by raising state-change events.

---

## Class Declaration

```csharp
namespace Text_Grab.Controls;

public partial class SearchBar : UserControl
```

---

## Fields & Constants

| Name | Type | Description |
| :--- | :--- | :--- |
| `RegexToolTip` | `const string` | Default tooltip text displayed on the Regex toggle button when valid: `"Search using Regular Expression syntax"`. |
| `suppressSearchChanged` | `bool` | Private flag used to temporarily suppress raising the `SearchChanged` event when updating multiple dependency properties in a single batch operation. |

---

## Dependency Properties

The control defines several WPF `DependencyProperty` members to allow easy data binding from host controls or view models.

| Property Name | Type | Default Value | Metadata / Binding Mode | Description |
| :--- | :--- | :--- | :--- | :--- |
| `SearchText` | `string` | `string.Empty` | `BindsTwoWayByDefault`, Callback: `OnSearchTextChanged` | Gets or sets the text entered into the search input box. |
| `UseRegex` | `bool` | `false` | `BindsTwoWayByDefault`, Callback: `OnUseRegexChanged` | Indicates whether regular expression matching is enabled. |
| `ExactMatch` | `bool` | `false` | `BindsTwoWayByDefault`, Callback: `OnExactMatchChanged` | Indicates whether exact matching is enabled. |
| `ShowExactMatchToggle` | `bool` | `false` | Standard PropertyMetadata | Controls the visibility of the exact-match toggle button. |
| `SelectedPattern` | `PatternItem?` | `null` | `BindsTwoWayByDefault`, Callback: `OnSelectedPatternChanged` | Holds the currently selected smart-pattern item displayed as a chip. |
| `PlaceholderText` | `string` | `"Type to search..."` | Standard PropertyMetadata | Gets or sets the placeholder text shown when the input box is empty. |
| `AcceptsReturn` | `bool` | `false` | Standard PropertyMetadata | Controls whether the underlying text box accepts return characters. |
| `AcceptsTab` | `bool` | `false` | Standard PropertyMetadata | Controls whether the underlying text box accepts tab characters. |

---

## Events

| Event Name | Type | Description |
| :--- | :--- | :--- |
| `SearchChanged` | `EventHandler?` | Raised whenever `SearchText`, `UseRegex`, `ExactMatch`, or `SelectedPattern` changes (unless `suppressSearchChanged` is true). |
| `ExactMatchChanged` | `EventHandler?` | Raised specifically when the `ExactMatch` property changes, allowing host controls to react to case-handling changes directly. |

---

## Public API

### Properties

#### `TextBox`
```csharp
public TextBox TextBox => InnerTextBox;
```
* **Description**: Exposes the underlying `TextBox` control (`InnerTextBox`) for scenarios where host views require direct access (e.g., focus handling, target OCR operations, caret manipulation).

---

### Methods

#### `SetRegexValidity(bool isValid, string? toolTip = null)`
```csharp
public void SetRegexValidity(bool isValid, string? toolTip = null)
```
* **Parameters**:
  * `isValid` (`bool`): Determines if the current regex pattern is valid.
  * `toolTip` (`string?`): Optional custom tooltip message. If omitted, defaults to `RegexToolTip` when valid, or `"Invalid Regular Expression"` when invalid.
* **Description**: Updates the visual state of the regex control container (`RegexSplitContainer`). If invalid, the container's `BorderBrush` is set to `Brushes.Red`. If valid, the explicit property is cleared to allow style triggers to drive appearance.

#### `FocusInput()`
```csharp
public void FocusInput()
```
* **Description**: Sets focus to `InnerTextBox` and positions the caret at the end of the text string.

---

## Internal Event Handling & Logic

### Property Change Callbacks

* **`OnSearchTextChanged`**: Invokes `UpdateAdornments()` and calls `RaiseSearchChanged()`.
* **`OnUseRegexChanged`**: Resets regex validity state by calling `SetRegexValidity(true)` and calls `RaiseSearchChanged()`.
* **`OnExactMatchChanged`**: Fires the `ExactMatchChanged` event and calls `RaiseSearchChanged()`.
* **`OnSelectedPatternChanged`**: Calls `UpdateChip()`, `UpdateAdornments()`, and `RaiseSearchChanged()`.

### Visual Updates & Adornments

* **`UpdateChip()`**:
  * Checks if `PatternChip` exists.
  * If `SelectedPattern` is non-null, sets `PatternChipText.Text` to the pattern's name and makes `PatternChip` visible.
  * If `SelectedPattern` is null, collapses `PatternChip`.
* **`UpdateAdornments()`**:
  * Shows `ClearButton` if `SearchText` is not empty; otherwise collapses it.
  * Displays `PlaceholderTextBlock` only when `SearchText` is empty **and** `SelectedPattern` is `null`.

### Dynamic Pattern Menu Generation

* **`PatternDropDownButton_Click`**: Positions `PatternMenu` beneath `PatternDropDownButton` and opens it (`IsOpen = true`).
* **`PatternMenu_Opened`**:
  1. Clears existing items in `PatternMenu`.
  2. Fetches all patterns via `PatternItem.GetAll()`.
  3. Groups items using `pattern.GroupLabel`. Inserts a `Separator` between different groups and adds a disabled `MenuItem` as a group header.
  4. Generates a `MenuItem` for each pattern, setting the header, tooltip, tag (`PatternItem`), and subscribing its click event to `PatternMenuItem_Click`.

### Applying Patterns

* **`ApplyPickedPattern(PatternItem pattern)`**:
  1. Sets `suppressSearchChanged = true` to prevent intermediate `SearchChanged` events.
  2. Checks pattern type:
     * **`PatternKind.SavedRegex`**: Sets `SelectedPattern = null`, sets `SearchText` to `savedRegex.Pattern`, and sets `UseRegex = true`.
     * **Other Patterns (Smart Patterns/Recognizers)**: Clears `SearchText` (`string.Empty`) and sets `SelectedPattern` to the selected item.
  3. Sets `suppressSearchChanged = false`.
  4. Invokes `RaiseSearchChanged()` and calls `FocusInput()`.

### Click Handlers

* **`PatternMenuItem_Click`**: Extracts the `PatternItem` stored in the sender `MenuItem`'s `Tag` property and calls `ApplyPickedPattern`.
* **`ChipClearButton_Click`**: Clears `SelectedPattern` (`null`) and returns focus to the input box.
* **`ClearButton_Click`**: Clears `SearchText` (`string.Empty`) and returns focus to the input box.