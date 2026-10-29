# mach-002 — Marble Machine Grand Prix

**Format:** `mach` (3D) · **Length:** ~5-7 min (laps tuned to hit it) · **Aspect:** 16:9 1920x1080 · **Status:** `built, not yet run in Unity` (2026-10-29)
**Brief:** `KnowledgeBase/formats/marble-machine.md` · **Scene:** `MarbleMachine_GrandPrix_3D`
**Config:** `Assets/Settings/Configs/MarbleMachine/MachineRaceConfig_001.asset` · **Seed:** random per Play, logged
**Menu:** Simulation Lobby ▸ 3D Formats ▸ Marble Machine (mach) ▸ Grand Prix (mach-002)

Why this exists: `mach-001` is our best long-form push since v15 (1,090 views at ~4d). This keeps its look and stations, swaps the
7-colour sorter for **16 numbered racers on a 3-lap course**, and uses a **different layout** so it isn't the same video.

## Premise

16 numbered marbles drop through a giant marble machine and loop it 3 times. **Which number crosses the line first?**

## Storyboard

| Beat | Time | On screen | Caption / counter |
|---|---|---|---|
| Hook | 0:00-0:08 | Medium shot of the start gate: 16 numbered, coloured marbles on a 4x4 grid, the whole course rising behind | Top: **WHICH MARBLE WINS 3 LAPS?** Right: top-5 board, all LAP 1/3 |
| Countdown | 0:08-0:12 | Push-in on the grid. 3-2-1 | big **3 2 1 GO** |
| Act 1: lap 1 | 0:12-1:50 | The pack drops through the course; the camera follows the leader and cuts to each station as the pack hits it | Board re-sorts live; **LAP 1/3** |
| Lap 1 payoff | ~1:45 | First racer crosses the line | **LAP 2** banner + leader number |
| Act 2: lap 2 | 1:50-3:30 | The pack has spread; ski-jump misses send racers back via the cannon | Board shows who was sent back |
| Midpoint twist | ~3:30 | A racer that was last takes the lead through a gamble station | lead-change callout |
| Act 3: final lap | 3:30-5:30 | **FINAL LAP** banner. Camera sticks to the top 3; the ski jump decides it | Rows pulse |
| Payoff | ~5:30 | The first ball crosses the line; confetti | **#N WINS!** + podium (top 3 numbers) |
| Outro | +12 s | Pull-back to the opening shot | final order |

Read it as a cold viewer with the sound off:
- [ ] The question names things visible from frame one: 16 numbered coloured marbles on the grid.
- [ ] Every cause is on screen: a ski-jump miss, the net, the cannon firing back to the start.
- [ ] One question: who wins the 3 laps. The board never shows a different tally.
- [ ] Numbers read at 480px: floating billboard tags above each racer, board rows with colour chip + number.

## Built (scene builder, 2026-10-29; compiled, never run in Unity)

Files: `Editor/MachineRaceSceneBuilder.cs` (new; menu 312 build / 332 reset config), `Editor/MachineMaterials.cs` (+`EnsureRacerMaterials`, `Racer_NN.mat`),
`Editor/MachineSceneBuilder.cs` (visibility-only extraction: planner helpers `internal`, `Probe.recycleOffset`; mach-001 behaviour unchanged).
Scene `Assets/Scenes/MarbleMachine_GrandPrix_3D.unity`; config `Assets/Settings/Configs/MarbleMachine/MachineRaceConfig_001.asset` (created once, never overwritten).

Layout (world; camera on -Z, X right; yaw 0 = +Z, 90 = +X, 180 = -Z, 270 = -X). Mirror of mach-001: up the left edge, across the back, down the right edge, along the front.
Revised 2026-10-29 (user: two screw lifts): Wave Floor and Tipping Tower removed; a second screw lift added. Lift #1 sits right after the Helix Slide
(instead of after the ski jump) so the 11 m of helix + peg-wall drop is split and the course never stacks more than ~9 m of drops between lifts.
Flow: **Start Gate -> Helix Slide -> Screw Lift 1 (lift) -> Peg Wall -> Bucket Wheel (lift) -> Ski Jump -> Vortex Funnel -> Screw Lift 2 (lift) -> closing connector.**

