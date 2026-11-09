# A1 — Action registry

| | |
|---|---|
| Track | Shared foundation (workflows and Edit Text Window both consume it) |
| Size / risk | Medium / Low to ship (inert), **high leverage** (everything later depends on its shape) |
| Recommended model | Opus |
| Depends on | F1 |
| Unblocks | W1, E2 |
| Branch | `v5/a1-action-registry` |

Read [README.md](README.md) first.

## Goal

One place in Core that describes every action Text Grab can perform on text, as data. This phase builds the registry and fills it with the pure text transforms. It changes no UI.

## Why

The same idea exists today in four unconnected shapes:

- 17 static `RoutedCommand`s and about 200 click handlers in `Text-Grab/Views/EditTextWindow.xaml.cs`.
- `Text-Grab/Models/ButtonInfo.cs` (about 100 static entries) whose `Command` and `ClickEvent` strings are bound by reflection onto `EditTextWindow` methods in `Text-Grab/Utilities/CustomBottomBarUtilities.cs`.
- `Text-Grab/Utilities/PostGrabActionManager.cs`, a second dispatcher over `ButtonInfo`.
- `Text-Grab/Utilities/TrayActions.cs` plus the hotkey router.

After v5, a transform defined once is a menu item, a ribbon button, a bottom-bar button, a post-grab action, a workflow node and a stage in the transformer pane.

## Current state (verify)

- Pure transforms already in `Text-Grab.Core/Utilities/StringMethods.cs` (1,269 lines): `TrimEachLine`, `MakeStringSingleLine`, `RemoveDuplicateLines`, `ShuffleLines`, `CorrectCommonGuidErrors`, `ToCamel`, `ReplaceReservedCharacters`, `UnstackStrings`, and more. Read the whole file and inventory it.
- Also pure and in Core: `PatternExecutor`, `RecognizerExecutor`, `TextSearchUtilities`, `ColumnSplitUtilities`, `CalculationService`.
- Still in the window: `ToggleCase` (~line 5696, mutates the window's `CaseStatusOfToggle`), `SplitOnSelectionCmdExecuted` and `SplitAfterSelectionCmdExecuted` (~lines 5652–5695).
- `GrabTemplateExecutor.ApplyTextOnlyTemplate` is app-side but is a pure function of `(GrabTemplate, string)`.

## Design

Namespace `Text_Grab.Core.Actions` in `Text-Grab.Core`:

```csharp
public enum ActionKind { TextTransform, EditorCommand, AppCommand }
public enum ActionCategory { Lines, Case, Cleanup, Extract, Search, Table, Calculate, Ai, Capture, File, Window, Other }

public sealed record ActionDescriptor(
    string Id,                 // "textgrab.text.trimLines" — stable forever, never renamed
    string DisplayName,
    string IconKey,            // a plain string; the app maps it to a Wpf.Ui SymbolRegular
    ActionCategory Category,
    ActionKind Kind,
    Type? ConfigType = null,   // a record describing user-settable parameters, or null
    string? DefaultGesture = null,
    bool RequiresCopilotPlus = false);

public interface ITextTransform
{
    ActionDescriptor Descriptor { get; }
    ValueTask<string> ApplyAsync(string input, JsonElement? config, CancellationToken ct);
}

public interface IActionRegistry
{
    IReadOnlyCollection<ActionDescriptor> All { get; }
    bool TryGet(string id, out ActionDescriptor descriptor);
    ITextTransform? GetTransform(string id);
    void Register(ITextTransform transform);
    void Register(ActionDescriptor descriptor);   // for kinds whose implementation lives elsewhere
}
```

- A `DelegateTextTransform` helper wraps a `Func<string, string>` so a pure `StringMethods` function registers in one line.
- ID scheme: `textgrab.<area>.<verb>`, lower camel case after the second dot. IDs are persisted in users' layouts, workflows and button settings. Once shipped they are never changed; add a test that pins the full ID list so a rename fails the build.
- `IEditorCommand` is declared in E2, because it needs `IEditorSession` from E1. The registry accepts its descriptor now and its implementation later.
- Registration: a static `CoreActions.RegisterAll(IActionRegistry)` in Core. The app's composition root creates the registry singleton and calls it. Core.Windows adds its own `RegisterAll` for Windows-backed transforms.

## Steps

1. **Inventory.** Produce a table of every candidate: source method, proposed ID, display name (copy the exact menu text from `EditTextWindow.xaml`), category, whether it needs config. Save it as an appendix at the bottom of this file. Mark anything that needs selection, caret or window state as "E2, not A1".
2. Registry types + `ActionRegistry` implementation + tests in `Tests.Core` (register, duplicate ID throws, lookup, pinned ID list).
3. Register the pure `StringMethods` transforms through `DelegateTextTransform`. One test per transform asserting it calls through to the same result as the direct method.
4. **Lift `ToggleCase`.** Characterization tests first, driving the existing window behavior through its current tests or a new one. Then extract a pure `(string text, CurrentCase state, CultureInfo culture) → (string result, CurrentCase next)` into Core. The window keeps `CaseStatusOfToggle` and calls the pure function.
5. **Lift split-on-selection / split-after-selection** to pure `(string fullText, string selection) → string` in Core, the same way. These two need a selection, so they are *not* registered as `ITextTransform`; they are helpers E2 will wrap.
6. Move `ApplyTextOnlyTemplate` to Core if it moves without dragging settings or WPF with it; register a parameterized "apply template" transform whose config carries the template ID only if a headless way to load the template exists. If not, note it and move on.
7. Register transforms with config for: unstack (column count), pattern extraction (pattern ID or regex), recognizer extraction.
8. In Core.Windows, register descriptors (and transforms where the service is headless) for translate, summarize, rewrite and text-to-table with `RequiresCopilotPlus = true`. Read `WinAiTranslator`, `WinAiMeetingNotes`, `WinAiLanguageModel` to see what is callable without a window.
9. Register the registry in `AppServices`. Nothing in the UI uses it yet.

## Out of scope

- Any change to menus, the bottom bar, `ButtonInfo`, post-grab actions or hotkeys (E2).
- `IEditorCommand` and `IEditorSession`.
- Workflow steps (W1).

## Verification

All three test executables pass. `TierBoundaryTests` passes. The existing `EditTextWindow*Tests` pass unchanged — proof the two lifts did not change behavior.

## Done when

- The registry resolves from DI and lists every pure transform with a pinned, tested ID.
- `ToggleCase` and both split functions are pure functions in Core that the window calls.
- The inventory appendix exists and marks what E2 must convert.

## Traps

- `IconKey` must stay a string. Do not reference `Wpf.Ui` from Core; `TierBoundaryTests` will fail.
- Some `StringMethods` functions are culture-sensitive. `ITextTransform.ApplyAsync` has no culture parameter: decide once (current culture, as today) and write it in the interface's XML doc.
- Do not "improve" a transform while wrapping it. If one looks buggy, note it in the report.
- Display names are user-facing strings users already know. Copy them; do not rewrite them.
