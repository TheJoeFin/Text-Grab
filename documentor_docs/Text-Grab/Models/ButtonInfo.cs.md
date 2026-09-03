# Technical Documentation: `Text-Grab/Models/ButtonInfo.cs`

## Overview

The `ButtonInfo.cs` file defines the data models and metadata needed to represent, configure, and manage actionable UI buttons and commands within the **Text-Grab** application. It contains:
1. `DefaultCheckState` enum: Indicates the default check state for post-grab or action buttons.
2. `ButtonInfo` class: Holds configuration properties for buttons (such as display labels, icons, click handler names, command strings, ordering, and hardware/window context relevance) as well as pre-defined collections of standard application buttons.

---

## Namespace & Imports

* **Namespace**: `Text_Grab.Models`
* **Dependencies**:
  * `System.Collections.Generic`
  * `System.Text.Json.Serialization`
  * `Text_Grab.Controls`
  * `Wpf.Ui.Controls`

---

## Enumerations

### `DefaultCheckState`

Specifies the initial check/selection state of a post-grab or action button.

| Value | Integer | Description |
| :--- | :--- | :--- |
| `Off` | `0` | Default state is disabled/unchecked. |
| `LastUsed` | `1` | Default state reflects the state from the previous usage session. |
| `On` | `2` | Default state is enabled/checked. |

---

## Class: `ButtonInfo`

The `ButtonInfo` class represents the model for individual dynamic or static buttons used throughout Text-Grab UI components (e.g., toolbar, edit window, bottom bar, fullscreen grab controls).

### Properties

| Property Name | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `OrderNumber` | `double` | `0.1` | Numerical index used for ordering and sorting buttons in UI layouts. |
| `ButtonText` | `string` | `""` | The textual label displayed on or associated with the button. |
| `SymbolText` | `string` | `""` | Unicode glyph or symbol character string used for visual icon rendering. |
| `Background` | `string` | `"Transparent"` | Color string representation (hex code or named color) for the button background. |
| `Command` | `string` | `""` | Name of the binding command triggered by the button. |
| `ClickEvent` | `string` | `""` | Name of the event handler method invoked when the button is clicked. |
| `IsSymbol` | `bool` | `false` | Indicates whether the button renders primarily as a symbol icon rather than text. |
| `SymbolIcon` | `SymbolRegular` | `SymbolRegular.Diamond24` | Icon identifier from the WPF UI library. Excluded from JSON serialization via `[JsonIgnore]`. |
| `IsRelevantForFullscreenGrab` | `bool` | `false` | Flags if the action button is applicable to Fullscreen Grab mode. |
| `IsRelevantForEditWindow` | `bool` | `true` | Flags if the action button is applicable to the Edit Window context. Default is `true`. |
| `DefaultCheckState` | `DefaultCheckState` | `DefaultCheckState.Off` | Sets the initial `DefaultCheckState` status for post-grab action buttons. |
| `TemplateId` | `string` | `string.Empty` | Holds the unique identifier if the button represents a Grab Template action; empty otherwise. |
| `RequiresCopilotPlus` | `bool` | `false` | Flags whether the action requires a Copilot+ PC (Windows AI-capable hardware) to function. |

---

### Constructors

#### 1. Default Parameterless Constructor
```csharp
public ButtonInfo()
```
Instantiates a new instance of `ButtonInfo` using default property values.

---

#### 2. Constructor from `CollapsibleButton`
```csharp
public ButtonInfo(CollapsibleButton button)
```
Initializes a `ButtonInfo` object using properties extracted from a `CollapsibleButton` instance:
* If `button.CustomButton` is not null, it populates `ButtonText`, `SymbolText`, `Background`, `Command`, `ClickEvent`, `IsSymbol`, `IsRelevantForFullscreenGrab`, `IsRelevantForEditWindow`, and `DefaultCheckState` directly from `button.CustomButton`.
* If `button.CustomButton` is null, it falls back to copying `ButtonText`, `Background` (as a string), and `IsSymbol` from `button`.

