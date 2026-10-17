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

## Obstacles (built 2026-10-09)

Obstacle kinds are data on the track layout (`ObstacleKind`): pegs (pyramid, plinko), pinwheel, fork divider, bottleneck,
chicane, bumps. They exist to create the catch-up geometry this brief asks for: a bottleneck compresses the pack, a fork
splits it with unequal risk, spinners and pegs add chaos late enough to flip positions. Keep a gap beside any obstacle
narrower than a marble so nothing can wedge there. Open: which kinds actually produce lead changes (read `lead_changes`
across seeds before deciding).

### Obstacle gap rules (learned on surv-002 Lava Run, 2026-10-16)

The track kit is shared (`Shared/Tracks`), so these apply to every format that builds a course with it:
- **Peg-to-rail gaps must be ≥ 2.5 marble diameters (0.55 m at r 0.11).** A "too small to enter" gap doesn't make it
  safe. With the outer peg's centre farther from the wall than a wall-hugging marble's centre, the hit deflects the
  marble *into* the wall and pins it. A 4-peg grid on a 3 m track did exactly that. Use centred 3-peg rows (pyramids).
- **Lanes (fork splits) ≥ 0.6 m.** About 2 diameters lets two marbles arch side by side and jam.
- **Obstacles go on ≥ 4° slope.** At ~1.8° a marble that clips a peg stops and gravity barely restarts it, and the
  pack piles up behind 5-6 dense rows. Thin fields (3 rows, ~0.85-1 m pitch) and steepen under them.
- **No flat walls facing the flow (chicane baffles).** At a gentle slope a marble that hits a baffle square-on lies
  flat against it, gets no gravity toward the gap, sleeps and stops. A whole Lava Run pack died that way. Use pinwheels,
  or long tapered walls that meet the flow at a shallow angle (≤ ~35°).
- **Bank only what the speed needs: atan(v²/rg).** Slow packs (2-3 m/s) on wide arcs need under 1°. Lava Run's legs were
  banked 3-6° on a 1.5-4° slope, so the sideways tilt beat the downhill one: every marble rode the low wall, skipped the
  centred pegs, and wall friction stalled some of them.
- **Minimum ~3° slope wherever marbles travel.** At 1.5° a marble dragging along a wall stops.
- **Pens need a slope too** (~7°). On a flat pen floor the gate opens and nothing leaves.
The Lava Run builder logs these as build errors (`LavaRunSceneBuilder` obstacle report). Promote that check into
`Shared/Editor/Tracks` when a second course format needs it.
