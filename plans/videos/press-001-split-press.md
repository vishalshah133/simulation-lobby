# press-001: Split Press

**Format:** `press` · **Length:** ~60-75s (6 rounds) · **Aspect:** 9:16 · **Status:** `built` 2026-10-20 (both packages compile together; harness-checked over 20 seeds; not yet played)
**Brief:** `KnowledgeBase/formats/hydraulic-press.md`
**Scene:** `HydraulicPress_Split_2D` (**Simulation Lobby ▸ 2D Formats ▸ Hydraulic Press (press) ▸ Split Press (press-001)**, priority 191)
**Config:** `Assets/Settings/Configs/HydraulicPressConfig_001.asset` · **Seed:** random per Play, logged at start

## Premise

**"Every crushed ball splits in 2. How many balls before the press STOPS?"**, asked six times, with the press 2×
stronger each round. Every ball can hold 1 ton, so the press stops once enough balls share its weight. The count
goes up ~4× per round, not 2×. That gap is the twist.

**Experiment lane** (`channel-strategy.md` → *Shorts mix*). Reference: @satisfying2dsims, the fastest riser in the
2D genre. Its top three are all multiply + crusher with the count on screen: *"It crushed one ball into 15,939
pieces"* 1.9M, *"Every ball crushed splits into 3"* 1.7M, *"How many balls can a hydraulic press handle?"* 725K
(`reference-channels.md` → 2D pass). It also takes the *how many?* + answer-ladder shape from `clack` (2,879 at day 3).
Plain clone-on-bounce did badly elsewhere (Bouncy Physics 8.9K), so the press is the visible cause that does the
multiplying.

## Storyboard

| # | Beat | On screen | Bottom line (hold → run → result) | Ladder |
|---|---|---|---|---|
| 1 | Hook / teach | Chamber, one big ball on the floor, the press above it with `2 TONS` on its face. Question up from frame 0 | `GUESS?` → press slams down, the ball squashes and pops into 2, the press settles on both → `2 BALLS.` | `2 TONS → 2` |
| 2 | Escalate | Press rises, its label ticks to `4 TONS`, the chamber resets to one ball | `2× STRONGER...` / `GUESS?` → pops ripple down the pile → `n BALLS.` | `4 TONS → n` |
| 3 | Notice | `8 TONS` | `GUESS?` → `n BALLS. NOT DOUBLE...` | `8 → n` |
| 4 | Confirm | `16 TONS` | `GUESS?` → `n. ~4× EVERY TIME` | `16 → n` |
| 5 | Spectacle | `32 TONS`, the pile turns to sand-sized grains | `GUESS?` → `n BALLS!` | `32 → n` |
| 6 | Peak | `64 TONS` | `GUESS?` → `n BALLS!` (the biggest pop cascade) | `64 → n` |
| End | Reason | Final pile at rest under the press, ladder with the footnote | `EACH BALL HOLDS 1 TON. 2× THE FORCE = 2× THE BALLS ACROSS, AND THEY'RE HALF AS WIDE` | stays |

Top panel: title `CRUSHED BALLS SPLIT IN 2` (small), question `HOW MANY BALLS STOP THE PRESS?` (big, from frame 0).
*Changed 10-20 after the first look: the longer question was hard to parse.* Each round's bottom line names the press
(`A 2-TON PRESS. GUESS?`, `2× STRONGER: 4 TONS. GUESS?`, `8 TONS. GUESS?` …). The ladder runs in two columns either side of
the piston housing, which lets the chamber draw ~1.8× wider (~110 px/unit, was ~62). Sub-caption under the chamber: `EACH BALL HOLDS 1 TON`. Bottom panel: the line above, plus the live counter
`BALLS 128`.

**The ending line depends on the harness.** If the measured round-to-round ratio is within ~3-5× for rounds 3-6 on
every one of 20 seeds, keep the storyboard (`~4× EVERY TIME`, the 2×/half-as-wide line). If it isn't, drop the claim:
round 4 becomes `n BALLS.`, and the end line becomes `EACH BALL HOLDS 1 TON. MORE FORCE, SMALLER BALLS.`
**Never show a ratio the run doesn't produce** (a live counter never contradicts the result).

Cold-viewer check (`learnings.md` → *Story legibility*):
- [x] The question names things visible from the first frame: a ball, a press, its tonnage
- [x] Every cause happens on screen: each crushed ball visibly squashes before it pops, and the press only moves when balls give way
- [x] Every beat raises the stakes (a stronger press, a bigger number). If it drags, cut round 5 first, never 3 or 4
- [x] The only number shown is the answer (`BALLS n`). The ladder is the answer history, and the twist lives in it
- [x] The ending says *why* in one line

