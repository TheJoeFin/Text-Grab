# PreviousGrabWindow.xaml.cs Technical Documentation

## Overview

The `PreviousGrabWindow.xaml.cs` file defines the interaction logic for a WPF window (`PreviousGrabWindow`) in the **Text-Grab** application. This control serves as a visual overlay over a previously selected screen region during or after a text screen-grab operation. 

It handles:
* Displaying visual indicators (success checkmark, loading spinner, or border flash).
* Rendering a frozen background image snapshot of the selected region.
* Presenting an animated choice bar to allow users to cancel, re-grab, toggle output options, or route the grabbed content to a Grab Frame during or after execution.

---

## Enumerations

### `PreviousGrabIndicator`
Defines the visual state rendered inside the overlay window upon initialization or state updates.

| Value | Description |
| :--- | :--- |
| `None` | Displays no icon; only the window border flashes briefly before closing. |
| `Success` | Displays a checkmark icon indicating a successful grab operation before closing after a short delay. |
| `Loading` | Displays a loading spinner until `ShowSuccess()` is called or the window is closed. |

### `GrabChoice`
Defines the user-selected action from the choice bar presented during an active or failed grab.

| Value | Description |
| :--- | :--- |
| `None` | Default value representing no action chosen. |
| `Cancel` | Cancels/abandons the grab operation entirely. |
| `ReGrab` | Cancels the current operation and triggers a return to region selection. |
| `SendToGrabFrame` | Opens a Grab Frame containing the captured region image. |

---

## Constants & Static Fields

* **`flashDuration`** (`TimeSpan`, static readonly): Set to 300 milliseconds. Defines the delay before automatically closing the window when `CloseAfterDelay()` is triggered.
* **`choiceBarSlideDuration`** (`Duration`, static readonly): Set to 250 milliseconds. Controls the animation duration for the choice bar slide and fade-in transitions.
* **`choiceBarSlideDistance`** (`double`, const): Set to `48`. Specifies the Y-axis offset distance in pixels for the choice bar slide-up animation.

---

## Events

* **`event EventHandler<GrabChoice>? ChoiceSelected`**  
  Raised when the user clicks one of the action buttons (`Cancel`, `ReGrab`, or `SendToGrabFrame`). Emits the chosen `GrabChoice`.
  
* **`event EventHandler<bool>? SendToEditTextToggled`**  
  Raised when the user toggles the "send to Edit Text Window" option. Emits a `bool` indicating whether the toggle is checked.

---

## Constructor

```csharp
public PreviousGrabWindow(Rect rect, PreviousGrabIndicator indicator = PreviousGrabIndicator.None, ImageSource? regionBackground = null)
```

### Parameters
* **`rect`** (`Rect`): The target rectangular bounds of the capture area on screen.
* **`indicator`** (`PreviousGrabIndicator`, default: `None`): Initial state icon to display.
* **`regionBackground`** (`ImageSource?`, optional): An optional frozen snapshot image of the selected screen area.

### Logic
1. **Window Sizing & Positioning**: Extends the input `rect` by a 3-pixel border thickness on all sides:
   * `Width = rect.Width + 6`
   * `Height = rect.Height + 6`
   * `Left = rect.Left - 3`
   * `Top = rect.Top - 3`
2. **Background Snapshot**: If `regionBackground` is provided, sets `RegionBackgroundImage.Source` to the image and displays `RegionBackgroundImage`.
3. **Indicator Configuration**:
   * **`PreviousGrabIndicator.Success`**: Displays `SuccessViewbox` and triggers `CloseAfterDelay()`.
   * **`PreviousGrabIndicator.Loading`**: Displays `LoadingViewbox`.
   * **`PreviousGrabIndicator.None` / Default**: Triggers `CloseAfterDelay()`.

---

## Public Methods

### `ShowSuccess()`
```csharp
public void ShowSuccess()
```
Replaces the active UI with a success state and initiates window closure:
1. Calls `HideChoiceBar()`.
2. Hides `LoadingViewbox` (`Visibility.Collapsed`).
3. Shows `SuccessViewbox` (`Visibility.Visible`).
4. Invokes `CloseAfterDelay()`.

### `ShowSuccessAsync()`
```csharp
public Task ShowSuccessAsync()
```
An asynchronous wrapper around `ShowSuccess()`. Returns a `Task` that completes after the window's `Closed` event has executed. This allows callers to await window cleanup and focus release before performing subsequent actions.

### `ShowRunningChoices(bool sendToEditTextChecked)`
```csharp
public void ShowRunningChoices(bool sendToEditTextChecked)
```
Configures and displays the choice bar while a grab operation is actively processing:
1. Shows `LoadingViewbox`.
2. Hides `SendToGrabFrameButton`.
3. Sets `SendToEtwToggleButton.IsChecked` to `sendToEditTextChecked` and makes it visible.
4. Invokes `ShowChoiceBar()`.

### `ShowFailedChoices()`
```csharp
public void ShowFailedChoices()
```
Configures and displays the choice bar when a grab completes without text output or fails:
1. Hides `LoadingViewbox`.
2. Shows `SendToGrabFrameButton`.
3. Hides `SendToEtwToggleButton`.
4. Invokes `ShowChoiceBar()`.

---

## Private Methods & Internal Operations

### `ShowChoiceBar()`
* Sets `ChoiceBar.IsEnabled = true`.
* If `ChoiceBar` is not already visible:
  * Makes `ChoiceBar` visible.
  * Configures a `CubicEase` (`EaseOut`) animation.
  * Animates `ChoiceBarSlide` translation from Y = `48` to `0` over 250 ms (`TranslateTransform.YProperty`).
  * Animates `ChoiceBar` opacity from `0` to `1` over 250 ms (`OpacityProperty`).
* Enables window interactivity: sets `IsHitTestVisible = true` and activates the window (`Activate()`).

### `HideChoiceBar()`
* Collapses `ChoiceBar`.
* Disables input hit-testing on the window (`IsHitTestVisible = false`).

### `RaiseChoice(GrabChoice choice)`
* Disables `ChoiceBar` (`ChoiceBar.IsEnabled = false`) to prevent double-click race conditions.
* Invokes the `ChoiceSelected` event with the specified `GrabChoice`.

### `CloseAfterDelay()`
* Instantiates a `DispatcherTimer` set to trigger after `flashDuration` (300 ms).
* Upon timer tick, stops the timer and calls `Close()` to close the window.

### Event Handler Operations
* **`CancelButton_Click`**: Invokes `RaiseChoice(GrabChoice.Cancel)`.
* **`ReGrabButton_Click`**: Invokes `RaiseChoice(GrabChoice.ReGrab)`.
* **`SendToGrabFrameButton_Click`**: Invokes `RaiseChoice(GrabChoice.SendToGrabFrame)`.
* **`SendToEtwToggleButton_Click`**: Raises `SendToEditTextToggled` with `SendToEtwToggleButton.IsChecked is true`.