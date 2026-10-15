# conv-001: Ellipse Focus

**Format:** `conv` · **Length:** ~24s (3 rounds) · **Aspect:** 9:16 · **Status:** `built` 2026-10-14 (compiles; not yet played)
**Brief:** `KnowledgeBase/formats/math-convergence.md`
**Scene:** `Convergence_EllipseFocus_2D` (**Simulation Lobby ▸ 2D Formats ▸ Math Convergence (conv) ▸ Ellipse Focus (conv-001)**)
**Config:** `Assets/Settings/Configs/EllipseFocusConfig_001.asset` · **Seed:** random per Play, logged at start (cosmetic only)

## Premise

**One question, two visible dots, one visible twist, then the explanation.** 120 balls fire from a red dot inside an ellipse. How many hit the blue dot? All of them, at once. Slide red a little and only a handful do. Slide it back and all hit again. Only that spot works: it is called a focus.

**Revision 2026-10-14:** the first cut (4 rounds: circle, focus, stretch, a 0.19 offset; an unmarked target; a MEET vs MISS score) was found incomprehensible on first Play: no visible target, an invisible twist, and score bookkeeping to follow. Rebuilt as 3 rounds on one ellipse with both dots visible from frame one.

**Experiment lane** (`channel-strategy.md` → *Shorts mix*). **What it tests:** a new *tension shape*
(prediction, not escalation), not just a new mechanic. Judge at day 7 against the ~1-1.7K band. Watch the
**like-rate** as well: the reference's 1.2-1.9% is the thing this shape claims to buy.

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md): `ISimulation`, `SimulationRunner`, seeded RNG, recorder
- [ ] **REUSE** [onscreen-counter](../blocks/onscreen-counter.md): `ScoreboardCanvasBuilder` / `VersusScoreboardHud` (question, caption/result, counter; versus row hidden via `SetScoreVisible(false)`)
- [ ] **REUSE** `CaptionHud` (round title + result line), `CountFormat`
- [ ] **REUSE** `BackdropStyler` + `BackdropThemeLibrary` (dark theme from seed), `AmbientDriftField`
- [ ] **REUSE** `ProceduralToneBank` (`CreateTone`, `CreateGlassClink`, `CreateAmbientBed`), presentation-side only
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md) (still `planned`; record the same way as the other Shorts)
- [x] **BUILD** analytic billiard kernel: point particles, exact ellipse reflection, in `Simulations/Convergence/Kernel/`.
  It goes to `Shared` only if another *format* needs it. Other `conv` topics reuse it in place

## Architecture (as built)

The genre is isolated in its own assembly, `Simulations/Convergence`. Nothing else references it.
The reuse seam is inside the genre:

- `IConvergenceKernel` (`Kernel/`) is the motion model: start, target, expected meet time, step, outline.
  It's **UnityEngine-free** and double precision, so it's exact, and it can be checked headless outside Unity.
- `ConvergenceConfig` (abstract) is the round shell: counts, meet radius and threshold, pacing, text. A topic
  subclasses it, holds its own per-round geometry and builds a kernel per round.
- `ConvergenceSimulation` (phases, verdict, scoring, the wall-hit ring buffer) and `View/ConvergencePresenter`
  (swarm, trails, outline morph, flash, audio, HUD) only ever see the base config and the kernel interface.
- **The next `conv` topic** (cycloid, pendulum wave, parabola) **is a new kernel plus a config subclass.** The
  simulation, presenter and builder pattern stay unchanged.

First topic: `EllipseBilliardKernel` + `EllipseFocusConfig`. A headless check (outside Assets, 120 bodies,
dt 1/120, r 0.25, peak within +-0.06s of E) is under *Modelled* below; two runs are bit-identical.

