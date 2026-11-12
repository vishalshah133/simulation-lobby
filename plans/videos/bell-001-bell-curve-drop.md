# bell-001: Bell Curve Drop

**Format:** `bell` · **Length:** ~70s (3 rounds) · **Aspect:** 9:16 · **Status:** `built` 2026-10-25 (compiles; not yet played in Unity)
**Brief:** `KnowledgeBase/formats/bell-curve.md`
**Scene:** `BellCurve_Drop_2D` (**Simulation Lobby ▸ 2D Formats ▸ Bell Curve Drop (bell) ▸ Bell Curve Drop (bell-001)**, priority 199)
**Config:** `Assets/Settings/Configs/BellCurveConfig_001.asset` · **Seed:** random per Play, logged at start (record it with the published video)

## Premise

**"What shape will the marbles make?"** Marbles drop through a peg board where every peg is a coin flip, and pile
into bins. The question is asked three times: 100 marbles, 1,000 marbles, then 1,000 on a rigged board.
Backlog item #4 (maths lane), with the twist round the *10-24 read* asked for.

## Storyboard

| # | Beat | On screen | Bottom line (hold → run → result) | Ladder |
|---|---|---|---|---|
| 1 | Hook | Empty peg board, spout on top, empty tray. Question up from frame 0 | `GUESS THE SHAPE?` → `100 MARBLES. EVERY PEG A COIN FLIP...` → `A ROUGH MOUND...` | `100: ROUGH` |
| 2 | Escalate | Pile drains; 1,000 marbles stream at 55/s, the bars climb | `GUESS AGAIN?` → `NOW 1,000 MARBLES...` → `A SMOOTH BELL CURVE!` (gold outline fades in) | `1,000: BELL` |
| 3 | Twist | Pegs turn amber and lean right | `STILL A BELL?` → `RIGGED: EVERY PEG LEANS RIGHT...` → `STILL A BELL. JUST SHIFTED.` | `RIGGED: BELL` |
| End | Why | Final pile and outline stay | `RANDOM LEFT/RIGHT STEPS ADD UP TO A BELL CURVE` | footnote `THE CENTRAL LIMIT THEOREM` |

Top panel: question `WHAT SHAPE WILL THEY MAKE?`, subline `EVERY PEG IS A COIN FLIP` (the cause, in one line).
Bottom panel: the line above plus the live counter `MARBLES 437` (counts landed marbles, ends on the round's total).

Cold-viewer check (`learnings.md` → *Story legibility*):
- [x] The question names things visible from frame one: marbles, pegs, bins
- [x] Every cause happens on screen: each marble visibly hops left or right off each peg; the rigging is visible (leaning amber pegs)
- [x] One question per video; rounds only change the marble count and the rigging
- [x] The only number shown is the answer (`MARBLES n`), which ends on the round total
- [x] The ending says why in one line

**Honesty rule:** round 1's result line is `A ROUGH MOUND...`, not "lumpy". A 100-marble pile on 10 rows can look nearly
bell-shaped on a lucky seed, and the caption must hold for every seed. The gold outline is drawn only on rounds 2-3 and
is fitted to the pile's own mean and spread (read from the sim, never hand-placed).

## Blocks used

- [x] **REUSE** [determinism-core](../blocks/determinism-core.md): `SimulationRunner`, `ISimulation`, `SimulationConfig`, `SeededRandom` (one branch per round)
- [x] **REUSE** [capture-shorts](../blocks/capture-shorts.md)
- [x] **REUSE** `CaptionCanvasBuilder` / `CaptionHud`, `ResultLadderHud`, `BackdropThemeLibrary` / `BackdropStyler`, `ProceduralToneBank` (`CreateClick`, `CreateScale`, `CreateGlassClink`, `CreateWhoosh`)
- [ ] **BUILD** the `bell` format: `BoardGeometry` kernel, config, simulation, presenter, scene builder

## Rules

- Analytic board, no physics solver (the brief allowed it): a marble's path is `rows` seeded coin flips; its bin is
  the number of rights. The presenter draws the arc between pegs from the path, so a seed replays exactly.
- Row r has r+1 pegs, so a marble on row r having gone right j times sits at x = (j - r/2) * binSpacing.
- Each bin's bar grows by `trayHeight / (marbles * peakFraction)` per landed marble: bars are a count graph, rescaled
  per round (100 marbles would be invisible at the 1,000 scale).
- Marble colour = destination bin, so the viewer can follow a marble to its pile.
- Audio: peg clicks (capped per frame), a pentatonic note per bin on landing (the middle bins ring most), clink on
  result, whoosh on transition.

## Open checks (need a Play in Unity)

- [ ] Read round 1 at 480 px: is a 100-marble pile readable as a pile, and does `ROUGH` hold across a few seeds?
- [ ] Marble pool (360) vs marbles in flight (~175 at the peak): no pool warning in the console
- [ ] Peg `lean` looks like a cause at phone size (amber, slanted slats); if not, enlarge `_maxLeanDegrees`
- [ ] Series length lands in the 60-120 s Shorts band (estimate ~71 s)
- [ ] Tray floor clears the bottom panel (builder log says so)
