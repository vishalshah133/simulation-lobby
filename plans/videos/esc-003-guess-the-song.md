# esc-003 — Do You Know This Song?

**Format:** `esc` · **Length:** ~2 min (7 rounds) · **Aspect:** 9:16 @ 1080x1920 · **Status:** `built` (code compiles 2026-10-03; scene not yet generated or playtested)
**Brief:** `KnowledgeBase/formats/containment-escape.md`
**Scene:** `Assets/Scenes/Escape_Circle_2D_Song.unity` (build in-editor: **Simulation Lobby ▸ Build Scene ▸ Escape_Circle_2D_Song**)
**Config:** `Assets/Settings/Configs/EscapeConfig_003.asset` (created on first build) · **Melody:** `Assets/Settings/Melodies/BaaBaaBlackSheep.asset` · **Seed:** TBD (starts at `1`, see below)

## Premise

esc-002's rotating gap, but the bounces play *Baa Baa Black Sheep*, one note per bounce. The song
keeps going across 7 rounds, and the final frame reveals the title.

## Why this one next

- **Song recognition made the genre's biggest videos** (satisfying.ba11s 78M/38M, FuncFlow 8.9M), and
  the Twinkle/Baa Baa tune specifically is MusicMarble3D's engine (32.3M, 15.2M, 12.3M…). See
  `KnowledgeBase/reference-channels.md` § 2D pass and § MusicMarble3D.
- **It keeps the core of our best video (v4)**: rotating gap 20°/s, growth ×1.035. Speed is fixed
  at 9 instead of v4's 6-13, because bounce rate is the song's tempo.
- **Same 7-round length as v4**, so this video tests the song alone. The 30-45s length test stays
  in the backlog for a later video.

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md)
- [ ] **REUSE** [collision-safety](../blocks/collision-safety.md)
- [ ] **REUSE** [escalation-rule](../blocks/escalation-rule.md)
- [x] **BUILD** [bounce-audio-sequencer](../blocks/bounce-audio-sequencer.md): this video pays for it.
      `MelodySequence` asset + song mode on `BounceMelodyPlayer` + `SongReveal`, all in `Presentation`
- [ ] **REUSE** [onscreen-counter](../blocks/onscreen-counter.md)
- [ ] ~~seed-scan-harness~~: still `planned`, so pick the seed by hand (below)
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md), same manual capture as v3-v6

## What was built, and where

The split: everything a second song video (or a song in `race`/`surv`) would need went into
`Presentation`. `esc` only got a variant entry and a presenter toggle. The `esc` simulation is untouched.

| Piece | Layer | Reusable by |
|---|---|---|
| `MelodySequence` (ScriptableObject: notes, title, source, tonic) | `Presentation/Audio` | any format, any song |
| `BounceMelodyPlayer.melody` + `FinishPhrase()` + `NotesPlayed` | `Presentation/Audio` | any format |
| `SongReveal`: on `ISimulation.IsComplete` → question becomes the title, melody finishes its line | `Presentation/Audio` | any `ISimulation` + `VersusScoreboardHud` |
| `BaaBaaBlackSheep.asset` (53 notes, in A) | `Settings/Melodies` | data |
| `EscapePresenter._restartMelodyEachRound` + `SetCopy()` | `Simulations/Escape` | esc only |
| `EscapeSceneBuilder` → `Variant` record, 2 menu entries | `Simulations/Escape/Editor` | esc only |

**Next song = one new `.asset` + one `Variant` entry.** No new code. If a second format wants a song,
it adds `SongReveal` in its own builder. `Presentation` already has everything.

Not promoted: the scene-builder `Variant` pattern stays inside `esc`. Promote it if `surv` grows a
second scene the same way.

## Config + tuning targets

| Knob | Value | Note |
|---|---|---|
| Rounds | 7 | Same as v4 |
| Gap | 24°, rotating 20°/s, seeded direction | Same as v4 |
| Speed | 9 (min = max) | **Differs from v4's 6-13.** A per-round draw could double the tempo between rounds. The RNG draw still happens, so draw order is unchanged |
| Growth | ×1.035/bounce, about 29 bounces to trapped | Same as v4 |
| Song | 53 notes, loops, carries across rounds | Full tune is 6 lines, and 1 line takes about 9 bounces |
| Tonic | 220 Hz (A3) | Warm on phone speakers |
| `finishSecondsPerBeat` | 0.36 | Pace of the line played out after the end |

## Acceptance criteria

- [ ] `Escape_Circle_2D` still builds identically (the refactor must not change esc-001)
- [ ] Notes played == bounces across the full run (no `[esc] TUNNELING`)
- [ ] The tune is recognizable within the first 9 bounces, checked **on a phone speaker**
- [ ] Rounds 2-7 continue the song mid-tune rather than restarting it
- [ ] At the end the question line reads **BAA BAA BLACK SHEEP** and the current line plays out
- [ ] Title + reveal readable at 480px wide, and nothing sits behind the HUD
- [ ] Same seed twice → same outcomes and the same bounce count

## Seed selection

Published 2026-10-03 as v7, most likely with the scene default `seed: 1` (unconfirmed, see
`PROJECTS.md`). These criteria are now encoded in
`Assets/Settings/Scans/SeedScan_EscapeConfig_003.asset`; for a re-render or a sequel, scan
(**Simulation Lobby ▸ Seed Scan**) instead of picking by hand. A good seed:

1. **≥ 53 total bounces**, so the whole song plays at least once before the reveal
2. At least one escape **and** one trap, so the score isn't a shutout
3. Ideally a near miss in the last round

| Seed | Outcomes | Total bounces | Note |
|---|---|---|---|

Record the chosen seed in the header **before** uploading. v3 and v5 lost theirs.

## Render + publish

- **Title** (copy the shape): *"Do you know this song? 🤔"* plus the usual tags. Don't put the answer
  or a 🐑 in the title or thumbnail.
- **Thumbnail:** the ball by the moving gap, with "?" in large type.
- **Pinned comment:** "Name it before the end 👇", the same guessing ask v5 drew comments with.
- **Description:** "sound on 🔊". Melody: traditional *Baa Baa Black Sheep* (1761 French air), public domain.
- Mark **not made for kids** (general audience). MusicMarble3D runs the same tunes, 0/50 marked for kids.

## Retro

Write `KnowledgeBase/retros/YYYY-MM-DD-esc-guess-the-song.md`, update `/PROJECTS.md`.
Open question: **does a known melody beat the pentatonic climb on the same mechanic (vs v4)?**
Same rounds and gap as v4. The only other change is fixed speed (9), which keeps the tempo steady.
