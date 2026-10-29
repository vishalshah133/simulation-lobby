# Learnings — video vs stats

Live pull from the YouTube Data API (`youtube` skill, `videos`/`report` commands). Latest: **2026-10-24 02:45Z** (see *10-24 update*).
The first table and channel line below are the 10-06 16:51Z pull; the trajectory table keeps the earlier pulls. Public data only: no
retention/drop-off (needs OAuth + Analytics API, not set up).

**How to read this file.** Each claim carries a tier. *Validated* = holds across 5+ reference
channels with a bootstrap interval that excludes 1x. *Lead* = direction consistent but thin or
not significant. *Null* = tested, no detectable effect. *Untestable* = our own n is too small, or
the data isn't public. Our own six current-era videos can't validate a factor; they can only
illustrate one.

These are **current totals at different ages**, not day-7 snapshots — compare with that in mind.

| # | Date | Age | Slug | Format | Title | Length | Views | Likes | Like% | Cmts |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 2025-05-01 | 520d | pre | — | How many balls will fit? | 48s | 15 | 4 | 26.7% | 0 |
| 2 | 2025-05-03 | 519d | pre | — | Marble Volcano 🌋 | 59s | 30 | 3 | 10.0% | 2 |
| 3 | 2026-09-27 | 9d | esc | Containment/escape (static gap) | The Ball Grows Every Bounce. Can It Escape? | 2:12 | 893 | 6 | 0.67% | 0 |
| 4 | 2026-09-29 | 7d | esc | Containment/escape (rotating gap) | Will It Escape? The Exit Keeps Moving | 2:05 | 3,150 | 17 | 0.54% | 0 |
| 5 | 2026-09-30 | 6d | surv | Wall vs Ball (3D, guess the hits) | How Many Hits to Destroy 300 Blocks? 🤔 | 1:20 | 241 | 4 | 1.66% | 2 |
| 6 | 2026-10-01 | 5d | impact | 0% vs 100% soft sweep (3D) | 0% vs 100% Soft🔥Glass Capsule Squeezes Through Hole | 37s | 1,185 | 16 | 1.35% | 0 |
| 7 | 2026-10-03 | 3d | esc | Containment/escape (rotating gap + guess the song) | Do You Know This Song? 🤔 Will the Ball Escape? | 1:19 | 1,635 | 18 | 1.1% | 3 |

| 8 | 2026-10-04 | 1d | brk | Ring breaker + Für Elise | Guess the Song 🎵 Can It Break All 5 Rings in 60 Seconds? | 1:06 | 1,554 | 19 | 1.22% | 1 |
| 9 | 2026-10-06 | 0d | rom | Release or Multiply (marbles) | How Many Marbles Reach the Bottom? | 40s | 912 | 12 | 1.32% | 1 |

v8 (`brk`, 10-04 14:59 UTC): 1,335 at ~11h, **1,500 at ~35h**. v9 (`rom`, first Release or Multiply upload) went up
10-06 01:49 UTC, so its curve starts from the 10-06 snapshot.

Channel 10-06 16:51Z: **22 subs · 9 videos · 8,656 views**. Channel 10-06 01:49Z: **20 subs · 9 videos · 8,433 views** (per-video sum 8,635; channel total lags). 10-05: **20 subs · 8 videos · 7,011 views**. Earlier, 10-04: **19 subs · 7 videos · 5,513 views** per the channel endpoint (the per-video sum is 7,035;
the channel total lags). From 13 subs / 26 views at revival on 2026-09-27.

### Trajectory between pulls

| # | 09-29 | 10-03 | 10-04 | 10-06 | Δ since 10-04 |
|---|---|---|---|---|---|
| 3 | 712 | 892 | 892 | 893 | +1 — flat, day-7 = 893 |
| 4 | 1,201 | 3,156 | 3,149 | 3,150 | +1 — stopped, **day-7 = 3,150** |
| 5 | — | 244 | 240 | 241 | +1 |
| 6 | — | 1,185 | 1,185 | 1,185 | 0 — flat since day 2 |
| 7 | — | — | 1,524 | 1,621 | +97 over 2 days |
| 8 | — | — | 1,335* | 1,500 | +165 (*10-05 02:05Z) |

Small drops (v4 −7, v5 −4) are YouTube removing views it decides are invalid, which is routine and
nothing to worry about.

## Factor analysis — validated against 10 reference channels (2026-10-05)

