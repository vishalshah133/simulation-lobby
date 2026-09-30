# Technical Architecture

The summary version lives in `CLAUDE.md`. This is the reasoning behind it, and the details that
don't fit there.

## The problem being solved

This project isn't one product — it's a factory for many videos across several formats, running for
years. Two forces pull against each other:

- **Reusability.** Cameras, leaderboards, spawners, pooling, recording, seeded RNG, contender
  identity — rebuilding these per format would make each new format expensive, and the channel needs
  new formats to be cheap.
- **Isolation.** A tweak to make marble wars read better must not silently change how a race resolves.
  Six months from now, re-rendering an old video from its config and seed must still produce the
  same footage. Shared code that everything mutates freely makes that guarantee impossible.

The resolution: **share aggressively downward, isolate absolutely sideways.** Common machinery moves
*down* into shared layers where it's stable and tested. Formats sit side by side and never touch.

## Layers

### Core
Format-agnostic, dependency-free primitives:
- `SeededRandom` — the only legal randomness source in simulation code
- `ISimulation` — the lifecycle contract every format implements
- `SimulationRunner` — fixed-timestep driver, identical in-editor and headless
- `RunResult` / `RunRecorder` — outcome data (finish order, counts over time, event log)
- `SimulationConfig` — base ScriptableObject type

Core changes are the riskiest in the project: they affect determinism everywhere. Change it
deliberately, and re-verify a known seed still reproduces after.

### Shared
Reusable building blocks that formats compose:
- Contender identity (name, color, index) and visual variation
- Object pooling and spawners
- Arena primitives: walls, funnels, shrink controllers, hazards
- Collision response components (bounce, convert, eliminate)

Nothing here knows which format it's in. A shrink controller doesn't care if it's `wars` or `surv`.

### Presentation / Capture
- **Presentation** — cameras (leader-follow, wide, punch-in), leaderboards, live counters,
  stacked population bars, result stings. Read-only over simulation state.
- **Capture** — aspect/resolution switching (9:16 vs 16:9), render settings, and the batch harness
  that runs N seeds headlessly and logs results for seed-scanning.

Capture is only possible *because* every format implements `ISimulation`. It runs formats it has
never heard of.

### Simulations/<Format>
One folder, one `.asmdef`, one format. Its own config type, its own rules, its own scene(s).

## Why assembly definitions

Rule 2 ("formats never reference each other") is unenforceable by good intentions alone — at 2am
before an upload, the fast fix is always to reach into the other format's code. An `.asmdef` per
layer and per format turns that into a compile error. The architecture defends itself.

Reference directions to configure:
- `Core` → nothing
- `Shared`, `Presentation`, `Capture` → `Core`
- `Simulations.<X>` → `Core`, `Shared`, `Presentation`
- `Simulations.Elimination` → the above, plus the formats it wraps
- Nothing → `Simulations.<X>` (except Elimination)

## Composition over inheritance

A tempting design is `Marble` → `RacingMarble` → `TeamRacingMarble`. Don't. Formats recombine traits
in ways a hierarchy can't anticipate, and a base class shared across formats reintroduces exactly
the coupling isolation is meant to prevent.

Instead: a contender is a GameObject with small components — `ContenderIdentity`, `TrailVisual`,
`ConvertOnContact`, `EliminateOnContact`, `TeamMembership`. A new format is a new prefab combination
plus its own rules class. This is also why cross-format experiments ("a race that becomes a war")
are cheap rather than an inheritance nightmare.

## Determinism as an architectural property

Determinism isn't a feature bolted on; it constrains the structure:

- Simulation runs on a fixed timestep through `SimulationRunner` — never on `Update`, never
  frame-rate dependent.
- Randomness comes only from the run's `SeededRandom`, threaded through explicitly. Any `static`
  mutable state or `UnityEngine.Random` call breaks it.
- Presentation is read-only, so rendering at 1080p or scanning headless at 540p produce identical
  outcomes. Seed-scanning would be worthless otherwise.

Regression check when touching `Core` or `Shared`: run a known seed on two formats, confirm the
finish order matches what's recorded in the config's committed result.

## Scene strategy

One scene per format *variant* (e.g. `MarbleRace_Spiral_2D`, `MarbleRace_Mega_3D`), not per video.
Individual videos are config assets + seeds pointed at an existing scene. If a new video needs a
scene change, it's a new variant, not an edit to the existing one — old videos must stay renderable.

## The deletion test

Could you delete `Assets/Scripts/Simulations/Wars/` entirely and have everything else compile?
If yes, isolation holds. If no, something leaked downward and needs promoting into `Shared` or
severing. Run this test mentally whenever adding a cross-cutting feature.

## Open questions

- Is `Presentation` better as one assembly or split per concern (camera / UI / VFX)? Start unified,
  split if it starts pulling format-specific types.
- Should `RunRecorder` output be committed alongside configs as the determinism regression baseline?
  Leaning yes — it makes "did I break an old video" a diff instead of a memory.
