# Technical Documentation: `JoinLinesWindow.xaml.cs`

## Overview

The `JoinLinesWindow.xaml.cs` file defines the code-behind logic for `JoinLinesWindow`, a Fluent WPF window in the `Text-Grab` application. Its primary purpose is to provide an interface that allows users to configure and preview options for joining multiple lines of text within an instance of `EditTextWindow`. 

The window supports real-time, debounced text previews with efficient text sampling and truncation algorithms to maintain UI responsiveness when processing large blocks of text.

---

## Class Signature & Dependencies

```csharp
namespace Text_Grab.Controls;

public partial class JoinLinesWindow : Wpf.Ui.Controls.FluentWindow
```

### Direct Class Dependencies
- **Base Class:** `Wpf.Ui.Controls.FluentWindow`
- **Owner Window:** `Text_Grab.Controls.EditTextWindow`
- **Utility Methods:** Uses text extension methods (e.g., `JoinLines()`) from `Text_Grab.Utilities`.

---

## Constants

The class defines several `private const` fields that govern debouncing and text sampling/truncation limits for the preview system:

| Constant | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `PreviewDebounceDelayMs` | `int` | `250` | Milliseconds to wait before recalculating the text preview after input changes. |
| `PreviewLeadingSegmentCount` | `int` | `3` | Maximum number of leading text segments included during preview sampling. |
| `PreviewTrailingSegmentCount` | `int` | `2` | Maximum number of trailing text segments included during preview sampling. |
| `PreviewLeadingLineCount` | `int` | `8` | Maximum number of leading lines sampled per segment. |
| `PreviewTrailingLineCount` | `int` | `4` | Maximum number of trailing lines sampled per segment. |
| `PreviewMaxCharsPerSegment` | `int` | `180` | Maximum character length allowed per segment in the preview. |
| `PreviewMaxCharsOverall` | `int` | `420` | Maximum overall character length allowed across the entire preview text. |
| `PreviewMaxSourceCharsSingleLine` | `int` | `240` | Maximum length of a single-line segment before truncation is applied during preview sampling. |
| `PreviewOmittedText` | `string` | `"[...]"` | Placeholder text inserted when text or segments are omitted during sampling or truncation. |

---

## Commands & State Fields

### Routed Commands
- `public static RoutedCommand JoinLinesCmd = new();`  
  Static command bound to executing the join operation and closing the window.
- `public static RoutedCommand ApplyCmd = new();`  
  Static command bound to executing the join operation while leaving the window open.

### Private Fields
- `private readonly DispatcherTimer previewDebounceTimer`: Controls the delayed execution of preview updates to prevent UI stuttering during fast user input.
- `private PreviewSegment[] previewSourceSegments`: Stores the current segments extracted from the owner window.
- `private bool previewUsesSampling`: Indicates whether sampling was used when generating preview source segments.

---

## Inner Data Structures

### `PreviewSegment`
```csharp
private readonly record struct PreviewSegment(string Text, bool IsPlaceholder = false);
```
A lightweight, immutable record struct representing a segment of text in the preview system.
- `Text`: The string content of the segment or placeholder text.
- `IsPlaceholder`: Boolean indicating whether the segment represents omitted content (`[...]`).

---

## Functional Architecture

### 1. Initialization and Lifecycle Management

- **`JoinLinesWindow()` (Constructor)**  
  Initializes WPF components, sets the `previewDebounceTimer` interval to 250ms, and attaches the `PreviewDebounceTimer_Tick` event handler.

- **`Window_Loaded(object sender, RoutedEventArgs e)`**  
  - Checks if `Owner` is an `EditTextWindow`.
  - Obtains preview text segments from `etwOwner.GetSelectedOrAllTextSegmentsForPreview()`.
  - Builds initial `PreviewSegment` array via `BuildPreviewSegments`.
  - Calls `UpdatePreview()` to render initial state.
  - Sets keyboard focus to `JoiningTextTextBox` and selects all of its content.

- **`Window_Closed(object? sender, EventArgs e)`**  
  - Stops `previewDebounceTimer`.
  - Clears `previewSourceSegments` and resets `PreviewTextBox`.

- **`Window_KeyUp(object sender, KeyEventArgs e)`**  
  Closes the window when the user presses the `Escape` key.

---

### 2. Command Processing & Line Joining Logic

