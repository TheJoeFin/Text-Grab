# W3 — Nodify graph editor

| | |
|---|---|
| Track | Workflows |
| Size / risk | Large / Medium |
| Recommended model | Opus |
| Depends on | W2, E4 |
| Required decisions | D4 (view-model library) |
| Branch | `v5/w3-graph-editor` |

Read [README.md](README.md) first.

## Goal

A visual node editor for `WorkflowDefinition`, built on [Nodify](https://github.com/miroiu/nodify), hosted as a pane in the Edit Text Window shell. It edits the same JSON the runner executes; it adds no runtime concepts of its own.

## Principle

The editor is a **projection** of the Core model. View models wrap `WorkflowNode` and `WorkflowConnection` one to one. Saving produces a `WorkflowDefinition`; loading consumes one. If something cannot be expressed in the model, it does not exist in the editor.

## Current state (verify)

- W1 model: node `X` / `Y` are plain doubles, intended to map to Nodify's node location.
- W1 step registry exposes `StepDescriptor`s with typed ports — the palette and the connector shapes come from these.
- E4 provides `IEditorPane`, the pane registry and layouts.
- Nodify is not yet referenced. Check its current version, license (MIT at time of writing) and .NET target before adding it, and read its documentation and samples rather than relying on memory of its API.

## Design

- Package: `Nodify` in the app project only.
- View models (app, namespace `Text_Grab.Workflows.Editor`): `GraphEditorViewModel`, `NodeViewModel`, `ConnectorViewModel`, `ConnectionViewModel`, `PendingConnectionViewModel`. Built with the D4 library.
- **Palette:** all registered `StepDescriptor`s grouped by `StepCategory`, with search. Transforms come straight from the action registry, so the palette grows whenever a transform is registered.
- **Node config:** a property panel generated from the step's `ConfigType` (string, number, bool, enum, file path, pattern picker). Start with a small reflection-based form generator; do not hand-build a form per step.
- **Connection rules** are the runner's validation rules, applied live: incompatible port types cannot be connected; an input that already has a connection replaces it; cycles are refused.
- **Validation surface:** run W1's validator on every change and badge the offending nodes.
- **Run from the editor:** a Run button executes the current definition with the manual trigger; each node shows its last result, duration and error from `WorkflowRunResult`.
- **Pane registration:** `IEditorPane` with ID `textgrab.pane.workflowGraph`, plus a built-in "Workflow" layout that shows the graph with the run log beneath it.
- Theme: follow the app's light/dark theme. Read how `App.SetTheme` and existing controls pick up WPF-UI resources and map Nodify's brushes to them.

## Steps

1. Spike (throwaway branch or folder, not merged): render a hard-coded three-node definition in Nodify inside a plain window, both themes. Confirm the package builds for x86, x64 and ARM64.
2. View models + mapping to and from `WorkflowDefinition`, with round-trip tests in `Tests` (load fixture → view models → save → equal definition, including positions).
3. Palette, add/remove node, drag, connect, disconnect, delete, with undo/redo for each.
4. Connection rules and live validation badges.
5. Config panel generator; cover every `ConfigType` used by the v1 steps.
6. Run-from-editor and per-node result display.
7. Pane registration and the built-in layout; "Open in graph editor" from the transformer pane (E5) and from the workflow list.
8. Keyboard access: every operation reachable without a mouse; `AutomationId`s on palette, canvas and config panel.

## Out of scope

- New runtime features (joins, loops, variables). If the editor makes their absence painful, write that up; do not add them here.
- A marketplace or sharing service. Export and import are files.

## Verification

- All three test executables pass.
- Manual: build the W2 sample workflow from scratch in the editor, save, and confirm the JSON runs identically from `--run-workflow`.
- Manual: open a workflow authored as JSON by hand; nothing is lost on save.
- Manual: both themes, 100% and 200% DPI.
- Report the new package for the MSIX check.

## Done when

Every v1 workflow that can be written as JSON can be created, edited, validated and run in the editor, and a file saved by the editor is byte-stable across a load/save cycle.

## Traps

- Do not put WPF types into `Text-Grab.Core`. Positions are doubles; colors and shapes are app-side lookups keyed by `PortDataType`.
- Nodify virtualizes and re-templates nodes; keep all state in view models, none in controls.
- An editor that silently "fixes" an invalid file on load destroys the user's work. Load invalid files as-is and show the validation errors.
