# Channel Revival — running plan

**Status:** active · **Started:** 2026-09-27 · **Channel:** [@simulationlobby](https://youtube.com/@simulationlobby)

The standing log for reviving the channel after ~16 months dormant. Append to it; don't rewrite it.
The value is in being able to look back and see *what we actually did and what happened next* —
which is the only way small-channel decisions get better over time.

## Division of labour with other docs

Three files, three jobs. Keeping them apart is the point — two files describing the same fact is how
they drift.

| File | Holds | Don't put here |
|---|---|---|
| `PROJECTS.md` | **State tables** — build status per format, block status, published-video stats | Narrative, cadence, session history |
| `plans/channel-revival.md` (this) | **Narrative and cadence** — session log, decisions, next steps | View-count tables (link to PROJECTS.md) |
| `KnowledgeBase/retros/` | **Per-video retros** — what retained, what confused, what to change | Running state |

Numbers live in `PROJECTS.md`. This file records *why we did things and what we decided*.

---

## The goal

**Monetize: get into the YouTube Partner Program through the Shorts path.** That needs **1,000
subscribers + 10M public Shorts views in the trailing 90 days**. It's a 90-day window, not a year.

**Deadline: 2027-02-01.** From that date new applicants need **20M Shorts views / 90 days** (or
8,000 watch hours), double today's bar (announced 2026-08-10). Hitting 10M by then means the window
**2026-11-03 → 2027-02-01** has to average **~111K views/day**. The other route, 4,000 public
long-form watch hours in 12 months, doesn't count Shorts views at all.

**Where we are (2026-10-04):** ~1,000 views/day across the 5 current-era uploads, 19 subs, median
~1.2K views per Short. That's **~100× short** of the run rate. Be honest about it: qualifying before
Feb 1 needs at least one breakout Short (1M+) on top of volume, and that can't be scheduled. The
playbook is the same either way, and if we miss Feb 1 it simply targets 20M.

**What the genre says it takes** (8 channels, `KnowledgeBase/channel-strategy.md` → *Cadence*):
there are two routes. **Volume**: `@satisfying2dsims` does ~2.8 Shorts/day at a 34K median, 19.7M
in 90 days. **Quality**: `@Kawaken_3DCG` / `@Oyen_3D` post ~2/week at a 0.6–1.4M median with
Blender-grade 3D (61–120M). Channels with a low median *and* low cadence don't get there. We can't
match Blender renders soon, so our route is volume with a quality floor: 10M / 90 days is e.g.
2/day × ~55K average. The two levers are **throughput** (we're at ~0.7/day) and **per-Short reach**
(we're at ~1.2K median).

**The 10M counts engaged views, not the public counter**, which since 2025-03 includes every replay.
Our real progress is lower than the public numbers suggest; read engaged views in Studio.

**Levers, in order:**

1. **Throughput to 2–3 Shorts/day.** This is now an engineering problem: seed-scan (built
   2026-10-04) picks runs without hand-playing, `capture-shorts` must render without hand-recording,
   and variants must be config-only. Every manual minute per video is the bottleneck.
2. **Length fit by format, not "shorter".** Across 8 channels, 3D spectacle wins under 30s, but 2D
   story sims sit at 30–70s, and CodeCrafted's biggest 2D hits are 63–70s. 2–3 min is nearly
   untested genre-wide. So: 3D Shorts at 10–20s, and test a ~60–70s `esc` cut against our 2-min
   control as a one-knob change (`channel-strategy.md` → *Length*).
3. **Proven big hooks:** song recognition (v7 had our best day 1), big numbers / multiply + crush
   (S2DS's 1.9M and 1.7M), reach-the-center (CodeCrafted's 25.7M).
4. **Titles as outcome statements with a number**, not plain questions
   (`KnowledgeBase/reference-channels.md`).

Earlier aim, still true: get back to a publishing rhythm the channel can sustain.

**Why revival rather than a fresh start:** the channel has 13 subscribers, 3 videos and a 2024
creation date. That history is worth more than a clean slate — an aged channel with any watch
history at all cold-starts better than a new one.

## Operating cadence

- **Publish:** aim for a regular slot and hold it. Consistency is the variable with the most obvious
  headroom — no format insight competes with simply publishing on schedule.
- **Record day-7 and day-30 views on time.** The API only ever returns *current* totals, so a row
  filled late reads high and silently corrupts every later comparison. A missed snapshot cannot be
  recovered — this already happened to videos 1 and 2.
- **Retro after each video** into `KnowledgeBase/retros/`, then update `PROJECTS.md`.
- **Avg % viewed and drop-off must come from YouTube Studio by hand.** The API key returns public
  data only. Don't substitute like-rate as a retention proxy — see `KnowledgeBase/youtube-api-setup.md`.

## Reading the numbers at this size

The channel is tiny. **Differences at this scale are noise.** 17 views vs 9 is not a format insight,
and building a content plan on a single-digit delta would misdirect months of work. Treat every
finding as provisional until there are 5+ videos. Like-rate is more informative than raw views at
this volume, but still not conclusive.

---

## Session log

Newest at the bottom. One entry per working session, whatever it produced.

### 2026-09-27 — Infrastructure built, first video in 16 months published

**Baseline at revival** (pulled live, `yt.py channel`): **13 subscribers · 3 videos · 26 total views**.
Previous upload was 2025-05-03, so the channel had been dormant ~16 months.

Built the whole pipeline from nothing — `Assets/Scripts/` was empty at the start of the day.

- **`Core`** (`SimulationLobby.Core`, zero references) — `SeededRandom`, `ISimulation`,
  `SimulationConfig`, `SimulationRunner`, `RunResult`/`RunRecorder`, `DeterminismVerifier`.
  Self-check passes: same seed twice is byte-identical, different seeds diverge, JSON round-trips,
  runner times out at exactly `MaxTicks`.
- **`Shared`** — `EscalationRule` + `SizeEscalationTarget2D`, `BounceDetector2D`, `ConstantSpeed2D`,
  `CircularBoundary2D`.
- **`Presentation`** — `VersusScoreboardHud` (uGUI + TMP), `BounceMelodyPlayer` +
  `ProceduralToneBank`, `AmbientDriftField`.
- **`Simulations/Escape`** — `EscapeSimulation`, `EscapeConfig`, `EscapePresenter`, and
  `EscapeSceneBuilder` (the scene is generated by a menu command, so it stays regenerable).

**Format decisions made this session:**

- Single attempt → **7-round series** with a `WALL vs BALL` score. More rounds means more resolution
  moments.
- Round ends when the ball exceeds the gap by 10%, rather than waiting for growth to clamp — the old
  rule left a hopeless ball bouncing through ~20s of dead footage.
- Starting size is drawn **once per video** and held across rounds. With a score on screen the rounds
  read as a fair contest, and unequal starting sizes would make that dishonest.
- Growth retuned ×1.075 → **×1.035** (~29 bounces/round) once we established Shorts allow 3 minutes.
  Bounces are the content — each is a note and a chance at the gap — so tune for event count first.
- Bounce audio is **generated in code** (pentatonic, so no sequence can sound sour). No audio files
  means no licensing exposure at all.

**Published:** video #3, *"The Ball Grows Every Bounce. Can It Escape?"*, 2:12, 2026-09-27.
Seed and config asset **not yet recorded** — see next steps.

**Fixed along the way:** `yt.py` crashed with `UnicodeEncodeError` on Windows whenever a title
contained an emoji or zero-width space. Now forces UTF-8 on stdout/stderr.

### 2026-09-30 — `impact` re-slotted as `surv`: *Wall vs Ball*, guess the hits

The `impact` build (single launch on 09-29, then a 9-wave rebuild) no longer matched the reference
channels' `impact` shape, which is one event resolved fast. Nine escalating waves at a wall is
survival. Converted it:

- **Format `surv`**, code in `Simulations/Survival` (`WallSurvival*`), scene
  `Survival_WallVsBall_3D`, plan [surv-001](videos/surv-001-wall-vs-ball.md), brief
  `KnowledgeBase/formats/survival-wall.md`.
- **Hook is a guess:** *how many hits to destroy all 300 blocks?* The run is open-ended until the
  wall is gone. The HUD shows a live `HITS N` with no total, and a miss doesn't count.
- **The wall is never rebuilt.** Holes persist, and elimination is measured from each block's
  original slot. Reforming erased the evidence viewers use to revise a guess, and snapping scattered
  blocks back into formation read as cheating.
- **Aims at the densest standing cluster**, because the centroid of a holed wall is often a hole.
- `impact` is back to `idea`. Its brief now records why rigid scatter can't carry a single-event
  video (settles in 3-4s) and what a real build would need (fracture / soft body / fluid).
- Untested in play mode. Baseline tuning is a first guess; see the plan's tuning-dial list.

### 2026-10-03 — Stats pull: 6 videos, 5,524 views, 18 subs

Four uploads in five days (v3–v6) took the channel from 26 views to 5,524. Numbers are in
`PROJECTS.md`, and the reading is in `KnowledgeBase/learnings.md`.

- **v5 (`surv`) and v6 (`impact`) were published** (09-30, 10-01). Both formats are now `published`
  in `PROJECTS.md`.
- **Early read, all provisional:** rotating-gap `esc` (v4) is the runaway at 3,156 and still climbing.
  The `impact` soft sweep opened strong (1,185 on day 1). `surv` Wall vs Ball lags at 244.
- **Decision deferred:** no format conclusions until the day-7 snapshots (10-04 → 10-08) and Studio
  retention for v4/v5/v6. Best vs worst (v4 vs v5) is the comparison worth explaining first.
- Cadence is the thing that changed. Four videos in five days, after 16 months of nothing, is the
  biggest single effect, ahead of any format choice.

### 2026-10-04 — Stats pull: 7 videos, 19 subs; monetization goal set; seed-scan built

- **v7 (`esc` guess the song) published 10-03:** 1,524 on day 1, the best first day of the current
  era. All four older videos were flat 10-03 → 10-04 (see `KnowledgeBase/learnings.md`).
- **v3 day-7 snapshot: 892.**
- **Goal set: YouTube Partner Program via Shorts.** The bar is 10M views in 90 days (not a year), and
  it doubles on 2027-02-01. Math and levers are under *The goal* above.
- **`seed-scan-harness` built** (`Assets/Scripts/Capture/`): headless scan of any `SimulationRunner`
  scene, a scoring profile per video, ranked CSV in `Scans/`, and a reverse-order verify pass.
  Compiles; not yet run in Unity.

---

## Next steps

Ordered. Keep this list short — a long queue means nothing is actually next.

### Immediate (this week)

1. **YouTube Studio pass** (~15 min, needs the user): real-time views 10-03 → 10-04 (did the older
   Shorts really stop, or is the public counter lagging?), Avg % viewed + drop-off for v4–v7, and
   confirm v5's and v7's seeds were `1`. Then write the first retros into `KnowledgeBase/retros/`.
2. **Day-7 snapshots:** 10-06 (v4), 10-07 (v5), 10-08 (v6), 10-10 (v7). v3 done (892).
3. **First real seed scan** on `Escape_Circle_2D_Song`: **Simulation Lobby ▸ Seed Scan ▸ Add Scanner
   To Open Scene**, Play, read the console. Then the acceptance test: turn the scanner off, Play the
   #1 seed fresh, and its `[SimulationRunner]` line must match the scan's exactly.

### Next build

4. **`capture-shorts`**: deferred 2026-10-04. The user records and publishes by hand. Revisit if
   recording becomes what limits cadence. `com.unity.recorder` 5.1.7 is already installed.
5. **brk-001 ring breaker + Für Elise** ([plan](videos/brk-001-ring-breaker-fur-elise.md)), `built`:
   new format at the user's call (5 breakable spinning rings, new shatter VFX). esc-004 shelved.
6. **`rom` / multiply + count**: the genre's biggest 2D bucket right now. Needs pooling + a hard cap.

### Open questions this revival should answer

- Does `esc` still perform in 2026, or has the format cooled?
- Does the 7-round series structure retain better than a single long attempt?
- Does a ~2min Short outperform a <60s one on this channel?

None of these are answerable yet. Revisit after 5+ videos, not before.

---

## Decision log

Decisions that would be expensive to silently reverse. Add a row rather than editing history.

| Date | Decision | Why |
|---|---|---|
| 2026-09-27 | Determinism is architectural, not a feature | A good run must be re-renderable at higher quality months later |
| 2026-09-27 | Scene is generated by a menu command, not hand-assembled | Hand-built scenes can't be diffed, and collision-safety setup rots silently |
| 2026-09-27 | Bounce audio generated in code, never sampled files | Removes copyright exposure entirely; also stays deterministic |
| 2026-09-27 | `esc` runs as a 7-round series with a versus score | More resolution moments; gives the viewer a side to root for |
| 2026-09-27 | Tune for event count first, length second | Shorts allow 3 min now; the old 60s ceiling was distorting the format |
| 2026-09-30 | The 9-wave `impact` build becomes `surv` (*Wall vs Ball*); `impact` back to `idea` | A format is defined by its tension mechanism; repeated escalating hits on a survivor count is survival, not a single-event spectacle |
| 2026-09-30 | Wall survival never rebuilds the wall; run is open-ended with a guess-the-hits hook | Persistent damage keeps the guess fair and readable; the hit count only means something if nothing caps it |
| 2026-09-30 | A miss is not a hit; `surv` aim is seeded by default | In a guessing format the revealed number must be honest and reproducible |
| 2026-10-04 | Goal is YPP via Shorts (1K subs + 10M views / 90 days), aiming before the 2027-02-01 doubling; production throughput is the first bottleneck | The genre benchmark (S2DS: 21M in 3 months) is reached by ~3 Shorts/day; we're at ~0.7/day with a manual render step |
