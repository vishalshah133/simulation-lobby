# brk-001 — Break All 5 Rings in 60 Seconds? (Guess the Song)

**Format:** `brk` · **Length:** ≤62s (60s clock + end beat) · **Aspect:** 9:16 @ 1080x1920 · **Status:** `built` (compiles 2026-10-04; scene not yet generated or playtested)
**Brief:** `KnowledgeBase/formats/ring-breaker.md`
**Scene:** `Assets/Scenes/Breaker_Rings_2D_Song.unity` (build: **Simulation Lobby ▸ Build Scene ▸ Breaker_Rings_2D_Song**)
**Config:** `Assets/Settings/Configs/BreakerConfig_001.asset` (created on first build) · **Melody:** `Settings/Melodies/FurElise.asset` (the full A section, 93 notes: theme, C-major middle passage, E/D♯ run-up, theme) · **Scan profile:** `Settings/Scans/SeedScan_BreakerConfig_001.asset` · **Seed:** TBD (from the scan)

## Premise

A white ball starts inside 5 nested, spinning, rainbow rings, **against a 60-second clock**. Every
touch plays the next note of a song and breaks the segment it hit. When the ball gets through a
hole, the whole ring explodes and the ball is on to the next one, a little faster. Break all 5
before the clock hits zero, or lose. The song is revealed at the end: *Für Elise*.

