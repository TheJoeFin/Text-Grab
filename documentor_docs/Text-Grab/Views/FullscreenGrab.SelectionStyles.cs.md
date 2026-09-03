# Documentation Guide: `Text-Grab/Views/FullscreenGrab.SelectionStyles.cs`

## Overview

The `Text-Grab/Views/FullscreenGrab.SelectionStyles.cs` file is a partial class implementation for `Text_Grab.Views.FullscreenGrab`. It contains the complete visual feedback, interaction handling, region calculation, screen capture, and post-capture workflow management for the four full-screen capture styles in Text-Grab:

1. **Region Selection (`FsgSelectionStyle.Region`)**: Standard rectangular drag-to-select mode.
2. **Window Selection (`FsgSelectionStyle.Window`)**: Automatic window candidate detection and highlighting under the mouse cursor.
3. **Freeform Selection (`FsgSelectionStyle.Freeform`)**: Path-based draw selection using freehand mouse movement.
4. **Adjust After Selection (`FsgSelectionStyle.AdjustAfter`)**: Rectangular drag selection followed by interactive positioning and resize handles before final commit.

---

## Technical Architecture & Responsibilities

### 1. State & Interaction Control
- **`SelectionInteractionMode` Enum**: Tracks active interactions: `None`, `CreatingRectangle`, `CreatingFreeform`, `MovingSelection`, and 8 directional resize modes (`ResizeLeft`, `ResizeTopLeft`, `ResizeBottomRight`, etc.).
- **Interactive State Fields**:
  - `isSelecting`: Indicates active drag or draw interactions.
  - `isAwaitingAdjustAfterCommit`: Flags whether the UI is in the intermediate adjustment state (showing resize handles and an accept button).
  - `clickedPoint` / `adjustmentStartPoint`: Tracks initial mouse anchor positions.
  - `selectionRectBeforeDrag`: Preserves original bounding rectangles during move/resize operations.

### 2. Selection Styles & UI Visuals
- **Dynamic Borders & Overlays**:
  - `selectBorder`: A WPF `Border` representing the primary bounding box or window highlight area.
  - `selectionOutlineBorder`: Secondary outline used to clearly mark overlay cutouts.
  - `freeformSelectionPath`: A WPF `Path` rendering freehand shapes via `freeformSelectionPoints`.
  - `selectionHandleBorders`: Eight resize handles rendered on the corners and edges during `AdjustAfter` mode.
- **Window Detection Badge**:
  - `windowSelectionHighlightContent`, `windowSelectionInfoBadge`, `windowSelectionAppNameText`, `windowSelectionTitleText`: Displays the candidate window's application name and window title when hovering over top-level windows in Window Selection mode.

### 3. Screen Bounds & Coordinate Transforms
- Translates WPF logical points to device pixels using `PresentationSource`, `Matrix.TransformToDevice`, and `Matrix.TransformFromDevice`.
- Coordinates multiple monitor boundaries using `DisplayInfo.ScaledBounds()` and `GetAbsolutePosition()`.

### 4. OCR & Processing Pipeline
- Directs captures to regular OCR (`OcrUtilities.GetTextFromBitmapSourceAsync`), Table OCR, UI Automation text extraction (`OcrUtilities.GetTextFromAbsoluteRectAsync`), or Windows AI Image Descriptions (`WindowsAiUtilities.GetTextDescriptionWithWinAI`).
- Executes post-grab actions via `PostGrabActionManager`.
- Routes results to the active destination (Clipboard, `GrabFrame`, `EditTextWindow`, or direct text insertion).
- Saves captures to history (`HistoryService`) when enabled.

---

## Enumerations & Constants

### `SelectionInteractionMode`
Defines the current state of user interaction with the screen canvas:
- `None`: No active selection interaction.
- `CreatingRectangle`: Dragging to define a standard rectangle.
- `CreatingFreeform`: Drawing a freehand path.
- `MovingSelection`: Dragging the existing selection box to translate its position.
- `ResizeLeft`, `ResizeTop`, `ResizeRight`, `ResizeBottom`: Edge-based resizing modes.
- `ResizeTopLeft`, `ResizeTopRight`, `ResizeBottomLeft`, `ResizeBottomRight`: Corner-based resizing modes.

### Structural Constants & Brushes
- `MinimumSelectionSize` (`6.0`): Minimum width or height threshold in logical pixels for a valid selection.
- `AdjustHandleSize` (`12.0`): Size in logical pixels of each corner/edge resize handle.
- `SelectionBorderBrush`: Solid brush (`RGB: 40, 118, 126`) applied to selection bounds and resize handles.
- `WindowSelectionFillBrush`: Semi-transparent white brush (`RGBA: 52, 255, 255, 255`) applied to window highlights.
- `WindowSelectionLabelBackgroundBrush`: Dark background brush (`RGBA: 224, 20, 27, 46`) for the window name badge.
- `FreeformFillBrush`: Translucent fill brush (`RGBA: 36, 40, 118, 126`) for closed freeform geometry.

