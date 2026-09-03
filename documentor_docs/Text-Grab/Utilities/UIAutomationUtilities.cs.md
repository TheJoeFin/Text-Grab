# Technical Documentation: `Text-Grab.Utilities.UIAutomationUtilities`

## Overview

The `UIAutomationUtilities` class is a core utility in Text-Grab that leverages Windows UI Automation (`System.Windows.Automation`) to programmatically extract text and construct UI overlay snapshots from specified screen coordinates, rectangular regions, or target window handles.

It provides asynchronous public methods that traverse UI automation element trees, query UIA control patterns (`TextPattern`, `ValuePattern`), fall back to element properties (`Name`), and normalize/deduplicate extracted text.

---

## Technical Constants & Configuration Defaults

### Traversal Depths
* **`FastMaxDepth = 2`**: Depth limit when `UiAutomationTraversalMode.Fast` is specified.
* **`BalancedMaxDepth = 6`**: Depth limit when `UiAutomationTraversalMode.Balanced` is specified.
* **`ThoroughMaxDepth = 12`**: Depth limit when `UiAutomationTraversalMode.Thorough` is specified.
* **`MaxPointAncestorDepth = 5`**: Maximum ancestor levels checked when looking up text sources at specific screen coordinates.

---

## Data Structures

### Enums

#### `AutomationTextSource`
Defines the source technique used to acquire text from an automation element. The integer values represent extraction precedence (higher values are preferred):
* `None = 0`
* `NameFallback = 1`
* `TextPattern = 2`
* `ValuePattern = 3`
* `PointTextPattern = 4`

### Internal Record Structs

* **`TextExtractionCandidate(string Text, AutomationTextSource Source, int Depth)`**: Holds extracted candidate text along with its source type and tree depth level.
* **`WindowPointCandidate(TextExtractionCandidate Candidate, double Area)`**: Holds candidate text for point extraction along with the window bounding area (used for candidate ranking).
* **`OverlayCandidate(UiAutomationOverlayItem Item, AutomationTextSource Source, int Depth)`**: Holds an overlay item along with its extraction source and depth.

---

## Public API

The public API methods execute work on background threads (`Task.Run`) and return asynchronous results.

### Point-Based Text Extraction

