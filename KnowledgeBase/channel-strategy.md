# Channel Strategy — Simulation Lobby

## Positioning

A "lobby" of simulations: viewers drop in, pick a contender, watch physics decide. The channel
identity is the *variety of formats under one consistent visual language*, not any single format.

What makes this category work on YouTube:

- **Zero-context entry.** Nothing to learn. Rules are visible in the first three seconds.
- **Built-in stakes.** Someone wins, someone loses, and it isn't authored — viewers trust physics.
- **Parasocial pick.** Viewers adopt a color/name and root for it. This is the comment engine.
- **Infinite variation from one build.** New seed, new config, new video.

## The goal: monetization

**Get into the YouTube Partner Program through the Shorts path: 1,000 subscribers + 10M Shorts
views in the trailing 90 days.** For new applicants that becomes **20M on 2027-02-01**, so the
practical target is the 90 days before then. Math and current gap: `plans/channel-revival.md`.

**The 10M is *engaged* views, not the public counter.** Since 2025-03-31 the public Shorts count
includes every start and replay. YPP eligibility still uses the old metric, now called "engaged
views" (Studio ▸ Advanced Mode). Public counts overstate progress, most of all on very short loops
that get replayed. Track engaged views in Studio; the API can't see them.

## Rethink 2026-10-11: earning, not just qualifying

**Status: proposed, awaiting the user's call.** The user restated the goal on 2026-10-11 as *earning
from ads*. That changes what each track is for, and the 10-11 numbers (`learnings.md` → *10-11
update*) change what's realistic.

**1. Entry and earnings come from different places.** YPP admits on either route, but once in, money is
paid per view, and Shorts pay far less per view than long-form. Creators commonly report Shorts ad
revenue in the **cents per 1,000 views** and long-form in **dollars per 1,000** (unverified for our
niche; read RPM in Studio once in). So even 10M Shorts views a quarter is a small cheque. The same
revenue from long-form needs a small fraction of the views. Note that the lower YPP tier (fan-funding
only) pays no ad revenue; we need the full tier.

**2. The Feb-1 Shorts deadline is not reachable on current numbers.** ~800 views/day since revival
(~120/day since 10-10) against ~111K/day needed. Only a breakout changes that, and breakouts can't be
scheduled. Stop letting the 2027-02-01 date drive build choices. After that date the bars are 20M
Shorts views/90 days or 8,000 watch hours/12 months; both are long campaigns, and watch hours
**accumulate for 12 months** while Shorts views expire after 90 days.

**3. Subs bind on every route.** 1K subs at our ~0.09% needs ~1M views. Whatever converts viewers to
subs better is worth more than raw views. Long-form's sub rate is untested for us.

**Proposed shape (replaces the throughput-first emphasis, keeps the two tracks):**

| | Shorts | Long-form |
|---|---|---|
| Job | Reach, subs, breakout tickets. **Not** the earnings engine | Watch hours (qualification) and the earnings engine once in |
| What to make | **Variants of the shapes that got the push**: threshold hooks (`esc` rotating gap, `brk`, `rom`, `impact`). Config-only, cheap. Pause contest-shaped Shorts (`cric`, race clips) until the 10-11 dip is explained | Real 8-15 min 16:9 uploads. The first one hasn't happened yet: race-001 went up at 2:05 |
| Cadence | 1/day is enough while diagnosing; more only if the floor holds | 1/week; 2/week only once a series template makes it cheap |
| Judge on | Viewed-vs-swiped and day-7 views | Avg view duration, subs gained per video, watch hours |

**Decision points:**
- **Next Short (diagnostic):** a proven threshold variant. ~1K means the contest shape/topic was the
  problem; <200 means a channel-level dip, and format choice isn't the lever.
- **After 4 real long uploads:** if average view duration is under ~2 min or none gains a sub, the
  long-form format needs rework before more volume; if subs-per-view beats Shorts', long-form leads.

## Audience and the pick mechanic

