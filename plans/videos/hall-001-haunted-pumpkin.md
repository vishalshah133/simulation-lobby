# hall-001: Escape the Haunted Pumpkin

**Format:** `hall` · **Length:** ~60-70s · **Aspect:** 9:16 · **Status:** `built`
**Brief:** `KnowledgeBase/formats/haunted-pumpkin.md`
**Build:** `Simulation Lobby ▸ 2D Formats ▸ Haunted Pumpkin (hall) ▸ Pumpkin` -> `Assets/Scenes/HauntedPumpkin_Pumpkin_2D.unity`, config `Assets/Settings/Configs/HauntedPumpkinConfig_001.asset` (never run yet)

## Premise

Every break splits the ghost. Can the swarm break out of the pumpkin before the candle burns out?

**Mix:** proven shape (one arena, one yes/no question, ring-by-ring progress from `brk`) plus the split from `mve`
plus a guess-the-song hook (song Shorts: 1.7K, 1.7K; "Will It Escape?": 3.2K, our best Short).
Judge at day 7 against the ~1.2-1.4K band; the Halloween date is a one-off boost, so do not read a spike as a format win.

## Storyboard (write before any code)

| Beat | On screen | Caption / counter |
|---|---|---|
| Hook (0-1 s) | Glowing jack-o'-lantern, 3 nested rings, one green ghost, candle at full height | `CAN THE GHOSTS ESCAPE BEFORE THE CANDLE DIES?` / `GUESS THE SONG` |
| Build (1-15 s) | Ghost hits a segment: it cracks, shatters into candy/bone bits, the ghost splits in two. Swarm grows, one note per break | `GHOSTS n` |
| Ring 1 opens (15-30 s) | Outer-ring gap appears, first ghost slips out and turns into a bat that flies up. Candle about 1/2 | `RINGS BROKEN 1/3` `BATS OUT n` |
| Tension (30-55 s) | Rings 2 and 3 grind down; candle low and flickering; a near-miss ring that holds until the last seconds | `THE CANDLE IS ALMOST OUT...` |
| Payoff (55-65 s) | Either bats flood out with a flash, or the candle dies and the screen goes dark | `THEY ESCAPED!` or `THE PUMPKIN WINS` |
| Why (last 3 s) | One line | `Every break splits a ghost, so more ghosts means more breaks.` |

Cold-viewer check:
- [ ] The question names things visible from frame one (pumpkin, ghost, candle)
- [ ] Every cause is on screen: crack, shatter, split, bat leaving
- [ ] One question only; the rings and candle are progress for that one question, not new questions
- [ ] Only the answer-relevant numbers show: rings broken and bats out. No tallies that contradict the result
- [ ] Ending states the why in one line

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md)
- [ ] **REUSE** [seed-scan-harness](../blocks/seed-scan-harness.md) (opt-in only; seeds are random, log the drawn one)
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md)
- [ ] **REUSE** [onscreen-counter](../blocks/onscreen-counter.md)
- [ ] **REUSE** [bounce-audio-sequencer](../blocks/bounce-audio-sequencer.md) (one tune note per break)
- [ ] **PROMOTE** whatever `Breaker` (breakable ring segments) and `MultiplyEscape` (clone-on-hit) share into `Shared`; `hall` must not reference either format

## Video-specific work

### Simulation / rules

- Ghosts are circles. A ghost touching a ring segment damages it; at 0 hit points the segment breaks.
- Each break has `cloneChance` of splitting the ghost (clone born just inside, seeded turn of 15-55 deg). Never clone on ghost-ghost hits.
- A ghost passing through an opening in the outer ring is removed and counted as a bat out.
- Hard body cap (`BodyPool2D`). Weight-based counting if the cap binds.
- Never launch from dead centre (`brk` lesson). Innermost ring breaks on the first touch (instant hook).
- **Real way to lose:** the candle clock hits 0 with fewer than `batsToWin` out. `mve` showed that cloning on every hit can never lose, so `cloneChance` is below 1 and tuned.
- Presentation reads state; the candle, bats and HUD never write sim state.

### Config + tuning targets

| Knob | Start at | Note |
|---|---|---|
| `ringCount` | 3 | segments 10/12/14, hit points 1/2/2 |
| `cloneChance` | 0.6 | the balance dial with `batsToWin` |
| `batsToWin` | 40 | tune so ~50% of seeds win and the clock is within ~10 s of expiring in a good share |
| `timeLimitSeconds` | 55 | candle length |
| `ghostSpeed` | 6 | constant speed |
| `bodyCap` | 400 | |

Tune from real run numbers, not the model (the `brk` model ran 2.7x optimistic; the `mve` formula 1.4x high).
Log a `[hall] round ...` summary (time, peak ghosts, rings broken, bats out) at the end of every run.

### Look

Near-black purple background, orange jack-o'-lantern glow, bone/candy-corn/cobweb segments in high-contrast tones, neon-green ghosts, purple bats.
Candle melt bar on the side, away from the arena. Check at 480px wide and at both aspects.
Audio: procedural only (`ProceduralToneBank`). Song: public domain, e.g. *Toccata and Fugue in D minor* or *Funeral March of a Marionette*, one note per break; creak when a ring opens.

## Acceptance criteria

- [ ] All block acceptance criteria met
- [ ] Same seed twice gives the identical outcome
- [ ] Readable at 480px wide; HUD clear of platform UI on a real phone
- [ ] Length 60-70 s
- [ ] Neither outcome dominates over ~10 random Plays; at least one near-miss ending
- [ ] No ghost leaks through solid wall

## Render + publish

Thumbnail: pumpkin with a ghost swarm pouring out of the top ring. Pinned comment: "How many bats do you think got out? Guess below, and name the song 👇".
Give title, description, and tags as copy-paste blocks when the video is ready. Record the logged seed here.

## Retro

Write `KnowledgeBase/retros/YYYY-MM-DD-hall-001-haunted-pumpkin.md`, update `/PROJECTS.md`.
Open question: does a themed spin on proven mechanics beat the ~1.3K band, or is it the same floor with a seasonal label?

## Rule change (2026-10-31, after first recordings)

The 40/50/60-bat target was unreachable (ghosts only spawn on breaks), so runs emptied early and counts never matched.
**Win = first ghost through the outer ring**, before a 65 s candle. Bats pour out in a 5 s finish hold. Config: 5 rings 10/12/14/16/18,
hit points 1/3/6/11/16, split 0.5, speed 3, hold 1.5 s. Video ~72 s max. See the format brief's *Learned* section for the model numbers.
Tune `timeLimitSeconds` so the breakout lands in the candle's last ~10 s in about half the runs.
