# `UiAutomationContractTests.cs` Documentation

## Overview

The `UiAutomationContractTests` class contains xUnit unit tests designed to validate UI Automation contracts and selector stability at the source-code level. Rather than launching the runtime application, these tests perform static checks on XAML files and C# code files within the repository to ensure UI selectors, `AutomationId`s, and custom UI Automation peers remain valid, unique, and present.

---

## Class Details

- **Namespace**: `Tests`
- **Class Name**: `UiAutomationContractTests`

---

## Methods

### Test Methods (`[Fact]`)

#### 1. `RequiredAutomationIds_ArePresentAndUniqueInXaml()`
Validates that critical UI elements defined in XAML files maintain required `AutomationId` attributes and that all `AutomationId` attributes across the `Text-Grab` project are strictly unique.

* **How it works**:
  1. Finds the repository root directory using `FindRepositoryRoot()`.
  2. Defines a mapping of XAML file paths to their required `AutomationId` values (`requiredIds`).
  3. Enumerates all `*.xaml` files in the `Text-Grab` directory recursively.
  4. Parses each XAML document using `XDocument.Load` with line info setting (`LoadOptions.SetLineInfo`).
  5. Scans all XML attributes looking for attributes named `AutomationId` or ending with `.AutomationId`.
  6. Collects all occurrences of each `AutomationId` value and tracks which file path(s) they are located in.
  7. **Assertions**:
     - Ensures every required `AutomationId` specified in `requiredIds` exists.
     - Confirms each required `AutomationId` appears exactly once across the repository.
     - Checks that each required `AutomationId` resides in its corresponding relative XAML file path.
     - Confirms that **all** scanned `AutomationId` attributes across all XAML files in the `Text-Grab` folder are strictly unique (location count equals 1).

* **Mapped Required XAML Files and IDs**:
  | Relative Path | Required Automation IDs |
  | :--- | :--- |
  | `Views\FirstRunWindow.xaml` | `FirstRunWindow`, `FirstRun.StartButton`, `FirstRun.DefaultFullscreenRadio`, `FirstRun.BackgroundToggle` |
  | `Views\SettingsWindow.xaml` | `SettingsWindow`, `Settings.Navigation`, `Settings.Nav.General`, `Settings.Nav.Danger` |
  | `Views\EditTextWindow.xaml` | `EditTextWindow`, `EditText.Editor`, `EditText.StatusText`, `EditText.LoadingStatus`, `EditText.Menu.ClipboardWatcher` |
  | `Views\QuickSimpleLookup.xaml` | `QuickLookupWindow`, `QuickLookup.Search`, `QuickLookup.ResultsGrid`, `QuickLookup.CopySelectedButton`, `QuickLookup.ErrorStatus` |
  | `Views\FullscreenGrab.xaml` | `FullscreenGrabWindow`, `FullscreenGrab.SelectionCanvas`, `FullscreenGrab.Language`, `FullscreenGrab.AcceptSelectionButton` |
  | `Views\GrabFrame.xaml` | `GrabFrameWindow`, `GrabFrame.ZoomSurface`, `GrabFrame.WordBordersCanvas`, `GrabFrame.GrabButton`, `GrabFrame.Status` |
  | `Controls\NotifyIconWindow.xaml` | `NotifyIconWindow`, `NotifyIcon`, `NotifyIcon.Menu.Settings`, `NotifyIcon.Menu.Close` |
  | `Controls\FindAndReplaceWindow.xaml` | `FindReplaceDialog`, `FindReplace.Search`, `FindReplace.Results` |
  | `Controls\RegexEditorDialog.xaml` | `RegexEditorDialog`, `RegexEditor.Pattern`, `RegexEditor.Error` |
  | `Controls\PatternMatchModeDialog.xaml` | `PatternMatchDialog`, `PatternMatch.Indices`, `PatternMatch.IndicesError` |
  | `Pages\KeysSettings.xaml` | `Settings.ShortcutsPage`, `Settings.Shortcuts.GlobalHotkeysToggle`, `Settings.Shortcuts.FullscreenGrab` |

---

#### 2. `WordBorders_ExposeValuePatternThroughDedicatedAutomationPeer()`
Ensures that the `WordBorder` control explicitly provides a dedicated Automation Peer that exposes the UI Automation `Value` pattern interface.

* **How it works**:
  1. Reads the source code text of `Text-Grab/Controls/WordBorder.xaml.cs`.
  2. **Assertions**:
     - Asserts that the source code contains `"OnCreateAutomationPeer"`.
     - Asserts that the source code contains `"IValueProvider"`.
     - Asserts that the source code contains `"PatternInterface.Value"`.

---

#### 3. `RuntimeAutomationSelectors_AreDerivedFromStableOwners()`
Validates that dynamically generated or runtime automation selectors in specific controls follow expected stable naming conventions.

* **How it works**:
  1. Reads source text from `Text-Grab/Controls/ShortcutControl.xaml.cs` and `Text-Grab/Controls/WordBorder.xaml.cs`.
  2. **Assertions**:
     - Asserts `ShortcutControl.xaml.cs` contains string patterns: `"{automationId}.Record"` and `"{automationId}.Enabled"`.
     - Asserts `WordBorder.xaml.cs` contains string pattern: `"WordBorder.{ResultRowID}.{ResultColumnID}"`.

---

### Private Helper Methods

#### `FindRepositoryRoot()`
Locates the root directory of the repository containing the `Text-Grab.sln` solution file.

* **Return Type**: `string`
* **How it works**:
  1. Starts at the execution base directory (`AppContext.BaseDirectory`).
  2. Iteratively checks parent directories to see if `Text-Grab.sln` exists in that directory.
  3. Returns the full directory path if found.
  4. If the traversal reaches `null` without finding the solution file, falls back to a relative path calculated four levels up from `AppContext.BaseDirectory` (`Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."))`).

---

## Dependencies & Imports

- `System`
- `System.Collections.Generic`
- `System.IO`
- `System.Linq`
- `System.Xml.Linq`
- `Xunit` (provides `[Fact]` and `Assert`)