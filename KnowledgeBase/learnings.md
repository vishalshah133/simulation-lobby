# Learnings — video vs stats

Live pull from the YouTube Data API (`youtube` skill, `videos`/`report` commands), **2026-10-04**.
Public data only — no retention/drop-off (needs OAuth + Analytics API, not set up).

These are **current totals at different ages**, not day-7 snapshots — compare with that in mind.

| # | Date | Age | Slug | Format | Title | Length | Views | Likes | Like% | Cmts |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 2025-05-01 | 520d | pre | — | How many balls will fit? | 48s | 15 | 4 | 26.7% | 0 |
| 2 | 2025-05-03 | 519d | pre | — | Marble Volcano 🌋 | 59s | 30 | 3 | 10.0% | 2 |
| 3 | 2026-09-27 | 7d | esc | Containment/escape (static gap) | The Ball Grows Every Bounce. Can It Escape? | 2:12 | 892 | 6 | 0.67% | 0 |
| 4 | 2026-09-29 | 5d | esc | Containment/escape (rotating gap) | Will It Escape? The Exit Keeps Moving | 2:05 | 3,149 | 17 | 0.54% | 0 |
| 5 | 2026-09-30 | 4d | surv | Wall vs Ball (3D, guess the hits) | How Many Hits to Destroy 300 Blocks? 🤔 | 1:20 | 240 | 4 | 1.67% | 2 |
| 6 | 2026-10-01 | 3d | impact | 0% vs 100% soft sweep (3D) | 0% vs 100% Soft🔥Glass Capsule Squeezes Through Hole | 37s | 1,185 | 16 | 1.35% | 0 |
| 7 | 2026-10-03 | 1d | esc | Containment/escape (rotating gap + guess the song) | Do You Know This Song? 🤔 Will the Ball Escape? | 1:19 | 1,524 | 15 | 0.98% | 3 |

Channel: **19 subs · 7 videos · 5,513 views** per the channel endpoint (the per-video sum is 7,035;
the channel total lags). From 13 subs / 26 views at revival on 2026-09-27.

### Trajectory between pulls

| # | 09-29 | 10-03 | 10-04 | Δ last day |
|---|---|---|---|---|
| 3 | 712 | 892 | 892 | 0 — flat |
| 4 | 1,201 | 3,156 | 3,149 | −7 — stopped (was ~490/day) |
| 5 | — | 244 | 240 | −4 |
| 6 | — | 1,185 | 1,185 | 0 — flat from day 2 |
| 7 | — | — | 1,524 | (day 1) |

Small drops (v4 −7, v5 −4) are YouTube removing views it decides are invalid, which is routine and
nothing to worry about.

## Observations (provisional — one video per variant, one day-7 snapshot so far)

- **Every older video went flat on the same day (10-03 → 10-04)**, including v4, which had been
  adding ~490/day. That looks like the usual Shorts pattern: the feed pushes a video in a burst,
  then stops. But all four stopping at once, the day v7 went up, also fits the feed moving to the
  newest upload, or the public counter lagging. **The API can't tell these apart.** Studio's
  real-time panel can. Check it before concluding that videos here only live ~3–5 days.
- **v7 (guess the song) had the best day 1 of the current era at 1,524**, vs v6 1,185 and v4 1,201
  at day 0. It reuses v4's rotating gap and adds the song hook, so it's the closest thing yet to a
  one-knob comparison against v4. Compare them at day 7 (v4 on 10-06, v7 on 10-10), not now.
- **The two "guess" hooks are the only current-era videos with comments**: v5 (guess the hits) has
  2 and v7 (guess the song) has 3; the other three have 0. A guessing prompt plus a pinned
  "name it" comment seems to start replies. It's 5 comments in total, so treat it as a lead, not a
  finding.
- **Rotating gap (v4) beat static gap (v3)** about 3.5x. v3's day-7 snapshot is **892**; v4's lands
  10-06. If v4 stays flat it will be about 3,150, and the gap stays large. Write it into
  `containment-escape.md` once that snapshot is in.
- **`impact` soft sweep (v6) held at 1,185 and flattened after day 1**, not climbing like v4 did.
  A 37s real-time 3D render still got a start on par with the 2D `esc` videos, so the open
  question in impact-003 (can URP compete with Blender renders?) is an early "yes, for the first
  push". Whether it gets a second push is unknown.
- **`surv` Wall vs Ball (v5) is still the clear underperformer** at 240. Possible causes, none
  tested: no studio-look retrofit, a title with no visual promise, or a weaker hook. Studio
  retention would separate look from hook. Don't retune blind.
- **Length doesn't separate the winners**: the top three are 2:05, 1:19 and 37s. Keep tuning for
  event count rather than a target length.
- **Like% sits at 0.5–1.7% in the current era** and tends to fall as reach grows (colder viewers).
  That makes it a poor quality signal across videos with different reach, and it's **not** a
  retention proxy.
- **Re-renderability:** v4 was randomized by design, v3's seed was never recorded, and v5 and v7
  have seeds that are assumed from the scene (`seed: 1`, randomize off) but not confirmed. v6 is
  still the only upload with a fully recorded scene + config + seed.

## What would make this useful

- **Day-7 snapshots: 10-06 (v4), 10-07 (v5), 10-08 (v6), 10-10 (v7).** v3's is done (892). They
  can't be recovered if missed.
- **Studio real-time view for 10-03 → 10-04**, to settle whether the older videos really stopped or
  the public counter is lagging.
- **Pull Avg % viewed + drop-off from Studio for v4, v5, v6 and v7.** v4 vs v5 (best vs worst) and
  v4 vs v7 (same arena, song hook added) are the two comparisons most worth explaining.
- Write the first retros into `KnowledgeBase/retros/`. There are none yet, and five current-era
  videos are now waiting.
