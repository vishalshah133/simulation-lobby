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
| `surv` | Survival | `published` | [surv-001](plans/videos/surv-001-wall-vs-ball.md) — published 2026-09-30 (v5) · [surv-002](plans/videos/surv-002-lava-run.md) **Lava Run** (long form, survival + proliferation, 10-12 min) — `published` 2026-10-17 (v19) (scene `Survival_LavaRun_3D`, menu **Simulation Lobby ▸ 3D Formats ▸ Survival (surv) ▸ Lava Run (surv-002)**) | `Survival_WallVsBall_3D` (build in-editor — run **Simulation Lobby ▸ 3D Formats ▸ Survival (surv) ▸ Wall vs Ball**) | **Wall vs Ball**, converted from the `impact` build 2026-09-30: one 300-block wall, never rebuilt between hits, hit by an escalating ball until nothing stands. Hook is a guess: *how many hits?* Open-ended (`maxHits` 25 is a safety cap only); misses don't count as hits; aims at the densest standing cluster. Keeps the whole drama layer (slow-mo, push-in + shake, riser, ambient bed, per-hit chime cascade). **3D** — determinism untested in 3D, and here the hit count *is* the content. Other `surv` variants (shrinking ring etc.) still `idea`. Brief: `KnowledgeBase/formats/survival-wall.md`. |
| `terr` | Territory: multiply or release | `published` 2026-10-12 (v15, 15:15 long-form) | [terr-001](plans/videos/terr-001-four-color-territory.md) · [terr-002](plans/videos/terr-002-country-wars.md) Country Wars (India/Australia/West Indies/South Africa, RAIN + BOOM faces, night-stadium theme) — `building` 2026-10-15 | `Territory_MultiplyRelease_3D`, `Territory_CountryWars_3D` (build: **Simulation Lobby ▸ 3D Formats ▸ Territory (terr) ▸ Multiply or Release / Country Wars (terr-002)**) | 3D, 16:9, long form (10-15 min target). Four colours on a 64x48 tile grid, one cannon and one PhysX Plinko box each: ×2 doubles the bank, R fires it, SPIN runs a seeded jackpot reel. Losing your cannon tile knocks you out. Income is independent of territory, so comebacks are built in. Reference: @marblejamboree. Brief: `KnowledgeBase/formats/territory-multiply-release.md`. |
| `mve` | Multiply vs Escape | `built` 2026-10-13 (never run in Unity) | [mve-001](plans/videos/mve-001-multiply-vs-escape.md) | `MultiplyEscape_Circle_2D` (build: **Simulation Lobby ▸ 2D Formats ▸ Multiply vs Escape (mve) ▸ Circle**) | **Experiment lane.** The rotating-gap circle, but every wall bounce clones the ball and every escape widens the exit +1.1°. FULL at 500 vs EMPTY, 5 rounds. Balance modelled in Python (~50/50 without collisions), not yet measured. Brief: `KnowledgeBase/formats/multiply-vs-escape.md`. |
| `conv` | Math convergence | `published` 2026-10-15 (v17 conv-002), 2026-10-23 (v25 conv-003) | [conv-001](plans/videos/conv-001-ellipse-focus.md) (24s, 3 rounds) · [conv-002](plans/videos/conv-002-ellipse-stretch.md) (62s, 9 rounds: stretch / squash / line / circle / 600) · [conv-003](plans/videos/conv-003-parabola-focus.md) **Parabola Focus** (61s, 6 rounds: 5 → 1000 balls into a parabola, `published` 2026-10-23, v25) | `Convergence_EllipseFocus_2D`, `Convergence_EllipseStretch_2D`, `Convergence_ParabolaFocus_2D` (build: **Simulation Lobby ▸ 2D Formats ▸ Math Convergence (conv) ▸ Ellipse Focus / Ellipse Stretch / Parabola Focus**) | **Experiment lane, a new tension shape (prediction).** 120 balls fire from a focus of an ellipse and all meet at the other. Teach (circle) → surprise (focus) → stretch → break (off-focus). Analytic billiard, no physics engine. Rounds modelled in Python. Reference: MathCa (12.2M hit, 1.2-1.9% likes). Brief: `KnowledgeBase/formats/math-convergence.md`. |
| `clack` | Pi clacks (colliding blocks count π) | `published` 2026-10-17 (v20) | [clack-001](plans/videos/clack-001-pi-blocks.md) (~65s, 7 rounds: 1 kg → 1T kg) | `PiClacks_Blocks_2D` (build: **Simulation Lobby ▸ 2D Formats ▸ Pi Clacks (clack) ▸ Pi Blocks (clack-001)**) | **Experiment lane, the second maths Short.** A big block slides into a small one against a wall; the clack count is the digits of π (3, 31, 314…) as mass goes ×100. Exact event-driven sim in doubles, checked against the formula (Python model exact to k=6). Ladder of past answers carries the twist. Brief: `KnowledgeBase/formats/pi-clacks.md`. Backlog of 9 more maths ideas: `math-convergence.md` → *Wider maths backlog*. |
| `brach` | Brachistochrone (fastest path) | `published` 2026-10-18 (v21) | [brach-001](plans/videos/brach-001-fastest-path.md) (~75s, 5 rounds) | `Brachistochrone_FastestPath_2D` (build: **Simulation Lobby ▸ 2D Formats ▸ Brachistochrone (brach) ▸ Fastest Path (brach-001)**) | **Third maths Short** (proven lane, proposed 10-18). Same start, same finish, different tracks: the cycloid wins every round, even when it dips below the finish. Analytic bead on a polyline, exact per-segment times. Brief: `KnowledgeBase/formats/brachistochrone.md`. |
| `press` | Hydraulic press (split on crush) | `published` 2026-10-20 (v23) | [press-001](plans/videos/press-001-split-press.md) (~60-75s, 6 rounds: 2 → 64 tons) | `HydraulicPress_Split_2D` (build: **Simulation Lobby ▸ 2D Formats ▸ Hydraulic Press (press) ▸ Split Press (press-001)**) | **Experiment lane.** A press lowers onto a ball; every ball holds 1 ton, a crushed ball splits into 2, and the press stops when enough balls share its weight. Harness median is ~4× per doubling, but not on every seed, so the video doesn't claim the ratio. Custom deterministic circle solver with a q-model load, not Physics2D. Reference: @satisfying2dsims' multiply + crusher hits (1.7-1.9M). Brief: `KnowledgeBase/formats/hydraulic-press.md`. |
| `royale` | Color Rumble (4 colours, one exit, one x2 arc) | `published` 2026-10-21 (v24) | [royale-001](plans/videos/royale-001-four-colour-ring.md) (~65-80s, one game, 4 x 5 marbles) | `RingRoyale_FourColour_2D` (build: **Simulation Lobby ▸ 2D Formats ▸ Color Rumble (royale) ▸ Color Rumble (royale-001)**) | **Experiment lane.** A spinning ring holds 4 colours x 5 marbles; leaving through the EXIT gap is out, a wall hit on the gold x2 arc clones the marble. From 40 s the exit widens 3 deg/s (sudden death); last colour with marbles wins. Reuses the `mve` arena; wall-hit sensor promoted to `Shared/Physics2D/WallHitSensor2D`. Brief: `KnowledgeBase/formats/ring-royale.md`. |
| `fall` | Freefall Symphony (music-synced descent) | `built` 2026-10-20 (compiles; harness 9:35, 1,435 hits; never run in Unity) | [fall-001](plans/videos/fall-001-freefall-symphony.md) (~10 min, 7 worlds / 7 public-domain songs) · `fall-002` Mountain King Short (9:16) | `Freefall_Symphony_3D`, `Freefall_MountainKing_3D` (build: **Simulation Lobby ▸ 3D Formats ▸ Freefall (fall)**) | **New genre: hypnotic, sound-on.** One marble falls down a well; pads are solved from the song so every bounce lands on the next note; lit pads leave a sculpture per song. Canon split at the midpoint, Mountain King accelerando finale. Inspired by MIKAN 3D *Abstract World* (14.5M), not copied |
| `wars` | Marble wars | `idea` | — | — | Long-form anchor; most expensive |
| `elim` | Elimination show | `published` 2026-10-19 (v22, 9:51 long-form) | [elim-001](plans/videos/elim-001-elimination-show.md) (~11-12 min, 6 heats) | `Elimination_Show_3D` (build: **Simulation Lobby ▸ 3D Formats ▸ Elimination (elim) ▸ Elimination Show (elim-001)**) | **Long-form series template** (Crazy Marble Race shape): 16 named marbles, 6 race heats on 6 tracks, last ones out, a Last Chance heat at the midpoint, one champion. Rules fixed, so later episodes are a theme + seed. Wraps `race` through the new `IHeatSimulation`; the runtime assembly never references `Race`. |
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
| Track course (promote `race` track system to `Shared`) | `built` 2026-10-16 (untested in Unity) | [track-course](plans/blocks/track-course.md) | `race`; `surv` Lava Run next, `elim` heats later |
| Shorts capture (9:16) | `planned` | [capture-shorts](plans/blocks/capture-shorts.md) | all Shorts |

