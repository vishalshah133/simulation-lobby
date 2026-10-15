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
| `esc` | Containment / escape | `published` | [esc-001](plans/videos/esc-001-will-the-ball-escape.md), [esc-002](plans/videos/esc-002-rotating-gap.md) — both published · [esc-003](plans/videos/esc-003-guess-the-song.md) guess the song — published 2026-10-03 (v7) · [esc-004](plans/videos/esc-004-fur-elise.md) Für Elise — shelved 2026-10-04, song moved to `brk-001` · [esc-005..007](plans/videos/esc-005-rotating-gap-variants.md) rotating-gap variants (v4 replica, shrinking exit, faster gap) — `built` 2026-10-11, not yet played | `Escape_Circle_2D`, `Escape_Circle_2D_RotatingGap`, `Escape_Circle_2D_Song` (build: **Simulation Lobby ▸ 2D Formats ▸ Escape (esc) ▸ Static Gap + Song**) | esc-002 adds a rotating gap (`EscapeConfig.gapAngularSpeed`) + widened speed range (6-13, was 8-10) — config-only variation, no new scene-builder or component. Published with `randomizeSeedOnStart` on, so its take is not reproducible. |
| `brk` | Ring breaker | `built` | [brk-001](plans/videos/brk-001-ring-breaker-fur-elise.md) — 5 rings + *Für Elise*, awaiting playtest | `Breaker_Rings_2D_Song` (build: **Simulation Lobby ▸ 2D Formats ▸ Ring Breaker (brk) ▸ Rings + Song**) | Ball breaks out through 5 spinning segmented rings; every touch is a note, a broken segment is a real hole, and each ring explodes on escape. Pacing modelled in Python (~46s median), not yet measured in Unity. Brief: `KnowledgeBase/formats/ring-breaker.md`. |
| `race` | Marble race | `built` (never run in Unity) | [race-001](plans/videos/race-001-grand-prix.md) | `Race_GrandPrix_3D` (build: **Simulation Lobby ▸ 3D Formats ▸ Race (race) ▸ Grand Prix**) | 3D, 16:9, long-form anchor. 12 jewel-glass marbles down a procedurally swept black-lacquer course (13 sections: straights, banked curves, a 2-loop helix, a pinch), brass trim, chrome columns, finish gantry. Roster, physics and safety nets in `RaceConfig`; track shape in `RaceTrackLayout`. Chase camera, sliding leaderboard, procedural sound. Course v2 (2026-10-09) adds data-driven obstacles: peg pyramid / plinko grid, kinematic pinwheels (seeded phase), fork divider, bottleneck, chicane, bumps. Start played and approved; obstacle build compiles, not yet played. No model imports needed. |
| `rom` | Release or multiply | `published` 2026-10-06 | [rom-001](plans/videos/rom-001-release-or-multiply.md) | `Release_Or_Multiply_2D` (build: **Simulation Lobby ▸ 2D Formats ▸ Release or Multiply (rom) ▸ Board**) | 4 lanes x 6 rows of gates (×n, +n, ÷2, ×0) over peg fields; marbles carry a weight so the count is exact while physics bodies stay capped (400). Lane order and every drop come from the seed, which is **random each Play and logged**. Published as a 40s Short (v9): 912 views at ~13h; day-1/3/7 snapshots pending. Brief: `KnowledgeBase/formats/release-or-multiply.md`. |
| `cric` | Cricket (super over) | `built` (never run in Unity) | [cric-001](plans/videos/cric-001-super-over.md) | `Cricket_SuperOver_2D` (build: **Simulation Lobby ▸ 2D Formats ▸ Cricket (cric) ▸ Super Over**) | Experiment, first sport. One ball per delivery falls through multiply / HIT / OUT gates; distance becomes runs (dot,1,2,3,4,6) and the exit position sets the wagon-wheel angle. Two `CricketTeamDef` assets are set in the inspector. Procedural crowd audio (`ProceduralCrowd`). Pacing and run mix are model-tuned only. Brief: `KnowledgeBase/formats/cricket-super-over.md`. |
| `surv` | Survival | `published` | [surv-001](plans/videos/surv-001-wall-vs-ball.md) — published 2026-09-30 (v5) | `Survival_WallVsBall_3D` (build in-editor — run **Simulation Lobby ▸ 3D Formats ▸ Survival (surv) ▸ Wall vs Ball**) | **Wall vs Ball**, converted from the `impact` build 2026-09-30: one 300-block wall, never rebuilt between hits, hit by an escalating ball until nothing stands. Hook is a guess: *how many hits?* Open-ended (`maxHits` 25 is a safety cap only); misses don't count as hits; aims at the densest standing cluster. Keeps the whole drama layer (slow-mo, push-in + shake, riser, ambient bed, per-hit chime cascade). **3D** — determinism untested in 3D, and here the hit count *is* the content. Other `surv` variants (shrinking ring etc.) still `idea`. Brief: `KnowledgeBase/formats/survival-wall.md`. |
| `terr` | Territory: multiply or release | `published` 2026-10-12 (v15, 15:15 long-form) | [terr-001](plans/videos/terr-001-four-color-territory.md) | `Territory_MultiplyRelease_3D` (build: **Simulation Lobby ▸ 3D Formats ▸ Territory (terr) ▸ Multiply or Release**) | 3D, 16:9, long form (10-15 min target). Four colours on a 64x48 tile grid, one cannon and one PhysX Plinko box each: ×2 doubles the bank, R fires it, SPIN runs a seeded jackpot reel. Losing your cannon tile knocks you out. Income is independent of territory, so comebacks are built in. Reference: @marblejamboree. Brief: `KnowledgeBase/formats/territory-multiply-release.md`. |
| `mve` | Multiply vs Escape | `built` 2026-10-13 (never run in Unity) | [mve-001](plans/videos/mve-001-multiply-vs-escape.md) | `MultiplyEscape_Circle_2D` (build: **Simulation Lobby ▸ 2D Formats ▸ Multiply vs Escape (mve) ▸ Circle**) | **Experiment lane.** The rotating-gap circle, but every wall bounce clones the ball and every escape widens the exit +1.1°. FULL at 500 vs EMPTY, 5 rounds. Balance modelled in Python (~50/50 without collisions), not yet measured. Brief: `KnowledgeBase/formats/multiply-vs-escape.md`. |
| `conv` | Math convergence | `built` 2026-10-14 (never run in Unity) | [conv-001](plans/videos/conv-001-ellipse-focus.md) (24s, 3 rounds) · [conv-002](plans/videos/conv-002-ellipse-stretch.md) (62s, 9 rounds: stretch / squash / line / circle / 600) | `Convergence_EllipseFocus_2D`, `Convergence_EllipseStretch_2D` (build: **Simulation Lobby ▸ 2D Formats ▸ Math Convergence (conv) ▸ Ellipse Focus / Ellipse Stretch**) | **Experiment lane, a new tension shape (prediction).** 120 balls fire from a focus of an ellipse and all meet at the other. Teach (circle) → surprise (focus) → stretch → break (off-focus). Analytic billiard, no physics engine. Rounds modelled in Python. Reference: MathCa (12.2M hit, 1.2-1.9% likes). Brief: `KnowledgeBase/formats/math-convergence.md`. |
| `wars` | Marble wars | `idea` | — | — | Long-form anchor; most expensive |
| `elim` | Elimination bracket | `idea` | — | — | Blocked: wraps `race`/`wars` via `ISimulation` |
| `impact` | Object vs. object single event | `published` | [impact-003](plans/videos/impact-003-soft-sweep.md): **0% vs 100% soft sweep**, published 2026-10-01 (v6). impact-001/002 superseded, never published | `Impact_SoftSweep_3D` (build in-editor: run **Simulation Lobby ▸ 3D Formats ▸ Impact (impact) ▸ Soft Sweep**) | Amber glass capsule dropped onto a lacquer plinth, 5 takes (0-100% soft), ~19s. Physics verified headless (squash 0 to 18cm, wobble 0 to 2.3s, deterministic, ~1.5ms/step). Visuals and sound unseen until the playtest. **Spike dropped:** a soft body only slides off a point, and impaling needs a tearing block. Brief: `KnowledgeBase/formats/impact.md`. |

