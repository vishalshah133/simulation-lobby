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

## Wide variant (comp-001)

Segment 5 of `comp-001`, **Iron Press**: 16:9, built by `Simulation Lobby ▸ 2D Formats ▸ Hydraulic Press (press) ▸ Wide: Iron Press (comp-001)`
(`HydraulicPressIronWideBuilder`, scene `Assets/Scenes/Comp001_05_IronPress_Wide.unity`, config `HydraulicPressConfig_IronWide`).

- Config only plus a cosmetic path: chamber 14 x 5.5, `startRadius` 2.5, `ballStrengthTons` 2, three rounds 8 / 32 / 128 t, holds 3.5 / 0.6 / 1.2.
  The presenter gained an additive `EnableIronWide(SegmentCardPresenter)` (molten palette, dark steel, deeper thuds, HUD to the segment card).
- Kernel-only harness (4 seeds, dt 1/60): 8 t = 4-6 balls in 1.7-2.3 s; 32 t = 101-117 balls in 5.3-7.9 s; 128 t = 1,467-1,504 balls in 11.6-16.3 s.
  Counts scale as tons squared (x16 per 4x force), so `maxBalls` 5000 is never reached; `startRadius` is the knob that sets the pile size.
- Estimated video length ~42 s (3.5 s lead-in, medians, 6 s end card). Not yet watched in Unity.
- Never claim a ratio: the ladder is 5 / ~110 / ~1,500, which is not a clean 4x.
