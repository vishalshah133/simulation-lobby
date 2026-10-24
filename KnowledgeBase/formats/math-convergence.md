# Format brief: `conv` — Math Convergence

**Tension type:** prediction (*will they all ___?*). The answer is mathematically certain, and it surprises.
**Origin:** reference-backed. MathCa (`reference-channels.md`, pulled 2026-10-14) did 12.2M / 902K / 474K in its
first 2 weeks with this shape, at a 1.2-1.9% like-rate. It's one young channel, so this is a lead.
**First video:** [conv-001](../../plans/videos/conv-001-ellipse-focus.md).

## The shape

Many identical bodies start together. The viewer is asked whether they'll all meet, sync or come back. A
geometric fact says yes (or, for the twist, no). The payoff is **seeing the theorem happen**, not having it
explained: MathCa's explainer-only uploads were its weakest (5-13K).

## Why it's different from what we have

| Format | Tension |
|---|---|
| `esc`, `mve`, `brk` | Escalation: will it break the threshold before the threshold breaks it? |
| `race`, `wars`, `terr`, `cric` | Competition: who wins? |
| `evolve` (idea) | Emergence: what does this become? (no known answer) |
| **`conv`** | **Prediction: will they all ___? (a known, surprising answer)** |

## Design rules

- **One question, two visible points, one visible twist, then the reason.** (Revised 2026-10-14, after the 4-round
  "teach → surprise → stretch → break" first cut proved incomprehensible; see `learnings.md` → *Story legibility*.)
  Show where they start and where they should arrive from frame one, and name both in the question. Change one
  thing *on screen* (animated; while red is off the focus, a ghost marks the focus and fades out before launch) and show the answer fail. Put it back and show it
  work. End by naming the idea.
- **Analytic, not physics-engine.** The answer is exact only if the motion is. Point particles with
  closed-form reflection in doubles, stepped in `Tick(fixedDelta)`. **No Rigidbody2D, no ball-ball
  collisions** (they all pass through one point by design).
- **Seed is cosmetic.** The outcome is set by geometry, so the seed picks only palette start, backdrop
  theme and a tiny global angle offset. Still logged, still re-renderable.
- **Colour by launch angle** (hue wheel). Converging = colours collapse to one point. Diverging = the
  rainbow sorts into bands. Both read at 480px.
- **Short trails** show the paths and, when the rule breaks, draw the caustic (a hidden-shape reveal for free).
- **A counter carries the payoff:** `AT THE POINT n / N`. A full house reads as a number, not just a picture.
- **Name the special spot in plain words, not the term** (a "MAGIC POINT" decal on the ghost). The end card names the
  real term ("IT'S CALLED A FOCUS"), so the label explains what to look at without spoiling the reason.
- **No versus score.** The counter is the answer. A tally of rounds is bookkeeping in a prediction format.
- Length is whatever the rounds need. The reference sits at 31-34s. Don't pad it.

## Candidate topics (each one config/scene work on the same kernel)

1. **Ellipse focus**: fire from one focus, they all hit the other at once (path length 2a). *conv-001.*
2. **Cycloid / tautochrone**: released at different heights, they all reach the bottom together. Needs a
   1D arc-length integrator, a second kernel.
3. **Pendulum wave**: they drift apart and line up again at period T. Analytic.
4. **Circle polygon orbits**: one ball at angle 360/n closes an n-gon, an irrational angle never closes.
5. **Parabolic mirror**: parallel rays all pass through the focus. Same billiard kernel, open boundary. *conv-003* ([plan](../../plans/videos/conv-003-parabola-focus.md)): a row falls in, all cross the focus at the same instant (every path is the same length), and rise out as a flat line. Constant speed, not gravity: dropped balls under gravity curve after the bounce and miss the focus. The row is gap-free and symmetric. Balls dropped within |x| < 4f²/W still hit the focus on time, but they exit over the rim diagonally rather than rising flat.

### Wider maths backlog (added 2026-10-17)

Not all of these fit the "will they all meet?" kernel. Each is tagged with the kernel or format it needs. A topic that
needs its own motion model and its own HUD becomes its own format (as `clack` did), not a flag on `conv`.

6. **Colliding blocks count π**: a heavy block slides into a light one against a wall, and the clack count spells
   π's digits (3, 31, 314…) as the mass goes ×100. *Own format: `clack`, first video
   [clack-001](../../plans/videos/clack-001-pi-blocks.md), brief [pi-clacks.md](pi-clacks.md).*
7. **Bell curve drop**: 1,000 marbles through a peg board pile up into a bell curve. Guess the shape. PhysX or
   analytic left/right steps; closest to the proven peg-board genre.
8. **Monty Hall lanes**: two ball streams play the 3-door game, one always stays and one always switches, and the
   switch pile ends up twice as tall. Seeded counting, no physics.
9. **Birthday collision**: 23 balls each drop into one of 365 slots. Do two share? About half the time, over many
   rounds.
10. **Buffon's needle**: sticks rain on striped floorboards, and the share that crosses a line converges on 3.14159.
11. **Brachistochrone race**: straight ramp vs steep dive vs curve, and the curve wins although it's longest. A
    1D arc-length integrator, shared with the tautochrone (topic 2). *Own format: `brach`, first video
    [brach-001](../../plans/videos/brach-001-fastest-path.md), brief [brachistochrone.md](brachistochrone.md).*
12. **Butterfly effect**: 100 pendulums (or billiard balls) start 0.0001° apart, move as one, then burst into
    chaos. Guess when.
13. **Chaos game**: a dot hops halfway to a random triangle corner each step, and 10,000 hops draw the Sierpinski
    triangle. Guess the shape.
14. **Collatz drop**: numbered balls go up on odd (×3+1) and down on even (÷2) on a tall tower, and all of them
    reach 1.
15. **Random walk escape**: 100 balls start mid-board and step left or right at random. How many reach the edge in
    1,000 steps? Fewer than expected (√n).

## Open questions

- Does our audience (escalation-shaped so far) take a calmer, prediction-shaped Short? This is the experiment.
- Is "math" a channel-identity risk for a *simulation lobby*? Our view: no, if it's framed as a physical
  sim with a question (balls and walls), not a lesson.
