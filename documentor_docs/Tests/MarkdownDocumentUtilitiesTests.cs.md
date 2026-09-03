# Technical Documentation: `MarkdownDocumentUtilitiesTests.cs`

## Overview

The `MarkdownDocumentUtilitiesTests` class is a unit test suite written in C# for validating the functional behavior of the `MarkdownDocumentUtilities` class (located in the `Text_Grab.Utilities` namespace).

The primary purpose of this test file is to ensure that converting Markdown strings to WPF `FlowDocument` instances and serializing `FlowDocument` instances back into Markdown text functions reliably. It also validates detection rules used for live formatting triggers and copy/paste detection.

---

## File Details

- **File Path:** `Tests/MarkdownDocumentUtilitiesTests.cs`
- **Namespace:** `Tests`
- **Dependencies:**
  - `System.Windows.Documents` (WPF document models like `FlowDocument`, `Paragraph`, `Run`, `List`)
  - `System.Windows.Media` (Font formatting models like `FontFamily`)
  - `Text_Grab.Utilities` (Contains `MarkdownDocumentUtilities`, the class under test)

---

## Tested Utilities Overview

The test suite validates five primary methods on `MarkdownDocumentUtilities`:

1. `CreateFlowDocument(string markdown, FontFamily fontFamily, double fontSize)`
2. `SerializeToMarkdown(FlowDocument document, bool preserveLiteralMarkdown = false)`
3. `ShouldPromoteLiveBlock(string marker)`
4. `ShouldPromoteLiveMarkdown(string text)`
5. `LooksLikeMarkdown(string text)`

---

## Detailed Test Suite Specification

### 1. Markdown Round-Trip Testing

These tests verify that parsing Markdown into a WPF `FlowDocument` and serializing it back back into Markdown preserves structural elements and formatting content.

| Test Method Name | Test Type | Purpose & Verification Logic |
| :--- | :--- | :--- |
| `Markdown_RoundTrips_CommonFormatting` | `[WpfFact]` | Ensures headers (`#`), bold text (`**bold**`), inline links (`[link](url)`), bullet lists (`-`), blockquotes (`>`), and code blocks (```csharp) survive serialization intact. |
| `Markdown_Tables_RoundTrip_ToPipeTable` | `[WpfFact]` | Verifies pipe table formatting (`\| Name \| Value \|`) is rendered into a `FlowDocument` and serialized back into Markdown pipe tables. |
| `Markdown_TaskLists_RoundTrip_ToCheckboxMarkers` | `[WpfFact]` | Validates task list items containing unchecked (`- [ ]`) and checked (`- [x]`) checkboxes during full round-trip conversion. |
| `Markdown_OrderedList_RoundTripsStartNumber` | `[WpfFact]` | Validates that non-standard starting indexes in ordered lists (e.g., starting at index `5`) maintain the `List.StartIndex` property in WPF and serialize correctly formatted text output (`5. fifth\r\n6. sixth`). |

---

### 2. Serialization & Character Escaping Tests

These tests evaluate how plain text and literal Markdown characters are handled during document serialization.

| Test Method Name | Test Type | Purpose & Verification Logic |
| :--- | :--- | :--- |
| `PlainText_WithMarkdownCharacters_IsEscapedDuringSerialization` | `[WpfFact]` | Asserts that plain text runs containing special characters (`*`, `[`, `]`) are automatically escaped during normal serialization (e.g., `\*literal\* \[value\]`). |
| `PreserveLiteralMarkdown_KeepsTypedMarkdownSyntax` | `[WpfFact]` | Verifies that passing `preserveLiteralMarkdown: true` to `SerializeToMarkdown` prevents character escaping, keeping typed syntax like `**bold** [link](url)` intact. |

---

### 3. Live Block Promotion Trigger Tests

These tests evaluate whether user input at the start of a block line triggers live block promotion.

| Test Method Name | Test Type | Input Data Examples | Expected Result |
| :--- | :--- | :--- | :--- |
| `LiveBlockTriggerMarkers_AreRecognized` | `[Theory]` | `"#"`<br>`"##"`<br>`">"`<br>`"  >"`<br>`"-"`<br>`"1."` | `ShouldPromoteLiveBlock` returns `true` |
| `NonTriggerText_DoesNotPromoteLiveBlock` | `[Theory]` | `"text"`<br>`"hello # world"`<br>`"1.2"` | `ShouldPromoteLiveBlock` returns `false` |

---

### 4. Live Parsing Promotion Tests

These tests verify inline text patterns to determine whether completed Markdown syntax should trigger live formatting/parsing.

| Test Method Name | Test Type | Input Data Examples | Expected Result |
| :--- | :--- | :--- | :--- |
| `CompletedMarkdownSyntax_PromotesLiveParsing` | `[Theory]` | `"**bold**"`<br>``"`code`"``<br>`"[link](https://example.com)"`<br>`"[ ] task"`<br>`"[x] done"` | `ShouldPromoteLiveMarkdown` returns `true` |
| `IncompleteMarkdownSyntax_DoesNotPromoteLiveParsing` | `[Theory]` | `"*"`<br>`"[link]"`<br>`"plain text"`<br>`"2026.04 release notes"` | `ShouldPromoteLiveMarkdown` returns `false` |

---

### 5. Heuristic Markdown Detection Tests

These tests validate detection algorithms that determine if a string (such as pasted text) contains Markdown formatting.

| Test Method Name | Test Type | Input Data Examples | Expected Result |
| :--- | :--- | :--- | :--- |
| `MarkdownLikeText_IsDetectedForPasteParsing` | `[Theory]` | `"# Heading"`<br>`"> quote"`<br>`"- item"`<br>`"1. item"`<br>`"[link](https://example.com)"`<br>``"```csharp\nConsole.WriteLine(\"hi\");\n```"`` | `LooksLikeMarkdown` returns `true` |
| `PlainText_IsNotDetectedAsMarkdown` | `[Theory]` | `"Just a normal sentence."`<br>`"2026.04 release notes"`<br>`"email me at joe@example.com"` | `LooksLikeMarkdown` returns `false` |

---

## Technical Notes

- **WPF Context:** Tests interacting with `FlowDocument` objects or UI controls use the `[WpfFact]` attribute rather than standard xUnit `[Fact]` attributes to ensure execution on a Single-Threaded Apartment (STA) thread required by WPF UI controls.