# brach-001: Fastest Path

**Format:** `brach` · **Length:** ~70-80s (5 rounds) · **Aspect:** 9:16 · **Status:** `built` 2026-10-18 (compiles; harness-checked; not yet played)
**Brief:** `KnowledgeBase/formats/brachistochrone.md`
**Scene:** `Brachistochrone_FastestPath_2D` (**Simulation Lobby ▸ 2D Formats ▸ Brachistochrone (brach) ▸ Fastest Path (brach-001)**, priority 181)
**Config:** `Assets/Settings/Configs/BrachistochroneConfig_001.asset` · **Seed:** random per Play, logged at start (cosmetic only)

## Premise

**"Which ball wins?"** Same start, same finish, different tracks. The curved track wins every round, against every
shape, even when it's the longest and even when it dips below the finish line.

Proven lane, proposed 10-18 (maths Shorts 2 for 2). Open question: does a third maths topic hold the ~2.5K level of
`conv-002` and `clack-001`, and does a race-shaped maths question do as well as a count-shaped one?

## Storyboard

| # | Beat | On screen | Bottom line (transition/hold → run → result) |
|---|---|---|---|
| 1 | Hook | Start gate A top-left, finish flag B lower-right. Two tracks: `STRAIGHT` (shortest) and `CURVE`. Balls at A. Question up from frame 0 | `SHORTEST vs CURVE` / `GUESS?` → (race) → `CURVE WINS!` then the two lengths fade in on the tracks: `BUT IT'S LONGER!` |
| 2 | Escalate | A third track grows in: `STEEP DROP` (falls nearly straight down, then runs flat) | `NOW A STEEP DROP` / `GUESS?` → `CURVE AGAIN!` |
| 3 | Close call | A fourth track grows in: `CIRCLE` (the arc Galileo guessed). It's close: slow motion at the finish | `NOW A CIRCLE` / `GUESS?` → `CURVE. JUST!` |
| 4 | Twist | The finish flag slides right and up. Every track redraws to the new B, and the curve now **dips below the finish and climbs back up** | `FINISH MOVES UP` then `CURVE GOES UNDER IT!` / `GUESS?` → `STILL THE CURVE!` |
| 5 | Peak | Six tracks: + `PARABOLA`, + `DEEP DIP` (goes much deeper than the curve) | `6 SHAPES` / `GUESS?` → `CURVE. ALWAYS!` |
| End | Reason | The winning track glows, its label turns to BRACHISTOCHRONE; the others fade | Top: `BRACHISTOCHRONE CURVE` / `GREEK FOR "SHORTEST TIME"` · bottom: `BRACHISTOS = SHORTEST` → `CHRONOS = TIME` |

Top panel: title `FASTEST PATH` (small), question `WHICH BALL WINS?` (big, from frame 0).
Bottom panel: the line above. No counter, no tally: the winner is the answer.

**HUD rule (easy to consume):** one short line at a time, **at most 4 words**, never two caption lines on screen at once.
Question ≥ 96 px and bottom line ≥ 80 px at the 1080x1920 reference, bold, with a dark outline or backing plate.
Track labels are one word where possible (`STRAIGHT`, `CURVE`, `STEEP`, `CIRCLE`, `PARABOLA`, `DEEP`), ≥ 0.35 world
units tall, in the track's colour with an outline. Each caption holds ≥ 1.2 s, so a viewer can read it.

Cold-viewer check (`learnings.md` → *Story legibility*):
- [x] The question names things visible from the first frame (balls, the start, the finish, the tracks)
- [x] Every change happens on screen: new tracks grow in from A to B, the flag slides in round 4, and the tracks redraw
- [x] Every beat raises the stakes: a more plausible rival (steep, circle), then the curve handicapped (dips below B), then every shape
- [x] The only number shown is the answer's support: the two lengths in round 1 (the twist is that the longer one won)
- [x] The ending says *why* in one line

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md): `SimulationRunner`, `ISimulation`, `SimulationConfig`
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md)
- [ ] **REUSE** `CaptionCanvasBuilder` / `CaptionHud`, `BackdropThemeLibrary` / `BackdropStyler`, `ProceduralToneBank`
- [ ] **BUILD** the `brach` format: kernel, config, simulation, presenter, audio, scene builder

