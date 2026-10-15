# Learnings — video vs stats

Live pull from the YouTube Data API (`youtube` skill, `videos`/`report` commands). Latest: **2026-10-13 03:14Z** (see *10-13 update* → *03:14Z pull*).
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
- **Compilation long form on a young Shorts channel got 7 views** (Bouncy Physics, 6:58). This is a second data
  point after our own v15 that long form doesn't inherit a Shorts audience *(illustrative)*.
- **Shorter on Bouncy Physics coincided with more views** (1:05-1:13 → 22-44s). It's confounded with channel age,
  and it doesn't change our rule of tuning for event count, not a target length.

**Action:** `conv` (math convergence) is planned as an experiment-lane Short, `plans/videos/conv-001-ellipse-focus.md`.

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

- **Day-7 snapshots: v3 (899), v4 (3,150), v5 (246), v6 (1,188), v7 (1,649), v8 (1,742), v9 (1,041) done; due v10 10-13, v11/v12 10-16, v13 10-17, v14 10-18, v15 10-19.** They
  can't be recovered if missed.
- **Studio real-time view for 10-03 → 10-04**, to settle whether the older videos really stopped or
  the public counter is lagging.
- **Pull Avg % viewed + drop-off from Studio for v4, v5, v6 and v7.** v4 vs v5 (best vs worst) and
  v4 vs v7 (same arena, song hook added) are the two comparisons most worth explaining.
- Write the first retros into `KnowledgeBase/retros/`. There are none yet, and five current-era
  videos are now waiting.