## Shared infrastructure — build state

Tracked separately because video plans depend on these blocks, and a video can't start until its
blocks exist.

| Block | Status | Plan block | Used by |
|---|---|---|---|
| Deterministic core (`SeededRandom`, `ISimulation`, runner) | `built` | [determinism-core](plans/blocks/determinism-core.md) | everything |
| Collision-safe physics setup | `building` | [collision-safety](plans/blocks/collision-safety.md) | `esc`, `race`, `rom` |
| Escalation rule component | `building` | [escalation-rule](plans/blocks/escalation-rule.md) | `esc`; `surv` wall variant escalates per hit in its own config instead |
| Circular arena with gap | `building` | (in `escalation-rule` scope) | `esc`, `surv`, `mve` (opt-in `keepGapSegments` + `RefreshGap` for a widening exit) |
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
| Contender identity (`ContenderIdentity`) + track path (`TrackPath`) | `built` | (in race-001) | `race`; ready for `wars`, `elim` |
| Chase camera (`ChaseCameraRig`) + leaderboard HUD (`LeaderboardHud`, landscape canvas) | `built` | (in race-001) | `race`; any format with named contenders |
| Breakable segmented ring (`SegmentedRing2D`) | `built` | (in brk-001) | `brk`; ready for `surv` shield variants |
| Shatter VFX (`ShatterBurst2D`) + procedural glass break | `built` | (in brk-001) | `brk`; any 2D break |
| Seed-scan batch harness | `built`, **opt-in, not a workflow step** (seeds are random by default, see CLAUDE.md) | [seed-scan-harness](plans/blocks/seed-scan-harness.md) | `esc` (profile `SeedScan_EscapeConfig_003`); any `SimulationRunner` scene |
| Body pool (`BodyPool2D`, hard cap) | `built` | (in rom-001) | `rom`; any format that spawns many bodies |
| Trigger zone (`TriggerZone2D`) | `built` | (in rom-001) | `rom`; any checkpoint / finish line |
| World-anchored labels (`WorldLabelLayer`) + `CountFormat` | `built` | (in rom-001) | `rom`; ready for `race` names, gate values |
| Territory grid (`OwnershipGrid`, `GridBouncer`, `TileGridView`, `ShareBarHud`) | `building` | [territory-grid](plans/blocks/territory-grid.md) | `terr`; ready for `wars` colour takeovers |
| Slot-board economy (`SlotEffect`, `CountBank`, `TriggerZone3D`, `BodyPool3D`, `SlotReelHud`) | `building` | [slot-board-economy](plans/blocks/slot-board-economy.md) | `terr`; follow-up: migrate `rom` gates onto it |
| Turret sweep (`SweepAim`) + focus camera (`FocusCameraRig`) | `building` | (in terr-001) | `terr`; ready for `wars`, `surv` |
| Backdrop themes + seeded variance (`BackdropTheme`, `BackdropStyler`, `BackdropThemeLibrary`) | `built` 2026-10-13 | — | `esc`, `mve`; any 2D scene with an `AmbientDriftField` (add `BackdropThemeLibrary.AddStyler` to its builder). 5 dark themes in `Assets/Settings/Backdrops/`; theme, hue, mote count, speed and drift direction come from the run seed, so a re-render matches |
| Shorts capture (9:16) | `planned` | [capture-shorts](plans/blocks/capture-shorts.md) | all Shorts |