## Architecture (contract between the two build packages)

New assemblies `SimulationLobby.Simulations.Brachistochrone` (runtime, folder `Assets/Scripts/Simulations/Brachistochrone/`,
same references as `SimulationLobby.Simulations.PiClacks`) and `SimulationLobby.Simulations.Brachistochrone.Editor`.
Namespace `SimulationLobby.Simulations.Brachistochrone` (editor: `…Brachistochrone.EditorTools`). It mirrors `clack`'s
layout: `Kernel/`, `View/`, `Editor/`.

**Package A (simulation)** owns `Kernel/*`, `BrachistochroneConfig.cs`, `BrachistochroneSimulation.cs`, the runtime
`.asmdef` and a C# harness in the scratchpad.
**Package B (presentation and scene)** owns `View/*` and `Editor/*` (plus the editor `.asmdef`).

```csharp
// Kernel (no UnityEngine, doubles only). A = (0,0). x to the right, y = DROP (positive downward).
public enum CurveKind { Straight, Cycloid, CircleArc, Parabola, SteepDrop, DeepDip }

public sealed class CurvePath {
    // Polyline from A to B = (bx, by), bx > 0, by > 0. `shape` is per-kind (SteepDrop bend radius fraction,
    // DeepDip depth factor; ignored by the others). Every sample except A has y > 0, so the bead never stalls.
    //  Straight:  the segment A→B.
    //  Cycloid:   x = r(θ − sin θ), y = r(1 − cos θ), θ ∈ [0, θB]; solve r, θB so it passes through B (Newton on θB).
    //             θB > π (it dips below B and climbs back) exactly when bx/by > π/2.
    //  CircleArc: the circle through A and B with a vertical tangent at A: centre (c, 0), c = (bx² + by²) / (2 bx).
    //  Parabola:  y = by · (1 − (1 − x/bx)²)  (steep at A, flat at B).
    //  SteepDrop: straight down to ~0.9 by, a tight bend (radius = shape · bx), then straight to B.
    //  DeepDip:   y = by · x/bx + shape · by · sin(π x / bx), shape ≈ 0.8-1.2 (deeper than the cycloid).
    public static CurvePath Build(CurveKind kind, double bx, double by, double shape, int samples);
    public int Count { get; }  public double Length { get; }
    public double X(int i); public double Y(int i); public double S(int i);    // S = cumulative arc length
    public void PointAt(double s, out double x, out double y, out double angle); // clamps to [0, Length]
}

public sealed class BeadRun {                  // frictionless bead from rest at A, gravity g
    public BeadRun(CurvePath path, double g);  // precomputes the per-segment time table
    public double FinishTime { get; }          // Σ 2L / (v0 + v1),  v = sqrt(2 g y)
    public double DistanceAt(double t);        // exact inside a segment: s0 + v0 τ + ½ a τ²,  a = g Δy / L
    public double SpeedAt(double t);
    public static double CycloidTime(double r, double thetaEnd, double g); // θ · sqrt(r / g), the harness check
}

[System.Serializable] public struct BrachTrack {
    public CurveKind kind; public string label;  // decal near the track: "STRAIGHT", "CURVE", "STEEP", "CIRCLE", "PARABOLA", "DEEP"
    public float shape;
}

[System.Serializable] public struct BrachRound {
    public BrachTrack[] tracks;                // tracks[i] keeps its colour across rounds when its kind repeats
    public float finishX, finishY;             // B in world units (A at the origin, y = drop)
    public string caption, holdCaption, result; // empty holdCaption = config.holdCaption
    public bool showLengths;                    // round 1: path lengths fade in on the result
    public float maxRunSeconds;                 // safety cap; a run that hits it logs an error
}

public sealed class BrachistochroneConfig : SimulationConfig {   // [CreateAssetMenu], defaults = the 5 storyboard rounds
    public BrachRound[] rounds;
    public float gravity = 1.6f;               // tuned so the winner takes ~3.5 s in round 1 (see targets)
    public float frameWidth = 10f;             // visible world width
    public float firstHoldSeconds, preLaunchHoldSeconds, transitionSeconds, resultHoldSeconds, endCardSeconds;
    public float slowMoRadius = 0.8f;          // world units from B: when the two leaders are both inside it and
    public float slowMoGap = 0.25f;            // their finish times differ by < slowMoGap s, the race clock slows
    public float slowMoRate = 0.3f;
    public string title = "FASTEST PATH", question = "WHICH BALL WINS?", holdCaption = "GUESS?";
    public string endCard = "BRACHISTOCHRONE CURVE", endTitle = "GREEK FOR \"SHORTEST TIME\"", endLabel = "BRACHISTOCHRONE";
    public string endLine = "BRACHISTOS = SHORTEST", endLine2 = "CHRONOS = TIME";
    public float SeriesSeconds { get; }        // estimate from the harness timings; logged by the builder
    public override int MaxTicks { get; }      // derived from rounds plus a margin
}

public enum BrachPhase { Hold, Run, Result, Transition, EndCard, Done }

public sealed class BrachistochroneSimulation : MonoBehaviour, ISimulation {   // FormatSlug "brach"
    public BrachistochroneConfig Config { get; } public BrachPhase Phase { get; } public float PhaseProgress { get; }
    public int RoundIndex { get; } public int RoundTotal { get; } public BrachRound CurrentRound { get; }
    public int TrackCount { get; }
    public CurvePath GetPath(int track);       // current round's geometry (during Transition: the *next* round's)
    public float GetTrackReveal(int track);    // 0..1: how much of the track is drawn (new tracks grow in A→B)
    public bool IsTrackNew(int track);         // true for tracks added this round
    public float FinishX { get; } public float FinishY { get; } // lerped during the round-4 flag slide
    public float RaceClock { get; }            // seconds since launch (slow-mo applied)
    public float ClockRate { get; }            // 1, or slowMoRate during a photo finish
    public void GetBall(int track, out float x, out float y, out float angle, out float speed);
    public int GetPlace(int track);            // 0 until finished, then 1, 2, ...
    public int WinnerTrack { get; }            // −1 until the first finish
    public bool RoundFinished { get; }         // every ball finished
    public float GetLength(int track);
}
```

