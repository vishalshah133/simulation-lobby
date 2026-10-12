# race-001: Grand Prix (first long-form anchor)

**Status:** `built`, never run in Unity · **Format:** `race` (3D) · **Aspect:** 16:9 1920x1080 · **Scene:** `Race_GrandPrix_3D`
**Config:** `RaceConfig_001` + `RaceTrackLayout_GrandPrix` (both in `Assets/Settings/Configs/Race/`) · **Seed:** random per Play, logged

Anchor for the long-form track (`KnowledgeBase/channel-strategy.md` -> *Two-track plan*). Series template:
"The [Theme] Marble Race". This one is the plain studio edition; themed editions swap roster, track and palette.

## Look

Piano-black lacquer course with brass rail caps and pinstripes, chrome columns, a brass-and-glass finish gantry, on a pale
polished champagne floor. Twelve jewel-tone lacquered-glass marbles (Ruby, Emerald, Sapphire, Topaz, Amethyst, Pearl, Coral,
Aqua, Citrine, Rose, Cobalt, Amber) with a faint coloured trail. Everything is generated from primitives and swept meshes: no model imports.

## Run

Orbit the field at the gate -> 3-2-1 -> gate opens -> chase camera follows the front runner (0.9s hysteresis against flicker) ->
winner gets a held wide frame on the line -> chase resumes for the rest. Timer and sliding leaderboard on the left; reads at 1080p.

## Obstacles (course v2)

Data in `RaceTrackLayout.obstacles`, placed by section index + metres from its start, built by `ObstacleBuilder`:
rumble-strip **bumps**, a **peg pyramid** and a **plinko grid** in a widened garden (3.4m), a rotating **pinwheel**
(3 arms, 110 deg/s) after the right sweeper, a **fork** (divider with a shifted nose so one lane is narrower, bumps in
the narrow lane) after the helix, **chicane baffles** and a **bottleneck** (0.5m gap) on the long run, and a
**reverse pinwheel** (2 arms, -140 deg/s) on the home straight. Pinwheels are `KinematicSpinner`s stepped by the sim tick
with a seeded starting angle, so they vary by seed and replay from it. Track 269m, ~30m drop; the builder's time model
(~30s) ignores obstacles, so expect the real race to run longer.

## Build

Menu: **Simulation Lobby > Build Scene > Race_GrandPrix_3D**. Re-running replaces the scene, never the config, layout or materials.
The console prints track length, drop, a rough race-time model and a warning if the layout passes through itself.

## Checklist (first playtest)

- [ ] Builds clean; no pink materials (marble, trail, floor); no console errors on Play.
- [ ] Marbles fill the gate, settle, and release in a clump; none spit out of the pen.
- [ ] Lead changes happen (>= 2 per race is the bar; `lead_changes` is in the result metrics).
- [ ] No marble leaves the track (check `respawn_*` and `nudge_*` events in the result).
- [ ] Race time lands where intended; compare the real time to the builder's estimate and tune slopes in the layout.
- [ ] Marbles readable against black lacquer at 480px wide; trails help or clutter? (open question in `marble-race.md`)
- [ ] Reflections: probe + softbox cards give the glass and lacquer a sheen; floor is not flat grey.
- [ ] Sound: clacks, roll bed, finish notes, GO whoosh. Not harsh.
- [ ] Record 1920x1080; check HUD text size.

## Obstacle checklist

- [ ] Re-run the scene build once: the old layout asset upgrades itself to course v2 (the old default sections are replaced).
- [ ] Pegs: marbles bounce and scatter but do not stack up and stick (a stalled marble gets a `nudge_*` event).
- [ ] Pinwheels turn, hit marbles believably, and nothing wedges between an arm tip and the rail.
- [ ] Fork: both lanes get used across several seeds; the narrow lane is a real gamble, not a trap.
- [ ] Bottleneck makes a short queue, not a permanent jam.
- [ ] Lead changes went up compared with the plain course (`lead_changes`).

## Known gaps

No names above marbles yet (`WorldLabelLayer` exists). No fork or jump. No portrait HUD for Shorts cut-downs (the Shorts come from clips). Determinism of PhysX with a
mesh collider is untested: run `DeterminismVerifier` before relying on a seed to re-render.
