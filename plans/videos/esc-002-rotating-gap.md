# esc-002 — The Exit Keeps Moving

**Format:** `esc` · **Length:** ~130s (7 rounds) · **Aspect:** 9:16 1080x1920 · **Status:** `published` (2026-09-29)
**Brief:** `KnowledgeBase/formats/containment-escape.md`
**Scene:** `Assets/Scenes/Escape_Circle_2D_RotatingGap.unity` (duplicate of `Escape_Circle_2D.unity`)
**Config:** `Assets/Settings/Configs/EscapeConfig_002.asset` · **Seed:** none — `randomizeSeedOnStart`
is on for this scene's `SimulationRunner`, by explicit request. **This breaks reproducibility**: the
run is drawn fresh from `Guid.NewGuid()` on every Play and is never written down, so a good take
cannot be re-rendered later, and the documented seed-scan workflow (scan 1–1000, score, pick) does
not apply here. See "Seed selection" below for what replaces it.

## Premise

Same growing-ball-in-a-circle as esc-001, but **the gap itself rotates around the boundary** at a
steady angular speed. The ball isn't just racing its own growth anymore — it has to be near the gap
*when the gap is there*. Escape becomes a timing problem, not just a size threshold.

Title: *"The Exit Keeps Moving"* / *"Can It Catch the Gap?"* — pick after seed-scan, whichever run
supports it best.

## Why this one next

esc-001 proved the pipeline (seed → scene → render) but only tested one escalation axis (growth).
This is the cheapest possible variation that changes what the viewer is watching for — from "is it
too big yet" to "is it lined up yet" — without touching a single shared block. Good second data
point for whether `esc` retains, and it's the differentiator KB flagged as untested (variant table
in `containment-escape.md` doesn't list this one — worth adding once it ships).

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md)
- [ ] **REUSE** [collision-safety](../blocks/collision-safety.md)
- [ ] **REUSE** [escalation-rule](../blocks/escalation-rule.md) — `BallSize`, multiplicative, same as esc-001
- [ ] **REUSE** [bounce-audio-sequencer](../blocks/bounce-audio-sequencer.md) — if it landed for esc-001; if not, this plan inherits the gap, not the debt — don't build it here
- [ ] **REUSE** [onscreen-counter](../blocks/onscreen-counter.md)
- [ ] ~~REUSE [seed-scan-harness](../blocks/seed-scan-harness.md)~~ — **not used**: this run randomizes
      its seed on every Play (`SimulationRunner.randomizeSeedOnStart`), so there is no seed to scan.
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md)

No new shared block, no new component class, no new scene-builder — see "Implementation" below.
The rotating gap is two config fields plus ~10 lines in the existing `esc` sim, not a reusable
primitive yet. If `surv` later wants a moving weak point in its shrinking arena, promote the pattern
then (per the isolation rule: promote on the second real use, not preemptively).

## Video-specific work

### Scene — literal duplicate of `Escape_Circle_2D.unity`

**Built as a plain file duplicate**, config reference repointed at `EscapeConfig_002`, nothing else
changed. No new scene-builder code, no new component, no new format — this variation is entirely
config + a few lines in the existing sim, which is the point: a second `esc` video should cost
almost nothing beyond authoring a config and picking a seed.

The boundary stays a plain (non-Rigidbody2D) `CircularBoundary2D` and is rotated by directly
rotating its `Transform` — no kinematic `Rigidbody2D` needed. At 20°/s and this arena's radius the
wall's edge speed is under 2 units/s, negligible next to the ball's 6–13 u/s, so treating the wall as
effectively stationary per-frame (Physics2D still re-syncs the moved colliders each `FixedUpdate`) is
an acceptable approximation rather than a corner cut worth the extra complexity of a kinematic body.

### Implementation — extended existing files, no new classes

- `SimulationRunner.cs` (Core): new `randomizeSeedOnStart` bool, off by default everywhere else in
  the project. When on, `Awake()` draws `Guid.NewGuid().GetHashCode()` instead of reading the `seed`
  field, and nothing persists it. This scene has it **on** — every other scene/format is unaffected.