#### `GetTextFromPointAsync(Point screenPoint)`
#### `GetTextFromPointAsync(Point screenPoint, IReadOnlyCollection<IntPtr>? excludedHandles)`
* **Description**: Extracts text located at a specific physical screen point.
* **Parameters**:
  * `screenPoint`: Target screen coordinates (`System.Windows.Point`).
  * `excludedHandles`: Optional collection of window handles (`IntPtr`) to exclude (e.g., Text-Grab's own windows).
* **Returns**: `Task<string>` containing normalized, extracted text, or `string.Empty` if no text is found.

### Region-Based Text Extraction

#### `GetTextFromRegionAsync(Rect screenRect)`
#### `GetTextFromRegionAsync(Rect screenRect, IReadOnlyCollection<IntPtr>? excludedHandles)`
* **Description**: Extracts text contained within a bounding rectangular region on screen.
* **Parameters**:
  * `screenRect`: Target rectangular region (`System.Windows.Rect`).
  * `excludedHandles`: Optional collection of window handles (`IntPtr`) to exclude.
* **Returns**: `Task<string>` containing newline-separated, normalized, deduplicated text.

### Window-Based Text Extraction

#### `GetTextFromWindowAsync(IntPtr windowHandle, Rect? filterBounds = null)`
* **Description**: Traverses the UI Automation tree rooted at the specified native window handle and extracts all readable text.
* **Parameters**:
  * `windowHandle`: The target window handle (`IntPtr`).
  * `filterBounds`: Optional clipping bounds (`System.Windows.Rect?`).
* **Returns**: `Task<string>` containing newline-separated extracted text.

### Region Overlay Snapshot Creation

#### `GetOverlaySnapshotFromRegionAsync(Rect screenRect)`
#### `GetOverlaySnapshotFromRegionAsync(Rect screenRect, IReadOnlyCollection<IntPtr>? excludedHandles)`
* **Description**: Captures structured overlay metadata (`UiAutomationOverlaySnapshot`) for elements within the target rectangle.
* **Returns**: `Task<UiAutomationOverlaySnapshot?>` containing the list of discovered `UiAutomationOverlayItem` objects sorted by screen position, or `null` if the region is invalid or no window is found.

---

## Core Operational Logic & Algorithms

### 1. Options and Settings Resolution (`GetOptionsFromSettings`)
Resolves runtime options (`UiAutomationOptions`) from application configuration settings (`AppUtilities.TextGrabSettings`):
* `UiAutomationTraversalMode`: Configures traversal depth limit (`Fast`, `Balanced`, or `Thorough`).
* `UiAutomationIncludeOffscreen`: Determines whether offscreen elements are included.
* `UiAutomationPreferFocusedElement`: Toggles prioritization of the currently focused control.

### 2. Window Candidate Selection

* **`FindTargetWindowCandidate`**: Selects a target window for a region. It first tries to find a window intersecting the center point of `selectionRect`. If none is found, it selects the window with the largest intersection area with `selectionRect`.
* **`FindPointTargetWindowCandidate`**: Finds a window at `screenPoint`. If no direct match exists, it checks a $2\times2$ pixel bounding rectangle centered at `(screenPoint.X - 1, screenPoint.Y - 1)`.

### 3. Sampling & Probing Strategies

* **`GetSamplePoints(Rect selectionRect)`**: Generates sample probe points across a region to locate text controls.
  * If width or height is $< 80$ pixels, it samples at the $50\%$ center point (`0.5`).
  * Otherwise, it creates a $3\times3$ grid using $20\%$, $50\%$, and $80\%$ horizontal/vertical ratios.
* **`GetPointProbePoints(Point screenPoint)`**: Generates 5 probing points around `screenPoint`: center, plus 4 cardinal offsets shifted by $\pm 2.0$ pixels (`(X-2, Y)`, `(X+2, Y)`, `(X, Y-2)`, `(X, Y+2)`).

### 4. Text Extraction Hierarchy & Control Patterns

Text is extracted from an `AutomationElement` according to the following priority order:

```
1. PointTextPattern   (TextPattern range derived directly from screen coordinate)
2. ValuePattern       (ValuePattern.Current.Value)
3. TextPattern        (TextPattern.DocumentRange or visible text ranges)
4. NameFallback       (AutomationElement.Current.Name, for supported ControlTypes)
```

#### Selection and Candidate Ranking
* **`IsBetterCandidate`**: Evaluates two candidates (`TextExtractionCandidate` or `OverlayCandidate`). A candidate is better if:
  1. Its `Source` enum value is strictly higher than the current candidate's source.
  2. If sources are equal, its element tree `Depth` is smaller (closer to the targeted leaf node).
* **`IsBetterWindowPointCandidate`**: Compares `WindowPointCandidate` items. Higher source priority wins; if equal, a smaller bounding area (`Area`) is preferred.

#### Control Type Name Fallbacks
`ShouldUseNameFallback` returns `true` only for specific control types:
* `ControlType.Text`
* `ControlType.Hyperlink`
* `ControlType.ListItem`
* `ControlType.DataItem`
* `ControlType.TreeItem`
* `ControlType.MenuItem`
* `ControlType.TabItem`
* `ControlType.HeaderItem`

Name fallback for non-`Text` controls is skipped if the element contains visible text descendants within 2 tree levels (`HasVisibleTextDescendant`).

#### Text-Bearing Control Filtering
`IsTextBearingControlType` identifies whether a control type is known to hold text content:
* `Text`, `Edit`, `Document`, `Button`, `CheckBox`, `RadioButton`, `Hyperlink`, `ListItem`, `DataItem`, `TreeItem`, `MenuItem`, `TabItem`, `HeaderItem`, `ComboBox`, `SplitButton`.

### 5. Tree Traversal (`EnumerateElementsWithDepth`)

Uses a bread-first search (BFS) queue (`Queue<(AutomationElement Element, int Depth)>`):
* Uses `TreeWalker.ControlViewWalker` by default.
* Uses `TreeWalker.RawViewWalker` if `TraversalMode == UiAutomationTraversalMode.Thorough`.
* Prunes enumeration when `depth >= maxDepth`.

### 6. Text Normalization & Deduplication

* **`NormalizeText`**: Splits input text by lines (`\r`, `\n`), trims whitespace from each line, removes empty entries, collapses contiguous spaces/tabs within lines into single spaces, and re-joins lines using `Environment.NewLine`.
* **`TryAddUniqueText`**: Normalizes text and adds it to an `ISet<string>` tracking set. Rejects empty or duplicate strings.
* **`BuildOverlayDedupKey`**: Generates a unique composite key for overlay deduplication using normalized text and rounded bounding box coordinates (`X|Y|Width|Height` rounded to 1 decimal place).
* **`SortOverlayItems`**: Sorts resulting overlay items primarily by top position (`ScreenBounds.Top`), secondarily by left position (`ScreenBounds.Left`), and finally by text content.

---

## Error Handling & Exception Resilience

Native UI Automation calls frequently fail when target elements vanish, become stale, or trigger internal COM/native access violations. `UIAutomationUtilities` guards against these conditions by explicitly catching:

* **`ElementNotAvailableException`**: Thrown when a targeted control disappears from the UI tree mid-operation.
* **`InvalidOperationException`**: Thrown when an automation pattern is unsupported or an element state invalidates property access.
* **`ArgumentException`**: Thrown on invalid handles or system parameters.
* **`AccessViolationException`**: Native UIA crash guard (especially around `TextPattern.GetVisibleRanges()` or `GetText()`); handled by skipping the affected range or returning `string.Empty`.
* **`COMException`**: Handled during range text extraction to safely ignore COM RPC/disconnection faults.