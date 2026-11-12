# Format brief: `bell` — Bell Curve Drop

**Tension type:** prediction with a certain, surprising answer (*what shape?*), escalated by marble count and a rigged round.
**Origin:** maths lane, `math-convergence.md` #7; promoted to next maths pick on 2026-10-24 with a twist round.
**First video:** [bell-001](../../plans/videos/bell-001-bell-curve-drop.md).

## The shape

Marbles drop through a Galton board, where every peg is a left/right coin flip, and pile into bins. Few marbles make
a rough mound; many make a bell curve; a board rigged to lean right makes the same bell, shifted. The answer is the
central limit theorem, shown rather than explained.

## Why it might work

- It is the closest maths shape to the proven peg-board look (`rom`, Plinko), with a certain answer like `conv` and `clack`.
- The curve is built on screen, marble by marble, so the viewer watches the guess resolve.
- Watch: the genre's most familiar look may read as "another Plinko". The rigged round and the outline are the
  differentiators. Test before making more peg-board maths.

## Design rules

- **Deterministic, no solver.** Paths are seeded flips; motion is arcs between pegs.
- **Captions must hold on every seed.** Round 1 says `ROUGH`, never claims a shape it might not show.
- **The outline is read from the pile** (fitted mean and spread), never drawn from a formula the run didn't produce.
- Bars rescale per round; compare shapes within a round, never heights across rounds.

## Wide variant (comp-001)

Segment 9 of the 16:9 compilation, "Rigged Board". Menu: `Simulation Lobby ▸ 2D Formats ▸ Bell Curve Drop (bell) ▸ Wide: Rigged Board (comp-001)`
(priority 200). Scene `Assets/Scenes/Comp001_09_RiggedBoard_Wide.unity`, config `Assets/Settings/Configs/BellCurveConfig_Comp001_09_Wide.asset`,
card `Assets/Settings/SegmentCards/comp001_09.asset`. Code: `Editor/BellCurveWideSceneBuilder.cs`, `View/BellWideHud.cs`, plus an additive
`BellCurvePresenter.ConfigureWide(gradient, pool, toneRoot)`; the 9:16 path is unchanged.

- Board: 18 rows (cap 20), 19 bins at `binSpacing` 1.0, `rowSpacing` 0.4, hop 0.16s; warm red-orange-yellow bin gradient on a dark ground.
- Round 1: 500 fair marbles at 60/s. Round 2: 1,500 at 150/s, `rightProbability` 0.25, `pegLean` -0.9 (left). 3s result holds, 4s end card.
- Honest by construction: the caption says "STILL A BELL, JUST SHIFTED" because a binomial at p=0.25, n=18 is bell-shaped (CLT), merely
  skewed slightly. The right-margin readout shows the pile's own fitted centre and spread against the fair centre (9.0); the why line
  (central limit theorem) shows on the end card.
- Edge bins: bin 0 gets about 0.6% of marbles at p=0.25 (about 8 of 1,500), so no wall clipping; the board is symmetric and the pile shifts, not the walls.
- Marble pool raised to 720 (about 150/s x 3.7s in flight = 560 live marbles); the 9:16 default of 360 would drop marbles from view.
- Estimated length ~42s (not measured). The first 7.5s of the explainer overlay covers the bottom caption while round 1 starts.