Phases: `Hold` (first round uses `firstHoldSeconds`) → `Run` (race clock advances by `fixedDelta · ClockRate`, balls
placed from `BeadRun.DistanceAt`) → `Result` (`resultHoldSeconds`, balls rest at B) → `Transition` (balls glide back to
A, new tracks grow in, existing tracks morph to the new B over `transitionSeconds`) → `Hold` … → `EndCard` → `Done`.
If a round's winner isn't a `Cycloid` track, log `[brach] round n: winner is X, expected the cycloid` as an error.
Place ties (identical finish times) break by track index, so it's deterministic.

## Video-specific work

### Rounds (start values; package A's harness tunes `gravity` and B)

| # | Tracks | B (bx, by) | Note |
|---|---|---|---|
| 1 | Straight, Cycloid | (6, 4) | bx/by = 1.5 < π/2, so the curve stays above B. `showLengths` |
| 2 | + SteepDrop (shape 0.12) | (6, 4) | |
| 3 | + CircleArc | (6, 4) | Close finish. Slow motion should fire here |
| 4 | Straight, SteepDrop, CircleArc, Cycloid | (8, 2.6) | bx/by ≈ 3.1: the cycloid dips well below B |
| 5 | all six (+ Parabola, + DeepDip shape 1.0) | (7, 3.2) | |

Harness acceptance: the cycloid wins every round. The polyline cycloid's `FinishTime` is within 0.05% of
`CycloidTime`. Round 1's winner takes 3-4 s. Every round's last finisher is in under 2.5× the winner's time (nobody
crawls in long after). Slow motion fires in round 3. Two runs are bit-identical. Report each round's finish times in
order, the lengths, and the total `SeriesSeconds` (target 70-80 s; stretch the holds, not the races).

### As built (package A)

Kernel, config, simulation and runtime asmdef are in `Assets/Scripts/Simulations/Brachistochrone/`. A console harness compiles
the real kernel, config, simulation and `Core` files against a UnityEngine stub (scratchpad `brach-harness/`). Defaults:
`gravity 1.5`, `slowMoGap 0.1` (was 0.25: at 0.25 slow motion also fired in rounds 4 and 5), first hold 3.5 s, pre-launch hold 2.4 s,
transition 3.0 s, result 3.2 s (round 1: 5.5 s), end card 6 s. The plan's B positions pass unchanged.