| # | Station | Yaw | Where |
|---|---|---|---|
| 1 | Start Gate | 0 | front-left, origin; flows +Z |
| 2 | Helix Slide | 0 | left edge |
| 3 | Screw Lift 1 (lift) | 0 | left edge, up to the back-left corner |
| 4 | Peg Wall | 90 | back-left corner |
| 5 | Bucket Wheel (lift) | 90 | back row |
| 6 | Ski Jump | 180 | back-right corner; gutter and cannon on the INSIDE of the loop |
| 7 | Vortex Funnel | 180 | right edge (ski outlet lead shortened to 2.5 m for the race only) |
| 8 | Screw Lift 2 (lift) | 270 | front row; exit 5.5 m right of the gate entry |

Both lifts use `screwLiftRise` (each); display names "Screw Lift 1/2"; mesh assets suffixed per lift. Closing connector: Screw Lift 2 exit -> Start Gate entry.
Cannon beside the ski jump's gutter end (`scatterRadius` 0.5). Chutes fall >= 12 deg (ski outlet 16).

Height arithmetic (estimates; the build logs the real table and sizes the rise): drops gate 0.5 + helix 6.4 + peg 4.7 + ski 2.5 + vortex 1.5 = ~15.6 m;
lifts: wheel ~5.5 + 2 x R. 8 chutes ~52 m x tan 12 deg = ~11.5 m (ski outlet at 16 deg included). Need 2R = 15.6 + 11.5 + 3 (margin) - 5.5 = ~24.6, so
**R ~ 12.3 m per lift** (45 deg tube ~18 m long). Cap 15 m per lift (a warning with the shortfall is logged if hit). Tallest point expected ~14-15 m (lift tops).
Lift time ~19 s each at 0.65 m/s climb, so expect a lap near or above 100 s: if so, cut laps rather than slow anything.

First things to eyeball after the build (console, prefix `[mach-002]`):
- [ ] Height line: `Height loop CLOSES` (else the shortfall in metres) and the station table (heights, slope to next).
- [ ] Connector table: no `GAP`, `UPHILL`, `SHALLOW`; slope min >= 12; lap-line distance to the screw-lift end of the closing connector <= 10 m.
- [ ] Layout check: no overlaps/clashes; the cannon is not in the front third of the course (camera side) and the recycle chute meets its rim.
- [ ] Start grid: 16 slots, numbers readable at 480 px, gate drops cleanly at GO.
- [ ] Ski-jump miss rate (target 10-20%, tune `skiJumpGap`; package B suggests 1.7-2.0) and that the cannon lands a racer on the platform.
- [ ] Both screw lifts and the bucket wheel carry a single racer (no stalls); lift meshes are separate assets per lift.
- [ ] Lap time vs the 70-110 s target (log gives a rough estimate; raise `laps` if short).
- [ ] Building this scene regenerates the shared mesh assets under `Assets/Art/Generated/MarbleMachine/Meshes`, so rebuild mach-001's scene before using it again (and vice versa).

## Design decisions (user, 2026-10-29)

- **3 laps, 16 distinct colours + numbers.** All 16 start together on a 4x4 grid; grid slots are shuffled by the seed.
- **The layout must differ from mach-001** (different order, direction and footprint).
- **Continuous track:** no gaps where a ball can fall through; every connector starts and ends flush (as in mach-001).
- **Fast-paced:** connector slope 12° (config `targetSlopeDegrees`), not 8. **A smaller course is fine; add laps if a lap is too short.**
  Target: winner at 5:00-7:00 total. If a lap takes under ~70 s, raise `laps` (3 → 4/5) rather than slowing the track.

## Layout (mirrored loop, new order)

