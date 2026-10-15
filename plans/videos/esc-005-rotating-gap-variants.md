# esc-005..007: rotating-gap variants

**Format:** `esc` · **Aspect:** 9:16 1080x1920 · **Status:** `built` (compiles; not yet played)
**Scenes (2026-10-13):** each config has its own menu entry under **Simulation Lobby ▸ 2D Formats ▸ Escape (esc)**:
`Escape_Circle_2D_RotatingGap` (005, published as v14), `Escape_Circle_2D_ShrinkingExit` (006),
`Escape_Circle_2D_FasterGap` (007) · **Seed:** random per Play, log it at record time
**Base plan:** [esc-002](esc-002-rotating-gap.md). Everything there applies, and only the differences are listed here.

## Why

v4 (esc-002) is the only breakout (3,150 at day 7). v11-v13, all contest formats, got 32-158 views.
These are three cheap Shorts in v4's shape, one variable each, posted on separate days
(`KnowledgeBase/learnings.md` → *10-11 update*).

| Config | Title idea | The one change vs v4 | Job |
|---|---|---|---|
| `EscapeConfig_005` | Will It Escape? The Exit Keeps Moving (Round 2) | gap spin 22°/s (v4: 20) | **Diagnostic.** ~1K views = floor intact, contest shape was the problem. <200 = channel-level dip |
| `EscapeConfig_006` | The Exit Shrinks Every Round. Can It Still Escape? | gap 35° on round 1 → 20° on round 7 (playtests 2026-10-13: 30 → 18 favoured the wall, 45 → 20 the ball; 35 aims for a close 4-3) | Series that gets harder as the score builds |
| `EscapeConfig_007` | The Gap Gets Faster Every Round | spin 12°/s → 36°/s across rounds | Same, on the timing axis |

Post order: 005 first, the `brk` follow-up next, then 006, then 007. Only one new thing per video.

## Implementation (2026-10-11)

- `EscapeConfig`: `finalGapDegrees`, `finalGapAngularSpeed` (0 = off), linear from round 1 to the
  last round. **No RNG draws**, so every older config and seed replays unchanged.
- `EscapeSimulation.BeginAttempt` rebuilds the wall only when the round's gap width differs.
- `EscapePresenter` rescales the existing `GapMarker` to the current gap width (found by name under
  the wall, so no scene rewiring).
- `CircularBoundary2D.ClearSegments` deactivates segments before `Destroy`, so a mid-run rebuild
  takes effect in the same physics step.
- 006 raises `maxAttemptSeconds` to 30: a 30° gap needs ~36 bounces to trap, about 22s.

## On-screen explainer (2026-10-13)

A ramp of a few degrees a round is hard to see, so a ramped config explains itself. Both lines come from
the config (`EscapeConfig.RampExplainer` / `RampStatus`), so 005 and any config without a ramp are unchanged.

| Config | Question line (top) | Round line (bottom) |
|---|---|---|
| 006 | THE EXIT SHRINKS EVERY ROUND | ROUND 3 / 7  ·  EXIT FITS 3.1 BALLS (live: counts down as the ball grows, then TOO BIG TO FIT) |
| 007 | THE EXIT SPINS FASTER EVERY ROUND | ROUND 3 / 7  ·  1 LAP EVERY 18s (down to 10s by round 7) |

Units are things a viewer can check by eye (balls of the current size, seconds per lap), not percentages or degrees.
The rule takes the question's slot, so put the hook (*Can it still escape?*) in the video title.

## Check on first Play

- [ ] 006: the gold marker visibly narrows each round and matches the opening in the wall.
- [ ] 006/007: no `[esc] TUNNELING` in the console. 007 round 7 is the fastest wall yet (36°/s).
- [ ] No round ends on TIME. If one does in 006, raise `maxAttemptSeconds` a bit more.
- [ ] Score isn't a 7-0 whitewash. If 006 ends 7-0 either way, move `finalGapDegrees` by about 2°.