| Round | B | Finish order (s, path length) | Last / winner | Slow-mo | Run (real s) |
|---|---|---|---|---|---|
| 1 | (6, 4) | CURVE 3.546 (7.72), STRAIGHT 4.163 (7.21) | 1.17x | no | 4.17 |
| 2 | (6, 4) | CURVE 3.546 (7.72), STEEP 4.013 (10.02), STRAIGHT 4.163 (7.21) | 1.17x | no | 4.17 |
| 3 | (6, 4) | CURVE 3.546 (7.72), CIRCLE 3.632 (8.52), STEEP 4.013 (10.02), STRAIGHT 4.163 (7.21) | 1.17x | **yes** | 4.55 |
| 4 | (8, 2.6) | CURVE 4.263 (9.51), CIRCLE 4.429 (11.11), STEEP 4.657 (10.92), STRAIGHT 6.024 (8.41) | 1.41x | no | 6.03 |
| 5 | (7, 3.2) | CURVE 3.874 (8.48), CIRCLE 4.001 (9.66), DEEP 4.179 (10.14), PARABOLA 4.202 (7.88), STEEP 4.293 (10.38), STRAIGHT 4.968 (7.70) | 1.28x | no | 4.98 |

Cycloid polyline vs `CycloidTime`: error < 0.00001% in every round. Rounds 4 and 5 dip below B (theta 4.08, 3.64 > pi).
Two runs hash-identical. **`SeriesSeconds` = 73.5 s** (the harness run totals 73.3 s). No `[brach]` errors.
Track order is fixed (Straight, Cycloid, Steep, Circle, Parabola, Deep) so `tracks[i]` keeps its colour across rounds.

**Contract additions** (all extra, nothing renamed):
- `BrachRound`: `caption2` (replaces `caption` in the 2nd half of the Transition and during the Run; round 4 `CURVE GOES UNDER IT!`),
  `result2` (replaces `result` from 40% into the Result; round 1 `BUT IT'S LONGER!`), `resultSeconds` (0 = config), `expectedRunSeconds`.
- `BrachistochroneConfig`: `pathSamples` (2000), `ResultSecondsOf(round)`, `RoundCount`, `DefaultGravity`.
- `BrachistochroneSimulation`: `BottomLine` (the line to show for the current phase, honouring the above), `GetDisplayPath(track)`
  (like `GetPath` but morphed to the new B during a Transition; a fresh object per tick), `GetFinishTime(track)`, `SlowMoFired`, `PaletteStart` (0-5, seeded), `Seed`.
- `CurvePath`: `Kind`, `MaxY`, `CycloidRadius`, `CycloidThetaEnd`, `Lerp`. `GetPath` during a Transition is the *final* next-round shape, as the contract says.
- `GetBall` during a Transition glides the balls from the old B back to A (first 70% of the phase); new tracks' balls sit at A.

### Look (package B)

- 2D, ortho camera. It fits the current round's track bounds (A, B, the lowest dip) inside the band between the top
  and bottom panels, and eases between rounds, so round 4's deeper dip stays in frame.
- Tracks: thick rounded tubes (line meshes, ~0.14 wide), each a high-contrast hue (the curve keeps one colour
  throughout; seeded palette start). A soft glow under each. A new track grows from A to B during the transition.
- Balls: glossy circles (radius ~0.22) in the track's colour with a highlight, spinning with distance. A short trail.
- Start gate at A (a small chrome post with a bar that lifts on launch). Finish flag at B (a checkered flag decal).
- Labels (one word, see the HUD rule): world-space TMP decals beside each track's midpoint, offset to avoid overlap. **At 480 px wide no two labels
  may overlap**: test with round 5's six tracks.
