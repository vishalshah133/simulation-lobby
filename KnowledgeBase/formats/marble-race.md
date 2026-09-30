# Format: Marble Race (`race`)

Contenders race a track; first to the finish wins. The foundational format — everything else borrows
its camera work and leaderboard.

## Rules on screen

Stated in under 3 seconds: "First to the bottom wins." No other explanation needed.

## Build notes

- **2D** (spiral/serpentine drop tracks) is cheaper, reads better in 9:16, and is the default for Shorts.
- **3D** (banked tracks, funnels, jumps) is the long-form variant — more spectacle, harder to follow,
  so it needs a tighter camera and a persistent leaderboard.
- Track length should produce a 20–45s run for Shorts, 2–4 min per heat for long form.
- Randomize only the start jitter and any in-track hazards — from the seed. The track itself is fixed.

## Camera

Follow the *leader*, not a fixed point, with a slight lag so overtakes happen on screen. Cut to a wide
only when the pack is compressed. Never let the leader leave frame.

## What makes a good run

Lead changes. A race where marble 3 leads from start to finish is a bad video regardless of visuals.
Design tracks with **catch-up geometry**: funnels that compress the pack, forks with asymmetric risk,
a final section where position can still flip.

## Variations

- Heats + final bracket (becomes `elim`).
- Obstacle gates that eliminate rather than slow.
- Terrain that changes between laps.
- Viewer-named marbles carried across a series.

## Failure modes

- Marbles tunnel through thin colliders at speed → lower fixed timestep or thicken walls.
- Pack spreads out and the tail is never seen again → add funnels.
- Contenders indistinguishable at speed → add trails keyed to color.

## Open questions

- Ideal contender count for 9:16? Start at 8, test 12 and 16.
- Do trails help or clutter at 60fps?