---

## Published videos

One row per upload. `Views (7d)` / `(30d)` are **snapshots at that age** — record them on time.
The API only knows current totals, so a row filled late reads high and misleads later comparisons.

| # | Date | Slug | Title | Length | Seed | Config | Views (7d) | Views (30d) | Avg % | Retro |
|---|---|---|---|---|---|---|---|---|---|---|
| 25 | 2026-10-23 | conv | How Many of 1000 Balls Hit the Dot? A PARABOLA Knows 🎯 | 1:05 | random (confirm from Unity console) | conv-003 Parabola Focus | _due 10-30_ (1,321 @ ~1.1d, 1.67% likes) | _due 11-22_ | ? | — |
| 24 | 2026-10-21 | royale | 4 Colors, 1 Exit, 1 x2… Which Color SURVIVES? 🏆 | 1:35 | random (confirm from Unity console) | royale-001 Color Rumble | _due 10-28_ (1,233 @ ~3.1d, 1.14% likes) | _due 11-20_ | ? | — |
| 23 | 2026-10-20 | press | Every Crushed Ball Splits in 2... How Many Balls STOP the Press? | 1:12 | random (confirm from Unity console) | press-001 Split Press | _due 10-27_ (1,425 @ ~4.1d, 1.54% likes) | _due 11-19_ | ? | — |
| 22 | 2026-10-19 | elim | 16 Marbles, 6 Races, Only 1 Survives \| Marble Elimination | 9:51 | random (confirm from Unity console) | elim-001 Elimination Show | _due 10-26_ (229 @ ~5.1d) | _due 11-18_ | ? | — |
| 21 | 2026-10-18 | brach | Same Start, Same Finish... Which Ball Gets There FIRST? 🏁 | 1:25 | random (confirm from Unity console) | brach-001 Fastest Path | _due 10-25_ (1,242 @ ~6.1d, flat since day 2, 1.45% likes) | _due 11-17_ | ? | — |
| 20 | 2026-10-17 | clack | How Many Times Will They CLACK? Each Block 100x Heavier 🧱 | 1:14 | random (confirm from Unity console) | clack-001 (Pi Blocks) | **2,970** (2.59% likes) | _due 11-16_ | ? | — |
| 19 | 2026-10-17 | surv | Which Color Survives the Lava? 4-Team Marble Survival Race | 6:45 | confirm (Unity console) | surv-002 Lava Run | **90** | _due 11-16_ | ? | — |
| 18 | 2026-10-15 | terr | Which Country Takes the Board? India vs Australia vs South Africa vs West Indies | 20:22 | confirm (Unity console) | terr-002 Country Wars | **~205** (missed; interpolated from 198 @ 5.1d and 213 @ 9.1d) | _due 11-14_ | ? | — |
| 17 | 2026-10-15 | conv | Stretch It, Squash It... Do They STILL All Hit Blue? 🎯 | 1:15 | random (confirm from Unity console) | conv-002 (Ellipse Stretch) | **~2,950** (missed; 2,945 @ 5.1d, 2,957 @ 9.1d) | _due 11-14_ | ? | — |
| 16 | 2026-10-13 | esc | The Exit Shrinks Every Round 😳 Can the Ball Still Escape? | 2:26 | random (confirm) | esc-006 shrinking exit | **1,721** | _due 11-12_ | ? | — |
| 15 | 2026-10-12 | terr | Which Color Takes the Board? 4-Team Multiply or Release | 15:15 | confirm (Unity console) | `TerritoryConfig` (terr-001; confirm) | **~1,900** (missed; interpolated from 1,638 @ 6.1d and 2,231 @ 8.1d) | _due 11-11_ | ? | — |
| 14 | 2026-10-11 | esc | Will It Escape? The Exit Keeps Moving 🔄 7 Rounds | 1:56 | confirm | esc rotating-gap variant (which of esc-005..007? confirm) | **1,719** (flat since day 4) | _due 11-10_ | ? | — |
| 13 | 2026-10-10 | race | 12 Marbles, 1 Track: Who Wins the Grand Prix? | 2:05 | confirm (Unity console) | `RaceConfig` (race-001) | **1,500** (1,779 @ ~8d, ~380/day, still accelerating) | _due 11-09_ | ? | — |
| 12 | 2026-10-09 | cric | West Indies Beat India in the Super Over! 😱 Marble Cricket | 1:34 | confirm | `CricketConfig_001` | 161 | _due 11-08_ | ? | — |
| 11 | 2026-10-09 | cric | Who Wins Today? India vs West Indies Marble Super Over | 1:23 | random (confirm) | `CricketConfig_001` | 51 | _due 11-08_ | ? | — |
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
> **Day-7 snapshots:** v3-v8 recorded. v11, v12 recorded 10-16, v13 10-17, v14 10-18, v16 10-20, v19 + v20 10-24; v15, v17, v18 missed (interpolated). Still due **10-25 (v21), 10-26 (v22), 10-27 (v23), 10-28 (v24), 10-30 (v25)**;
> can't be reconstructed later if missed (videos 1 and 2 already lost theirs).
> **Video 5's and video 7's seeds are unconfirmed** — both scenes hold `seed: 1`, `randomizeSeedOnStart: 0`;
> confirm those were the published takes (esc-003's seed table was never filled in).
>
> **Current totals, 2026-10-24 02:45Z:** v25 **1,321** (~1.1d, `conv-003` parabola, 1.67% likes) · v24 1,233 (~3.1d, `royale-001`) · v23 1,425 (~4.1d, `press-001`) · v22 229 (~5.1d, `elim` long-form) · v21 1,242 (~6.1d, flat) · v20 2,970 (day-7 final) · v19 90 (day-7 final) · v17 2,957 · v15 **2,605** (day 12, ~95/day) · v4 3,190 (+40, a late trickle). Channel: **58 subs (+12) · 25 videos · 30,985 views** (per-video sum; endpoint 29,377). **Every Short since v21 landed in the 1.2-1.4K band, maths and experiments alike; subs converted at ~0.25%, the best rate so far; long form is 0 for 4.** See `learnings.md` → *10-24 update*.
>
> *Older, 2026-10-20 02:09Z:* v22 7 (~1.1d, `elim-001` long-form, 9:51) · v21 **1,220** (~2.1d, `brach-001`, 1.48% likes) · v20 2,879 (day 3.1, stopped fast) · v19 73 (day 3.1) · v18 198 (day 5.1) · v17 2,945 (day 5.1, slowing) · v16 1,721 (day-7 final) · v15 **2,231** (day 8.1, ~300/day) · v13 1,837 (day 10, drip stopped: +58 in 2 days). Channel: **46 subs (+4) · 22 videos · 26,200 views** (per-video sum; endpoint 25,901). **The long-form replication is 0 for 3 so far and the 16:9 drip is slowing; brach landed in the band, not above it.** See `learnings.md` → *10-20 update*.
>
> *Older, 2026-10-18 03:19Z:* v20 **2,694** (<0.75d, `clack-001`, 2.49% likes, best first day of the revival) · v19 38 (~0.7d) · v18 129 (~3.0d; v15 had 1,039 at 3.2d) · v17 **2,607** (day 3.1, still climbing) · v16 1,718 (stopped) · v15 1,638 (~390/day) · v14 1,719 (day-7 final) · v13 1,779 (day 8, ~380/day). Channel: **42 subs (+5) · 20 videos · 23,647 views** (per-video sum; endpoint lags at 19,553). **Both maths Shorts are the two best results since v4, and the 16:9 drip is speeding up.** See `learnings.md` → *10-18 update*.
>
> *Older, 2026-10-17 09:52Z:* v19 0 (just up, `surv-002` Lava Run long-form) · v18 **28** (~2.3d; v15 had 819 at this age) · v17 **2,183** (still climbing, 2.43% likes) · v16 1,713 · v15 1,354 (~235/day) · v14 1,719 · v13 **1,500** (day 7, accelerating). Channel: **37 subs (+5) · 19 videos · 19,831 views**. **The long-form replication stalled, and every country-themed upload is a low.** See `learnings.md` → *10-17 update*.
>
> *Older, 2026-10-16 01:40Z:* v18 6 (<1d, `terr-002` long-form) · v17 **1,593** (~day 1, `conv-002`, 2.64% likes, best of the era) · v16 1,539 · v15 1,039 (day 3.2, still climbing, slowing) · v14 1,720 · v13 1,043 (16:9, still climbing). Channel: **32 subs · 18 videos · 18,248 views**. **The conv experiment cleared the band on day 1.** See `learnings.md` → *10-16 update*.
>
> **Totals, 2026-10-15 03:20Z:** v17 4 (8 min, `conv-002`) · v16 1,415 (day 2, esc-006) · v15 **819** (day 2.3, long-form) · v14 1,717 · v13 838 · v12 159 · v11 50 · v10 1,082 · v9 1,042 · v8 1,744 · v7 1,656 · v6 1,192 · v5 248 · v4 3,163 · v3 899 · v2 35 · v1 19. Channel: **32 subs · 17 videos · 16,082 views**. **Long form got a push (26 → 819 in 48h), and both 16:9 uploads climb slowly instead of bursting.** Rotating-gap `esc` is 4 for 4 in the band. See `learnings.md` → *10-15 update*.
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
- **Maths Shorts are 2 for 2 above the band** *(lead, n=2, 10-18)*: `conv-002` (v17) 2,607 at day 3 and still
  climbing, `clack-001` (v20) 2,694 in under 18h. They're the two best results since v4, with the best like% of the era
  (2.3-2.5%). Different formats, same shape: a prediction with a certain, surprising maths answer.
  **10-20: now 2 of 3.** `brach-001` (v21) has 1,220 at day 2.1, in the band but not above it. conv-002 (2,945, day 5)
  and clack-001 (2,879, day 3) both stopped near ~2.9K. So maths is still the best shape we have, not a guaranteed 2x.
  **10-24: 2 of 4.** `conv-003` (v25, a *how many?* question) has 1,321 at day 1.1, behind conv-002's 1,593 at the same age.
  Day 3 (10-26) decides whether it climbs like conv-002 or stops like brach (final ~1.24K).
- **The ~1.2-1.4K band is now the floor *and* the ceiling** *(lead, 10-24)*. The last four Shorts (brach, press, royale,
  conv-003) all sit at 1,233-1,425, maths and experiments alike. Experiments are cheap insurance: none has fallen below.

**Underperforming:**
- **Country-themed uploads got almost no push:** `cric` x2 (52, 162) and `terr-002` (v18, 129 at day 3). The
  contest-shape explanation is out: `race` Grand Prix (v13, a colour contest in 16:9) looked dead at 435 and is now
  1,779 on day 8. *Lead (n=3, confounded with length and title).* Carson Jay's biggest video is a 50-country
  elimination (7.9M), so it's 2-4 nation matches that failed for us, not countries in general.
  **10-20: theme doesn't explain the long-form lows.** v19 (Lava Run, colours) has 73 at day 3, below v18 (countries,
  129). So for long form the country lead is gone; it still stands for the two `cric` Shorts.
