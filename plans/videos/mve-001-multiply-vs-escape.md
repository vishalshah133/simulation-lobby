# mve-001: Multiply vs Escape

**Format:** `mve` · **Length:** ~80-100s (5 rounds of ~15s) · **Aspect:** 9:16 · **Status:** `built` (compiles; not yet played)
**Brief:** `KnowledgeBase/formats/multiply-vs-escape.md`
**Scene:** `MultiplyEscape_Circle_2D` (**Simulation Lobby ▸ 2D Formats ▸ Multiply vs Escape (mve) ▸ Circle**)
**Config:** `Assets/Settings/Configs/MultiplyEscapeConfig_001.asset` · **Seed:** random per Play, logged at start

## Premise

Every bounce adds a ball. Every escape widens the exit. Will the circle fill up?

**Experiment lane** (`channel-strategy.md` → *Shorts mix*). **What it tests:** a new mechanic on the
proven threshold shape. Same arena, same spinning exit and same question shape as the rotating-gap `esc`
uploads; only the mechanic is new. Judge at day 7 against the ~1-1.7K band.

## Blocks used

- [x] **REUSE** [determinism-core](../blocks/determinism-core.md): runner, seeded RNG, recorder
- [x] **REUSE** `CircularBoundary2D`, extended with an opt-in `keepGapSegments` + `RefreshGap()` so the exit
  can widen without a rebuild (`esc` unaffected: off by default)
- [x] **REUSE** `BodyPool2D` (hard cap), `ConstantSpeed2D`
- [x] **REUSE** [onscreen-counter](../blocks/onscreen-counter.md): `ScoreboardCanvasBuilder` / `VersusScoreboardHud`
- [x] **REUSE** [bounce-audio-sequencer](../blocks/bounce-audio-sequencer.md): two `BounceMelodyPlayer`s (clone / escape)

## Rules

- One ball, launched after a 1s hold. **A wall hit clones it** (seeded turn of 15-55°, born just inside).
  Ball-ball hits don't clone. A ball that's in the exit channel doesn't clone.
- Any ball that gets out through the exit is gone, and the exit widens **+1.1°** (from 16°). It keeps
  spinning at 25°/s, with the direction seeded per round.
- **FULL** = 500 inside at once: MULTIPLY scores. **EMPTY** = nobody left: ESCAPE scores. 5 rounds.
- A ball the crowd pushes through solid wall is a **leak**. It's removed uncounted, and the `leaks` metric
  and one console warning flag it.

## Tuning (modelled 2026-10-13, no ball-ball collisions, 600 runs)

| Knob | Value | Note |
|---|---|---|
| `startGapDegrees` | 16 | |
| `gapDegreesPerEscape` | 1.1 | The balance dial. Modelled at ~50/50, and ~35% of rounds empty after passing 400 |
| `ballSpeed` | 5 | ~15s median round |
| `fillTarget` | 500 | ~43% of the circle's area at r 0.12 |
| `gapAngularSpeed` | 25 | |

Crowding cuts wall bounces in Unity, which favours EMPTY. **If FULL never lands, lower
`gapDegreesPerEscape` (try 0.8).** If FULL always wins, raise it.

## Check on first Play

- [ ] The first clone appears on the first wall hit, and colours step through the rainbow by birth order.
- [ ] The wall visibly opens as balls escape. No `[mve] A ball left through the wall` warning, or only rarely.
- [ ] The score over 5 rounds isn't a 5-0 whitewash, and at least one EMPTY round peaks above ~300.
- [ ] Frame rate holds near 500 bodies (record at 60fps).
- [ ] The audio isn't a wall of noise at peak. If it is, lower `_maxNotesPerFrame` on the presenter to 1.
- [ ] HUD: `INSIDE n / 500` turns to its alert colour from 400; results read `FULL! MULTIPLY WINS` or `ALL OUT! PEAKED AT n`.

## Publish

Title options: *"Every Bounce Adds a Ball. Every Escape Opens the Exit."* · *"Will It Fill Up? 1 Ball vs the Exit"*.
Record the logged seed here.
