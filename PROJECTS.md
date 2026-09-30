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
| `esc` | Containment / escape | `published` | [esc-001](plans/videos/esc-001-will-the-ball-escape.md), [esc-002](plans/videos/esc-002-rotating-gap.md) — both published | `Escape_Circle_2D`, `Escape_Circle_2D_RotatingGap` | esc-002 adds a rotating gap (`EscapeConfig.gapAngularSpeed`) + widened speed range (6-13, was 8-10) — config-only variation, no new scene-builder or component. Published with `randomizeSeedOnStart` on, so its take is not reproducible. |
| `race` | Marble race | `idea` | — | — | Foundational; `elim` depends on it |
| `rom` | Release or multiply | `idea` | — | — | Needs pooling + hard cap first |
| `surv` | Survival | `building` | [surv-001](plans/videos/surv-001-wall-vs-ball.md) | `Survival_WallVsBall_3D` (build in-editor — run **Simulation Lobby ▸ Build Scene ▸ Survival_WallVsBall_3D**) | **Wall vs Ball**, converted from the `impact` build 2026-09-30: one 300-block wall, never rebuilt between hits, hit by an escalating ball until nothing stands. Hook is a guess: *how many hits?* Open-ended (`maxHits` 25 is a safety cap only); misses don't count as hits; aims at the densest standing cluster. Keeps the whole drama layer (slow-mo, push-in + shake, riser, ambient bed, per-hit chime cascade). **3D** — determinism untested in 3D, and here the hit count *is* the content. Other `surv` variants (shrinking ring etc.) still `idea`. Brief: `KnowledgeBase/formats/survival-wall.md`. |
| `wars` | Marble wars | `idea` | — | — | Long-form anchor; most expensive |
| `elim` | Elimination bracket | `idea` | — | — | Blocked: wraps `race`/`wars` via `ISimulation` |
| `impact` | Object vs. object single event | `building` | [impact-003](plans/videos/impact-003-soft-sweep.md): **0% vs 100% soft sweep**, built 2026-09-30, awaiting first playtest. impact-001/002 superseded, never published | `Impact_SoftSweep_3D` (build in-editor: run **Simulation Lobby ▸ Build Scene ▸ Impact_SoftSweep_3D**) | Amber glass capsule dropped onto a lacquer plinth, 5 takes (0-100% soft), ~19s. Physics verified headless (squash 0 to 18cm, wobble 0 to 2.3s, deterministic, ~1.5ms/step). Visuals and sound unseen until the playtest. **Spike dropped:** a soft body only slides off a point, and impaling needs a tearing block. Brief: `KnowledgeBase/formats/impact.md`. |

## Shared infrastructure — build state

Tracked separately because video plans depend on these blocks, and a video can't start until its
blocks exist.

| Block | Status | Plan block | Used by |
|---|---|---|---|
| Deterministic core (`SeededRandom`, `ISimulation`, runner) | `built` | [determinism-core](plans/blocks/determinism-core.md) | everything |
| Collision-safe physics setup | `building` | [collision-safety](plans/blocks/collision-safety.md) | `esc`, `race`, `rom` |
| Escalation rule component | `building` | [escalation-rule](plans/blocks/escalation-rule.md) | `esc`; `surv` wall variant escalates per hit in its own config instead |
| Circular arena with gap | `building` | (in `escalation-rule` scope) | `esc`, `surv` |
| Bounce audio sequencer | `planned` | [bounce-audio-sequencer](plans/blocks/bounce-audio-sequencer.md) | `esc`, `race`, `surv` |
| On-screen counter / HUD | `built` | [onscreen-counter](plans/blocks/onscreen-counter.md) | `esc`, `surv` |
| Scoreboard canvas builder (shared editor) | `built` | (in `onscreen-counter` scope) | `esc`, `surv` |
| Slow-motion director | `built` | — | `surv`; ready for `wars` |
| Camera rig (push-in + impact shake) | `built` | — | `surv`; ready for any 3D format |
| Procedural riser + ambient bed | `built` | (in `bounce-audio-sequencer` scope) | `surv` |
| Soft body (XPBD, analytic colliders) | `built` | [soft-body-xpbd](plans/blocks/soft-body-xpbd.md) | `impact` |
| Parameter sweep runner + caption | `built` | [parameter-sweep](plans/blocks/parameter-sweep.md) | `impact`; any format via `ISimulation` |
| Studio look 3D (glass/chrome/lacquer kit) | `built` | [studio-look-3d](plans/blocks/studio-look-3d.md) | `impact`; retrofit `surv` |
| Material contact audio | `built` | [material-contact-audio](plans/blocks/material-contact-audio.md) | `impact` |
| Seed-scan batch harness | `planned` | [seed-scan-harness](plans/blocks/seed-scan-harness.md) | all |
| Shorts capture (9:16) | `planned` | [capture-shorts](plans/blocks/capture-shorts.md) | all Shorts |
| Contender identity + leaderboard | `idea` | — | `race`, `wars`, `elim` |