---

## Published videos

One row per upload. `Views (7d)` / `(30d)` are **snapshots at that age** — record them on time.
The API only knows current totals, so a row filled late reads high and misleads later comparisons.

| # | Date | Slug | Title | Length | Seed | Config | Views (7d) | Views (30d) | Avg % | Retro |
|---|---|---|---|---|---|---|---|---|---|---|
| 17 | 2026-10-15 | conv | Stretch It, Squash It... Do They STILL All Hit Blue? 🎯 | 1:15 | random (confirm from Unity console) | conv-002 (Ellipse Stretch) | _due 10-22_ (4 @ 8 min) | _due 11-14_ | ? | — |
| 16 | 2026-10-13 | esc | The Exit Shrinks Every Round 😳 Can the Ball Still Escape? | 2:26 | random (confirm) | esc-006 shrinking exit | _due 10-20_ (1,415 @ day 2) | _due 11-12_ | ? | — |
| 15 | 2026-10-12 | terr | Which Color Takes the Board? 4-Team Multiply or Release | 15:15 | confirm (Unity console) | `TerritoryConfig` (terr-001; confirm) | _due 10-19_ (819 @ day 2.3) | _due 11-11_ | ? | — |
| 14 | 2026-10-11 | esc | Will It Escape? The Exit Keeps Moving 🔄 7 Rounds | 1:56 | confirm | esc rotating-gap variant (which of esc-005..007? confirm) | _due 10-18_ (1,717 @ day 3.6) | _due 11-10_ | ? | — |
| 13 | 2026-10-10 | race | 12 Marbles, 1 Track: Who Wins the Grand Prix? | 2:05 | confirm (Unity console) | `RaceConfig` (race-001) | _due 10-17_ (838 @ day 5) | _due 11-09_ | ? | — |
| 12 | 2026-10-09 | cric | West Indies Beat India in the Super Over! 😱 Marble Cricket | 1:34 | confirm | `CricketConfig_001` | _due 10-16_ (158 @ day 1.8) | _due 11-08_ | ? | — |
| 11 | 2026-10-09 | cric | Who Wins Today? India vs West Indies Marble Super Over | 1:23 | random (confirm) | `CricketConfig_001` | _due 10-16_ (52 @ day 2.4) | _due 11-08_ | ? | — |
| 10 | 2026-10-06 | impact | Dropping a 500KG Ball on a Cube Castle 😳 | 51s | confirm | (impact scene; confirm config) | 1,082 (flat since day 3) | _due 11-05_ | ? | — |
| 9 | 2026-10-06 | rom | How Many Marbles Reach the Bottom? | 40s | random (logged in Unity console; confirm) | `RomConfig_001` | 1,041 (flat since day 3) | _due 11-05_ | ? | — |
| 8 | 2026-10-04 | brk | Guess the Song 🎵 Can It Break All 5 Rings in 60 Seconds? | 1:06 | scan #1? (confirm) | `BreakerConfig_001` + Für Elise | **1,742** (day 6.9) | _due 11-03_ | ? | — |
| 7 | 2026-10-03 | esc | Do You Know This Song? 🤔 Will the Ball Escape? | 1:19 | `1`? (scene default — confirm) | `EscapeConfig_003` + `Melodies/BaaBaaBlackSheep` | **1,649** | _due 11-02_ | ? | — |
| 6 | 2026-10-01 | impact | 0% vs 100% Soft🔥Glass Capsule Squeezes Through Hole | 37s | `0` | `SoftSweep/SoftSweep_001` + `SoftDrop_000..100` | 1,188 (read day 8; flat since day 2) | _due 10-31_ | ? | — |
| 5 | 2026-09-30 | surv | How Many Hits to Destroy 300 Blocks? 🤔 | 1:20 | `1`? (scene default — confirm) | `WallSurvivalConfig_001` | 246 (read day 9; flat since day 4) | _due 10-30_ | ? | — |
| 4 | 2026-09-29 | esc | Will It Escape? The Exit Keeps Moving | 2:05 | **none — randomized, not re-renderable** | `EscapeConfig_002` | **3,150** | _due 10-29_ | ? | — |
| 3 | 2026-09-27 | esc | The Ball Grows Every Bounce. Can It Escape? | 2:12 | **TBC** | `EscapeConfig_001` | 892 | _due 10-27_ | ? | — |
| 2 | 2025-05-03 | pre | Marble Volcano 🌋 | 59s | — | — | ? | ? | ? | — |
| 1 | 2025-05-01 | pre | How many balls will fit? | 48s | — | — | ? | ? | ? | — |

