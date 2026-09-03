# Technical Documentation: `Text-Grab/Views/FullscreenGrab.xaml.cs`

## Overview

The `FullscreenGrab.xaml.cs` file implements the code-behind logic for the `FullscreenGrab` window in **Text-Grab**. The `FullscreenGrab` class is a WPF `Window` responsible for providing a full-screen overlay that captures screen regions, handles image freezing, processes Optical Character Recognition (OCR), renders template region overlays, supports canvas panning and zooming, and executes configurable post-grab actions.

---

## Class Architecture & Properties

### Namespace & Base Class
- **Namespace:** `Text_Grab.Views`
- **Class:** `public partial class FullscreenGrab : Window`

### Public Properties
- `DestinationTextBox` (`TextBox?`): Gets or sets a target `TextBox` control for sending OCR results. Setting this updates the `SendToEditTextToggleButton.IsChecked` property.
- `IsFreeze` (`bool`): Flag indicating whether the screen capture is currently frozen (default: `false`).
- `TextFromOCR` (`string?`): Stores text extracted via OCR operations.
- `PreselectedTemplateId` (`string?`): ID of a pre-selected template to use for capture operations.

### Fields & Constants
- **Zoom & Edge Panning Constants:**
  - `MaxZoomScale` (`16.0`): Maximum allowed zoom factor for background images.
  - `EdgePanThresholdPercent` (`0.10`): Fractional screen threshold (10%) from the edge that triggers auto-panning while zoomed in.
  - `EdgePanSpeed` (`8.0`): Speed of auto-panning when the cursor approaches window edges.
- **Window Maximization Guard Constants:**
  - `MinimumMaximizedDimension` (`200.0`): Minimum dimension threshold used to ensure the window is properly maximized.
- **Menu Item Tags:**
  - `EditPostGrabActionsTag` (`"EditPostGrabActions"`): Identifier tag for editing post-grab actions.
  - `ClosePostGrabMenuTag` (`"ClosePostGrabMenu"`): Identifier tag for closing the post-grab actions menu.
- **Timers:**
  - `edgePanTimer` (`DispatcherTimer`): Ticks every 16ms (~60 FPS) to handle edge panning when zoomed.
  - `maximizeGuardTimer` (`DispatcherTimer`): Ticks every 1 second to verify and enforce full-screen maximization upon launch.
- **State Flags & UI Elements:**
  - `usingTesseract` (`bool`): Determines whether Tesseract OCR is available and enabled.
  - `templateOverlayCanvas` (`Canvas`): Overlay canvas used to render active template regions inside the selection area.
  - `_isCleanedUp` (`bool`): Guard flag to prevent redundant or duplicate cleanup calls upon window close or unload.

---

## Key Functional Modules

### 1. Initialization, Lifecycle & Maximization Guards

- **Constructor (`FullscreenGrab()`):**
  - Sets the application theme via `App.SetTheme()`.
  - Determines if Tesseract OCR should be used (`usingTesseract`).
  - Initializes selection styles and timers (`edgePanTimer`, `maximizeGuardTimer`).
  - Subscribes to window events (`LayoutUpdated`, `Closed`).
- **Maximization Guards (`EnsureWindowMaximized`, `MaximizeGuardTimer_Tick`, `ShouldForceMaximize`):**
  - Fullscreen overlays can occasionally drop maximization state upon startup.
  - `ShouldForceMaximize()` checks if `WindowState != WindowState.Maximized` or if dimensions are less than `MinimumMaximizedDimension`.
  - `MaximizeGuardTimer_Tick()` checks this state every second upon load and stops once the window achieves true full-screen status.
  - `FullscreenGrab_LayoutUpdated()` provides continuous layout monitoring to maintain full-screen bounds.
- **Resource Cleanup (`CleanupFullscreenGrab()`):**
  - Unsubscribes from all events (UI clicks, key events, mouse movements, layout updates).
  - Stops timers (`edgePanTimer`, `maximizeGuardTimer`, `windowSelectionTimer`).
  - Disposes bitmap sources (`DisposeBitmapSource`) to clear WPF memory usage.
  - Clears context menus, canvas children, and references to UI controls and images.

---

### 2. Panning, Zooming, and Selection Processing

- **Canvas Zooming (`RegionClickCanvas_PreviewMouseWheel`):**
  - Handles mouse wheel scrolling on the canvas.
  - Creates or updates a `TransformGroup` containing `ScaleTransform` and `TranslateTransform` on `BackgroundImage`.
  - Zoom range is constrained between `1.2` and `MaxZoomScale` (`16.0`).
  - Automatically starts or stops `edgePanTimer` based on zoom state.
- **Edge Panning (`EdgePanTimer_Tick`, `PanBackgroundImage`):**
  - Triggers when zoomed in (`ScaleX > 1.0`).
  - Calculates mouse distance relative to the window boundary. If within `EdgePanThresholdPercent` (10%), it pans the background image.
  - `PanBackgroundImage()` clamps `TranslateTransform` offsets to ensure image boundaries never drift inside the visible window area.
- **Selection Panning (`PanSelection`):**
  - Adjusts selection boundaries when moving the selected rectangle using shift controls.
  - Clamps selection boundaries within screen bounds using `Math.Clamp`.
- **Cropping & Selection Geometry (`TryGetBitmapCropRectForSelection`):**
  - Static utility that transforms display selection coordinates into bitmap pixel coordinates, accounting for device transforms and scale/translation matrices.
  - Returns an `Int32Rect` defining the cropped region of the source image.