---

## Published videos

One row per upload. `Views (7d)` / `(30d)` are **snapshots at that age** — record them on time.
The API only knows current totals, so a row filled late reads high and misleads later comparisons.

| # | Date | Slug | Title | Length | Seed | Config | Views (7d) | Views (30d) | Avg % | Retro |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 2026-09-29 | esc | Will It Escape? The Exit Keeps Moving | 2:05 | **none — randomized, not re-renderable** | `EscapeConfig_002` | _due 10-06_ | _due 10-29_ | ? | — |
| 3 | 2026-09-27 | esc | The Ball Grows Every Bounce. Can It Escape? | 2:12 | **TBC** | `EscapeConfig_001` | _due 10-04_ | _due 10-27_ | ? | — |
| 2 | 2025-05-03 | pre | Marble Volcano 🌋 | 59s | — | — | ? | ? | ? | — |
| 1 | 2025-05-01 | pre | How many balls will fit? | 48s | — | — | ? | ? | ? | — |

> **Video 3 needs its seed recorded** — without it the run isn't re-renderable, which is the whole
> point of the deterministic core. Fill in `Seed` while it's still known.
> **Video 4 has no seed by design** — `SimulationRunner.randomizeSeedOnStart` was turned on for this
> scene at the user's explicit request, so the published take cannot be re-rendered. Don't try to
> backfill a "Seed" value for it later; there isn't one to recover.
> **Day-7 snapshots due 2026-10-04 (video 3) and 2026-10-06 (video 4)**; can't be reconstructed later
> if missed (videos 1 and 2 already lost theirs).

*Both predate this project — no config, seed, or plan exists for them. Marked `pre`. Current totals
as of 2026-09-27: 17 and 9 views (26 total, 13 subs). Day-7/30 snapshots were never taken and can't
be recovered.*

## What's working — read before picking the next build

Updated after each retro. Three bullets max per section; if it's longer, it isn't a conclusion yet.

**Performing:**
- *Insufficient data.* 2 videos, 26 total views. At this volume view differences are noise —
  17 vs 9 is not a format insight, and treating it as one would misdirect the whole build order.

**Underperforming:**
- The channel was **dormant from May 2025 to Sept 2026** (~16 months). Broken 2026-09-27 with the
  first `esc` video. Consistency remains the variable with the most obvious headroom; no format
  insight competes with simply publishing regularly.

**Next pick, and why:**
- `seed-scan-harness` — the last block gating quality rather than existence. Manual physics stepping
  (`Physics2D.simulationMode = Script`) runs a 130s seed in milliseconds, which is both how we pick
  good runs and the only way to run the 1000-seed tunneling check collision-safety requires.
- Then `capture-shorts` (needs `com.unity.recorder`), then esc-002/003 varying one knob at a time.
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
