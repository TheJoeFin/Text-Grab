# Developer Documentation: `ClipboardUtilities.cs`

## Overview

The `ClipboardUtilities` class in the `Text_Grab.Utilities` namespace provides helper methods for interacting with the system clipboard. It handles text retrieval via Windows DataTransfer APIs, image extraction (including native WPF clipboard bitmap reading and embedded Base64 URI strings), and parsing HTML table structures copied to the clipboard into tab-separated values (TSV).

---

## Class Architecture & Dependencies

* **Namespace:** `Text_Grab.Utilities`
* **Access Level:** `public class`
* **Key Dependencies:**
  * `System.Windows` / `System.Windows.Media.Imaging`: Used for WPF clipboard access and image processing (`BitmapImage`, `ImageSource`).
  * `Windows.ApplicationModel.DataTransfer`: Used for asynchronous modern Windows DataPackage clipboard operations (`DataPackageView`, `StandardDataFormats`).
  * `System.Text.RegularExpressions`: Used for stripping HTML tags during cell content cleaning.
  * `System.Net.WebUtility`: Used for decoding HTML entities.

---

## Constants

| Constant | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `MaxHtmlTableSpan` | `int` | `16_384` | Maximum allowable limit for `colspan` and `rowspan` attribute values during HTML table parsing to prevent memory or performance issues. |

---

## Public Methods

### `TryGetClipboardText()`
```csharp
public static async Task<(bool, string)> TryGetClipboardText()
```
* **Purpose:** Asynchronously attempts to retrieve plain text from the modern Windows DataTransfer clipboard.
* **Returns:** A tuple containing:
  * `bool`: `true` if plain text was successfully retrieved; otherwise `false`.
  * `string`: The extracted clipboard text if successful, or an error message/empty string if unsuccessful.
* **Logic:**
  1. Calls `Windows.ApplicationModel.DataTransfer.Clipboard.GetContent()` to obtain a `DataPackageView`.
  2. Checks if the data package contains `StandardDataFormats.Text`.
  3. Awaits `dataPackageView.GetTextAsync()` to fetch the text content.
  4. Catches exceptions and returns `(false, errorMessage)` on failure.

---

### `TryGetImageFromClipboard()`
```csharp
public static (bool, ImageSource?) TryGetImageFromClipboard()
```
* **Purpose:** Attempts to retrieve an image from the clipboard, supporting both standard WPF bitmap formats and Base64-encoded image strings.
* **Returns:** A tuple containing:
  * `bool`: `true` if an image was successfully extracted; `false` otherwise.
  * `ImageSource?`: The extracted `ImageSource` instance, or `null` if unsuccessful.
* **Logic:**
  1. Checks whether the clipboard contains a Base64-encoded image string via `ClipboardContainsBase64Image()`.
  2. If a Base64 image is found, calls `GetBase64ClipboardContentAsImageSource()`.
  3. If no Base64 image is present, inspects `System.Windows.Clipboard.GetDataObject()` for `DataFormats.Bitmap` and retrieves the bitmap using `System.Windows.Clipboard.GetImage()`.
  4. Returns `(true, imageSource)` if a valid `ImageSource` is retrieved, or `(false, null)` if retrieval fails.

---

### `TryGetHtmlTableAsTabSeparated()`
```csharp
public static bool TryGetHtmlTableAsTabSeparated(out string tabSeparated)
```
* **Purpose:** Attempts to extract HTML format data from the WPF clipboard and convert an embedded HTML table into tab-separated text.
* **Parameters:**
  * `tabSeparated` (`out string`): Receives the resulting tab-separated string on success, or `string.Empty` on failure.
* **Returns:** `true` if an HTML table was parsed and converted; `false` otherwise.
* **Logic:**
  1. Checks if `System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Html)` is true.
  2. Retrieves the raw HTML data string from the clipboard.
  3. Invokes `ConvertHtmlToTabSeparated(...)` to process the HTML.
  4. Returns `true` if non-empty TSV content is generated.

---

## Internal Methods

### `ConvertHtmlToTabSeparated()`
```csharp
internal static string ConvertHtmlToTabSeparated(string cfHtml)
```
* **Purpose:** Converts raw clipboard HTML format string (`CF_HTML`) to a tab-delimited table representation.
* **Parameters:**
  * `cfHtml` (`string`): The raw HTML string retrieved from the clipboard.
* **Returns:** A multi-line string where cells in a row are separated by `\t` and rows are separated by `\n`.
* **Logic:**
  1. Calls `ExtractHtmlFragment(cfHtml)` to strip clipboard header metadata.
  2. Calls `ParseHtmlTableToGrid(fragment)` to transform HTML tags into a 2D string grid (`List<List<string>>`).
  3. Joins row elements with `\t` and combines rows with `\n`.

