# conv-003: Parabola Focus

**Format:** `conv` · **Length:** ~61s (6 rounds) · **Aspect:** 9:16 · **Status:** `published` 2026-10-23 (v25; 1,321 views at ~1.1d)
**Brief:** `KnowledgeBase/formats/math-convergence.md` (candidate topic 5, *Parabolic mirror*)
**Scene:** `Convergence_ParabolaFocus_2D` (**Simulation Lobby ▸ 2D Formats ▸ Math Convergence (conv) ▸ Parabola Focus (conv-003)**)
**Config:** `Assets/Settings/Configs/ParabolaMirrorConfig_003.asset` · **Seed:** random per Play, logged at start (cosmetic only: the geometry is exact)

## Premise

A rainbow row of balls falls straight down into a parabola. Every ball bounces once, and **all of them cross the
blue dot (the focus) at the same instant**. Then they fan out, bounce a second time, and rise out of the bowl as a
**perfectly flat, perfectly vertical line**, back at the height they fell from. Escalate the count: 5, 10, 50,
100, 500, 1000. The answer never changes, and the picture gets more satisfying every round.

The user's pick (2026-10-23), maths lane after conv-002 and clack-001 both cleared the band. It's a pure
escalation: no break round (conv-001/002 had one). That's the experiment: does "it keeps working, with more balls"
hold without a twist? If it lands flat, the obvious v2 is a **tilt round** (tilt the bowl, the focus smears into a
caustic, then tilt it back).

**Physics note.** Real dropped balls accelerate, and with gravity the paths after the bounce curve and miss the
focus. So the balls move at **constant speed in straight lines**, like light rays. They still read as "dropping".
This is the same analytic billiard as the ellipse, with no physics engine.

## Storyboard

| # | Beat | On screen | Hold caption, then result |
|---|---|---|---|
| 1 | Hook | Parabola, blue dot at its focus. 5 balls hang in a row above it (hold 2.0s), then fall, bounce, cross blue together, and rise as a flat line | `5 BALLS. GUESS?`, then `ALL 5! SAME MOMENT.` |
| 2 | Escalate | 10 balls hang, then fall | `10 BALLS...`, then `ALL 10!` |
| 3 | Escalate | 50 | `50 BALLS...`, then `ALL 50!` |
| 4 | Escalate | 100 | `100 BALLS...`, then `ALL 100!` |
| 5 | Spectacle | 500: a solid rainbow sheet | `500 BALLS...`, then `ALL 500!` |
| 6 | Finale | 1000 | `1000 BALLS?!`, then `ALL 1000. EVERY SINGLE ONE.` |
| End | Reason | Static bowl + dot | `EVERY PATH IS THE SAME LENGTH. THE DOT IS THE FOCUS.` |

Question (top panel, all video): `HOW MANY BALLS WILL HIT THE DOT?` · Title `THE PERFECT CURVE`, which becomes `IT'S CALLED A PARABOLA` on the end card (`endTitle`) · Counter `HIT THE DOT n / N`.

Cold-viewer check (`learnings.md` → *Story legibility*):
- [x] The question names things visible from frame one: the balls hang above the bowl through the hold, and the dot is drawn
- [x] Every cause is on screen: fall, bounce, cross, bounce, rise. Nothing is hidden
- [x] One question, five escalations of it, no new question
- [x] The only number is the answer (`HIT THE DOT n / N`)
- [x] It closes with the one-line *why* (equal path lengths, which is also why the exit line is flat)

## Blocks used

- [x] **REUSE** the `conv` round shell, presenter, flash, HUD, backdrop and audio (see conv-001 → *Architecture (as built)*)
- [x] **NEW kernel** `Kernel/ParabolaMirrorKernel.cs` + topic config `ParabolaMirrorConfig.cs` (open outline)
- [x] **EXTEND** (generic, conv-001/002 unchanged by default):
  - `ConvergenceConfig.showBodiesDuringHold` (false): bodies are placed at launch positions through Hold, frozen. This is the anticipation beat: you see the row before it drops
  - `ConvergenceConfig.showStartPoint` (true): hide the red start dot when the topic has no start point
  - `ConvergenceConfig.hueSpan` (1): the fraction of the hue wheel spread across the bodies. A row needs < 1 so the two ends aren't both red
  - Round `ballCount` range 600 → 1000. Base `ballCount` range min 8 → 1
  - Scene builder: presets take any `ConvergenceConfig`. Framing comes from sampling every round's kernel outline + start + target (identical numbers for the ellipse scenes)

