# Developer Documentation: `Tests/FullscreenGrabSelectionStyleTests.cs`

## Overview

The `FullscreenGrabSelectionStyleTests` class is a unit test suite within the `Tests` namespace. It provides automated tests using xUnit to verify the behavior of selection style logic and candidate processing within the **Text-Grab** application's Fullscreen Grab feature (`FullscreenGrab` and `WindowSelectionCandidate`).

- **File Path:** `Tests/FullscreenGrabSelectionStyleTests.cs`
- **Target System:** Text-Grab Fullscreen Grab logic (`Text_Grab.Views.FullscreenGrab`, `Text_Grab.Models.WindowSelectionCandidate`, and `Text_Grab.Models.FsgSelectionStyle`)
- **Testing Framework:** xUnit (`[Fact]`, `[Theory]`, `[InlineData]`, `Assert`)

---

## Dependencies and Imports

- `System.Windows`: Provides WPF types such as `Rect`.
- `Text_Grab`: Core namespace for the Text-Grab application.
- `Text_Grab.Models`: Contains data models like `FsgSelectionStyle` and `WindowSelectionCandidate`.
- `Text_Grab.Views`: Contains view logic, specifically the static/helper methods on `FullscreenGrab`.

---

## Class Structure

```csharp
namespace Tests;

public class FullscreenGrabSelectionStyleTests
```

The class contains five unit test methods: three parameterized data-driven tests (`[Theory]`) and two standard unit tests (`[Fact]`).

---

## Detailed Test Method Reference

### 1. `ShouldKeepTopToolbarVisible_MatchesSelectionState`

```csharp
[Theory]
[InlineData(FsgSelectionStyle.Window, false, true)]
[InlineData(FsgSelectionStyle.Window, true, true)]
[InlineData(FsgSelectionStyle.Region, true, true)]
[InlineData(FsgSelectionStyle.Region, false, false)]
[InlineData(FsgSelectionStyle.Freeform, false, false)]
[InlineData(FsgSelectionStyle.AdjustAfter, false, false)]
public void ShouldKeepTopToolbarVisible_MatchesSelectionState(
    FsgSelectionStyle selectionStyle,
    bool isAwaitingAdjustAfterCommit,
    bool expected)
```

- **Purpose:** Verifies whether `FullscreenGrab.ShouldKeepTopToolbarVisible` returns the expected boolean value based on the current selection style and whether the selection is awaiting an "Adjust After" commit.
- **Tested Method:** `FullscreenGrab.ShouldKeepTopToolbarVisible(FsgSelectionStyle, bool)`
- **Test Matrix / Inline Data:**

| `selectionStyle` | `isAwaitingAdjustAfterCommit` | Expected Result (`expected`) |
| :--- | :--- | :--- |
| `FsgSelectionStyle.Window` | `false` | `true` |
| `FsgSelectionStyle.Window` | `true` | `true` |
| `FsgSelectionStyle.Region` | `true` | `true` |
| `FsgSelectionStyle.Region` | `false` | `false` |
| `FsgSelectionStyle.Freeform` | `false` | `false` |
| `FsgSelectionStyle.AdjustAfter` | `false` | `false` |

---

### 2. `ShouldUseOverlayCutout_MatchesSelectionStyle`

```csharp
[Theory]
[InlineData(FsgSelectionStyle.Region, true)]
[InlineData(FsgSelectionStyle.Window, false)]
[InlineData(FsgSelectionStyle.Freeform, false)]
[InlineData(FsgSelectionStyle.AdjustAfter, true)]
public void ShouldUseOverlayCutout_MatchesSelectionStyle(FsgSelectionStyle selectionStyle, bool expected)
```

- **Purpose:** Asserts that `FullscreenGrab.ShouldUseOverlayCutout` correctly determines if an overlay cutout rendering mode should be active for a given selection style.
- **Tested Method:** `FullscreenGrab.ShouldUseOverlayCutout(FsgSelectionStyle)`
- **Test Matrix / Inline Data:**

| `selectionStyle` | Expected Result (`expected`) |
| :--- | :--- |
| `FsgSelectionStyle.Region` | `true` |
| `FsgSelectionStyle.Window` | `false` |
| `FsgSelectionStyle.Freeform` | `false` |
| `FsgSelectionStyle.AdjustAfter` | `true` |

---

### 3. `ShouldDrawSelectionOutline_MatchesSelectionStyle`

```csharp
[Theory]
[InlineData(FsgSelectionStyle.Region, true)]
[InlineData(FsgSelectionStyle.Window, false)]
[InlineData(FsgSelectionStyle.Freeform, false)]
[InlineData(FsgSelectionStyle.AdjustAfter, true)]
public void ShouldDrawSelectionOutline_MatchesSelectionStyle(FsgSelectionStyle selectionStyle, bool expected)
```

- **Purpose:** Tests whether `FullscreenGrab.ShouldDrawSelectionOutline` returns the expected boolean indicating if a visual selection outline should be rendered for the provided selection style.
- **Tested Method:** `FullscreenGrab.ShouldDrawSelectionOutline(FsgSelectionStyle)`
- **Test Matrix / Inline Data:**

| `selectionStyle` | Expected Result (`expected`) |
| :--- | :--- |
| `FsgSelectionStyle.Region` | `true` |
| `FsgSelectionStyle.Window` | `false` |
| `FsgSelectionStyle.Freeform` | `false` |
| `FsgSelectionStyle.AdjustAfter` | `true` |

---

### 4. `ShouldCommitWindowSelection_RequiresSameWindowHandleOnMouseUp`

```csharp
[Fact]
public void ShouldCommitWindowSelection_RequiresSameWindowHandleOnMouseUp()
```

- **Purpose:** Validates the conditions required to commit a window selection in `FullscreenGrab.ShouldCommitWindowSelection`.
- **Tested Method:** `FullscreenGrab.ShouldCommitWindowSelection(WindowSelectionCandidate?, WindowSelectionCandidate?)`
- **Behavior Validated:**
  - **Returns `true`** when the pressed candidate and released candidate share the exact same window handle (`(nint)1`).
  - **Returns `false`** when the pressed candidate and released candidate have different window handles (`(nint)1` vs `(nint)2`).
  - **Returns `false`** if the released candidate is `null`.
  - **Returns `false`** if the pressed candidate is `null`.

---

### 5. `WindowSelectionCandidate_DisplayText_UsesFallbacksWhenMetadataMissing`

```csharp
[Fact]
public void WindowSelectionCandidate_DisplayText_UsesFallbacksWhenMetadataMissing()
```

- **Purpose:** Ensures that `WindowSelectionCandidate` uses appropriate fallback string values for application names and window titles when metadata (such as the title string) is empty.
- **Tested Component:** `WindowSelectionCandidate` model properties `DisplayAppName` and `DisplayTitle`.
- **Behavior Validated:**
  - Instantiates `WindowSelectionCandidate` with handle `(nint)1`, bounds `Rect(0, 0, 40, 40)`, empty title string (`string.Empty`), and process ID `100`.
  - Verifies `candidate.DisplayAppName` falls back to `"Application"`.
  - Verifies `candidate.DisplayTitle` falls back to `"Untitled window"`.