**Promotion candidates, deliberately left in the format** (promote on a second user):
`ConvergenceFlash` (bloom + ring burst), the round-robin `AudioSource` voice pool (duplicates
`BounceMelodyPlayer`'s), and the LineRenderer-from-points updater. Promoted now:
`VersusScoreboardHud.ShowNeutral(text)`, which puts a caption in the result slot with no win/lose colour.

## Rounds

One ellipse throughout (half-width 3.75, half-height 5, speed 5, so E = 2a/v = 2.0s). **Red** is the start (upper
focus), **blue** is the target (lower focus). Both are solid discs with a white ring, visible from the first frame.
The twist is the red dot visibly sliding 0.7 sideways, with a faint ghost ring left at the focus. The ghost shows only while red is off the focus, and fades out before launch.

| # | Red dot | Transition / Hold caption | Run | Result line | Answer |
|---|---|---|---|---|---|
| 1 | On the focus | Hold 1.5s: `WATCH BLUE` | 2.08s. Fan out, bounce, all land on blue. Flash + chord. Freezes just after the hit | `ALL 120. AT THE SAME TIME.` | **MEET** |
| 2 | Slides +0.7 in x (1.5s, smoothstep); a ghost ring stays on the focus and fades out before launch | `NOW MOVE RED A LITTLE...` | 7s, trails 1.5s. Nearly comes together, smears, counter stays low | `ONLY {n} HIT.` | **MISS** |
| 3 | Slides back to the start (the ghost marks the focus and vanishes as red lands on it) | `PUT IT BACK...` | 2.08s. Full hit again, flash | `ALL 120 AGAIN.` | **MEET** |

End card (3s): `ONLY THIS POINT WORKS. IT'S CALLED A FOCUS.` Total about 24s.

### Modelled 2026-10-14 (headless kernel, 120 balls, v=5, meet radius 0.25, peak within +-0.06s of E=2.0s)

- **R1 and R3 (on the focus):** 120/120.
- **R2 (red +0.7):** **10/120** (threshold 114).
- Probes: offset +0.6 gives 12, +0.8 gives 8. Any offset of 0.5 to 1.0 is a clear miss; 0.7 reads as "a little" on screen.
- Two runs are bit-identical.
- Earlier Python model (over a wider window) gave ~11 at 0.6 and ~8 at 0.8, consistent.

## Video-specific work

### Simulation / rules (`ConvergenceSimulation : ISimulation`, slug `conv`)

- State per particle in **doubles**: position and unit direction. Constant speed per round.
- `Tick(dt)`: advance distance `v·dt`. While the path hits the wall inside the step: solve ray-ellipse (quadratic
  on `x²/a² + y²/b² = 1`), move to the hit, reflect about the normal `(x/a², y/b²)`, and carry the remaining
  distance. Emit a wall-hit event (particle id, hit angle) for presentation.
- Launch angles are even (`2πi/N`) plus a seeded global offset under 1° (cosmetic).
- Exposes read-only `AtTargetCount` (balls within `meetRadius` of the round's target point), `RoundIndex`,
  `Phase` (hold / run / result) and `ScoreMeet` / `ScoreMiss`.
- A round's verdict is **declared by config, then checked at runtime**: at the expected return time
  (`2a/v`), `AtTargetCount ≥ meetThreshold` must agree with the config's expected answer. If not, log an error.
  This keeps a mis-tuned round from shipping with the wrong caption.
- Between rounds: clear the balls, morph the boundary over `transitionSeconds` while empty, then hold
  `preLaunchHoldSeconds` with the start point pulsing (the anticipation beat).
- `IsComplete` after the last round's hold plus the end card.

### Config: `ConvergenceConfig : SimulationConfig`, asset `EllipseFocusConfig_001`

| Knob | Value | Note |
|---|---|---|
| `ballCount` | 120 | Raise to 180 if the fan looks sparse at 480px |
| `speed` | 5 | 2.0s per focus-to-focus pass at a=5 |
| `meetRadius` | 0.25 | For the counter and the verdict |
| `meetFraction` | 0.95 | Threshold 114 |
| `verdictWindowSeconds` | 0.06 | Verdict = peak count within +-0.06s of E, so the freeze lands right after the hit |
| `rounds[]` | see table | Each round: caption, result template (`{n}`, `{total}`), expected, run and trail seconds, `halfWidth`, `halfHeight`, `startOffset` |
| `counterLabel` | `HIT BLUE` | Counter reads `HIT BLUE  n / 120` |
| `firstHoldSeconds` / `preLaunchHoldSeconds` | 1.5 / 0.6 | |
| `transitionSeconds` | 1.5 | The red dot's slide |
| `resultHoldSeconds` / `endCardSeconds` | 1.5 / 3 | |

The scene builder **deletes and recreates** the config asset on every build so these defaults always reach it
(the GUID changes; the scene is rebuilt in the same call). Tune values in the config *class*, not the asset.

### Look

- Dark theme from `BackdropThemeLibrary`. Ellipse outline as a glowing `LineRenderer` (~0.05 wide, near-white).
- Balls: small glowing discs (r≈0.07), **hue by launch angle**. One shared material, so batching holds up at 120-180.
- **Trails** around 0.4s long (`TrailRenderer` per ball, width ~0.03, fading). In round 4, lengthen to ~1.5s so
  the caustic builds up.
- Start point: a pulsing ring during the hold. Target point: none in rounds 1-3 (the surprise is *where*),
  then a flash ring on the meet.
- Meet flash: the convergence point blooms white, and a quick ring expands from it. Presentation reads `AtTargetCount`.

### Audio (presentation, reads events)

- **Wall hits:** a short tone with **pitch by hit angle** around the ellipse. From a focus the hits come in
  angle order, so each pass is a natural **glissando** sweeping up the boundary. Cap at 2 notes per frame
  (the `mve` lesson on noise walls).
- **Meet:** a bright `CreateGlassClink` plus a low tone together, on the frame `AtTargetCount` crosses the
  threshold. A full meeting should sound like a chord landing.
- **Miss** (round 4's first return): a detuned, dull version of the meet sound.
- Under everything, a quiet `CreateAmbientBed`.

### HUD

Reuses `VersusScoreboardHud`, with its versus row hidden by a new additive `SetScoreVisible(false)` (the builder binds
the row's root via `BindScoreRow`; other formats are untouched). No score, no round label.

- **Top:** the headline `THE MAGIC POINT` (title slot), and under it the question, `120 BALLS FROM RED. HOW MANY HIT BLUE?`.
- **Bottom:** the caption (neutral) through Transition, Hold and Running; then the result line (green on Meet, red on
  Miss) through Result; the end card at the end. Counter `HIT BLUE  n / 120` while running, holding the verdict count
  during Result. The counter never uses the HUD's red alert colour, so a full hit looks good.
- **Dots:** red (1, 0.25, 0.3) start, blue (0.25, 0.6, 1) target, 0.42 units across with a thin white ring, under the
  balls (sort 4/5 vs 10) and under the flash (20). The red dot pulses gently during Hold only. The ghost ring marks the focus,
  only while red is off it (`IdealStartPoint` vs `StartPoint`). It fades out across the hold and is gone by launch.
- The arena stays clear between the panels (all rounds share one ellipse, so the ortho size is about 10.2).

## Check on first Play

- [ ] Build: **Simulation Lobby ▸ 2D Formats ▸ Math Convergence (conv) ▸ Ellipse Focus (conv-001)**. The console summary lists 3 rounds,
  ~24s, no score, ortho size about 10.2.
- [ ] No `[conv]` verdict errors in the console.
- [ ] From the first frame: a red dot at the top focus and a blue dot at the bottom focus, caption `WATCH BLUE`.
  The pulse is gentle, and the dots read at 480px wide.
- [ ] R1: the fan leaves red and lands on blue together. Flash plus chord on the hit, the balls freeze still clumped,
  the counter reads `HIT BLUE  120 / 120` and the result `ALL 120. AT THE SAME TIME.`
- [ ] Transition: red visibly slides right, a ghost ring stays on the focus and fades out before launch, caption `NOW MOVE RED A LITTLE...`.
- [ ] R2: nearly comes together, then smears. The counter stays low (about 10), the trails draw the pattern, the result
  is red `ONLY 10 HIT.` It runs about 7s and does not drag.
- [ ] Transition: red slides back (the ghost marks the focus and vanishes as red lands on it) (`PUT IT BACK...`). R3 hits 120 again with the flash.
- [ ] End card reads `ONLY THIS POINT WORKS. IT'S CALLED A FOCUS.` The versus row and round label are absent.
- [ ] The dots sit under the balls (the fan leaves red) and the flash sits over everything.
- [ ] The wall hits sound like a rising sweep each pass, not noise. If too dense, lower `_maxNotesPerFrame` to 1.
- [ ] No trail streaks in from the previous round.
- [ ] While red is off the focus, the ghost carries a **"MAGIC POINT"** decal (`idealSpotLabel` on the config) that fades with it. It sits inside the outline and clear of red, or just outside the wall where the ellipse is too narrow. It never straddles the line. It reads at 480px wide

## Acceptance criteria

- [ ] All block acceptance criteria met
- [ ] Same seed twice → identical outcome (it's analytic, so this should hold trivially; verify anyway)
- [ ] Rounds 1 and 3 hit 120/120 at the expected return time. Round 2 stays under the threshold. The runtime verdict check never fires
- [ ] The smear in round 2 is legible at 480px wide, and a first-time viewer can say what the twist was
- [ ] The glissando is audible and not mush. The meet chord lands on the meet frame
- [ ] HUD clear of the ellipse and the platform UI on a real phone
- [ ] Length about 24s

## Seed

Random per Play and logged at start (house rule). The seed is cosmetic here: backdrop theme, palette start and
angle offset. No scanning.

## Publish

Title options, copying the question *shape*, not the reference's wording:
- *"120 Balls From Red. How Many Hit Blue?"*
- *"Fire From Here and They ALL Meet Over There 🎯"*
- *"Will They All Meet Again? (Ellipse)"*

Pinned comment: *"Why does it only work from that one point? 👇"* (it invites the explanation in the comments).
Hashtags: `#physics #maths #satisfying #shorts`. Record the logged seed here.

## Retro

The open question this video answers: **does a prediction-shaped Short reach our audience as well as the
escalation shapes, and does it earn a higher like-rate?** Compare at day 7 with the band and the channel's
0.5-1.7% like-rate.
