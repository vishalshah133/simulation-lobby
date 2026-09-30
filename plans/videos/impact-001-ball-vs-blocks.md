# impact-001 — Ball vs 200 Blocks

> **Superseded, never published.** One launch settled in 3-4s. Rebuilt as
> [impact-002](impact-002-nine-waves.md), which in turn became [surv-001](surv-001-wall-vs-ball.md).
> Why the single-event version failed: `KnowledgeBase/formats/impact.md`.

**Format:** `impact` · **Length:** ~20-30s · **Aspect:** 9:16 1080x1920 · **Status:** `building`
**Brief:** `KnowledgeBase/formats/impact.md`

## Premise

A single heavy, fast ball launches into a tightly packed wall of small blocks. One hit. Watch them
scatter. No rounds, no score, no escape condition — the entire video is the setup and the payoff.

Title: *"Ball vs 200 Blocks"* — name the impactor, name the scale, per the format's title convention.

## Why this one first

Cheapest possible `impact` build: plain rigidbody scatter, no destructible/soft-body physics, no new
shared block. Validates the format itself (does a single-collision-no-escalation video even retain
on this channel) before investing in anything fancier. Also the first format on the channel with
neither competitive nor threshold tension — a genuine third data point on what kinds of tension work
here at all.

## 3D, not 2D — first format to leave 2D

Matches what the reference channels (`Kawaken_3DCG`, `RenderZen`, `Oyen_3D`) actually do — depth and
scatter toward/past camera is a real part of why this category reads as dramatic. This is a real cost,
not a label change:

- `determinism-core`'s acceptance criteria have only been proven in 2D so far (`esc-001`/`esc-002`).
  Unity's 3D physics (PhysX) is a different solver from `Physics2D` — same fixed-timestep,
  no-`Time.deltaTime` discipline applies, but this is the first video that actually tests it.
  **Re-run `DeterminismVerifier` against this format specifically** before trusting a 3D seed the way
  `esc`'s 2D seeds are trusted.
- `collision-safety`'s guidance (continuous detection, thick colliders, velocity clamp) carries over
  conceptually, but 3D's `CollisionDetectionMode.ContinuousDynamic` and `Rigidbody`/`BoxCollider`/
  `SphereCollider` replace the 2D equivalents throughout.
- Camera becomes a real design surface for the first time — 2D formats use a fixed orthographic
  framing; this needs a perspective (or angled orthographic) camera placed to read depth clearly at
  9:16 without blocks flying directly at/away from camera looking like they've vanished.

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md) — verify its 3D-specific criteria
      (not yet exercised by any published video) rather than assuming they hold
- [ ] **PARTIAL REUSE** [collision-safety](../blocks/collision-safety.md) — same principles, ported to
      3D components (`Rigidbody`, `ContinuousDynamic`, 3D colliders) since nothing on the channel has
      done this yet
- [ ] **BUILD** [capture-shorts](../blocks/capture-shorts.md) — if still not landed by the time this
      is built, this plan pays that cost; otherwise reuse
- [ ] **BUILD** [onscreen-counter](../blocks/onscreen-counter.md) — "N BLOCKS SCATTERED", if it hasn't
      landed yet either

No `escalation-rule` — nothing escalates. No `bounce-audio-sequencer` requirement — a single impact
has no bounce sequence to score, though a single low "thud" sample on impact is worth trying.
No `seed-scan-harness` requirement in the blocking sense (this format doesn't need a 1000-seed
tunneling scan the way `esc` does — one heavy ball hitting a stationary field is much lower tunneling
risk), but reuse the harness anyway for picking the best-looking scatter if it exists by then.

## Video-specific work

### Scene — new: `Assets/Scenes/Impact_BallVsBlocks_3D.unity`

3D, URP, static camera — perspective (or a steep angled orthographic, test both) framed wide enough
to see the entire block field at rest and after scatter, and angled so depth reads clearly at 9:16.
Single shot, no cuts, matching the reference channels' convention. Blocks scattering toward/away from
camera should still read as motion, not just blocks shrinking/growing — frame test this before
committing to a final angle.

Block field: a grid of small `Rigidbody` + `BoxCollider` objects (start 10 × 20 × 1 = 200, a flat
wall to keep the first build simple — a full 3D volume is a later variant), packed edge-to-edge,
resting under light gravity or zero-gravity-until-impact (test both — gravity adds a natural settle
but also means blocks are already drifting before the hit if timing is loose).

Ball: one larger, heavier `Rigidbody` + `SphereCollider`, launched from off-screen at the block
field's centroid.

### `ImpactSimulation : ISimulation` — `Assets/Scripts/Simulations/Impact/`

New assembly definition (`SimulationLobby.Simulations.Impact`), isolated the same way `Escape` is —
references `Core` and `Shared` only.

Rules:
- Blocks spawn in a fixed grid; each gets a small seeded position jitter (from `SeededRandom`) so the
  packing doesn't look perfectly uniform, and a small seeded mass variance for the same reason.
- Ball spawns off-screen, launched toward the field centroid at a seeded speed within a config range.
- **Complete when:** every block's linear velocity magnitude drops below a settle threshold for N
  consecutive ticks, or `maxDurationSeconds` hits (safety cap — should never actually trigger if
  tuning is right).