---

## Private Helper Methods

### Image Processing Helpers

#### `GetBase64ClipboardContentAsImageSource()`
```csharp
private static ImageSource? GetBase64ClipboardContentAsImageSource()
```
* Reads text from `System.Windows.Clipboard.GetText()`.
* Strips Microsoft Teams wrapper tags via `CleanTeamsBase64Image()`.
* Extracts the Base64 data substring after the comma `,`.
* Converts the Base64 string to a byte array and loads it into a `MemoryStream`.
* Constructs a `BitmapImage`, assigns the stream source, sets `BitmapCacheOption.None`, calls `Freeze()`, and returns it.
* *Note:* The `MemoryStream` is intentionally left undisposed to prevent empty rendering errors in WPF views.

#### `ClipboardContainsBase64Image()`
```csharp
private static bool ClipboardContainsBase64Image()
```
* Reads and trims clipboard text.
* Cleans Microsoft Teams formatting using `CleanTeamsBase64Image()`.
* Inspects data prefixes via `base64ImageExtension()`. Returns `true` if a recognized image MIME prefix is present; otherwise `false`.

#### `CleanTeamsBase64Image()`
```csharp
private static string CleanTeamsBase64Image(string dirtyTeamsString)
```
* Checks if `dirtyTeamsString` starts with `<img src="`.
* If matched, strips the prefix `<img src="` and suffix `" alt="image" iscopyblocked="false">` generated by Microsoft Teams.

#### `base64ImageExtension()`
```csharp
private static string base64ImageExtension(ref string base64String)
```
* Detects standard Data URI scheme prefixes:
  * `data:image/png;base64,` (`.png`)
  * `data:image/jpeg;base64,` (`.jpeg`)
  * `data:image/bmp;base64,` (`.bmp`)
  * `data:image/gif;base64,` (`.gif`)
  * `data:image/x-icon;base64,` (`.ico`)
  * `data:image/svg+xml;base64,` (`.svg`)
  * `data:image/webp;base64,` (`.webp`)
* Returns the file extension string or `string.Empty` if not matched.

---

### HTML Parsing Helpers

#### `ExtractHtmlFragment()`
```csharp
private static string ExtractHtmlFragment(string cfHtml)
```
* Extracts the core HTML content from Windows Clipboard format (`CF_HTML`).
* First attempts to locate HTML comment markers `<!--StartFragment-->` and `<!--EndFragment-->` (or variants with trailing spaces).
* Fallback: Parses `StartFragment:` and `EndFragment:` byte-offset numeric headers in the `CF_HTML` string metadata.

#### `ParseHtmlTableToGrid()`
```csharp
private static List<List<string>> ParseHtmlTableToGrid(string html)
```
* Locates `<table` and `</table>` boundaries within the HTML string.
* Processes each `<tr>...</tr>` row section sequentially.
* Uses a `Dictionary<int, (int RemainingRows, string Content)>` (`rowspanMap`) to track and carry over cells spanning multiple rows (`rowspan`).
* Integrates horizontal spans (`colspan`) by calling `FindNextFreeColumnRange()`.
* Constructs a normalized 2D grid (`List<List<string>>`) ensuring missing or spanned cells are populated properly per column.

#### `FindNextFreeColumnRange()`
```csharp
private static int FindNextFreeColumnRange(IReadOnlyDictionary<int, string> rowData, int startColumn, int columnCount)
```
* Iterates from `startColumn` to locate the first column index in `rowData` that has `columnCount` contiguous unoccupied slots available.

#### `ParseHtmlRowCells()`
```csharp
private static List<(string Text, int ColSpan, int RowSpan)> ParseHtmlRowCells(string rowHtml)
```
* Parses all `<td>` and `<th>` elements within a row block string.
* Extracts `colspan` and `rowspan` attributes via `ParseSpanAttribute()`.
* Cleans cell inner content using `CleanHtmlCellContent()`.
* Returns a list of cell metadata tuples containing `(Text, ColSpan, RowSpan)`.

#### `ParseSpanAttribute()`
```csharp
private static int ParseSpanAttribute(string tagAttributes, string attributeName)
```
* Parses integer attribute values for `"colspan"` or `"rowspan"`.
* Limits returning span values to `Math.Min(span, MaxHtmlTableSpan)` (max 16,384). Returns `1` if omitted or invalid.

#### `CleanHtmlCellContent()`
```csharp
private static string CleanHtmlCellContent(string html)
```
* Replaces `<br>` and `<br/>` tags with spaces (`" "`).
* Strips all remaining HTML tags using the regex `<[^>]*>`.
* Decodes HTML entities using `WebUtility.HtmlDecode()`.
* Returns trimmed plain text.