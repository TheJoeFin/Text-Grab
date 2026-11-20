# Text Grab v5 — phase work plans

Text Grab v5 adds two features on one shared foundation:

1. **Node-based workflows**: triggers → transforms → outputs, runnable headless.
2. **A composable Edit Text Window (ETW)**: a shell that hosts panes, chrome and buttons described by a layout file.

Each file in this folder is the work plan for one phase. Every phase is sized to be one branch and one pull request, and the app must build, pass its tests and behave the same for existing users after each one.

**If you are an agent starting a phase: read this whole file, then your phase file. Both are binding.**

**Which phase ships in which release is in [Release-Plan.md](Release-Plan.md).** Read it before choosing what to start next: the order below follows it.

## Phase index

Update the Status column when you finish a phase (`Not started` → `In progress` → `Done (<short commit hash>)`).

| Phase | Work plan | Depends on | Ships in | Status |
| --- | --- | --- | --- | --- |
| F1 | [DI composition root](F1-DI-Composition-Root.md) | — | 4.17 | Done (790bc4e) |
| F2 | [Per-call OCR option overrides](F2-OCR-Option-Overrides.md) | — | 4.17 | Done (9d8b126) |
| F3 | [Headless OCR service](F3-Headless-OCR-Service.md) | F2 | 4.17 | Done (bf1a568) |
| F4 | [Output services](F4-Output-Services.md) | F1 | 4.17 | Done (fea6fe7) |
| F5 | [Trigger infrastructure](F5-Trigger-Infrastructure.md) | F1 | 4.17 | Done (964b0d0) |
| F6 | [Observable history store](F6-History-Store.md) (optional) | — | — | Shelved until a consumer needs it (steps 1–2 done, fe8a24b; see the phase file) |
| A1 | [Action registry](A1-Action-Registry.md) | F1 | 4.17 (inert) | Done (96e59be) |
| W1 | [Workflow engine](W1-Workflow-Engine.md) | A1, F3, F4, F5 | 4.18 (inert) | Not started |
| W2 | [Workflow end-to-end and entry points](W2-Workflow-E2E-Entry-Points.md) | W1 | 5.0 | Not started |
| W3 | [Nodify graph editor](W3-Nodify-Graph-Editor.md) | W2, E4a | 5.0 | Not started |
| E1 | [Editor session and surfaces](E1-Editor-Session-Surfaces.md) | — | 4.17 | Done (E1a 21428ec, E1b 1644515) |
| — | Code mode (#645, PR #710; not a planned phase — see [E1](E1-Editor-Session-Surfaces.md#after-e1-code-mode-2026-11-14-pr-710)) | E1 | 4.17 | Done (2bc0599) |
| E2 | [ETW commands onto the registry](E2-Commands-Onto-Registry.md) | A1, E1 | 4.18 | Not started |
| E3 | [Extract ETW clusters into panes](E3-Extract-Clusters.md) | `calc`: E1. Others: E2 | `calc`: 4.17. Others: 4.18 | In progress (`calc` done, 0fb4420) |
| E4a | [Pane host and pane layouts](E4-Layout-Engine.md) | E3 `calc` | 4.17 | Done (8033cba) |
| E4b | [Chrome from the layout definition](E4-Layout-Engine.md) | E2, E3, E4a | 4.18 (opt-in) | Not started |
| E5 | [New panes](E5-New-Panes.md) | E4a (\+ per pane) | 4.17: `recent`, `mermaid`, `markdown-preview`. 5.0: `transformer`, `audio` | Not started |
| E6 | [Ribbon, layout picker, layout editor](E6-Ribbon-Layout-Editor.md) | `picker`: E4a. Others: E4b | `picker`: 4.17. Others: 5.0 | Not started |

Order from here: **4.17** — E3 `calc` → E4a → E5 `recent`, `mermaid`, `markdown-preview` and E6 `picker` (E5 `folder-tree` only if the rest lands smoothly; see the release plan). **4.18** — E2 → the other E3 clusters → E4b, with W1 alongside. **5.0** — W2, E5 `transformer` and `audio`, the rest of E6 → W3.

## Rules for every phase

1. **Check prerequisites first.** If a phase your plan depends on is not `Done` in the table above, stop and say so.
2. **Verify "Current state" before relying on it.** Line numbers and counts in the phase files were correct on 2026-11-09 (`dev` @ `08eae21`); the unfinished E and W files were revised on 2026-11-14 (`dev` @ `2bc0599`) after Code mode landed, and E3 `calc`, E4a, E5 and E6 `picker` again on 2026-11-19 (`dev` @ `5e3b889`) after D1, D4 and D5 were confirmed. Re-check them with a search; fix the phase file if they have drifted.
3. **Stay in scope.** If you find a blocker outside your phase, stop and report it. Do not widen the phase to fix it.
4. **No behavior change unless the plan says so.** Change only what the work requires. No drive-by renames, reformatting or "cleanup".
5. **Tests before risky moves.** When a plan says "characterization tests first", write and pass them against the *old* code before changing it.
6. **Follow the tier rules** in [`../Core-Split-Plan.md`](../Core-Split-Plan.md) §1 and §3. In short:
   
     - Dependencies point one way: `Text-Grab` (WPF app) → `Text-Grab.Core.Windows` → `Text-Grab.Core`.
     - `Text-Grab.Core` is plain `net10.0`: no WPF, no WinRT, no P/Invoke, no `System.Drawing.Common`.
     - `Text-Grab.Core.Windows` keeps `UseWPF=false` and `UseWindowsForms=false`. Never flip them, and never add a `FrameworkReference` to `Microsoft.WindowsDesktop.App`.
     - All projects share `RootNamespace = Text_Grab`. Moved files keep their namespace.
     - Geometry in Core and Core.Windows is `System.Drawing.RectangleF` / `PointF` / `SizeF`. `BitmapSource` never leaves the app.
     - Both libraries must keep `<RuntimeIdentifiers>win-x86;win-x64;win-arm64</RuntimeIdentifiers>`.
     - Anything the app registers for lower tiers to call is registered from a `[ModuleInitializer]`, not from `App.appStartup`. The `Tests` project loads the app assembly without ever raising the WPF Startup event.
7. **New code style:** nullable enabled, file-scoped namespaces, records for data models, `async` + `CancellationToken` on anything that does I/O. New namespaces: `Text_Grab.Core.Actions`, `Text_Grab.Core.Workflows`, `Text_Grab.Core.Abstractions`, `Text_Grab.Core.Editing`, `Text_Grab.Core.Layouts`. Also read `.github/copilot-instructions.md`.
8. **Git:** work on a new branch off `dev` named in your prompt. If the working tree is dirty when you start, stop and report. Commit after each step that passes the gates. Do not push, and do not open a pull request, unless told to.

## Build and test gates

```bash
# Primary gate: builds Core -> Core.Windows -> app -> Tests.
dotnet build Tests/Tests.csproj -c Debug -p:Platform=x64

# Fast library gates.
dotnet build Tests.Core/Tests.Core.csproj -c Debug
dotnet build Tests.Core.Windows/Tests.Core.Windows.csproj -c Debug -p:Platform=x64
```

Run the tests by launching the built test executables (they are xunit.v3 executables). Find each one under its project's `bin` folder after building:

```bash
Tests.Core/bin/Debug/net10.0/Tests.Core.exe
Tests.Core.Windows/bin/x64/Debug/net10.0-windows10.0.22621.0/Tests.Core.Windows.exe
Tests/bin/x64/Debug/net10.0-windows10.0.22621/Tests.exe
Tests/bin/x64/Debug/net10.0-windows10.0.22621/Tests.exe -class Tests.SomeClassName   # one class
```

Things that have bitten people here:

- `dotnet test Tests/Tests.csproj` has reported "Zero tests ran" in this repo. Never read that as a pass. Run the executable.
- The exact `bin` path depends on the platform you built for; if a path above does not exist, search `bin` for the `.exe` and check its timestamp is from your build. Stale copies in other platform folders are common.
- **Do not run `dotnet build Text-Grab.sln`.** The MSIX project (`Text-Grab-Package.wapproj`) cannot build under the dotnet CLI. It needs Visual Studio.
- You cannot verify MSIX packaging from the CLI. If your phase adds a NuGet package, say so in your report so the package build gets checked in Visual Studio.
- When smoke-testing the app by hand, launch the exe from the folder your build just wrote, and confirm no older Text Grab instance is already running.

## Decisions

Open design decisions. A phase that lists one as **required** must use the value in the Choice column. If Status is `Default`, the choice is a recommendation the owner has not yet confirmed: use it for spikes and prototypes, but ask before building production code on it.

| ID | Decision | Choice | Alternative | Required by | Status |
| --- | --- | --- | --- | --- | --- |
| D1 | Docking | Use AvalonDock v5<br />for 4.17-beta2 just replace existing capability (no floating panes, auto-hide, or drag for now) and hide chrome until more features are turned on. Double clicking for pane sizing could get dropped.<br />layouts persist as our JSON, not AvalonDock XML | Own split-tree from Grid \+ GridSplitter | E4a — **needed for 4.17** | Confirmed |
| D2 | Ribbon | Custom tabbed command bar built from WPF-UI controls, generated from the action registry | Fluent.Ribbon, or `System.Windows.Controls.Ribbon` | E6 `ribbon` | Default |
| D3 | Range highlighting in the editor | **Changed 2026-11-14.** Highlight in the code editor: an AvalonEdit `IBackgroundRenderer` on `CodeEditor`, as `SelectionOccurrencesRenderer` already does for matching words. A layout that needs highlighting (Transcription) opens its text in Code mode as Plain Text. No highlighting in Text, Markdown or Spreadsheet modes. | Adorner layer over the `TextBox` (the previous choice; still needed if highlighting must work in Text mode) | E5 `audio` | Default |
| D4 | View-model library for new components | Use CommunityToolkit.Mvvm | Hand-rolled `INotifyPropertyChanged` | E5, W3 — **needed for 4.17** | Confirmed |
| D5 | Layout scope | Per window instance, with a per-file-type default and a global default | One global layout | E4a — **needed for 4.17** | Confirmed |
| D6 | Document of record (new 2026-11-14) | Keep the `TextBox` as the document in every mode through 4.18; the code editor mirrors it edit by edit. Revisit before 5.0 with a release of Code mode behind us. | Make AvalonEdit the Text surface too and retire the `TextBox`: one editor, one undo stack, half the memory for large documents — but it loses spell check, the link and email context-menu items and Use Margins until they are rebuilt, and changes IME and touch behavior | Nothing yet | Default |

Fixed decisions (do not revisit):

- Full `Microsoft.Extensions.DependencyInjection`; composition root in the app.
- `GrabFrame` and `FullscreenGrab` stay code-behind shells. Only the Edit Text Window is decomposed.
- Existing behavior moves into components largely as-is. New components are written with view models. No blanket MVVM rewrite of working code.
- Layouts and workflows are JSON files in stores, not entries in `Settings.settings`.

## Shared vocabulary

These names are used across phase files. The phase that introduces each one owns its final shape; later phases consume it as built.

| Name | Introduced in | What it is |
| --- | --- | --- |
| `ServiceAccess` | F1 | Core-side static through which lower tiers reach the app's DI container. |
| `OcrOptions` | F2 | Immutable per-call OCR options record. |
| `IOcrService` | F3 | Headless OCR of a file, stream or screen region. |
| `IClipboardService`, `ITextFileWriter`, `IToastService`, `IHttpPostService` | F4 | Output seams. |
| `ITriggerSource`, `TriggerPayload` | F5 | Things that start a workflow. |
| `ActionDescriptor`, `ITextTransform`, `IEditorCommand`, `IActionRegistry` | A1 | One description of every action, consumed by menus, buttons, hotkeys, post-grab actions and workflow nodes. |
| `WorkflowDefinition`, `IWorkflowStep`, `WorkflowRunner`, `IWorkflowStore` | W1 | Workflow model and headless runtime. |
| `IEditorSession`, `IEditorSurface`, `TextSpan` | E1 | The ETW document and the editor control behind it, without naming a control; `TextSpan` is a range of the document in plain-text offsets. |
| `EtwEditorMode.Code`, `CodeSurface`, `CodeEditor` | Code mode | The fourth editor: an AvalonEdit control holding the same characters as the text box. `CodeSurface` is its `IEditorSurface`. |
| `TextEdit` | Code mode | One replacement in a text (`Removed` span, `Inserted` string). `TextEdit.Between(old, new)` is the smallest edit between two texts. Reuse it wherever ranges must survive an edit (E5 `audio`). |
| `CodeLanguages`, `CodeLanguageDetector`, `CodeFolding` | Code mode | Core: the language list with extensions, the heuristic detector, and the folding rules. |
| `IEditorPane` | E3 | A dockable component of the ETW. |
| `EtwLayoutDefinition`, `ILayoutStore` | E4a | JSON description of an ETW's panes and, from E4b, its chrome and buttons. |
| `TranscriptDocument` | E5 | Timed transcript segments anchored to text ranges. |

## Reporting

End every phase with a report containing:

1. What was done, step by step, with commit hashes.
2. Gate results: the exact commands run and their pass/fail counts. Paste failures; never summarize a failure as a pass.
3. Anything skipped or deferred, with the specific reason.
4. Anything in the phase file that turned out to be wrong, and how you corrected it.
5. New NuGet packages added (for the MSIX check).
6. Manual checks the owner still needs to do.