- **`JoinLines_CanExecute(object sender, CanExecuteRoutedEventArgs e)`**  
  Sets `e.CanExecute = true` only if `Owner` is an instance of `EditTextWindow`.

- **`JoinLines_Executed(object sender, ExecutedRoutedEventArgs e)`**  
  Executes `ApplyJoinLines()` and closes the window.

- **`Apply_Executed(object sender, ExecutedRoutedEventArgs e)`**  
  Executes `ApplyJoinLines()` without closing the window.

- **`ApplyJoinLines()`**  
  Applies line-joining transformations to the owner `EditTextWindow` by passing parameters from UI controls:
  - `JoiningTextTextBox.Text`: Text used to join lines together.
  - `TrimLineBeforeJoiningToggle.IsChecked is true`: Indicates if lines should be trimmed before joining.
  - `TextAtBeginningTextBox.Text`: Prefix string added to the beginning.
  - `TextAtEndTextBox.Text`: Suffix string added to the end.

---

### 3. Preview & Debouncing Pipeline

```
[UI Input Event] -> PreviewInputChanged -> Start/Reset Debounce Timer (250ms)
                                                     |
                                            (Timer Ticks)
                                                     v
PreviewDebounceTimer_Tick -> UpdatePreview -> BuildPreviewText -> TruncateMiddle -> UI Update
```

- **`PreviewInputChanged(object sender, RoutedEventArgs e)`**  
  Handles events when input controls change value. Restarts the 250ms `previewDebounceTimer` if the window is fully loaded.

- **`PreviewDebounceTimer_Tick(object? sender, EventArgs e)`**  
  Stops the timer and triggers `UpdatePreview()`.

- **`UpdatePreview()`**  
  - Generates preview text via `BuildPreviewText`.
  - Updates `PreviewTextBox.Text` if the content has changed.
  - Updates `PreviewHeaderTextBlock.Text` to show `"Preview (sampled)"` if sampling or truncation occurred, otherwise `"Preview"`.

- **`BuildPreviewText(ref bool previewWasTruncated)`**  
  Processes `previewSourceSegments`:
  1. Skips transformation if `previewSegment.IsPlaceholder` is `true`.
  2. Applies `JoinLines()` with current UI control values to normal segments.
  3. Truncates each individual transformed segment to `PreviewMaxCharsPerSegment` using `TruncateMiddle`.
  4. Truncates overall assembled preview text to `PreviewMaxCharsOverall`.

---

### 4. Text Sampling & Truncation Helpers

- **`BuildPreviewSegments(IEnumerable<string> sourceSegments)`**  
  Samples source text segments when segment count exceeds limits:
  - Collects leading segments up to `PreviewLeadingSegmentCount` (3).
  - Collects trailing segments up to `PreviewTrailingSegmentCount` (2) using a queue.
  - If total segments exceed the threshold, inserts a `PreviewSegment` placeholder (`[...]`) between leading and trailing segments and sets `usesSampling = true`.

- **`SampleSegmentText(string sourceText, out bool segmentWasSampled)`**  
  Samples lines within a single text segment:
  - Reads source text line by line using carriage return (`\r`) and newline (`\n`) bounds.
  - Retains up to `PreviewLeadingLineCount` (8) leading lines and `PreviewTrailingLineCount` (4) trailing lines.
  - If lines exceed `PreviewLeadingLineCount + PreviewTrailingLineCount`, joins retained line ranges with an omitted text placeholder (`[...]`) and sets `segmentWasSampled = true`.
  - If segment is single-line, truncates it to `PreviewMaxSourceCharsSingleLine` (240).

- **`AppendLineRanges(StringBuilder builder, string sourceText, IEnumerable<(int Start, int Length)> lineRanges)`**  
  Appends specified line ranges from source text to a `StringBuilder`, separating lines with `Environment.NewLine`.

- **`TruncateMiddle(string text, int maxLength, ref bool wasTruncated)`**  
  Truncates text from the middle if `text.Length` exceeds `maxLength`:
  1. Calculates remaining budget after subtracting placeholder length (`PreviewOmittedText.Length`).
  2. Splits remaining budget evenly between prefix and suffix lengths.
  3. Returns a string formatted as: `[Prefix] + "[...]" + [Suffix]`.
  4. Sets `wasTruncated = true`.