Counter-clockwise (mach-001 runs clockwise), start gate front-left, no sorter, no tubes, no trapdoor run, no silo.
Flow order: **Start Gate → Helix Slide → Wave Floor → Peg Wall → Bucket Wheel (lift) → Ski Jump → Vortex Funnel → Tipping Tower →
Screw Lift (lift) → back to the Start Gate lap line.**
The Ski Jump's miss net leads to the cannon, which fires the racer back onto the start platform (a lap-sized setback).
The scene builder decides offsets/yaws, with the same planner as mach-001 (heights from the fixpoint planner, collision-checked).

## Racers

Balls: radius 0.2 m, mass 0.2, bounciness 0.25. Chute width stays 1.8 (4.5 diameters). 16 racers, number = index + 1,
colours from `MachineRaceConfig.DefaultRacerColours()`. Each racer has a floating billboard number tag above it (world-space TMP,
Presentation, reads position only) and a short coloured trail. Stations scale by `chuteWidth` as before; any that assume bulk
volume (tipping tower threshold, trapdoor pile, bucket handoff, screw feed) must work with a single ball or small pack.

## Rules (simulation)

- Racers exist from tick 0 on the start grid, held by the gate plate. Phases: Grid (hold) → Countdown → Racing → FinalLap → Finished.
- `Progress` = laps completed + fraction through the current lap (via the course centreline); ranking = finished order, then Progress.
- A racer completes a lap by crossing the lap line (IStartGate.LapLinePoint/Normal) in flow direction after having cleared the
  last station (guards against the grid-side jitter). Finishing lap `Laps` = finish; first one wins.
- Recycled racers (ski miss, lost below kill plane, stalled) are fired by the cannon onto the start platform; they keep their laps.
- Determinism: grid shuffle and every station phase come from the seeded RNG; mechanisms driven from sim time only.

## Config targets

| Knob | Start at | Note |
|---|---|---|
| `racerCount` | 16 | |
| `laps` | 3 | Raise if the lap is under ~70 s |
| `ballRadius` | 0.2 | |
| `chuteWidth` | 1.8 | |
| `targetSlopeDegrees` | 12 | Faster than mach-001 |
| `tipperThreshold` | 2 | One or two racers tip a trough |
| `skiJumpGap` | tune | Target 10-20% of passes miss |
| `holdSeconds` / `countdownSeconds` | 6 / 3 | |
| `maxDurationSeconds` | 900 | Timeout means retune |

## Blocks used

- [ ] **REUSE** determinism-core (`SimulationRunner`, `SeededRandom`, fixed step 1/100), material-contact-audio, collision-safety
- [ ] **REUSE** `LeaderboardHud` + `CaptionHud`, `ConfettiBurst3D`, `TourCameraRig`, `ClatterBed` (from mach-001)
- [ ] **REUSE** all mach-001 stations except Silo, Sorter, TubeRack, Trapdoor Run; Cannon retargeted
- [ ] **BUILD** `StartGateStation` + `IStartGate`; `MachineRaceSimulation` + `IRaceState`; `MachineRaceConfig`; `RacePresenter`,
  `RaceCameraDirector`, `RacerTag` (billboard numbers); `MachineRaceSceneBuilder`

## Code layout + work packages

All in `Simulations/MarbleMachine/` (same assembly pair as mach-001; deleting this format folder still compiles the rest).
**Contract files (written, not to be edited by work packages):** `MachineRaceConfig.cs`, `IRaceState.cs`, plus the mach-001
contracts (`MachineStation.cs`, `StationContracts.cs`, `MachineBall.cs`). `MachineConfig` was un-sealed (the only change to it).

