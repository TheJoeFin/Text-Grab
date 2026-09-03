# Technical Documentation: `GrabFrameWordGroupingMode.cs`

## Overview

The `GrabFrameWordGroupingMode` file defines a public C# enumeration (`enum`) within the `Text_Grab.Models` namespace. Its primary purpose is to define the available modes for grouping Optical Character Recognition (OCR) results into `WordBorder` objects within the GrabFrame feature of Text-Grab.

## File Information

* **File Path:** `Text-Grab/Models/GrabFrameWordGroupingMode.cs`
* **Namespace:** `Text_Grab.Models`
* **Type:** `enum` (`GrabFrameWordGroupingMode`)

---

## Enum Values and Key Components

The `GrabFrameWordGroupingMode` enumeration provides four distinct modes that specify how recognized OCR text is partitioned or grouped into `WordBorder` instances:

### 1. `Line`
* **Summary:** One `WordBorder` per OCR line.
* **Details:** Groups text line-by-line. This is identified in the inline documentation as the original default mode.

### 2. `Word`
* **Summary:** One `WordBorder` per individual OCR word.
* **Details:** Groups text at the most granular level, assigning an individual border to every detected word in the OCR output.

### 3. `Paragraph`
* **Summary:** Wrapped lines merged into paragraph blocks.
* **Details:** Groups related or wrapped lines of text together into larger paragraph-level blocks.

### 4. `Window`
* **Summary:** All OCR output in a single `WordBorder`.
* **Details:** Combines all detected OCR text into one single, overarching `WordBorder`.

---

## How It Works

`GrabFrameWordGroupingMode` serves as a strongly typed set of options used by models or components handling GrabFrame operations. 

When OCR processing takes place, code referencing this enum checks the selected `GrabFrameWordGroupingMode` value to determine how to construct `WordBorder` containers for the detected text:

* **Line-level grouping (`Line`)** isolates each line.
* **Word-level grouping (`Word`)** isolates each word.
* **Paragraph-level grouping (`Paragraph`)** combines wrapped lines into logical blocks.
* **Window-level grouping (`Window`)** captures all detected text across the frame in one container.