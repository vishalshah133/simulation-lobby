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

## The Black Cat-Gear — `youtube.com/@TheBlackCatGear` (pulled 2026-10-09)

3D/2D marble-race channel: **198K subs, 124 uploads, 109.9M views**, created 2019-03. Algodoo for 2D,
Blender for 3D and editing; states "no AI". Full upload list pulled via the API (`--limit 200`).

**It is a long-form channel, and the only one in our set that is.** 107 of 124 uploads are over 3 min
(85.7M views); the median long-form upload has 414K lifetime views. Caveat on the Short/long split:
the tool tags anything under 3 min as "S", but the 17 "Shorts" dated before 2024-10 predate 3-minute
Shorts, so they were ordinary 2-3 min videos, not vertical Shorts. **The channel has no real Shorts
history**, so it says nothing about Shorts and should not be compared to our Shorts numbers.

| Year | Uploads | Views earned by that year's uploads |
|---|---|---|
| 2019 | 32 | 16.1M |
| 2020 | 28 | 7.6M |
| 2021 | 14 | 38.0M (one 12.8M hit) |
| 2022 | 8 | 12.6M |
| 2023 | 13 | 6.2M |
| 2024 | 18 | 24.7M (18.4M *Squid Game Marble Race*, 2024-12) |
| 2025 | 7 | 3.4M |
| 2026 | 4 | 1.3M (to date) |

**What it shows:**

1. **Hit-driven, with a fat middle.** The top 3 videos are 37% of all views, but the median video
   still holds 414K. Typical uploads are 100-700K lifetime; a hit is 1-18M, roughly one in 10-15.
2. **Cadence fell from ~2.5/month (2019-20) to ~1/month (2025-26), and 2025-26 uploads are weaker.**
   Median 2025-26 long-form is ~378K and 2026-04's *Catapults #2* did 106K. The newest, *Black Holes*
   (2026-10-02), has 56K in 7 days (~9K/day), so the recent trend is down.
3. **Evergreen is real here.** 2021-22 races still sit at 1-3.6M, years on. Long-form accumulates
   where our Shorts go flat after 2-5 days.