- `EscapeConfig.cs`: two new fields, `gapAngularSpeed` (deg/s, 0 = esc-001's static gap) and
  `gapDirectionSeeded` (bool).
- `CircularBoundary2D.cs`: gap math (`IsWithinGap`, `GapDirection`) now reads
  `EffectiveGapCenterDegrees = gapCenterDegrees + transform.eulerAngles.z`, so rotating the transform
  moves the *logical* gap along with the physical segments (which rotate for free as children).
  Zero rotation → identical to esc-001.
- `EscapeSimulation.cs`: draws the rotation sign from the seeded RNG in `BeginAttempt` (after size,
  speed, direction — draw order still fixed and still skipped entirely when `gapAngularSpeed == 0`,
  so esc-001 configs draw exactly as before), resets `_boundary.transform.rotation` to identity at
  the start of each attempt, and applies `transform.Rotate` once per `Tick` — simulation state, ticks
  in `FixedUpdate` only, same as growth.
- **Does not replace `EscalationRule`.** Ball growth still escalates on bounce, same as esc-001. Two
  things move (ball size, gap position) but only one is a *trigger-driven escalation* — the rotation
  is continuous and independent, so this isn't the "combining two escalations" trap the KB warns
  about (that's about stacking two trigger-driven axes, e.g. grow + speed).

### Tuning targets

| Knob | Start at | Note |
|---|---|---|
| Gap angular speed | 15–25°/s | Full lap in 14–24s — needs to be slow enough to read as "the gap moved," not spinning-blur fast |
| Rounds | 7 | Same series structure as esc-001 |
| Growth | ×1.035/bounce | Unchanged from esc-001 — keep one variable new per video |
| Gap width | same as esc-001 (~2.5× initial ball diameter) | Don't retune two things at once |
| Direction | seeded, held for the full round | Reversing mid-round reads as unfair, not tense |

### Look

Mark the gap with a distinct color/glow so its position is legible as it moves — this is the entire
new visual read the format needs. Everything else (trail, color-cycle, dark background) unchanged
from esc-001.

## Acceptance criteria

- [ ] All block acceptance criteria still hold (this reuses, doesn't modify, shared blocks).
- [ ] With `randomizeSeedOnStart` temporarily off and a fixed seed, same seed twice → identical
      rotation direction/speed **and** identical outcome (a one-time check that the rotation math
      itself is still deterministic, even though production runs don't rely on that property here).
- [ ] Manual verification against tunneling across many live Plays — no 1000-run automated scan is
      possible without a seed, so watch for `[esc] TUNNELING` in the console across enough runs to be
      confident, including against the moving boundary segment specifically.
- [ ] Gap position is readable at 480px wide at every point in its rotation, not just at 12 o'clock.
- [ ] Chosen take has the ball miss at least one "gap pass" narrowly before catching one.

## Take selection — replaces seed scanning for this video

Because the seed is drawn fresh and unrecorded on every Play, there is nothing to scan offline.
Instead: **record every live Play as a candidate**, watch it back, and only keep the take that
happens to satisfy the acceptance criteria above. Practically:

- Keep the Unity Recorder (or whatever capture is wired up) running across repeated Plays rather than
  starting it only once you see a promising run — you cannot go back and reproduce a good one.
- Once a take is chosen, note its console-logged seed anyway (see the `randomizeSeedOnStart` log) as
  a courtesy for the retro, but treat it as **not reproducible** even though a number exists — the
  boundary's rotation direction, the RNG draw order, or a future code change could make replaying it
  diverge, and nothing here has verified otherwise the way esc-001's committed seed has.

## Render + publish

- 1080x1920 @ 60, `PC_RPAsset`, audio baked in.
- Thumbnail: ball approaching the gap from the wrong side, arrow showing gap's motion.
- Pinned comment: ask viewers to guess whether it catches the gap in time.

## Retro

Write `KnowledgeBase/retros/YYYY-MM-DD-esc-the-exit-keeps-moving.md`, update `/PROJECTS.md`.
The open question this video answers: **does adding a timing element to `esc` help or hurt
retention compared to esc-001's pure-size threshold** — first real basis for picking the next
escalation variant (speed / shrink / layers / multiply from the KB's variant table).
