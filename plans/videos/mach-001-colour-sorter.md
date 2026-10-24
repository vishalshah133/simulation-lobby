# mach-001 — The Colour Sorter Machine

**Format:** `mach` (3D) · **Length:** ~11 min · **Aspect:** 16:9 1920x1080 · **Status:** `building`: wave 2 compiled, not yet run in Unity (2026-10-24)
**Brief:** `KnowledgeBase/formats/marble-machine.md` · **Scene:** `MarbleMachine_ColourSorter_3D`
**Config:** `Assets/Settings/Configs/MarbleMachine/MachineConfig_001.asset` · **Seed:** random per Play, logged
**Menu:** Simulation Lobby ▸ 3D Formats ▸ Marble Machine (mach) ▸ Colour Sorter (mach-001)

Inspiration: @ball_simulations' Blender loop machines (`reference-channels.md`). We borrow the **style only**: a light-grey
cyclorama, neutral textured machinery (wood, asphalt, terrazzo, brick, chrome, glass, cast iron) and saturated glossy balls.
**Every station and the layout are original.**

## Premise

16,800 balls in seven colours run through a giant marble machine, and a sorter drops each one into its colour's glass tube.
**Which colour fills its tube first?**

## Storyboard (write before any code)

| Beat | Time | On screen | Caption / counter |
|---|---|---|---|
| Hook | 0:00-0:08 | Wide shot of the whole machine. The full glass silo on the left, **seven empty tubes front and centre**, each with its colour band and fill line | Top: **WHICH COLOUR FILLS ITS TUBE FIRST?** Right: 7-row leaderboard, all 0 / 480 |
| Valve | 0:06 | The camera pushes in on the silo. The star feeder starts turning and the first balls drop | |
| Act 1: first lap | 0:10-1:40 | The camera flies alongside the first batch, station by station: Peg Wall → Vortex Funnel → Wave Floor → Bucket Wheel → Ski Jump → Tipping Tower → Trapdoor Run → Screw Lift → Helix → Sorter. Each mechanism is revealed once, in flow order | |
| First sort | ~1:30 | The scanner arch flashes a ball's colour and its flap drops it into the tube. **First payoff** | That tube's row ticks to 1 |
| Act 2: steady | 1:40-5:30 | The release rate goes up. The tour continues, with a cut back to the tubes about every 45-60 s and on every lead change. Ski Jump misses fall into the net, roll to the cannon and get **fired back into the silo** | Leaderboard re-sorts live |
| Midpoint twist: FLOOD | ~5:30 | The silo's flood gate slams open and ~1,200 balls pour out at once. The machine strains: the bucket wheel overflows, the Ski Jump rains misses, the sorter queue backs up | Banner: **FLOOD GATE OPEN** |
| Act 3: the race tightens | 6:00-9:30 | The tubes fill past the halfway mark. The camera checks the tubes more often, and lead changes now matter | |
| Finale | ~9:30-10:30 | The leader passes 85%. The camera locks on the tubes, with a slow push along the top 3 | Rows past 85% pulse |
| Payoff | ~10:30 | The first tube hits its fill line. The chrome ring lights up, confetti, a push-in on the winning tube | **<COLOUR> WINS!** + final counts |
| Outro | +14 s | Slow pull-back to the opening wide shot. The machine keeps running | |

Read it as a cold viewer with the sound off:
- [x] The question names things that are **visible from the first frame**: seven coloured tubes, front and centre, in the opening wide shot.
- [x] Every change that causes the result **happens on screen**: the scanner flash and flap drop are animated and sit right above each tube.
- [x] Every beat raises the stakes: release rate → flood → finale lock.
- [x] The only number shown is the answer (tube count vs. capacity). There are seven real sides, so a leaderboard is legitimate.
- [ ] *Why* line: not a curiosity format. The ending says who won and by how many.

**Shorts cut-downs (later, no extra build):** one 9:16 crop per station, plus the finale.

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md): `SimulationRunner`, `SeededRandom`, fixed step 1/100
- [ ] **REUSE** [material-contact-audio](../blocks/material-contact-audio.md): procedural, nothing sampled
- [ ] **REUSE** [collision-safety](../blocks/collision-safety.md): kill plane → recycle, stall nudge
- [ ] **REUSE** `LeaderboardHud` + `CaptionHud` (Presentation/UI), `ConfettiBurst3D`
- [ ] **BUILD** `TourCameraRig` (Presentation/Cameras): format-agnostic, glides between shot poses with dwell and no hard cuts
- [ ] **BUILD** `ClatterBed` (Presentation/Audio): granular many-ball clatter driven by an activity level
- [x] **NOT used:** `studio-look-3d` (dark glass studio). The reference's look wins (memory: *Exception 2026-10-11*). Light-grey studio is format-local for now

