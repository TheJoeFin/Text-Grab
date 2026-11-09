# W1 — Workflow engine

| | |
|---|---|
| Track | Workflows |
| Size / risk | Large / Medium (design risk only; the code is inert until W2 wires it in) |
| Recommended model | Opus |
| Depends on | A1, F3, F4, F5 |
| Unblocks | W2, the transformer pane in E5 |
| Branch | `v5/w1-workflow-engine` |

Read [README.md](README.md) first.

## Goal

A workflow model, a step registry, and a headless runner in `Text-Grab.Core`, with the built-in steps needed to run: *trigger → transforms → search with branching → outputs*. No editor UI, no app entry point.

## Rule that governs this phase

**Write the two end-to-end tests in "Steps" 1 first, and build only what they need.** This is the phase most likely to be over-engineered. Every type below is a starting point; cut anything the tests and the v1 step list do not use.

## Design

Namespace `Text_Grab.Core.Workflows`.

**Model** — records, `System.Text.Json`, versioned:

```csharp
public sealed record WorkflowDefinition(string SchemaVersion, Guid Id, string Name,
    IReadOnlyList<WorkflowNode> Nodes, IReadOnlyList<WorkflowConnection> Connections);
public sealed record WorkflowNode(Guid Id, string StepTypeId, double X, double Y, JsonElement? Config);
public sealed record WorkflowConnection(Guid SourceNodeId, string SourcePortId, Guid TargetNodeId, string TargetPortId);
```

`X` / `Y` are plain doubles so the future graph editor can map them to node positions without WPF types in Core.

**Steps:**

```csharp
public enum PortDataType { Signal, Text, TextList, Table, FilePath, Image, Audio }
public enum PortKind { Input, Output, ExclusiveBranch, Iterating }
public enum StepCategory { Trigger, Transform, Output }

public sealed record PortDescriptor(string Id, string DisplayName, PortDataType DataType, PortKind Kind);
public sealed record StepDescriptor(string TypeId, string DisplayName, StepCategory Category,
    IReadOnlyList<PortDescriptor> Inputs, IReadOnlyList<PortDescriptor> Outputs, Type? ConfigType);

public interface IWorkflowStep
{
    StepDescriptor Descriptor { get; }
    IAsyncEnumerable<PortEmission> ExecuteAsync(StepContext context, CancellationToken ct);
}
public sealed record PortEmission(string PortId, object? Payload);
```

`StepContext` carries the input payload, the node's config, the run's `IServiceProvider` scope and a logger callback.

**Branching.** `IAsyncEnumerable<PortEmission>` covers every case with one mechanism. The search step has four outputs:

| Port | Kind | Emits |
|---|---|---|
| `noMatch` | ExclusiveBranch | once, when nothing matched |
| `firstMatch` | ExclusiveBranch | once, the first match |
| `eachMatch` | Iterating | once per match; the runner runs the downstream subgraph for each |
| `allMatches` | Output | once, a `TextList` |

`ExclusiveBranch` and `Iterating` are declarations for validation and for the future editor. The runner treats every emission the same way: enqueue it for whatever is connected to that port.

**Transforms come from the action registry.** One generic `TextTransformStep` adapts any `ITextTransform` to an `IWorkflowStep` (one `Text` input, one `Text` output, config passed through). The step type ID is the action ID. Hand-written steps are only for triggers, outputs and non-text ports.

**Runner** (`WorkflowRunner`):

- Validate before running: every `StepTypeId` is registered; every connection's ports exist; connected port data types are compatible; no cycle.
- One `IServiceScope` per run. A work queue of `(targetNodeId, targetPortId, payload)`.
- **v1 limits, enforced by validation and documented on the class:** each input port has at most one incoming connection; a node has one data input; fan-out is allowed; there are no join nodes.
- One linked `CancellationTokenSource` per run. A step that throws fails its branch, not the run; the error is captured per node.
- Returns `WorkflowRunResult` (status, per-node results with duration and error, started/finished).
- Guard rails: a maximum emission count per run and a maximum run duration, both configurable, so an `eachMatch` over a huge input cannot hang the app.

**Store** — `IWorkflowStore` in Core (`ListAsync`, `GetAsync(Guid)`, `SaveAsync`, `DeleteAsync`); `FileWorkflowStore` in Core.Windows writing `workflows/<id>.json` through the same storage approach `FileUtilities` uses for packaged and unpackaged installs.

**Trigger coordination** — `WorkflowTriggerCoordinator` (Core): given enabled workflows and the `ITriggerSource`s from F5, starts a run when a trigger fires. The app constructs it in W2; here it is built and tested with fake sources.

## v1 built-in steps

| Category | Steps |
|---|---|
| Trigger | manual, clipboard-changed, folder-file, hotkey |
| Transform | every `ITextTransform` in the registry (automatic), OCR-file (`IOcrService`), search |
| Output | clipboard (`IClipboardService`), save-file (`ITextFileWriter`), HTTP POST (`IHttpPostService`), toast (`IToastService`), speak (`TtsService`) |

Steps that need Windows services are declared wherever their dependency's *interface* lives. Since all four output interfaces are in Core, the steps can be too; only their registrations differ.

Not in v1 — list as follow-ups: OCR-screen-region, apply grab template, transcribe audio, translate / summarize / rewrite, text ↔ table, open in Edit Text Window (needs E4), after-grab trigger.

## Steps

1. **Tests first**, in `Tests.Core`, with fakes for every service:
   - *Linear:* manual trigger → trim lines → remove duplicate lines → fake clipboard. Assert the clipboard text.
   - *Branching:* manual trigger with fixed text → search for a GUID pattern, `eachMatch` → fake file writer. Assert one write per match. Second case with no match: assert the `noMatch` branch ran and the writer did not.
2. Model records + JSON round-trip tests, including an unknown `SchemaVersion` (reject with a clear error) and an unknown step type (load succeeds, validation fails).
3. Step abstractions, step registry, `TextTransformStep` adapter.
4. Validation, with one test per rule.
5. `WorkflowRunner` until both tests from step 1 pass. Then tests for cancellation, a throwing step, the emission cap.
6. Built-in trigger and output steps, each with a test against a fake service.
7. OCR-file step with a `Tests.Core.Windows` test against a fixture image.
8. `IWorkflowStore` + `FileWorkflowStore` + temp-folder tests.
9. `WorkflowTriggerCoordinator` + tests with fake trigger sources, including: a workflow whose clipboard output must not retrigger its own clipboard trigger.
10. Register the registry, runner and store in `AppServices`. Nothing calls them yet.

## Out of scope

- Any UI, CLI argument or protocol handler (W2).
- Join nodes, loops, variables, sub-workflows.
- The Nodify editor (W3).

## Verification

All three test executables pass. `TierBoundaryTests` passes. The app's behavior is unchanged — nothing invokes the engine.

## Done when

Both step-1 tests pass through the real runner, a workflow JSON file round-trips through the store, and every v1 step has a test.

## Traps

- Payloads are `object?`. Validate the runtime type against the port's `PortDataType` at the edge of each step and fail that node with a clear message; do not let an `InvalidCastException` surface.
- `IAsyncEnumerable` steps run lazily. If the runner enqueues without awaiting enumeration, cancellation and error capture go wrong. Enumerate each step to completion inside a `try`.
- Do not let a step resolve services from the root provider; use the run scope.
- Step type IDs and port IDs are persisted in users' files. Pin them with a test, as A1 does for action IDs.