## Blocks used

- [x] **REUSE** [determinism-core](../blocks/determinism-core.md): `SimulationRunner`, `ISimulation`, `SimulationConfig`, `SeededRandom`
- [x] **REUSE** [capture-shorts](../blocks/capture-shorts.md)
- [x] **REUSE** `CaptionCanvasBuilder` / `CaptionHud`, `ResultLadderHud` (from `clack`), `BackdropThemeLibrary` / `BackdropStyler`,
  `ProceduralToneBank` (`CreateClick`, `CreateGlassClink`, `CreateImpactThud`, `CreateRumbleLoop`, `CreateWhoosh`)
- [ ] **BUILD** the `press` format: kernel, config, simulation, presenter, audio, scene builder

## Rules

- A 2D chamber (floor and two walls) with one ball at rest on the floor, x offset seeded. The press (a thick plate on
  a piston rod) lowers at `pressSpeed` until it rests on the pile.
- **Load model (the heart of the format).** While the press touches the pile, its weight `W` (tons) flows down
  through the contact graph: each ball passes everything it carries, plus its own weight (`ballWeightTons`, ~0), to the
  balls and floor under it, split in proportion to each contact's vertical normal component. That's a static q-model
  over the current contacts, sorted top to bottom, computed every tick. Walls take no vertical load.
- **Crush.** A ball whose load is over `ballStrengthTons` (1) for `squeezeSeconds` in a row is crushed: it flattens on
  screen over that time, then pops into **2 balls of radius r/√2** (area conserved), side by side along a seeded
  axis (horizontal ± `splitJitterDegrees`), inside the parent's footprint. Gen = parent gen + 1.
  `maxPopsPerTick` caps the cascade so it reads as a ripple, highest load first.
- **Stop.** The round ends when the press is resting, no ball is over strength, and the pile has been still
  (max speed < `settleSpeed`) for `stopHoldSeconds`. The answer is the ball count at that moment.
- `minRadius` is a safety floor: a ball at it can't split (logs a warning, a tuning failure).
- Exact physics isn't needed; **determinism is.** A custom position-based circle solver in pure C# (no Physics2D):
  gravity, floor, walls, press plate, circle-circle contacts, uniform-grid broadphase, fixed substeps, fixed
  iteration order. Same seed → bit-identical counts.

## Architecture (contract between the two build packages)

New assemblies `SimulationLobby.Simulations.HydraulicPress` (runtime, folder `Assets/Scripts/Simulations/HydraulicPress/`,
same references as `SimulationLobby.Simulations.PiClacks`) and `SimulationLobby.Simulations.HydraulicPress.Editor`.
Namespace `SimulationLobby.Simulations.HydraulicPress` (editor: `…HydraulicPress.EditorTools`). It mirrors `clack`'s
layout: `Kernel/`, `View/`, `Editor/`.

**Package A (simulation)** owns `Kernel/PressKernel.cs`, `HydraulicPressConfig.cs`, `HydraulicPressSimulation.cs`, the
runtime `.asmdef` and a headless C# harness in the scratchpad (a `dotnet` console project that compiles `Kernel/*.cs`).
**Package B (presentation and scene)** owns `View/*`, `Editor/*` (+ the editor `.asmdef`).

