# Projects Index — live

The dashboard: what's built, what's published, what it earned, and what to pick next.
Update on every build milestone and after every retro. Keep it honest — a wrong number here
misdirects the next month of work.

**This file holds state tables.** The running narrative — session log, decisions, next steps — lives
in [plans/channel-revival.md](plans/channel-revival.md). Don't duplicate numbers into it.

**Channel:** [@simulationlobby](https://youtube.com/@simulationlobby) · id `UCRyMyxpvggZYYIpwn-DtrlQ`
· created 2024-01-07

> **Public stats auto-pull; retention is manual.** The `youtube` skill fetches views/likes/comments
> live (`python .claude/skills/youtube/scripts/yt.py report @simulationlobby`). **Avg % viewed and
> drop-off are owner-only metrics** that need the Analytics API with OAuth — not set up yet, so fill
> those by hand from YouTube Studio. See `KnowledgeBase/youtube-api-setup.md`.

**Status vocabulary:** `idea` → `planned` (plan written) → `building` → `built` (renders correctly)
→ `published` → `retired` (format cooled, stop making these).

---

## Formats — build state

| Slug | Format | Status | Plan | Scene | Notes |
|---|---|---|---|---|---|
| `esc` | Containment / escape | `published` | [esc-001](plans/videos/esc-001-will-the-ball-escape.md), [esc-002](plans/videos/esc-002-rotating-gap.md) — both published · [esc-003](plans/videos/esc-003-guess-the-song.md) guess the song — published 2026-10-03 (v7) · [esc-004](plans/videos/esc-004-fur-elise.md) Für Elise — shelved 2026-10-04, song moved to `brk-001` | `Escape_Circle_2D`, `Escape_Circle_2D_RotatingGap`, `Escape_Circle_2D_Song` (build: **Simulation Lobby ▸ Build Scene ▸ Escape_Circle_2D_Song**) | esc-002 adds a rotating gap (`EscapeConfig.gapAngularSpeed`) + widened speed range (6-13, was 8-10) — config-only variation, no new scene-builder or component. Published with `randomizeSeedOnStart` on, so its take is not reproducible. |
| `brk` | Ring breaker | `built` | [brk-001](plans/videos/brk-001-ring-breaker-fur-elise.md) — 5 rings + *Für Elise*, awaiting playtest | `Breaker_Rings_2D_Song` (build: **Simulation Lobby ▸ Build Scene ▸ Breaker_Rings_2D_Song**) | Ball breaks out through 5 spinning segmented rings; every touch is a note, a broken segment is a real hole, and each ring explodes on escape. Pacing modelled in Python (~46s median), not yet measured in Unity. Brief: `KnowledgeBase/formats/ring-breaker.md`. |
| `race` | Marble race | `idea` | — | — | Foundational; `elim` depends on it |
| `rom` | Release or multiply | `idea` | — | — | Needs pooling + hard cap first |
| `surv` | Survival | `published` | [surv-001](plans/videos/surv-001-wall-vs-ball.md) — published 2026-09-30 (v5) | `Survival_WallVsBall_3D` (build in-editor — run **Simulation Lobby ▸ Build Scene ▸ Survival_WallVsBall_3D**) | **Wall vs Ball**, converted from the `impact` build 2026-09-30: one 300-block wall, never rebuilt between hits, hit by an escalating ball until nothing stands. Hook is a guess: *how many hits?* Open-ended (`maxHits` 25 is a safety cap only); misses don't count as hits; aims at the densest standing cluster. Keeps the whole drama layer (slow-mo, push-in + shake, riser, ambient bed, per-hit chime cascade). **3D** — determinism untested in 3D, and here the hit count *is* the content. Other `surv` variants (shrinking ring etc.) still `idea`. Brief: `KnowledgeBase/formats/survival-wall.md`. |
| `wars` | Marble wars | `idea` | — | — | Long-form anchor; most expensive |
| `elim` | Elimination bracket | `idea` | — | — | Blocked: wraps `race`/`wars` via `ISimulation` |
| `impact` | Object vs. object single event | `published` | [impact-003](plans/videos/impact-003-soft-sweep.md): **0% vs 100% soft sweep**, published 2026-10-01 (v6). impact-001/002 superseded, never published | `Impact_SoftSweep_3D` (build in-editor: run **Simulation Lobby ▸ Build Scene ▸ Impact_SoftSweep_3D**) | Amber glass capsule dropped onto a lacquer plinth, 5 takes (0-100% soft), ~19s. Physics verified headless (squash 0 to 18cm, wobble 0 to 2.3s, deterministic, ~1.5ms/step). Visuals and sound unseen until the playtest. **Spike dropped:** a soft body only slides off a point, and impaling needs a tearing block. Brief: `KnowledgeBase/formats/impact.md`. |

## Shared infrastructure — build state

Tracked separately because video plans depend on these blocks, and a video can't start until its
blocks exist.

| Block | Status | Plan block | Used by |
|---|---|---|---|
| Deterministic core (`SeededRandom`, `ISimulation`, runner) | `built` | [determinism-core](plans/blocks/determinism-core.md) | everything |
| Collision-safe physics setup | `building` | [collision-safety](plans/blocks/collision-safety.md) | `esc`, `race`, `rom` |
| Escalation rule component | `building` | [escalation-rule](plans/blocks/escalation-rule.md) | `esc`; `surv` wall variant escalates per hit in its own config instead |
| Circular arena with gap | `building` | (in `escalation-rule` scope) | `esc`, `surv` |
| Bounce audio sequencer (song mode + reveal) | `built` | [bounce-audio-sequencer](plans/blocks/bounce-audio-sequencer.md) | `esc`; ready for `race`, `surv` |
| On-screen counter / HUD | `built` | [onscreen-counter](plans/blocks/onscreen-counter.md) | `esc`, `surv` |
| Scoreboard canvas builder (shared editor) | `built` | (in `onscreen-counter` scope) | `esc`, `surv` |
| Slow-motion director | `built` | — | `surv`; ready for `wars` |
| Camera rig (push-in + impact shake) | `built` | — | `surv`; ready for any 3D format |
| Procedural riser + ambient bed | `built` | (in `bounce-audio-sequencer` scope) | `surv` |
| Soft body (XPBD, analytic colliders) | `built` | [soft-body-xpbd](plans/blocks/soft-body-xpbd.md) | `impact` |
| Parameter sweep runner + caption | `built` | [parameter-sweep](plans/blocks/parameter-sweep.md) | `impact`; any format via `ISimulation` |
| Studio look 3D (glass/chrome/lacquer kit) | `built` | [studio-look-3d](plans/blocks/studio-look-3d.md) | `impact`; retrofit `surv` |
| Material contact audio | `built` | [material-contact-audio](plans/blocks/material-contact-audio.md) | `impact` |
| Breakable segmented ring (`SegmentedRing2D`) | `built` | (in brk-001) | `brk`; ready for `surv` shield variants |
| Shatter VFX (`ShatterBurst2D`) + procedural glass break | `built` | (in brk-001) | `brk`; any 2D break |
| Seed-scan batch harness | `built` (first Unity run pending) | [seed-scan-harness](plans/blocks/seed-scan-harness.md) | `esc` (profile `SeedScan_EscapeConfig_003`); any `SimulationRunner` scene |
| Shorts capture (9:16) | `planned` | [capture-shorts](plans/blocks/capture-shorts.md) | all Shorts |
| Contender identity + leaderboard | `idea` | — | `race`, `wars`, `elim` |

---

## Published videos

One row per upload. `Views (7d)` / `(30d)` are **snapshots at that age** — record them on time.
The API only knows current totals, so a row filled late reads high and misleads later comparisons.

| # | Date | Slug | Title | Length | Seed | Config | Views (7d) | Views (30d) | Avg % | Retro |
|---|---|---|---|---|---|---|---|---|---|---|
| 7 | 2026-10-03 | esc | Do You Know This Song? 🤔 Will the Ball Escape? | 1:19 | `1`? (scene default — confirm) | `EscapeConfig_003` + `Melodies/BaaBaaBlackSheep` | _due 10-10_ | _due 11-02_ | ? | — |
| 6 | 2026-10-01 | impact | 0% vs 100% Soft🔥Glass Capsule Squeezes Through Hole | 37s | `0` | `SoftSweep/SoftSweep_001` + `SoftDrop_000..100` | _due 10-08_ | _due 10-31_ | ? | — |
| 5 | 2026-09-30 | surv | How Many Hits to Destroy 300 Blocks? 🤔 | 1:20 | `1`? (scene default — confirm) | `WallSurvivalConfig_001` | _due 10-07_ | _due 10-30_ | ? | — |
| 4 | 2026-09-29 | esc | Will It Escape? The Exit Keeps Moving | 2:05 | **none — randomized, not re-renderable** | `EscapeConfig_002` | _due 10-06_ | _due 10-29_ | ? | — |
| 3 | 2026-09-27 | esc | The Ball Grows Every Bounce. Can It Escape? | 2:12 | **TBC** | `EscapeConfig_001` | 892 | _due 10-27_ | ? | — |
| 2 | 2025-05-03 | pre | Marble Volcano 🌋 | 59s | — | — | ? | ? | ? | — |
| 1 | 2025-05-01 | pre | How many balls will fit? | 48s | — | — | ? | ? | ? | — |

> **Video 3 needs its seed recorded** — without it the run isn't re-renderable, which is the whole
> point of the deterministic core. Fill in `Seed` while it's still known.
> **Video 4 has no seed by design** — `SimulationRunner.randomizeSeedOnStart` was turned on for this
> scene at the user's explicit request, so the published take cannot be re-rendered. Don't try to
> backfill a "Seed" value for it later; there isn't one to recover.
> **Day-7 snapshots:** v3 recorded 2026-10-04 (892). Still due **10-06 (v4), 10-07 (v5), 10-08 (v6),
> 10-10 (v7)**; can't be reconstructed later if missed (videos 1 and 2 already lost theirs).
> **Video 5's and video 7's seeds are unconfirmed** — both scenes hold `seed: 1`, `randomizeSeedOnStart: 0`;
> confirm those were the published takes (esc-003's seed table was never filled in).
>
> **Current totals, 2026-10-04** (not snapshots): v7 1,524 (day 1) · v6 1,185 (day 3) · v5 240 (day 4) ·
> v4 3,149 (day 5) · v3 892 (day 7) · v2 30 · v1 15. Channel: **19 subs · 7 videos · 5,513 views** (channel
> endpoint lags; per-video sum is 7,035). All four older videos were flat 10-03 → 10-04. Breakdown and
> reading in `KnowledgeBase/learnings.md`.

*Both predate this project — no config, seed, or plan exists for them. Marked `pre`. Current totals
as of 2026-09-27: 17 and 9 views (26 total, 13 subs). Day-7/30 snapshots were never taken and can't
be recovered.*

## What's working — read before picking the next build

Updated after each retro. Three bullets max per section; if it's longer, it isn't a conclusion yet.

**Performing (provisional — one video per variant, one day-7 snapshot so far):**
- `esc` rotating gap (v4) leads at 3,149 vs static-gap v3's day-7 892. v7 (rotating gap + guess
  the song) had the best day 1 yet (1,524). v4 vs v7 at day 7 (10-06 / 10-10) is the next comparison.
- `impact` soft sweep (v6): 1,185, flat since day 1. A 37s 3D render got a first push on par with
  `esc`, so length isn't what made `esc` work. All older videos went flat on 10-04, see learnings.md.

**Underperforming:**
- `surv` Wall vs Ball (v5): 240 at day 4, the lowest of the current era by ~4x. One video, cause
  unknown (look, title, or the format itself), and it needs Studio retention before anything is retuned.
- The channel was **dormant from May 2025 to Sept 2026** (~16 months). Broken 2026-09-27 with the
  first `esc` video. Consistency remains the variable with the most obvious headroom; no format
  insight competes with simply publishing regularly.

**Next pick, and why:**
- **Goal: YouTube Partner Program via Shorts** (1K subs + 10M views / 90 days), before the bar
  doubles on 2027-02-01. We're ~100× short of the run rate. The genre benchmark gets there at
  ~3 Shorts/day (volume) or with Blender-grade 3D at ~2/week (quality, out of reach for now), so
  **throughput is the first bottleneck**. Math in [channel-revival](plans/channel-revival.md#the-goal).
- **brk-001 (ring breaker + Für Elise)**: user's pick. It combines the genre's hottest 2D shape
  (break / reach the centre) with the song hook. First video to take its seed from the scan.
  esc-004 is shelved.
- `capture-shorts` deferred: the user records by hand. v7 is already 1:20, close to the 2D genre's
  30–70s band, so the length test waits until a song result is in.
- Full queue and reasoning: [plans/channel-revival.md](plans/channel-revival.md).

---

## How to pick next

In priority order:

1. **Unblock infrastructure.** If a shared block is `planned` and two formats need it, build it first.
2. **Follow the data.** Once 5+ videos are published, pick the format with the best views-per-build-hour.
3. **Keep both tension types alive** (competitive vs threshold — see `KnowledgeBase/channel-strategy.md`).
   Don't let the index drift into one kind.
4. **Cheapest viable test** when genuinely uncertain. Three Shorts beat one long-form bet.

## Automation

**Working now** — API key in `.env`, `youtube` skill:

```
python .claude/skills/youtube/scripts/yt.py report  @simulationlobby   # rows for the table above
python .claude/skills/youtube/scripts/yt.py videos  @simulationlobby --sort views
python .claude/skills/youtube/scripts/yt.py channel @simulationlobby
```

Merge `report` output into the table — don't blind-overwrite, the slug/seed/config/retro columns
aren't in the API.

**Still manual:** `Avg %`, drop-off, traffic sources, subs-per-video. These are owner-only and need
the Analytics API with OAuth (`KnowledgeBase/youtube-api-setup.md`). Fill from YouTube Studio during
the retro — ~2 min, and Studio is open anyway.

Reference channels use the same script via the `youtube-research` skill.
