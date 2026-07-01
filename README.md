<p align="center">
  <img width="128" align="center" src="images/TealSelect.png">
</p>
<h1 align="center">
  Text Grab
</h1>
<p align="center">
  Copy any text you can see.
</p>
<p align="center">
  <a href="https://www.microsoft.com/en-us/p/text-grab/9mznkqj7sl0b?cid=TextGrabGitHub" target="_blank">
    <img src="images/storeBadge.png" width="200" alt="Store link" />
  </a>
</p>

<p align="center">
    <img src="https://img.shields.io/github/downloads/thejoefin/text-grab/total" alt="GitHub Downloads (all assets, all releases)" />
    <img src="https://img.shields.io/github/v/release/thejoefin/text-grab" alt="GitHub Release" />
</p>

## Overview
![All Modes In Light Mode](images/All-Modes-Light.png)

Use Text Grab on Windows to capture text with OCR, clean it up quickly, and move it into the next step of your workflow.

When text gets trapped inside images, videos, PDFs, and parts of apps where you cannot select it, Text Grab helps you get it back out. You can take a screenshot or open a supported file, run it through the OCR engine, and send the result to the clipboard or straight into an editor. All OCR runs entirely on your device — no internet connection, no cloud service, and no per-use cost. Everything stays local and private. You can also do much more than copy text from images, because Text Grab gives you multiple modes for capture, post-grab cleanup, spreadsheet-style editing, and fast text reuse.

You can use it all day without friction. Launch it quickly from the taskbar, open specific modes from the command line, or enable the background process so global hotkeys work anywhere in Windows.