> **Video 3 needs its seed recorded** — without it the run isn't re-renderable, which is the whole
> point of the deterministic core. Fill in `Seed` while it's still known.
> **Video 4 has no seed by design** — `SimulationRunner.randomizeSeedOnStart` was turned on for this
> scene at the user's explicit request, so the published take cannot be re-rendered. Don't try to
> backfill a "Seed" value for it later; there isn't one to recover.
> **Day-7 snapshots:** v3-v8 recorded. Still due **10-16 (v11, v12), 10-17 (v13), 10-18 (v14), 10-19 (v15), 10-20 (v16), 10-22 (v17)**;
> can't be reconstructed later if missed (videos 1 and 2 already lost theirs).
> **Video 5's and video 7's seeds are unconfirmed** — both scenes hold `seed: 1`, `randomizeSeedOnStart: 0`;
> confirm those were the published takes (esc-003's seed table was never filled in).
>
> **Current totals, 2026-10-15 03:20Z:** v17 4 (8 min, `conv-002`) · v16 1,415 (day 2, esc-006) · v15 **819** (day 2.3, long-form) · v14 1,717 · v13 838 · v12 159 · v11 50 · v10 1,082 · v9 1,042 · v8 1,744 · v7 1,656 · v6 1,192 · v5 248 · v4 3,163 · v3 899 · v2 35 · v1 19. Channel: **32 subs · 17 videos · 16,082 views**. **Long form got a push (26 → 819 in 48h), and both 16:9 uploads climb slowly instead of bursting.** Rotating-gap `esc` is 4 for 4 in the band. See `learnings.md` → *10-15 update*.
>
> *Older, 2026-10-13 03:14Z:* v15 26 (7.5h, long-form) · v14 1,583 (day 1.6) · v13 435 · v12 159 · v11 52 · v10 1,082 · v9 1,041 · v8 1,743 · v7 1,656 · v6 1,189 · v5 246 · v4 3,160 · v3 899 · v2 33 · v1 18. Channel: **28 subs · 15 videos · 13,322 views**. **v14 (proven-shape `esc`) landed in the band, so the v11-v13 dip was the contest shape/topic, not the channel** — see `learnings.md` → *10-13 update*.
>
> *Older, 2026-10-11 12:31Z:* v13 32 (day 1.4) · v12 158 · v11 52 · v10 1,082 · v9 1,040 · v8 1,742 · v7 1,652 · v6 1,189 · v5 246 · v4 3,160 · v3 899 · v2 33 · v1 18. Channel: **23 subs · 13 videos · 11,303 views** (per-video sum). **The last three uploads (v11-v13) got 3-15% of the ~1K first push every Short since v6 had** — see `learnings.md` → *10-11 update*. v13 was planned as 16:9 long-form; whether it went up as a Short or a 16:9 video is unconfirmed (the API can't see aspect).
>
> *Older, 2026-10-09:* v11 2 (just up) · v10 1,087 (day 3) · v9 1,040 (day 3) · v8 1,738 (day 5) · v7 1,649 (day 6) · v6 1,188 · v5 243 · v4 3,157 · v3 897 · v2 33 · v1 18. Channel: **22 subs · 11 videos · 11,057 views**. Subs flat at 22 since 10-06 despite +2.4K views. v10 and v11 seeds unconfirmed.
>
> *Older, 2026-10-06 16:51Z:* v9 912 (~13h) · v8 1,554 · v7 1,635 · v6 1,186 · v5 241 · v4 3,150 · v3 893 · v2 30 · v1 15. Channel: **22 subs · 9 videos · 8,656 views**. v9 seed is random and only in the Unity console log; confirm it if still known.
>
> *Older note, 2026-10-04:* **Current totals, 2026-10-04** (not snapshots): v7 1,524 (day 1) · v6 1,185 (day 3) · v5 240 (day 4) ·
> v4 3,149 (day 5) · v3 892 (day 7) · v2 30 · v1 15. Channel: **19 subs · 7 videos · 5,513 views** (channel
> endpoint lags; per-video sum is 7,035). All four older videos were flat 10-03 → 10-04. Breakdown and
> reading in `KnowledgeBase/learnings.md`.

*Both predate this project — no config, seed, or plan exists for them. Marked `pre`. Current totals
as of 2026-09-27: 17 and 9 views (26 total, 13 subs). Day-7/30 snapshots were never taken and can't
be recovered.*

## What's working — read before picking the next build

Updated after each retro. Three bullets max per section; if it's longer, it isn't a conclusion yet.

**Validated across 10 reference channels (2026-10-05, `learnings.md`):** publishing Fri-Sun UTC beats
Mon-Tue by ~1.1-1.4x; very short titles (<=25 chars) lose; tags, hashtags, emoji, description length and
category show **no effect**. Subscriber conversion is ~0.1% (ours: 10 subs on ~11.3K views), so on the
Shorts route views bind; on the watch-hours route subs bind (4K hours is only ~70K views).

**Performing (provisional, day-7 reads in for v3-v8):**
- **Threshold Shorts get a reliable ~1-1.7K first push, then stop by day 3-5.** v6-v10 (`impact`, `esc`
  + song, `brk` + song, `rom`, `impact`) all landed at 1,040-1,742, so formats inside that band are noise.
- `esc` rotating gap (v4) is still the only breakout-shaped result: **3,150 at day 7**, 2x the band.
  The song hook on the same arena (v7) landed at 1,649, so the song didn't add reach; it added a longer tail.

**Underperforming:**
- **Head-to-head contests got almost no push:** `cric` x2 (52, 159, flat) and `race` Grand Prix (435, slow
  climb). v14, a threshold `esc` Short uploaded right after, landed at 1,583, so it's **not a channel-level
  dip**: the contest shape and/or topic is the problem. Studio's "viewed vs swiped" separates shape from topic.
- `surv` Wall vs Ball (v5): 246, the other outlier low. Needs Studio retention before any retune.

**Next pick, and why:**
- **Goal is ad revenue (2026-10-11), not just entering YPP.** Shorts are the qualification and
  sub engine; long-form is where the money is once in. Feb-1 via Shorts is out of reach at today's run
  rate (~800 views/day, ~1% of what's needed). Proposed rethink: [channel-strategy](KnowledgeBase/channel-strategy.md#rethink-2026-10-11-earning-not-just-qualifying).
- **Shorts run on a 2:1 proven/experiment mix** ([channel-strategy → Shorts mix](KnowledgeBase/channel-strategy.md#shorts-mix-proven--experiment-decided-2026-10-13)).
  Next proven: **esc-006** (shrinking exit, built, config-only), then esc-007. Next experiment: **Multiply vs
  Escape** (mve-001, built 2026-10-13, awaiting first Play). `cric` stays paused.
- **Long-form test is live:** v15 `terr` (15:15), 819 views by day 2.3 and still climbing. Judge it at day 7 (10-19)
  on Studio watch time, avg % viewed, traffic source and subs, not on API views.
- **esc-006 is up (v16) and in the band; conv-002 (v17) is the live experiment.** Read v17 at ~day 1.
- **Proposed 10-15: long form may be the realistic route.** The watch-hour bar before 02-01 is ~2x away on v15's
  pace, against ~100x for Shorts. Subs bind everywhere. Ship a second long upload soon, and decide at v15 day 7. See
  [channel-strategy → 10-15 read](KnowledgeBase/channel-strategy.md#10-15-read-long-form-may-be-the-realistic-route-proposed-a-lead-from-one-video).

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
