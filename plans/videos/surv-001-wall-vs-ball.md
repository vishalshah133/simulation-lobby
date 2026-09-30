# surv-001 — "How many hits to destroy 300 blocks?"

**Format:** `surv` · **Length:** 90-170s · **Aspect:** 9:16 @ 1080x1920 · **Status:** `building`
**Brief:** `KnowledgeBase/formats/survival-wall.md`
**Scene:** `Assets/Scenes/Survival_WallVsBall_3D.unity` · **Config:** `WallSurvivalConfig_001.asset` · **Seed:** TBD

Supersedes [impact-002](impact-002-nine-waves.md) (built, never published). Same wall, same ball,
same drama layer. The changes: the wall is never rebuilt between hits, the run is open-ended until
the wall is gone, and the question is a guess.

## Premise

> GUESS: how many HITS to destroy all 300 BLOCKS?

Scored as **WALL vs BALL**: blocks standing vs blocks destroyed. A live `HITS N` tally with no
total, because the total is the answer.

## The arc

| Beat | Seconds (sim) | What the viewer sees |
|---|---|---|
| Guessing window | 3.0 | The intact wall, the question, "LOCK IN YOUR GUESS". No ball yet. |
| Hit hold ×N | 1.0 each | Ball placed and still, riser building. "ONLY N LEFT" during the last stand. |
| Flight ×N | ~2-5 each | The hit, slow-mo on first contact, knocks and shake. Tally ticks up. |
| Result ×N | 0.9 + cascade | Chime cascade as knocked-out blocks vanish, "-N BLOCKS" (or "MISSED"). |
| Reveal | 2.2 slow-mo + 4.0 | Slow-mo the moment the last blocks go, then "DESTROYED IN N HITS". |

About 6-7s on screen per hit including slow-mo. Target is 12-22 hits. The builder prints the ball
at hits 1/10/20 and the worst-case length. Read it after any config change.

## Blocks used

- [x] **REUSE** [determinism-core](../blocks/determinism-core.md): first real 3D verification is
      this video's acceptance.
- [x] **REUSE** [collision-safety](../blocks/collision-safety.md): continuous detection, air gap,
      depenetration cap.
- [x] **REUSE** [onscreen-counter](../blocks/onscreen-counter.md): shared
      `ScoreboardCanvasBuilder`. Adds `VersusScoreboardHud.SetRoundLabel` for a round line with no
      total.
- [x] **REUSE** [bounce-audio-sequencer](../blocks/bounce-audio-sequencer.md): pentatonic chime
      ladder, now restarted every hit.
- [ ] **REUSE** [seed-scan-harness](../blocks/seed-scan-harness.md): still `planned`. Seed picking
      is manual until it exists.
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md): still `planned`.

Also reused as-is: `SlowMotionDirector`, `ImpactCameraRig`, `ImpactAudioPlayer`,
`RigidbodyFieldSettleTracker`, `CollisionDetector3D`.

## Video-specific work (done 2026-09-30)

- Code moved `Simulations/Impact` → `Simulations/Survival` as `WallSurvival*`. Asmdef renamed to
  `SimulationLobby.Simulations.Survival`, format slug `surv`.
- No reform: wall built once, elimination measured from each block's original slot.
- Densest-cluster aiming replaces centroid aiming. The centroid of a holed wall is often a hole.
- Open-ended run: ends at zero survivors; `maxHits` 25 is a safety cap only.
- A miss isn't a hit: `HitsLanded` counts shots with contact, `ShotIndex` drives escalation.
- Reveal slow-mo keyed off `WipeoutUnderway` (outcome), plus last-stand slow-mo at ≤12 blocks.
- **After first playback (2026-09-30):** wall was cut on the left of the 9:16 frame → builder now
  solves the camera framing at full push-in. Displaced blocks weren't counting as broken → threshold
  1.25 → 0.5 → 0.2 spacings, plus a 25° tilt test. Lone blocks left floating looked wrong → blocks with no
  standing neighbour now count as destroyed (`minNeighboursToStand` 1).
- `unseededAim` defaults **off**. The revealed number must be reproducible.

## Tuning dials, in the order to reach for them

1. **Too few hits / guess too easy** → `massGrowthPerHit` down, then `ballMass` down.
2. **Too many hits / sags in the middle** → `massGrowthPerHit` or `radiusGrowthPerHit` up.
3. **Late game drags on scattered blocks** → `radiusGrowthPerHit` up (a bigger ball sweeps
   several), or `lastStandBlocks` up so the drag at least gets slow-mo drama.
4. **Misses** → `aimJitterBallRadii` down.
5. **Scoreboard disagrees with the screen** → `eliminationDistanceBlocks` (0.2 after playback;
   1.25 let shoved blocks count as standing, 0.5 still felt loose) and `eliminationTiltDegrees` (25°) down. Too low and
   blocks nudged by a neighbour vanish without being visibly hit.
   **Stray survivors look like leftovers** → `minNeighboursToStand` 1 → 2.
6. **Cascade too quick to enjoy** → `eliminationsPerTick` down.
7. **Too long overall** → `hitResultHoldSeconds`, then `preLaunchHoldSeconds`.

## Acceptance

- [ ] Wall is visibly never rebuilt: holes persist from hit to hit.
- [ ] Wall falls in 12-22 hits on the chosen seed; renders 90-170s at 1080x1920.
- [ ] Same seed twice → **same hit count** and same survivors per hit (3D determinism check).
- [ ] No first-hit wipeout; no take reaching the `maxHits` cap.
- [ ] A miss shows "MISSED" and doesn't advance the tally.
- [ ] Wall never behind HUD text at either aspect.
- [ ] Reveal slow-mo fires on the hit that clears the last blocks.

## Seed selection

Until the scan harness exists, try seeds by hand and note hits per seed here. Prefer a seed where
the answer is *later than it looks*: big early holes, a stubborn last few blocks.

| Seed | Hits | Length | Note |
|---|---|---|---|

## Render + publish

Title: *"How Many Hits to Destroy 300 Blocks?"*. Thumbnail: intact wall, ball, big "?". Pinned
comment: "Guess before the end 👇". Audio: all procedural (`ProceduralToneBank`).

## Retro

_To fill after publishing._
