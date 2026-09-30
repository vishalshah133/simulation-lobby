# esc-001 — Will the Ball Escape?

**Format:** `esc` · **Length:** ~130s (7 rounds) · **Aspect:** 9:16 1080x1920 · **Status:** `building`
**Brief:** `KnowledgeBase/formats/containment-escape.md`

## Premise

A ball bounces inside a circle. **Every bounce it gets bigger.** Eventually it won't fit — does it
escape through the gap, or get stuck?

Title: *"Will the Ball Escape?"* — the premise is the title is the hook.

## Why this one first

Cheapest possible build (one rigidbody, one boundary, one rule), and it forces `Core` + `Shared` into
existence under real conditions. Every heavier format inherits that foundation. If the layering is
wrong, this is the cheapest place to find out.

## Blocks used

Build order. `BUILD` = first implementation, this video pays the cost. `REUSE` = already exists.

- [x] **BUILD** [determinism-core](../blocks/determinism-core.md) — seeded RNG, `ISimulation`, runner
- [x] **BUILD** [collision-safety](../blocks/collision-safety.md) — components built, **scan not yet run**
- [x] **BUILD** [escalation-rule](../blocks/escalation-rule.md) — target `BallSize`, multiplicative
- [ ] **BUILD** [bounce-audio-sequencer](../blocks/bounce-audio-sequencer.md) — the retention engine
- [ ] **BUILD** [onscreen-counter](../blocks/onscreen-counter.md) — bounce count, top-center
- [ ] **BUILD** [seed-scan-harness](../blocks/seed-scan-harness.md) — with the `esc` scoring function
- [ ] **BUILD** [capture-shorts](../blocks/capture-shorts.md) — needs `com.unity.recorder` added first

Everything after esc-001 checks most of these off as REUSE. This plan is long *because* it's first.

## Video-specific work

### Scene — `Assets/Scenes/Escape_Circle_2D.unity`

2D, URP. Circular boundary built from short segments or a polygon collider — **not** a thin ring
(see collision-safety). One gap in the circle is the escape route; gap width is the core tuning knob.

Orthographic camera framing the circle in 9:16 with headroom for the counter.

### `EscapeSimulation : ISimulation` — `Assets/Scripts/Simulations/Escape/`

Rules, and nothing that belongs in a block:
- Ball spawns at center with a seeded random direction, fixed initial speed.
- Perfectly elastic: restitution 1, zero damping, zero gravity. Energy must never bleed off.
- Each bounce → escalation applies → ball grows.
- **Complete when:** ball's center passes outside the boundary through the gap (escape), or the ball
  is too large to pass the gap *and* growth has clamped (trapped), or max duration hits.
- `RunResult` records: outcome, bounce count, final size, near-miss count.

### `EscapeConfig : SimulationConfig`

Gap width · initial speed · initial size · growth rate · max size clamp · max duration · melody asset.

### Tuning targets

Revised 2026-09-27: this is now a **7-round series** with a `WALL vs BALL` score, not a single attempt.

| Knob | Start at | Note |
|---|---|---|
| Rounds | 7 | More rounds = more resolution moments. `attemptCount` in the config |
| Growth | ×1.035 / bounce | ~29 bounces to the trapped threshold, ~18s per round |
| Initial size | 1/13–1/11 of circle diameter | Drawn once per video, held across rounds so the series is fair |
| Gap width | ~2.5× initial ball diameter | The main tension dial |
| Speed | 8–10, constant within a round | Do **not** also escalate speed on the first video |
| Round cap | 24s | A safety net, not a target — a round that times out reads as anticlimax |
| Total length | ~130s | **Shorts allow 3 minutes now**; the old "dies past 55s" note was wrong |

**Round ends** when the ball escapes, or when its diameter exceeds the gap by 10% (escape has become
impossible — ending here removes the dead footage of a hopeless ball still bouncing).

### Look

Color-cycles on bounce (genre convention, and it makes speed legible). Trail on. Dark background,
high-saturation ball. The gap is visually marked so the viewer knows where escape can happen — the
tension only works if they can see the target.

## Acceptance criteria

- [ ] All block acceptance criteria met.
- [ ] Same seed twice → identical outcome and bounce count.
- [ ] 1000-seed scan: **zero** tunneling escapes (ball never passes through solid wall).
- [ ] Bounce count == notes played, every run.
- [ ] Chosen run is 25–50s with at least two near-misses before resolution.
- [ ] Counter readable at 480px wide; HUD clear of Shorts UI on a real phone.
- [ ] Melody recognizable on phone speakers at the climax.

## Seed selection

Scan 1–1000. `esc` scoring = near-miss count, with duration in the 25–50s band. Render top 10 at
preview quality, pick by eye.

**A ball that escapes in 8 seconds is not a video. One that never escapes is a cheat unless the
title's question honestly answers "no."**

## Render + publish

- 1080x1920 @ 60, `PC_RPAsset`, audio baked in.
- Thumbnail: ball mid-bounce near the gap, huge "WILL IT ESCAPE?".
- Pinned comment asking for a guess before watching to the end.
- Public-domain or original melody — record which one here: `______`

## Retro

After publishing, write `KnowledgeBase/retros/YYYY-MM-DD-esc-will-the-ball-escape.md` and update
`/PROJECTS.md` with views. The open question this video answers: **does this format still perform in
2026, or has it cooled?** Two more `esc` Shorts before deciding to commit to a series.
