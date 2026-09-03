# Documentation: `Text-Grab/Controls/WordBorder.xaml.cs`

## Overview

The `WordBorder` control is a custom WPF `UserControl` that represents an interactive, selectable bounding box around a unit of text (or barcode) identified by Optical Character Recognition (OCR) in the **Text-Grab** application.

It supports editing, text transformation, canvas positioning, context menu operations, dynamic AI-assisted translation, output template indexing, and WPF UI Automation.

---

## Class Signature & Attributes

```csharp
namespace Text_Grab.Controls;

[DebuggerDisplay("{Word} : Size {Width}:{Height} Pos. {Left}:{Top} Table {ResultRowID}:{ResultColumnID}")]
public partial class WordBorder : UserControl, INotifyPropertyChanged
```

*   **Base Class**: `UserControl`
*   **Interfaces**: `INotifyPropertyChanged`
*   **Debugger Display**: Displays the word text, bounding size, canvas position, and row/column grid indices during debugging.

---

## Dependency Properties

| Property Name | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `WordProperty` | `string` | `string.Empty` | Backing property for `Word`. Triggers `OnWordChanged` to sync with `DisplayText`. |
| `DisplayTextProperty` | `string` | `string.Empty` | Backing property for `DisplayText`. Triggers `OnDisplayTextChanged` to sync with `Word` and raise UI Automation events. |
| `KeepSingleLineOutputProperty` | `bool` | `false` | Indicates whether newlines should be removed for single-line output. Triggers `OnLayoutPropertyChanged`. |
| `DisplayLineHeightProperty` | `double` | `0d` | Configures line height formatting for rendered text inside the control. Triggers `OnLayoutPropertyChanged`. |
| `TemplateIndexProperty` | `int` | `0` | Holds the index of the word within a dynamic output template. |

---

## Key Properties

### Positioning & Canvas Integration
*   `Left` (`double`): Sets/gets the X coordinate on a WPF `Canvas` via `Canvas.SetLeft`.
*   `Top` (`double`): Sets/gets the Y coordinate on a WPF `Canvas` via `Canvas.SetTop`.
*   `Right` (`double`, read-only): Returns `Left + Width`.
*   `Bottom` (`double`, read-only): Returns `Top + Height`.

### OCR & Table Grid Context
*   `LineNumber` (`int`): The line index where this word appears.
*   `ResultRowID` (`int`): Row coordinate in tabular layout output.
*   `ResultColumnID` (`int`): Column coordinate in tabular layout output.
*   `IsBarcode` (`bool`): Identifies whether the element represents a recognized barcode rather than standard text.

### Visual & State Properties
*   `IsSelected` (`bool`): Indicates if the control is selected.
*   `IsEditing` (`bool`, read-only): Returns `true` if `EditWordTextBox` is currently focused.
*   `IsFromEditWindow` (`bool`): Indicates if the control originated from an editing window context.
*   `WasRegionSelected` (`bool`): Tracks whether the region was selected.
*   `TemplateBadgeVisibility` (`Visibility`, read-only): Returns `Visibility.Visible` if `TemplateIndex > 0`.
*   `TemplateBadgeText` (`string`, read-only): Formats the template index as `"{TemplateIndex}"` when `TemplateIndex > 0`.
*   `MatchingBackground` (`SolidColorBrush`): Sets background color and dynamically computes luma (`0.2126 * R + 0.7152 * G + 0.0722 * B`). If luma exceeds 180, sets the text foreground color to black for contrast.
*   `OwnerGrabFrame` (`GrabFrame?`): Reference to the parent `GrabFrame` view controlling this `WordBorder`.

---

## Internal UI Automation Support

The control provides native accessibility/automation support using a nested `FrameworkElementAutomationPeer` implementation implementing `IValueProvider`.

### `WordBorderAutomationPeer`
*   **Automation Control Type**: `AutomationControlType.Edit`
*   **Patterns Supported**: `PatternInterface.Value`
*   **Properties Provided**: Reads/writes `Value` mapped to `WordBorder.DisplayText`.
*   **Events**: Fires `RaisePropertyChangedEvent` (via `ValuePatternIdentifiers.ValueProperty`) when text changes.

---

## Core Functionality & Methods

### Constructors & Initialization

1.  **`WordBorder()`**: Default constructor. Calls `StandardInitialization()`.
2.  **`WordBorder(WordBorderInfo info)`**: Parametric constructor initialized using a `WordBorderInfo` DTO. Sets properties, positioning, UI Automation IDs (`AutomationProperties.SetAutomationId`), and background color.
3.  **`StandardInitialization()`**:
    *   Initializes component XAML.
    *   Sets `DataContext = this`.
    *   Attaches event handlers (`Loaded`, `SizeChanged`).
    *   Attaches a placeholder empty `ContextMenu` (lazily built upon opening to minimize memory allocation per OCR pass).
    *   Configures a 300ms `debounceTimer` for text changes.

