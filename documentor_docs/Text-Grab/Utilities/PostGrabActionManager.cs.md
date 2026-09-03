# Technical Documentation: `PostGrabActionManager.cs`

**Class Namespace:** `Text_Grab.Utilities`  
**Class Name:** `PostGrabActionManager`  
**File Path:** `Text-Grab/Utilities/PostGrabActionManager.cs`

---

## 1. Overview

The `PostGrabActionManager` class is a static utility manager in Text-Grab responsible for defining, configuring, persisting, and executing post-grab actions. Post-grab actions are operations or transformations applied to text (and optional spatial context) after OCR (Optical Character Recognition) or text extraction takes place.

This class serves as the central hub for:
* Building lists of default and custom post-grab actions.
* Integrating saved templates (`GrabTemplate`) as actionable post-grab buttons.
* Persisting enabled action lists and button check states (including `LastUsed` state tracking) via `TextGrabSettingsService`.
* Executing text modification or external operations based on string event identifiers (`ClickEvent`).

---

## 2. Class Architecture & Dependencies

### External Dependencies & Types Used
* **Models & Interfaces:** `ButtonInfo`, `GrabTemplate`, `PostGrabContext`, `WebSearchUrlModel`, `DefaultCheckState`
* **Services & Utilities:**
  * `AppUtilities.TextGrabSettingsService`: Persistence layer for saved actions and check states.
  * `GrabTemplateManager`: Interface for querying templates, creating template `ButtonInfo` objects, and recording template usage.
  * `GrabTemplateExecutor`: Handles async execution of template-based OCR operations.
  * `Singleton<T>`: Used to access singleton instances of `WebSearchUrlModel` and `TtsService`.
  * `TtsService`: Text-To-Speech execution service.
  * `WindowsAiUtilities` & `LanguageUtilities`: Handles device AI capabilities check and AI translation.
* **UI & WPF Control Libraries:** `Wpf.Ui.Controls.SymbolRegular` (for button icons), `System.Windows.Rect`.
* **System Framework APIs:** `System.Net.WebUtility`, `Windows.System.Launcher`.

---

## 3. Public Static Methods

### 3.1 Action Retrieval & Management

#### `GetAvailablePostGrabActions()`
```csharp
public static List<ButtonInfo> GetAvailablePostGrabActions()
```
* **Purpose:** Assembles all actions suitable for display/selection, sorted by `OrderNumber`.
* **Logic:**
  1. Starts with the default post-grab actions array from `GetDefaultPostGrabActions()`.
  2. Queries `ButtonInfo.AllButtons` for actions where `IsRelevantForFullscreenGrab` is `true` and the action is not already present in the list (matched by `ButtonText`).
  3. Retrieves saved templates via `GrabTemplateManager.GetAllTemplates()` and converts each into a `ButtonInfo` object using `GrabTemplateManager.CreateButtonInfoForTemplate(template)`. Ensures no duplicate template entries exist (matched by `TemplateId`).
  4. Returns the compiled list ordered by `OrderNumber`.

#### `GetDefaultPostGrabActions()`
```csharp
public static List<ButtonInfo> GetDefaultPostGrabActions()
```
* **Purpose:** Returns the hardcoded standard set of post-grab actions provided by Text-Grab.
* **Returns:** A `List<ButtonInfo>` containing the following 6 actions:
  1. **"Fix GUIDs"** (`CorrectGuid_Click`, `SymbolRegular.Braces24`, Order: `6.1`)
  2. **"Trim each line"** (`TrimEachLine_Click`, `SymbolRegular.TextCollapse24`, Order: `6.2`)
  3. **"Remove duplicate lines"** (`RemoveDuplicateLines_Click`, `SymbolRegular.MultiselectLtr24`, Order: `6.3`)
  4. **"Web Search"** (`WebSearch_Click`, `SymbolRegular.GlobeSearch24`, Order: `6.4`)
  5. **"Try to insert text"** (`Insert_Click`, `SymbolRegular.ClipboardTaskAdd24`, Order: `6.5`)
  6. **"Speak text"** (`SpeakText_Click`, `SymbolRegular.Speaker224`, Order: `6.6`)
* *Note: All default actions are instantiated with `DefaultCheckState.Off`.*

#### `GetEnabledPostGrabActions()`
```csharp
public static List<ButtonInfo> GetEnabledPostGrabActions()
```
* **Purpose:** Loads custom saved post-grab actions configured by the user.
* **Logic:** Loads custom actions from `AppUtilities.TextGrabSettingsService.LoadPostGrabActions()`. If the returned list is empty, it falls back to returning `GetDefaultPostGrabActions()`.

