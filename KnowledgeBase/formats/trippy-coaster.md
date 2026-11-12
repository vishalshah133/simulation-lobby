# Format brief: `trip` — Trippy Coaster

**Tension type:** spectacle and hypnosis. No contest, no riders, no scares: the stake is "can you take the whole ride", with the
world changing every ~40 s so the viewer always has a next thing to see.
**Origin:** user brief 2026-11-06; backlog "Abstract World" coaster (14.5M). Sibling of `coast` but deliberately not a reskin.
**First video:** [trip-001](../../plans/videos/trip-001-trippy-coaster.md). Status: `built, not yet played` (3D, long form with Shorts cuts).

## The shape

One cart (the viewer's), first person, one rail of ~24 km and about 5 minutes, at 125-400 km/h. The ride crosses **seven worlds**, each a different
look painted by one procedural shader, and a world title card announces each. The HUD is only the hook question, the world name,
`WORLD n / 7`, speed and a progress bar with a notch per world.

| # | World | Wall | Pattern | Props | Course |
|---|---|---|---|---|---|
| 1 | NEON GATE | open | glow | neon gate rings, pillars | station hold, 35-degree lift, crest, a 78-degree plunge (~500 m), double vertical loop |
| 2 | UV TUNNEL | circle | spiralling stripes | fluorescent tube cages | banked S-curves, a single loop, airtime hills, a 60-degree drop |
| 3 | KALEIDOSCOPE | 6-lobe star | polar fold | crystals seated on the lobes | banked descending helix (< 1 turn) |
| 4 | INVERTED SKY | wide slab | neon grid | floating spheres, arches | two camelback drops + a full barrel roll |
| 5 | LIQUID LIGHT | rippling tube | plasma | glowing orbs | serpentine airtime hills, a double loop, a plunge |
| 6 | FRACTAL BLOOM | open | glow | recursive octahedra | drop, second double loop, spiral descent |
| 7 | SINGULARITY | closing funnel | polar fold | shrinking rings | climb, hang, the longest plunge, a final loop, brake |

## How it is built (and why it is cheap)

- **Everything moves in the shader.** `Assets/Shaders/Trip/TripPlasma.shader` is unlit, and every pattern, palette scroll and
  "breathing" prop is a pure function of the global `_TripTime` (= simulation time). Geometry is static, merged into ~100 m chunks.
- **Speed.** Lift 115 km/h, cap ~396 km/h, and after the first drop the cart is held above 137 km/h (`minSpeed`). Pattern periods are tens of metres on purpose: at these speeds a short period is a strobe.
- **Course as feelings.** `RailCourse` (Shared) turns a list of `CourseKey`s (length, pitch, turn rate, bank) into rail points. A bank
  of 360 is a barrel roll; a pitch run to 360 with a `linear` key is a vertical loop, and `drift` offsets its exit sideways so the track never crosses itself. The scene builder warns if any stretch of rail passes through another stretch's tunnel. `TrippyCoasterDefaults` is the whole storyboard as data.
- **Shared, not copied.** `Shared/Coaster/` holds `CoasterRail`, `ICoasterRail`, `RailCourse`, `RideDynamics`; `coast` now uses the
  same rail. (`coast` still has its own scare-aware `CoasterDynamics`; promote the rest if a third coaster appears.)
- **Lights and post.** Three palette-coloured point lights orbit the cart (lighting the cart body). `TripPostFx` drives a runtime URP
  Volume: bloom, chromatic aberration, lens warp, hue drift, all scaled by each world's `intensity`.
- **Sound:** all procedural (`TripAudio`): per-world pad + pentatonic arpeggio + sub pulse at the world's tempo and root note,
  cross-faded with a rising sweep on each world change, wind with speed, chain ticks on the lift.

## Design rules

- **Flash safety is a hard rule.** No strobing above 3 Hz, no full-frame white. Palette scroll under 0.15 cycles/s, hue shift peaks
  near 5 deg/s, lens warp under 0.1 Hz, pulses under 1.5 Hz. A warning card opens the video.
- **Readable at 480 px.** The HUD sits in the top and bottom panels; the middle stays clear. Check both 9:16 and 16:9.
- **Deterministic.** The seed only drives the cart's wobble phases; the ride is otherwise identical, and shader patterns follow sim time.
- **The first 3 s is an anticipation beat**: the cart waits in the Neon Gate while the warning card shows, then the lift ticks.
- **Shorts cuts**: the 1-2 min cut is the lift+drop, Kaleidoscope helix, barrel roll and Singularity plunge.

## Open questions (fill in after the first Plays)

- Does a ride with no stake hold retention through acts 3-5, or does it need a counter (e.g. "worlds survived") or a mid-ride twist?
- Which world is the clip people share? Double down on it for the Shorts cut.
- Does bloom + chromatic aberration at 1080x1920 stay legible on a phone, or is the intensity too high?
- Is the 4-minute length right, or do the worlds want to be 25 s each for a 3-minute ride?
