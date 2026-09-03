# Technical Documentation: InlinePickerRichTextBox

## Overview

The `InlinePickerRichTextBox` class is a specialized WPF `RichTextBox` control located in the `Text-Grab.Controls` namespace. It provides an inline, context-sensitive autocomplete picker (popup) when a user types a trigger character (default is `{`). 

When an item is selected from the popup, the typed trigger text and filter string are replaced with an inline visual element known as a **chip** (`InlineChipElement` wrapped inside an `InlineUIContainer`). It also supports interactive pattern/recognizer selection dialogs and bidirectional serialization between rich visual documents and plain-text representation (e.g., converting visual chips into `{1}`, `{p:Pattern:all}`, or `{r:Recognizer:mode:text}`).

---

## Class Hierarchy & Inheritance

```
System.Windows.Controls.RichTextBox
  └── Text_Grab.Controls.InlinePickerRichTextBox
```

---

## Component Architecture

```
InlinePickerRichTextBox (RichTextBox)
│
├── _popup (Popup)
│    └── Border (Shadow & Styling)
│         └── _listBox (ListBox)
│              ├── Uses PickerItemTemplateSelector
│              ├── Selectable Item Template (DisplayName + Value)
│              └── Header Item Template (Section headers)
│
└── Inlines Flow (Document)
     ├── Run (Plain Text)
     └── InlineUIContainer
          └── InlineChipElement (Interactive Chip)
```

---

## Dependency Properties

| Dependency Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `ItemsSourceProperty` | `IEnumerable<InlinePickerItem>` | `null` | Bindable collection of items displayed in the autocomplete picker popup. |
| `SerializedTextProperty` | `string` | `string.Empty` | Bindable plain-text representation of the document content where chips are converted to their raw values. Binds two-way by default. |

---

## Properties

### CLR Properties

*   **`ItemsSource`** (`IEnumerable<InlinePickerItem>`): Wrapper for `ItemsSourceProperty`. Returns an empty array `[]` if the value is `null`.
*   **`SerializedText`** (`string`): Wrapper for `SerializedTextProperty`.
*   **`TriggerChar`** (`char`): Specifies the character that opens the picker popup. Defaults to `'{'`.

---

## Events and Delegates

### Events

*   **`ItemInserted`** (`EventHandler<InlinePickerItem>?`): Raised after a user completes a selection and an `InlineChipElement` has been successfully inserted into the document.

### Delegates & Callbacks

*   **`PatternItemSelected`** (`Func<InlinePickerItem, TemplatePatternMatch?>?`):
    *   Invoked when a selected item has `Kind == PatternKind.SavedRegex`.
    *   Expects the host application to display a configuration dialog (such as `PatternMatchModeDialog`) and return a `TemplatePatternMatch` object, or `null` to cancel insertion.
*   **`RecognizerItemSelected`** (`Func<InlinePickerItem, TemplateRecognizerMatch?>?`):
    *   Invoked when a selected item has `Kind == PatternKind.Recognizer`.
    *   Expects the host application to display a match-mode dialog and return a `TemplateRecognizerMatch` object, or `null` to cancel insertion.

---

## Internal Constants

*   **`HeaderGroupTag`** (`string`): Equal to `"__header__"`. Sentinels group value used to mark non-selectable section header items inside the popup `ListBox`.

---

## Detailed Methods Breakdown

### Constructor & Initialization

*   **`InlinePickerRichTextBox()`**:
    *   Disables return keys (`AcceptsReturn = false`) and scrollbars.
    *   Constructs `_listBox` via `BuildPopupListBox()`.
    *   Constructs `_popup` with custom border, rounded corners (radius 8), drop shadow effect (`DropShadowEffect`), and resource references for background and border colors (`SolidBackgroundFillColorBaseBrush`, `Teal`).
    *   Attaches event listeners: `TextChanged`, `PreviewKeyDown`, `LostKeyboardFocus`.

### UI Builder Methods

*   **`BuildPopupListBox()`**:
    *   Creates a non-focusable, transparent `ListBox` for displaying popup items.
    *   Assigns `PickerItemTemplateSelector` with selectable and header `DataTemplate`s.
    *   Applies compact item styling via `BuildCompactItemStyle()`.
*   **`BuildSelectableItemTemplate()`**:
    *   Creates a `DataTemplate` containing a horizontal `StackPanel` displaying the item's `DisplayName` and `Value` (semi-transparent).
*   **`BuildHeaderItemTemplate()`**:
    *   Creates a `DataTemplate` displaying group section headers (`FontWeight.SemiBold`, non-hit-testable).
*   **`BuildCompactItemStyle()`**:
    *   Constructs a custom `ControlTemplate` for `ListBoxItem` to override touch-sized paddings/margins. Configures hover and selection triggers with custom brush colors (`#22308E98` and `#44308E98`).

### Keyboard and Focus Handling

*   **`OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)`**:
    *   Hides `_popup` and clears `_triggerStart`, unless focus moves within the popup visual tree itself.
*   **`OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)`**:
    *   Intercepts mouse clicks on `InlineChipElement` remove buttons. Routes the click event directly to the chip's close button if detected.
