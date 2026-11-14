# Release plan — 4.17, 4.18 and 5.0

*Written 2026-11-14 against `dev` @ `2bc0599` (Code mode merged, PR #710). Version in the tree: 4.16.0.*

This file says which phase ships in which release, and what each release has to prove before the next one starts. The phase files say how to build each phase; [README.md](README.md) holds the shared rules.

## The short version

| Release | What users get | What it proves |
|---|---|---|
| **4.17.0-beta1** | Everything already on `dev`: Code mode, the Markdown additions, Find on Screen, the tray flyout, window capture. No panes. | The foundation work nobody can see: DI startup (F1), file OCR loading (F3), the new clipboard and toast paths (F4), the hotkey router and shared clipboard monitor (F5), the editor session (E1), and a new package (AvalonEdit) in the MSIX. |
| **4.17.0-beta2** | **Panes.** The calc pane becomes the first real pane; new Recent, Mermaid and Markdown-preview panes (Folder tree if it is ready); a Layouts menu with a few built-in layouts. Menus and the bottom bar are unchanged. | The pane contract, the dock renderer, layout files and their storage. |
| **4.17.0** | beta2 plus fixes. | — |
| **4.18.0-beta1 → 4.18.0** | Almost nothing new on the surface. Optional: a "new window chrome (preview)" setting. | The two changes most likely to break an existing user's setup: saved bottom-bar buttons and post-grab actions moving onto action IDs (E2), and the window's menus being generated from a layout (E4b). |
| **5.0.0-beta1** | Generated chrome on by default, ribbon, layout editor, per-file-type layouts, the transformer and audio panes, `--run-workflow`, workflows in the tray flyout. | Workflows running for real, without the graph editor. |
| **5.0.0-beta2 → 5.0.0** | The Nodify workflow editor. | — |

4.18 is the release you asked about: one whose job is to battle test changes before v5. It is a recommendation, not a requirement — see [Why a 4.18](#why-a-418).

## What is on `dev` today and has never shipped

147 commits since the `v4.16.0` tag. A beta is due on the strength of these alone.

**Features:** Code mode (#645, AvalonEdit); Markdown images, HTML, footnotes, math, interactive task lists and native Mermaid, with a full-screen Mermaid viewer; Find on Screen in the fullscreen grab; window capture through Windows.Graphics.Capture in Window Select mode; print-style page ranges for multi-page grabs; Quick Simple Lookup auto save, source filter and popup style; the searchable tray flyout; smooth scrolling and touch panning in the Edit Text Window.

**Foundation, invisible if it works:**

| Phase | What changed underneath | What would break if it is wrong |
|---|---|---|
| F1 | A DI container built lazily; `Singleton<T>` routed through it; `AppRuntimeState` | First run, settings migration, packaged vs. unpackaged startup, tray, hotkeys, toast activation, `--windowless` |
| F3 | Image files load through GDI+ instead of a WPF `BitmapImage`; engine dispatch moved to Core.Windows | Rotated photos, unusual image formats, Tesseract, barcodes |
| F4 | Grabbed text reaches the clipboard through a WinRT `IClipboardService` with its own threading | Copy after a grab, from every grab window, with a clipboard manager running |
| F5 | Global hotkeys go through `HotkeyActionRouter`; one app-wide clipboard monitor with self-write suppression | Every hotkey; the Edit Text Window's clipboard watcher |
| E1 | All text and selection access goes through `EditorSession` | Any transform, Find and Replace, grabs into an open window, in every mode |
| A1 | The action registry | Nothing — it is inert |

## 4.17.0-beta1 — ship what is on `dev`

No new phase work. A hardening pass, then a GitHub pre-release, as 4.14 and 4.16 had.

**Before tagging:**

1. **MSIX package build in Visual Studio**, x64 and ARM64. Two packages have been added since 4.16.0 and neither has been through a package build: `AvalonEdit` 6.3.1.120 and `Microsoft.Extensions.DependencyInjection`.
2. **Third-party notices:** add AvalonEdit (MIT) to `BUILT-WITH.md` and the Licenses window. Neither mentions it today.
3. **Bump the version** to 4.17.0 in `Text-Grab.csproj` and `Package.appxmanifest`.
4. **Run the release sign-off** (`UiTests/Invoke-ReleaseSignoff.ps1`) against the packaged and unpackaged builds.
5. **F1 startup matrix by hand:** packaged and unpackaged × first run and upgrade from 4.16.0 with customized settings × normal launch, tray-only launch, hotkey launch, toast activation, `--windowless`, `text-grab://`.
6. **Look at #705 and #692 first.** #705 (the app stays running after a fullscreen grab) was reported against 4.16.0, and F1 and F4 both changed code on that path. Know whether `dev` fixes it, leaves it or worsens it before testers report it again.
7. **Code mode — fix or list as known:** the calc pane does not scroll with the code editor; Use Margins does nothing; no spell check; a shorter right-click menu; touch panning unverified. The first is fixed by the calc extraction in beta2; say so in the notes rather than patching it twice.

**Tell testers in the release notes:**

- Code files (`.cs`, `.json`, `.xml`, `.py`, …) now open in Code mode. They opened as Raw Text before.
- **Going back to 4.16.0 can drop history entries saved in Code mode.** History stores the editor mode by name, and 4.16.0 does not know `Code`. Its item-by-item recovery should drop only those entries, but that has not been tested: test it before the beta, and put the result in the notes.

**Exit:** two weeks in testers' hands with no open regression in startup, clipboard, hotkeys or OCR of image files.

## 4.17.0-beta2 — panes

The plan as first written could not ship a pane before converting every command (E2), extracting every cluster (E3) and regenerating the whole window from JSON (E4). That is most of v5. Three changes make a 4.17 with panes possible:

1. **E3's `calc` run no longer waits for E2.** The calc cluster needs the session and nothing from the command conversion. Its toggle stays a click handler until E2 reaches it.
2. **E4 is split.** [E4a](E4-Layout-Engine.md) is the pane host: the layout model, the store, and a renderer for the *dock tree only*. The menu bar, bottom bar and status bar stay hand-written XAML. E4b is the rest — chrome generated from the definition — and moves to 4.18.
3. **New panes depend on E4a**, not on all of E4.

**Work, in order.** Each is one branch and one pull request, as before.

| # | Phase and run | Depends on | Model | Notes |
|---|---|---|---|---|
| 1 | E3 `calc` | E1 | Opus | Defines `IEditorPane`. Add a scroll offset to `IEditorSurface` for the text **and code** surfaces, which fixes calc scrolling in Code mode. |
| 2 | E4a | #1; D1, D5 | Opus | Classic = editor + calc pane, rendered from `classic.json`. Parity is limited to the editor area and the calc pane. |
| 3 | E5 `recent` | #2; D4 | Sonnet | Closes #592. |
| 4 | E5 `mermaid` | #2; D4 | Sonnet | The building blocks are all on `dev`. |
| 5 | E5 `markdown-preview` | #2; D4 | Sonnet | Source side in Code mode with Markdown highlighting. |
| 6 | E6 `picker` | #2 | Sonnet | Layouts menu, Panes toggles, "Save current layout as…". |
| 7 | E5 `folder-tree` | #2; D4 | Sonnet | **Stretch.** Cut it rather than hold the beta. |

**Decisions you need to confirm before #2 starts** (set them to `Confirmed` in the README):

- **D1** — own split tree, no drag-to-dock.
- **D5** — layout per window, with a per-file-type default and a global default.
- **D4** — CommunityToolkit.Mvvm for new view models (needed from #3).

**Rules for this beta:**

- A user who never opens the Layouts or Panes menu sees the 4.16 window. A pane that is not in the layout is not constructed, and neither are its services.
- The layout a history item was saved with is stored as an **optional string**, not an enum, so an older build ignores it. Do not repeat the Code mode downgrade problem.
- Keep the old calc XAML path behind a setting for this one release, as E4 already planned, so a tester with a broken calc pane has a way back.
- Layout files are user-editable input: an unknown pane ID or a malformed file opens Classic and says why.
- No E2 work merges to `dev` between beta1 and 4.17.0. E2 touches every menu handler; keeping it out means a regression in this window is a pane regression.

**Not in 4.17:** generated menus, ribbon, layout editor, per-file-type defaults (unless `defaults` falls out cheaply), transformer pane, audio pane, anything workflow.

**Exit:** two weeks with no open regression in the calc pane (width and open state restored from history and settings), window position restore, or any of the three other editor modes.

## 4.17.0

beta2 plus fixes. Delete nothing yet: the old calc path stays until 4.18. Submit to the Store and publish the GitHub release together. Budget for a 4.17.1 — both recent minor releases needed one.

## Why a 4.18

Two remaining changes can lose something a user built, and neither adds anything they can see:

- **E2** rewrites how every saved bottom-bar button and post-grab action is resolved. A wrong row in the migration table silently drops a button.
- **E4b** replaces 220 hand-written menu items with a generated menu. Access keys, shortcuts, tab order and automation IDs all have to match.

Shipped inside 5.0, a regression in either is one of many things that changed at once, in the release that gets the most new users. Shipped alone in 4.18, it is the only thing that changed. That is the whole argument.

**4.18 contents:**

| Phase | State in 4.18 |
|---|---|
| E2, all runs | On. Legacy button fields keep being written, so going back to 4.17 works. |
| E3, remaining clusters (`transcription`, `localai`, `files`, `markdown`, `spreadsheet`, `code`) | On. No visible change. |
| E4b | Behind a setting, **off by default** in the beta, with the UI Automation parity test as the gate. Turn it on by default in 4.18.0 only if the beta is clean; otherwise it stays opt-in and becomes the default in 5.0-beta1. |
| W1 | Merged and inert. Nothing calls the engine. |
| E5 `folder-tree` | If it missed 4.17. |

W2 is deliberately not here. `--run-workflow` and the protocol entry are an attack surface and should arrive with the release notes, settings and import warnings that explain them, which is 5.0.

**If you would rather not do a 4.18:** fold E2 and E3 into a 4.17.x patch train behind no flag (they are behavior-preserving), and make E4b the first thing in 5.0-beta1, default off. The cost is a longer 5.0 beta.

## 5.0

| Beta | Phases |
|---|---|
| **5.0.0-beta1** | E4b on by default (delete the old XAML path one release after that); E6 `defaults`, `ribbon`, `editor`; E5 `transformer` and `audio`; W2 (`--run-workflow`, `text-grab://run-workflow?id=`, workflows in the tray flyout) |
| **5.0.0-beta2** | W3, the Nodify graph editor, with the Workflow layout |
| **5.0.0** | Fixes; `Singleton.cs` deleted; README section on the project layout |

Decisions due before 5.0-beta1: **D2** (ribbon), **D3** (range highlighting — the recommendation changed with Code mode; see the README), **D6** (which control is the document of record).

## Where this leaves each phase

| Phase | Status today | Ships in |
|---|---|---|
| F1–F5, A1, E1, Code mode | Done, on `dev` | 4.17.0-beta1 |
| F6 | Shelved | When a consumer needs it; `recent` works without it |
| E3 `calc` | Not started | 4.17.0-beta2 |
| E4a | Not started | 4.17.0-beta2 |
| E5 `recent`, `mermaid`, `markdown-preview` | Not started | 4.17.0-beta2 |
| E6 `picker` | Not started | 4.17.0-beta2 |
| E5 `folder-tree` | Not started | 4.17.0-beta2 (stretch) or 4.18 |
| E2 | Not started | 4.18 |
| E3, other clusters | Not started | 4.18 |
| E4b | Not started | 4.18 (opt-in), default in 5.0 at the latest |
| W1 | Not started | 4.18 (inert) |
| E6 `defaults`, `ribbon`, `editor` | Not started | 5.0.0-beta1 |
| E5 `transformer`, `audio` | Not started | 5.0.0-beta1 |
| W2 | Not started | 5.0.0-beta1 |
| W3 | Not started | 5.0.0-beta2 |

## Every release, every time

- MSIX package build in Visual Studio, x64 and ARM64. The CLI cannot build it.
- All three test executables, run as executables (`dotnet test` has reported zero tests here).
- `UiTests/Invoke-ReleaseSignoff.ps1`.
- Upgrade test from the previous **stable** with customized settings: bottom bar, post-grab actions, hotkeys, patterns, grab templates, history.
- Downgrade test to the previous stable: the app starts and settings load. State anything that is lost in the release notes.
- Betas are GitHub pre-releases. Stable goes to the Store and GitHub together.