---

## Key Methods Breakdown

### 1. Initialization & Style Switching

#### `InitializeSelectionStyles()`
Constructs default property values, corner radii, hit-test visibility settings, and child visual hierarchies for selection borders, outlines, info badges, and text blocks. Connects `windowSelectionTimer.Tick` to `WindowSelectionTimer_Tick`.

#### `ApplySelectionStyle(FsgSelectionStyle selectionStyle, bool persistToSettings = true)`
- Updates `currentSelectionStyle`.
- Synchronizes menu check states (`RegionSelectionMenuItem`, `WindowSelectionMenuItem`, `FreeformSelectionMenuItem`, `AdjustAfterSelectionMenuItem`).
- Updates `SelectionStyleComboBox` via `SyncSelectionStyleComboBox`.
- Optionally saves the preference to `DefaultSettings.FsgSelectionStyle`.
- Resets visual state via `ResetSelectionVisualState()`.
- Configures mouse cursors (`Cursors.Hand` for Window mode, `Cursors.Cross` otherwise).
- Immediately queries window highlights if `Window` style is selected.

#### Helper Evaluation Methods (Static Internal)
- `ShouldKeepTopToolbarVisible(FsgSelectionStyle selectionStyle, bool isAwaitingAdjustAfterCommit)`: Returns `true` if in Window style or waiting to commit an Adjust-After selection.
- `ShouldCommitWindowSelection(WindowSelectionCandidate? pressedWindowCandidate, WindowSelectionCandidate? releasedWindowCandidate)`: Evaluates if mouse-down and mouse-up occurred on the exact same window handle.
- `ShouldUseOverlayCutout(FsgSelectionStyle selectionStyle)`: Returns `true` for `Region` and `AdjustAfter` styles.
- `ShouldDrawSelectionOutline(FsgSelectionStyle selectionStyle)`: Evaluates whether to draw `selectionOutlineBorder` based on cutout settings.

---

### 2. Canvas & Mouse Event Handlers

#### `HandleRegionCanvasMouseDown(MouseButtonEventArgs e)`
Directs the initial click based on `CurrentSelectionStyle`:
- **`Window`**: Finds and sets `clickedWindowCandidate`, captures mouse input.
- **`Freeform`**: Invokes `BeginFreeformSelection(e)`.
- **`AdjustAfter`**: Checks if the user clicked an existing resize/move handle via `TryBeginAdjustAfterInteraction(e)`. If not, begins drawing a new rectangle via `BeginRectangleSelection(e)`.
- **`Region`**: Invokes `BeginRectangleSelection(e)`.

#### `HandleRegionCanvasMouseMove(MouseEventArgs e)`
Processes mouse tracking based on `selectionInteractionMode`:
- `CreatingRectangle`: Calls `UpdateRectangleSelection(movingPoint)`.
- `CreatingFreeform`: Calls `UpdateFreeformSelection(movingPoint)`.
- `None`: Calls `UpdateAdjustAfterCursor(movingPoint)` if in `AdjustAfter` mode.
- *Move / Resize Modes*: Calls `UpdateAdjustedSelection(movingPoint)`.

#### `HandleRegionCanvasMouseUpAsync(MouseButtonEventArgs e)`
Finalizes active mouse interactions:
- `CreatingRectangle`: Executes `FinalizeRectangleSelectionAsync()`.
- `CreatingFreeform`: Executes `FinalizeFreeformSelectionAsync()`.
- *Move / Resize Modes*: Executes `EndSelectionInteraction()`, updates handles and cursor.
- `Window`: Validates pressed vs. released candidate window handles. If matching, constructs a result via `CreateWindowSelectionResult` and invokes `CommitSelectionAsync`.

---

### 3. Selection Drawing & Modification Logic

#### `BeginRectangleSelection(MouseEventArgs e)`
Resets visuals, stores `clickedPoint`, calculates DPI scale, updates state to `SelectionInteractionMode.CreatingRectangle`, sets `isSelecting = true`, hides top buttons, captures mouse input, clips mouse cursor to the window (`CursorClipper.ClipCursor`), and identifies the active display screen via `SetCurrentScreenFromMouse()`.

