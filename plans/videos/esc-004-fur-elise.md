# esc-004 — Do You Know This Song? (Für Elise)

**Format:** `esc` · **Length:** ~1:20 (7 rounds, same as v7) · **Aspect:** 9:16 @ 1080x1920 · **Status:** `shelved` 2026-10-04: the user moved the song to a ring-breaker format, [brk-001](brk-001-ring-breaker-fur-elise.md). The scene variant and scan profile stay usable.
**Brief:** `KnowledgeBase/formats/containment-escape.md` · **Parent:** [esc-003](esc-003-guess-the-song.md). Everything not listed here is identical to it.
**Scene:** `Assets/Scenes/Escape_Circle_2D_Song_FurElise.unity` (build: **Simulation Lobby ▸ Build Scene ▸ Escape_Circle_2D_Song_FurElise**)
**Config:** `Assets/Settings/Configs/EscapeConfig_004.asset` (created on first build) · **Melody:** `Assets/Settings/Melodies/FurElise.asset` · **Scan profile:** `Assets/Settings/Scans/SeedScan_EscapeConfig_004.asset` · **Seed:** TBD (from the scan)

## Premise

v7 again, with *Für Elise* instead of *Baa Baa Black Sheep*. **The only change is the song.**

## Why this one next

- **v7 had the best first day of the current era (1,524).** One video can't separate "song hooks
  work for us" from luck. Running the same setup with a second song is the cheapest way to tell.
- **Song recognition is the genre's most proven hook** (satisfying.ba11s, FuncFlow, MusicMarble3D;
  `KnowledgeBase/reference-channels.md`). If it repeats, every public-domain tune is a new video:
  one melody asset + one builder entry, no code. That's the volume route in
  `KnowledgeBase/channel-strategy.md` → *Cadence*.
- **Length stays at v7's ~1:20 on purpose.** That's already near the 45–75s band where 2D genre
  hits sit. Changing length and song together would make the result unreadable.
- *Für Elise* is identified by **pitch alone**, and that suits a song that plays one note per bounce. Its
  E–D♯–E–D♯–E opening is recognizable by the fifth bounce. Beethoven, 1810: public domain worldwide.

## Blocks used

- [ ] **REUSE** everything esc-003 used (determinism, collision safety, escalation, bounce audio
      sequencer in song mode, scoreboard HUD)
- [ ] **REUSE** [seed-scan-harness](../blocks/seed-scan-harness.md): first video to pick its seed by scan
- Capture: recorded by hand by the user, same as v3–v7

## What was added

| Piece | Where |
|---|---|
| `FurElise.asset`: 35 notes, tonic A3 (220 Hz), range C4–E5 | `Settings/Melodies` |
| `Escape_Circle_2D_Song_FurElise` variant + `Esc004Defaults` (= esc-003 tuning) | `Simulations/Escape/Editor/EscapeSceneBuilder.cs` |
| `SeedScan_EscapeConfig_004`: ≥35 bounces, 1–6 escapes, near misses, close score | `Settings/Scans` |

The melody is the main theme as written: motif → rising answer (C–E–A–B, E–G♯–B–C) → motif →
cadence (E–C–B–A). Sixteenths are 0.5 beats and phrase-end notes are 1.5. The last A is 3 beats,
so the reveal's played-out line lands on the tonic.

## Test (user, in Unity)

1. **Build Scene ▸ Escape_Circle_2D_Song_FurElise.**
2. **Seed Scan ▸ Add Scanner To Open Scene**, then Play. The console shows the top 10 and
   `Scans/Escape_Circle_2D_Song_FurElise_EscapeConfig_004_1-500.csv`.
3. Choose a seed from the top 10 (don't just take #1 if a lower one *looks* better). Then
   **Seed Scan ▸ Turn Scanner Off In Open Scene**, set `SimulationRunner.seed`, and Play.
4. Check:
   - [ ] The `[SimulationRunner]` console line matches the scan's line for that seed exactly
   - [ ] **On a phone speaker:** the tune is recognizable within the first ~5 bounces
   - [ ] The D♯ notes sound like Für Elise rather than a wrong note (chromatic steps are new;
         Baa Baa had none)
   - [ ] Rounds 2–7 carry the tune on mid-phrase, and the end reveals **FÜR ELISE** with the Ü
         rendered (not a box)
   - [ ] No `[esc] TUNNELING` in the console
5. Write the chosen seed into this header and into `PROJECTS.md` **before** uploading.

## Render + publish (user)

- **Title:** keep v7's shape exactly so only the song differs: *"Do You Know This Song? 🤔 Will
  the Ball Escape?"* + the same tags. No answer in the title or thumbnail.
- **Pinned comment:** "Name it before the end 👇", same as v7.
- **Description:** "sound on 🔊". Melody: Beethoven, *Für Elise* (1810), public domain.
- Not made for kids.

## Retro

Compare against v7 at **equal age** (day 1, day 7), not by current totals. Also compare comments,
since the guess hook's other job is to start replies.
**Open question: does a song hook repeat on our channel, or was v7's first day luck?**
If it repeats, the next songs (*Ode to Joy*, *Jingle Bells* for December, *Twinkle* is the same
tune as Baa Baa so skip it) are one asset each. That's when to extract a `song-variant` block
rather than writing a third plan like this one.