The Full-Screen Grab mode is also the basis of the [PowerToys Text Extractor](https://learn.microsoft.com/en-us/windows/powertoys/text-extractor).

### Requirements

- **Windows 10 or later** — required for all features using the Windows OCR API.
- **Windows 11 on a Copilot+ PC (Microsoft Store install)** — required for Windows AI features, which use the on-device Neural Processing Unit for higher-accuracy recognition.

## How to Install

### Official

- [Microsoft Store](https://www.microsoft.com/en-us/p/text-grab/9mznkqj7sl0b?cid=TextGrabGitHub)
- [GitHub Releases](https://github.com/TheJoeFin/Text-Grab/releases/latest)

### Community

- [scoop](https://scoop.sh/) — `scoop install text-grab`
- [choco](https://community.chocolatey.org) - `choco install text-grab`

## How to Build

Build and test Text Grab on Windows.

Get the code:
- Install Git: https://git-scm.com/download/win
    - `winget install git.git`
- `git clone https://github.com/TheJoeFin/Text-Grab.git`

### With Visual Studio 2022 or Visual Studio 2026
- Install Visual Studio (the free Community edition is sufficient).
    - Install the "Universal Windows Platform development" workload.
    - Install the ".NET desktop development" workload.
    - Install the ".NET cross-platform development" workload.
    - Install Windows 10 SDK `10.0.22621.0`
- Open `Text-Grab.sln` in Visual Studio.
- Set `Text-Grab-Package` as the startup project.
- Set the CPU target to `x64` or `ARM64`.
- Press `F5` or choose **Local Machine**.

### With the .NET SDK or Visual Studio Code
- Install the .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
- This repository pins SDK `10.0.100` in `global.json`.
- Optional for debugging: install Visual Studio Code https://code.visualstudio.com/ and the C# extension / C# Dev Kit.
- Open the `Text-Grab` folder in VS Code.
- Restore dependencies with `dotnet restore Text-Grab.sln`
- Build with `dotnet build Text-Grab\Text-Grab.csproj`
- Run tests with `dotnet test Tests\Tests.csproj`
- In VS Code, press `F5` to launch with the included debug configuration.

## Choose from Four Modes

### 1. Full-Screen Mode (basis of [Text Extractor](https://learn.microsoft.com/en-us/windows/powertoys/text-extractor))
![Select text from a region](images/FSG-V4.gif)

Use Full-Screen Mode when you want to select any region of the screen and copy the recognized text straight to your clipboard.

You can also click once to try to copy a single word. That works because the Windows OCR API draws a bounding box around each recognized word.

If you click or select an area with no text, the Text Grab window stays active so you can try again. To exit, press Escape, right-click and choose Cancel, or press Alt+F4.

### 2. Grab Frame Mode
![Grab Frame](images/3-2-GF-Editing-Table-2.gif)

Use Grab Frame when you want a movable OCR window you can keep over part of your screen. Position the frame over the text you want, then grab text by searching for it, clicking a word border, or clicking the Grab button.

Grab Frame uses the same OCR engine as Full-Screen Mode, so you get the same strengths and tradeoffs. OCR is not perfect, but you can often improve accuracy by adjusting the size and position of the frame.

### 3. Edit Text Window

Use the Edit Text Window to turn OCR results into clean, usable content. You can work in plain text, spreadsheet-style, or markdown mode depending on what you need. Grab text with Full-Screen Mode or Grab Frame, then keep refining it without leaving Text Grab.

Inside the Edit Text Window, you get tools that help you quickly fix, extract, and structure text.

**Clean OCR output**
- Make text into a single line
- Toggle between UPPERCASE, lowercase, and Titlecase
- Trim spaces and empty lines
- Remove duplicate lines
- Replace reserved characters (like spaces, /, %, etc.)
- Extract text based on patterns like phone numbers, emails, or customer patterns

**Search and extract**
- Find and replace
- Extract regular expressions
- Launch URLs

**Structure data**
- Convert stacked data to table format
- Continue working in Spreadsheet mode for row-and-column cleanup
- Transpose captured table data when OCR gives the right data in the wrong orientation

**Workflow helpers**
- List files and folders in chosen directory
- Watch clipboard for changes
- Copy text from every image in a folder
- Save or copy cleaned results into the next app in your workflow

If you capture tabular data, use Spreadsheet mode to keep cleaning it after OCR. You can straighten columns, copy selected rows, copy the current column, and save the final result as delimited data for use elsewhere.

The Edit Text Window also includes a **Calc Pane** for quick calculations alongside your text. It evaluates expressions in real time line by line, so you can stay in the same window while checking totals, conversions, or quick calculations from OCR results.

The Calc Pane can help with:
- Basic math like `+`, `-`, `*`, `/`, `%`, and exponents
- Functions like `sin`, `cos`, `tan`, `sqrt`, `abs`, and `log`
- Variables and constants like `x = 10`, `pi`, `e`, and `tau`
- Unit conversions like `5 miles to km` or `100 fahrenheit to celsius`
- Date math like `today + 5 days` or comparing dates in weeks
- Aggregate values such as sum, average, median, count, min, max, and product

Use the Calc Pane when OCR pulls numbers, quantities, dates, or measurements out of an image and you want quick answers without moving the data into another calculator first. It can also summarize numeric values from spreadsheet selections, copy all calculation results, and hide or show errors depending on how much detail you want while cleaning data.

### 4. Quick Simple Lookup
![Quick Simple Lookup](images/Quick-Simple-Lookup.gif)

Quick Simple Lookup is not about OCR. Use it to retrieve frequently used text like URLs, emails, part numbers, and more. Think of it as your long-term memory: a custom dictionary you can edit and recall instantly whenever you need it. The workflow is fast:

1. Press the hotkey (default is Win + Shift + Q)
2. Begin typing to filter the lookup to the item you want
3. When what you want is the first result, press Enter
4. Paste the value into the application you are using

### From OCR to usable text

Text Grab keeps working after OCR. After each grab, you can run one or more post-grab actions to clean or route the text before it lands in the next tool. That helps when you repeatedly grab invoice line items, SKU tables, IDs, or lists from screenshots and want them in a cleaner, more usable shape right away.

Examples of post-grab actions and workflow helpers include:
- Trim each line
- Remove duplicate lines
- Fix GUID formatting
- Try to insert text into the active app
- Open a web search
- Save reusable combinations of actions as grab templates

One example workflow: capture a pricing table with Full-Screen Grab or Grab Frame, run a post-grab action like **Trim each line** to remove extra whitespace, then send the result into the **Edit Text Window** in **Spreadsheet** mode. From there you can correct OCR mistakes, move data into the right columns, transpose the table if needed, and then copy or save the cleaned result as data ready for Excel, Google Sheets, an ERP, or another step in your workflow.

## How Text Grab Captures Text

Text Grab supports several capture methods so you can choose the one that best fits your hardware, workflow, and source material. All methods run entirely on your device — nothing is sent to a server, and there are no per-use costs.

### WinAI (Windows AI API) — Copilot+ PCs

The newest and most accurate option. The Windows AI API is available on Copilot+ PCs with Text Grab installed through the Microsoft Store, and runs on the dedicated Neural Processing Unit (NPU). Because inference happens on dedicated silicon, it is fast and power-efficient. It produces higher-quality results than traditional OCR, especially on handwriting, stylized fonts, and complex layouts. WinAI models support a wide range of languages automatically without requiring additional language packs. If your device supports it, Text Grab will offer this as an option automatically.

### WinRT OCR (Windows OCR API) — Windows 10 and later

The default capture method for most users. The Windows Runtime OCR API has shipped with Windows since Windows 10 and runs entirely on your device. It is fast, reliable, and produces excellent results for printed text in screenshots, documents, and images. Recognized languages depend on the language packs installed in Windows — add languages through Windows Settings to enable recognition in those languages.

### Tesseract

An open-source OCR engine that Text Grab can use as an alternative to the WinRT API. Tesseract has been around for decades, is highly configurable, and supports a very large range of languages through downloadable language data files available in the [tessdata repository](https://github.com/tesseract-ocr/tessdata). It can be a good choice when you need more control over recognition parameters or when working with image types where you want to compare output between engines.

### Direct Text (UI Automation)

Sometimes OCR is not needed at all. When text is displayed in a native UI element — a text box, label, list, or document rendered by the operating system — Text Grab can read it directly using Windows UI Automation without running any OCR. This approach is faster, perfectly accurate, and works regardless of display resolution or font. Use it when the text is selectable in theory but not easily accessible through normal copy and paste.

## Command Line Interface

Use these arguments with `Text-Grab.exe`:
- `Fullscreen` launches into Fullscreen Grab mode
- `GrabFrame` launches a new Grab Frame
- `EditText` launches a new Edit Text Window
- `QuickLookup` launches Quick Simple Lookup
- `Settings` opens Text Grab settings
- `--grabframe "file path"` opens a supported image or PDF directly in Grab Frame
- `--windowless "file path"` reads or OCRs a file and copies the resulting text without opening a window
- `"file path"` opens text files in Edit Text and opens supported image or PDF files in Grab Frame
- `"folder path"` e.g. `.\Text-Grab.exe "C:\Users\myPC\Downloads"` opens a new Edit Text Window and scans the images in that directory

### Bulk processing folders of images

You are not limited to one screenshot at a time. You can also bulk process a folder full of images or PDFs and collect the OCR output in the Edit Text Window for review, cleanup, and export.

Use this when you have a batch of scans, receipts, product labels, screenshots, or archived documents that all need text extracted in one pass. Instead of opening files one by one, point Text Grab at the folder and let it process the supported files it finds.

Use bulk folder OCR to:
- Scan every supported image or PDF in a selected folder
- Open a folder directly from the command line
- Optionally include file names, headers, and footers in the output
- Process subfolders recursively when needed
- Apply a grab template while processing a batch
- Write `.txt` output files for the processed items

After the batch OCR finishes, you can continue cleaning the combined results in the Edit Text Window, use find and replace or regex extraction, convert stacked data into table form, or move the result into Spreadsheet mode for final cleanup before sending it elsewhere in your workflow.

### Patterns (Regular Expressions / RegEx)

Patterns, also known as regular expressions or RegEx, help you make text cleanup more robust, accurate, fast, and repeatable throughout Text Grab. They are especially useful when OCR results contain IDs, dates, prices, email addresses, URLs, phone numbers, or other text that follows a recognizable structure even when the surrounding text is messy.

Patterns are not just a niche power-user feature. They help you turn one successful cleanup step into a reusable workflow you can apply again and again.

Patterns are used throughout the app to:
- Find similar values in OCR text even when the exact text changes
- Extract only the parts you care about from larger blocks of text
- Save reusable cleanup and extraction rules
- Power grab templates that can pull the first, last, all, or specific matches from OCR output
- Speed up repeated searches in tools like Edit Text Window and Quick Simple Lookup

Text Grab also helps you build patterns faster. In the Edit Text Window, selecting text can generate a suggested pattern automatically, and you can adjust how broad or specific it is. That makes it easier for you to start with a real OCR result, find similar matches, and then reuse the pattern in Find and Replace or save it for later.

Built-in and saved patterns make repeated cleanup more practical. For example, you can save patterns for common data types like email addresses, phone numbers, URLs, GUIDs, dates, currency amounts, or custom IDs used in your work. Once a pattern is saved, you can test it, explain it, reuse it in searches, and include it in templates so your cleanup steps stay consistent across future grabs and bulk-processing jobs.

### Grab Templates

Grab Templates turn a one-time OCR setup into a repeatable workflow. You can define how Text Grab should collect text from similar screenshots, images, PDFs, forms, labels, invoices, or tables so you do not have to rebuild the same extraction steps every time.

Build templates from:
- **Named capture regions** for fixed parts of an image, such as a total field, header, SKU, or table cell
- **Pattern placeholders** that use saved RegEx patterns to pull matching values from the OCR text
- **Output templates** that format the final result with labels, line breaks, tabs, and placeholders

Use Grab Templates when some data always appears in the same place, while other data is easier to find by pattern. One template can OCR exact regions for known fields and also use Patterns to pull the first, last, all, or specific matches for values like IDs, dates, totals, email addresses, or codes.

Grab Templates help make OCR workflows:
- More **robust** by mixing region-based capture with pattern-based extraction
- More **accurate** by focusing OCR on the exact areas that matter
- **Faster** by eliminating repeated manual cleanup and re-selection
- More **repeatable** by saving the structure of a successful extraction for reuse later

Grab Templates fit naturally with the rest of Text Grab. You can apply them during capture, use them with bulk image-folder processing, and send the formatted output into the Edit Text Window or Spreadsheet mode for final cleanup before the data moves to the next step in your workflow.

## Bringing it all together

Text Grab does more than copy text from images. You can use it as a platform for understanding text through **patterns**, **position**, and **automation**.

Some information makes the most sense based on where it appears on the screen or page. Other information makes the most sense based on the pattern it follows, like an ID, a date, a price, an email address, or a code. Text Grab brings those approaches together so you can capture text, identify what matters, clean it up, structure it, calculate with it, and move it into the next step of your work.

That is why the features in Text Grab are designed to work together:
- OCR capture modes get text into the app quickly
- Post-grab actions clean and route text immediately
- Patterns make extraction robust, accurate, and repeatable
- Grab Templates combine regions, patterns, and formatting into reusable workflows
- Edit Text Window, Spreadsheet mode, and the Calc Pane help finish the job without switching tools

Instead of treating OCR as the end of the process, Text Grab is the beginning of a smarter text workflow.

## Thanks for using Text Grab

I hope this app saves you time. If you have questions or feedback, reach out on Bluesky [@TheJoeFin](https://bsky.app/profile/thejoefin.com), Twitter [@TheJoeFin](http://www.twitter.com/thejoefin), or by email at joe@textgrab.net.

### It would not be possible without these open source packages
- WPF UI - Fluent UI Style: https://github.com/lepoco/wpfui
- ZXing.Net - Barcode and QR Code scanning: https://github.com/micjahn/ZXing.Net
- CliWrap: https://github.com/Tyrrrz/CliWrap
- Microsoft Community Toolkit: https://github.com/CommunityToolkit

For the current direct NuGet dependency list and local third-party license notices, see [BUILT-WITH.md](BUILT-WITH.md).

### Pssst, on a Mac?
Check out the awesome app Text Sniper. It is very similar to Text Grab, but for Mac. If you use my [affiliate link here](https://gumroad.com/a/984365907/NYNNM), you will also support Text Grab development.