*   **`OnPreviewKeyDown(object sender, KeyEventArgs e)`**:
    *   Handles keyboard interactions when `_popup.IsOpen` is `true`:
        *   `Up` / `Down`: Calls `MoveSelection()` to navigate listbox items (skipping headers).
        *   `Enter` / `Tab`: Calls `CommitSelection()` to insert the item.
        *   `Escape`: Closes popup and clears `_triggerStart`.
        *   `Back`: Closes popup if caret moves past or back to `_triggerStart`.
*   **`MoveSelection(int direction)`**:
    *   Cycles through selection index by `+1` or `-1`, automatically skipping items where `Group == HeaderGroupTag`.

### Text Change & Popup Management

*   **`OnTextChanged(object sender, TextChangedEventArgs e)`**:
    *   Guarded by `_isModifyingDocument`.
    *   Detects if `TriggerChar` was typed immediately before the caret.
    *   Updates filtering or hides the popup if the caret moves backward past `_triggerStart`.
    *   Updates `SerializedText`.
*   **`RefreshPopup()`**:
    *   Filters items based on user input.
    *   Positions `_popup` below the caret using `CaretPosition.GetCharacterRect()`.
    *   Automatically selects the first selectable non-header item.
*   **`GetFilteredItems()`**:
    *   Extracts text typed after `_triggerStart` (ignoring `TriggerChar`).
    *   Filters `ItemsSource` by checking if `DisplayName` or `Value` contains the typed string (case-insensitive).
    *   Calls `InsertGroupHeaders()` to add section headers if multiple distinct groups exist.
*   **`InsertGroupHeaders(List<InlinePickerItem> items)`**:
    *   Group items by `InlinePickerItem.Group`. If more than one distinct non-empty group is present, inserts synthetic `InlinePickerItem` instances with `DisplayName = "── {group} ──"` and `Group = HeaderGroupTag`.

### Selection & Chip Insertion

*   **`ListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)`**:
    *   Handles direct mouse clicks on `ListBoxItem` elements in the popup to trigger `CommitSelection()`.
*   **`CommitSelection()`**:
    1.  Validates that selected item is not a group header.
    2.  Saves `_triggerStart`.
    3.  If item is a saved pattern (`Kind == PatternKind.SavedRegex`) and `PatternItemSelected` is configured:
        *   Invokes callback to show UI dialog.
        *   Builds pattern placeholder text via `BuildPatternPlaceholder()`.
    4.  If item is a recognizer (`Kind == PatternKind.Recognizer`) and `RecognizerItemSelected` is configured:
        *   Invokes callback to show UI dialog.
        *   Builds recognizer placeholder text via `BuildRecognizerPlaceholder()`.
    5.  Removes typed trigger text (`new TextRange(savedTriggerStart, CaretPosition).Text = string.Empty`).
    6.  Instantiates `InlineChipElement` and wraps it in an `InlineUIContainer` set to `BaselineAlignment.Center`.
    7.  Subscribes `Chip_RemoveRequested` handler to the chip's removal event.
    8.  Moves caret immediately past the newly inserted chip.
    9.  Fires `ItemInserted` event and updates serialization.
*   **`BuildPatternPlaceholder(TemplatePatternMatch config)`**:
    *   Generates placeholder text with format `{p:PatternName:MatchMode}` or `{p:PatternName:MatchMode:Separator}` depending on match mode requirements.
*   **`BuildRecognizerPlaceholder(TemplateRecognizerMatch config)`**:
    *   Generates placeholder text with format `{r:RecognizerName:MatchMode[:text|:value][:Separator]}`.
*   **`Chip_RemoveRequested(object? sender, EventArgs e)`**:
    *   Removes the target `InlineUIContainer` from the containing `Paragraph.Inlines` collection when a chip requests deletion.

### Serialization Methods

*   **`GetSerializedText()`**:
    *   Traverses document paragraphs and inline elements.
    *   Converts `Run` elements into plain text strings.
    *   Converts `InlineUIContainer` containing `InlineChipElement` into the raw string stored in `chip.Value`.
    *   Returns complete string representation.
*   **`SetSerializedText(string text, IEnumerable<InlinePickerItem>? items = null)`**:
    *   Clears the existing document.
    *   Parses the input string against known `InlinePickerItem.Value` definitions.
    *   Reconstructs plain text runs (`Run`) and visual chip UI containers (`InlineUIContainer` holding `InlineChipElement`) in sequence.

### Helper Utilities

*   **`FindVisualAncestor<T>(DependencyObject element)`**:
    *   Walks up the visual tree looking for an ancestor of type `T`. Safely stops if a non-visual `ContentElement` is reached.
*   **`IsVisualDescendant(DependencyObject? root, DependencyObject target)`**:
    *   Checks whether `target` is a visual or logical descendant of `root`.

---

## Supporting Classes

### `PickerItemTemplateSelector`

Inherits from `DataTemplateSelector`. Used internally by `BuildPopupListBox()`.

```csharp
internal class PickerItemTemplateSelector : DataTemplateSelector
```

*   **`SelectTemplate(object item, DependencyObject container)`**:
    *   Returns the `headerTemplate` if `item` is an `InlinePickerItem` with `Group == InlinePickerRichTextBox.HeaderGroupTag`.
    *   Otherwise, returns `selectableTemplate`.