# core-001 — Can the Ball Break In to the Core?

**Format:** `core` · **Length:** 60-120s (100s clock + end beat; target median ~75s) · **Aspect:** 9:16 @ 1080x1920 · **Status:** `built` 2026-10-30 (code written, not yet compiled or played in Unity)
**Brief:** `KnowledgeBase/formats/reach-the-core.md` (not written yet)
**Scene:** `Assets/Scenes/ReachCore_Rings_2D.unity` (build: **Simulation Lobby ▸ 2D Formats ▸ Reach the Core (core) ▸ Reach the Core (core-001)**, priority 205) · **Config:** `Assets/Settings/Configs/CoreConfig_001.asset`
**Seed (published take, Movie_033):** `-352170834` (win, 8.3s left, 92.89s sim). Final file `Recordings/Can_The_Ball_Break_In_To_The_Core_Guess_The_Song_Physics_Simulation.mp4` (1:41 with the 5s outro), thumbnail `Recordings/thumbnails/core-001-reach-the-core.jpg`.
**Lane:** experiment (2:1 mix, [channel-strategy → Shorts mix](../../KnowledgeBase/channel-strategy.md#shorts-mix-proven--experiment-decided-2026-10-13)). Paired proven pick: esc-006.

## Premise

A ball starts **outside** 5 nested, spinning rings with a glowing core in the middle, **against a 70-second clock**.
Every touch breaks a segment (inner rings take more hits) and plays the next note of a song. Get through a hole and
the ring behind you shatters, the arena closes in, and the ball is a little faster. Reach the core before the clock
hits zero, or lose. Brief #2 of the *10-13 experiment slate*: `brk` inverted.

**What this tests (one thing):** direction. Same proven threshold shape and the same song hook as `brk-001`, but
the ball travels *inward* toward a visible prize. Reference: CodeCraftedPhysics *"When it finally reached the center"*
25.7M, satisfying2dsims *"Can Pac-Man reach the core?"* 670K.

## Storyboard

| # | Beat | On screen | Caption / counter |
|---|---|---|---|
| 1 | Hook (0-2s) | Glowing core in the middle, 5 rings, ball in the outer corridor. Ring 1 breaks on the first touch | Top: `CAN IT BREAK IN TO THE CORE?` / `GUESS THE SONG`. Clock `1:10` |
| 2 | Tension (2-50s) | Each ring clear: shatter ripple, arena wall steps in, ball speeds up | `RINGS LEFT 4 → 3 → 2`; clock turns red under 10s |
| 3 | Near-miss | Spinning holes slide past the ball; a cracked inner segment (3 hits) | Crack darkens per hit |
| 4 | Payoff (win) | Ball touches the core, core flares, white-out shatter | `REACHED THE CORE WITH 4.1s LEFT!` |
| 4b | Payoff (lose) | Clock hits 0, ball and rings freeze, core still dark | `TIME'S UP! 3 RINGS STILL STANDING` |
| End | Reveal | Song name card | `THE SONG WAS ODE TO JOY` (public domain) |

Cold-viewer check (`learnings.md` → *Story legibility*), read with sound off:
- [ ] The question names things visible from frame one: ball, rings, core
- [ ] Every cause is on screen: the touched segment swells, cracks, then breaks; the escape shatters the ring
- [ ] One question per video; rings only repeat it
- [ ] The only number shown is the answer: `RINGS LEFT`, the clock
- [ ] A loss is possible and shown (the clock), so the question isn't a guaranteed yes (the `brk` lesson)

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md), [capture-shorts](../blocks/capture-shorts.md), [seed-scan-harness](../blocks/seed-scan-harness.md) (opt-in)
- [ ] **REUSE** `SegmentedRing2D`, `BounceDetector2D.LastCollider`, `ConstantSpeed2D`, `ShatterBurst2D`, `ProceduralToneBank.CreateShatter`, `BounceMelodyPlayer` + `SongReveal`, `GeneratedArt`, caption HUD, `AmbientDriftField`
- [ ] **BUILD** `Simulations/ReachCore`: `CoreConfig`, `CoreSimulation`, `CorePresenter`, scene builder, own `.asmdef` (and Editor asmdef). References only Core/Shared/Presentation, never `Breaker`.
- [ ] **BUILD** a *containment wall* (a circle edge collider that steps in on each ring clear) and a *core* trigger with glow.

**Promotion flag:** `CoreSimulation` will duplicate `BreakerSimulation`'s hit-point bookkeeping, the clock and the decide/finish logic. Per CLAUDE.md that's the signal to promote, not copy. Plan: build `core` first against the real need, then lift the shared "segment HP + clock" piece into `Shared` in a follow-up and point both formats at it. Do not make `Breaker` reference `ReachCore` or vice versa.

## Rules (`CoreSimulation`)

- Rings are indexed **outermost first**; `CurrentRing` 0 is the outer ring. The ball starts in the corridor between the containment wall and ring 1, at a seeded angle, launched across (never along a radius).
- A touch removes a hit point. At 0 the collider switches off: a real hole. Ball only enters through an opening that exists; same TUNNELING assertion as `brk` on every crossing (inward this time).
- On crossing a ring's inner edge, the rest of that ring shatters, the containment wall steps in to the next ring's outer edge + corridor, speed steps up.
- **Win:** the ball enters the core trigger before the clock ends. **Lose:** clock hits 0 (ticks, so it's part of the seed): ball and rings freeze, 1.2s hold.
- All randomness through the run's `SeededRandom`; draw order fixed (spin sign, start angle, launch offset).

## Config + tuning targets

| Knob | Start at | Note |
|---|---|---|
| Rings | 5, outer ring inner radius 4.2, spacing 0.9, thickness 0.22 | Outer edge 4.42 inside the 5.4 half-width frame |
| Core radius | 0.7 | Visible prize, glows brighter as rings fall |
| Corridor | 0.7 | Gap between ring and containment wall. Too tight and the ball just rolls along the ring |
| Segments | 20 outer → 12 inner | One-segment hole must fit the ball in every ring (`Validate`) |
| Hit points | 1 outer → 4 inner | Instant hook; the core is armoured, so it escalates |
| Spin | 18°/s, alternating, seeded sign | Moving holes make near misses |
| Ball | r 0.16, speed 6.8 + 0.4/ring | Speed is the song's tempo; prefer changing hits/segments to rebalance |
| Clock | 100s | Portrait Short must land 60-120s. Tune so both wins and losses are common AND most wins finish past ~60s (a 25s win is too short; raise hit points, not the clock) |

**Pacing is unknown.** `brk` taught that the model was 2.7x optimistic. Tune from real Plays, not from a spreadsheet.
The inward geometry is not the same problem: segment count falls as the ball goes in, so rings get *faster* to hit but
tougher per segment. Expect the median to differ from `brk`'s ~55s.

## Look

- Navy background with drifting dust. Core: warm gold with a soft pulse. Rings cyan outside → pink inside, so the colour walks toward the core. Ball white with a short trail.
- Containment wall: a thin bright circle that visibly contracts on each ring clear. This is the "arena closing in" cause the viewer must see.
- Song: **Ode to Joy** (`Settings/Melodies/OdeToJoy.asset`), a different tune from `brk-001`'s *Für Elise*.

## Acceptance criteria

- [ ] All block acceptance criteria met
- [ ] Same seed twice → identical outcome; no `TUNNELING` in a scan
- [ ] Notes played == hits (the song never skips)
- [ ] Readable at 480px wide; HUD clear of platform UI
- [ ] Portrait 9:16 @ 1080x1920. Total length 60-120s on every seed tried (decision time + 1.2s hold + song reveal); wins and losses both occur across a handful of seeds
- [ ] Deleting `Simulations/ReachCore` leaves everything else compiling

## Seed selection

Random on start, seed logged. No hunting. Record the logged seed here before publishing.

## Render + publish

Thumbnail: glowing core, rings mid-shatter, ball between, a big `?`. No answer in the title. Pinned comment: "Name the song before it hits the core 👇". Melody: Beethoven, *Ode to Joy* (1824), public domain. Copy-paste metadata per CLAUDE.md after a take is kept.

## Retro

Write `KnowledgeBase/retros/YYYY-MM-DD-core-001-reach-the-core.md`, update `PROJECTS.md`.
**Open question:** does the inward direction beat `brk-001`'s outward one with the same song hook? Compare at equal age.
