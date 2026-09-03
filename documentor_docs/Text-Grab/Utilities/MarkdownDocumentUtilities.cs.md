# Technical Documentation: `MarkdownDocumentUtilities.cs`

## Overview

The `MarkdownDocumentUtilities` static class in `Text-Grab.Utilities` provides a comprehensive suite of utilities for converting, serializing, detecting, and styling Markdown content within a Windows Presentation Foundation (WPF) application. 

It acts as a bridge between raw Markdown strings, the [Markdig](https://github.com/xooferew/markdig) parsing engine, and WPF `FlowDocument` elements. It enables bidirectional transformation (Markdown-to-FlowDocument and FlowDocument-to-Markdown), dynamic visual theme application, and real-time Markdown syntax detection for editor features.

---

## Class Signature & Namespace

- **Namespace**: `Text_Grab.Utilities`
- **Class**: `public static partial class MarkdownDocumentUtilities`

---

## Type Aliases

To prevent ambiguity between Markdig syntax elements and WPF document elements, the file defines several type aliases:

| Alias | Source Type |
| :--- | :--- |
| `MarkdigBlock` | `Markdig.Syntax.Block` |
| `MarkdigInline` | `Markdig.Syntax.Inlines.Inline` |
| `MarkdigTable` | `Markdig.Extensions.Tables.Table` |
| `MarkdigTableCell` | `Markdig.Extensions.Tables.TableCell` |
| `MarkdigTableRow` | `Markdig.Extensions.Tables.TableRow` |
| `WpfBlock` | `System.Windows.Documents.Block` |
| `WpfInline` | `System.Windows.Documents.Inline` |
| `WpfList` | `System.Windows.Documents.List` |
| `WpfTable` | `System.Windows.Documents.Table` |
| `WpfTableCell` | `System.Windows.Documents.TableCell` |
| `WpfTableRow` | `System.Windows.Documents.TableRow` |

---

## Key Responsibilities & Features

1. **Markdown to FlowDocument Parsing**: Converts Markdown text into native WPF `FlowDocument` objects using a `MarkdigPipeline` with advanced extensions enabled.
2. **FlowDocument to Markdown Serialization**: Serializes a WPF `FlowDocument` back into formatted Markdown text while retaining structural elements like nested blockquotes, lists, code fences, task lists, and tables.
3. **Theming**: Dynamically updates the styling (`Brush`, `FontFamily`, `FontSize`, borders, padding) of a `FlowDocument` using WPF resource host lookups for light and dark themes.
4. **Syntax Detection & Live Editing Prompters**: Evaluates text input against compiled regular expressions to identify potential Markdown constructs (e.g., live block triggers, live inline promotions, or general Markdown detection).
5. **Metadata Preservation via Dependency Properties**: Attaches Markdown-specific structural metadata (heading levels, quote depths, code fence descriptors, roles) directly to WPF `DependencyObject` flow elements.

---

## Data Structures & Internal Types

### Enums

#### `MarkdownBlockRole`
Defines specific block-level roles assigned to WPF `Block` elements:
- `None`: Standard structural block.
- `CodeBlock`: Paragraph containing raw code / code block content.
- `ThematicBreak`: Horizontal rule / break (`---`).

#### `MarkdownInlineRole`
Defines inline formatting roles assigned to WPF `Inline` or `Run` elements:
- `None`: Standard inline text.
- `CodeSpan`: Inline code snippet wrapped in backticks.
- `LiteralMarkdown`: Markdown constructs retained as verbatim literal text.
- `TaskListMarker`: Checkbox character marker (`☑` or `☐`).

### Records

#### `MarkdownTheme`
A private `sealed record` holding style properties used when rendering or re-theming a document:
- `ForegroundBrush` (`Brush`)
- `BorderBrush` (`Brush`)
- `AccentBrush` (`Brush`)
- `QuoteBrush` (`Brush`)
- `TableHeaderBrush` (`Brush`)
- `CodeBackgroundBrush` (`Brush`)
- `BaseFontFamily` (`FontFamily`)
- `CodeFontFamily` (`FontFamily`)
- `BaseFontSize` (`double`)

---

## Attached Dependency Properties

The class registers several attached WPF `DependencyProperty` objects. These allow standard WPF flow elements to retain Markdown-specific attributes during document lifetime, which facilitates accurate serialization and dynamic styling.

| Dependency Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `QuoteDepthProperty` | `int` | `0` | Indicates nesting depth inside Markdown blockquotes (`>`). |
| `HeadingLevelProperty` | `int` | `0` | Stores header level (1 through 6). |
| `BlockRoleProperty` | `MarkdownBlockRole` | `None` | Maps WPF block elements to specific Markdown block roles. |
| `InlineRoleProperty` | `MarkdownInlineRole` | `None` | Maps WPF inline elements to specific inline formatting roles. |
| `TaskListMarkerCheckedProperty` | `bool` | `false` | Stores checked state of task list items. |
| `CodeFenceInfoProperty` | `string` | `""` | Stores language identifier info string of fenced code blocks (e.g., ```` ```csharp ````). |
| `IsTableHeaderProperty` | `bool` | `false` | Identifies if a WPF `TableCell` represents a table header row. |

---

## Public Methods

### 1. `CreateFlowDocument`

```csharp
public static FlowDocument CreateFlowDocument(string? markdownText, FontFamily fontFamily, double fontSize)
```

- **Purpose**: Parses a raw Markdown string into a WPF `FlowDocument`.
- **Parameters**:
  - `markdownText`: The raw string containing Markdown content.
  - `fontFamily`: Base font family for the document.
  - `fontSize`: Base font size for the document.
- **Returns**: A populated `FlowDocument` object.
- **Behavior**:
  - Initializes a `FlowDocument` with zero page padding.
  - Parses Markdown using `Markdown.Parse` configured with `.UseAdvancedExtensions()`.
  - Iterates over the resulting Markdig AST blocks and delegates rendering to `AppendBlock`.
  - Guarantees at least one empty `Paragraph` exists if no blocks were produced.

---

### 2. `SerializeToMarkdown`

```csharp
public static string SerializeToMarkdown(FlowDocument document, bool preserveLiteralMarkdown = false)
```

- **Purpose**: Converts a WPF `FlowDocument` back into a Markdown-formatted string.
- **Parameters**:
  - `document`: The WPF `FlowDocument` instance to convert. Throws `ArgumentNullException` if null.
  - `preserveLiteralMarkdown`: When `true`, prevents escaping reserved Markdown characters in literal text.
- **Returns**: The generated Markdown text.
- **Behavior**:
  - Iterates through `document.Blocks`.
  - Separates top-level blocks with blank lines (`Environment.NewLine` pairs).
  - Delegates individual block processing to `WriteBlock`.
  - Trims trailing line returns before returning.

---

### 3. `GetDocumentPlainText`

```csharp
public static string GetDocumentPlainText(FlowDocument document)
```

- **Purpose**: Extracts plain text content from a `FlowDocument`.
- **Parameters**:
  - `document`: Target `FlowDocument`. Throws `ArgumentNullException` if null.
- **Returns**: A normalized plain text string generated by converting document text ranges and normalizing newline representations (`\r\n` to `\n`).

---

### 4. `ShouldPromoteLiveBlock`

```csharp
public static bool ShouldPromoteLiveBlock(string? lineTextBeforeSpace)
```

- **Purpose**: Evaluates whether text typed before a space character matches a Markdown block trigger.
- **Parameters**:
  - `lineTextBeforeSpace`: The substring preceding the cursor or space.
- **Returns**: `true` if matched by `LiveBlockTriggerRegex`; otherwise, `false`. Matches elements such as headings (`#` through `######`), blockquotes (`>`), bullet list markers (`-`, `+`, `*`), and ordered list markers (`1.`, `1)`).

---

### 5. `LooksLikeMarkdown`

```csharp
public static bool LooksLikeMarkdown(string? text)
```

- **Purpose**: Determines if a block of text contains Markdown formatting constructs.
- **Parameters**:
  - `text`: Input text string.
- **Returns**: `true` if matching `MarkdownPatternRegex`; otherwise, `false`. Detects block headers, blockquotes, list items, code fences, thematic breaks, links, images, and tables.

---

### 6. `ShouldPromoteLiveMarkdown`

```csharp
public static bool ShouldPromoteLiveMarkdown(string? paragraphText)
```

- **Purpose**: Evaluates if a paragraph contains inline Markdown syntax suitable for live conversion/promotion.
- **Parameters**:
  - `paragraphText`: The paragraph string to check.
- **Returns**: `true` if matching `LiveInlinePromotionRegex`; otherwise, `false`. Checks for task list markers (`[ ]`, `[x]`), bold/italic formatting, backtick code spans, and inline links.

---

### 7. `ApplyTheme`

```csharp
public static void ApplyTheme(FlowDocument document, FrameworkElement resourceHost, bool isLightTheme)
```

- **Purpose**: Recursively applies visual styles and themes to an existing `FlowDocument`.
- **Parameters**:
  - `document`: Target `FlowDocument` (throws `ArgumentNullException` if null).
  - `resourceHost`: WPF `FrameworkElement` used to resolve theme dynamic resource brushes (throws `ArgumentNullException` if null).
  - `isLightTheme`: Boolean indicating whether light theme colors should be prioritized.
- **Behavior**:
  - Builds a `MarkdownTheme` instance by looking up dynamic WPF resource keys (`TextFillColorPrimaryBrush`, `ControlStrokeColorDefaultBrush`, `Teal`, `TextFillColorSecondaryBrush`).
  - Sets base document parameters (`Foreground`, `FontFamily`, `FontSize`).
  - Recursively calls `ApplyBlockTheme` and `ApplyInlineTheme` on all children elements.

---

## Conversion Mechanics

### AST Parsing & Document Building (Markdown $\rightarrow$ WPF)

1. **Blocks (`AppendBlock`)**:
   - `HeadingBlock`: Creates a `Paragraph`, sets `FontWeight.Bold`, attaches heading level metadata (`1` to `6`), and parses contained inlines.
   - `ParagraphBlock`: Creates a standard `Paragraph` with inline children.
   - `QuoteBlock`: Recursively calls `AppendBlock` on children, incrementing the `quoteDepth` parameter.
   - `ListBlock`: Creates a WPF `List`. Evaluates `IsOrdered` to pick `TextMarkerStyle.Decimal` or `TextMarkerStyle.Disc`. Traverses child `ListItemBlock` items.
   - `FencedCodeBlock` / `CodeBlock`: Generates a code block paragraph via `CreateCodeParagraph`, tagging it with `MarkdownBlockRole.CodeBlock` and saving code fence language details.
   - `ThematicBreakBlock`: Creates a paragraph containing `----------` tagged as `MarkdownBlockRole.ThematicBreak`.
   - `MarkdigTable`: Maps table columns and builds `TableRowGroup`, `WpfTableRow`, and `WpfTableCell` structures via `CreateTable`.
2. **Inlines (`AppendInlineContainer` / `AppendInline`)**:
   - `LiteralInline` $\rightarrow$ WPF `Run`.
   - `LineBreakInline` $\rightarrow$ WPF `LineBreak`.
   - `CodeInline` $\rightarrow$ WPF `Run` with `Consolas` font and tagged with `MarkdownInlineRole.CodeSpan`.
   - `TaskList` $\rightarrow$ WPF `Run` containing `\u2611` (`☑`) or `\u2610` (`☐`), tagged with `MarkdownInlineRole.TaskListMarker`.
   - `EmphasisInline` $\rightarrow$ WPF `Bold` or `Italic` elements depending on delimiter count.
   - `LinkInline` $\rightarrow$ WPF `Hyperlink` (if non-image) or literal Markdown text (if an image).
   - `HtmlInline` / Fallbacks $\rightarrow$ Verbatim WPF `Run` tagged with `MarkdownInlineRole.LiteralMarkdown`.

---

### Document Serialization (WPF $\rightarrow$ Markdown)

1. **Blocks (`WriteBlock` / `WriteParagraph` / `WriteList` / `WriteTable`)**:
   - **Paragraphs**: Inspects `HeadingLevel` attached property to prepend `#` markers. Inspects `BlockRole`:
     - If `ThematicBreak`, renders `---`.
     - If `CodeBlock`, wraps content in ``` triple backticks including language metadata.
   - **Lists**: Calculates nesting indentation and markers (`- ` for unordered, `1. ` for ordered based on `StartIndex`). Supports multiline item continuation indentation.
   - **Tables**: Reads header cells, renders Markdown header separator lines (`| --- |`), escapes pipe characters (`|` to `\|`), and replaces newlines inside cells with `<br />`.
   - **Blockquotes**: Prepends `> ` matching the depth stored in the attached `QuoteDepthProperty`.
2. **Inlines (`WriteInline`)**:
   - `Run`: Reads `InlineRole` attached property:
     - `TaskListMarker` $\rightarrow$ `[x]` or `[ ]`.
     - `CodeSpan` $\rightarrow$ Encapsulated in backticks (`` `text` ``).
     - Escapes special characters (`\`, `` ` ``, `*`, `_`, `[`, `]`, `|`) unless `preserveLiteralMarkdown` is `true`.
   - `Hyperlink` $\rightarrow$ Formats as `[text](url)`.
   - `Bold` / `Italic` $\rightarrow$ Encapsulated in `**` or `*`.

---

## Regular Expressions Reference

The class uses source-generated static partial regular expressions (`[GeneratedRegex]`):

```csharp
// Matches block triggers typed at the beginning of lines (0-3 leading spaces followed by markdown syntax tokens)
[GeneratedRegex(@"^\s{0,3}(#{1,6}|>+|[-+*]|\d+[.)])$", RegexOptions.Compiled)]
private static partial Regex LiveBlockTrigger();

// Matches inline syntax constructs for live editor promotion
[GeneratedRegex(@"(^|\s)\[( |x|X)\](\s|$)|(\*\*|__)(?=\S).+?\4|(?<!\*)\*(?=\S).+?(?<=\S)\*|(?<!_)_(?=\S).+?(?<=\S)_|`[^`\r\n]+`|\[[^\]\r\n]+\]\([^)]+\)", RegexOptions.Compiled)]
private static partial Regex LiveInlinePromotion();

// Multi-line expression to detect if text input contains markdown formatting constructs
[GeneratedRegex(@"(^|\n)\s{0,3}(#{1,6}\s|>+\s|[-+*]\s|\d+[.)]\s|```|~~~|---\s*$|___\s*$|\*\*\*\s*$)|\[[^\]]+\]\([^)]+\)|!\[[^\]]*\]\([^)]+\)|(^|\n)\|.+\|\s*$", RegexOptions.Multiline | RegexOptions.Compiled)]
private static partial Regex MarkdownPattern();
```

---

## System Dependencies

- **Framework**: WPF (`System.Windows`, `System.Windows.Documents`, `System.Windows.Media`)
- **Third-Party Assemblies**:
  - `Markdig`
  - `Markdig.Extensions.TaskLists`
  - `Markdig.Extensions.Tables`
  - `Markdig.Syntax`
  - `Markdig.Syntax.Inlines`