- `RunResult` records: blocks displaced past a distance threshold (the "scatter count" the HUD shows),
  max scatter distance, settle tick.

### `ImpactConfig : SimulationConfig`

Grid rows/columns · block size/spacing · block mass · block position-jitter range · ball radius/mass ·
ball speed range · settle velocity threshold · settle-tick count · max duration (safety cap).

### Tuning targets

| Knob | Start at | Note |
|---|---|---|
| Grid | 10 × 20 (200 blocks) | Matches the title; raise only after confirming frame rate holds |
| Ball speed | 15-20 units/s, seeded | Fast enough to read as a genuine impact, not a nudge |
| Ball mass vs. block mass | ~50:1 | Heavy enough that individual blocks read as flung, not just tipped |
| Position jitter | ±5% of block spacing | Enough to break uniformity, not enough to leave visible gaps pre-impact |
| Settle threshold | velocity < 0.05 u/s for 15 ticks | Same shape as Unity's own rigidbody sleep heuristic |
| Max duration | 8s | Generous safety cap — actual runs should settle well under this |

### Look

High-contrast blocks (single saturated color, or a light gradient by row so the scatter pattern reads
clearly), dark background. Ball leaves a brief trail on its approach so its speed is legible before
impact. No color-cycling — unlike `esc`, there's no bounce sequence for it to track.

### Setup beat and audio — added after the first pass looked and sounded like a placeholder

The first build (scene, config, sim) was bare primitives on a flat black background with the ball
launching on tick zero and no sound at all — unwatchable. Fixed with:

- **`ImpactConfig.preLaunchHoldSeconds`** (default 1.2s): the field sits static and lit before the
  ball launches, giving the viewer a beat to register the scale of the field before the payoff. Also
  fixed a real bug this surfaced: the settle tracker was ticking from frame zero, so a field at rest
  *before* it had ever been hit satisfied the settle condition almost immediately — the run would
  complete before the ball arrived. Settle tracking now only starts once `ImpactSimulation.HasLaunched`
  is true.
- **Environment**: a floor plane (grounding, shadow catcher), a fill/rim light alongside the key light,
  linear fog for depth, and an `AmbientDriftField` dust backdrop (reused from `esc`, decorative only)
  for quiet motion during the hold beat. Camera was also on the wrong side of the field in the first
  draft (behind the wall from the ball's approach) — fixed to sit on the ball's own side, angled
  across the field so both the approach and the wall's face read at once.
- **Audio** — new `ImpactAudioPlayer` (`Presentation`), same "generated, so nothing to licence"
  discipline as `BounceMelodyPlayer`: a rising whoosh (`ProceduralToneBank.CreateWhoosh`) plays on
  launch, and a round-robin bank of knock sounds (`ProceduralToneBank.CreateThudBank`, pitch-jittered
  per hit) plays once per ball/block collision. Driven by a new `CollisionDetector3D` (`Shared`,
  the 3D counterpart to `BounceDetector2D`) on the ball, and a new `ImpactPresenter` that diffs
  `ImpactSimulation.ImpactCount` the same way `EscapePresenter` diffs `BounceCount` — never lets
  a rendered frame drop a knock the way it must never drop a bounce-note.

## Acceptance criteria

- [ ] Same seed twice → identical scatter (positions, settle tick) — the reproducibility bar every
      format holds, even though this format's stakes for it are lower than `esc`'s. **This is the
      first time that bar is checked against 3D physics** rather than assumed to carry over from 2D.
- [ ] No blocks tunnel through each other or off the visible frame edge in a way that reads as broken
      physics (soft check — this format doesn't need `esc`'s formal containment assertion since
      there's no boundary to escape).
- [ ] Scatter reads as physical (asymmetric, blocks separate cleanly) at 480px wide.
- [ ] Settle detection actually fires within `maxDurationSeconds` across a range of seeds — a run that
      hits the safety cap instead is a tuning bug, not acceptable output.
- [ ] Counter (if built by then) is readable and doesn't overlap the block field.

## Seed selection

Scan a modest range (50-100, this format has much less variance to search through than `esc`).
Score by scatter symmetry/spread — reward seeds where the scatter looks most dramatic without any
block leaving the frame entirely (which would look like a bug, not a feature).

## Render + publish

- 1080x1920 @ 60, `PC_RPAsset`.
- Thumbnail: the moment of impact, blocks mid-flight, huge number ("200 BLOCKS").
- Pinned comment: ask viewers to guess how far the ball will punch through.
- No melody requirement for this format — if a simple impact "thud" sound is added, note the source
  here (must be original/public-domain, same rule as `esc`).

## Retro

Write `KnowledgeBase/retros/YYYY-MM-DD-impact-ball-vs-blocks.md`, update `/PROJECTS.md`.
The open question this video answers: **does a single-event, no-escalation, no-competition video
retain at all on this channel** — the first data point for a third tension mechanism alongside
competitive and threshold-escalation. Also the first real test of whether `determinism-core` and
`collision-safety` actually hold in 3D as claimed, not just 2D — a second open question this video is
carrying that `esc-001` didn't have to. And settles whether `impact`'s much lower tunneling risk means
the full `collision-safety` block (1000-seed scan) is overkill for this format, or whether it's
needed regardless.