4. **Series IP is the repeatable engine.** Titles are one template: *"The [Theme] Marble Race"*, with
   sequels (*Squid Game 2* did 1.28M, *Catapults #2* did 106K) and **"(HD)" re-renders of its own older
   races** (*Dispenser*, *Building Site*, *Catapults*, *Holes*, *Balance*). Pop-culture themes
   (Squid Game, Super Mario) are the breakouts. The HD re-render habit is exactly what our
   deterministic seed + config design makes cheap.
5. **Likes are 0.3-0.6% of views**, similar to the Shorts channels. Subs/views is **0.18%** lifetime
   (198K / 109.9M), about 2x our ~0.1%, but it's one channel with a 7-year audience.
6. **Production cost is high.** 8-16 min Blender/Algodoo races, ~1/month. This is a one-person
   craft channel, which is also why its output has slowed.

**What we take:** the sequel / "(HD)" re-render pattern, the pop-culture-theme title template, and
the fact that long-form compounds. **What we don't:** its production model (Blender offline, monthly)
and its Shorts-agnostic growth, which came from the 2019-2022 platform era. **Unverified:** retention
and watch time (owner-only), and whether the 2025-26 decline is the channel or the platform.

---

## Sources

- [Will the Ball Escape? / Ball Bouncing Brainrot — Know Your Meme](https://knowyourmeme.com/memes/will-the-ball-escape-ball-bouncing-brainrot)
- [FuncFlow — YouTube](https://www.youtube.com/@FuncFlow)
- [Carson Jay Marbles — YouTube](https://www.youtube.com/@carsonjay)
- [Crazy Marble Race — YouTube](https://www.youtube.com/@crazymarblerace)

## @marblejamboree: Multiply or Release long form (data pulled 2026-10-12)

The direct reference for `terr`. **A small channel:** 5,600 subs, 96 videos, 2.76M lifetime views, created 2023-03.
Its long-form *Multiply or Release* series (S2 "with Spinner", S3 "[Sq]", a [Hex] beta) is its core, and active uploads
end around 2024-09.

| Measure | Value |
|---|---|
| MoR long-form views | 8.8K–27.6K each; most 11–17K |
| Best | *[BETA] [Hex] Multiply or Release - Marble Race in Unity*, 27.6K |
| Length | **35–63 min** typical (one 11:42 special) |
| Like-rate | 0.57–0.76% (its Shorts run 1.0–2.0%) |
| Titles | Plain series labels: "Multiply or Release S3 #5 [Sq] - Marble Race in Unity". No hook, no question, no emoji |

**Reading it honestly:**
- This is a niche with a steady, returning audience (numbered seasons holding 9–18K each), not a breakout genre.
- It's one channel, so weak evidence on its own (see [[multi-channel-evidence]]).
- Next to it: `moreballs` long-form marble elimination does 30K–270K, and territory-takeover *Shorts* median about 25K at S2DS.
- The 35–63 min runtimes matter for the watch-hours route: one 13K-view video at that length banks far more hours than a Short.
- Its titles and thumbnails are unoptimised, which is the gap we can compete on.
- Not verified: search-wide genre volume. Our `yt.py` has no search command.

---

## @MathCarlooo ("MathCa") and @BouncyPhysics: two brand-new Shorts channels (data pulled 2026-10-14)

Both channels were **created in the last week of September 2026**, so every number below is about 2 weeks of
history. Both post 2-3 Shorts a day. Small samples, very young channels: read this as early signal, not proof.

### MathCa: math and physics "inevitability" Shorts

| Measure | Value |
|---|---|
| Created | 2026-09-29 |
| Subs / uploads / views | **12,100 / 35 / 13.0M** |
| Best | *Will they all hit the same point?* (2026-10-05), **12.2M** in 9 days, 1.55% likes |
| Next | *Will this pattern ever close?* 902K; *Can two mirrors catch them all?* 474K; then 4 at 100-180K |
| Median (top 30) | 32.8K. Median of the last 15 uploads: **69.8K**, so the channel is still rising |
| Length | 21-46s, almost all **31-34s** |
| Like-rate | **1.0-2.0%**, mostly 1.2-1.9%. That's 2-4x what the bouncing-ball and marble channels get |
| Titles | One template: **"Will they all ___?"** / "Can ___ ever ___?" / "Why do ___?". Short, a question every time, an emoji now and then, hashtags `#maths #physics #geometry` |

Topics (from titles; we can't watch through the API, so the mechanics are *inferred*): many glowing balls
released together with a guess about whether they **converge, sync up or close the loop**. Likely the
tautochrone/cycloid ("arrive at once", "reach the bottom together"), ellipse foci and mirrors ("two mirrors
catch them all", "still meet if I stretch it"), pendulum waves ("line up again"), the golden angle (sunflower 137.5°,
"will this pattern ever close"), plus explainers (Klein bottle, 4D cube, Möbius strip, Collatz, Galton board,
the colliding-blocks π count).

**What it shows:**
1. **The "will they all…?" convergence shapes win.** The six best videos are all *many bodies, one prediction,
   and a satisfying certain answer*. The pure explainers (*What is a Klein bottle?* 11.6K, *4D cube* 13.5K,
   *Ising model* 5.2K) sit at the bottom. So the hook is the payoff of seeing a theorem happen, not the maths lesson.
2. **Its earliest uploads used our own titles and did badly.** *"Splitting vs exponential. Who breaks out
   first?"* (10.3K) and *"Accelerating vs exponential"* (8.0K) are `mve`/`esc`-shaped. It switched to the
   convergence shape on day 3 and the views followed. That's one channel, and the hit probably drove much of it.
3. **One hit is 94% of its views.** Without the 12.2M video it's a 2-week-old channel with a ~70K recent
   median. That's still well above the ~1-30K of the 2D bouncing channels in the same window.
4. **Its like-rate is the standout.** It holds 1.2-1.9% across the catalogue. Like-rate barely depends on age,
   so it's the cleanest quality signal here. The audience rates these higher than ours.

### Bouncy Physics: our own genre, launched the same week

| Measure | Value |
|---|---|
| Created | 2026-09-30 |
| Subs / uploads / views | **128 / 29 / 124K** |
| Best | *Ball SHRINKS, wall GROWS. Can you guess the SHAPE?* 25.5K (and a 18.2K first version) |
| Next | *Every try starts FASTER. Can the ball reach the center?* 16.0K; *1 SEXTILLION in 70 bounces* 14.3K; *Every bounce = 1 new ball. When will the ring BREAK?* 8.9K |
| Median | 3.0K (all), 4.6K (last 15) |
| Length | Started at 1:04-1:13 (Oct 2-5, ~1.3K each). Moved to **22-44s** (Oct 6 on) and views went up. The confound: the channel was also 4 days older |
| Like-rate | 0.4-2.2%, mostly ~1.0-1.4% |
| Long form | One 6:58 compilation (*I Shattered 16,714 Pieces of Glass in 7 Simulations*): **7 views** |
| Titles | Statement in CAPS plus a question plus an emoji: "Every X = Y. When will Z?", "Can one ball ___ a [shape]?" |

Practically a mirror of our 2D lane: grow/shrink, reach the centre, sextillion counter, clone-per-bounce ring
break, glass that heals, shatter-a-shape (star, heart, egg), jelly merge-fill, melon slicing, string weaving,
a colour drain. **Nearly every one is already in our backlog or built**, which is a check on our
idea list more than a source of new ones. It also shows how crowded the lane is: a near-identical channel launched
the same week, and its 3K median sits at the low end of the genre band.

**What we take from Bouncy Physics:**
- **The "guess the shape" reveal** (its #1 and #2): the sim carves or draws a hidden picture and the viewer
  guesses it. *Hidden picture* (string weaving), *constellation appears* and *what flower is it* are the same
  move. This is new to us, see below.
- **Themed targets for `brk`**: "Can one ball break a heart / star / egg?" is `brk` with the outer shape
  swapped for a recognisable icon. That's a config-level change, not a new format.
- A second data point that a **compilation long-form on a young Shorts channel gets nothing** (7 views). It's the
  same lesson as `two-track-strategy`: long form needs its own audience and isn't a re-upload of Shorts.

### Does it find new genres? Yes, two (one strong, one weak)

1. **Math inevitability / convergence** (MathCa). *New, and the strongest signal in this pull.* Many identical
   bodies start from different points, and a mathematical fact says they'll meet, sync or close up. The title asks
   whether they will. The tension isn't competition (`race`), escalation (`esc`) or open-ended emergence
   (`evolve`, CodeCrafted). It's **a prediction with a known and surprising answer**. Our engine fits it well:
   deterministic, no seed variance needed, 30s, high-contrast glowing bodies on dark ground. It's the
   like-rate leader of every channel we've pulled.
2. **Hidden-shape reveal** (Bouncy Physics). *New as a hook, but small evidence (25K best, 128 subs).* An
   escalation loop (shrink/grow, weaving, packing) whose payoff is a picture the viewer has to guess. It
   merges `esc` mechanics with the `evolve` curiosity gap, plus a "guess" call to action that invites comments.

**What we don't take:** MathCa's explainer topics (Klein bottle, 4D cube). They're its weakest videos, and they're
an education channel's job, not a lobby's. **Unverified:** retention (owner-only). Whether the 12.2M hit was the
topic or the luck of the algorithm: one hit on a 2-week-old channel can't tell those apart. And the specific
mechanics behind each title, which come from title wording and haven't been watched.

---

## MIKAN (@mikan2d) and MIKAN 3D (@mikan3d): the genre's long-form giants (data pulled 2026-10-16)

One creator, two channels. **Count them as one data point for genre claims** (see [[multi-channel-evidence]]): same
author, same audience, same title style. Both build marble races, MIKAN 3D in **Unity** (our engine), and MIKAN in
Algodoo early on, then Unity.

| | @mikan2d ("MIKAN") | @mikan3d ("MIKAN 3D") |
|---|---|---|
| Subs / lifetime views | **3.05M / 2.95B** | 291K / 134M |
| Uploads | 451, created 2020-04 | 22, created 2021-03 |
| Shape | **All long form, 16:9**. 17 Shorts ever, none since 2023 | Mostly 4-12 min long form, a few 1-3 min 16:9 roller-coaster pieces |
| Cadence now | ~1 a month in 2026 (6 uploads, May-Sep), after a 6-month gap (Nov 2025 → May 2026) | Dormant: 1 upload since 2023 (2024-10, 5.6M) |
| Median views | 2024: 11.1M · 2025: 5.8M · **2026: 4.1M** (6 uploads, still accruing) | 3.5M (all-time) |
| Like-rate | **0.04-0.3%**, the lowest of any channel we've pulled | 0.13-0.7% |

**2026 uploads are still pulling ~45-65K views/day each, months after release** (*The Doubling* 46K/day at day 133,
*Elimination Soft Marble Race* 63K/day at day 122, *Core Destruction - Crane Game* 48K/day at day 41). That's the
16:9 long tail at scale, the same slow-climb shape our v13/v15 show at ~200/day.

**Top videos (MIKAN, all-time):** *Roller Coaster - Survival Worm Race* 115M (8:45) · *The Humanoid Factory* 99M
(8:26) · *The Nightmare - Survival Bicycle Race* 88M (9:32) · *The Baby Factory* 84M (9:41, 2025) · *Escape from the
Lava #4* 52M · *The Worm Factory #2* 49M (10:25, 2024). **MIKAN 3D:** *Escape from Me - Proliferation Survival
Marble Race* 28M (5:11) · *Escape from Me 3* 21M · *Abstract World - Roller Coaster* 14.5M (2:01).

**What their series are** (title keyword counts across 451 uploads: *survival* 81, *elimination*/*team* 53,
*escape* 33, *core destruction* 24, *proliferation* 23, *castle destruction* 20, *factory* 13):

1. **Survival race**: marbles run a course while a hazard (lava, tsunami, a rolling object, a crusher) takes them out.
   Often **"proliferation"**: marbles multiply as they go, so a survival race is also a multiply race.
2. **Team elimination**: colour teams, rounds, knockouts, 14-45 min, numbered series (#10, Mega #2, Decisive First #3).
3. **Core destruction**: each team defends a core and attacks the others'. Structurally the same as our `terr`
   (lose your cannon tile, you're out).
4. **Factory**: a physics assembly line that "builds" a thing (worm, baby, humanoid, brain). The biggest hits
   (84-115M), and the least like a race. It's a curiosity/spectacle arc, not a contest.

**Titles:** a concept noun phrase plus a format label (*"The Doubling - Team Elimination Marble Race"*,
*"Escape from the Lava #4 - Survival Bicycle Race"*). No questions (2 in 451), no emoji, no CAPS. A recurring
series name, numbered.

**Length (MIKAN long form since 2024, n=27, older uploads have had longer to accrue):** 8-12 min median ~21M (10
videos) · 12-20 min ~5.9M · 20-40 min ~7.7M · 40+ min ~2.4M. **Every 20M+ hit since 2024 is 5:41-10:51 (6 of 7 at 10-11 min).**
Era-confounded, but the direction matches the top-25: almost all of them are 3:30-12 min.

**Reading it honestly:**
- **Long-form marble physics is not saturated in 2026.** A channel posting monthly still averages millions per
  upload. That's the strongest evidence yet for Track B, but it's a 3M-sub channel with years of browse history.
  It shows the audience exists, not that a new channel can reach it quickly.
- **The ultra-low like% is an audience signal, not a quality one.** It fits a very young, browse-fed, often
  logged-out audience (autoplay, TV, kids). That's the audience 16:9 marble long form reaches. Expect our
  long-form like% to stay below our Shorts' (v15 0.58%, v13 0.29% already show it).
- **Most likely to be what suggests our long form.** *Which Color Takes the Board?* sits squarely in MIKAN's
  team/core-destruction lane. Check it against Studio's *Suggested videos* list (see `learnings.md` → *10-16 update*).
- **Unverified:** retention and traffic sources (owner-only), and whether 2026's lower median is decay or just
  younger uploads.

**What we take:**
- **10-12 min as the default long-form length**, not 15-20. Our `terr` (15:15) and `terr-002` (20:22) are both
  above the 10-11 min where 6 of MIKAN's 7 20M+ hits since 2024 sit. Test a 10-12 min cut of the next long video.
- **Survival + proliferation as a long-form arc.** Marbles multiply while a hazard thins them: our `rom`/`mve`
  economy on a `race` course, scaled to 10 min. It's the one MIKAN series we have every piece of except the
  course hazard.
- **Named, numbered series.** One concept name per series and a number (*Core Destruction #24*). That's what
  turns one-off uploads into something viewers and the algorithm return to. `terr` is the obvious first series.
- **Core destruction confirms the `terr` shape.** Teams, a core to defend, knock-out on loss. Ours is already that.

**What we don't take:**
- **The title style, wholesale.** Their label titles work on a 3M-sub channel that browse already trusts. Ours
  needs the click from strangers, so keep the stake/question form (*Which Color Takes the Board?*) and borrow only
  the series label as a suffix.
- **Factory videos, for now.** They're the biggest hits, but they're an authored animation arc (many hand-built
  stations), not a seedable contest. Expensive per video and against compose-don't-rewrite. Revisit once the
  long-form track proves itself.
- **Multi-hour runs** (161-176 min Stage Lottery / 200-Elimination). They're its weakest-per-view uploads.

## @carsonjay and @crazymarblerace: Algodoo 2D marble long form (data pulled 2026-10-18)

Two separate creators, so **two data points**, unlike MIKAN. Both started in **Algodoo**, a 2D physics sandbox: flat
side-view tracks, simple shapes and colours, little lighting. That's the look our 2D builds already have, so
these are the closest references for 2D long form.

| | @carsonjay ("Carson Jay Marbles") | @crazymarblerace ("Crazy Marble Race") |
|---|---|---|
| Subs / lifetime views | 157K / 59.4M | **256K / 111.7M** |
| Uploads | 302, created 2017-08 | 215, created 2018-08 |
| Shape | All long form, 16:9. 35 Shorts, none since 2024-05 (median 9K) | All long form, 16:9. 7 Shorts, none since 2025-02 (median 72K) |
| Length | 25 min median since 2025 (8-55 min) | 15 min median since 2025 (5-38 min) |
| Cadence now | **Dormant**: last upload 2025-10-23. Before that, a burst of 21 uploads in 5 weeks (May-June 2025) | **Every 2 weeks, like clockwork**, 18 uploads in 2026 |
| Median views by year | 2022 254K · 2023 64K · 2024 12K · **2025 195K** (the burst) | 2020 556K · 2023 353K · 2024 166K · 2025 101K · **2026 58K** |
| Like-rate | 0.3-0.75% | **Hidden** (API returns 0 likes). Can't be compared |

**Top videos.** Carson Jay: *50 Countries Elimination Marble Race* **7.9M** (2022, 50 min) · *ALL 67 Italian
Brainrot Characters Marble Race* 5.8M (2025, 40 min) · *Multiply or Release #2* 3.4M (2021, 13 min) · *Escape the
Seeker - Hide and Seek Marble Race* 2.9M (2020, 9 min) · *Zombie Defense - Survive 100 Rounds* 2.0M (2022) ·
*Escape The Virus - Infection Marble Race* 2.0M (2020, 5.5 min). Crazy Marble Race: *24 Marble Survival : Angry Ball*
**7.0M** (2023, 12.5 min) · *24 Marble Race EP. 18: Disaster Race* 6.1M (2020) · *EP. 1: Lucky Race* 5.8M (2019) ·
*24 Races Elimination* 4.0M (2021, 25 min) · *Marble Survival : Happy Ball* 2.6M (2024).

**What their series are** (title keywords across both channels):
- **Crazy Marble Race's spine is one numbered, themed show:** *24 Marble Race EP. 78: Tiny Race*. It always has 24
  marbles and 5 stages, and 4 marbles go out per stage. A new theme every episode (Western, Zoo, Chess, Vegetable).
  That's MIKAN's "named, numbered series", run every two weeks for seven years. *Marble Survival* (26 titles, median
  123K) is its best series per view: marbles escape a hazard ball that chases them.
- **Carson Jay rides themes and trends:** countries (4 titles, median 408K), *Multiply or Release* (24 titles,
  median 207K), zombie defense (17, median 208K), and the *Italian Brainrot* meme (21 titles in 2025, median 308K,
  the 5.8M hit). Territory wars (11 titles, median 59K) is its weakest series.

**Reading it honestly:**
- **Crazy Marble Race moved from Algodoo to Unity in February 2026** (EP. 71 onward is titled *(by Unity)*).
  Unity episodes median ~78K (n=8) against ~101K for 2025 Algodoo episodes (n=13). The Unity ones are younger and
  the channel was already declining ~40%/year, so **the switch cost no visible audience**. It's evidence that a
  2D-look marble audience accepts a Unity build, which is what we make.
- **2D Algodoo long form is decaying.** Crazy Marble Race's median has halved roughly every 1.5 years since 2020
  despite a perfect cadence. Carson Jay's 2024 fell to 12K before the meme burst. MIKAN's 2026 median (4.1M) is
  ~70x Crazy Marble Race's (58K). The genre's long-form money is in MIKAN-style 3D spectacle, not plain 2D.
- **Country theming works for Carson Jay.** *50 Countries Elimination* is its biggest video, and *200 Countries*
  got 446K. That cuts against our 10-17 country-theme lead (v11, v12 and v18 got no push). The difference may be
  scale and audience (a 2022 channel with browse history, a 50-country elimination rather than a 2- or 4-nation
  match), so our lead stays a lead, but it's no longer "country theming doesn't work". It's "country theming hasn't
  worked *for us*, at 2-4 teams".
- **Trend-riding pays, briefly.** The *Italian Brainrot* burst took Carson Jay from a 12K median to 195K, then the
  channel stopped. Unverified: why it went dormant.
- **Both channels dropped Shorts.** Neither has posted one since early 2025. One possible reason is that their
  audience is browse-fed 16:9, like MIKAN's. That's inferred from titles and cadence; we can't see their traffic.

**What we take:**
- **A fixed-rules numbered show with a new theme each episode** (24 marbles, 5 stages, 4 out per stage). The rules
  never change, so each episode costs a theme, not a build. Our `race` course system plus `elim` heats is the
  same structure. This is the clearest template for a cheap long-form series.
- **Marble Survival: a hazard ball chases the field.** 7.0M and 2.6M hits, and the channel's best series per view.
  It's a `surv` variant that reuses the Lava Run idea, with a moving hunter instead of rising lava.
- **Multiply or Release in long form** (Carson Jay #2, 3.4M) is more support for `terr`/`rom` at 10-15 min.
- **Infection / hide-and-seek** (2.0M, 2.9M): one marble converts or hunts the others. That's our backlog's `wars`
  *1 red vs 100 blue, conversion on touch*, now with reference support.

**What we don't take:**
- **Algodoo itself, or its flat look.** The 2D-look audience is shrinking, and our 3D studio look is closer to
  where the money is (MIKAN).
- **Meme-character trend-chasing** (Italian Brainrot). It's a short-lived spike, character likeness is someone
  else's IP, and the channel went dormant straight after.
- **25-55 min runs.** Carson Jay's longest uploads aren't its best per view, and MIKAN's 10-12 min finding holds.

## @ball_simulations ("Ball Simulations"): Blender marble-machine loops (data pulled 2026-10-24)

A small, young channel, but the closest reference we have for the **closed-loop marble machine** look. The
user dropped two of its videos into `Recordings/Inspiration/` (*20000 Colorful Balls*, 4:21, and *16,800 Colorful
Balls Marble Run Loop animation V07*). Neither matches a public upload exactly: the channel's six public videos
are V01-V06, so the V07 file and the 4:21 cut are probably newer, unlisted or re-cut. Unverified.

| | @ball_simulations |
|---|---|
| Subs / lifetime views | 296 / 155K |
| Uploads | 6 since 2026-05-10 (channel created 2025-11), **all 16:9 long form, 3:27-6:08** |
| Cadence | ~1 a month, every upload a numbered version of the same machine idea (V01 → V06) |
| Views | V02 85.8K (best, ~540/day) · V01 34.6K · V03 16.3K · V04 8.5K · V05 7.6K · V06 2.2K (6 days old). Median 16.3K |
| Like-rate | 0.17-0.32%, the same browse-fed, low-like band as MIKAN |
| Tool | **Blender** (stated in the channel description) |

**Titles:** `<N> Colorful Balls Marble Run Loop animation V0x | Ball Simulations`. A big number plus a label, no
question, no stake. The number (17,500-21,500) is the hook.

### What the machine is (from frame analysis of the 4:21 file)

- **One closed loop, ~15 stations, one continuous camera flight.** Cut detection found no hard cuts. The camera
  glides from station to station, spends 6-12 s at each one, and opens and closes on a wide shot of the whole loop.
- **Stations:** a clear hopper releasing onto an asphalt chute, a glass S-curve tank, a paddle scoop, a gear-linkage
  rocker rake, a **stepped comb lifter** (alternating slats walk balls up in single-file lanes), a wide slatted belt
  elevator, pusher trays, a terrazzo staircase, a **6-arm carousel of glass cups**, a **rod-grate step lifter**, mesh
  sluices, a rotating drum, a tilt tray, a **ladder shelf elevator**, and finally a giant glass reservoir that refills
  the hopper.
- **Look:** a grey seamless cyclorama, soft area light and contact shadows. The machine is in **neutral, textured
  materials** (wood, asphalt, terrazzo, brick, chrome rods, glass, cast gears) and the balls are the only saturated
  colour (~7 glossy hues). Neutral machine plus saturated payload is the readability trick.
- **Sound:** dense ball clatter over a quiet music bed, mixed loud (mean −8.6 dB, peaks at 0 dB). The clatter swells
  on every batch drop.
- **"20,000" is mostly stored balls.** Most of them sit in the reservoir and hopper. Only hundreds to low thousands
  are moving at any one station.

**Reading it honestly:**
- **One small channel, so it's a lead, not evidence.** The per-version views decline (V02 86K → V05 7.6K) on a
  channel posting the same idea again and again. That looks like novelty decay, but six uploads can't confirm it.
- **It's proof the no-contest machine loop gets browse views** at all on a sub-300 channel (V02 86K). That's the
  same lane as MIKAN's *Factory* videos (84-115M), at a far smaller scale.
- **It breaks our story rules on purpose** (no question, no counter, no one to root for). Any version we build
  needs a light spine added (see `ideas-backlog.md`), or it's a pure ambient piece that our strategy doesn't
  currently fund.

**What we take:**
- **The look kit:** a grey cyclorama, neutral textured machinery and saturated glossy balls. It's cheap in URP and
  matches our 3D baseline (floor, fill light, depth cue).
- **Lift mechanisms as reusable `Shared` arena pieces** (stepped comb, rod-grate step lifter, cup carousel, ladder
  elevator). Each one is a kinematic animation pushing rigidbodies, so they're reusable as return legs in `race`,
  `terr` or `royale` reservoirs.
- **A one-shot camera flight** with a wide open and a wide close, so the loop reads as a loop.

**What we don't take:**
- **Its station list or its layout.** Our machine is original. Only the style is borrowed.
- **The bare number title.** We keep a stake or question form.
- **20K live rigidbodies in real time.** In Unity that's an offline render at a fixed timestep, with most balls asleep.