### Property Synchronization & Text Layout

*   **`OnWordChanged` / `OnDisplayTextChanged`**: Bidirectional property synchronization between `Word` and `DisplayText` guarded by `isSyncingTextProperties` to prevent infinite loop re-entrancy.
*   **`ApplyTextLayout()`**:
    *   If `IsBarcode` is true, styling is handled by `SetAsBarcode()`.
    *   If `KeepSingleLineOutput` is true and `DisplayLineHeight > 0`, text wrapping is enabled (`TextWrapping.Wrap`), and line height stack properties are applied.
    *   Otherwise, wrapping is disabled (`TextWrapping.NoWrap`) and dynamic width/height constraints are cleared.
*   **`DebounceTimer_Tick`**: When text changes, this 300ms timer debounces rapid inputs before notifying `OwnerGrabFrame.WordChanged()`.

### Selection & Highlight Management

*   **`Select()`**: Marks `IsSelected = true` and updates border brush color to `Colors.Orange`.
*   **`Deselect()`**: Marks `IsSelected = false` and resets the border brush according to template state.
*   **`SetHighlightedForOutput(bool isHighlighted)`**: Sets `_isInOutputPattern` and updates border color (Orange if highlighted in an output template, Teal `#308E98` otherwise).
*   **`ApplyTemplateStateBorderBrush()`**: Evaluates `_isInOutputPattern` and updates `WordBorderBorder` and `MoveResizeBorder` brush colors accordingly.

### Interactivity & Editing Modes

*   **`EnterEdit()` / `ExitEdit()`**: Toggles visibility of `EditWordTextBox` and adjusts background opacity.
*   **`FocusTextbox()`**: Focuses `EditWordTextBox` and selects all text.
*   **`SetAsBarcode()`**: Updates layout properties for displaying barcodes (wrapped text, center alignment, font size 14, and sets background to blue if text is a valid URI).
*   **`IntersectsWith(Rect rectToCheck)`**: Returns whether the control's bounding rect overlaps with `rectToCheck`.

---

## Context Menu & Command Handling

The context menu is constructed dynamically upon opening via `EnsureContextMenuItems` and `EditWordTextBox_ContextMenuOpening`.

### Static Context Menu Options
*   **Copy Text**: Copies `Word` to system clipboard.
*   **Try To Make Numbers**: Uses extension method `.TryFixToNumbers()` on text or active text selection.
*   **Try To Make Letters**: Uses extension method `.TryFixToLetters()` on text or active text selection.
*   **Make Text Single Line**: Strips line breaks via `.MakeStringSingleLine()`.
*   **Merge Selected Word Borders**: Binds to `MergeWordsCommand` (shortcut `Ctrl + M`).
*   **Break into words**: Delegates to `OwnerGrabFrame.BreakWordBorderIntoWords()`.
*   **Search for similar text**: Delegates to `OwnerGrabFrame.SearchForSimilar()`.
*   **Delete**: Delegates to `OwnerGrabFrame.DeleteThisWordBorder()`.
*   **URL Context Item**: If `Word` is formatted as a valid absolute URI, adds an item ("Try to go to: ...") to launch the URL in a default web browser process.

### Dynamic AI Translation
*   If device support is validated via `WindowsAiUtilities.CanDeviceUseWinAI()`, a menu item titled `"Translate to [Language]"` is displayed.
*   **`TranslateWordMenuItem_Click()`**: Obtains system language via `LanguageUtilities.GetSystemLanguageForTranslation()`, requests async translation via `WindowsAiUtilities.TranslateText()`, pushes an undoable change via `OwnerGrabFrame.UndoableWordChange()`, and updates `Word`.

---

## Event Handlers & Input Interactions

*   **`WordBorderControl_MouseDown`**: Left-click toggles selection status. Right-click is ignored to allow context menu display.
*   **`WordBorderControl_MouseDoubleClick`**:
    *   If textbox is collapsed: calls `EnterEdit()`.
    *   If already open: copies text to clipboard, presents notification toast (if enabled in settings), or inserts text into open target window (if `IsFromEditWindow` is true).
*   **`EditWordTextBox_GotFocus`**: Selects the border and notifies `OwnerGrabFrame.FreezeFrameForWordEditing()` to pause overlay refreshes.
*   **`MoveResizeBorder_MouseDown` / `SizeHandle_MouseDown`**: Begins interactive repositioning/resizing operations handled by `OwnerGrabFrame.StartWordBorderMoveResize()`.
*   **`WordBorder_MouseEnter` / `WordBorder_MouseLeave`**: Displays move/resize borders when `Ctrl` key is depressed.

---

## Cleanup & Unloading

To prevent memory leaks when controls are detaching from UI trees:
*   **`WordBorderControl_Unloaded`**:
    *   Unsubscribes mouse click, loaded, size-changed, and unloaded handlers.
    *   Stops and unsubscribes `debounceTimer`.
    *   Clears reference to `OwnerGrabFrame`.