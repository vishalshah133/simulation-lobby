# clack-001: Pi Blocks

**Format:** `clack` · **Length:** ~65s (7 rounds) · **Aspect:** 9:16 · **Status:** `built` 2026-10-17 (compiles; harness-checked; not yet played)
**Brief:** `KnowledgeBase/formats/pi-clacks.md`
**Scene:** `PiClacks_Blocks_2D` (**Simulation Lobby ▸ 2D Formats ▸ Pi Clacks (clack) ▸ Pi Blocks (clack-001)**, priority 161)
**Config:** `Assets/Settings/Configs/PiClacksConfig_001.asset` · **Seed:** random per Play, logged at start (cosmetic only)

## Premise

**"How many times will they clack?"**, asked seven times, with the big block 100× heavier each time. The answers
are 3, 31, 314, 3141, 31415, 314159, 3141592. They stack up on screen until the viewer sees π before we name it.

Experiment lane, and the second maths Short after `conv`. Open question: does a maths curiosity with an
escalating number hold like `conv-002` did (2,183 views at ~2.3 days, 2.43% likes)?

## Storyboard

| # | Beat | On screen | Bottom line (transition/hold → run → result) | Ladder |
|---|---|---|---|---|
| 1 | Hook | Wall on the left, small block `1 kg`, big block `1 kg` (same size, other colour) sliding in. Question up from frame 0 | `GUESS?` → `WATCH THE COUNTER` → `3 CLACKS.` | `3` |
| 2 | Escalate | Big block grows, label ticks to `100 kg` | `NOW 100× HEAVIER...` / `GUESS?` → `31 CLACKS.` | `31` |
| 3 | Escalate | Grows, `10K kg`. A buzz at the wall, then a long quiet wait and one last clack | `100× HEAVIER AGAIN...` / `GUESS?` → `314. WAIT...` | `314` |
| 4 | Notice | Grows, `1M kg` | `1 MILLION kg...` / `GUESS THE NEXT ONE?` → `3141. IT'S π!` | `3141` |
| 5 | Confirm | Grows, `100M kg` | `100 MILLION kg...` / `GUESS?` → `31415. STILL π.` | `31415` |
| 6 | Spectacle | Grows, `10B kg`. The buzz turns into a tone | `10 BILLION kg...` / `GUESS?` → `314159!` | `314159` |
| 7 | Peak | Grows, `1T kg` | `1 TRILLION kg...` / `GUESS?` → `3141592. EXACTLY π.` | `3141592` |
| End | Reason | Blocks at rest, ladder with `π = 3.1415926…` under it | `EVERY 100× HEAVIER = ONE MORE DIGIT OF π` | stays |

Top panel: title `PI BLOCKS` (small), question `HOW MANY TIMES WILL THEY CLACK?` (big, from frame 0).
Bottom panel: the line above, plus the live counter `CLACKS 314`.

Cold-viewer check (`learnings.md` → *Story legibility*):
- [x] The question names things visible from the first frame (two blocks and a wall. A "clack" is a touch, and round 1 shows all 3 at walking pace)
- [x] Every cause happens on screen: the block grows and its label changes during each transition, with a caption saying 100× heavier
- [x] Every beat raises the stakes (a bigger number). If it drags, cut round 5 first, never 3 or 4
- [x] The only number shown is the answer (the live `CLACKS` counter). The ladder is the answer history, and the twist lives in it
- [x] The ending says *what* in one line (100× = one more digit). The deeper *why* (a circle in velocity space) is
      left for the pinned comment

## Blocks used

- [x] **REUSE** [determinism-core](../blocks/determinism-core.md): `SimulationRunner`, `ISimulation`, `SimulationConfig`
- [x] **REUSE** [capture-shorts](../blocks/capture-shorts.md)
- [x] **REUSE** `CaptionCanvasBuilder` / `CaptionHud`, `BackdropThemeLibrary` / `BackdropStyler`, `ProceduralToneBank` (`CreateClick`, `CreateTone`, `CreateGlassClink`)
- [ ] **BUILD** `Presentation/UI/ResultLadderHud`: a format-agnostic stacked list of past results (a candidate block for any "the answers form a pattern" video)
- [ ] **BUILD** the `clack` format: kernel, config, simulation, presenter, audio, scene builder

## Architecture (contract between the two build packages)

New assemblies `SimulationLobby.Simulations.PiClacks` (runtime, folder `Assets/Scripts/Simulations/PiClacks/`, same
references as `SimulationLobby.Simulations.Convergence`) and `SimulationLobby.Simulations.PiClacks.Editor`. Namespace
`SimulationLobby.Simulations.PiClacks` (editor: `…PiClacks.EditorTools`). It mirrors `conv`'s layout:
`Kernel/`, `View/`, `Editor/`.

**Package A (simulation)** owns `Kernel/BlockCollisionKernel.cs`, `PiClacksConfig.cs`, `PiClacksSimulation.cs`, the
runtime `.asmdef` and a C# harness in the scratchpad.
**Package B (presentation and scene)** owns `View/*`, `Editor/*` (+ the editor `.asmdef`) and `Presentation/UI/ResultLadderHud.cs`.

