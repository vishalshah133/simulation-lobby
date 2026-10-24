# royale-001: Four Colour Ring

**Format:** `royale` · **Length:** ~65-80s (one game) · **Aspect:** 9:16 · **Status:** `built`
**Brief:** `KnowledgeBase/formats/ring-royale.md`
**Scene:** `RingRoyale_FourColour_2D` (**Simulation Lobby ▸ 2D Formats ▸ Color Rumble (royale) ▸ Color Rumble (royale-001)**)
**Config:** `Assets/Settings/Configs/RingRoyaleConfig_001.asset` · **Seed:** random per Play, logged at start

## Premise

4 colours, 1 exit, 1 x2. Which color survives?

**Experiment lane** (`channel-strategy.md` → *Shorts mix*). **What it tests:** four colour teams to root for
in a Short, on the proven spinning-ring arena.

## Storyboard

| Beat | t | On screen | Caption / counter |
|---|---|---|---|
| Hook | 0-2s | Ring, still. 20 marbles (5 red, 5 blue, 5 green, 5 yellow) frozen in place. EXIT gap and gold x2 arc labelled | Top: **COLOR RUMBLE**, share bar 5 / 5 / 5 / 5. Bottom: **WHICH COLOR SURVIVES?** |
| Launch | 2s | All marbles fire in random directions; the ring starts spinning | |
| Growth | 2-30s | x2 hits pop a clone (gold flash + chime); exits drop marbles out of the ring (thud) | Share bar counts move live |
| First out | ~25-45s | A colour hits 0 | Bottom: **GREEN IS OUT!** (its bar segment greys) |
| Escalation | 40s | The exit starts widening, visibly | Bottom: **SUDDEN DEATH: THE EXIT IS GROWING** |
| Last two | ~50-65s | Two colours left, the gap now wide | Bar shows 2 live segments |
| Payoff | end | The last marble of the runner-up leaves; the winners freeze; **the EXIT slides shut behind them** (0.9s, edging turns the winner's colour), heavy thud, confetti in the winning colour | **RED WINS!** + `with N marbles left` |

Cold-viewer checklist:
- [x] The question names things visible from frame one (4 colours, the EXIT and x2 labels on the ring)
- [x] Every cause is on screen: x2 flash + clone pop, exits leave through the visible gap, sudden death opens the gap
- [x] Every beat changes the answer: lead changes (~15 modelled), eliminations, sudden death
- [x] The only numbers shown are the four live counts, which are the answer
- [ ] Not a curiosity, so no *why* line needed

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md): runner, seeded RNG, recorder
- [ ] **REUSE** `CircularBoundary2D` with `keepGapSegments` + `RefreshGap()` (gap widens without a rebuild, as in `mve`)
- [ ] **REUSE** `BodyPool2D`, `ConstantSpeed2D`, `BounceDetector2D`
- [ ] **REUSE** `ShareBarCanvasBuilder` / `ShareBarHud` (4 segments, counts), `CaptionHud`, `ConfettiBurst2D`
- [ ] **REUSE** `ProceduralToneBank` for clone chime / exit thud / elimination sting
- [ ] **PROMOTE?** `mve`'s `SwarmBall` (wall-hit consume, exiting flag). If `royale` needs it near-verbatim,
  promote the shared part into `Shared/Physics2D` instead of copying. Formats never reference each other.

## Rules

- 4 colours × `marblesPerColour` (5). Spawned scattered inside 0.75 r with a minimum spacing, never at the centre.
  Each gets a seeded random direction. All launch together after `preLaunchHoldSeconds` (2s).
- Constant speed (`ballSpeed`). Ball-ball collisions on.
- Ring spins at `ringAngularSpeed` (30°/s), direction seeded. EXIT gap centred at 90°, x2 arc at 270°, both rotating with the ring.
- **x2:** a wall hit whose contact angle is inside the x2 arc clones the marble (same colour, heading turned by a seeded
  15-55°, born just inside the wall). Capped at `bodyCap` (300).
- **EXIT:** passing out through the gap = out. Through solid wall = leak (uncounted, warned once).
- **Sudden death:** from `suddenDeathAtSeconds` (40) the exit widens by `suddenDeathDegreesPerSecond` (3).
- A colour at 0 is eliminated. The last colour standing wins. Safety cap `maxGameSeconds` (150); if it's hit, the most marbles wins (logged as a timeout).
- After the win: the winners freeze (kinematic), the EXIT closes over `sealSeconds` (0.9s) to seal them in, then thud + confetti, hold `resultHoldSeconds` (4s), complete.

## Tuning (modelled, see brief)

| Knob | Value | Note |
|---|---|---|
| `marblesPerColour` | 5 | |
| `exitStartDegrees` | 16 | |
| `x2ArcDegrees` | 40 | The growth dial. Wider = bigger numbers, longer game |
| `suddenDeathAtSeconds` | 40 | The length dial. Modelled game ~55-65s |
| `suddenDeathDegreesPerSecond` | 3 | |
| `ringAngularSpeed` | 30 | |
| `ballSpeed` | 5 | |
| `ringRadius` / `ballRadius` | 4.6 / 0.18 | The ball must fit through the 16° gap (chord ≈ 1.28) |

## Look

Dark background, light-grey ring. EXIT gap edged in red with a world-space TMP `EXIT` decal outside the ring.
The x2 arc is a thick gold overlay on the wall with an `x2` decal; it flashes on each clone. Marbles in high-contrast
red / blue / green / yellow with a darker rim. HUD: top question + 4-segment share bar with counts, bottom caption.

## Check on first Play

- [ ] Marbles visibly clone on the gold arc only, and leave only through the gap
- [ ] No `[royale] leak` warning, or only rarely
- [ ] The game ends in 50-90s; there's at least one lead change and one colour elimination before sudden death
- [ ] Share bar and captions readable at 480px wide

## Publish

Record the logged seed here.
