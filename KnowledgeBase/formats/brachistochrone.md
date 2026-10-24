# Format brief: `brach` — Brachistochrone (fastest path)

**Tension type:** prediction (*which ball wins?*). The answer is mathematically certain, and it surprises: the
fastest way down is not the shortest.
**Origin:** `math-convergence.md` → *Wider maths backlog* #11, picked 2026-10-18 after `conv-002` and `clack-001` both
beat the proven band by ~1.7x (`learnings.md` → *10-18 update*). It needs its own motion model and HUD, so it's its own
format, as `clack` was.
**First video:** [brach-001](../../plans/videos/brach-001-fastest-path.md).

## The shape

Balls start together at the same point and roll to the same finish, each on a differently shaped track. The viewer
guesses the winner. The cycloid wins every time, against every shape, including when it dips *below* the finish and
has to climb back up. The payoff is seeing the longer path win, not having it explained.

## Design rules

- **Same start, same finish, visible from frame one.** A start gate at A, a finish flag at B, every track drawn in
  full before anything moves. The question names them.
- **Analytic, not physics-engine.** A frictionless bead on a polyline, from rest. Each segment's time is exact under
  constant acceleration (`2L / (v0 + v1)`, with `v = sqrt(2 g h)`), and position inside a segment is closed-form. No
  Rigidbody, no ball-ball contact (each ball owns its track).
- **Rolling vs sliding doesn't change the answer.** A rolling solid ball takes `sqrt(7/5)` times longer on every
  track, so the order is the same. Simulate the bead and spin the ball for looks. Say so in the pinned comment if asked.
- **The seed is cosmetic**: palette start, backdrop theme. The outcome is set by geometry.
- **One question all video.** Rounds add tracks or move the finish. They never ask a new question. The tautochrone
  ("released from anywhere, they all arrive together") is a *different* question, so it's a later video
  (`brach-002`) on the same kernel, not a round here.
- **Photo finishes get slow motion**, driven from simulation state (the leaders' distance to B), so it's deterministic.
- **Name the shape at the end, not before, and make the reveal the loudest text on screen.** The answer replaces the
  question in the top panel (*BRACHISTOCHRONE CURVE* / *GREEK FOR "SHORTEST TIME"*), and the winning track's label
  renames itself. A reveal only in the bottom line got missed in the first cut.
- **Explain the name, not the mechanism.** The user's pick (2026-10-18): use the brachistochrone name rather than
  "cycloid", and split it into its Greek roots (*BRACHISTOS = SHORTEST* → *CHRONOS = TIME*). The name *is* the answer
  to the question. A compressed mechanism line (*STEEP START = MORE SPEED*) made a viewer stop and decode it. "Cycloid"
  and the steep-start reason belong in the description and pinned comment.

## Candidate videos on the same kernel

1. **Fastest path** (*brach-001*): which ball wins? Five rounds, the cycloid always.
2. **Tautochrone**: balls released from different heights on one cycloid all reach the bottom at once.
3. **Galileo vs Bernoulli**: circle arc vs cycloid only, the finish moved round by round, and the gap never closes.
