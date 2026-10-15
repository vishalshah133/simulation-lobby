# conv-002: Ellipse Stretch

**Format:** `conv` · **Length:** ~62s (9 rounds) · **Aspect:** 9:16 · **Status:** `built` 2026-10-14 (compiles; harness-checked; not yet played)
**Brief:** `KnowledgeBase/formats/math-convergence.md`
**Scene:** `Convergence_EllipseStretch_2D` (**Simulation Lobby ▸ 2D Formats ▸ Math Convergence (conv) ▸ Ellipse Stretch (conv-002)**)
**Config:** `Assets/Settings/Configs/EllipseFocusConfig_002.asset` · **Seed:** random per Play, logged at start (cosmetic only)

## Premise

**The same question, nine times, with one visible change each time.** "How many balls from red hit blue?" Move red a little: it breaks. Put it back: it works. Stretch the ellipse taller, squash it wide, make it nearly a line, a circle, 600 balls: it still works. Stretch it but leave the dots where they were: it breaks. Only the focus matters, and the shape around it does not.

**Experiment lane, length test against [conv-001](conv-001-ellipse-focus.md).** Same question and same mechanic, ~62s instead of ~24s. Judge at day 7: does the longer, escalating "will it still work?" run hold viewers, or does the first break-and-fix already say everything? Retention needs YouTube Studio (the API key can't see it).

## Storyboard

| # | Beat | On screen | Caption (Transition/Hold), then result |
|---|---|---|---|
| 1 | Hook | Ellipse 3.75 x 5, red on the top focus, blue on the bottom. Hold 1.5s. Fan leaves red and lands on blue together | Hold `GUESS?`, run `WATCH BLUE`. `ALL 120. AT THE SAME TIME.` |
| 2 | Twist | Red slides right 0.7 (a ghost stays on the focus and fades out before launch). Hold, launch: nearly meets, smears | `NOW MOVE RED A LITTLE...` then `GUESS?`. `ONLY {n} HIT.` |
| 3 | Fix | Red slides back to the focus | `PUT IT BACK...` then `GUESS?`. `ALL 120 AGAIN.` |
| 4 | Escalate | The outline stretches taller (3.0 x 5.75); both dots glide with the foci | `STRETCH IT TALLER...` then `GUESS?`. `STILL ALL 120!` |
| 5 | Trap | Stretches again (2.0 x 5.75) but **the dots stay** where the old foci were | `STRETCH AGAIN... BUT THE DOTS STAY` then `GUESS?`. `ONLY {n} HIT.` |
| 6 | Other axis | Squashes wide (4.6 x 2.6); the dots swing onto the sideways foci | `SQUASH IT WIDE...` then `GUESS?`. `ALL 120. SIDEWAYS TOO.` |
| 7 | Extreme | Almost a line (1.0 x 5.75); dots near the tips | `ALMOST A LINE...` then `GUESS?`. `STILL ALL 120!` |
| 8 | Odd one | A circle (3.75): both dots merge into the centre | `MAKE IT A CIRCLE...` then `GUESS?`. `ALL 120 BACK TO THE MIDDLE.` |
| 9 | Spectacle | Back to 3.75 x 5, 600 balls, trails off | `NOW 600 BALLS...` then `GUESS?`. `ALL 600!` |
| End | Reason | Static ellipse | `ONLY THE FOCUS WORKS. EVEN WHEN YOU STRETCH IT.` |

Cold-viewer check (`learnings.md` → *Story legibility*):
- [x] The question (`HOW MANY BALLS FROM RED HIT BLUE?`) names things visible from the first frame (red and blue dots, both drawn before launch)
- [x] Every cause happens on screen: red slides (R2, R3), the outline morphs and the dots glide (R4-R8). R5 is the one subtle case: the outline stretches and the dots visibly do not follow, so the caption says so (`BUT THE DOTS STAY`)
- [x] Every beat changes the answer or raises the stakes. R6-R9 each remove a doubt ("only works vertical?", "only a fat ellipse?", "only a few balls?"). If the first Play drags, cut R7 first
- [x] The only number shown is the answer (`HIT BLUE n / N`). No tally, no round label
- [x] The ending says why in one line, over a viewer who has now watched it survive every stretch

Result lines land the moment the verdict closes (just after the expected arrival), while the trails keep drawing. The counter is live up to then and locks to the verdict count after, so a later bunch near blue can't contradict the line.

## Blocks used

Everything is reused from conv-001. See its **Architecture** section; nothing is copied here.

- [x] **REUSE** the `conv` kernel + round shell, presenter, flash, HUD, backdrop and audio (conv-001 → *Architecture (as built)*)
- [x] **EXTEND** (generic, conv-001 unchanged): kernel `targetOffset`, per-round `ballCount`, `holdCaption`, departure-gated counter, first-pass-peak verdict, `DisplayCount` lock, early result line, trails off at `trailSeconds` 0, pool pre-warm to the largest round

## Video-specific work

### Rounds (`EllipseFocusConfig_002`)

All rounds speed 5 and the config's 120 balls unless noted. Foci sit at c = sqrt(hh² - hw²) from the centre, on the long axis.

| # | hw x hh | c | Red offset | Blue offset | Run s | Trail s | Expected |
|---|---|---|---|---|---|---|---|
| 1 | 3.75 x 5 | 3.307 | 0 | 0 | 2.08 | 0.4 | Meet |
| 2 | 3.75 x 5 | 3.307 | (0.7, 0) | 0 | 6 | 1.5 | Miss |
| 3 | 3.75 x 5 | 3.307 | 0 | 0 | 2.08 | 0.4 | Meet |
| 4 | 3.0 x 5.75 | 4.9054 | 0 | 0 | 2.38 | 0.4 | Meet |
| 5 | 2.0 x 5.75 | 5.3910 | (0, -0.4856) | (0, +0.4856) | 2.45 | 0.6 | Miss |
| 6 | 4.6 x 2.6 (foci on x) | 3.795 | 0 | 0 | 1.92 | 0.4 | Meet |
| 7 | 1.0 x 5.75 | 5.662 | 0 | 0 | 2.38 | 0.4 | Meet |
| 8 | 3.75 x 3.75 (circle) | 0 | 0 | 0 | 1.58 | 0.4 | Meet |
| 9 | 3.75 x 5, **600 balls** | 3.307 | 0 | 0 | 2.08 | **off** | Meet |

**Round 5 arithmetic.** Round 4's foci are at ±4.9054 (sqrt(5.75² - 3²) = sqrt(24.0625)) and round 5's at ±5.3910 (sqrt(5.75² - 2²) = sqrt(29.0625)). The dots stay at ±4.9054, so red is 5.3910 - 4.9054 = **0.4856 inside** the +y focus (offset -0.4856) and blue is 0.4856 inside the -y focus (offset +0.4856). The brief quoted ±5.385 / 0.479; the exact figures are 5.391 / 0.486, and the config computes them from the formula, so the dots land exactly on the old foci. The offset is about 5% of the long axis, small enough to read as "the dots stayed", large enough to miss.

Text: headline `THE MAGIC POINT` (title slot, above the question), question `HOW MANY BALLS FROM RED HIT BLUE?`, `holdCaption` `GUESS?`, counter `HIT BLUE`, endCard `ONLY THE FOCUS WORKS. EVEN WHEN YOU STRETCH IT.`
Pacing: firstHold 1.5, preLaunchHold 1.0, transition 1.5, resultHold 1.5, endCard 3. Base defaults otherwise (r 0.25, meetFraction 0.95, verdict window 0.06).

### Length (`SeriesSeconds`)

| Video | Series | Rounds |
|---|---|---|
| conv-001 | 24.36 s | 3 |
| conv-002 | **62.30 s** | 9 |

(conv-002: end card 3 + R1 5.08 (1.5 hold + 2.08 run + 1.5 result) + eight later rounds at 4.0 fixed each (1.0 hold + 1.5 transition + 1.5 result) = 32, plus their runs 22.22 = 62.30.)

### Harness numbers (`scratchpad/convcheck`, kernel only, dt 1/120, r 0.25, departed-gated)

Verdict is the peak count over the whole first pass, [0, E + 0.06]. "After" is the maximum later in the run, for reference: the counter is locked by then.

| Round | E s | First-pass peak | Verdict | Max after verdict |
|---|---|---|---|---|
| 001 R1 / R3 | 2.00 | 120/120 | Meet | 0 |
| 001 R2 (red +0.7) | 2.00 | 10/120 | Miss | 14 @ 2.65s |
| 002 R1 / R3 | 2.00 | 120/120 | Meet | 0 |
| 002 R2 (red +0.7) | 2.00 | 10/120 | Miss | 14 @ 2.65s |
| 002 R4 (3.0 x 5.75) | 2.30 | 120/120 | Meet | 0 |
| 002 R5 (2.0 x 5.75, dots stay) | 2.30 | **14/120** | Miss | **59 @ 2.75s** |
| 002 R6 (4.6 x 2.6) | 1.84 | 120/120 | Meet | 0 |
| 002 R7 (1.0 x 5.75) | 2.30 | 120/120 | Meet | 119 @ 2.38s (end of run) |
| 002 R8 (circle) | 1.50 | 120/120 | Meet | 0 |
| 002 R9 (600 balls) | 2.00 | 600/600 | Meet | 0 |

Threshold 114 of 120 (570 of 600). Two runs are bit-identical. R5 is clearly low (14, under 25). **R5 is cut at 2.45s** (lead review). About 59 of 120 balls bunch near blue at 2.75s, after `ONLY 14 HIT.` is up. The lock kept the numbers consistent, but the picture would still contradict the line (`learnings.md` → *Story legibility*, rule 6), so the run ends before the bunch forms.

### Framing

Ortho size 11.6, so the 9:16 view is 13.05 x 23.2 units. Tallest ellipse 11.5 (hh 5.75), widest 9.2 (R6, hw 4.6), which leaves about 1.9 units spare each side. The builder logs these and warns if the widest round leaves under 0.4.

## Check on first Play

- [ ] Build **Simulation Lobby ▸ 2D Formats ▸ Math Convergence (conv) ▸ Ellipse Stretch (conv-002)**. The console lists 9 rounds, ~62s, ortho 11.6, widest `fits`
- [ ] No `[conv]` verdict errors in the console (R2 and R5 must read Miss, the rest Meet)
- [ ] The bottom line reads `GUESS?` through every hold, the round caption during the transition, and the result line appears at the arrival while trails still draw
- [ ] R5: the outline stretches and the dots are clearly left behind. Red and blue sit slightly inside the new foci. The result is a small number
- [ ] R6: the dots swing from the vertical to the horizontal foci and stay under the balls. The wide ellipse is inside the frame with no clipping
- [ ] R8: the counter reads 0 at launch (not 120), then jumps at the centre. The dots sit on top of each other and one reads as a single dot
- [ ] R9: 600 balls spawn without a hitch, hues spread around the full wheel, no trails
- [ ] The counter never drops after the answer shows; no flash on R2 or R5 after their results
- [ ] Wall-hit audio isn't mush at 600 balls (cap is 2 notes per frame)
- [ ] Total runtime is about 62s and rounds 7 and 8 don't feel like padding
- [ ] While red is off the focus, the ghost carries a **"MAGIC POINT"** decal (`idealSpotLabel` on the config) that fades with it. It sits inside the outline and clear of red, or just outside the wall where the ellipse is too narrow. It never straddles the line. It reads at 480px wide

## Acceptance criteria

- [ ] Same seed twice gives an identical outcome
- [ ] Every round's measured verdict matches the config
- [ ] Readable at 480px wide; HUD clear of the ellipse in every round
- [ ] A first-time viewer can say what changed in each round

## Publish

Title options:
- *"Stretch It. Squash It. Do They STILL All Hit Blue?"*
- *"How Many Balls Hit Blue? (It Never Matters What Shape)"*
- *"Red Fires 120 Balls. Where Do They Land? (Ellipse)"*

Pinned comment: *"Why did the stretched one fail when the dots stayed put? 👇"*. Hashtags `#physics #maths #satisfying #shorts`. Record the logged seed here.

## Retro

The open question: **does the ~62s nine-round version hold a viewer longer than the ~24s three-round one, with the same question?** Compare at day 7. Retention needs Studio.
