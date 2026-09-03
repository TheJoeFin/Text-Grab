# Technical Documentation: `AddOrRemoveWindow.xaml.cs`

## Overview

The `AddOrRemoveWindow` class is a WPF control window in the `Text_Grab.Controls` namespace. Inheriting from `Wpf.Ui.Controls.FluentWindow`, it provides a dialog interface for modifying text within a parent `EditTextWindow`. 

Depending on user input, it allows users to:
1. **Add** specific text to the beginning or end of lines.
2. **Remove** a specified number of characters from the beginning or end of lines.
3. **Limit** lines to a maximum character length from the beginning or end.

---

## Class Signature and Hierarchy

```csharp
namespace Text_Grab.Controls;

public partial class AddOrRemoveWindow : Wpf.Ui.Controls.FluentWindow
```

- **Namespace:** `Text_Grab.Controls`
- **Base Class:** `Wpf.Ui.Controls.FluentWindow`

---

## Fields

| Field Name | Type | Description |
| :--- | :--- | :--- |
| `AddRemoveCmd` | `RoutedCommand` | Static command used to apply changes and close the window. |
| `ApplyCmd` | `RoutedCommand` | Static command used to apply changes while keeping the window open. |

---

## Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `AddRemove` | `AddRemove` | `AddRemove.Add` | Holds the active operation mode (`AddRemove` enum). |
| `LengthToChange` | `int?` | `null` | Parsed integer indicating how many characters to remove or limit. |
| `SelectedTextFromEditTextWindow` | `string` | `""` | Text passed into the window (typically selected text from the owner window). |
| `SpotInLine` | `SpotInLine` | `SpotInLine.Beginning` | Indicates where operations apply in a line (`SpotInLine` enum). |
| `TextToAdd` | `string` | `""` | The string content to add to lines. |

---

## Method Documentation

### Constructors

#### `AddOrRemoveWindow()`
Initializes a new instance of the `AddOrRemoveWindow` class and executes `InitializeComponent()`.

---

### Command Execution Handlers

#### `AddRemove_CanExecute(object sender, CanExecuteRoutedEventArgs e)`
Determines whether the `AddRemoveCmd` or related routed commands can execute.
- Sets `e.CanExecute = true` if:
  - `AddRadioButton` is checked AND `TextToAddTextBox.Text` is not null or empty.
  - OR (`RemoveRadioButton` is checked OR `LimitRadioButton` is checked) AND `LengthToChange` is not null.
- Otherwise, sets `e.CanExecute = false`.

#### `AddRemove_Executed(object sender, ExecutedRoutedEventArgs e)`
Executes when the `AddRemoveCmd` command is invoked.
- Verifies that `Owner` is an instance of `EditTextWindow`. If not, it returns without performing any action.
- Calls `Apply(etwOwner)`.
- Closes the window via `Close()`.

#### `Apply_Executed(object sender, ExecutedRoutedEventArgs e)`
Executes when the `ApplyCmd` command is invoked.
- Verifies that `Owner` is an instance of `EditTextWindow`. If not, it returns without performing any action.
- Calls `Apply(etwOwner)` to apply transformations without closing the window.

---

### Operation Processing Methods

#### `Apply(EditTextWindow etwOwner)`
Routes the execution to the appropriate text manipulation method depending on which radio button is checked:
- Calls `AddText(etwOwner)` if `AddRadioButton.IsChecked` is `true`.
- Calls `RemoveText(etwOwner)` if `RemoveRadioButton.IsChecked` is `true`.
- Calls `LimitText(etwOwner)` if neither of the above is checked (e.g., `LimitRadioButton` is selected).

#### `AddText(EditTextWindow etwOwner)`
Applies the add operation to the owner `EditTextWindow`.
- Checks `BeginningRDBTN.IsChecked`:
  - If `true`: Calls `etwOwner.AddCharsToEditTextWindow(TextToAddTextBox.Text, SpotInLine.Beginning)`.
  - If `false`: Calls `etwOwner.AddCharsToEditTextWindow(TextToAddTextBox.Text, SpotInLine.End)`.

#### `RemoveText(EditTextWindow etwOwner)`
Applies the character removal operation to the owner `EditTextWindow`.
- Returns early if `LengthToChange` is `null`.
- Checks `BeginningRDBTN.IsChecked`:
  - If `true`: Calls `etwOwner.RemoveCharsFromEditTextWindow(LengthToChange.Value, SpotInLine.Beginning)`.
  - If `false`: Calls `etwOwner.RemoveCharsFromEditTextWindow(LengthToChange.Value, SpotInLine.End)`.

#### `LimitText(EditTextWindow etwOwner)`
Applies the character limit operation to the owner `EditTextWindow`.
- Returns early if `LengthToChange` is `null`.
- Checks `BeginningRDBTN.IsChecked`:
  - If `true`: Calls `etwOwner.LimitNumberOfCharsPerLine(LengthToChange.Value, SpotInLine.Beginning)`.
  - If `false`: Calls `etwOwner.LimitNumberOfCharsPerLine(LengthToChange.Value, SpotInLine.End)`.

---

### UI Event Handlers

#### `InputTextBox_TextChanged(object sender, TextChangedEventArgs e)`
Triggers when text changes inside the input controls.
- Updates `TextToAdd` with the text from `TextToAddTextBox` if `AddRadioButton.IsChecked` is `true`.
- Parses `LengthTextBox.Text` into an `int`:
  - If parsing succeeds: Sets `LengthToChange` to the parsed integer.
  - If parsing fails: Sets `LengthToChange` to `null`.

#### `RemoveRadioButton_Checked(object sender, RoutedEventArgs e)`
Handles UI control enablement based on mode selection.
- If `RemoveRadioButton` is checked:
  - Disables `TextToAddTextBox` (`IsEnabled = false`).
  - Enables `LengthTextBox` (`IsEnabled = true`).
- Otherwise:
  - Enables `TextToAddTextBox` (`IsEnabled = true`).
  - Disables `LengthTextBox` (`IsEnabled = false`).

#### `Window_KeyUp(object sender, KeyEventArgs e)`
Handles keyboard shortcuts for window control.
- Intercepts the `Escape` key:
  - If either `TextToAddTextBox` or `LengthTextBox` contains text/whitespace, it clears both text boxes (`Clear()`).
  - If both text boxes are empty, it closes the window (`this.Close()`).

#### `Window_Loaded(object sender, RoutedEventArgs e)`
Handles window initialization upon loading.
- Populates `TextToAddTextBox.Text` with `SelectedTextFromEditTextWindow`.
- Populates `LengthTextBox.Text` with the string length of `SelectedTextFromEditTextWindow`.

---

## Workflow Integration Summary

```
[Window_Loaded] 
      │
      ├──> Pre-fills TextToAddTextBox and LengthTextBox from SelectedTextFromEditTextWindow
      │
[Input Synchronization]
      │
      ├──> InputTextBox_TextChanged updates TextToAdd and parses LengthToChange
      ├──> RemoveRadioButton_Checked toggles controls availability
      │
[Execution Triggers]
      │
      ├──> AddRemove_Executed ──> Apply(etwOwner) ──> Close()
      └──> Apply_Executed     ──> Apply(etwOwner) (Keeps Window Open)
      │
[Operation Execution]
      │
      ├──> AddRadioButton checked    ──> AddText()    ──> etwOwner.AddCharsToEditTextWindow()
      ├──> RemoveRadioButton checked ──> RemoveText() ──> etwOwner.RemoveCharsFromEditTextWindow()
      └──> Otherwise                 ──> LimitText()  ──> etwOwner.LimitNumberOfCharsPerLine()
```