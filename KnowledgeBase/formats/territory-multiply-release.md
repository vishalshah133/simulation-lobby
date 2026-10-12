# Format: Territory — Multiply or Release (`terr`)

Four colours own a tiled board, one quadrant each. Every colour has a cannon on its own corner of the
board and its own little Plinko box beside it. Balls rattle down the box into slots: **×2** doubles that
colour's ammo bank, **R** releases the whole bank through the cannon, **SPIN** triggers a jackpot reel.
Cannon shots flip enemy tiles to the shooter's colour and bounce back. A colour whose cannon tile is
captured is out. Last colour standing wins.

Reference: **@marblejamboree** (multiply-or-release territory, long form). Seen 2026-10-11 from a
screenshot; their view numbers not yet pulled — run `/youtube-research` before reading too much into it.

## Why it can hold a long-form video

- **Four sides to root for.** Viewers pick a colour in the first ten seconds; the comment section argues.
- **Comebacks are built into the economy.** Income comes from each colour's own Plinko box, *not* from
  territory, so a colour squeezed into a corner still rolls ×2, ×2, ×2, R and can retake half the map.
  The leader can't snowball by construction, which is the `wars` brief's #1 failure mode solved.
- **The board never stops moving.** Something flips every second; a release is a 5-20s barrage.
- **Two clocks running at once:** the slow territory swing and the fast bank counters. Tension comes from
  watching a big bank build up before its R lands.

## Design (terr-001, 2026-10-11)

- 3D, 16:9, long form. Target **10-15 min**, decision forced at 20 min, hard safety at 25 min.
- Grid 64 x 48 tiles in quadrants. Cannon sits a few cells in from its outer corner and sweeps a 90° wedge.
- Shots move in **grid space with our own maths** (`GridBouncer`): tiles are never colliders. A shot that
  enters a non-own cell paints it, reflects off that face and loses one health. That keeps 3,000 tiles
  cheap and the territory fully deterministic.
- Plinko boxes are **real PhysX** (tilted lacquer board, chrome pegs, glass cover). That is where the
  satisfying rattle comes from; the economy is whatever the physics drops into the slots.
- **Barrage cap:** a release fires at most `maxBarrageSeconds` worth of shots; bigger banks make
  heavier shots (more health, bigger sphere) instead of a longer barrage. This is `rom`'s weighted-marble
  trick, and it stops a 1e6 bank from becoming a 17-hour barrage.
- The spinner is decided in the sim (seeded draw when SPIN is hit, applied a fixed number of ticks
  later). The reel animation only shows a result that's already been drawn.

## Look decision (2026-10-11)

The first build used the dark glass/chrome studio look and read as unpolished next to the reference. `terr`
now matches the reference instead: a bright toy look, one flush table, boards tinted in team colour, chunky cannons,
a purple tiled floor and an always-on drum. That makes it an exception to the glass/chrome house style.
Boards are visually flat, and their slope is faked with tilted gravity (only board balls are rigidbodies). SPIN moved
from edge slots to a **golden ball**, which reads better because you watch it roll down. Spec: terr-001 plan, *Look v2*.

## Rules on screen

"×2 doubles it · R fires it all · lose your cannon, you're out". Shown during the setup hold, then
gone. After that the board explains itself.

## Readability

- A **stacked 4-way territory bar** across the top is the always-on scoreboard (the `wars` brief's
  retention device).
- A big bank number on each Plinko box, and "BLUE ×2 → 64" style callouts.
- The reel caption reads "BLUE is SPINNING". Eliminations get a full-width banner and a camera push.

## Tuning dials (in `TerritoryConfig`)

Grid size (length), the Plinko slot mix (how often R lands, which sets typical bank size), `bankCap`
(how big a swing can be), `shotHealth` / `maxBarrageSeconds`, cannon sweep speed, base inset (how
exposed a cannon is), escalation start.

## Failure modes to watch on the first run

- One colour eliminated in the first 2 minutes: base too exposed. Raise `baseInsetCells`, or use more R slots
  so banks stay small early.
- A stalemate that never resolves: escalation too late or too soft.
- An unreadable barrage: too many shots on screen. Lower the fire rate and let health carry the weight.
- Spinner fires constantly: the SPIN slots are too central. Keep them at the edges.

## Open questions

- Does the four-colour, comeback-heavy shape hold Studio retention past 5 min? That's the long-form test.
- Should the losing colours get pity mechanics, or is the pure economy enough?