## Stations (flow order)

Ball radius 0.09 m (diameter 0.18). Connectors and station entries are `chuteWidth` 0.9 m wide (5 diameters). Each station
is built in local space with **the entry anchor at the local origin and flow along local +Z**, then placed by the scene builder.

| # | Station | Kind | Owner | Mechanism (the exciting bit) |
|---|---|---|---|---|
| 1 | **Silo** | logic | WP1a | A glass cylinder (r 1.6, ~4 m tall) on chrome legs with a terrazzo cone. A **star feeder** meters the flow, and a **flood gate** slide dumps everything at the midpoint |
| 2 | **Peg Wall** | drop | WP2b | A near-vertical (75°) glass-fronted pachinko wall, 2 m × 3 m, with hex chrome pegs and two counter-rotating pinwheels. It funnels to a bottom exit |
| 3 | **Vortex Funnel** | drop | WP2b | A hyperbolic gravity-well funnel (rim r 1.6). Balls enter tangentially, spiral faster and faster, and drop through the throat |
| 4 | **Wave Floor** | transport | WP2a | A 14 × 5 bed of square pistons rising in a **travelling sine wave**, so balls surf along the floor |
| 5 | **Bucket Wheel** | lift | WP2a | A 3 m radius ferris wheel with 12 level-hanging copper buckets. They scoop from a pit, and a cam tips each one at the top |
| 6 | **Ski Jump** | drop + risk | WP2b | An inrun, a kicker, a **1.6 m gap**, then a catch crate. **Misses fall into a net** and roll to the recycle exit (→ cannon) |
| 7 | **Tipping Tower** | drop | WP2b | 4 tiers of seesaw troughs, alternating sides. Each one tips when 25 balls are in it, so the cascade goes down the tower |
| 8 | **Trapdoor Run** | drop | WP2a | Balls pile up on a long tray. Ten trapdoors open in a **travelling wave** every 5 s and the pile drops to a brick chute below |
| 9 | **Screw Lift** | lift | WP2a | An **Archimedes screw** inside a glass tube, inclined ~45°, ~7 m long. Balls climb in a visible helix |
| 10 | **Helix Slide** | drop | WP2b | A banked corkscrew, 2.5 turns around a chrome column |
| 11 | **Sorter** | logic | WP1b | A rotating singulator disk feeds a single-file lane. A **scanner arch** flashes each ball's colour, and **7 flaps** drop it into its colour's tube. Unmatched balls → recycle |
| 12 | **Tube Rack** | logic | WP1b | Seven glass tubes (r 0.6, ~2.4 m) with a colour band, a world-space TMP name decal, and a chrome **fill-line ring** at capacity |
| R | **Cannon** | logic | WP1a | A cast-iron cannon. Recycled balls (jump misses, sorter leftovers, lost balls) are **fired in arcs back into the silo top** |

Heights (approximate, the scene builder finalises them so every connector slopes 6-20° down): silo valve ~4.5 → peg wall →
funnel → wave floor (y ~0.35) → wheel lifts to ~6.5 → ski jump → tipping tower → trapdoor run (y ~0.3) → screw lifts to ~6.5
→ helix → sorter lane ~3.0 over the tubes (bases on the floor). Footprint ~40 × 25 m, arranged as a loop so the opening
wide shot sees all of it with the silo, sorter and tubes nearest the camera.

## Rules (simulation)

- All 5,000 balls exist from tick 0, pre-placed in a hex lattice inside the silo with colours shuffled by the seed. No spawning.
- Release follows `config.stages`: Hold (feeder off) → Trickle 4/s → Steady 6/s → **Flood** (gate open ~35 s) → Steady.
  The real rate is physical, and the feeder rpm is derived from the target.
- The sorter opens flap *i* only for a ball of colour *i* inside gate window *i*. Counting happens in the tube, once per ball.
- The first tube to reach `tubeCapacity` (480) wins. Finale starts when the leader reaches 85%.
- Recycle: ski-jump misses, sorter leftovers and anything below the kill plane go to the cannon hopper, and get fired into the silo.
- All mechanisms are **kinematic and driven from sim time only** (`MovePosition`/`MoveRotation` in `Tick`). The tipping
  tower's tip is triggered by a deterministic overlap count, not a hinge joint.

