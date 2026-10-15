# cric-001 — Super Over: India vs Australia

**Format:** `cric` · **Length:** ~100s (first guess) · **Aspect:** 9:16 · **Status:** `built`, never run in Unity
**Brief:** `KnowledgeBase/formats/cricket-super-over.md`

## Premise

Two cricket teams bat one over each on a multiply-or-release board, and the wagon wheel shows every
shot. Who wins the super over?

## Blocks used

- [x] **REUSE** [determinism-core](../blocks/determinism-core.md)
- [x] **REUSE** `CaptionHud` (onscreen-counter), `WorldLabelLayer`, `TriggerZone2D`, `ShatterBurst2D`
- [x] **BUILD** `RadialHitChart` (Presentation/VFX), `ProceduralCrowd` (Presentation/Audio)
- [ ] **SKIP** seed-scan-harness: seeds are random by default (CLAUDE.md)
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md) (still `planned`; the user records by hand)

## Video-specific work

`CricketConfig`, `CricketTeamDef`, `CricketMatch`, `CricketSimulation`, `CricketPresenter`,
`CricketSceneBuilder`. Build: **Simulation Lobby ▸ 2D Formats ▸ Cricket (cric) ▸ Super Over**. Teams: swap
`teamA` / `teamB` on `CricketConfig_001` (no rebuild).

### Config + tuning targets

| Knob | Start at | Note |
|---|---|---|
| `oversPerInnings` / `wicketsPerInnings` | 1 / 2 | A super over. More overs = longer video |
| `baseDistance` + `bands` | 6 m; 8/14/20/30/60 m | Python model: ~34% dot, 19% single, 13% four, 8% six, 10% wicket |
| gate columns | see config | Only the last column has OUT; a column of multipliers can swing a ball |
| `resultHoldSeconds` | 2 | Trim first if the video runs long |
| `flowAcceleration` | 4 | The strip's pull toward the gates; lower = slower delivery |

## Acceptance criteria

- [ ] Scene builds from the menu with no errors; Play runs both innings to a held result
- [ ] Ground fully visible, score text clear of the ground, strip clear of both (9:16 Game view, 1080x1920)
- [ ] Console prints the seed at start; pasting it back (randomize off) reproduces every ball
- [ ] Run mix over ~10 matches is roughly cricket-shaped (wickets 8-15%, sixes 5-12%); else retune bands/gates
- [ ] Gate labels, score and the wagon wheel readable at 480px wide; nothing behind the HUD
- [ ] Crowd levels balanced by ear: cheers do not clip, bed sits under everything
- [ ] Length in the 80-120s band, else trim the holds
- [ ] Swapping `teamA` / `teamB` changes names and colours with no rebuild

## Seed

Random each Play. Record the logged seed with the upload in `PROJECTS.md`; don't scan.

## Retro

The open question: does a team-vs-team sport sim draw comments and views above our ~1.3K median?
Day-1 / 3 / 7 snapshots via `snapshot.py`. Look at comment count first: rivalry is the bet.
