# Technical Documentation Guide: `Tests/FullscreenGrabPostGrabActionTests.cs`

## Overview

The `FullscreenGrabPostGrabActionTests` class contains unit and UI integration tests for post-grab action logic in the `FullscreenGrab` view (`Text_Grab.Views`). It tests helper methods that manage:
- Key generation for post-grab menu actions (`GetPostGrabActionKey`).
- Context menu item filtering to separate actionable items from utility controls (`GetActionablePostGrabMenuItems`).
- State snapshot generation for post-grab actions, including template toggle exclusivity (`BuildPostGrabActionSnapshot`).
- Persistence evaluation for actions configured with `DefaultCheckState.LastUsed` (`ShouldPersistLastUsedState`).

---

## Class Information

- **Namespace:** `Tests`
- **Class Name:** `FullscreenGrabPostGrabActionTests`
- **Tested Methods in `FullscreenGrab`:**
  - `FullscreenGrab.GetPostGrabActionKey(ButtonInfo)`
  - `FullscreenGrab.GetActionablePostGrabMenuItems(ContextMenu)`
  - `FullscreenGrab.BuildPostGrabActionSnapshot(List<MenuItem>, string, bool)`
  - `FullscreenGrab.ShouldPersistLastUsedState(ButtonInfo, bool, bool, string?)`

---

## Dependencies & Imports

- `System.Windows.Controls` (and explicit alias `MenuItem = System.Windows.Controls.MenuItem`): WPF menu and UI components.
- `Text_Grab.Models`: Contains `ButtonInfo` and `DefaultCheckState`.
- `Text_Grab.Views`: Contains `FullscreenGrab`.
- `Wpf.Ui.Controls`: Provides symbol definitions like `SymbolRegular.Apps24`.
- `Xunit`: Standard test runner framework (`[Fact]`, `[WpfFact]`, `Assert`).

---

## Test Methods

### 1. `GetPostGrabActionKey_UsesTemplateIdForTemplateActions`

* **Test Framework Attribute:** `[Fact]`
* **Purpose:** Verifies that actions containing a `TemplateId` format their lookup key using the `"template:"` prefix followed by the template ID string.
* **Execution Flow:**
  1. Instantiates a `ButtonInfo` action with `TemplateId = "template-123"`.
  2. Calls `FullscreenGrab.GetPostGrabActionKey(action)`.
* **Assertions:**
  - `Assert.Equal("template:template-123", key)`

---

### 2. `GetPostGrabActionKey_FallsBackToButtonTextWhenClickEventMissing`

* **Test Framework Attribute:** `[Fact]`
* **Purpose:** Verifies that if no click handler/template override dictates key generation, key generation falls back to using the `"text:"` prefix appended with `ButtonText`.
* **Execution Flow:**
  1. Instantiates a `ButtonInfo` object providing `ButtonText = "Custom action"`.
  2. Calls `FullscreenGrab.GetPostGrabActionKey(action)`.
* **Assertions:**
  - `Assert.Equal("text:Custom action", key)`

---

### 3. `GetActionablePostGrabMenuItems_ExcludesUtilityEntriesAndPreservesOrder`

* **Test Framework Attribute:** `[WpfFact]` (Requires WPF STA thread context)
* **Purpose:** Tests filtering of a `ContextMenu` to ensure standard actionable items (tagged with `ButtonInfo`) are extracted while utility elements (such as `Separator` controls and items with string tags like `"EditPostGrabActions"` or `"ClosePostGrabMenu"`) are filtered out.
* **Execution Flow:**
  1. Creates a `ContextMenu` containing:
     - First actionable `MenuItem` (Tag set to `ButtonInfo` for "First action").
     - A `Separator`.
     - A utility `MenuItem` (Header "Customize", Tag `"EditPostGrabActions"`).
     - Second actionable `MenuItem` (Tag set to `ButtonInfo` for "Second action").
     - A utility `MenuItem` (Header "Close this menu", Tag `"ClosePostGrabMenu"`).
  2. Passes the `ContextMenu` to `FullscreenGrab.GetActionablePostGrabMenuItems(contextMenu)`.
* **Assertions:**
  - `Assert.Collection` verifies the returned `List<MenuItem>` contains exactly `firstAction` and `secondAction` in their original order.

---

### 4. `BuildPostGrabActionSnapshot_KeepsChangedTemplateCheckedAndUnchecksOthers`

* **Test Framework Attribute:** `[WpfFact]` (Requires WPF STA thread context)
* **Purpose:** Ensures `BuildPostGrabActionSnapshot` updates state snapshots correctly when toggling a template action. Mutually exclusive template actions are unchecked when a new template action is checked, while regular non-template actions retain their checked state.
* **Execution Flow:**
  1. Defines three actions:
     - `regularAction` (Standard action, non-template)
     - `firstTemplate` (`TemplateId = "template-a"`)
     - `secondTemplate` (`TemplateId = "template-b"`)
  2. Constructs a list of `MenuItem` objects:
     - `regularAction` item: `IsChecked = true`
     - `firstTemplate` item: `IsChecked = true`
     - `secondTemplate` item: `IsChecked = false`
  3. Invokes `FullscreenGrab.BuildPostGrabActionSnapshot` with:
     - Target key: Key for `secondTemplate`
     - Target state: `true`
* **Assertions:**
  - `snapshot[key(regularAction)]` is `true` (remains checked).
  - `snapshot[key(firstTemplate)]` is `false` (un-checked due to template exclusivity).
  - `snapshot[key(secondTemplate)]` is `true` (toggled to checked).

---

### 5. `ShouldPersistLastUsedState_ForForcedSourceAction_ReturnsTrue`

* **Test Framework Attribute:** `[Fact]`
* **Purpose:** Verifies that `ShouldPersistLastUsedState` returns `true` when an action configured with `DefaultCheckState.LastUsed` matches the `forcePersistActionKey` parameter.
* **Execution Flow:**
  1. Instantiates `ButtonInfo` with `DefaultCheckState.LastUsed`.
  2. Generates its key via `FullscreenGrab.GetPostGrabActionKey(lastUsedAction)`.
  3. Calls `FullscreenGrab.ShouldPersistLastUsedState` passing `previousChecked: true`, `isChecked: true`, and `forcePersistActionKey` set to the action key.
* **Assertions:**
  - `Assert.True(shouldPersist)`

---

### 6. `ShouldPersistLastUsedState_DoesNotPersistUnchangedNonSourceAction`

* **Test Framework Attribute:** `[Fact]`
* **Purpose:** Verifies that `ShouldPersistLastUsedState` returns `false` for an action with `DefaultCheckState.LastUsed` when it is not the source action triggering the forced persistence update and its check state has not changed (`previousChecked` equals `isChecked`).
* **Execution Flow:**
  1. Instantiates `ButtonInfo` with `DefaultCheckState.LastUsed`.
  2. Calls `FullscreenGrab.ShouldPersistLastUsedState` passing `previousChecked: true`, `isChecked: true`, and no `forcePersistActionKey` parameter (defaults to `null`).
* **Assertions:**
  - `Assert.False(shouldPersist)`