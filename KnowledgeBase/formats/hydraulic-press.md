# Format brief: `press` — Hydraulic Press (split on crush)

**Tension type:** threshold with a number answer (*how many before it stops?*), escalated by an answer ladder.
**Origin:** the experiment lane (2026-10-20), shaped on @satisfying2dsims' multiply + crusher hits.
**First video:** [press-001](../../plans/videos/press-001-split-press.md).

## The shape

A press lowers onto a ball. Every ball holds a fixed load, and a crushed ball splits into two smaller ones. The
press stops when enough balls share its weight. Make the press stronger and ask again. The answers form a ladder,
and the growth rate (~4× for 2× force, if the harness confirms it) is the twist.

## Why it might work

- **Reference-backed.** @satisfying2dsims (21M views in 3 months, the fastest riser in 2D) has its top three in this
  shape: *"It crushed one ball into 15,939 pieces"* 1.9M, *"Every ball crushed splits into 3"* 1.7M, *"How many balls
  can a hydraulic press handle?"* 725K. Multiply plus a crusher, with the count on screen.
- **It takes `clack`'s shape**: a *how many?* question, a guess beat per round, an answer ladder that hides a pattern
  (`clack-001`: 2,879 at day 3, 2.57% likes).
- **The cause is visible.** Plain clone-on-bounce did badly elsewhere (Bouncy Physics, 8.9K; MathCa's early
  *"Splitting vs exponential"*, 10K). Here every new ball has a reason you can see: it got squashed.

## Design rules

- **Squash before pop.** A ball visibly flattens before it splits, or the cause isn't on screen.
- **Area is conserved on a split** (r → r/√2), so the pile doesn't grow out of nowhere.
- **Load flows through the pile**, not just the top layer, so pops ripple downward.
- **The answer is the count when the press stops.** Never claim a ratio the runs don't produce.
- Deterministic custom solver, not Physics2D: the count *is* the content.

## Open questions (fill in after press-001)

- Does the ~4× ratio hold across seeds, or is the pile too irregular for a clean law?
- Does a 2,000-grain pile read at 480px, or does it turn to noise?
