# rom-001 — How Many Marbles By The Bottom?

**Format:** `rom` · **Length:** ~20-30s (first guess) · **Aspect:** 9:16 · **Status:** `built`, never run in Unity
**Brief:** `KnowledgeBase/formats/release-or-multiply.md`

## Premise

Marbles fall through gates that multiply, add, halve or wipe the count. How many reach the bottom?

## Blocks used

- [x] **REUSE** [determinism-core](../blocks/determinism-core.md)
- [x] **REUSE** [onscreen-counter](../blocks/onscreen-counter.md) (`CaptionHud`, new `Style.Dark`)
- [x] **BUILD** `BodyPool2D`, `TriggerZone2D`, `WorldLabelLayer`, `CountFormat` (promoted to Shared / Presentation on first use)
- [ ] **SKIP** seed-scan-harness: seeds are random by default (CLAUDE.md, *Working agreements*)
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md) (still `planned`; the user records by hand)

## Video-specific work

Gate rules and layout only: `RomConfig`, `RomSimulation`, `RomPresenter`, `RomSceneBuilder`.

### Config + tuning targets

| Knob | Start at | Note |
|---|---|---|
| `startMarbles` / `dropIntervalSeconds` | 8 / 0.9s | Longer release = longer video |
| `gravityScale` | 0.4 | Lower = slower fall, more time per gate |
| `maxBodies` | 400 | Raise only if frame rate holds |
| final row | ×100, ×0, ×10, ×0 | Must stay the highest-variance row |

## Acceptance criteria

- [ ] Scene builds from the menu with no errors; Play runs to a held final number
- [ ] Console prints the random seed at start; pasting it back (randomize off) reproduces the run
- [ ] Marbles never exceed `maxBodies`; the count keeps climbing past it
- [ ] Count and gate labels readable at 480px wide; labels clear of the top and bottom HUD
- [ ] Length lands in the 20-45s band, else tune the dials above

## Seed

Random each Play. Note the logged seed with the upload in `PROJECTS.md`; don't scan.

## Retro

The open question: does a count-and-multiply Short beat our ~1.3K median? Day-1 / 3 / 7 snapshots via `snapshot.py`.
