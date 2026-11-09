# W2 — Workflow end-to-end and entry points

| | |
|---|---|
| Track | Workflows |
| Size / risk | Small–medium / Low, plus a security review |
| Recommended model | Sonnet |
| Depends on | W1 |
| Unblocks | W3; first user-visible workflow feature |
| Branch | `v5/w2-workflow-entry` |

Read [README.md](README.md) first.

## Goal

Make workflows runnable by a user: from the command line, from the `text-grab://` protocol, from triggers while the app runs in the background, and from the tray flyout. Prove the whole chain with a real-engine integration test.

## Current state (verify)

- `Text-Grab/App.xaml.cs` parses `--automation-profile`, `--windowless`, `--grabframe` (~lines 287–300) and dispatches `text-grab://` commands (~lines 327–480).
- `Text-Grab.Core/Utilities/ProtocolUtilities.cs` is the pure URI parser; its header comment lists the supported commands. `Text-Grab/Utilities/ProtocolHandlerUtilities.cs` validates `path=` against an allow-list of folders.
- `text-grab://grab-text?path=` already OCRs a file to the clipboard with no window — the closest existing precedent.
- The tray flyout (`TrayFlyoutController`, `TrayFlyoutWindow`, `TrayFlyoutItemProvider`, `TrayFlyoutItem`) lists history items with search.
- `Text-Grab.Core/Utilities/Singleton.cs` is the F1 shim.

## Steps

1. **Real integration test** in `Tests.Core.Windows`: a workflow JSON fixture — OCR-file on the existing `GUIDs.png` fixture → fix-GUIDs transform → search with a GUID pattern (`eachMatch`) → save-file to a temp folder — run through the real `WorkflowRunner`, real `IOcrService`, real `TextFileWriter`. Assert the files.
2. **CLI:** `--run-workflow <id-or-path>`. A GUID is looked up in the store; anything else is treated as a file path. Runs windowless, with no tray icon, then exits with code 0 on success, 1 on a failed run, 2 on "not found or invalid". Reuse how `--windowless` suppresses UI today.
3. **Protocol:** `text-grab://run-workflow?id=<guid>`. Add the command to `ProtocolUtilities` (and its header comment) with a `Tests.Core` test. **`id=` only. Do not accept `path=`.** See Security.
4. **Triggers at runtime:** construct `WorkflowTriggerCoordinator` when the app is running in the background, feeding it the F5 sources and the enabled workflows from the store. Add a per-workflow `Enabled` flag (in the store's metadata, not in `Settings.settings`). Workflows are **disabled by default** after import.
5. **Tray flyout:** list saved workflows as a new item kind; activating one runs it. Follow how history items are merged and searched in `TrayFlyoutItemProvider`.
6. **Run feedback:** on completion show a toast only on failure, or always if the workflow says so. Keep the last `WorkflowRunResult` per workflow in memory for the future run-log pane.
7. **Import:** an "Import workflow…" command that reads a JSON file, shows a summary (see Security), and saves it to the store disabled.
8. **Cleanup:** if no `Singleton<T>` call sites remain, delete `Singleton.cs`; otherwise leave it and report the remaining count. Add a short "Project layout" and "Workflows" section to the repo `README.md`.
9. Ship one sample workflow file under `docs/v5/samples/` and reference it from the README section.

## Security

A workflow can read the clipboard or OCR the screen and send the result to a network address. Treat a workflow file like a script.

- The protocol handler runs workflows **by ID from the user's own store only**. A web page or another app must not be able to make Text Grab execute a workflow it supplies. This is the same reasoning as the folder allow-list in `ProtocolHandlerUtilities`.
- Import shows, before saving: the triggers, every HTTP destination host, every file path written, and whether it reads the clipboard or the screen.
- Imported workflows start disabled. Enabling a workflow with a clipboard or folder trigger is an explicit user action.
- The HTTP POST step's destination is visible wherever the workflow is listed and in the run result.
- `--run-workflow <path>` from the command line is allowed (the user typed it), but it does not persist or enable anything.

List in your report every place external input reaches the runner, and how each is constrained.

## Out of scope

- A workflow editor of any kind. Workflows are authored as JSON until the transformer pane (E5) and the graph editor (W3).
- A settings page for workflows beyond enable/disable/delete/import. If there is no natural home, put these in the tray flyout's item context menu and note it.

## Verification

- All three test executables pass.
- Manual, unpackaged Debug build: `Text-Grab.exe --run-workflow docs/v5/samples/<sample>.json` writes the expected output with no window and returns exit code 0 (`echo $?` / `$LASTEXITCODE`); a bad path returns 2.
- Manual: import the sample, enable it, trigger it, see the result; run it from the tray flyout.
- Manual: `text-grab://run-workflow?id=<unknown guid>` does nothing harmful and does not crash.
- Report that packaged (MSIX) protocol activation still needs a check in Visual Studio.

## Done when

A user can import, enable and run a workflow without a debugger, and no entry point accepts an untrusted workflow definition.

## Traps

- A windowless run must still have whatever message pump the clipboard service needs (F4). Test `--run-workflow` with a clipboard-output workflow specifically.
- A second Text Grab instance launched by `--run-workflow` while one is already in the tray: check how single-instance and `NumberOfRunningInstances` behave today and do not break background launch.
- Exit codes from a WPF `WinExe` are only visible to callers that wait for the process. Document the PowerShell `Start-Process -Wait -PassThru` form in the README section.
