# fit-001 — Perfect Fit

**Format:** `fit` · **Length:** ~66s · **Aspect:** 9:16 · **Status:** `built, not yet played`
**Brief:** `KnowledgeBase/formats/perfect-fit.md`

## Premise
Will the shape fit through the hole? Seven shapes, the gap shrinking from 30 mm to 0.5 mm.

## Storyboard

| Beat | On screen | Caption / counter |
|---|---|---|
| Hook (0-1.8 s) | Spinning wall, glowing star-shaped hole, shape tumbling in front | WILL THE TRIANGLE FIT? / GAP 30 MM |
| Approach (~3 s) | Shape dashes in, tumble and spin offset fall to zero just before the wall | same |
| Pass (~2 s) | Camera pushes in; shape slides through, only a hairline of light left | same |
| Payoff (1.5 s) | Through, still turning in step; glass note | FITS. |
| Rounds 2-7 | Next wall slides in; tighter gap, faster spin, harder shape | GAP 15 / 8 / 4 / 2 / 1 / 0.5 MM |
| End card (4.5 s) | Last wall, wide shot | 7 SHAPES. ZERO TOUCHES. / SAME SHAPE, SAME SPIN: THE GAP NEVER CLOSES. |

Cold-viewer checklist:
- [x] The question names things visible from frame one (the shape and the hole)
- [x] The cause happens on screen (it visibly turns into step with the wall)
- [x] Every round raises the stakes (gap, spin, shape complexity)
- [x] The only number is the gap
- [x] The ending says why in one line

## Blocks used
- [x] **REUSE** determinism-core (seeded draws, fixed step), capture-shorts
- [x] **REUSE** `ProceduralToneBank` (riser, click, scale, glass clink), `CaptionHud` / `CaptionCanvasBuilder`
- Not used: `studio-look-3d` (its backdrop is high-key white; the glow-through-the-gap trick needs a dark stage, so this scene has its own small dark stage)

## Video-specific work
Everything is in `KnowledgeBase/formats/perfect-fit.md`. Tuning knobs live in `PerfectFitConfig_001.asset`: `rounds` (gap, spin), `flightSeconds` (5.2), `alignLeadFraction`, camera poses, `slideSeconds`.

## Acceptance criteria
- [ ] Same seed twice → identical take
- [ ] Readable at 480 px wide; gap glow visible on rounds 5-7
- [ ] Bottom 288 px empty on a real phone
- [ ] Length 60-90 s
- [ ] Is it satisfying, or just repetitive? (the open question this video answers)

## Render + publish
Seed is random per Play and logged; record it. Title / description / tags once it has been played and approved.
