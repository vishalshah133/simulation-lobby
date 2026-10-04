# Reference Channels

What's working elsewhere in the genre, and what we take from it. Update as new channels are studied.

> FuncFlow's section was originally written from third-party documentation. It was verified by
> live API pull on 2026-10-03 (see [2D pass](#2d-pass--2026-10-03)), and the caveat is retired.

---

## FuncFlow — `youtube.com/@FuncFlow`

**Genre:** "Will the Ball Escape?" / ball-bouncing physics Shorts. Documented as one of the channels
that popularized the format, alongside `satisfying.ba11s` (the originator, Nov 7 2023), Jack's
Physics, and CodeCraftedPhysics.

**Scale of the format:** individual videos in this genre reached 38M views in five months and 67M in
four. The trend ran hot from Nov 2023 through 2024. Whether it still performs in 2026 is the open
question — test before committing a series.

**What we took:**

1. **A new format family — `esc`.** See `formats/containment-escape.md`. Our original five formats
   were all competitive (who wins?). This one is threshold-based (will it happen?). Different tension
   mechanism, different failure modes, and proven at enormous scale.
2. **Collision-triggered melody.** Each bounce plays the next note; the tune resolves as the action
   peaks. This is *the* retention engine of the genre and it's simulation-driven, not an edit-stage
   addition. Generalizes to our other formats — built as `Shared/BounceAudioSequencer`.
3. **Production economics.** One ball, one boundary, one escalation rule is dramatically cheaper than
   a 200-marble war. This is the realistic path to daily Shorts while heavier formats are in build.
4. **Escalation as the whole structure.** No rules explanation, no contender introductions —
   monotonic escalation from second zero toward a binary outcome.

**What we deliberately don't take:**

- Single-format identity. These are largely one-trick channels. Simulation Lobby's premise is a
  *lobby* — many formats, one visual language. `esc` is one room in it, not the building.
- Unlicensed recognizable music. Common in the genre, a claim risk on a monetized channel.
  Public-domain or original melodies only.

**Verified 2026-10-03:** see the [2D pass](#2d-pass--2026-10-03) below. In short, the format peaked
in 2024, and FuncFlow itself went dormant (Dec 2024 to Jul 2026) and came back to ~2K views.

---

## CodeCraftedPhysics — `youtube.com/@CodeCraftedPhysics`

Studied 2026-09-29, live API pull (`channel` + `videos --sort views/date --limit 10`).

**Scale:** 467K subs, 293 videos, 313M total views. Alive and current — top 10 by views span
2026-08-05 through 2026-09-28, so this is present performance, not a 2023 peak coasting on old
uploads.

**Category — distinct from `esc`, not the same genre with different dressing.** Titles are a
curiosity-gap/mystery hook over a *generative or evolving* simulation, not an escalation-to-threshold
one: *"Is that ending how the universe was made?"*, *"I think it evolved too much..."*, *"This
Simulation Contains Itself FOREVER"*, *"How many simulations are in there?"*. The hook is "what will
this system turn into," not "will it cross a threshold" — closer to procedurally-generated art /
cellular-automaton content than to the ball-escape genre. Best performers (1.08M, 846K, 651K views)
are all short (53s–1:25) with this mystery framing; the two long-form pieces (10–11 min) are the
weakest in the sample (18K, 50K views) — this channel's long-form doesn't carry the way its Shorts do.

**What this maps onto for us:** a fourth tension mechanism, alongside competitive (`race`/`wars`),
threshold-escalation (`esc`/`surv`), and choice-gate (`rom`) — call it **generative curiosity**: a
system evolves under its own rule and the hook is purely "what does it become," no win condition and
no explicit threshold. No existing format slug covers this. Cheap to prototype if `Core`'s determinism
already exists (same seeded-RNG, fixed-tick discipline), but the creative burden shifts to designing a
visually interesting *rule*, not a config knob — harder to templatize than `esc`'s single-number
tuning table.

**Not yet studied:** `satisfying.ba11s` (originator), Jack's Physics.

---

## Adjacent categories — pulled directly via API, 2026-09-29

User-supplied channels, studied to map what's popular *around* `esc` rather than inside it. All
numbers are live API pulls (channel totals + `videos --sort views/date --limit 8`), not third-party
documentation — no unverified-research caveat needed for this section.

| Channel | Subs | Total views | Category | Verdict |
|---|---|---|---|---|
| `@AlgoMarblerYT` | 110K | 50.0M | 2D (Algodoo) mass-marble chaos — "N marbles vs. object" | Alive, steady mid-2025 through Dec 2025 uploads in the 90K–700K range |
| `@Satisfying2DSims` | 18K | 19.6M | 2D physics, `esc`-style (channel bio literally describes our exact mechanic — grow/bounce/fill + collision-triggered melody) | Small but **brand-new** (created 2026-07), posting daily; views 1K–31K, much smaller scale than the others here |
| `@RenderZen` | 38.8K | 132.5M | 3D Blender — "perfect fit" shape-insertion puzzles | Alive, one video spiked to 2.1M (rest of the batch 55K–233K) — high variance around one repeated title template. **Full pass below** ([RenderZen](#renderzen--youtubecomrenderzen)) |
| `@MusicMarble3D` | 973K | 1.83B | 3D Blender — marble runs playing a recognizable nursery-rhyme melody | Huge and alive — 1M–5M+ on recent uploads, all 9–11s long |
| `@moreballs` | 195K | 103.9M | 3D Blender — long-form marble elimination races (capture-the-flag, compilations) | Alive but **long-form only** (16–43 min), 30K–270K range — a completely different production model from Shorts |
| `@Oyen_3D` | 261K | 331.3M | 3D Blender — "drop realistic fluid/physics on a Minecraft character" | Alive, one video at 6.9M, rest 15K–640K — high variance, "vs Minecraft" crossover branding is doing real work |
| `@Kawaken_3DCG` | 512K | 822.6M | 3D Blender — "X vs Y" material/texture stress tests (balloon vs. spikes, cone vs. shredder, viscosity sims) | Alive and consistently strong — every recent upload in the 250K–4.3M range, tightest spread of any channel here |

### What this maps onto for us

- **Confirms `esc` is not oversaturated** — `@Satisfying2DSims` is running the *exact* mechanic
  (growing ball, collision-triggered melody) and is brand-new (created July 2026) yet already posting
  daily. The genre is still being actively entered in 2026, not abandoned.
- **The single biggest adjacent category we don't have**: "**material/object vs. object" stress-test
  format** (`Kawaken_3DCG`, `RenderZen`, `Oyen_3D` all run variants of this — drop/press/cut one
  thing into or onto another, no escalation loop, no "will it escape," just one dramatic collision
  resolved fast). This is a different tension mechanism from anything in our format table — not
  competitive, not threshold-escalation, more like a single satisfying event. Consistently the
  best-performing shape across all three channels that run it.
- **3D Blender dominates the very top of this list by raw scale** (`MusicMarble3D` 1.83B total,
  `Kawaken_3DCG` 822M, `Oyen_3D` 331M) vs. `AlgoMarblerYT`'s 2D-only 50M — but this project is
  deliberately Unity/2D-and-3D-URP, not Blender-rendered, so treat this as evidence the *tension
  mechanism* travels across render pipelines, not as pressure to switch tooling.
- **`moreballs` is the outlier**: same "marble race" premise as our own `race` slug, but long-form
  only (16–43 min) and does fine at that length (30K–270K views) — worth knowing `race` doesn't have
  to be a Short to work, if a long-form marble race is ever on the table.
- **Survivorship bias applies to all of the above** — these are seven channels that got named to us
  as examples, i.e. already selected for success. This is not a random sample of the genre.

### MusicMarble3D: nursery-rhyme melodies, checked 2026-10-03

Pulled the last 50 uploads plus `status.madeForKids`, which is public on `videos.list part=status`.

- **Twinkle Twinkle is the channel's engine.** Its top 12 are *Baa Baa Black Sheep* (32.3M, the same
  tune) and seven Twinkle variants (15.2M, 15.1M, 12.3M, 7.0M, 6.3M, 6.1M, 5.5M), all 8-11s.
- **None of the 50 is marked made for kids** (0/50). Nursery-rhyme melodies on a general-audience
  physics channel haven't forced the kids classification there. Our upload setting stays our call,
  and it should be honest: the audience is general, not children.
- **Comments are low** (31-140 on 5-32M views), so instant recognition plus a loop drives views.
  "Guess the song" comment-bait does not. For a tune everyone knows, the hook is the payoff of
  hearing it emerge, not a quiz.

---

## RenderZen — `youtube.com/@renderzen`

Studied 2026-09-30, live API pull of **all 284 uploads** (`videos @renderzen --limit 300 --json`).
Triggered by the user sharing [DUUuNOKfCxE](https://www.youtube.com/shorts/DUUuNOKfCxE),
*"🔥0% vs 100%🔥 Soft Body Simulation in Blender 3D — Hyper Realistic!"*. It's 14s long, has 10.2M views and was published 2025-09-21.

**Scale:** 38.9K subs, 132.7M total views, channel created 2025-01-14. Views are ~3,400× subs:
reach comes from the Shorts feed, not a subscribed audience.

**Two templates, run one after the other.** The channel is almost entirely two title templates:

| Template | Videos | Ran | Median views | >1M | Best |
|---|---|---|---|---|---|
| **"0% vs 100%" soft-body sweep** | 76 | 2025-07-16 → 2025-10-22 | 110K | 6 | 10.2M (DUUuNOKfCxE) |
| **"Perfect Fit"** (shape drops into a matching hole) | 162 | 2025-07-12 → now | ~175K | 13 | 19.7M (2026-04-08) |

- **Was DUUuNOKfCxE an outlier?** Partly. It was the best sweep, and 4 of the channel's top 10 videos are
  sweeps (10.2M, 8.8M, 6.8M, 2.6M), so the template produced repeated hits, not a single fluke. But
  the typical sweep did ~110K and only ~8% passed 1M. Budget for the median, not the hit.
- **They dropped the sweep after Oct 2025** and went all-in on Perfect Fit. The sweep's monthly median
  was still *rising* when they stopped (57K → 117K → 111K → 131K). In Oct 2025, Perfect Fit's
  median was 348K. *Inferred, not observed:* Perfect Fit simply outperformed it. The sweep didn't fail.
- **Length:** every hit is 10–19s. Their two 83–89s compilations got 3K–5K views. Short and loopable
  is the whole shape.
- **Cadence:** ~1/day July–December 2025, then 2–3/week in 2026. Monthly medians held at
  115K–295K through 2026, so the channel is alive, not coasting.
- **Likes are hidden on 219 of 284 videos**, so like-rate is unusable for this channel. Don't
  compare it against others.
- **Title shape:** emoji + `0% vs 100%` + what + `in Blender 3D` + a rotating superlative ("Hyper
  Realistic!", "Most Realistic Yet!"). The titles are near-identical, a pure template channel.

**The visual grammar (from 4 frames of DUUuNOKfCxE):** one glossy, translucent object in one hue
(amber-orange) drops onto a wooden spike on a wooden plinth. The background is a dark top-to-grey gradient. One
white caption at the top ("Soft 25%", "Soft 50%", "Soft 100%"), and the same drop repeated at each
setting. Only the parameter changes between takes. Nothing needs explaining, so it reads with the sound off.

**What we take:**

1. **The parameter sweep as a structure**: one scene, one event, one knob stepped from 0% to 100%,
   caption per take. Each take is a new, fixed-length payoff, which is how a 14s video retains without
   escalation. It also fixes what killed `impact-001` (a single event over too fast). See
   [plans/videos/impact-003-soft-sweep.md](../plans/videos/impact-003-soft-sweep.md).
2. **Cheap per video.** A sweep is `scene + N configs`, which matches our `scene + config + seed` model
   exactly. Once the scene exists, the next video is a different object, knob or colour.
3. **One-hue, one-subject, dark-studio framing**, and caption-only UI.

**What we deliberately don't take:**

- **The single-template identity.** Same lobby principle as above. The sweep is a *structure* any
  of our formats can use ("0% vs 100% bounciness" works for `esc` too), not a channel.
- **Chasing Blender's offline realism.** We render in URP in real time. We compete on elegant
  materials (glass, chrome, lacquer), clean studio lighting and **sound**. RenderZen's sound is
  unverified (the API can't hear), and whether it's any good is still an open question.

**Still unverified:** audio design, exact take count and timing (only 4 thumbnail frames seen),
and retention (owner-only, invisible to us).

## 2D pass — 2026-10-03

Live API pull of **every upload** on five 2D channels (`videos --limit 500 --json`). `--limit` applies
*before* `--sort`, so an unlimited pull is needed to find a big channel's real top videos.

| Channel | Uploads | Active | Peak | 2026 state |
|---|---|---|---|---|
| `@satisfying.ba11s` | 424 | 2023-11 → **2025-05, stopped** | 78M, 38M, 36M (all song-recognition, 2023-24) | Dead. Its last 12 uploads did 60-160K |
| `@funcflow` | 55 | 2023-11 → 2024-12, then 2 in Jul 2026 | 35.9M *"Will the ball escape?"*, 13.5M, 11.0M | Came back from 19 months dormant to **1-2K views** |
| `@CodeCraftedPhysics` | 293 | continuous | 25.7M (2026-03), 23.3M, 18.8M | Alive. 2026 has 4 of its top 10 videos |
| `@satisfying2dsims` | 269 | created 2026-07-06, **~3/day** | 1.9M, 1.7M (Sep 2026) | Fastest riser: 21M views in 3 months, Sep median 44K/Short |
| `@AlgoMarblerYT` | 94 | 2021 → 2025-12 | 8.0M *Shutter Crush* (10-min Algodoo) | Long form, monthly. Not a Shorts model |

**What the 2D genre looks like now:**

1. **Alive, but the winners are whoever is posting.** Both originators stopped and collapsed, and
   FuncFlow's return after 19 months got ~2K views. Meanwhile a 3-month-old channel posting 3/day
   is the fastest riser. The same lesson as our own revival: cadence matters more than format.
2. **Big numbers and destruction lead `@satisfying2dsims`**: *"It crushed one ball into 15,939
   pieces"* (1.9M), *"Every ball crushed splits into 3"* (1.7M), *"How many balls can a hydraulic
   press handle?"* (725K). Multiply plus a crusher, with the count shown on screen.
3. **Reach-the-center is the inverse of `esc`, and it's hot**: CodeCrafted's #1 is *"When it finally
   reached the center"* (25.7M, 2026-03), and *"Can it break the whole spiral?"* did 10.7M. On
   S2DS: *"Can Pac-Man reach the core?"* (670K), *"Can the ice ball reach the molten core?"* (110K).
4. **Song recognition made the 2023-24 mega-hits**, all of them: ba11s's top 15 and FuncFlow's
   8.9M *"Do you know this song?"*. FuncFlow used **classical pieces, which are public domain**. Our
   procedural synth plays the notes, so there's no recording to license either. It's now a mid
   performer rather than a mega one (S2DS: 90-100K).
5. **Element vs element** (fire/water/lava) has S2DS's highest bucket median: **82K, n=7**, best
   842K *"It came down to ONE flame"*. Needs a particle/fluid layer that we don't have.
6. **Weaker buckets:** colour/territory takeover (median 25K) and grow/shrink-per-bounce (22K,
   which is our `esc` mechanic, and saturated at S2DS's volume).
7. **Length:** S2DS's median Short is **34s**, and every 2D hit above is 16-70s. Our `esc` videos
   run 2:05-2:12. Not proven to hurt (v4 is our best), but it's the most obvious untested gap.
8. **Title shape has shifted** from plain questions to **outcome or reaction statements with a
   number**: *"It crushed one ball into 15,939 pieces 💀"*, *"How did the 4th ball even miss it? 😭"*,
   *"It missed by one ball. It never missed again"*. Questions still work (*"Can the water put out
   the LAST flame?"*, 44K in 2 days).

**Caveats:** survivorship bias (these channels were named because they succeeded). S2DS's numbers
are partly volume, since at 3/day some Shorts will hit by chance. Bucket medians come from title
keywords, not from watching the videos.

---

## Length vs views across channels (2026-10-04)

Live pull of every upload from 10 channels (`videos --limit 600 --json`). Kept Shorts ≤3 min,
published since **2024-10-15** (when 3-min Shorts opened up), at least 7 days old. FuncFlow and
moreballs had fewer than 8 in that window and are excluded. Each cell is that bucket's count and
its median **relative to the channel's own median**, so channels of different size are comparable.

| Channel | n | Channel median | <15s | 15–30s | 30–60s | 1–2m | 2–3m |
|---|---|---|---|---|---|---|---|
| `@MusicMarble3D` | 160 | 2.97M | 157 ×1.01 | — | 3 ×0.02 | — | — |
| `@renderzen` | 284 | 125K | 170 ×1.31 | 98 ×0.73 | 7 ×0.13 | 8 ×0.06 | 1 ×0.02 |
| `@Oyen_3D` | 104 | 354K | 32 ×1.52 | 65 ×1.06 | 3 ×0.37 | 2 ×1.24 | 2 ×0.28 |
| `@Kawaken_3DCG` | 146 | 1.97M | 5 ×0.49 | 51 ×0.99 | 90 ×1.09 | — | — |
| `@satisfying2dsims` | 222 | 35K | 1 ×0.59 | 66 ×1.24 | 148 ×0.98 | 7 ×0.14 | — |
| `@satisfying.ba11s` | 68 | 63K | 2 ×2.51 | 7 ×1.14 | 49 ×1.00 | 8 ×0.79 | 2 ×0.61 |
| `@CodeCraftedPhysics` | 102 | 146K | — | 3 ×0.59 | 63 ×0.99 | 36 ×1.18 | — |
| `@AlgoMarblerYT` | 10 | 116K | — | — | 8 ×1.10 | 1 ×0.75 | 1 ×0.50 |

**Top 3 per channel (length, views):** MusicMarble3D 11s 241M / 11s 208M / 9s 168M · Oyen 16s 103M
/ 17s 44M / 12s 32M · Kawaken 22s 39M / 29s 37M / 31s 36M · CodeCrafted **63s 25.8M / 68s 18.8M /
70s 16.4M** · renderzen 14s 20M / 11s 11M / 14s 10M · satisfying2dsims 51s 2.0M / 48s 1.7M / 16s 0.9M.

**Reading:**

- **Length clusters by sub-genre.** 3D spectacle loops live under 30s; 2D story sims live at
  30–70s. Off-band videos usually underperform their own channel (renderzen's 1–2 min ×0.06 are
  compilations), so this is partly "videos that don't fit the channel's style", not length alone.
- **The one channel that tested 60s+ seriously in 2D won with it.** CodeCrafted's 1–2 min uploads
  sit at ×1.18, with 13 of 36 breaking out at ≥5× its median. In 2026, its 60s+ uploads beat its
  sub-60s ones in most months (e.g. Mar 2026: 8.7M vs 288K monthly medians). Small monthly samples,
  but the direction holds.
- **2–3 min is nearly unoccupied**: 6 videos across 8 channels, all below their channel median.
  Too few to call.

### Last 90 days: who would pass 10M

Shorts *published* in the last 90 days and the views they've earned so far. This undercounts
because older uploads keep earning too, and it's the public count, which is higher than the engaged
views YPP uses.

| Channel | Shorts | Per day | Views | Median | Median length |
|---|---|---|---|---|---|
| `@Kawaken_3DCG` | 26 | 0.29 | 119.6M | 1.37M | 36s |
| `@Oyen_3D` | 20 | 0.22 | 61.0M | 569K | 18s |
| `@satisfying2dsims` | 256 | 2.84 | 19.7M | 34K | 34s |
| `@renderzen` | 33 | 0.37 | 8.2M | 117K | 15s |
| `@CodeCraftedPhysics` | 14 | 0.16 | 7.6M | 252K | 58s |
| `@funcflow` | 2 | 0.02 | 3.6K | 1.8K | 34s |

Two ways to clear the bar: **volume** (satisfying2dsims, ~3/day at a modest median) or **quality**
(Kawaken/Oyen: Blender-grade 3D at ~2/week, 1000× the per-video reach). Nobody passes it on cadence
alone at a low median, or on 2D quality alone at a low cadence (CodeCrafted falls just short).

---

## Sources

- [Will the Ball Escape? / Ball Bouncing Brainrot — Know Your Meme](https://knowyourmeme.com/memes/will-the-ball-escape-ball-bouncing-brainrot)
- [FuncFlow — YouTube](https://www.youtube.com/@FuncFlow)