- Round 1 result: each track's length appears at its midpoint (`10.3 m`, `12.1 m`), and `BUT IT'S LONGER!` points at the curve.
- Winner: its track pulses and its ball gets a burst at B. Losers dim slightly.
- Backdrop from `BackdropThemeLibrary`, the same as `conv`/`clack`.

### Sound (package B, first-class)

- Each ball has a soft rolling hum whose pitch and volume follow its speed (`ProceduralToneBank.CreateTone`, looped or
  re-triggered, capped at 6 voices).
- A finish chime per place: the winner gets a bright chord, the rest a soft tick, in descending pitch.
- A whoosh on each transition, a clink when the gate lifts, and a low swell under slow motion.

### As built (package B)

Files: `View/BrachistochronePresenter.cs`, `View/BrachistochroneAudio.cs`, `View/BrachBurst.cs`, `Editor/BrachistochroneSceneBuilder.cs` (+ editor asmdef). Differences from the plan:

- **Everything dynamic is created at Play.** The saved scene holds only camera, backdrop, runner/sim/presenter and the HUD canvas; tracks, balls, labels, gate and flag appear when you press Play (the Scene view is empty at edit time).
- **Tubes are `LineRenderer`s** (round caps/corners, `Sprites/Default`), not custom meshes, with a wider soft glow line under each. They draw from `GetDisplayPath` (morph) and `GetTrackReveal` (grow-in). Colour comes from the curve kind with the run's `PaletteStart` as a hue offset; the cycloid is always hue slot 0.
- **Camera** is refit every frame (eased, ~0.5 s) to the tracks, flag and gate so they sit in a band between 26% and 82% of screen height (tunable on the presenter), widening for round 4's dip.
- **Labels** are runtime world TMP with an outline. A deterministic greedy declutter picks each label's spot (fractions along its track, both sides, growing gaps) once per round when geometry is final, avoiding other labels (hard), tracks, the gate and the flag (soft). Labels fade out during transitions and back in at the hold.
- **Round-1 lengths** appear as `xx.x m` tags under each label during Result. The cycloid's tag turns gold and pulses once `BottomLine` switches to `BUT IT'S LONGER!`. There is no separate world decal or arrow with that sentence (it would duplicate the bottom line).
- **Bottom line** is `sim.BottomLine`, except: round 1's Hold shows the round caption for its first half before `GUESS?`, and from the first finish the round's `result` is shown (gold) instead of waiting for the last ball. The bottom line sits on a dark plate, auto-sized 80-96 px; the question is 100 px.
- **Audio** (all procedural): per-ball rolling hum (6 voices; pitch and volume follow speed, silent once the ball finishes), a four-note chord for the winner, descending soft ticks for later places, a whoosh on transition, a glass clink when the gate lifts, and a low swell loop while `ClockRate < 0.97`.
- Winner: its tube pulses and a ring + dots burst at B; losers dim to 45%. End card (9 s): only the cycloid stays lit (glow, breathing width), its label switches CURVE → BRACHISTOCHRONE, and the top panel swaps the question for `BRACHISTOCHRONE CURVE` / `GREEK FOR "SHORTEST TIME"`; the bottom line splits the name into its Greek roots.
- Fix 2026-10-18: the first cut showed the name for only 3 s in the bottom line and never said "brachistochrone"; `STEEP START = MORE SPEED` read as jargon. The user then chose the brachistochrone name over "cycloid", with its Greek breakdown (brachistos = shortest, chronos = time) as the explanation.

## Check on first Play

- [ ] Build **Simulation Lobby ▸ 2D Formats ▸ Brachistochrone (brach) ▸ Fastest Path (brach-001)**. The console lists 5 rounds and the estimated length
- [ ] No `[brach]` errors (the cycloid wins every round)
- [ ] Round 1: both tracks and labels visible from frame 0; the lengths appear after the result
- [ ] New tracks visibly grow in; round 4's flag slides and the curve visibly dips below the finish
- [ ] Round 3 goes to slow motion at the finish
- [ ] Round 5: six labels readable without overlap at 480 px wide
- [ ] The rolling hum follows speed; the winner's chime is clearly different

## Acceptance criteria

- [ ] Same seed twice gives an identical outcome
- [ ] Readable at 480 px wide; the HUD stays clear of the tracks
- [ ] Length 70-80 s

## Publish

Title options: *"Which Ball Wins? The Shortest Path LOSES 🏁 #physics #math #shorts"*, *"Same Start, Same Finish... Which Ball Wins? #physics #maths #shorts"*.
Pinned comment: *"It's called the brachistochrone. Dropping steep early buys speed you keep for the whole ride, and a rolling ball gives the same winner 👇"*. Record the logged seed here.

## Retro

The open question: **does a race-shaped maths question hold like the count-shaped ones (`conv-002`, `clack-001`)?** Compare views and like rate at day 7.