```csharp
// Kernel: no UnityEngine, doubles. Chamber floor top at y = 0, inner walls at x = ±HalfWidth.
public readonly struct PopEvent {               // one crushed ball
    public readonly int ParentId; public readonly int ChildA, ChildB; // stable ids (index into the ball arrays)
    public readonly double X, Y, Radius;        // the parent, at the moment it popped
    public readonly int Generation;             // the children's generation
}
public sealed class PressKernel {
    public PressKernel(PressKernelSettings s, int seed);    // settings = plain struct copied from config + round
    public int Count { get; }                                // live balls
    // Parallel arrays, valid for [0, Count). Ids are stable: a pop reuses the parent's slot for ChildA, appends ChildB.
    public double[] X { get; } public double[] Y { get; } public double[] R { get; }
    public double[] Load { get; }                            // tons, this tick (0 if not carrying)
    public double[] Squeeze { get; }                         // 0..1, how far into squeezeSeconds this ball is
    public int[] Generation { get; }
    public double PressY { get; }                            // bottom face of the plate
    public bool PressResting { get; }
    public bool IsStopped { get; }                           // the stop rule above has fired
    public double MaxLoad { get; }
    public int Step(double dt, System.Action<PopEvent> onPop); // returns pops this step
}

[System.Serializable] public struct PressRound {
    public float pressTons;       // 2, 4, 8, 16, 32, 64
    public string pressLabel;     // decal on the plate: "2 TONS"
    public string caption;        // bottom line during hold + run (empty = config.holdCaption)
    public string result;         // {n} = the count, e.g. "{n} BALLS."
    public float maxRunSeconds;   // safety cap: a run that hits it logs an error
    public float expectedRunSeconds; // measured by the harness, used for SeriesSeconds
}

public sealed class HydraulicPressConfig : SimulationConfig { // [CreateAssetMenu], defaults = the 6 storyboard rounds
    public PressRound[] rounds;
    public float chamberWidth, chamberHeight, startRadius, ballStrengthTons = 1f, ballWeightTons, pressSpeed,
                 squeezeSeconds, splitJitterDegrees, settleSpeed, stopHoldSeconds, minRadius;
    public int maxPopsPerTick, substeps, iterations;
    public float firstHoldSeconds, holdSeconds, resultHoldSeconds, transitionSeconds, endCardSeconds;
    public string title, question, subCaption = "EACH BALL HOLDS 1 TON", holdCaption = "GUESS?",
                  counterLabel = "BALLS", endLine, ladderFootnote;
    public float SeriesSeconds { get; }  public override int MaxTicks { get; }
}

public enum PressPhase { Hold, Run, Result, Transition, EndCard, Done }

public sealed class HydraulicPressSimulation : MonoBehaviour, ISimulation { // FormatSlug "press"
    public HydraulicPressConfig Config { get; } public PressPhase Phase { get; } public float PhaseProgress { get; }
    public int RoundIndex { get; } public int RoundTotal { get; } public PressRound CurrentRound { get; }
    public PressKernel Kernel { get; }        // read-only use by the presenter; replaced each round
    public float DisplayPressY { get; }       // plate bottom, world units over the floor (lifts during Transition)
    public string PressLabel { get; }         // flips at the transition midpoint
    public int BallCount { get; }             // live, never ahead of the picture
    public bool RoundFinished { get; }
    public long PopSerial { get; }            // all pops this run, monotonic (presenter diffs it per frame)
    public bool TryGetPop(long serial, out PopEvent pop); // ring buffer of the last 4096
    public IReadOnlyList<int> CompletedCounts { get; }
    public int SeedUsed { get; }
}
```

Phases: `Hold` (round 1 uses `firstHoldSeconds`) → `Run` (kernel steps until `IsStopped`) → `Result` (`resultHoldSeconds`,
nothing moves) → `Transition` (press lifts, label flips, pile fades/drains, one fresh ball drops in) → `Hold` … →
`EndCard` → `Done` (`IsComplete`). `RunResult` metrics: per-round counts, run seconds, max generation, `minRadius` hits.

## Video-specific work

### Tuning targets (package A's harness sets the values)

| Knob | Start at | Target |
|---|---|---|
| `chamberWidth` × `chamberHeight` | 6 × 7 | The final pile fits under the plate with room above it for the press |
| `startRadius` | 1.4 | Round 1 ends at exactly **2** on every seed |
| round 6 count | — | ≤ ~2,500 balls; ≤ ~6 ms per tick in the harness at the peak |
| run length | — | Rounds 1-2 ~3-5 s, 3-6 ~5-8 s. Total `SeriesSeconds` 60-75 |
| ratio, rounds 3-6 | — | Report it per seed (20 seeds). It decides the ending line (see Storyboard) |
| `minRadius` hits | — | 0 on every seed |
| `squeezeSeconds` | 0.12 | Each squash readable at round 1-2; cascades still ripple at round 6 |

Harness acceptance: report a table per round (count min/median/max over 20 seeds, run s, max generation, worst
tick ms); same seed twice → identical counts and identical final positions; no ball ever outside the chamber; every
round stops before `maxRunSeconds`. **If the load model turns out unstable** (counts that don't settle, or runaway
pops), report what happened. Don't hide it with a tuning hack.

### Look (package B)

- 2D, ortho camera, 9:16. The chamber fills the middle band between the HUD panels (arena clear of text).
- Chamber: dark steel walls and floor with a bevel line. Press: a heavy plate with hazard stripes along its lower
  edge, a chrome piston rod up to the top of the frame, and the tonnage as a world-space TMP decal on the plate face.