```csharp
// Kernel (no UnityEngine, doubles only). Wall face at x = 0, positions are left faces.
public enum ClackKind { Wall, Blocks }
public readonly struct ClackEvent { public readonly ClackKind Kind; public readonly double X; /* contact x */ public ClackEvent(ClackKind k, double x); }
public sealed class BlockCollisionKernel {
    public BlockCollisionKernel(double smallMass, double bigMass, double smallSize, double smallGap, double bigGap, double speed);
    public double SmallX { get; } public double BigX { get; } public double SmallV { get; } public double BigV { get; }
    public long Count { get; }
    public bool IsDone { get; }              // SmallV >= 0 && BigV >= SmallV: no collision can ever happen again
    public double LastClackTime { get; }     // seconds since construction
    public int Step(double dt, System.Action<ClackEvent> onClack); // resolves every event inside dt; returns clacks this step
    public static long ExpectedCount(double massRatio); // ceil(π / atan(sqrt(1/ratio))) - 1
}

[System.Serializable] public struct PiClacksRound {
    public int massExponent;   // big mass = 100^massExponent, small mass = 1
    public string massLabel;   // decal on the big block: "1 kg", "100 kg", "10K kg", "1M kg", "100M kg", "10B kg", "1T kg"
    public float bigSize;      // drawn side, world units (log growth: ~1.0 → 3.0)
    public float smallGap;     // wall → small block's left face at start
    public float bigGap;       // small's right face → big's left face at start
    public float speed;        // big block's starting speed toward the wall, units/s
    public float maxRunSeconds;// safety cap: a run that hits this logs an error
    public string caption;     // bottom line during transition + run
    public string holdCaption; // empty = config.holdCaption
    public string result;      // {n} = the count
}

public sealed class PiClacksConfig : SimulationConfig {   // [CreateAssetMenu], defaults = the 7 storyboard rounds
    public PiClacksRound[] rounds; public float smallSize = 1f; public string smallMassLabel = "1 kg";
    public float frameWidth = 12f;      // visible world width; the wall sits 0.6 in from the left edge
    public float firstHoldSeconds, preLaunchHoldSeconds, transitionSeconds, resultHoldSeconds, endCardSeconds;
    public string title, question, holdCaption = "GUESS?", counterLabel = "CLACKS", endCard, endLine = "π = 3.1415926…";
    public float SeriesSeconds { get; }  // estimate, from maxRunSeconds-free timings measured by the harness; logged by the builder
    public override int MaxTicks { get; } // derived from the rounds plus a margin
}

public enum PiClacksPhase { Hold, Run, Result, Transition, EndCard, Done }

public sealed class PiClacksSimulation : MonoBehaviour, ISimulation {   // FormatSlug "clack"
    public PiClacksConfig Config { get; } public PiClacksPhase Phase { get; } public float PhaseProgress { get; }
    public int RoundIndex { get; } public int RoundTotal { get; } public PiClacksRound CurrentRound { get; }
    // Display state in world units relative to the wall face (x) and floor top (y = 0). Lerped during Transition.
    public float SmallX { get; } public float SmallSize { get; } public float BigX { get; } public float BigSize { get; }
    public string BigMassLabel { get; }       // flips to the next round's label at the transition's midpoint
    public long ClackCount { get; }           // this round, live; never ahead of the picture
    public long ExpectedCount { get; }        // N(k) for this round
    public bool RoundFinished { get; }        // true from the tick the kernel IsDone (the result line shows then)
    public long ClackSerial { get; }          // all clacks this run, monotonic (presenter diffs it per frame)
    public bool TryGetClack(long serial, out ClackEvent clack); // ring buffer of the last 256 clacks
    public IReadOnlyList<long> CompletedCounts { get; } // one entry per finished round, appended when RoundFinished flips
}
```

Phases: `Hold` (first round uses `firstHoldSeconds`) → `Run` (kernel steps until `IsDone`, `RoundFinished` set) → `Result`
(kernel keeps moving the blocks for `resultHoldSeconds`, no clacks possible) → `Transition` (next round: blocks glide to
the new start, big block grows, label flips) → `Hold` … → `EndCard` → `Done` (`IsComplete`). A count that disagrees with
`ExpectedCount` logs `[clack] round n: counted X, expected Y` as an error.

## Video-specific work

### Rounds (start values; package A's harness tunes gaps and speed)

| # | k | Label | bigSize | Expected | Tuning target |
|---|---|---|---|---|---|
| 1 | 0 | 1 kg | 1.0 | 3 | 3 clacks clearly apart (~1 s each) |
| 2 | 1 | 100 kg | 1.35 | 31 | |
| 3 | 2 | 10K kg | 1.7 | 314 | the late last clack (t ≈ 8 s at gaps 2/2) stays: it's the "wait…" beat |
| 4 | 3 | 1M kg | 2.0 | 3141 | |
| 5 | 4 | 100M kg | 2.35 | 31415 | |
| 6 | 5 | 10B kg | 2.7 | 314159 | |
| 7 | 6 | 1T kg | 3.0 | 3141592 | under ~10 ms of CPU per tick at the peak |