| WP | Owns | Summary |
|---|---|---|
| A (sim) | `MachineRaceSimulation.cs`, `RaceProgress.cs` | ISimulation + IRaceState: spawn racers, phases, lap line, progress, ranking, recycle, safety, result |
| B (stations) | `Stations/StartGateStation.cs`, `Editor/Stations/StartGateBuilder.cs`, edits to `CannonStation.cs`/`CannonBuilder.cs` for a start-gate target, and bulk-volume fixes in existing station runtime/builders | Start gate + grid, cannon retarget, single-racer-safe stations (defaults unchanged for mach-001) |
| C (scene) | `Editor/MachineRaceSceneBuilder.cs` (+ a minimal shared extraction from `MachineSceneBuilder.cs`) | Menu, layout, slope profile, scene/config creation, HUD canvas, wiring |
| D (presentation) | `RacePresenter.cs`, `RaceCameraDirector.cs`, `RacerTag.cs`, `RaceAudio.cs` | HUD binding (top-5 board, LAP n/3, banners), follow/tour camera, tags, audio |

## Acceptance criteria

- [ ] `dotnet build` of both MarbleMachine csprojs clean; mach-001 files behave identically
- [ ] Menu build makes the scene with no console errors, no pink materials, no gaps (continuity check passes)
- [ ] Every racer completes the course; `lost` < 5%, stall nudges logged
- [ ] A lap takes 70-110 s; winner at 5:00-7:00 (else retune laps)
- [ ] Ski jump miss rate 10-20% of passes; the cannon lands the racer on the start platform
- [ ] Lead changes >= 3 across 3 laps
- [ ] Opening frame readable at 480 px wide: 16 numbers and colours distinct
- [ ] Same seed twice → same winner and same finish times (`DeterminismVerifier`)

## Camera

Rebuilt 2026-10-29 after four rejections ("choppy, not soothing"); design and diagnosis in `mach-002-camera-rework.md`.
`RaceCameraDirector` is now a thin adapter over the pure `RaceCameraCore` (`Simulations/MarbleMachine/Camera/`): a drone rig that follows
an arc-length focus along a smoothed course rail, with every output behind a rate-limited critically damped spring (`CameraSpring`),
set-piece zones that blend rather than switch, and an obstruction probe that only biases pitch and distance (no position clamp). No scene
rebuild needed. All tunables live in `RaceCameraDirector.settings` (`RaceCameraSettings`).

- **Prove smoothness offline:** menu `Simulation Lobby > Tools > Race Camera Smoothness Test (mach-002)` (or `RaceCameraHarness.RunSuite(null)`).
  It replays a seeded synthetic race on the open scene's real course (two ski-jump recycles included) at 60 fps, 30 fps and jittery frame
  times, in 16:9 and 9:16, and writes `Temp/RaceCamTraces/report.txt`. 16:9 is judged against the section D thresholds; 9:16 is report only.
- **Real runs:** `RaceTraceRecorder.Begin(sim)` records a live race to `Temp/RaceCamTraces/live.trace`; `RunSuite(path)` replays it.
- **Live check:** tick `logLiveMetrics` on the director; every 10 s it logs max acceleration, angular velocity and pops. The Begin log
  `[RaceCam] L=.. interiorRight=.. zones:` should show L about 173.1, interiorRight true, non-overlapping zones.
- **Eyeball first:** the GO swing from the grid shot, the helix (no orbiting), Screw Lift 1, the Ski Jump miss, Screw Lift 2 (leader at the
  right of frame while the lift pulls the view), the winner and outro.
- **Tuned away from the plan** (to pass the thresholds): yaw 11 deg/s max, 0.9 omega; look accel 2.8, omega 2.5; focus accel 2.5; fit
  fill 0.58; Screw Lift focus pull 0.1; max frame time 0.25 s; the distance fit also counts the leader's real distance from the look point.

## Seed selection

None. Random per Play, logged. Record the seed of the published take.

## Render + publish

1920x1080 60 fps via Recorder. Thumbnail: tight shot of the pack at the ski jump, numbers legible. Title shape: a contest ("16 Marbles, 3 Laps, 1 Giant Machine: Which Number Wins?").
Chapters from the storyboard beats.

## Retro

`KnowledgeBase/retros/YYYY-MM-DD-mach-grand-prix.md`, update `/PROJECTS.md`. Open question: **does a racer-contest on the machine hold
attention better than the colour-fill contest, vs mach-001's 1,090 at day 4?**
