# Format brief: `brk` — Ring Breaker

**Tension type:** threshold (*will it break out?*), like `esc`, but with progress you can see
ring by ring.
**First video:** [brk-001](../../plans/videos/brk-001-ring-breaker-fur-elise.md).

## The shape

A ball inside nested rings made of segments. Touches break segments, holes let the ball out, and
each ring explodes when the ball escapes it. The ball speeds up per ring; the outer rings take
more hits.

## Why it works (genre evidence, 2026-10-04)

- CodeCraftedPhysics: *"Can it break the whole spiral?"* 10.7M, *"When it finally reached the
  center"* 25.7M (the inverse shape). satisfying2dsims: *"Can Pac-Man reach the core?"* 670K.
  See `reference-channels.md`.
- **Visible progress** (5 → 4 → 3 rings left) gives a long run a structure, which the 2D genre's
  60–70s hits rely on (`channel-strategy.md` → *Length*).
- **Every touch is an event** (a note plus a crack or a shatter), so there's no dead time.

## Design rules

- **Holes only from physics.** A segment breaks because it was touched; the ball leaves only
  through an opening that exists. The sim fails a seed that violates this.
- **Instant hook:** the innermost ring breaks on the first touch.
- **Never launch from dead centre:** the ball would bounce along one diameter forever.
- **One-segment hole must fit the ball** in every ring (`BreakerConfig.Validate`), or escape needs
  two adjacent breaks, which is a much slower game.

## Variants to try

- Song per video (the song-series route): one melody asset each.
- Ring count / spin speed as the one-knob change.
- "Guess how many hits" instead of a song (comments + a different hook).
- Two balls racing to break out (competitive tension), which needs a second ball and a per-ball score.

## Learned

- **2026-10-04: it needs a way to lose.** Without one the ball always breaks free eventually, and
  the hook question has a guaranteed answer (user's catch). First scan: 188/200 finished, the rest
  only timed out on the safety cap. Fixed with a 60s clock (`timeLimitSeconds`).
- **2026-10-04: the Python pacing model was 2.7× optimistic** (46s modelled vs 123s measured).
  Tune from real scan numbers, not the model.
- **2026-10-04: spin rings with a kinematic Rigidbody2D.** Moving static colliders by transform
  made a 200-seed scan take 38 minutes.