- Balls: **one draw call** (a dynamic mesh of quads with a circle sprite/shader, per-vertex colour), never one
  GameObject per ball. Colour by generation: a seeded rainbow offset, each generation a fixed hue step, so the
  cascade reads as colour waves. Each crushing ball draws flattened by its `Squeeze` (up to ~35% shorter, wider to match).
- A pop makes a small white burst ring at its x/y (pooled, capped per frame).
- Backdrop from `BackdropThemeLibrary`, the same as `clack`.
- HUD on a canvas from `CaptionCanvasBuilder` (top: title + question, bottom: line + `BALLS n`). `ResultLadderHud`
  rows in `<mspace>`: `2 TONS → 2`.
- Small camera shake when the press first lands each round, and a heavier one on the stop. Presentation only.

### Sound (package B, first-class)

- A hydraulic hum (`CreateRumbleLoop`, low) while the press moves, and a heavy `CreateImpactThud` when it lands and when it stops.
- A pop per crush (`CreateClick`/`CreateGlassClink` blend), pitch rising with generation (smaller = higher), at
  most 3 per frame plus a soft crackle bed when more than that pop at once.
- `CreateGlassClink` on each result and the ladder row. A whoosh on the transition.
- Listening check on first Play: round 1's single pop must be unmistakable; round 6 should sound like crackling ice, not noise.

## Check on first Play

- [ ] Build **Simulation Lobby ▸ 2D Formats ▸ Hydraulic Press (press) ▸ Split Press (press-001)**. The console logs the seed, 6 rounds and the estimated length
- [ ] Round 1: the ball squashes, pops into 2, and the press settles on both. Counter goes 1 → 2
- [ ] Every round's count matches the harness band for its round
- [ ] The press label changes every transition, and the chamber resets to one ball
- [ ] Round 6 keeps frame rate (record at 60 fps) and stays readable at 480px wide
- [ ] No `[press]` errors (timeouts, `minRadius` hits, balls outside the chamber)

## Acceptance criteria

- [ ] Same seed twice gives an identical outcome
- [ ] Readable at 480px wide; the HUD stays clear of the chamber
- [ ] Length 60-75 s

## Publish

Title options: *"Every Crushed Ball Splits in 2... How Many Before the Press STOPS? 🔩 #physics #satisfying #shorts"*.
Pinned comment: *"Each ball holds 1 ton. Double the press and you need double the balls side by side, and they're half as
wide, so ~4× as many 👇"* **Don't use it: the harness ratio didn't hold on every seed (see *As built*).** Use instead: *"Each ball holds 1 ton. The press only stops when enough balls share its weight, so more force means smaller balls 👇"*. Record the logged seed here.

## Retro

The open question: **does a crusher-plus-multiply Short (the S2DS shape) clear the ~1-1.7K band for us?** Compare views
and like-rate at day 7 with `clack-001`.

## As built: simulation (package A)

Harness: `dotnet` console project in the session scratchpad (`press-harness`), compiles `Kernel/PressKernel.cs` by link, no Unity.
20 seeds per round, dt 1/60, the config defaults. Same seed twice gives identical counts and identical final positions
(FNV hash of every x/y) for all six tonnages. No ball ever outside the chamber, no round hit its cap, 0 `minRadius` hits.

| Press | Balls min / median / max | Run s median (min-max) | Max generation |
|---|---|---|---|
| 2 t | 2 / 2 / 2 | 2.85 (2.85-2.87) | 1 |
| 4 t | 4 / 4 / 4 | 3.10 (3.10-3.23) | 2 |
| 8 t | 11 / 13 / 20 | 3.63 (3.40-4.58) | 7 |
| 16 t | 54 / 67 / 87 | 5.68 (4.92-7.32) | 9 |
| 32 t | 269 / 278 / 304 | 7.85 (6.45-10.35) | 11 |
| 64 t | 996 / 1029 / 1051 | 12.88 (8.50-17.48) | 12 |

`SeriesSeconds` = 64.2 s (median runs); worst seed (round 6 at 17.5 s) is about 69 s. Tick cost at round 6 (release .NET, ~1,050
balls): median 2.6-2.8 ms, p99 6-10 ms, worst ~11-17 ms on a busy machine. Rounds 1-2 run 2.9-3.1 s, rounds 3-4 3.6-5.7 s,
rounds 5-6 are longer than the 5-8 s target (7.9 and 12.9 s median): the end of a big cascade is a long avalanche of small
pops (see *What the load model does*).