---

#### 3. Parameterized General Constructor
```csharp
public ButtonInfo(string buttonText, string symbolText, string background, string command, string clickEvent, bool isSymbol)
```
Populates primary visual and execution properties (`ButtonText`, `SymbolText`, `Background`, `Command`, `ClickEvent`, `IsSymbol`).

---

#### 4. Parameterized Post-Grab Action Constructor
```csharp
public ButtonInfo(string buttonText, string clickEvent, SymbolRegular symbolIcon, DefaultCheckState defaultCheckState)
```
Configures post-grab actions specifically:
* Sets `ButtonText`, `ClickEvent`, `SymbolIcon`, and `DefaultCheckState`.
* Automatically sets `IsSymbol = true`, `IsRelevantForFullscreenGrab = true`, and `IsRelevantForEditWindow = false`.

---

### Methods

#### `Equals(object? obj)`
* **Returns**: `bool`
* **Behavior**: Evaluates if the passed object `obj` is a non-null `ButtonInfo` and checks if its hash code matches `GetHashCode()`.

#### `GetHashCode()`
* **Returns**: `int`
* **Behavior**: Generates a hash code combining the following properties:
  * `ButtonText`
  * `SymbolText`
  * `Background`
  * `Command`
  * `ClickEvent`
  * `IsRelevantForFullscreenGrab`
  * `IsRelevantForEditWindow`
  * `DefaultCheckState`

---

### Static Pre-defined Button Collections

`ButtonInfo` exposes two static, lazy-initialized collections containing predefined application actions:

#### 1. `DefaultButtonList`
* **Type**: `List<ButtonInfo>`
* **Behavior**: Uses backing field `_defaultButtonList` to lazily construct and return a collection of default toolbar buttons, including:
  * Copy and Close (`CopyCloseBTN_Click`)
  * Make Single Line (`SingleLineCmd`)
  * New Fullscreen Grab (`NewFullscreen_Click`)
  * Open Grab Frame (`OpenGrabFrame_Click`)
  * Find and Replace (`SearchButton_Click`)
  * Edit Bottom Bar (`EditBottomBarMenuItem_Click`)

#### 2. `AllButtons`
* **Type**: `List<ButtonInfo>`
* **Behavior**: Uses backing field `_allButtons` to lazily construct and return a complete catalog of standard buttons available in Text-Grab, organized numerically by `OrderNumber`. Categories of actions included in this list:
  * **1.x - File & Main Actions**: Copy/Close, Insert, File saving, Line joining, Fullscreen Grab, Grab Frame, Find & Replace, Regex/Patterns, Web Search.
  * **2.x - Utility & Navigation**: Settings, File opening, OCR Paste, Launch URL.
  * **3.x - Line & Text Operations**: Trimming, Number/Alpha conversion, Case toggling, Duplicate line removal, Line shuffling, Reserved char replacement, Text unstacking, Add/Remove at index.
  * **4.x - Selection & Line Editing**: Word/Line selection, Move line up/down, Split text, Isolate selection, Selection/Pattern deletion, Insert selection on every line.
  * **5.x - Tools & File/Window Management**: Quick Simple Lookup, File listing, Extract text from image folders, Text file creation per image, New window creation, QR Code generation.
  * **6.x - Spreadsheet & Mode Controls**: Table transposition, Row/Column insertions, moves, deletions, Spreadsheet copy tools, Mode switching (Raw Text, Spreadsheet, Markdown).
  * **7.x - View, Calculation & Window Controls**: Calc pane toggling, Always on top, Window position restoration, Margins, Text wrap, Font selection, Select All/None, Character details, Similar matches.
  * **8.x - Local AI & Copilot+ Actions** (`RequiresCopilotPlus = true`): Summarization, Local AI rewriting, Table conversion, Multi-language translations (System Language, English, Spanish, French, German, Italian, Portuguese, Russian, Japanese, Chinese Simplified, Korean, Arabic, Hindi), AI RegEx extraction.