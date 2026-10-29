# Marble Machine (`mach`)

**Status:** `building` (mach-001, 2026-10-24) · **Shape:** 16:9 long form, 10-12 min · **Engine:** Unity 3D (PhysX)

## The format

A big closed-loop **marble machine** made of mechanical stations (lifts, drops, a risky jump, a sorter). Thousands of balls
run through it. The genre is pure satisfying spectacle (see `reference-channels.md` → *@ball_simulations*), and **we add a
contest spine** so a cold viewer has something to follow: the machine sorts balls by colour into seven glass tubes, and
the question is **which colour fills its tube first?**

## Why this shape

- @ball_simulations shows no-contest machine loops get browse views even on a sub-300-sub channel (V02 86K). MIKAN's
  *Factory* videos (84-115M) are the same "watch the machine work" lane at scale.
- Our story rules (`learnings.md` → *Story legibility*) need a question visible from frame one. The seven tubes are front
  and centre in the opening wide shot. The sorter's scanner flash and flap drop show *why* each ball lands where it does.
- A symmetric seven-way contest is a random walk, like a marble race. With ~480 balls per tube, the spread is around ±20,
  so lead changes keep coming until the end.

## Look

The reference style, not the house glass studio: a **light-grey seamless cyclorama**, neutral textured machinery (wood,
asphalt, terrazzo, brick, chrome, cast iron, copper, glass), and **balls as the only saturated colour** (seven glossy hues).

## Reusable pieces this format pays for

The station mechanisms (bucket wheel, Archimedes screw, wave floor, trapdoor run, tipping tower, vortex funnel, ski jump)
are self-contained `MachineStation`s posed from sim time. **Promote to `Shared` when a second format wants one** (e.g. a
lift as a `race` return leg). Don't copy them.

## Open questions (answer in the mach-001 retro)

- Does a no-character machine hold long-form attention once it has a contest spine? Check Studio retention at 1, 3, 6 and 10 min.
- PhysX cost with ~1,500 balls in motion: is the editor preview watchable, and how long does an offline 11-min render take?
- Do the stations hand off cleanly at 5,000 balls, or does the flood jam the machine (spectacle vs. a stall)?
- Shorts: does a single-station 9:16 crop carry on its own?

## Continuous track, 2x wide (2026-10-25)

- `chuteWidth` is 1.8 (was 0.9). Every connector and every station entry/exit is that width, built by
  `MachineConnectorBuilder.BuildPort` in the connector's exact profile (same walls, floor, materials), and chutes now start
  and end flush at the lips (`LipDrop/LipAhead/EntryClearance` = 0). Stations scale lateral sizes with it.
- Connector slope floor is 8 degrees (was 5), so the planner lifts the silo several metres; `HeightBudget` is 40.
- Silo storage cylinder, bucket-wheel radius, sorter lanes and the tube rack were **not** doubled (see the retro question:
  the sorter, ~5 balls/s per lane, is now the throughput bottleneck).
- Screw lift has two helical ribs per pitch on the glass and a high-friction tube (`GripPhysics`), so balls don't slide back.
- Trapdoor rows are leaf pairs hinged on the side walls; `tipperThreshold` is 200 for the 4x-capacity troughs.
- Untested in Unity: rebuild the scene and read the `[mach]` station table and warnings first.

## Sorter by collision filter, 100-ball contest, boosters (2026-10-25)

- The sorter has no moving valves: a ball ignores the gate plates of its own colour (`Physics.IgnoreCollision`, set once), so it
  falls through its tube's window; other colours roll on over solid plates. Windows are 0.7 m long.
- `tubeCapacity` is 100. Tubes are 0.35 m radius with a 0.9 m guide funnel above each (tube spacing 2.0 m).
- `RollerBank` boosters (kinematic rollers, `boosterSurfaceSpeed` 7 m/s) sit on the helix exit run and the trapdoor chute exit.
  The helix to sorter chute is planned at the hopper's 15 degree tilt so it is collinear with the sorter's entry port.
- Camera: opening zooms out to the whole track, glides in and orbits the silo (config `holdSeconds` is 20 to fit it); while the next
  stop waits for 100-150 balls the camera stays put and no tube check-in fires.
