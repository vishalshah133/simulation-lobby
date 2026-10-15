# Format brief: `mve` — Multiply vs Escape

**Tension type:** threshold (*will it fill up?*), driven by two feedback loops racing each other.
**Origin:** an original idea from the experiment lane (2026-10-13), not taken from a reference channel.
**First video:** [mve-001](../../plans/videos/mve-001-multiply-vs-escape.md).

## The shape

The rotating-gap circle from `esc`, with one rule change: every wall bounce **clones** the ball, and every
escape **widens** the exit. Growth is exponential on both sides. The swarm fills up fast, but each escape
makes the next one likelier. A round ends FULL (fill target reached) or EMPTY (everyone out).

## Why it might work

- It keeps the shape that holds the ~1K floor (one arena, one yes/no question, a spinning exit). Only the
  mechanic is new (`channel-strategy.md` → *Shorts mix*, rule 3).
- **Numbers going up on screen** and **near-elimination saves** are both on the *Hooks that reliably
  work* list. A round that reaches 480/500 and then drains is the money shot, and the tuning aims for it.
- The wall disappearing as the swarm pours out is a satisfying visual of its own.

## Design rules

- **Real way to lose on both sides.** Tune `gapDegreesPerEscape` so neither outcome dominates. Modelled
  without collisions, the knife edge sits near *peak inside ≈ 360·(x0 − ln x0 − 1)/δ*, where x0 = start
  gap/360 and δ = degrees per escape. Set the fill target close to that.
- **Clone only on wall hits**, never on ball-ball hits. In a crowd, ball-ball cloning runs away and the
  question disappears.
- **Exits only through the opening.** A ball pushed through solid wall is a leak, not an escape.
- **Never start from dead centre** (the `brk` lesson).

## Open questions (fill in after mve-001)

- Does crowding shift the balance much compared with the model?
- Does the swarm stay readable at 480px wide, or does it turn into noise?