- **Grab Frame Creation (`PlaceGrabFrameInSelectionRect`):**
  - Crops the background bitmap based on the selected region.
  - Instantiates a new `GrabFrame` window containing the cropped image and positions it accurately on screen based on DPI metrics.

---

### 3. Background Capture and Freeze Management

- **`SetImageToBackground()`:**
  - Clears existing background resources via `DisposeBitmapSource`.
  - Captures screen bounds into `BackgroundImage.Source` via `ImageMethods.GetWindowBoundsImage(this)`.
  - Sets `BackgroundBrush.Opacity` based on settings (`FsgShadeOverlay`).
- **`FreezeUnfreeze(bool Activate)`:**
  - Toggles the frozen capture state.
  - When frozen: Temporarily hides top control panels, resets background opacity, captures the desktop image, and restores controls if the mouse is over the window.
  - When unfrozen: Disposes of the frozen background bitmap.

---

### 4. Template Region Overlays

- **`GetActiveTemplate()`:**
  - Scans active post-grab menu items to locate an enabled template (`action.ClickEvent == "ApplyTemplate_Click"`).
- **`UpdateTemplateRegionOverlays(double selLeft, double selTop, double selWidth, double selHeight)`:**
  - Rendered when a selection area is drawn and a template is active.
  - Maps `RatioLeft`, `RatioTop`, `RatioWidth`, and `RatioHeight` from `TemplateRegion` to current selection rectangle dimensions.
  - Draws `Border` elements on `templateOverlayCanvas` using distinct highlight colors (e.g., active vs. unreferenced template regions).

---

### 5. Dynamic Post-Grab Actions

- **`LoadDynamicPostGrabActions()`:**
  - Clears and builds the dynamic `ContextMenu` attached to `NextStepDropDownButton`.
  - Populates menu items for enabled post-grab actions and template shortcuts.
  - Appends shortcuts (`Ctrl+1` through `Ctrl+9`).
  - Adds menu options for customizing actions (`EditPostGrabActionsTag`) and closing the menu (`ClosePostGrabMenuTag`).
- **State Persistence & Synchronization (`ApplyPostGrabActionSnapshot`, `BuildPostGrabActionSnapshot`, `SynchronizePostGrabActionSelection`):**
  - Syncs post-grab action check states across fullscreen window instances using `WindowUtilities.SyncFullscreenPostGrabActionStates`.
  - Ensures only one template action is checked at a time when building snapshots.
  - Saves states to persistent settings when configured as `DefaultCheckState.LastUsed`.
- **Visual Status (`CheckIfAnyPostActionsSelected`, `RefreshPostGrabActionVisuals`):**
  - Updates `NextStepDropDownButton` background and foreground colors depending on whether any post-capture action is toggled on.

---

### 6. OCR Language Handling

- **`LoadOcrLanguages(ComboBox languagesComboBox, bool usingTesseract)`:**
  - Asynchronously loads available languages from `CaptureLanguageUtilities.GetCaptureLanguagesAsync()`.
  - Selects the initial preferred language based on system settings or last used language.
- **`LanguagesComboBox_SelectionChanged(...)`:**
  - Persists selected language using `CaptureLanguageUtilities.PersistSelectedLanguage()`.
  - Evaluates table output support via `ApplySelectedLanguageState()` and adjusts UI visibility of table controls accordingly.
- **Cache Invalidation (`LanguagesComboBox_PreviewMouseDown`):**
  - Clears cached OCR languages when middle-clicked.

---

### 7. Keyboard Shortcuts and Controls

The `KeyPressed` method handles keyboard hotkeys:

| Key | Function |
| :--- | :--- |
| **G** | Toggles Grab Frame mode (`NewGrabFrameToggleButton`). |
| **S** | Toggles Single Line mode (`SingleLineToggleButton`). Updates persistent setting. |
| **E** | Toggles Send to Edit Text Window (`SendToEditTextToggleButton`). Updates persistent setting. |
| **F** | Toggles Freeze screen state (`FreezeMenuItem`). |
| **N** | Toggles Standard/Normal mode (`StandardModeToggleButton`). Updates persistent setting. |
| **T** | Toggles Table mode (`TableToggleButton`), if table support is visible. |
| **R** | Applies **Region** selection style. |
| **W** | Applies **Window** selection style. |
| **D** | Applies **Freeform** selection style. |
| **A** | Applies **Adjust After** selection style. |
| **1–9** | Switches active OCR language index (when Ctrl is not held down). |
| **Ctrl + 1–9**| Triggers matching Post-Grab Action shortcut index. |

---

## Static Utility Methods

The class exposes several internal/static utility functions:

1. `GetPostGrabActionKey(ButtonInfo action)`: Generates a unique key string for post-grab actions based on `TemplateId`, `ClickEvent`, or `ButtonText`.
2. `GetActionablePostGrabMenuItems(ContextMenu contextMenu)`: Retrieves all `MenuItem` objects from a context menu where `Tag is ButtonInfo`.
3. `BuildPostGrabActionSnapshot(...)`: Creates a dictionary map of action keys to boolean selection states while ensuring single-template selection rules.
4. `ShouldPersistLastUsedState(...)`: Determines if a post-grab action state needs to be saved based on state transitions and default settings.
5. `TryGetBitmapCropRectForSelection(...)`: Calculates pixel-accurate crop coordinates for a given screen selection box against background render transforms.
6. `GetFullscreenClipBounds(Size renderedSize)`: Returns a bounding `Rect` for clipping geometry based on size.
7. `ShouldForceMaximize(WindowState state, double width, double height)`: Evaluates window dimensions and state to determine if full-screen enforcement is required.