Round-to-round ratio per seed (20 seeds):

| Ratio | min | median | max | Seeds inside 3-5x |
|---|---|---|---|---|
| R3 / R2 | 2.75 | 3.25 | 5.00 | 16 of 20 |
| R4 / R3 | 2.70 | 4.90 | 6.82 | 10 of 20 |
| R5 / R4 | 3.15 | 4.37 | 5.30 | 19 of 20 |
| R6 / R5 | 3.38 | 3.65 | 3.83 | 20 of 20 |

Median counts: 2, 4, 13, 66, 278, 1,028 (ratios x2, x3.3, x5.1, x4.2, x3.7).

**Ending-line verdict: the fallback.** The storyboard keeps `~4x EVERY TIME` only if rounds 3-6 are within ~3-5x on every seed.
They are not: R3 / R2 reaches 2.75 and R4 / R3 reaches 6.82 (the round 3 count is the noisy one, 11-20, from a handful of
balls). Only R6 / R5 sits in 3-5x on all 20 seeds (R5 / R4 on 19, with one seed at 5.30).
So the shipped defaults use the fallback: round 4 result `{n} BALLS.`, `endLine` = `EACH BALL HOLDS 1 TON. MORE FORCE, SMALLER BALLS.`
Round 3 keeps `NOT DOUBLE...` (true on every seed: 2.75x or more). Optional if the retro wants it: a `~4x` claim on rounds 5-6
only is nearly supported by the data (3.2-5.3x and 3.4-3.8x), but not on the earlier rounds.

### Tuned knobs (config defaults)

`startRadius` 0.9 (not 1.4: at 1.26 the final pile is nearly a crystal of 5 cm grains and the solver boils), chamber 6 x 7,
`startOffset` 0.6, `pressSpeed` 2.5, `squeezeSeconds` 0.12 with `squeezeDecay` 0 (a squeeze holds when the load dips),
`loadTolerance` 0.15 (a ball is over strength above 1.15 t), `loadSmoothing` 0.04 s, `calmSpeed` 0.5 (per ball),
`splitJitterDegrees` 3, `settleSpeed` 0.1 (pile RMS speed), `stopHoldSeconds` 0.5, `minRadius` 0.008,
`maxPopsPerTick` 64, `substeps` 3, `iterations` 4, `relaxIterations` 16, `contactTolerance` 0.1, `gravity` 4, `damping` 0.5,
`friction` 0.05, `maxSpeed` 6, `maxBalls` 5000. Per round `expectedRunSeconds` 2.9 / 3.1 / 3.6 / 5.7 / 7.9 / 12.9,
`maxRunSeconds` 6 / 6 / 10 / 14 / 20 / 32.

### What the load model does (and where it is soft)

- **Plate load.** Once the plate rests on the pile it presses like a soft pad: its weight is shared over the pile's exposed top
  surface in proportion to each ball's visible width (columns sampled across the chamber), not on whichever ball is tallest.
  A "tallest ball takes everything" plate was tried first and was unstable: the count ran away to the `minRadius` floor on every
  seed from round 3 up. Below the plate the q-model is as specified: top to bottom over the contact graph, split by vertical
  normal component, the floor absorbs. The pile then settles to roughly "balls across ~ tons", so the count goes as tons squared,
  which is why the ladder is about x4 per doubling.
- **Why the counts are not exactly x4.** Pops are irreversible and a pile is disordered, so each round overshoots its ideal
  gen-k grid by a seed-dependent amount. Small ladders (rounds 2-3) are quantised by a few balls (round 3 = 11-20).
- **Solver quality changes the answer.** More substeps / iterations give a denser pile and more balls (round 6 ranged from ~800 to ~2,000
  across the solver settings tried). The numbers above are for the shipped defaults only; if the solver settings
  change, re-run the harness table and refill `expectedRunSeconds`.
- **Not a literal q-model, three softenings, all in the config:** `loadTolerance` 0.15 and `loadSmoothing` 0.04 stop
  solver noise from popping marginal balls; `squeezeDecay` 0 makes the squeeze cumulative; the squeeze only builds while the
  ball is calm (`calmSpeed`). Without them the end of round 6 never settles within 15-20 s.
- **Known weak spot.** The tail of rounds 5-6 is a slow avalanche (~5-7 pops/s of grain-sized balls). It is not visible at 480 px,
  but it is why those rounds run 8-17 s.
