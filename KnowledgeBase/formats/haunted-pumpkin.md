# Format brief: `hall` — Escape the Haunted Pumpkin

**Tension type:** race against a clock (*can the swarm get out before the candle dies?*), with a growth loop.
**Origin:** Halloween experiment (2026-10-31): `brk` ring-by-ring progress + `mve` split-on-hit + a guess-the-song hook.
**First video:** [hall-001](../../plans/videos/hall-001-haunted-pumpkin.md). Status: built, not yet played.

## The shape

Neon-green ghosts bounce inside a jack-o'-lantern of 3 nested rings (bone, candy corn, cobweb). A ghost touching a
segment damages it; at 0 hit points the segment breaks and, with `cloneChance`, the ghost splits in two (seeded 15-55 deg
turn, never on ghost-ghost hits). A ghost that gets out through the outer ring becomes a bat and flies up. The candle
melts over `timeLimitSeconds`; fewer than `batsToWin` bats out when it dies and the pumpkin wins.

HUD: hook caption on top, `GHOSTS n`, `RINGS BROKEN n/3`, `BATS OUT n / 40` at the bottom, a candle bar on the right
edge (clear of the arena), the result (`THEY ESCAPED!` / `THE PUMPKIN WINS`) and the one-line why. Procedural audio only:
one note of *Toccata and Fugue in D minor* per break, a creak when a ring opens, a low drone under it all.

## Design rules

- **Real way to lose.** `cloneChance` stays below 1 (cloning on every break can never lose, see `mve`). Balance dials:
  `cloneChance` and `batsToWin`; target ~50% wins and the clock within ~10 s of expiring in a good share of seeds.
- **Holes only from physics.** A ghost may only cross a ring through a broken segment (or a neighbour). Anything else is a
  leak: removed uncounted, warned about, counted in the `leaks` metric.
- **Never launch from dead centre** (`brk`); the innermost ring breaks on the first touch (instant hook).
- **Hard body cap** (`bodyCap` 400, `BodyPool2D`). Past it a split adds weight to the parent body instead of a new one.
- Rings count as "broken" when a ghost first passes through them, innermost first.

## Code map

`Assets/Scripts/Simulations/HauntedPumpkin/`: `HauntedPumpkinConfig`, `HauntedPumpkinSimulation` (ISimulation, `FormatSlug` hall),
`GhostBody` (per-ghost ring contacts), `HauntedPumpkinPresenter` (LateUpdate, read-only), `HauntedPumpkinHud` (uGUI + TMP),
`Editor/HauntedPumpkinSceneBuilder`. Reuses `SegmentedRing2D`, `BodyPool2D`, `ConstantSpeed2D`, `ShatterBurst2D`,
`BounceMelodyPlayer`, `ProceduralToneBank`. No reference to `Breaker` or `MultiplyEscape`. `GhostBody` is a near-copy of the
`SwarmBall` idea (own contact flag, wall = any kinematic body); if a third format needs it, promote it to `Shared`.

## Open questions (fill in after the first Plays)

- Win rate and how close the clock gets; is `batsToWin` 40 right for a 55 s candle?
- Do the narrow corridors (0.85 wide, ghosts 0.36) jam, or leak under the 400-body crowd?
- Is the swarm readable at 480 px, or does the green halo turn into soup?
- Does a themed spin on proven mechanics beat the ~1.3K Shorts band, or is it the same floor with a seasonal label?

## Learned (2026-10-31, first recordings)

- **A bat target was unreachable.** Ghosts are only born on breaks, so the swarm is capped near
  1 + cloneChance x total segments (~43 for 70 segments at 0.6, against a 60-bat target; ~22 vs 40 with 3 rings).
  The pumpkin emptied early, the counter stalled short of the target, and the run sat waiting for the candle.
  **Rule now:** the first ghost through the outer ring wins; the rest pour out as bats during the finish hold.
  HUD shows `GHOSTS n` (only rises until the breakout), `RINGS LEFT n` / `LAST RING!`, the candle, and `BATS OUT n` only after the breakout.
- **Pacing lives in the outer rings' hit points.** Point model (no ghost-ghost collisions, 24 seeds): 1/2/3/3/4 broke out in
  ~16 s (matches what was seen); 1/3/6/11/16 at split 0.5, speed 3 -> 45-59 s, median 50, rings opening ~2/6/15/31/51 s.
  Earlier models ran 1.2-2.7x optimistic against Unity, so the 65 s candle is a guess to be checked against the
  `[hall] ring n open at ...` lines (logged live, so even a stopped Play reports pacing).