The single highest-leverage habit: **make it trivially easy to pick a side in the first 3 seconds.**
Named, high-contrast, few enough to track (8–16 for Shorts, up to 100 for long-form spectacle where
the *mass* is the appeal rather than individual tracking).

Ask for the pick explicitly on screen and in the pinned comment. "Comment your color before it ends."

## Shorts vs long form

| | Shorts | Long form |
|---|---|---|
| Length | **by format, see below**: 10–20s for 3D spectacle loops, 45–75s for 2D story sims | 8–20 min |
| Aspect | 9:16 1080x1920 | 16:9 1920x1080 |
| Job | Discovery + subscribe; the YPP qualifying route | Watch time + session depth; higher earnings once in YPP |
| Best formats | `esc`, `rom`, short `race`, `surv`, `impact` | `wars`, `elim`, tournament `race` |
| Structure | Hook → rules → run → finish | Cold open of the climax → rules → rounds → finale |

Shorts are the top of the funnel and the fastest route into YPP. Shorts views don't count toward the
4,000/8,000 watch-hour route, so long form is a second track, not a supplement.
**As of 2026-10-09 both run in parallel: see *Two-track plan* below.** The *Best formats* row above
is the starting point; `race` and `elim` are the long-form anchors.

## Two-track plan (decided 2026-10-09)

**Direction:** run Shorts and long-form together. **1-2 high-production long-form videos a week, and
Shorts more often than today's ~0.7/day.** Decided after the Black Cat-Gear study
(`reference-channels.md`): long-form is the only content that compounds, and it is the second YPP route
(watch hours), which Shorts views never feed. This replaces the earlier "one cheap long-form test"
recommendation in `learnings.md`; the test is now the first weeks of the plan.

| | Track A: Shorts | Track B: long-form |
|---|---|---|
| Job | Discovery, subs, the 10M route | Watch hours, evergreen search/suggested, session depth |
| Cadence | 1-2/day (up from ~0.7/day), batch-built | **1/week while ramping, 2/week once the race/elim blocks exist** |
| Aspect / length | 9:16, by format (see *Length*) | 16:9, 8-15 min |
| Bar | Legible, one clear hook, config-only variants | **Studio-look 3D, procedural score and SFX, a cold open, a leaderboard, an intentional finale** |
| Source | Own runs, plus climax clips cut from Track B | Heats composed through `ISimulation` (`race`, `elim`, `wars`, `cric`) |