## Video-specific work

### Geometry (all rounds the same)

Parabola `y = vertexY + x² / 4f`, with **f 0.8, rim half-width W 4, vertexY -4.5**, so the rim is at y 0.5 and the focus is at (0, -3.7).
Balls start in a row at **dropY 4.5** moving straight down at **speed 3.2**. The row is symmetric about the origin, so the existing centred framing works.

- **No gap, exact symmetry** (user, 2026-10-23). The row is evenly spaced across |x| ≤ 4·0.94 = 3.76, x_i = -3.76 + 7.52(i+0.5)/N, so odd counts put a ball exactly on the centre line. Every ball still crosses the focus at the same instant. A ball dropped at x₁ lands its second bounce at x₂ = -4f²/x₁, so balls with |x₁| < 4f²/W = 0.64 (the middle ~17% at f 0.8) leave over the rim at an angle (up to ~43°) instead of rising vertically, and the centre ball skips the second bounce. The flat exit line is the outer ~83%. f 0.6 would shrink the middle band to ~10% (≤33°) at the cost of a deeper bowl (rim y 2.17).
- **Timing.** Every path from the drop row to the focus is dropY - vertexY + f = **9.8** long, so they all meet at E = 9.8 / 3.2 = **3.0625s**. Every exit path back to the drop height is the same again, so the run is **6.125s** and ends with the line back where it began.
- **Notes.** Wall-hit pitch is 1 - |x|/W. The first bounces sweep from the rim inward (rising), the second from the centre outward (falling). The meet chord sits between the two.

### Python model (`scratchpad/parab.py`, dt 1/120, r 0.25)

| N | Peak at dot | At | Wall hits | End y (min..max) | Max abs(dx) at end |
|---|---|---|---|---|---|
| 5 / 10 / 50 / 100 / 500 / 1000 | N / N | 2.99s | 2N | 4.5000..4.5000 | < 1e-9 |

Gap-free row (current), N 1000: peak at the dot **1000/1000**. 170 balls (the middle band) don't leave vertical, at most 43° off. The table above is the earlier slit layout.

### Rounds (`ParabolaMirrorConfig_003`)

| # | Balls | Trail s | Run s | Expected |
|---|---|---|---|---|
| 1 | 5 | 1.0 | 6.125 | Meet |
| 2 | 10 | 1.0 | 6.125 | Meet |
| 3 | 50 | 0.8 | 6.125 | Meet |
| 4 | 100 | 0.7 | 6.125 | Meet |
| 5 | 500 | 0.5 | 6.125 | Meet |
| 6 | 1000 | 0.5 | 6.125 | Meet |

Pacing: firstHold 2.0, preLaunchHold 1.5, transition 0.5, resultHold 1.4, endCard 4. **Series 61.15s.**
Look: ballRadius 0.1, hueSpan 0.85, `showBodiesDuringHold` on, `showStartPoint` off, maxAngleOffsetDegrees 0. Base meet defaults (r 0.25, fraction 0.95, window 0.06).

### Framing

The tallest extent is ±4.5 (vertex / drop row) and the widest is ±4 (rim). Ortho is about 9.2, so the 9:16 view is about 10.35 wide and the rim leaves about 1.2 spare each side.

## Check on first Play

- [ ] Build **Simulation Lobby ▸ 2D Formats ▸ Math Convergence (conv) ▸ Parabola Focus (conv-003)**. The console lists 6 rounds, ~61s, widest `fits`
- [ ] No `[conv]` verdict errors. Every round reads Meet
- [ ] Each hold shows the row hanging still above the bowl, and there's no red start dot
- [ ] The row falls, crosses the dot in one flash + chord, and rises as a flat line that stops at the start height
- [ ] Rainbow left to right, ends not both red. 1000 balls spawn without a hitch
- [ ] Bounce notes: a rising sweep, the chord, then a falling sweep. Not mush at 1000 (cap 2 notes per frame)
- [ ] Ellipse scenes (conv-001/002) rebuild with the same logged framing as before
- [ ] Readable at 480px. The counter and captions are clear of the bowl and the row

## Acceptance criteria

- [ ] Same seed twice gives an identical outcome
- [ ] Every round's measured verdict matches the config
- [ ] Seed logged with the published video below

**Published seed:** _record from the console_
