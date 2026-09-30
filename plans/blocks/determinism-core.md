# Block: Deterministic Core

**Status:** `built` (code) / verification partial · **Layer:** `Assets/Scripts/Core/` · **Used by:** every format, every video

> Built 2026-09-27. Files and the per-file roles are listed in `Assets/Scripts/README.md`.
> Self-check: **Simulation Lobby ▸ Verify Deterministic Core** (needs no scene, no physics).
> The frame-rate and resolution criteria below stay open until `esc-001` gives us a real scene.

The foundation that makes a run reproducible from a seed. Build once, change rarely — a change here
can silently invalidate every previously published video's re-renderability.

## Why

A good run must be re-renderable months later at higher quality, and seed-scanning (picking good
runs from many) is impossible without it. Determinism is an architectural property here, not a
feature — see `KnowledgeBase/architecture.md`.

## Parameterized

Nothing. This block is identical for every video. Configs and seeds are inputs at runtime.

## Steps

1. **`SeededRandom`** — wraps `System.Random`. Expose `NextFloat(min,max)`, `NextInt(min,max)`,
   `NextDirection2D()`. Constructed with an `int` seed. The *only* legal randomness in simulation code.
2. **`ISimulation`** — the contract every format implements:
   `Initialize(int seed, SimulationConfig config)`, `Tick(float fixedDelta)`, `bool IsComplete`,
   `RunResult Result`. This is what lets the capture harness run formats it has never heard of.
3. **`SimulationConfig`** — base `ScriptableObject`. Holds only what's universal: max duration,
   fixed timestep override. Formats derive their own.
4. **`SimulationRunner`** — `MonoBehaviour` driving an `ISimulation` from `FixedUpdate`. Sets
   `Time.fixedDeltaTime` from config on init. Stops at `IsComplete` or max duration. Must behave
   identically in-editor and headless.
5. **`RunResult` / `RunRecorder`** — outcome data: completion reason, duration in ticks, final state,
   and a timestamped event log. Serializable to JSON so seed scans produce a readable table.
6. **Assembly definition** `SimulationLobby.Core` — **zero references**. If it needs to reference
   anything, the design is wrong.

## Acceptance criteria

- [x] Same seed run twice → byte-identical `RunResult`. *(`DeterminismVerifier`, covered by the self-check.)*
- [ ] Same seed at 30fps and 120fps display rate → identical result (proves no frame-rate coupling).
      *Open — structurally guaranteed (ticks only, no `deltaTime`) but unproven without a scene.*
- [ ] Same seed headless at 540p and windowed at 1080p → identical result. *Open until `esc-001`.*
- [x] `grep` for `UnityEngine.Random` in `Core/`, `Shared/`, `Simulations/` returns nothing.
- [x] `SimulationLobby.Core.asmdef` has an empty references list.
- [x] Extra: different seeds produce different runs; `RunResult` survives a JSON round-trip;
      the runner times out at exactly `MaxTicks`.

## Deviations from the plan as written

- **`ISimulation` gained `FormatSlug`** (a `string`). The batch harness needs to label rows and name
  output files without a per-format branch, which is the whole point of the interface.
- **`RunRecorder` is created by the format, not injected by the runner.** Keeps
  `Initialize(seed, config)` exactly as specified; the format returns `recorder.Result` from `Result`.
- **`RunResult` records no wall-clock time and no frame count** — either would differ between two
  runs of the same seed and make byte-identical comparison impossible. Ticks and simulated seconds only.
- **Kept `System.Random`** per `CLAUDE.md`. Worth knowing: its implementation is not contractually
  stable across .NET/Unity versions, so an engine upgrade could shift old seeds even though our code
  is deterministic. `unityVersion` on `RunResult` is what makes that diagnosable. If it ever bites,
  the fix is a hand-rolled PRNG inside `SeededRandom` — one file, no callers change.
- **`autoReferenced: false`** on the assembly: loose scripts outside an `.asmdef` cannot silently
  use `Core`. Every consumer must declare the reference, which is what makes the layering enforceable.

## Gotchas

- `Time.deltaTime` anywhere in simulation code breaks determinism. `FixedUpdate` only.
- Static mutable state defeats reproducibility — thread the `SeededRandom` instance explicitly.
- Physics settings (gravity, solver iterations, default contact offset) are part of determinism.
  They live in `ProjectSettings` — changing them invalidates old seeds. Note any change in a retro.
- Unity's physics is deterministic *for a fixed binary and settings*, not across Unity versions.
  An engine upgrade may change old runs. Record the Unity version in `RunResult`.

## Used by

Everything. Treat as `stable` the moment `esc-001` publishes.