**Method.** Every Short (<=3 min) published 2024-01 onward and at least 14 days old, from 10
channels: **1,519 videos**. (`moreballs` and `AlgoMarblerYT` are long-form, so they contribute almost
nothing.) For each video, y = log(views / own channel's median that quarter), so a big channel
and a small one are comparable. For each factor: effect per channel (with-factor median minus without,
needing >=8 videos each side), then the mean across channels with a **bootstrap over channels**
(the channel is the unit, not the video, because one channel's videos aren't independent).
Public counter only: views, not engaged views or retention.

### Validated (CI excludes 1x, direction holds in most channels)

| Factor | Effect | Evidence | What to do |
|---|---|---|---|
| **Publish Fri-Sun (UTC)** | **1.27x** [1.07-1.62] weekend vs weekday; **Sat 1.37x** (5/7 ch), Sun 1.13x (5/6), Fri 1.08x (5/6) | 8 channels, n=437. **Mon 0.92x and Tue 0.90x** are the weak days (1/7 channels positive each) | Aim uploads at Fri/Sat/Sun UTC. Our v7 (Sat) and v8 (Sun) already fit this. Mind the timezone: Sat UTC is Fri evening in the US |
| **Clean title of 26-45 chars** (hashtags stripped) | 1.06x [1.01-1.11] (4/7 ch); **<=25 chars is 0.87x** [0.76-0.98], 0/5 channels positive | n=354 short titles | Weak, but very short titles lose consistently |

Honest size: these are small effects (about 1.1-1.4x). They help at the margin and do not explain
the 10x gap between a 1K video and a 1M video. **Topic and execution do that**, and neither is in this table.

### Leads (right direction, not significant or n too small)

- **Posting 00-06 UTC** (US evening): 1.33x [0.96-1.96], 4/6 channels. Could be the weekend effect in disguise.
- **Question mark in title**: 1.20x [0.97-1.52], 4/6. **Digit in title**: 1.22x [0.93-1.79], but only 2/5 channels, so driven by a couple of channels.
- **"finally / last / ending / never" in title**: 1.52x [1.27-1.81], but only **2 channels, n=32**. Payoff-promise language is plausible and worth one test.
- **"ball" in the title**: 2.36x [1.13-7.32], 5/5 channels, n=140. Wide, and mostly one channel (FuncFlow's 27x: its top video is "Will the ball escape?" and its other 2026 uploads are tiny). Read it as "the ball genre still has pull", not as a keyword to stuff.
- **"fit / hole" titles**: 1.17x [1.06-1.31], 3/3 channels. This is RenderZen's template, and the one `impact` is closest to.
- **Titles with "escape / exit / gap": 0.64x [0.54-0.76]**, 0/3 channels positive, **n=18 only**. If it holds, it supports the earlier finding that plain `esc` is a weak bucket now. v4 (our best) used "Exit Keeps Moving", so that's one contradiction, not a refutation.
- **45-75s duration**: 1.13x [0.81-1.52], 4/5 channels. Consistent with the length table above.

### Nulls (tested, no detectable effect, so stop spending time on them)

Emoji in title (0.93x), hashtags in title (0.95x), 4+ hashtags in title (1.00x), 3+ hashtags in
description (0.98x), `#shorts` in the text (0.88x), has tags at all (0.98x), 15+ tags (0.93x), long
description (0.95x), "vs" in title (0.94x), "can/will it" (0.95x), "satisfying" (0.99x),
Entertainment vs People & Blogs category (0.99x), 20-45s duration (0.98x). The CIs are wide
enough that a 1.2x effect could hide in any of them, but **nothing here is a lever worth the effort**.
The tag lists and 7-hashtag title strings we've been writing are, as far as this data can say,
decoration. Note v8's title has a stray empty `#` token ("# #physicssimulation"): harmless, but fix it.

### Song/guess hook: not a multiplier any more

`song/guess/melody` in title: **0.95x [0.75-1.20]**, 2/4 channels, n=50. The 2023-24 mega-hits are song-based,
but in the 2024+ window the keyword on its own doesn't beat a channel's median. v7 and v8 both lean on
it, and both sit at about our median (1.3-1.6K vs ~1.2K): no contradiction, and no evidence the hook lifted them.

### What we can say about our own channel (illustrative, not validated)

- **Reach is tight, with two outliers.** Our six current-era videos: 241, 892, 1,186, 1,335 (v8, 11h), 1,566, 3,149.
  Median ~1.3K. Four of six sit within 0.7-1.2x of the median. **Format differences inside that band are noise**:
  v3 vs v6 vs v7 vs v8 are not distinguishable. Only v4 (2.5x median) and v5 (0.2x) stand apart.
- **First-day views are most of the lifetime views** for 4 of 5 older videos (v6 flat from day 2, v7 +42 over
  a day, v3 712 -> 892 over 8 days). v4 is the exception: 1,201 -> 3,149 over four days. A breakout here
  looks like a second-day push, and we've seen exactly one.
- **Subscriber conversion is ~0.10%**: 13 -> 20 subs over ~7,000 new views. At that rate 10M
  views would bring ~10K subs, so **the 1K-subs half of the YPP bar isn't the bottleneck; views are.**
  (Subs lag, and Shorts-feed viewers rarely subscribe, so this is a floor.)
- **Engagement is low and flat**: likes 0.5-1.7% of views, comments 0-3 per video. The two earlier
  "guess" videos had the only comments (v5: 2, v7: 3), but v8 (also a guess) has 1 at 11h. Too few to call.
- **Our weekend uploads (v3 Sun, v7 Sat, v8 Sun) median 1,335 vs weekday (v4, v5, v6) 1,186.** The sign matches the
  cross-channel finding, but 3 vs 3 proves nothing.
- **Category:** v3 and v4 were filed as People & Blogs (22), the rest as Entertainment (24). The cross-channel
  test found no effect (0.99x). Set it to Entertainment consistently and forget it.

### Factors we cannot test yet (and what unlocks them)

| Factor | Why not | Unlock |
|---|---|---|
| **Retention / avg % viewed / swipe-away rate** | Owner-only | Studio by hand, or OAuth for the Analytics API. This is the biggest gap: **it's the metric that separates hook from look from audio** |
| **Sound quality / melody recognition** | The API can't hear. Nobody's audio is measured | Retention at the first note, plus comment text ("is that X?") |
| **First frame / thumbnail** | Shorts viewers swipe into the video; no API signal | Studio's "viewed vs swiped away" |
| **Time of day** (beyond weekday) | Posting hour barely varies within a channel, and the audience timezone is unknown | One planned test: same format, two time slots |
| **View velocity** (6h, 24h, 72h) | The API returns totals only | **`snapshot.py` now logs it** (below) |
| **Traffic source** (Shorts feed vs browse vs search) | Owner-only | Studio |
| **Title/description wording on *our* channel** | n=6 | More videos. The cross-channel tests above stand in meanwhile |

**New instrument:** `python .claude/skills/youtube/scripts/snapshot.py` appends one row per video
(views, likes, comments, subs, UTC timestamp) to `KnowledgeBase/data/snapshots.csv`. Run it twice a day
(Windows Task Scheduler) and every future video gets a real velocity curve. First snapshot taken
2026-10-05 02:00 UTC. Cross-channel analysis: `.claude/skills/youtube-research/scripts/factors/`
(`pull.py` then `an.py` then `an2.py`).

### Caveats on the cross-channel numbers

- **Survivorship**: the channels were named to us because they succeeded. Effects measure what separates
  a channel's good videos from its median ones, not what separates success from failure.
- **Channel-level confounds**: a factor that only varies in 3 channels (e.g. "vs") is really a statement
  about those 3. Per-channel effects are printed by `an2.py`.
- **Many tests, one pass** (~45 factors): at 90% intervals, ~4 will look "significant" by chance. The weekend
  effect (7/8 channels, plus a consistent Mon-Tue trough) is more credible than the title-length one for that reason.
- **Public views include replays since 2025-03**, so these are not the engaged views YPP counts.

---

## 10-06 update

- **Everything older is dead flat.** v3, v4, v5, v6 moved +1, +1, +1, 0 in two days. v4 did not resume, so these
  Shorts have a real life of about **2-5 days, then nothing**. Two days flat makes a lagging counter unlikely,
  though the API still can't prove it.
- **v4's day-7 snapshot is 3,150 vs v3's 893: 3.5x**, matching the earlier read. Still one video per variant.
- **v7 (song hook on v4's arena) is at 1,621 at ~2.9d**, +97 in two days: a small tail, about half of v4's.
  Day-1: v7 1,524 vs v8 1,500, so **the song-hook videos land at ~1.5K on day 1 consistently**, consistent with the
  cross-channel 0.95x. Neither is a breakout. v4 (no song) is still the only >2x.
- **v8 has 1 comment** (v7: 3). The "guess" prompt isn't reliably producing replies.
- **Channel: 8,433 views from 26 at revival (09-27), 13 → 20 subs.** Sub rate over the new views is ~0.08%,
  in line with the ~0.10% estimate above. Views remain the bottleneck for YPP.
- **v9 is the first `rom` upload (40s).** Snapshot at 6h, day 1, 3, 7. Closest length-matched reference is v6
  (37s, 1,185), but the formats differ, so that's one data point, not a comparison.
- **Snapshots:** `snapshots.csv` has 2 pulls for the older videos (10-05 02:05Z, 10-06 01:49Z); v9's baseline is 0.

## 10-06 evening update (16:51Z)

- **v9 (`rom`) is at 912 views after ~13h** (12 likes, 1 comment). v8 had 1,335 at ~11h and v7 ~1.2K on
  day 0, so `rom` starts a little **below** the song-hook Shorts but well above v5, and level with v6
  (1,185 by day 2). One video, so it's a starting point, not a verdict on the format.
- **Its YouTube title is "How Many Marbles Reach the Bottom?"**, not the working title in the notes. Use the live
  title when comparing. The count-guess question is the same hook shape as v5, which flopped, so v5's
  failure was likely the look/format and not the "guess the number" prompt. Untested.
- **v7 +14 and v8 +54 since the 01:49Z pull**, v3-v6 +0/+1. Still just the two newest videos moving.
- **22 subs (+2 in ~15h), 8,656 channel views.** Sub rate stays near 0.1% of views.
- **Day-7 snapshots still due:** v5 10-07, v6 10-08, v7 10-10, v8 10-11, v9 10-13. Note: the 16:51Z
  `snapshots.csv` rows came from running `snapshot.py --help`, which writes a pull instead of printing help.
  The pull is valid data, but that flag is a script bug worth fixing.

## 10-09 update

Pull 2026-10-09: **22 subs · 11 videos · 11,057 views** (from 8,656 on 10-06). Snapshot appended to
`data/snapshots.csv`.

- **Day-7 reads recorded:** v4 3,157 (day-7 3,150), v5 243, v6 1,188. All three are flat, as before.
  v7 (1,649) and v8 (1,738) are still gaining slowly (+14 / +54 on 10-06, now +28 / +184 over three days),
  so the two song videos have longer tails than the older ones.
- **v10 (`impact`, 500KG ball on a cube castle, 51s) 1,087 and v9 (`rom`) 1,040, both at day 3.** The
  Shorts are landing in the same 1.0-1.7K band as everything since v6. Nine videos in, the first push is
  ~1K whatever the format, apart from v4 (3.1K) and v5 (243). That's still noise, not a format ranking.
- **v11 (`cric`, 1:23) went up today** and has 2 views. First sport format, flagged an experiment.
- **Subs stayed at 22 while views grew 2.4K.** Under 0.1% of new views now. The sub requirement
  (1K) binds before the view requirement on any route; see the long-form decision below.
- **Cadence since revival:** 11 uploads, 9 of them in 13 days (about 0.7/day). That's the highest
  throughput this channel has had, and views per upload hasn't dropped as it rose.
- **Seeds:** v10 and v11 seeds are not recorded anywhere I can see. Log them from the Unity console if
  still known.

### 10-09 evening pull (18:00Z)

**22 subs · 11 videos · 11,057 channel views.** Since the 02:49Z pull, v1-v10 are flat apart from small
moves: v3 897 → 899, v4 and v6 unchanged, v7/v8/v9/v10 within a few views of the morning figures.
Only **v11 (`cric`) moved: 2 → 22 views, 1 like, 1 comment**, after ~15h. That is far below the
~900-1,000 the other Shorts had at a similar age, but one video in a new format is a data point, not a
verdict. Check it again at day 1 before deciding whether `cric` needs a different hook or thumbnail.

- **Day-7 reads corrected:** v5 is **246** (the 243 above was a few hours stale), v3 is 899.
- **Subs have been 22 for the last ~3 days** while views grew ~2.4K. Subscribers are the clear bottleneck.
- Pull appended to `data/snapshots.csv` (10-09T18:00Z).

## 10-10 update

Pull 2026-10-10: **22 subs · 12 videos · 11,050 channel views** (per-video sum 11,139; channel total lags). Snapshot appended.

- **Day-7 read recorded: v7 = 1,649** (flat since 10-09). Still-moving list is now just v8 (1,738) and the newest uploads.
- **v9 `rom` 1,040 and v10 `impact` 1,088 are flat at day 4**: both landed in the same ~1.0-1.7K band as v6-v8 and have stopped.
- **`cric` is a second upload, not one:** two Shorts went up 10-09, "Who Wins Today? India vs West Indies Marble Super Over" (1:23, **29 views**) and "West Indies Beat India in the Super Over!" (1:34, **54 views**), each with 1 like. At ~1 day old both are at 3-5% of the ~1K that other Shorts had by then. Two videos, same format, same result: this is now more than a one-off, so the `cric` hook/thumbnail or the topic itself is the suspect. Not a verdict (no retention data), but don't build a third before checking Studio's impressions and "viewed vs swiped away".
- **The result-reveal title ("West Indies Beat India...") did better than the question title**, 54 vs 29, but that is noise at these counts.
- **Subs: still 22**, flat for ~4 days across +2.5K views. Subs remain the binding constraint (see below).
- **Grand Prix (`race-001`) is the first long-form test** and is not uploaded yet (not on the channel as of this pull). Judge it on Studio retention and sub gain, not views, per the recommendation below. Record its seed from the Unity console at upload.
- **Day-7 snapshots still due:** v8 10-11, v9 and v10 10-13, v11/v12 10-16.

### Should we get into long-form? (analysis 2026-10-09, from `reference-channels.md` Black Cat-Gear)

Evidence **for**: long-form compounds (Black Cat-Gear's 2021-22 races still earn years later, median
long-form 414K) while our Shorts are done in 2-5 days. `elim` and `wars` are already scoped as long
form, and we have a re-render pipeline (seed + config) that Black Cat-Gear's "(HD)" re-uploads show
has a market. Evidence **against**: the one long-form reference is a hit-driven craft channel that
took 7 years to reach 198K subs, its cadence and recent uploads are declining, and it's Blender-grade
production. Its numbers say nothing about whether a 6-week-old, 22-sub channel can get a long video
seen: long-form depends on search/suggested, which needs history we don't have.

Two numbers frame it:

- **Subs bind either way.** 1K subs at ~0.1% of views needs ~1M views, on a Shorts or watch-hours
  route alike. Black Cat's 0.18% would cut that to ~550K. So long-form only helps YPP if it converts
  subs better than Shorts, which is **untested for us** (and unknowable from the API).
- **Timing.** The Shorts bar doubles on 2027-02-01, roughly 16 weeks away. Compounding long-form takes
  longer than that to show. The watch-hours route (standard thresholds; confirm current ones in
  Studio) is a separate fallback worth checking, not something I verified.

**Recommendation: stay Shorts-first on throughput, and run one cheap long-form test rather than a
pivot.** Pick a format that is already long (an `elim` bracket, or a full `cric` match, or a
Black-Cat-style "The X Race" using `race`) so it costs a scene, not new infrastructure. Judge it on
Studio retention and sub gain, not views: one video is not a trend at this channel size. Don't build
long-form-only features until that test returns.

## 10-11 update

Pull 2026-10-11 12:31Z: **23 subs · 13 videos · 11,303 views** (per-video sum). Snapshot appended.

| # | Slug | Title (short) | Age | Views | Like% |
|---|---|---|---|---|---|
| 13 | race | 12 Marbles, 1 Track: Who Wins the Grand Prix? | 1.4d | 32 | 3.1% |
| 12 | cric | West Indies Beat India in the Super Over! | 1.8d | 158 | 1.3% |
| 11 | cric | Who Wins Today? India vs West Indies | 2.4d | 52 | 1.9% |
| 10 | impact | 500KG Ball on a Cube Castle | 4.8d | 1,082 | 0.9% |
| 9 | rom | How Many Marbles Reach the Bottom? | 5.3d | 1,040 | 1.3% |
| 8 | brk | Guess the Song: 5 Rings | 6.9d | **1,742 (day-7)** | 1.3% |

- **Day-7 reads recorded:** v7 1,649, v8 1,742. v8 has the longest tail of any Short (+4 since 10-10, still
  creeping); both song videos outlived the non-song ones by a few days, without a bigger peak.
- **The ~1K floor broke.** Every Short v6-v10 got a first push of ~1-1.7K regardless of format. v11, v12
  and v13 got 52, 158 and 32. v12 is still climbing (+104 since 10-10), so it may end near 200-300, still
  a fraction of the band. Three in a row is past "one bad video".
- **What the three have in common:** all are head-to-head contests with named sides (two cricket teams,
  12 racers), and two use a sports topic. Every Short that got the push is a **threshold** question
  ("will it escape", "how many", "can it break"). Hypotheses, none tested:
  1. **Shape:** "who wins" needs a pick and a longer watch to pay off; "will it" pays off for anyone in
     one second. Cold Shorts viewers swipe.
  2. **Audience match:** the feed has learned our viewers as satisfying-sims viewers; cricket is a
     different audience, and India vs West Indies is a narrow seed.
  3. **Channel-level dip:** the feed testing the channel less after 13 uploads in 14 days.
  4. **v13 only:** race-001 was planned 16:9. A 16:9 2:05 video is long-form to YouTube, not a Short, and
     a 23-sub channel gets almost no long-form browse traffic. Confirm the aspect in Studio.
- **The diagnostic is cheap:** the next upload should be a proven-shape threshold Short (an `esc` rotating-
  gap or `brk` variant, config only). If it lands near 1K, the contest shape/topic is the problem and the
  floor is intact. If it also stalls under ~200, it's the channel, and format choice isn't the lever.
  Studio's "viewed vs swiped away" on v11-v13 answers it directly, without waiting.
- **Run rate since revival:** ~11.3K views and +10 subs in 14 days (~800 views/day, ~0.7 subs/day).
  That projects to ~72K views per 90 days, under 1% of the 10M Shorts bar, and ~1K subs in about 4
  years. **Since 10-10 it's ~120 views/day**, because the new uploads didn't get the push.
- **Subs: 22 → 23.** Still ~0.09% of views.

## 10-13 update

Pull 2026-10-12 19:13Z (10-13 local): **27 subs · 14 videos · 13,219 views** (per-video sum; the channel endpoint
lags at 11,386). Snapshot appended.

| # | Slug | Title (short) | Age | Views | Δ since 10-11 | Like% |
|---|---|---|---|---|---|---|
| 14 | esc | Will It Escape? The Exit Keeps Moving 🔄 7 Rounds | 1.3d | **1,580** | new | 1.65% |
| 13 | race | 12 Marbles, 1 Track: Who Wins the Grand Prix? | 2.7d | 363 | +331 | 0.28% |
| 12 | cric | West Indies Beat India in the Super Over! | 3.0d | 159 | +1 | 1.3% |
| 11 | cric | Who Wins Today? India vs West Indies | 3.7d | 52 | 0 | 1.9% |
| 10 | impact | 500KG Ball on a Cube Castle | 6.0d | 1,082 | 0 | 0.9% |
| 9 | rom | How Many Marbles Reach the Bottom? | 6.6d | 1,041 | +1 | 1.3% |

- **The diagnostic answered: the floor is intact.** v14 is the proven-shape threshold Short the 10-11 update
  called for (`esc` rotating gap, config only), and it reached 1,580 in ~30h, inside the 1.0-1.7K band and ahead
  of v4's day-0 1,201. So hypothesis 3 (channel-level dip from upload volume) is out for now, and the v11-v13
  stall points at the **contest shape and/or the topic** (hypotheses 1-2), not the channel. Still one video;
  Studio's "viewed vs swiped away" on v11-v13 vs v14 would separate shape from topic.
- **`cric` is dead flat:** v11 52, v12 159, unchanged in ~1.5 days. Final reads are ~5-15% of the band. Don't
  build a third `cric` Short without a different hook.
- **v13 (`race`) got a late push: 32 → 363 in ~31h**, after a near-zero first day. That's the second-day pattern
  v4 showed, at a tenth of the scale. Like% 0.28% (1 like) is the lowest on the channel. Still a third of the band,
  and still the aspect question from 10-11 is unconfirmed (16:9 = long-form to YouTube). Check Studio's traffic
  source: Shorts feed vs browse/suggested tells which it was served as.
- **Subs: 23 → 27 (+4 in ~31h)**, the biggest jump since revival, over ~1.9K new views (~0.2%, double the running
  rate). It coincides with v14's day 1 and v14 has the best like% of the current era (26 likes). One day, so a
  lead, not a rate change.
- **Day-7 reads:** v10 1,082 (flat since day 3, final read due 10-13 18:17Z), v9 1,041 (flat; formally due
  10-13 04:03Z, won't move). Both confirm "landed ~1K, stopped by day 3".
- **The rotating-gap `esc` is now 2 for 2 above the median** (v4 3,160, v14 1,580 at 1.3d), plus v7 (same arena +
  song) at 1,655. It's the channel's most reliable shape. Reuse it as the control whenever a new format stalls.

### 10-13 03:14Z pull

**28 subs · 15 videos · 13,322 views** (per-video sum). Snapshot appended. Only ~8h after the previous pull.

| # | Slug | Title (short) | Age | Views | Δ since 10-12 19:13Z | Like% |
|---|---|---|---|---|---|---|
| 15 | terr | Which Color Takes the Board? 4-Team Multiply or Release (15:15, long-form) | 7.5h | 26 | new | 3.8% (1) |
| 14 | esc | Will It Escape? The Exit Keeps Moving 🔄 7 Rounds | 1.6d | 1,583 | +3 | 1.64% |
| 13 | race | 12 Marbles, 1 Track: Who Wins the Grand Prix? | 3.0d | 435 | +72 | 0.23% |

- **v15 is the first real long-form upload** (`terr`, 15:15, 16:9). 26 views at 7.5h is what a 28-sub channel
  gets with no Shorts feed behind it, so don't read it as a format verdict. Long-form is judged on Studio
  watch time, avg % viewed and subs per video, not on API views. First useful read: day 7 (10-19).
- **v14 stopped almost at once: +3 in 8h at day 1.6.** The burst came in day 1 and finished. That's quicker than
  v7/v8, which crept on for days, and fits the "push, then stop" shape. It still sits inside the band, so the
  10-13 conclusion holds.
- **v13 (`race`) is still climbing slowly (+72 in 8h, ~200/day).** It's the only current-era video with a
  late, sustained climb rather than a day-1 burst. That fits either browse/suggested traffic for a 16:9 upload
  or a delayed Shorts test. Studio's traffic source would settle it. Like% keeps falling (1 like on 435).
- **Subs 27 → 28.** +5 since 10-12 morning. Still a lead, not a rate.

## Story legibility: from the conv-001 first cut (2026-10-14)

The user played the first `conv-001` build and found it **"not very comprehensible, difficult to understand"**,
and its twist "not visible". This is our own review, not audience data, but the failures were structural, so
they apply to every format. **A video's story must be understandable with the sound off, by someone who swiped in
2 seconds ago.**

What went wrong, and the rule each failure gives:

1. **The question didn't say where to look.** "Will they all meet again?" with the target hidden (kept hidden "for
   the surprise") meant the payoff happened somewhere nobody was watching.
   → **Make the question concrete and point at visible things.** Name the objects on screen ("from RED, how many
   hit BLUE?"), and show every object the question mentions from the first frame. A surprise comes from the
   *outcome*, never from hiding what to watch.
2. **The twist was invisible.** The start moved 0.19 units (4% of the arena), cut between rounds with no
   before/after, so the miss read as a glitch.
   → **A cause must be seen happening.** Animate the change on screen (a slide, not a cut), make it big enough to
   see at 480px, and mark the *correct* spot (a ghost) **only while the thing is off it**. Fade it out before the payoff. A
   ghost that shows when nothing is wrong, or that outlives the move, reads as a second copy (user feedback, 2026-10-15). If a viewer can't point to *what changed*, the result has no meaning.
3. **Too many rounds that each needed explaining.** Circle → focus → stretch → offset: four ideas in 40s, two of
   which (circle, stretch) added nothing the viewer needed.
   → **One question, one reveal, one twist.** Cut every beat that doesn't change the answer. Escalation formats
   get many rounds because each round is the *same* question with a higher number; a prediction format doesn't.
4. **Bookkeeping competed with the picture.** A MEET vs MISS score and "ROUND n / N" made viewers track a tally.
   → **Show only the number that is the answer** (`HIT BLUE 120 / 120`). Add a versus score only when there are
   genuinely two sides to root for. (This narrows the "always frame as two named sides" UI habit.)
5. **The idea was never named.** The trick (a focus) was implied, not told.
   → **End with the one-line reason** ("ONLY THIS POINT WORKS. IT'S CALLED A FOCUS."). A curiosity format owes the
   viewer the answer. MathCa's titles ask the question; the video still has to land the reason.

6. **A live counter must never contradict the result** (found modelling conv-002, before it shipped). Off-focus
   balls bunch near blue *later* (58 at once), after a first pass that hit only 11. A live counter would show 58
   and then caption "ONLY 11 HIT".
   → **Define the answer once** (here: most balls at blue at once during the first pass), **lock the counter at
   the verdict**, and show the result line at that moment. Visuals after that are decoration, not data.

**Process lesson:** the first plan was checked for *maths* (the Python model was right) but not for *story*.
Before building any new format, write the storyboard as a table of beat / what's on screen / caption, and read it
as a cold viewer: for each beat, can they say what they're watching for and why it matters? Do this in the plan,
before code.

## 10-14 update: reference pull (MathCa, Bouncy Physics)

No new pull of our own channel. Two channels launched in late September were added to `reference-channels.md`
(data 2026-10-14). Both are about 2 weeks old, so every claim below is a **lead**, not validated.

- **Math convergence is the strongest new-genre signal yet** *(lead, one channel)*. MathCa reached 12.1K subs and
  13.0M views in 15 days. Its top six are all one shape: many bodies, one "will they all ___?" prediction, and a
  certain mathematical answer (they meet, sync up or close the loop). Its pure explainers (Klein bottle, 4D cube,
  Ising) are its worst at 5-13K. **The payoff is seeing a theorem happen, not being taught it.**
- **That audience rates it higher** *(lead)*. MathCa holds a 1.2-1.9% like-rate across its catalogue. That's
  2-4x the bouncing-ball and marble channels, and above our 0.5-1.7%. Like-rate barely depends on age, so this
  is the cleanest cross-channel quality signal we have. One hit (12.2M) is 94% of its views, though, so its
  *reach* can't be separated from algorithm luck.
- **Our mechanic shapes did badly on MathCa** *(illustrative)*. Its first uploads, *"Splitting vs exponential.
  Who breaks out first?"* (10.3K) and *"Accelerating vs exponential"* (8.0K), are `mve`/`esc`-shaped. It switched
  shape on day 3 and grew. So that audience didn't come for escalation, it came for convergence.
- **Our 2D lane is crowded** *(illustrative)*. Bouncy Physics launched the same week with nearly our backlog:
  grow/shrink, reach the centre, sextillion, clone-per-bounce, shatter a shape. It sits at a 3K median and 128 subs.
  A new rival in the same shape is a reason to put experiment-lane slots into new shapes, not more variants.
- **Hidden-shape reveal** ("can you guess the shape?") was Bouncy Physics' #1 and #2 (25.5K, 18.2K) *(weak lead)*.
- **Compilation long form on a young Shorts channel got 7 views** (Bouncy Physics, 6:58) *(illustrative)*. This
  originally cited our v15 as a second data point, but v15 reached 819 by day 2.3 (see *10-15 update*). Read it as
  "compilations get nothing", not "long form gets nothing".
- **Shorter on Bouncy Physics coincided with more views** (1:05-1:13 → 22-44s). It's confounded with channel age,
  and it doesn't change our rule of tuning for event count, not a target length.

**Action:** `conv` (math convergence) is planned as an experiment-lane Short, `plans/videos/conv-001-ellipse-focus.md`.

## 10-15 update

Pull 2026-10-15 03:20Z: **32 subs · 17 videos · 16,082 views** (per-video sum; the channel endpoint lags at 14,816).
Snapshot appended. ~48h since the 10-13 03:14Z pull.

| # | Slug | Title (short) | Age | Views | Δ since 10-13 03:14Z | Like% |
|---|---|---|---|---|---|---|
| 17 | conv | Stretch It, Squash It... Do They STILL All Hit Blue? (conv-002) | 8 min | 4 | new | — |
| 16 | esc | The Exit Shrinks Every Round 😳 Can the Ball Still Escape? (esc-006) | 2.0d | **1,415** | new | 1.13% |
| 15 | terr | Which Color Takes the Board? (15:15, long-form) | 2.3d | **819** | **+793** | 0.73% |
| 14 | esc | Will It Escape? The Exit Keeps Moving 🔄 7 Rounds | 3.6d | 1,717 | +134 | 1.75% |
| 13 | race | 12 Marbles, 1 Track: Who Wins the Grand Prix? | 5.0d | 838 | +403 | 0.36% |

v1-v12 are flat (±3). v11 lost 2 views (52 → 50), the routine invalid-view cleanup.

- **Long form isn't dead on arrival: v15 went 26 → 819 in 48h.** That reverses the 10-14 note that called v15 a
  second "long form doesn't inherit a Shorts audience" data point. It was read at 7.5h, before any push arrived.
  It's still one video and still judged on Studio, not API views. At 15:15 even a low avg % viewed adds real
  watch hours, so day 7 (10-19) needs **watch time, avg % viewed, traffic source and subs gained** from Studio.
- **Both 16:9 uploads have the same curve** *(lead, n=2)*. v13 and v15 had a near-zero first day, then a slow
  climb that's still going (v13 ~200/day on days 3-5, v15 ~400/day on days 1-2). Every vertical Short instead
  bursts in day 1 and stops by day 3. That fits YouTube serving 16:9 through browse/suggested, not the Shorts
  feed. Both also have the channel's lowest current-era like% (0.36%, 0.73%), which fits a colder browse audience.
  Studio's traffic-source panel would confirm it. If it holds, **don't read a 16:9 upload's day 1 as a verdict.**
- **esc-006 (shrinking exit) landed in the band**: 1,415 at day 2, against v14's 1,580 at 1.3d and v4's 1,201 at
  day 0. The rotating-gap `esc` arena is now **4 for 4** at ~1.4K or more (v4, v7, v14, v16). It's the proven
  lane. Its like% (1.13%) is below v14's (1.75%), but like% falls with reach, so that's not a quality signal.
- **v14 didn't fully stop**: +134 in 48h on day 2-3.6, after +3 in the 8h before. The tail is small, and it's
  still the best like% of the current era.
- **v17 (`conv-002`) is the first math-convergence upload**, the experiment-lane test of the MathCa lead. 4 views
  at 8 min means nothing. The first read worth having is ~day 1 (10-16). Compare it with the ~1K Shorts band
  and on like%, where MathCa's 1.2-1.9% is the reference.
- **Subs 28 → 32 (+4) over ~2.8K new views (~0.14%).** That's the third pull in a row above the old ~0.1%, but
  it's +4, so it stays a lead.
- **Run rate recovered: ~1,380 views/day over the last 48h**, from ~120/day around 10-10/11 and above the ~800/day
  post-revival average. That's two band-level Shorts plus a long-form climb. It's still ~1% of the 10M/90d Shorts bar.
- **Day-7 reads:** v10 **1,082** (flat since day 3, recorded). v11 (50) and v12 (159) are due 10-16 and won't move,
  so treat these as final.

## 10-16 update

Pull 2026-10-16 01:40Z: **32 subs · 18 videos · 18,248 views** (per-video sum; the channel endpoint lags at 16,038).
~22h since the 10-15 03:20Z pull.

| # | Slug | Title (short) | Age | Views | Δ since 10-15 03:20Z | Like% |
|---|---|---|---|---|---|---|
| 18 | terr | Which Country Takes the Board? (terr-002, 20:22, long-form) | <1d | 6 | new | — |
| 17 | conv | Stretch It, Squash It... Do They STILL All Hit Blue? (conv-002) | ~1d | **1,593** | **+1,589** | **2.64%** |
| 16 | esc | The Exit Shrinks Every Round 😳 (esc-006) | 2.9d | 1,539 | +124 | 1.17% |
| 15 | terr | Which Color Takes the Board? (15:15, long-form) | 3.2d | 1,039 | +220 | 0.58% |
| 14 | esc | Will It Escape? The Exit Keeps Moving 🔄 7 Rounds | 4.5d | 1,720 | +3 | 1.86% |
| 13 | race | 12 Marbles, 1 Track (16:9) | 5.9d | 1,043 | +205 | 0.29% |

v1-v12 flat (±2). **Day-7 finals: v11 51, v12 161.**

- **conv-002 (v17), the experiment-lane test, cleared the band on day 1 with the channel's best current-era like%.**
  1,593 at ~day 1 against the ~1-1.5K proven band, and 2.64% likes, above MathCa's 1.2-1.9% reference and every
  current-era upload (next best v14 1.86%). Like% normally *falls* as reach grows, so a high like% at band reach
  is the strongest quality hint we've had. Still n=1 *(lead)*. Day 7 is 10-22. A second `conv` (conv-001, or a
  new shape) is the cheapest replication, and the Shorts-mix rule says it's now a proven-lane candidate if it holds.
- **The 16:9 slow-climb lead held for both** *(lead, n=2)*. v13 added +205 on day 5-6 (a vertical Short is flat by
  day 3), v15 +220 on day 2.3-3.2. v15 is decaying, ~400/day → ~240/day. If it keeps halving every ~2 days it ends
  near ~1.4-1.5K. That's fine reach but below the ~730/day the 4,000 h route needs; see the strategy note below.
- **v18 (`terr-002`, 20:22) is the second long-form upload.** 6 views at under a day means nothing: both 16:9 uploads
  had an empty day 1. First read at day 3 (10-18), verdict at day 7 (10-22) on Studio numbers.
- **Subs 32 → 32 (+0) over ~2.2K new views.** The ~0.14% of the last pulls didn't repeat. Noise at this size.
- **Run rate ~2,330 views/day over 22h, the best of the revival.** Mostly v17's day-1 burst.
- **Which videos suggest our long form is unanswerable from the API.** `relatedToVideoId` was removed from the
  Data API in 2023, and per-video traffic sources are Analytics-only. Studio has it: video → Analytics → *Reach* →
  *Traffic source types* → **Suggested videos** lists the exact videos we're shown beside. That list is the most
  useful long-form signal we can get right now (see *What would make this useful*).

## 10-17 update

Pull 2026-10-17 09:52Z: **37 subs · 19 videos · 19,831 views** (per-video sum; the channel endpoint lags at 18,748).
~32h since the 10-16 01:40Z pull.

| # | Slug | Title (short) | Age | Views | Δ since 10-16 01:40Z | Like% |
|---|---|---|---|---|---|---|
| 19 | surv | Which Color Survives the Lava? (surv-002 Lava Run, long-form) | <1d | 0 | new (still processing, API shows 0s) | — |
| 18 | terr | Which Country Takes the Board? (terr-002, 20:22, long-form) | ~2.3d | **28** | +22 | 3.57% (1 like) |
| 17 | conv | Stretch It, Squash It... Do They STILL All Hit Blue? (conv-002) | ~2.3d | **2,183** | +590 | **2.43%** |
| 16 | esc | The Exit Shrinks Every Round 😳 (esc-006) | ~4.2d | 1,713 | +174 | 1.11% |
| 15 | terr | Which Color Takes the Board? (15:15, long-form) | ~5.3d | 1,354 | +315 | 0.59% |
| 14 | esc | Will It Escape? The Exit Keeps Moving 🔄 7 Rounds | ~5.9d | 1,719 | −1 | 1.86% |
| 13 | race | 12 Marbles, 1 Track (16:9) | ~7d | **1,500** | **+457** | 0.27% |

v1-v12 flat (±3). **Day-7 final: v13 1,500.**

- **The second long-form upload has not replicated the first.** v18 has 28 views at ~2.3 days. v15 had 819 at the
  same age, and v13 had ~400. Both earlier 16:9 uploads had an empty first day too, but by day 2 they were
  climbing, and v18 isn't. It's still one video and day 7 is 10-22. Three things changed at once, so the cause
  can't be separated: **country theming** (India/Australia/SA/WI vs colours), **length** (20:22 vs 15:15) and the
  title. *Lead (n=3, confounded):* **every country-themed upload is a current-era low**: `cric` v11 51, `cric` v12
  161, `terr-002` v18 28. Every colour-themed upload got a push. v19 (Lava Run) uses colours at long length, so it
  separates the two: if it climbs like v15, theme was the problem; if it stalls like v18, the cause is long-form
  reach or timing.
- **The 16:9 slow climb isn't decaying evenly.** v13 (`race`) added +457 on days 6-7 and is now accelerating, not
  stopping (+205 in the 22h before). v15 added +315 (~235/day), down from ~400/day on days 1-2 but not halving as
  the 10-16 note projected. *Lead (n=2):* 16:9 uploads keep getting a slow browse/suggested drip after a vertical
  Short has stopped. That's the watch-hour route's mechanism, and Studio's traffic sources can confirm it.
- **conv-002 (v17) is still climbing** at day 2.3: +590 after its 1,593 day-1 burst, while other Shorts stop by
  day 3. It's the best Short since v4 (3,169), and its like% (2.43%) held as reach grew. Day 7 is 10-22. It's
  a strong lead for promoting `conv` to the proven lane.
- **esc-006 (v16) has a tail too**: +174 on days 3-4. That's the fifth rotating-gap `esc` in the band, at 1,713.
- **Subs 32 → 37 (+5) over ~1.6K new views (~0.3%)**, the best ratio of the revival. It's +5, so it's noise until it
  repeats. The pull doesn't show which video brought them (Studio: *Subscribers* by content).
- **Run rate ~1,190 views/day over 32h.** That's down from the 10-16 burst but above the ~800/day revival average.

## 10-18 update

Pull 2026-10-18 03:19Z: **42 subs · 20 videos · 23,647 views** (per-video sum; the channel endpoint lags at 19,553).
~17.5h since the 10-17 09:52Z pull.

| # | Slug | Title (short) | Age | Views | Δ since 10-17 09:52Z | Like% |
|---|---|---|---|---|---|---|
| 20 | clack | How Many Times Will They CLACK? Each Block 100x Heavier (clack-001) | <0.75d | **2,694** | new | **2.49%** (5 comments) |
| 19 | surv | Which Color Survives the Lava? (surv-002 Lava Run, 6:45, long-form) | ~0.7d | 38 | +38 | 7.9% (3 likes) |
| 18 | terr | Which Country Takes the Board? (terr-002, 20:22, long-form) | ~3.0d | 129 | +101 | 1.55% |
| 17 | conv | Stretch It, Squash It... Do They STILL All Hit Blue? (conv-002) | ~3.1d | **2,607** | +424 | 2.26% |
| 16 | esc | The Exit Shrinks Every Round 😳 (esc-006) | ~5.1d | 1,718 | +5 | 1.11% |
| 15 | terr | Which Color Takes the Board? (15:15, long-form) | ~6.1d | 1,638 | **+284** | 0.49% |
| 14 | esc | Will It Escape? The Exit Keeps Moving 🔄 7 Rounds | ~7d | 1,719 | 0 | 1.86% |
| 13 | race | 12 Marbles, 1 Track (16:9) | ~8d | 1,779 | **+279** | 0.22% |

v1-v12 flat (±10). **Day-7 final: v14 1,719.**

- **Maths Shorts are 2 for 2, and both beat the proven band by ~1.7x** *(lead, n=2)*. `clack-001` (v20) did 2,694 in
  under 18h, the best first day of the revival (v17 had 1,593 at ~1d, v7 1,524). Its 2.49% likes and 5 comments are
  the era's best on both. `conv-002` (v17) is still climbing on day 3 (+424, ~580/day), when every other vertical
  Short has stopped. They're different formats with the same shape: one prediction, a certain answer, a surprising
  reason. That's the "two in the band" bar from the Shorts-mix rule, cleared by two different builds, so it's a
  shape-level result and not one lucky format. It's still two uploads in four days, and day 7 for both is ahead.
- **The country-theme lead got stronger, and the contest-shape lead is gone.** v13 (`race`, a colour contest)
  looked like a contest-shape flop at 435 on 10-13 and is now 1,779 on day 8, still accelerating. So "head-to-head
  contests get no push" was a day-3 read of a 16:9 upload, the mistake the 10-15 note warned about. The lows that
  remain are all country-themed (v11 52, v12 162, v18 129 at day 3). *Lead (n=3, still confounded with length,
  sport and title).* **Counter-evidence (same day, `reference-channels.md` → *@carsonjay*):** Carson Jay's biggest
  video is *50 Countries Elimination* (7.9M), and its 4 country titles have a 408K median. So the lead is narrower
  than "countries don't work". It's "2-4 nation matches haven't worked *for us*".
- **v18 is climbing now, but ~8x behind v15** (129 vs 1,039 at day 3). That's the day-3 read the strategy asked for.
  v19 (colours, 6:45) has 38 at ~0.7d, ahead of both earlier long uploads at that age (v15 26 at 7.5h, v18 6 at <1d).
  Too early to split theme from long-form reach; day 3 is 10-20.
- **The 16:9 drip is speeding up, not decaying** *(lead, n=2)*. v13 added ~380/day on day 7-8 and v15 ~390/day on
  day 5-6, up from ~235/day. All four 16:9 uploads together ran **~970 views/day**. The watch-hour route needs
  ~2,260 watch-minutes a day to reach 4,000 h by 02-01. That's ~750 views/day at a 3-min average view or ~1,130 at
  2 min. We don't know the average view (Studio), and v13 is only 2:05 long, so its views count for little. It's
  the first pull where the 16:9 total is near the needed pace, though.
- **Subs 37 → 42 (+5) over ~3.8K new views (~0.13%)**, in line with the ~0.1-0.14% of earlier pulls. Subs are up
  32 → 42 in ~2 days (~4.8/day), against the ~9/day the 1K-by-02-01 line needs.
- **Run rate ~5,250 views/day over 17.5h**, the best of the revival by ~2x. About 70% is clack's first day.

## 10-20 update

Pull 2026-10-20 02:09Z: **46 subs · 22 videos · 26,200 views** (per-video sum; the channel endpoint lags at 25,901).
~47h since the 10-18 03:19Z pull.

| # | Slug | Title (short) | Age | Views | Δ since 10-18 03:19Z | Like% |
|---|---|---|---|---|---|---|
| 22 | elim | 16 Marbles, 6 Races, Only 1 Survives (elim-001, 9:51, long-form) | ~1.1d | 7 | new | 1 like |
| 21 | brach | Same Start, Same Finish... Which Ball Gets There FIRST? (brach-001) | ~2.1d | **1,220** | new | 1.48% |
| 20 | clack | How Many Times Will They CLACK? (clack-001) | ~3.1d | 2,879 | +185 | 2.57% |
| 19 | surv | Which Color Survives the Lava? (surv-002, 6:45, long-form) | ~3.1d | 73 | +35 | 5.5% (4 likes) |
| 18 | terr | Which Country Takes the Board? (terr-002, 20:22, long-form) | ~5.1d | 198 | +69 | 1.0% |
| 17 | conv | Stretch It, Squash It... Do They STILL All Hit Blue? (conv-002) | ~5.1d | **2,945** | +338 | 2.41% |
| 16 | esc | The Exit Shrinks Every Round 😳 (esc-006) | ~7.1d | 1,721 | +3 | 1.1% |
| 15 | terr | Which Color Takes the Board? (15:15, long-form) | ~8.1d | **2,231** | **+593** | 0.4% |
| 13 | race | 12 Marbles, 1 Track (16:9) | ~10d | 1,837 | +58 | 0.22% |

v1-v12 and v14 flat (±5). **Day-7 finals: v16 1,721; v15 ~1,900** (missed on 10-19, interpolated).

- **Long form is 0 for 3 at replicating v15** *(lead, n=4)*. At day 3: v15 1,039, v18 (countries, 20:22) 129,
  v19 (colours, 6:45) 73. v22 (`elim`, 9:51) has 7 at day 1. v19 was the test that split theme from long-form reach,
  and it came in *below* the country upload. **So country theming doesn't explain the long-form lows**, and the
  10-17 pause rests only on the two `cric` Shorts now. Length doesn't explain it either (6:45, 9:51 and 20:22 all
  stalled). v15 is the outlier, not the baseline. Studio's traffic sources for v15 are the only way to see what
  pushed it (browse, suggested from which video, or search).
- **The 16:9 drip is slowing, which reverses the 10-18 lead.** v13 went from ~380/day to ~29/day (+58 in 2 days, so
  it has stopped). v15 is still going at ~300/day, down from ~390. All 16:9 uploads together ran **~380 views/day**,
  from ~970 on 10-18. The watch-hour route needs ~770/day at a 3-min average view, so it's now ~2x off pace again,
  carried by one video.
- **Maths Shorts: 2 of 3 above the band** *(lead, n=3)*. `brach-001` (v21) has 1,220 at day 2.1, in the ~1-1.7K
  proven band, with 1.48% likes (the other two had ~2.5%). conv-002 and clack-001 both stopped near ~2.9K (clack added
  only +185 after its first-day burst). By the Shorts-mix bar (two in the band) maths stays proven, but "maths beats
  the band by 1.7x" was n=2 and doesn't hold for every topic. One difference to check against the next maths upload:
  brach's question is *which wins?* (a pick, like a race), while conv and clack ask *how many?* / *will they all?*
  with a number or a yes as the answer.
- **Subs 42 → 46 (+4) over ~2.55K new views (~0.16%)**, in line with recent pulls. ~2/day over this window, against
  the ~9/day needed for 1K by 02-01.
- **Run rate ~1,300 views/day**, back near the revival average after the clack burst.

## 10-24 update

Pull 2026-10-24 02:45Z: **58 subs · 25 videos · 30,985 views** (per-video sum; the channel endpoint lags at 29,377).
~4.0 days since the 10-20 02:09Z pull. Ages are from publish dates (the API returns days only).

| # | Slug | Title (short) | Age | Views | Δ since 10-20 | Like% |
|---|---|---|---|---|---|---|
| 25 | conv | How Many of 1000 Balls Hit the Dot? (conv-003 parabola) | ~1.1d | **1,321** | new | 1.67% (2 comments) |
| 24 | royale | 4 Colors, 1 Exit, 1 x2… Which Color SURVIVES? (royale-001) | ~3.1d | 1,233 | new | 1.14% |
| 23 | press | Every Crushed Ball Splits in 2... (press-001) | ~4.1d | **1,425** | new | 1.54% |
| 22 | elim | 16 Marbles, 6 Races, Only 1 Survives (elim-001, 9:51, long-form) | ~5.1d | 229 | +222 | 0.87% |
| 21 | brach | Same Start, Same Finish... (brach-001) | ~6.1d | 1,242 | +22 | 1.45% |
| 20 | clack | How Many Times Will They CLACK? (clack-001) | ~7.1d | **2,970** | +91 | 2.59% |
| 19 | surv | Which Color Survives the Lava? (surv-002, 6:45, long-form) | ~7.1d | 90 | +17 | 4.4% |
| 18 | terr | Which Country Takes the Board? (terr-002, 20:22, long-form) | ~9.1d | 213 | +15 | 0.94% |
| 17 | conv | Stretch It, Squash It... (conv-002) | ~9.1d | 2,957 | +12 | 2.4% |
| 15 | terr | Which Color Takes the Board? (15:15, long-form) | ~12d | **2,605** | **+374** | 0.42% |
| 4 | esc | Will It Escape? The Exit Keeps Moving | ~25d | 3,190 | +40 | 0.53% |

Others flat (±15). **Day-7 finals: v19 90, v20 2,970. v17 ~2,950 and v18 ~205 missed on 10-22** (interpolated).

- **The band is now the ceiling as well as the floor** *(lead, n=4)*. brach (1,242, final), press (1,425, day 4),
  royale (1,233, day 3) and conv-003 (1,321, day 1) all landed at 1.2-1.4K. Two experiments and two maths Shorts, so
  the format didn't move the number. The ~1-1.7K first push looks like what the channel gets per Short for now, and
  only conv-002, clack-001 and v4 have broken it (~3K each). The experiments are cheap insurance: none has fallen below.
- **conv-003 is behind conv-002 on day 1** (1,321 vs 1,593), and its like% is lower (1.67% vs 2.64%). It asks
  *how many?*, the question type the 10-20 note favoured over brach's *which wins?*, so the question type alone isn't
  the lever. Too early to call: conv-002 kept climbing to day 5. **Read it on day 3 (10-26).** The other open
  variable: conv-002 and clack had a *twist* (the break round, the π digits), and conv-003 and brach are pure
  escalations with no twist. That's what the conv-003 plan called the experiment. *Lead to watch, n=2 each side.*
- **Long form is 0 for 4 at replicating v15**, but v22 (`elim`) is the best of the four after v15: 229 at day 5, against
  v18's 198 and v19's 90 at their day 7. It's still ~10x behind v15. v15 keeps a tail (+374, ~95/day on days 8-12).
- **The 16:9 drip is ~170 views/day** (v15 ~95, v22 ~55, the rest ~20), from ~380 on 10-20. The watch-hour route
  needs ~800 long-form views/day at a 3-min average view, so it's ~5x off pace.
- **Subs 46 → 58 (+12) over ~4.8K new views (~0.25%)**, the best conversion of any pull (0.1-0.16% before). It's
  one window and 12 subs, so it's noise until it repeats; Studio's per-video subs would say which upload drove it.
  ~3/day, against the ~9.4/day the 1K-by-02-01 line needs.
- **Run rate ~1,190 views/day**, the revival average. Three Shorts in four days gave about the same rate as one did.

## 10-27 update

Pull 2026-10-27: **60 subs · 27 videos · 33,393 views** (per-video sum; endpoint 32,922). ~3 days since the 10-24 pull.

| # | Slug | Title (short) | Age | Views | Δ since 10-24 | Like% |
|---|---|---|---|---|---|---|
| 27 | bell | Do 1,000 Marbles Make a BELL Curve? (Short) | ~2d | 1,189 | new | 1.35% |
| 26 | mach | 14,000 Marbles in a Giant Marble Machine (5:33, long-form) | ~2d | 406 | new | 0.25% |
| 25 | conv | How Many of 1000 Balls Hit the Dot? (conv-003) | ~4d | 1,423 | +102 | 1.55% |
| 24 | royale | 4 Colors, 1 Exit, 1 x2 (royale-001) | ~6d | 1,313 | +80 | 1.22% |
| 23 | press | Every Crushed Ball Splits in 2 (press-001) | 7d | 1,434 | +9 | 1.53% |
| 22 | elim | 16 Marbles, 6 Races (elim-001, 9:51, long-form) | ~8d | **573** | **+344** | 0.52% |
| 15 | terr | Which Color Takes the Board? (15:15, long-form) | ~15d | 2,769 | +164 | 0.4% |

Others flat (±25). Day-7 final: v23 1,434. v21 and v22 snapshots were missed.

- **The Short band still holds** *(validated, n=7)*. press 1,434, royale 1,313, conv-003 1,423, bell 1,189 (day 2, may climb a little),
  brach 1,246. Seven Shorts in a row at 1.2-1.4K. The format and topic aren't moving the number; only conv-002, clack and v4 broke it.
- **Long form is showing legs for the first time since v15** *(lead, n=2)*. v22 (`elim`) grew 229 → 573 in 3 days (~115/day), and v26
  (`mach`) has 406 at ~2d, the best day-2 of any long-form upload except v15 (~800 at 2.3d). Both are 5-10 min marble-centric
  videos, unlike the 15-20 min `terr` pair. Two videos is not a trend, and the 0.25-0.5% like rates are low. Watch v26 on 10-30.
- **Subs 58 → 60 (+2)** over ~2.4K new views (~0.08%), back to the old rate; the 10-24 conversion was a one-window spike.
- **Run rate ~800 views/day on existing videos plus new uploads**; slightly down from ~1,190/day.
- Retention is unreadable from the API; v22's growth would need Studio to say whether it's watched.

## 10-27 reference note: compilations (@Satisfying2DSims)

Full pull of its 23 long-form uploads (details in `reference-channels.md` → *@Satisfying2DSims: its compilations*).
- **Compilations are 13% of its views but ~5-30x its Shorts median per upload** *(validated within one channel, n=23)*: 8-10 min
  "N Most Satisfying Simulations" videos, 40-130K typical, 1.03M and 714K at the top. Most were built from clips already published.
- **This qualifies, not overturns, "compilations get nothing"** *(lead)*. The 7-view Bouncy Physics case stands, but S2DS shows the
  format works once a library of proven Shorts exists. Our library is ~20 Shorts, with ~7 in the 1.2-1.4K band and 3 at ~3K.
- Single reference channel; a rule on this needs a second one. See the *Multi-channel evidence* rule.

## 10-29 update

Pull 2026-10-29: **61 subs · 28 videos · 34,556 views** (per-video sum; endpoint 34,245). ~2 days since the 10-27 pull.

| # | Slug | Title (short) | Age | Views | Δ since 10-27 | Like% |
|---|---|---|---|---|---|---|
| 28 | compilation | 10 Most Satisfying Simulations (8:08, long-form) | ~1d | 10 | new | 10.0% (1 like) |
| 27 | bell | Do 1,000 Marbles Make a BELL Curve? (Short) | ~4d | 1,299 | +110 | 1.62% |
| 26 | mach | 14,000 Marbles in a Giant Marble Machine (5:33, long-form) | ~4d | **1,090** | **+684** | 0.46% |
| 25 | conv | How Many of 1000 Balls Hit the Dot? (conv-003) | ~6d | 1,430 | +7 | 1.61% |
| 24 | royale | 4 Colors, 1 Exit, 1 x2 | ~8d | 1,317 | +4 | 1.21% |
| 22 | elim | 16 Marbles, 6 Races (9:51, long-form) | ~10d | 743 | +170 | 0.54% |
| 15 | terr | Which Color Takes the Board? (15:15, long-form) | ~17d | 2,876 | +107 | 0.42% |

Others flat (±15). Day-7 reads: v24 ~1,317 (recorded late), v25 due 10-30 (1,430 now).

- **`mach` is the second long-form upload to get a real push** *(lead, n=2 with v15)*. 406 at ~2d, 1,090 at ~4d, ~340/day and
  accelerating. v15 had 819 at 2.3d and 1,039 at 3.2d, so mach tracks it a day or so behind. It's 5:33, 3D, marble-centric, with a
  one-line question (which colour fills first?). Like% rose from 0.25% to 0.46%, still low. The 10-27 guess (5-10 min marble
  videos, not 15-20 min `terr`) now has two of its candidates growing: `elim` ~85/day, `mach` ~340/day.
- **Long form is 2 for 6 at "a real push" (v15, v26), 1 more with a drip (v22).** v18 (country) and v19 (lava) are the two that
  did nothing. Don't read it as a rule: v26 and v22 differ in format, and Studio's average view duration is still unread.
- **The 16:9 drip is ~490 views/day**, up from ~170/day on 10-24 (mach ~340, elim ~85, terr ~55, rest ~10). The watch-hour route
  needs ~800/day at a 3-min average view, so it's ~1.6x off pace, from ~5x. It rests on one new video, so it can fade.
- **The compilation (v28) has 10 views at ~1d**, the same as v22 (7 at 1.1d). Too early to say anything; the 10-27 S2DS lead
  (compilations work once a library exists) stays untested. **Read it on 10-31 (day 3).**
- **The Short band holds** *(validated, n=8)*. bell is 1,299 at day 4, the top of the band but not above it. Eight Shorts in a
  row at 1.2-1.4K. Only conv-002, clack and v4 have broken it.
- **Subs 60 → 61 (+1)** over ~1.2K new views (~0.09%), the old rate. ~0.5/day, against ~9/day needed.
- **Run rate ~580 views/day** (+1,163 in ~2d), with mach carrying more than half of it.

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

- **Day-7 snapshots: v3 (899), v4 (3,150), v5 (246), v6 (1,188), v7 (1,649), v8 (1,742), v9 (1,041), v10 (1,082) done; v11 (51), v12 (161) done; due v13 (1,500), v14 (1,719) done; due v15 10-19, v16 10-20, v17 10-22, v18 10-22, v19 10-24, v20 10-24.** They
  can't be recovered if missed.
- **Studio → Suggested videos list for v15 and v18** (Reach → Traffic source types → Suggested videos): which channels
  and titles we're shown next to. Paste it in and it becomes the long-form direction (see *10-16 update*).
- **Studio traffic source for v13 and v15** (Shorts feed vs browse/suggested), to test the 16:9 slow-climb lead.
  Plus v15's watch time and avg % viewed at day 7.
- **Studio real-time view for 10-03 → 10-04**, to settle whether the older videos really stopped or
  the public counter is lagging.
- **Pull Avg % viewed + drop-off from Studio for v4, v5, v6 and v7.** v4 vs v5 (best vs worst) and
  v4 vs v7 (same arena, song hook added) are the two comparisons most worth explaining.
- Write the first retros into `KnowledgeBase/retros/`. There are none yet, and five current-era
  videos are now waiting.