#### `UpdateRectangleSelection(Point movingPoint)`
- If `Keyboard.Modifiers == ModifierKeys.Shift`, delegates to `PanSelection(movingPoint)`.
- Otherwise, calculates `left`, `top`, `width`, and `height` between `clickedPoint` and `movingPoint`, then updates the selection bounding rect via `ApplySelectionRect`.

#### `BeginFreeformSelection(MouseEventArgs e)`
Initializes `selectionInteractionMode = SelectionInteractionMode.CreatingFreeform`, hides top buttons, captures and clips mouse, adds initial point to `freeformSelectionPoints`, and builds geometry via `FreeformCaptureUtilities.BuildGeometry`.

#### `UpdateFreeformSelection(Point movingPoint)`
Appends `movingPoint` to `freeformSelectionPoints` if it has moved at least 2 units from the last point, rebuilding the geometry data on `freeformSelectionPath`.

#### `UpdateAdjustedSelection(Point movingPoint)`
Handles dragging and resizing an established rectangle in `AdjustAfter` mode:
- Clamps bounds within the active canvas surface.
- Handles translation (`MovingSelection`) by moving `left` and `top` relative to `adjustmentStartPoint`.
- Handles edge and corner resizing by clamping boundary changes so the resulting width and height remain above `MinimumSelectionSize`.
- Applies the calculated rect and updates visual resize handle positions via `UpdateSelectionHandles()`.

---

### 4. Adjust-After Mode & Handles

#### `EnterAdjustAfterMode()`
Enables `isAwaitingAdjustAfterCommit = true`, sets `selectionInteractionMode = SelectionInteractionMode.None`, displays `AcceptSelectionButton` and `TopButtonsStackPanel`, generates resize handle borders on screen, and updates the hover cursor.

#### `UpdateSelectionHandles()`
Clears existing handle borders and, if `isAwaitingAdjustAfterCommit` is active, creates 8 `Border` elements positioned at the corners and midpoints of `GetCurrentSelectionRect()`.

#### `GetSelectionInteractionModeForPoint(Point point)`
Determines if a given point lies within any of the 8 resize handle rects (via `GetHandleRect`), inside the selection bounding box (returning `MovingSelection`), or outside (returning `None`).

#### `GetCursorForInteractionMode(SelectionInteractionMode mode)`
Maps selection interaction modes to standard WPF cursors:
- `MovingSelection`: `Cursors.SizeAll`
- `ResizeLeft` / `ResizeRight`: `Cursors.SizeWE`
- `ResizeTop` / `ResizeBottom`: `Cursors.SizeNS`
- `ResizeTopLeft` / `ResizeBottomRight`: `Cursors.SizeNWSE`
- `ResizeTopRight` / `ResizeBottomLeft`: `Cursors.SizeNESW`
- Default: `Cursors.Cross`

---

### 5. Window Candidate Detection & Overlay Mechanics

#### `WindowSelectionTimer_Tick(object? sender, EventArgs e)`
Fires every 100ms when `CurrentSelectionStyle == FsgSelectionStyle.Window`. Updates window candidate detection under the mouse unless an interaction is actively in progress.

#### `UpdateWindowSelectionHighlight()`
Retrieves candidate windows via `GetWindowSelectionCandidateAtCurrentMousePosition()` and passes the candidate to `ApplyWindowSelectionHighlight()`.

#### `ApplyWindowSelectionHighlight(WindowSelectionCandidate? candidate)`
- Intersects candidate window device bounds with current window device bounds (`GetWindowDeviceBounds()`).
- Converts intersection bounds to logical local coordinates via `ConvertAbsoluteDeviceRectToLocal()`.
- Draws `selectBorder` using `WindowSelectionFillBrush`.
- Updates `windowSelectionInfoBadge` text with the application name (`candidate.DisplayAppName`) and window title (`candidate.DisplayTitle`).

#### `ComposeCapturedImageFromFullscreenBackgrounds(Rect absoluteCaptureRect)`
Constructs a single `BitmapSource` image when capturing a window candidate that spans one or multiple screens:
1. Iterates over all open `FullscreenGrab` windows in `Application.Current.Windows`.
2. Intersects each window's device bounds with `absoluteCaptureRect`.
3. Crops the corresponding portion from each `FullscreenGrab` instance's `BackgroundImage`.
4. Draws cropped segments onto a unified `DrawingVisual`.
5. Renders the output onto a `RenderTargetBitmap` (Format: `Pbgra32`, 96 DPI).

---

### 6. Image Generation & Selection Finalization

#### `CreateRectangleSelectionResult(FsgSelectionStyle selectionStyle)`
Converts the logical `selectBorder` bounds into device-scaled screen coordinates and returns a `FullscreenCaptureResult` with the device-coordinate `Rect`.