- **Long form hasn't replicated v15** *(lead, n=4; 10-24: v22 `elim` 229 at day 5, best of the three, still ~10x behind)*: v18 198 (day 5), v19 73 (day 3), v22 `elim` 7 (day 1). v15 is the
  one long upload that got a push. The 16:9 drip is slowing too (~380 views/day, from ~970 on 10-18).
- `surv` Wall vs Ball (v5): 246, the other outlier low. Needs Studio retention before any retune.

**Next pick, and why:**
- **Proposed 10-24: aim the next experiment at a breakout, not at the band.** Every Short since v21 landed in the band,
  so the band is the floor we already have; only a new shape can beat ~3K. Next maths pick stays a *how many / will
  they all* question (Bell curve drop is next on the slate). Long form stays at the decided cadence; v15's traffic
  sources are still unread and still decide the next long build. See
  [channel-strategy → 10-24 read](KnowledgeBase/channel-strategy.md#10-15-read-long-form-may-be-the-realistic-route-proposed-a-lead-from-one-video).
- **Goal is ad revenue (2026-10-11), not just entering YPP.** Shorts are the qualification and
  sub engine; long-form is where the money is once in. Feb-1 via Shorts is out of reach at today's run
  rate (~800 views/day, ~1% of what's needed). Proposed rethink: [channel-strategy](KnowledgeBase/channel-strategy.md#rethink-2026-10-11-earning-not-just-qualifying).
- **Shorts run on a 2:1 proven/experiment mix** ([channel-strategy → Shorts mix](KnowledgeBase/channel-strategy.md#shorts-mix-proven--experiment-decided-2026-10-13)).
  Next proven: **esc-006** (shrinking exit, built, config-only), then esc-007. Next experiment: **Multiply vs
  Escape** (mve-001, built 2026-10-13, awaiting first Play). `cric` stays paused.
- **Long-form test is live:** v15 `terr` (15:15), 819 views by day 2.3 and still climbing. Judge it at day 7 (10-19)
  on Studio watch time, avg % viewed, traffic source and subs, not on API views.
- **Proposed 10-18: maths joins the proven lane, and the next experiment slot builds another maths Short.**
  conv-002 and clack-001 both cleared the band by ~1.7x. Next picks from `formats/math-convergence.md` → *Wider maths
  backlog*: **Bell curve drop** (#7, closest to the peg-board genre) and **Brachistochrone race** (#11, reuses `race`
  tracks). `esc` rotating gap stays the control. See [channel-strategy → 10-18 read](KnowledgeBase/channel-strategy.md#10-15-read-long-form-may-be-the-realistic-route-proposed-a-lead-from-one-video).
- **Second long-form upload is climbing, slowly:** v18 `terr-002` (20:22) has 129 views at day 3, against v15's 1,039.
  Judge it at day 7 (10-22). **Third is live:** v19 `surv-002` Lava Run (colours, 6:45) has 38 at ~0.7d, ahead of v18's
  first day (6) and v15's (26 at 7.5h). It splits *country theme* from *long-form reach*, so read it at day 3 (10-20).
- **Proposed 10-20: keep the two-track balance; don't make long form the priority track yet.** v18 and v19 both
  stalled, and they differ in theme, length and format, so v15's push hasn't repeated. Read v15's Studio traffic
  sources (what pushed it?) before the 10-22 call. Next Shorts: an `esc` control and a maths pick, as planned. See
  [channel-strategy → 10-20 read](KnowledgeBase/channel-strategy.md#10-15-read-long-form-may-be-the-realistic-route-proposed-a-lead-from-one-video).
- **Proposed 10-17: pause country theming** (v11, v12 and v18 are the three lows, all country-themed), so the next
  `terr` in the series uses colour teams. Also **hold the long-form-priority call** until v18's day 7. See
  [channel-strategy → 10-17 read](KnowledgeBase/channel-strategy.md#10-15-read-long-form-may-be-the-realistic-route-proposed-a-lead-from-one-video).
- **Decided 10-16 (from MIKAN, `reference-channels.md`):** long form targets **10-12 min**, runs as **named,
  numbered series** (`terr` first), and the **next long-form format is survival + proliferation** (multiply gates
  on a race course while a hazard thins the field). Backlog: `ideas-backlog.md` → *From MIKAN*.
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