#### `SavePostGrabActions(List<ButtonInfo> actions)`
```csharp
public static void SavePostGrabActions(List<ButtonInfo> actions)
```
* **Purpose:** Saves the specified list of custom actions to application settings via `AppUtilities.TextGrabSettingsService.SavePostGrabActions(actions)`.

---

### 3.2 Check State Persistence

#### `GetCheckState(ButtonInfo action)`
```csharp
public static bool GetCheckState(ButtonInfo action)
```
* **Purpose:** Determines whether a given action should be checked/enabled by default.
* **Logic:**
  1. Loads stored check states via `AppUtilities.TextGrabSettingsService.LoadPostGrabCheckStates()`.
  2. If stored check states exist, contains a key matching `action.ButtonText`, and the action's `DefaultCheckState` is set to `DefaultCheckState.LastUsed`, it returns the stored boolean state.
  3. Otherwise, it falls back to checking if `action.DefaultCheckState == DefaultCheckState.On`.

#### `SaveCheckState(ButtonInfo action, bool isChecked)`
```csharp
public static void SaveCheckState(ButtonInfo action, bool isChecked)
```
* **Purpose:** Persists the checked state of an action (primarily for actions utilizing `DefaultCheckState.LastUsed`).
* **Logic:** Loads the existing check state dictionary, updates `checkStates[action.ButtonText] = isChecked`, and saves it back via `AppUtilities.TextGrabSettingsService.SaveCheckState(...)`.

---

### 3.3 Action Execution

#### `ExecutePostGrabAction(ButtonInfo action, string text)`
```csharp
public static async Task<string> ExecutePostGrabAction(ButtonInfo action, string text)
```
* **Purpose:** Overload method that wraps a simple text string into a `PostGrabContext` using `PostGrabContext.TextOnly(text)` and invokes the full context handler.

#### `ExecutePostGrabAction(ButtonInfo action, PostGrabContext context)`
```csharp
public static async Task<string> ExecutePostGrabAction(ButtonInfo action, PostGrabContext context)
```
* **Purpose:** Primary execution engine that processes a post-grab action on the provided `PostGrabContext`.
* **Return Value:** Returns a `Task<string>` representing the modified text resulting from the action.

---

## 4. Supported Post-Grab Action Click Events

The `ExecutePostGrabAction` method handles actions by evaluating `action.ClickEvent` in a switch block:

| `ClickEvent` Key | Action Description | Behavior / Logic | Text Modified? |
| :--- | :--- | :--- | :--- |
| `"CorrectGuid_Click"` | Fix GUIDs | Calls extension method `text.CorrectCommonGuidErrors()`. | Yes |
| `"TrimEachLine_Click"` | Trim each line | Splits text by `Environment.NewLine`. Removes null/whitespace lines, trims each line, and re-joins them with `Environment.NewLine` plus a trailing newline. Returns `string.Empty` if no lines remain. | Yes |
| `"RemoveDuplicateLines_Click"`| Remove duplicate lines | Calls extension method `text.RemoveDuplicateLines()`. | Yes |
| `"WebSearch_Click"` | Web Search | URL-encodes text using `WebUtility.UrlEncode(text)`, creates URI using `Singleton<WebSearchUrlModel>.Instance.DefaultSearcher`, and opens standard web browser via `Launcher.LaunchUriAsync(...)`. | No |
| `"Insert_Click"` | Try to insert text | Placeholder/deferred action. Execution is handled separately in `FullscreenGrab` after closing. | No |
| `"SpeakText_Click"` | Speak text | Passes text to `Singleton<TtsService>.Instance.Speak(text)` for Text-To-Speech execution. | No |
| `"Translate_Click"` | Translate to system language | Checks `WindowsAiUtilities.CanDeviceUseWinAI()`. If true, retrieves target language via `LanguageUtilities.GetSystemLanguageForTranslation()` and calls `WindowsAiUtilities.TranslateText(text, systemLanguage)`. | Yes (if WinAI supported) |
| `"ApplyTemplate_Click"` | Apply Grab Template | Validates `action.TemplateId` is present and `context.CaptureRegion != Rect.Empty`. Fetches template via `GrabTemplateManager.GetTemplateById(...)`, executes template re-OCR via `GrabTemplateExecutor.ExecuteTemplateAsync(...)`, and calls `GrabTemplateManager.RecordUsage(...)`. | Yes |
| `default` | Unknown | Unrecognized click event string returns text unchanged. | No |