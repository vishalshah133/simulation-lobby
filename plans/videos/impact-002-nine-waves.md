# impact-002 — "Can 300 blocks survive 9 waves?"

> **Superseded 2026-09-30 by [surv-001](surv-001-wall-vs-ball.md). Never published.** Nine
> escalating waves at a wall is survival, not `impact`, so the code moved to `Simulations/Survival`.
> Two design changes came with it: no reform between hits, and an open-ended guess-the-hits hook.
> The scene and config named below no longer exist under these names (`Survival_WallVsBall_3D`,
> `WallSurvivalConfig_001`). Kept as history only.

**Format:** `impact` · **Aspect:** 9:16 @ 1080x1920 · **Target length:** 75-95s
**Scene:** `Assets/Scenes/Impact_BallVsBlocks_3D.unity` · **Config:** `ImpactConfig_001.asset` · **Seed:** TBD

Supersedes [impact-001](impact-001-ball-vs-blocks.md), which is built and unpublishable: a single
launch settles in 3-4 seconds. Same scene, same physics, rebuilt run structure.

## Hook

> Can 300 blocks survive 9 waves?

Stated on the HUD as a question, scored as **WALL vs BALL** — survivors against blocks destroyed.

## The arc

| Beat | Seconds (sim) | What the viewer sees |
|---|---|---|
| Opening hold | 2.5 | The intact wall, lit, still. No ball in frame yet. |
| Wave hold ×9 | 1.6 each | The ball placed and motionless, riser building, "WAVE N INCOMING". |
| Flight ×9 | ~2-8 each | The hit, slow-motion on first contact, knocks and shake. |
| Result ×9 | 2.2 each | Damage legible, cascade of chimes as knocked-out blocks vanish, "-N BLOCKS". |
| Final hold | 4.0 | "N SURVIVED" or "TOTAL WIPEOUT". |

Worst case ~110s simulated; typical ~75s, plus whatever slow-motion adds on screen. Run the scene
builder and read the printed worst-case figure rather than trusting this table after a config change.

## Blocks used

- [determinism-core](../blocks/determinism-core.md) — seeded RNG, fixed tick, recorder.
- [collision-safety](../blocks/collision-safety.md) — continuous detection on ball and blocks.
- [onscreen-counter](../blocks/onscreen-counter.md) — now built as the shared
  `Presentation/Editor/ScoreboardCanvasBuilder`, promoted out of `EscapeSceneBuilder` because this
  video needed the identical canvas.
- [bounce-audio-sequencer](../blocks/bounce-audio-sequencer.md) — the chime ladder is the same
  pentatonic ladder, walked by eliminations instead of bounces.

## What is new here (and belongs to the channel, not this video)

- `Presentation/Cameras/SlowMotionDirector` — time-scale holds, safe for determinism.
- `Presentation/Cameras/ImpactCameraRig` — push-in across a run, decaying shake on impact.
- `ProceduralToneBank.CreateRiser` / `CreateAmbientBed` / `CreateRumbleLoop` — anticipation, room
  tone, and a scatter rumble driven by how much of the field is moving.
- `ScoreboardCanvasBuilder` — shared scoreboard canvas.

All four are format-agnostic. `surv` and `wars` should use them rather than rebuilding them.

## Tuning dials, in the order to reach for them

1. **Video too short** → `waveCount`, then `waveResultHoldSeconds`.
2. **Sags in the middle** → `aimJitterFraction` up (each wave hits somewhere new), or
   `massGrowthPerWave` down so the wall lasts longer and the ending stays in doubt.
3. **Ending never in doubt** → lower `massGrowthPerWave` / `radiusGrowthPerWave`.
4. **Scoreboard disagrees with the screen** (scattered blocks counted as survivors) →
   `eliminationDistanceBlocks` down toward 1.
5. **Cascade too quick to enjoy** → `eliminationsPerTick` down (it is the ASMR payoff).
6. **Hits feel weightless** → `slowMotionSeconds` up, `SlowMotionDirector.slowScale` down.

## Acceptance

- [ ] Renders 75-95s at 1080x1920.
- [ ] Wall is never behind HUD text at either aspect.
- [ ] Every wave lands somewhere visibly different from the previous one.
- [ ] Same seed re-run produces the same survivor count — **requires `unseededAim` off**, which the
      baseline turns on. With it on the take is one-off, exactly like esc-002's
      `randomizeSeedOnStart`: note the config, accept the footage cannot be re-rendered, and do not
      run a seed scan against it.
- [ ] Outcome is not obvious before wave 7.

## Retro

_To fill after publishing._