## Config + tuning targets

| Knob | Start at | Note |
|---|---|---|
| `ballCount` | 5000 | The title number. Raise once the frame-time check passes |
| `tubeCapacity` | 480 | Sets the video length. Winner should land around 10:00-10:45 |
| `stages` | 6 s hold, 4/s, 6/s @120 s, flood @330 s, 6/s @365 s | Sorter throughput (~6/s) is the bottleneck |
| `skiJumpGap` | 1.6 m | Target 10-20% misses. 0% is dull, and 40% floods the cannon |
| `tipperThreshold` | 25 | Tips every ~4 s at steady flow |
| `waveAmplitude / Length / Frequency` | 0.25 / 2.0 / 0.6 | Must carry balls without launching them |
| `fixedTimestep` | 1/100 | |
| `maxDurationSeconds` | 900 | A timeout means retune `tubeCapacity` |

## Look

A light-grey seamless cyclorama (gradient #DCDCDE top → #C4C5C7 floor), soft key light with soft shadows, a fill light, a rim
light and a reflection probe. Neutral, textured machine materials (procedurally generated textures, saved under
`Assets/Art/Generated/MarbleMachine/`). **Balls are the only saturated colour**: seven glossy plastic hues (Red, Orange,
Yellow, Green, Blue, Purple, Pink). The tubes' colour bands are the only other saturated colour.

## Sound

- **Clatter bed** (`ClatterBed`): granular clicks whose density and brightness follow ball activity near the camera.
  This is the reference's signature sound.
- **Sorter notes:** each sorted ball plays its colour's pentatonic note, so the sorter "plays" a seed-driven melody.
- **Mechanism one-shots** from station pulses: bucket tip, trapdoor clacks, tipper slosh, cannon thump plus whoosh, flood roar.
- A quiet procedural pad bed. Win: a rising chord plus crowd (`ProceduralCrowd`).
- Mix: mean around −14 LUFS-ish. The reference runs very loud (mean −8.6 dB), so don't chase that. Limiter on the master.

## Code layout + contracts

Runtime `Simulations/MarbleMachine/` (asmdef `SimulationLobby.Simulations.MarbleMachine`: Core, Shared, Presentation).
Editor `Simulations/MarbleMachine/Editor/` (asmdef `…MarbleMachine.Editor`). **Contract files, written before the build
and not to be edited by work packages:** `MachineConfig.cs`, `MachineStation.cs`, `MachineBall.cs`, `IMachineState.cs`,
`Editor/MachineKit.cs`, `Editor/MachineGeometry.cs`. A work package that needs a contract change reports it rather than editing.

| WP | Owns | Summary |
|---|---|---|
| WP1a | `MachineSimulation.cs`, `Stations/SiloStation.cs`, `Stations/CannonStation.cs`, `Editor/Stations/SiloBuilder.cs`, `Editor/Stations/CannonBuilder.cs` | Sim core: ball lattice, release schedule, phases, counting via TubeRack, recycle, safety, result |
| WP1b | `Stations/SorterStation.cs`, `Stations/TubeRack.cs`, `Editor/Stations/SorterBuilder.cs`, `Editor/Stations/TubeRackBuilder.cs` | Singulator, scanner, flaps, tubes, fill rings, name decals |
| WP2a | Bucket Wheel, Wave Floor, Trapdoor Run, Screw Lift (runtime + builders) | Kinematic lifts and transport |
| WP2b | Peg Wall, Vortex Funnel, Ski Jump, Tipping Tower, Helix Slide (runtime + builders) | Drops |
| WP3 | `Editor/MachineSceneBuilder.cs`, `Editor/MachineStudioEnvironment.cs`, `Editor/MachineMaterials.cs`, `Editor/MachineConnectorBuilder.cs` | Menu, layout, connectors, materials and textures, studio, HUD canvas, wiring |
| WP4 | `Presentation/Cameras/TourCameraRig.cs`, `Presentation/Audio/ClatterBed.cs`, `MachinePresenter.cs`, `MachineCameraDirector.cs`, `MachineAudio.cs` | Camera, HUD binding, sound |

## Built (2026-10-24)

Six parallel packages, compiled clean together (`dotnet build`), not yet run. Defaults changed from the plan:
`singulatorDegreesPerSecond` 270 (8 paddles → 6 balls/s), `screwDegreesPerSecond` 330 (120 gave ~2 balls/s). Peg pitch widened to
4.6 r (3.2 r jammed on paper).

- **Layout:** mirrored from the plan. The silo is front-right and the loop runs: back row (wave floor, wheel, ski jump) → left
  edge (tower, trapdoors) → front row (screw, helix, sorter → tubes). The cannon sits between the tubes and the silo. Heights are
  planned backwards from the sorter at ~6° chute slopes. The silo top may come out tall (~15-18 m) because the two lifts
  (wheel +4.8 m, screw +5.3 m) are short against the drops between them. Check the logged station table first.
- **First things to eyeball:** the console station table and warnings · the opening wide shot · the bucket-wheel scoop and
  pour · the wave floor carrying balls · the screw mouth · the vortex tangential entry · ski-jump misses reaching the net
  and the cannon · sorter single-file and flaps · fill-ring height vs. 480 balls.
- **Known look gap:** textures stretch on primitive cubes (UV 0-1 per face). Shadow distance is capped at 50 m by
  `PC_RPAsset` (left unchanged).

## Wave 2 (2026-10-24): 16,800 balls, compact machine, readable opening

The user's first play: the opening was too far out to read, the light clipped to white, the motion was choppy, and they wanted
>15,000 balls. Edit-mode diagnostics also found the silo feeder jamming after ~31 balls, silo spill landing in the cannon
below it, a bucket wheel that never handed off, and a ~26 m tall layout on stilts. Five parallel packages rebuilt it:

- **Scale:** `ballCount` 16,800, `ballRadius` 0.065, `tubeCapacity` 1,000, release 6/s → 13/s plus the flood,
  `sorterLanes` 3, `tipperThreshold` 60, `sorterFlapOpenSeconds` 0.10, `cannonIntervalSeconds` 0.2, `waveAmplitude` 0.2.
- **Perf:** frozen silo and settled tube balls have no body and are drawn GPU-instanced (80-tri mesh). Moving balls are capped
  at 2,000 (`maxActiveBalls`), normally < 1,500. The flood plate eases shut if the tray backs up.
- **Silo:** r 2.0 with a 38° cone, a 6-diameter throat, a 6-vane feeder (about 30 balls per pocket) with a measured-rate trim and an
  anti-bridging bar. The top is 5.42 m above the exit lip.
- **Lifts:** the bucket wheel has r 4.0, 16 open buckets, reversed rotation and a side apron hand-off (+6.3 m, ~35/s). The screw lift
  rises +6.1 m (~34/s).
- **Sorter:** 3 lanes, a splitter and a shared paddle wheel (~18/s), and per-lane flaps with a block rule. The tube fill line is at 1.79 m.
- **Layout:** shorter drops (3-tier tower), 5° connectors, the tubes and silo side by side at the front, and the cannon behind the
  ski jump. The expected max height is ~11-12 m (the `[mach] Station table` log has the real number).
- **Look:** a medium opening on the tubes and silo, then a push-in and a glide to the valve. Wide shots are 3/4 and capped, and
  station shots are within 9 m. Key/fill/rim are 0.85/0.25/0.35, with ambient about a third of before. The post profile now updates on rebuild.

The committed scene is still the wave-1 build. Rebuild it from the menu before judging anything.

## Acceptance criteria

- [ ] Menu build makes the scene with no console errors and no pink materials
- [ ] Every station hands balls to the next without spills (track `lost` in the result: < 1% of balls)
- [ ] No permanent jam anywhere (stall nudges < 50 per run)
- [ ] Winner lands at 10:00-11:30. Lead changes ≥ 5 (`lead_changes` metric)
- [ ] Ski Jump miss rate 10-20%. The cannon visibly lands balls in the silo
- [ ] Opening frame reads at 480 px wide: the seven tubes, the colours and the question
- [ ] Same seed twice → same winner and the same counts at the same tick (`DeterminismVerifier`)
- [ ] Editor preview ≥ 20 fps with ~1,500 balls moving. The Recorder render runs offline at 60 fps regardless

## Seed selection

None. Random per Play, logged (house rule). Record the seed of the take that gets published.

## Render + publish

1920x1080 60 fps via Recorder (`captureFramerate` offline). Thumbnail: seven tubes, two neck-and-neck near the line, the
machine blurred behind. Title shape: a stake, not a bare number (e.g. *"5,000 Balls, 7 Colours: Which Fills Its Tube
First?"*). Chapters from the storyboard beats.

## Retro

Write `KnowledgeBase/retros/YYYY-MM-DD-mach-colour-sorter.md`, update `/PROJECTS.md`.
The open question this video answers: **does a no-character machine loop hold long-form attention once it has a contest spine?**
