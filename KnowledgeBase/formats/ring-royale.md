# Format brief: `royale` — Color Rumble

**Tension type:** last one standing, four sides. **Origin:** user idea, 2026-10-21.
**First video:** [royale-001](../../plans/videos/royale-001-four-colour-ring.md).

## The shape

Four colours of marbles in one spinning ring. The ring has two marked arcs, opposite each other:
- **EXIT**, a gap. A marble that leaves through it is out.
- **x2**, a gold wall arc. A marble that bounces off it **doubles**: a clone of the same colour is born.

Every marble starts with a random velocity and keeps a constant speed. When a colour has no marbles
left it's eliminated. The last colour with marbles inside wins.

## Why it might work

- Four named sides to root for, visible from frame one (the `terr` / `surv-002` colour-team pull, in Short form).
- **Numbers going up** (x2 hits) and **near-elimination saves** (a colour down to 1 marble hits x2 and
  climbs back) are both on the *Hooks that reliably work* list. The x2 arc is the comeback engine.
- It reuses the proven `esc` / `mve` arena (spinning ring, one exit).

## Design rules

- **Balance favours growth early and the exit late.** With x2 wider than the exit, populations grow
  until a scheduled *sudden death* starts widening the exit. Without that escalation, a balanced ring
  can stall for minutes. The widening is on screen (the gap visibly opens) and captioned.
- **Clone only on x2 arc hits**, never on ball-ball hits or other wall hits.
- **Exits only through the opening.** A marble pushed through solid wall is a leak (the `mve` rule).
- **Never start from dead centre.** Marbles spawn scattered with a minimum spacing.
- **A body cap.** When the cap is reached, x2 still flashes but doesn't clone (logged, as in `mve`).

## Model (2026-10-21, collision-free, 40 seeds per config)

Script: session scratchpad `ringmodel.py`; rerun to retune. Ring r 4.6, speed 5, spin 30°/s.

| Per colour | Exit | x2 | Sudden death | Game length p10 / median / p90 | Peak marbles | Lead changes |
|---|---|---|---|---|---|---|
| 5 | 22° | 22° | 45 s, +1.5°/s | 44 / 64 / 75 s | 22 | 9 |
| 5 | 18° | 36° | 35 s, +2.5°/s | 52 / 59 / 67 s | 33 | 13 |
| **5** | **16°** | **40°** | **30 s, +3°/s** | **47 / 53 / 58 s** | **50** | **17** |

Equal arcs give a near-critical branching process: it drifts, and the population barely grows. A wide x2
is what makes the numbers climb. Ball-ball collisions aren't modelled. They push marbles off clean
chords, so expect more wall hits and shorter games in Unity.

## Open questions (fill in after royale-001)

- Does the share bar read at 480px with four colours, and does a 1-marble colour stay findable?
- Does sudden death feel earned, or like a cheat? (Caption it, and show the gap opening.)