Harness acceptance: every count equals `ExpectedCount`. Every contact x stays ≤ `frameWidth − 1.2` (inside the frame).
No round's run (launch → `IsDone`) is longer than 8 s. Two runs are bit-identical. Report the per-round run time and the
total `SeriesSeconds`.

### Look (package B)

- 2D, ortho camera sized so the visible width equals `frameWidth`, the wall face 0.6 from the left edge, and the floor
  slightly below centre. The ladder sits between the question and the floor, and the counter in the bottom panel.
- Wall: a solid bar with diagonal hatching. Floor: a thin bright line. Blocks: rounded-corner squares with a soft
  gradient and a drop shadow. The small block and the big block get a seeded high-contrast colour pair.
- Labels are world-space TMP decals on each block face (`1 kg` on the small one, the round's label on the big one), sized
  to the block.
- A clack makes a short white spark at the contact x. In a buzz (many clacks in a frame), the seam at the wall glows
  steadily, with brightness following the clack rate.
- Backdrop from `BackdropThemeLibrary`, the same as `conv`.
- **Ladder:** left-aligned digits in `<mspace>` so the columns line up. The newest row punches in, and its last digit
  is tinted (the digit this round added). `π = 3.1415926…` fades in under it on the end card.

### Sound (package B, first-class)

- Fewer than ~90 clacks/s (buzz on at 90, off at 60): one `CreateClick` per clack (at most 3 per frame), with a seeded ±3% pitch jitter.
- Above that, a looping buzz (`PiClacksAudio.CreateBuzzLoop`: a 220 Hz sawtooth, since `CreateTone` decays and can't loop). Its pitch maps log10(clacks/s) from 1.5 to 6.5 onto 0.7 to 2.4, and its
  volume follows the rate. It fades out within ~80 ms when the clacks stop.
- `CreateGlassClink` on each result, and a softer one when the ladder row lands. A whoosh on the transition growth.
- Listening check on first Play: rounds 1-2 must sound like separate clacks, and rounds 6-7 like a rising tone.

### As built (harness, dt 1/60, all rounds gaps 2/2, speed 2)

| Round | Count | Run s | Rightmost contact x | Worst tick |
|---|---|---|---|---|
| 1 | 3 | 3.00 | 3.00 | 0 ms |
| 2 | 31 | 3.93 | 4.84 | 0 ms |
| 3 | 314 | 7.90 (last clack 7.89: the "wait" beat) | 3.00 | 0 ms |
| 4 | 3141 | 3.70 | 4.37 | 0.1 ms |
| 5 | 31415 | 3.08 | 3.16 | 1.3 ms |
| 6 | 314159 | 5.78 | 8.54 | 1.2 ms |
| 7 | 3141592 | 3.53 | 3.00 | **12 ms** (1.67M clacks in one tick: a one-off editor hitch, not in a fixed-step capture) |

`SeriesSeconds` ≈ 65.2. `PiClacksRound.expectedRunSeconds` holds the measured run time; re-measure it if gaps or speed change.
`π` isn't in the static LiberationSans atlas. It comes through the dynamic `LiberationSans SDF - Fallback` (the TTF has it), warmed in `Start`.

## Check on first Play

- [ ] Build **Simulation Lobby ▸ 2D Formats ▸ Pi Clacks (clack) ▸ Pi Blocks (clack-001)**. The console lists 7 rounds, the estimated length and the frame
- [ ] No `[clack]` count errors (every round equals its expected count)
- [ ] Round 1's three clacks read one by one; the counter goes 1, 2, 3 in step with them
- [ ] The big block visibly grows and its label changes in every transition
- [ ] Round 3's quiet wait before the final clack is visible: the small block drifts back to the wall
- [ ] No clack happens off screen
- [ ] The ladder digits line up in columns at 480 px wide; the π line appears on the end card
- [ ] π renders (round 4 result, end card, ladder footnote). If not: add U+03C0 to the fallback font asset's character table
- [ ] Rounds 6-7 don't hitch (frame time OK at the buzz), and the buzz rises in pitch

## Acceptance criteria

- [ ] Same seed twice gives an identical outcome
- [ ] Every round's count matches the formula
- [ ] Readable at 480 px wide; the HUD stays clear of the blocks
- [ ] Length 60-75 s

## Publish

Title options: *"How Many Times Will They CLACK? 🧱 #physics #math #shorts"*, *"Heavier Block, More Clacks... Guess How Many? #physics #maths #shorts"*.
Pinned comment: *"Why π? The two speeds always sit on a circle, and every clack is a step around it 👇"*. Record the logged seed here.

## Retro

The open question: **does an escalating maths curiosity (`clack`) hold like `conv-002`?** Compare views and like-rate at day 7.
