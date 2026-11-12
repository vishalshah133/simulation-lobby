# Format brief: `mve` — Multiply vs Escape

**Tension type:** threshold (*will it fill up?*), driven by two feedback loops racing each other.
**Origin:** an original idea from the experiment lane (2026-10-13), not taken from a reference channel.
**First video:** [mve-001](../../plans/videos/mve-001-multiply-vs-escape.md).

## The shape

The rotating-gap circle from `esc`, with one rule change: every wall bounce **clones** the ball, and every
escape **widens** the exit. Growth is exponential on both sides. The swarm fills up fast, but each escape
makes the next one likelier. A round ends FULL (fill target reached) or EMPTY (everyone out).

## Why it might work

- It keeps the shape that holds the ~1K floor (one arena, one yes/no question, a spinning exit). Only the
  mechanic is new (`channel-strategy.md` → *Shorts mix*, rule 3).
- **Numbers going up on screen** and **near-elimination saves** are both on the *Hooks that reliably
  work* list. A round that reaches 480/500 and then drains is the money shot, and the tuning aims for it.
- The wall disappearing as the swarm pours out is a satisfying visual of its own.

## Design rules

- **Real way to lose on both sides.** Tune `gapDegreesPerEscape` so neither outcome dominates. Modelled
  without collisions, the knife edge sits near *peak inside ≈ 360·(x0 − ln x0 − 1)/δ*, where x0 = start
  gap/360 and δ = degrees per escape. Set the fill target close to that.
- **Clone only on wall hits**, never on ball-ball hits. In a crowd, ball-ball cloning runs away and the
  question disappears.
- **Exits only through the opening.** A ball pushed through solid wall is a leak, not an escape.
- **Never start from dead centre** (the `brk` lesson).

## Open questions (fill in after mve-001)

- Does crowding shift the balance much compared with the model?
- Does the swarm stay readable at 480px wide, or does it turn into noise?

## Wide variant (comp-001)

Segment 10 of the ten-sims compilation, "Fill or Escape", 16:9. Built, **never run** (mve-001 itself has not been run either, so balance and 480px readability are unknown).

- Menu: Simulation Lobby ▸ 2D Formats ▸ Multiply vs Escape (mve) ▸ Wide: Fill or Escape (comp-001). Scene `Assets/Scenes/Comp001_10_FillOrEscape_Wide.unity`, config `Assets/Settings/Configs/MultiplyEscapeConfig_comp001.asset` (kept on rebuild, so tuning survives).
- One round, fill 300, **`cloneChance` 0.6**, start gap 24 deg, clone turn 20-70 deg, `gapDegreesPerEscape` 1.5, cap 38s, hold 2s, result beat 4.5s (~45s total, estimated).
- Palette: near-black frame, steel wall, cyan = inside, amber = escaped. Counter right margin `IN n / 300`, `OUT n` panel left margin (`MultiplyEscapeWidePresenter`), caption bottom, result then one-line why.
- Balance: the brief formula gives 777/delta = 300 at delta 2.6, but mve-001's own model put 50/50 at fill 500 / delta 1.1 where the formula says 706, so the formula runs ~1.4x high. Calibrated start: 777/(300*1.4) = 1.85, set 1.8 for crowding.
- Tune after run 1, in order: `gapDegreesPerEscape` (FULL always: raise; EMPTY with peak under ~200: lower), `startGapDegrees`, `maxRoundSeconds`. A "so close" ending (peak 255+/300) is a seed outcome, not a config guarantee: re-roll Play and keep the take.

## Why cloning always won (found 2026-10-28, first look at the wide variant)

With a clone on **every** bounce, net growth per bounce is `1 - p` (p = the exit's share of the wall), which is positive
until the exit is the whole wall. The swarm cannot lose, so FULL was certain and the formula in the brief only held for
tiny swarms. Fix: `cloneChance` (config, default 1 so mve-001 is unchanged). Growth now stalls when p passes the clone chance,
and the peak is `1 + (c*ln(c/p0) - c + p0) / (delta/360)`, independent of bounce rate. A Python event model (bounce rate .65/s,
effective gap = gap + 4 deg) for c .7, gap 24, delta .8-1.0 gave FULL ~20-75% across +-3 deg of effective-gap error, ~30%
near-misses (peak 240+), 18-30 s. The outcome flips sharply with `gapDegreesPerEscape`, so **tune that one number** after run 1.
Also added: `EXIT n°` under `OUT n`, and a running caption that names who is winning (`ALMOST FULL!` / `THE EXIT IS WINNING...`).

### Round 2 (10-28): c .7 / +0.9 still filled in ~16 s every time

Editor.log gave the real numbers. The sim's RNG draw count gives hits and clones (draws = 3 + hits + 2 x clones), so
escapes = clones - 299: only **3 and 15 escapes in ~450 wall hits** (a flat gap-share model said ~30-40). Real escape rate at a 24-38 deg exit is about
**a quarter of the geometric share** (the thick wall's channel turns most oblique approaches back; it improves as the exit widens).
Bounce rate fits ~0.63/s per ball. Model re-fitted with an escape factor that rises with gap width; c .6, +1.5 gives
FULL ~50% / EMPTY ~50% around that factor, with ~35% near-misses and 20-28 s. The sim now logs a `[mve] round ...` summary
(time, peak, born, escaped, exit width) at round end: read it after a run and tune `gapDegreesPerEscape` from it.