**Why this can work on our tooling.** Long-form follows the same rule as the code: compose, don't
rewrite. A video is a *series template* (Black Cat-Gear's "The [Theme] Marble Race") where only
the theme, track and config change between uploads, so the second one costs a fraction of the first. The
`(HD)` re-render habit on that channel is what our seed + config design is for.
**Each long video also feeds Track A**: the climax, a near-miss and the finish cut into 2-3 Shorts, so
long-form raises Shorts throughput instead of competing with it.

**Build order (infrastructure before volume, per `PROJECTS.md` → *How to pick next*):**
1. `race` 3D + contender identity + leaderboard + follow camera (all `idea` today, `elim` is blocked on
   it). This is the engine for the anchor series.
2. `capture-long`: 16:9 1920x1080 60fps Recorder path, plus end cards. `capture-shorts` was deferred
   because the user records by hand; at 1-2 long videos a week plus daily Shorts, hand-recording is the
   likely bottleneck. Revisit it with this.
3. Series bible for the anchor: 3 themes, a title template, a finale rule, a sequel/(HD) policy.
4. `elim` bracket wrapping `race`; then `wars` and a full `cric` match as second and third series.
Until step 1 lands, the first long upload is a stopgap built from what exists (for example a long
`rom` run of many rounds, or a full `cric` match) so the channel gets its first long-form data point.
It is labelled a stopgap and shouldn't be judged as the anchor series.

**What it has to answer (judge on Studio, not on public views):** average % viewed and drop-off, subs
gained per long video, and day-7/28 views. One long video at our size is noise. **Review after 6 long
uploads.** Proposed continue / cut rule, to be confirmed with the user: if the median long video is
under ~2K views at day 28 *and* gains almost no subs, drop to 1/week and rework the format; if long-form
subs-per-view beats Shorts' ~0.1%, make it the priority track.

**YPP math, with its assumptions.** Before 2027-02-01 the long-form route needs 4,000 public watch hours
in 12 months, and 8,000 after (`channel-revival.md` → *The goal*). 4,000 h is 240,000 min. If a 10-min
video holds ~3.5 min per view (**an assumption; we have no retention data**), that is ~69K views, or
~2.9K per video across ~24 videos at 1.5/week for 16 weeks. That's plausible against Black Cat-Gear's
100-700K typical lifetime, but those are after years of accrual, not 16 weeks. Subs are the harder
limit: **1,000 subs at ~0.1-0.18% needs 0.5-1M views on either route.** Treat both tracks as feeding
one sub count. Verify the current thresholds in Studio.

**Risks, stated plainly:**
- **Black Cat-Gear itself slowed** from ~2.5 uploads/month to ~1 as production got heavier. 1-2 a week
  at high production is 4-8x its current pace; it only holds if each upload is a template swap, which
  it isn't until the `race` engine exists.
- **A new channel's long-form depends on search and suggested** traffic that we have no history for. The
  first several uploads may see very little. Shorts and Track B cross-promote through cut-down clips
  and a pinned link, but that is a hope, not a result.
- **Capacity.** The user runs and plays Unity by hand. Four-plus deliverables a week means the
  hands-on steps (playtest, record, upload, Studio pass) are the cap. Cut Shorts before cutting
  quality on long-form.

## Length — what the data says (2026-10-04)

Pulled every upload from 8 reference channels (~1,100 Shorts since 2024-10-15, when 3-minute Shorts
became possible) and compared each video to **its own channel's median**, so a big channel can't
swamp a small one. Full table: `reference-channels.md` → *Length vs views*.

| Length | Shorts | Median vs own channel | Breakouts (≥5× own median) |
|---|---|---|---|
| <15s | 367 | ×1.11 | 14.7% |
| 15–30s | 290 | ×0.99 | 13.8% |
| 30–60s | 371 | ×0.98 | 8.1% |
| 1–2 min | 62 | ×0.81 | 21.0% (all 13 from CodeCraftedPhysics) |
| 2–3 min | 6 | ×0.51 | 0 of 6 |

**What it means:**

1. **"Short wins" is a sub-genre effect, not a law.** The <30s channels are all 3D spectacle loops
   (MusicMarble3D 11s, RenderZen 15s, Oyen 18s). The 2D story sims sit at 30–70s
   (satisfying2dsims 34s, CodeCraftedPhysics 58s). Each channel settles on the length that fits its
   content, so pooled medians mostly reflect *which kind of content* sits in each bucket.
2. **Longer works when the extra time is tension, not padding.** CodeCraftedPhysics (2D physics, our
   genre) has its three biggest hits at 63–70s (25.8M, 18.8M, 16.4M). In 2026 its 60s+ uploads
   outperform its sub-60s ones in most months. A 2D run with escalation and a payoff earns a minute.
3. **2–3 minutes is basically untested.** Only 6 of ~1,100 genre Shorts are that long. Our best
   video (v4, 2:05) is one of very few data points. It isn't proven to hurt, and it isn't proven to
   help.

**Why short tends to win in the feed:** the feed decides a Short's reach from how many viewers keep
watching rather than swipe, and how much of it they watch. Every extra second has to hold the
viewer, so a longer video needs more events, not more time. Short loops also replay, which pads
the *public* count but not the engaged views YPP uses.

**Rule for us:** set length by format, not by a target.
- **3D spectacle (`impact`, `surv`)**: 10–20s, loopable, payoff in the first few seconds.
- **2D story (`esc`, `race`, `rom`)**: 45–75s, the band where the genre's 2D hits sit. Keep the
  escalation that makes it work, and cut rounds rather than slowing anything down.
- **Our 2-min `esc` is the control.** Test a ~60–70s cut against it as a one-knob change before
  moving the whole format.

## Cadence

Two routes clear 10M+ per 90 days in this genre (Shorts published in the last 90 days, live pull
2026-10-04):

| Channel | Shorts / day | Median views | Views on those uploads | Route |
|---|---|---|---|---|
| `@satisfying2dsims` | 2.8 | 34K | 19.7M | **Volume**: 2D, 34s, posts constantly |
| `@Kawaken_3DCG` | 0.3 | 1.37M | 119.6M | **Quality**: Blender 3D spectacle, 36s |
| `@Oyen_3D` | 0.2 | 569K | 61.0M | **Quality**: Blender 3D, 18s |
| `@renderzen` | 0.4 | 117K | 8.2M | Between the two; just under the bar |
| `@CodeCraftedPhysics` | 0.16 | 252K | 7.6M | Quality, 2D, 58s; just under the bar |

The quality route earns 10–40× more per video, but it rests on Blender-grade renders we don't have.
Our URP 3D videos (v5, v6) haven't broken out from our 2D ones yet. **So for now the realistic Shorts route
is volume with a quality floor** (Track B, long-form, is separate, see *Two-track plan*): 1–3 Shorts/day, each a scanned, legible run. Push per-video reach
up through hooks, length fit and titles while that runs.

Daily Shorts is realistic once the pipeline is deterministic and batched. The build is reusable, so
the marginal cost of a Short is a config, a scan and a render. Every manual minute per video is
what caps cadence, so automating capture comes before new formats.

Batch production: build/refine a format one week, scan and render many variants, then publish over
following weeks. Never build and publish the same day.

## Two kinds of tension — carry both

Studying the wider genre (see `reference-channels.md`) surfaced a distinction worth building around:

- **Competitive tension** — *who wins?* Multiple contenders, viewer picks a side, comments follow.
  Covered by `race`, `wars`, `elim`, `surv`.
- **Threshold tension** — *will it happen?* One object, one binary outcome, escalating toward it.
  Covered by `esc` and `rom`.

These retain for different reasons and fail for different reasons. A channel running only one kind
is fragile to that kind going stale. Keep both in rotation.

**Two more mechanisms surfaced studying adjacent channels (2026-09-29, see `reference-channels.md`
"Adjacent categories" and the `CodeCraftedPhysics` section) — not yet built, tracked as ideas only:**

- **Single-event spectacle** — *what happens when X meets Y?* No escalation, no threshold, one
  dramatic collision resolved fast (15–40s). The single best-performing shape across the three
  Blender channels that run it (`Kawaken_3DCG`, `RenderZen`, `Oyen_3D`) — worth knowing this exists
  as a category even before deciding whether to build it, since "cheapest single collision" is a
  different production bet than anything currently in the format table. **Prototyped 2026-09-29 as
  `impact`:** rigid scatter settles in 3-4s, so the cheap version isn't a video, and the build
  turned into survival instead (`formats/impact.md`).
- **Generative curiosity** — *what does this become?* No win condition, no threshold; a system
  evolves under its own rule and the hook is purely watching what it turns into
  (`CodeCraftedPhysics`, 467K subs, current-performing). The highest creative cost of the four —
  can't be tuned by a single config knob the way `esc` can.

See `ideas-backlog.md`'s "New categories" section for working names and build-cost notes on both.

## Shorts mix: proven + experiment (decided 2026-10-13)

The user's standing rule: **every week of Shorts mixes tried-and-tested uploads with new experiments, and the
experiments keep real room for creative ideas.** Proven shapes protect the ~1K floor. Experiments are how the
next v4 gets found; reference data only shows what already worked for someone else.

**Ratio: about 2 proven to 1 experiment.** Every third Short is an experiment, and there's at least one a week.
If proven shapes start to fade (two in a row under the band), go to 1:1. A fading proven shape means it's time
for something new.

**Proven lane.** A shape counts as proven once it has 2+ uploads inside the ~1-1.7K band. Today that's the
`esc` rotating gap (v4, v14, plus v7 with a song), and `brk`/`esc` with a song (v7, v8). `rom` and `impact` have one
in-band upload each, so they're one result from joining. Proven uploads are config-only variants that change one
knob. The `esc` rotating gap is the **control**: when an experiment stalls, the next proven upload tells us
whether the channel or the experiment is at fault (that's how v14 diagnosed v11-v13).

**Experiment lane.** A new mechanic, format, visual style, topic or hook. It doesn't need reference-channel
evidence; an original idea is exactly what this slot is for. Reference-backed ideas fill it when nothing
original is ready. Rules:
1. **Cheap to build.** It should take 1-2 sessions, mostly composed from `Shared` blocks. If it needs a new
   block, that block should be reusable afterwards. Built in batch, never on the day it's posted.
2. **Keep the Shorts floor.** The run starts within 3s, there's a real way to lose (*Things to avoid*), and it
   reads at 480px.
3. **Change one axis where you can:** mechanic *or* topic *or* tension shape. `cric` changed shape and topic at
   once, so its flop can't say which one failed. A bold experiment is fine, as long as we know what it's testing.
4. **Write down what it tests before posting** (one line in the video plan), then judge it at day 7 against the band:
   - **≥ ~1K:** make a second one. Two in the band moves it into the proven lane.
   - **200-1K:** iterate once on the most likely weak point (hook, title, opening seconds), then decide.
   - **< 200:** shelve it, write down why in `learnings.md`, and post a proven upload next as the control.
5. **A flop costs one slot, not the channel.** v11-v13 flopped and v14 still landed in the band right after.

**Current experiment slate** (details in `ideas-backlog.md` → *Experiment slate*): *Multiply vs Escape* (new),
*Reach the Core* (reference-backed), *Glass Gauntlet* (3D, studio look).

## Hooks that reliably work

- Countdown/odds shown before the start ("Red 12% · Blue 31%").
- A visible imbalance the viewer expects to resolve ("the tiny one against 50").
- Near-elimination saves — design arenas that produce them rather than editing them in.
- Numbers going up on screen (counts, multipliers, survivors remaining).
- **Ask for a guess** ("How many hits to destroy 300 blocks?"). The viewer commits to a number
  up front, then checks it against a live tally. Pure comment bait. *Untested:* video #1 used it at
  numbers too small to read, and `surv-001` is the first real test. Never show the total on screen
  (it's the answer), and never count something the viewer wouldn't (a miss isn't a hit).
- **Collision-triggered audio.** Bounces play successive notes of a melody that becomes recognizable
  as the action accelerates. This is a top-tier retention device in this genre and it is generated
  by the simulation, not added in the edit. Applies well beyond `esc` — test it on `race` and `surv`.

## Things to avoid

- Slow starts. The run begins within 3 seconds or the Short is dead.
- Too many contenders to track in 9:16 — beyond ~16 the viewer stops having a horse in the race
  unless the format is explicitly about the swarm.
- Visually identical contenders. Color alone is not enough; vary shape or trail too.
- Fake outcomes. If runs are scripted and viewers sense it, the trust that carries the category is gone.
- **A question with a guaranteed answer.** "Can it break all 5 rings?" is no question if the ball
  always wins given time. Every threshold format needs a real way to lose: a clock, growth that
  traps it (`esc`), a hit budget. Check this before building, not after the first scan (`brk-001`).

## Title / thumbnail patterns

Titles state the premise as a question or a number: "100 Marbles, One Survives", "Release or Multiply?".
The genre has shifted toward **outcome or reaction statements with a number** (*"It crushed one ball
into 15,939 pieces 💀"*, *"It missed by one ball"*). Questions still work. Test both
(`reference-channels.md`).
Thumbnails: one readable subject, huge number, high-saturation contrast, no small text.

## Open questions

- Persistent contender identities across videos (recurring "characters") — worth testing for retention?
- Live/premiere runs where chat picks sides?
- Leaderboards carried across a season?