**Why the clock (2026-10-04):** the first build had no way to lose. Given time, the ball always
breaks free, so "Can it break all 5 rings?" was a guaranteed yes (user's catch). The first scan
confirmed it: 188/200 finished, the rest only timed out on the safety cap. The clock makes the
outcome genuinely open, and the scan can now rank close finishes.

## Why this one

- **The user's call, and the data agrees.** "Break / reach the centre" is the genre's hottest 2D
  shape right now (CodeCraftedPhysics *"Can it break the whole spiral?"* 10.7M; *"When it finally
  reached the center"* 25.7M). Song recognition is its most proven hook. This combines the two.
- **Instant hook:** the innermost ring breaks on the first touch, so something shatters within
  about a second of the video starting.
- **Built-in escalation:** inner rings break in one touch, outer ones need 3 (they crack first),
  and the ball speeds up every ring, so the song speeds up with it.
- Replaces esc-004 (Für Elise on the escape arena), which is shelved. The melody asset moved over
  unchanged.

## Blocks used

- [x] **BUILD** `SegmentedRing2D` (`Shared/Arena`): breakable ring of true arc segments
- [x] **BUILD** `ShatterBurst2D` (`Presentation/VFX`): shards + spark streaks + shockwave
- [x] **BUILD** `ProceduralToneBank.CreateShatter` (`Presentation/Audio`): procedural glass break
- [x] **BUILD** `GeneratedArt` (`Presentation/Editor`): shard / dot / ring / circle textures
- [x] **EXTEND** `BounceDetector2D.LastCollider` (`Shared`): which segment was hit
- [ ] **REUSE** determinism core, `ConstantSpeed2D`, `BounceMelodyPlayer` + `SongReveal`,
      scoreboard HUD, `AmbientDriftField`, [seed-scan-harness](../blocks/seed-scan-harness.md)
- Capture: recorded by hand by the user

## Rules (`BreakerSimulation`)

- The ball starts off-centre (never dead centre, which would bounce along one diameter forever)
  and is launched across the ring.
- A touch removes a hit point from the touched segment. At 0 its collider switches off: a real hole.
- The ball leaves a ring only through physics. On every exit the sim checks the crossing segment
  (or a neighbour) is broken; otherwise it fails the seed as **TUNNELING**.
- On exit, the rest of that ring shatters, speed steps up, and the next ring begins.
- **Win:** the last ring breaks before 60.0s → "BROKE FREE WITH 3.2s LEFT!", 1.2s hold (ball flies
  out), then the song reveal.
- **Lose:** the clock hits 0 first → ball and rings freeze, "TIME'S UP! 3 / 5 RINGS", 1.2s hold,
  then the song reveal.
- The clock is simulated time (ticks), so it's part of the seed. The HUD shows `0:07` and turns
  red in the last 10s.

## Config + tuning targets

| Knob | Value | Note |
|---|---|---|
| Rings | 5, inner radius 1.6, spacing 0.75, thickness 0.24 | Outermost edge 4.84, inside the 5.4 half-width frame |
| Segments | 14 + 1 per ring (14 → 18) | Was 16 → 28, then 16 → 20. Trimmed to offset the slower ball. One-segment hole fits the ball in every ring (validated) |
| Hit points | 2 on every ring | Crack, then break. 1 touch broke free in 20–30s; 1→3 took ~122s |
| Clock | 60s | The stakes. Tuned so wins and losses are both common |
| Spin | 18°/s, alternating directions, seeded sign | Moving holes create near misses |
| Ball | radius 0.16, speed 6.8 + 0.4/ring (top 8.4) | 15% slower than 8 + 0.5: the song sounded rushed. Ball speed *is* the song's tempo |

**Pacing, measured, then extrapolated.** The Python model said 46s; the first real scan (1→3
hits, 4.5 u/s, no clock) measured a **123s median** (2.7× the model). So the model is retired, and
tuning now comes from real per-ring numbers: ring 1 needed 7 touches, rings 2–5 needed 19/19/26/38.
Rescaled to 1-hit rings at 5 u/s, the estimate was ~57s, but the user's playtest broke free in
**20–30s**, with rings 1–2 gone in 5s. A second touch per segment makes a ring take 5–10× longer.
**Balanced (2026-10-04):** 2 touches everywhere, 16→20 segments, 8 u/s +0.5. Estimated rings
~7/8/10/15/14s = **~55s median**. Check by playing: if most takes win easily, lower `startSpeed`
by 0.5 (~6% longer); if most lose, raise it. Each 0.5 is roughly ±3s on the total.
**Then (same day):** the song sounded rushed, so the ball is 15% slower (6.8 +0.4) and each ring
has 2 fewer segments (14 → 18) to hold the ~55s median. Tempo is set by speed, so prefer changing
segments or touches (not speed) to rebalance from here.

## Look

- Dark navy background with faint drifting dust. Rings in cyan → lime → amber → orange → pink.
  The ball is white with a short white trail, so it reads against every ring.
- **Break:** the segment swells and flashes white for 0.09s, then debris (spinning, falling under
  gravity, shrinking) plus spark streaks blow outward, with a soft glass crack.
- **Each ring has its own debris shape:** ring 1 triangle shards, ring 2 glass slivers, ring 3
  curved mini ring pieces, ring 4 diamonds, ring 5 sparkle stars (80% signature, 20% mixed).
  Debris and sparks are the ring's own colour, with per-piece hue/brightness jitter and the odd
  bright glint, so it reads as coloured glass rather than white glitter.
- **Crack** (outer rings): the segment darkens per lost hit point, with a small burst.
- **Ring clear:** the explosion ripples round the ring from the exit hole over 0.35s, with a
  shockwave ring, a camera kick and a bigger glass crash.
- **HUD:** "GUESS THE SONG" / "CAN IT BREAK ALL 5 RINGS?", RINGS vs BALL score, "RING n / 5", hit
  counter, and "RING n BROKEN!" flashes.

## Test (user, in Unity)

1. **Build Scene ▸ Breaker_Rings_2D_Song.** The console prints each ring's geometry.
2. Press **Play** once on seed 1 to judge the look and sound. Then **Seed Scan ▸ Add Scanner To
   Open Scene** and Play (200 seeds). The profile ranks the **closest finishes** first: wins with
   little time left, and losses on the last ring with most of it broken. The CSV's `won` column
   says which is which. Choose either; a channel that sometimes loses is one viewers trust.
   *Seed 176 from the first scan is obsolete: the rules changed.*
3. Turn the scanner off, set the seed, Play, and check:
   - [ ] The `[SimulationRunner]` line matches the scan's line for that seed exactly
   - [ ] No `[brk] TUNNELING` anywhere in the scan or the run
   - [ ] Notes played == hits (the song never skips)
   - [ ] **Phone speaker:** the tune is recognisable, and the glass crash doesn't drown it
   - [ ] Shards read as glass at 480px wide; the ring-clear ripple is visible; the camera kick
         isn't nauseating
   - [ ] Ball never visibly passes through an intact segment
   - [ ] The ending reveals **FÜR ELISE**
4. Record. Write the seed here and in `PROJECTS.md` **before** uploading.

**Tuning dials if something's off:** the ball wins too often → lower `startSpeed` (or raise
`outerHitPoints`). It loses too often → the reverse. Too few near misses → raise `ringAngularSpeed`. Debris too busy → lower
`shardsPerBurst` on **Shatter VFX**.

## Render + publish (user)

- **Title:** *"Can It Break All 5 Rings in 60 Seconds? 🤔 Guess the Song 🎵"*, plus the usual
  tags. No answer (song *or* result) in the title or thumbnail.
- **Thumbnail:** the rings mid-shatter, ball in the middle, a big "?".
- **Pinned comment:** "Name the song before the last ring breaks 👇"
- **Description:** "sound on 🔊". Melody: Beethoven, *Für Elise* (1810), public domain.

## Retro

Compare with v7 (the best guess-the-song day 1 so far) at equal age.
**Open question: does the ring-breaker shape beat the escape arena with the same song hook?**
If it does, `brk` becomes the song-series format: each new public-domain tune is one melody asset.
