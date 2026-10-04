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
Our URP 3D videos (v5, v6) haven't broken out from our 2D ones yet. **So for now the realistic route
is volume with a quality floor:** 1–3 Shorts/day, each a scanned, legible run. Push per-video reach
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