#### `CreateFreeformSelectionResult()`
1. Converts all `freeformSelectionPoints` to device points.
2. Extracts device bounds using `FreeformCaptureUtilities.GetBounds`.
3. Captures raw screen contents using `ImageMethods.GetRegionOfScreenAsBitmap`.
4. Masks the bitmap outside the freeform polygon using `FreeformCaptureUtilities.CreateMaskedBitmap`.
5. Caches the result in `Singleton<HistoryService>.Instance` and returns a `FullscreenCaptureResult` containing the `BitmapSource`.

#### `CommitSelectionAsync(FullscreenCaptureResult selection, bool isSmallClick)`
Handles the main commitment pipeline:
- **`GrabFrame` Check**: If `NewGrabFrameMenuItem.IsChecked` is true, routes execution to `PlaceGrabFrameInSelectionRectAsync` and exits.
- **AI Description Processing**: If the language is `WindowsAiDescriptionLang`, executes `CommitAiDescriptionAsync`.
- **Loading Overlay (`PreviousGrabWindow`)**: Shows a loading indicator over the selection region during processing unless using UI Automation (`UiAutomationLang`). Pre-captures the region bitmap to avoid including the indicator in OCR/history.
- Calls `CommitSelectionCoreAsync` to perform OCR or text parsing.
- Displays a success flash on `PreviousGrabWindow` if text was found; otherwise, closes the indicator.

#### `CommitAiDescriptionAsync(FullscreenCaptureResult selection, bool isSmallClick, ILanguage selectedOcrLang)`
1. Launches an asynchronous AI image description task via `WindowsAiUtilities.GetTextDescriptionWithWinAI`.
2. Displays a `PreviousGrabWindow` preview overlay with a spinner and hides main full-screen grab windows (`SetFullscreenGrabsVisible(false)`).
3. If processing exceeds 2 seconds, surfaces running action choices (**Cancel** / **Re-grab**) and optionally speaks status via TTS if `SpeakProcessingStatus` is enabled.
4. Handles completion or cancellation. If empty/failed, surfaces post-failure choices (**Cancel**, **Re-grab**, **Send to Grab Frame**).

#### `CommitSelectionCoreAsync(FullscreenCaptureResult selection, bool isSmallClick)`
Executes text extraction according to the active configuration:
- **Single-click / Small click**: Calls `OcrUtilities.GetClickedWordAsync` to extract the word at the clicked position.
- **UI Automation Language (`UiAutomationLang`)**: Calls `OcrUtilities.GetTextFromAbsoluteRectAsync`.
- **Table Mode (`isTable`)**: Calls `OcrUtilities.GetTextFromBitmapSourceAsTableAsync` or `OcrUtilities.GetTextFromBitmapAsTableAsync`.
- **Standard Image Capture**: Calls `OcrUtilities.GetTextFromBitmapSourceAsync` or `OcrUtilities.GetTextFromAbsoluteRectAsync`.
- Directs result text to `TextFromOCR` and invokes `FinishCommitWithTextAsync`.

#### `FinishCommitWithTextAsync(FullscreenCaptureResult selection, bool isSmallClick, bool isSingleLine, bool isTable, ILanguage selectedOcrLang)`
1. **History Persistence**: Creates a `HistoryInfo` object with language metadata, DPI scale, timestamps, position rect, and bitmap content if history settings are enabled.
2. **Empty Text Handling**: If `TextFromOCR` is empty/whitespace, resets the screen via `ResetForNewSelection(selection)` and returns `false`.
3. **Post-Grab Actions**: Evaluates checked items in `NextStepDropDownButton.Flyout` context menu. Runs actions via `PostGrabActionManager.ExecutePostGrabAction`. Warns if template actions are attempted on freeform captures.
4. **Output Routing**:
   - Opens or gets target controls (`EditTextWindow`, destination text box).
   - Passes text to `OutputUtilities.HandleTextFromOcr`.
   - Closes all full-screen grab windows (`WindowUtilities.CloseAllFullscreenGrabs()`).
   - Handles background auto-insert (`WindowUtilities.TryInsertString`) if `shouldInsert` is requested.

#### `PlaceGrabFrameInSelectionRectAsync(FullscreenCaptureResult selection)`
Instantiates and displays a new `GrabFrame` window bounded to the capture region:
- Retrieves image source via `GetBitmapSourceForGrabFrame`.
- Obtains UI Automation snapshot if language is `UiAutomationLang`.
- Sets destination controls, table toggle settings, position, and dimensions.
- Displays `GrabFrame`, disposes background bitmaps, and closes all `FullscreenGrab` instances.