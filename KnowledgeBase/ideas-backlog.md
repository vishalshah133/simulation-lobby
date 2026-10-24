# Ideas Backlog

Unfiltered dump. Promote an idea into `formats/` only when it's earned a real brief. Prefix with the
format slug it would use. Mark `[built]` / `[published]` / `[dead]` as they move.

## Shorts

- `rom` — Release or Multiply, classic gate ladder, final gate 70/30
- `rom` — Start at 1,000,000 and survive the halving gates
- `rom` — Carry-over series: each video starts where the last ended
- `race` — 8 marbles, 2D spiral drop, 30s
- `race` — Two marbles only, photo finish track
- `surv` — Shrinking circle, 20 marbles, 45s
- `surv` — Rising lava, last platform standing
- `race` — Tiny marble vs giant marble down the same track
- `wars` — 1 red vs 100 blue, conversion on touch
- `esc` — Will the ball escape? Grows on every bounce, single circle `[published]` esc-001, 2026-09-27
- `esc` — The gap itself rotates around the boundary at a steady angular speed, timing not just size `[published]` esc-002, 2026-09-29 — seed is randomized-on-start by design, not reproducible
- `esc` — Boundary shrinks each bounce instead of the ball growing
- `esc` — 100 wall layers, one breaks per hit, layer counter on screen
- `esc` — Regenerating walls: can it out-pace the regrowth?
- `esc` — Ball speeds up each bounce, melody emerges as it accelerates
- `esc` — Two balls in one circle, first to escape wins (competitive crossover)

## Long form

- `wars` — 4-faction arena, 200 each, 12 min
- `elim` — 16-marble bracket tournament, different track each round
- `race` — 100 marbles, 3D banked mega-track, full leaderboard. **Supported 10-18:** the 2:05 16:9 Grand Prix (v13) is at 1,779 on day 8 and still accelerating, so a full-length race has a real drip to build on. **Weaker 10-20:** v13 stopped at ~1.84K by day 10 (+58 in 2 days)
- `elim` — Season: recurring named contenders with a standings table across videos
- `wars` — Conversion vs destruction A/B, same seed, side by side
- `surv` — Escalating hazard gauntlet, 15 min, survivor counter throughout

### From MIKAN (2026-10-16, see `reference-channels.md` → *MIKAN*)

- **Survival + proliferation race** (new long-form format, **next long-form pick**) `[published]` v19, 73 views at day 3 [surv-002 Lava Run](../plans/videos/surv-002-lava-run.md). Colour teams run a 3D course;
  ×2 gates multiply marbles while a hazard (lava rising behind, a crusher, a rolling boulder, a tsunami) thins them.
  Last colour with marbles alive wins. Reuses `rom`/`mve` multiply gates, `BodyPool3D`, the `race` course builder and
  `terr` team colours; only the hazard is new. 10-12 min, one arc in acts (calm → hazard starts → hazard speeds up →
  last survivors). Reference: MIKAN *Escape from Me* (28M), *Roller Coaster - Survival Worm Race* (115M).
- `terr` — **Numbered series**: *Which Country Takes the Board? #2*, rotating country line-ups. Cut to 10-12 min. **On hold (proposed 10-17, stronger 10-18):** country theming is on every low (v11, v12, v18). Continue the series with colour teams instead. *10-20:* colours didn't rescue v19 either, so theme isn't why long uploads stall.
- `elim` — **Team elimination series**, numbered: 8 colour teams, a different course per round, the losing team out
  each round. MIKAN's longest-running series (53 titles). Needs `elim` built.
- `terr`/`wars` — **Core destruction**: each team defends a core, attacks the others'. Same shape as `terr`
  (cannon tile = core); a pure-physics variant without the Plinko economy is the cheaper cousin.
- `race` — **Survival roller coaster**: marbles on a looping coaster track, the ones flung off are out. MIKAN's
  biggest single title shape (115M, also MIKAN 3D's 14.5M *Abstract World* coaster). Short (2 min, 16:9) or long.
- `fall` — **Freefall Symphony** `[building]` [fall-001](../plans/videos/fall-001-freefall-symphony.md): a hypnotic, sound-on long form from
  MIKAN 3D's *Abstract World* (14.5M) as inspiration. A marble falls through 7 worlds and every bounce plays a public-domain song.
  Spin-offs if it works: one-song 9:16 Shorts per world, a "guess the song from the sculpture" variant.
- **Factory** (`[parked]`, too costly per video): a physics assembly line that "builds" something. MIKAN's
  biggest hits (84-115M), but hand-authored stations and no seedable contest. Revisit once long form is proven.

### From Carson Jay + Crazy Marble Race (2026-10-18, see `reference-channels.md` → *@carsonjay*)

- `elim`/`race` — **Fixed-rules numbered show** `[published]` v22, 2026-10-19 (7 views at day 1; judge at day 7), elim-001 (16 marbles, 6 heats, a Last Chance heat): *24 Marble Race EP. N: <Theme> Race*. 24 marbles, 5 stages, 4 out
  per stage, a new theme (and course dressing) every episode. The rules never change, so an episode is a theme and a
  seed. Crazy Marble Race has run it every 2 weeks for 7 years (5.8M for EP. 1, ~60-110K now). **Strongest
  long-form series candidate after Lava Run.** Needs `elim` built on the `Shared` track course.
- `surv` — **Marble Survival: a hunter ball chases the field**, anything it touches is out. Crazy Marble Race's best
  series per view (*Angry Ball* 7.0M, *Happy Ball* 2.6M, median 123K). A Lava Run sibling: the hazard moves and
  hunts instead of rising.
- `wars` — **Infection / hide and seek**: one marble converts (*Escape the Virus*, 2.0M) or hunts (*Escape the Seeker*,
  2.9M) the rest. Reference support for the `wars` *1 red vs 100 blue, conversion on touch* idea above.
- `surv` — **Zombie defense, survive 100 rounds** (Carson Jay, 2.0M; 17 titles, median 208K). Marbles defend a castle
  against waves. Overlaps `terr`'s core defence.
- `elim` — **Elimination variants as one-rule episodes** (Crazy Marble Race): *Size Elimination*, *Leader
  Elimination*, *Don't be too fast, the last will lose*, *Elimination Wheel*. Each changes one rule, at 8-13 min.
- `elim` — **Big-field country elimination** (Carson Jay *50 Countries*, 7.9M). Only if countries come off pause, and
  as a many-nation knockout, not a 2-4 nation match.
- `[dead]` — Meme-character races (*Italian Brainrot*, Carson Jay's 2025 burst). A short spike, other people's
  character IP, and the channel went dormant straight after.

## New categories — from adjacent-channel research (2026-09-29, see `reference-channels.md`)

No format slug exists yet for any of these — each is a genuinely different tension mechanism from
the six already in `PROJECTS.md`, not a variant of one. Promote to `formats/` only once one is
actually being built, per this file's own rule.

- **Object-vs-object stress test** (working name `impact`) — drop/press/cut one thing into another,
  single dramatic collision resolved in 15–40s, no escalation loop, no win condition. Best-performing
  shape across three of the seven adjacent channels studied (`Kawaken_3DCG`, `RenderZen`, `Oyen_3D`).
  ~~Cheapest of the three new categories to prototype~~. **Prototyped 2026-09-29, and not cheap:**
  plain rigid scatter settles in 3-4s. The reference versions last because soft body, fluid or
  fracture *unfolds*. The prototype's wave rebuild became the `surv` format (*Wall vs Ball*,
  `formats/survival-wall.md`). `impact` itself is back to an idea; see `formats/impact.md` for
  what a real build would need.
- **Melody-run** — the marble run's entire premise is playing a recognizable tune as it descends,
  not audio as a retention layer bolted onto another premise (`MusicMarble3D`, 973K subs, 1M–5M+ per
  video, videos only 9–11s long). Overlaps `bounce-audio-sequencer` conceptually but inverts the
  priority — track/melody choice drives the layout, not the other way round. Licensing risk if using
  a recognizable tune; public-domain/original only, same rule as `esc`.
- **Generative curiosity** (working name `evolve`) — a system evolves under its own rule with no win
  condition and no explicit threshold; hook is a curiosity-gap title ("what does this become?"), not
  competition or escalation (`CodeCraftedPhysics`, 467K subs, best Shorts 650K–1.08M views). Reuses
  `Core`'s determinism directly, but the creative cost shifts to designing a visually interesting
  *rule* each time — harder to templatize than `esc`'s single-number tuning table.

## From the 2D reference pass (2026-10-03, see `reference-channels.md` § 2D pass)

- `esc` — `[built]` esc-003 with *Baa Baa Black Sheep*, 2026-10-03. **Name the song**: same scene, the bounce melody plays a public-domain piece (Für Elise,
  Hall of the Mountain King, Ode to Joy, Canon in D). Near config-only. Era of mega-hits (35M+), now ~100K
- `esc` — **Reach the core** (inverted escape): ball starts outside nested rotating rings and must break
  or slip inward to the center. Reuses `CircularBoundary2D` + rotating gap. CodeCrafted #1 at 25.7M
- `rom` — **Crusher × multiply**: every ball the press/saw crushes splits into 3, counter climbs to a
  huge number. Needs pooling + hard cap, which `rom` needs anyway. 1.9M / 1.7M / 725K references
- `rom` — **1 → quintillion in 60 bounces**: doubling-per-bounce counter, bodies capped and the
  number carries the climb. 456K / 367K references
- `esc` — **Polygon loses a side every bounce** (FuncFlow 11M, 2024-era evidence, may be stale)
- `esc` — **Multi-ball race to escape** (already listed above). CodeCrafted *"How did the 4th ball even miss it?"* 16.1M
- **Element vs element** (fire/water/lava, "the LAST flame"). Highest S2DS bucket median, but needs a
  2D particle/fluid layer. Its own format slug if built
- `esc` — **A 30-45s cut** of an existing `esc` config, to test length against the 2:05 originals

## Experiment slate (2026-10-13) — fills the experiment lane in `channel-strategy.md` → *Shorts mix*

Ranked. Each one tests a single new thing, and the reuse notes are what keep it cheap.

1. **Multiply vs Escape** (new, no reference) `[built]` mve-001, 2026-10-13, slug `mve`. Its arena shipped as `royale-001` `[published]` 2026-10-21 (1,233 at day 3). One ball sits in the rotating-gap circle. **Every wall bounce
   clones it**, and any ball through the gap is gone for good. The question: *will the circle fill up before
   they all get out?* Two live counters (INSIDE / ESCAPED), and the swarm sweeps the gap like a tide. Tests:
   **a new mechanic on the proven threshold shape** (the shape stays, only the mechanic changes). Reuse:
   `CircularBoundary2D` + rotating gap, `BodyPool2D` hard cap (the count carries the number past the body cap,
   as in `rom`), `CountFormat`, bounce audio. Needs: a new scene and its own small simulation. It's *not* a
   flag on `esc` (see CLAUDE.md on flags). How it loses: the inside count hits a cap ("the circle is full").
2. **Reach the Core** (reference-backed, CodeCrafted #1 at 25.7M). This is `brk` inverted: the ball starts
   outside nested rotating rings and has to break its way *in* to the centre. Tests: an inverted direction on a
   proven arena. Reuse: `SegmentedRing2D`, `ShatterBurst2D`, song mode. How it loses: a clock or a hit budget.
3. **Glass Gauntlet** (3D, studio look). One chrome ball rolls through a corridor of 50 glass panes, each one
   a bit thicker. *How many can it break?* Tests: whether 3D glass and the shatter sound can pull the band in
   a threshold shape (v5 was 3D threshold, but without the studio look). Reuse: studio-look kit, `CollisionDetector3D`,
   procedural glass break, slow-mo director, camera rig. How it loses: the ball stops.
4. **Bell curve drop** (maths, added 10-18). 1,000 marbles through a peg board pile into a bell curve. *Guess the
   shape.* Tests: whether the maths shape still lifts a peg board, the genre's most familiar look. Reuse: `rom` peg
   fields, `BodyPool2D`, `TriggerZone2D` slot counters. Brief: `math-convergence.md` #7. **10-24:** promoted to next
   maths pick, but give it a twist round (e.g. a rigged / tilted board that skews the curve), per the *10-24 read*.
5. **Brachistochrone race** (maths, added 10-18) `[published]` brach-001, 2026-10-18 (1,242, in the band). Straight ramp vs steep dive vs curve, and the longest path wins.
   Tests: maths inside a contest. Reuse: the `Shared` track course. Brief: `math-convergence.md` #11.

6. **Split Press** (added 10-20, reference-backed) `[published]` press-001, 2026-10-20 (1,425 at day 4, in the band). A press lowers onto a ball; every crushed
   ball splits in 2, and each ball holds 1 ton. *How many balls before the press stops?*, with the press 2× stronger
   each round. Tests: the @satisfying2dsims multiply + crusher shape (*"Every ball crushed splits into 3"* 1.7M,
   *"crushed one ball into 15,939 pieces"* 1.9M) on our `clack` answer-ladder. Brief: `formats/hydraulic-press.md`.

## From MathCa + Bouncy Physics (2026-10-14, see `reference-channels.md`)

- **Math inevitability / convergence** (`conv`) `[published]` conv-002, 2026-10-15 (2,607 at day 3) and conv-003 parabola, 2026-10-23 (1,321 at day 1). **conv-003 v2: tilt round** (tilt the bowl, the focus smears into a caustic, tilt it back), if day 3 shows the pure escalation stalled. Sibling `clack` `[published]` clack-001, 2026-10-17 (2,694 in <18h). **Maths is the strongest Shorts shape so far** (`learnings.md` → *10-18 update*); next topics live in `formats/math-convergence.md` → *Wider maths backlog*. Many identical glowing bodies
  start at different points, and a known mathematical fact makes them meet, sync or close up: cycloid
  (tautochrone) balls arriving together, ellipse-focus rebounds, a pendulum wave lining up, the golden-angle
  spiral. Title shape: **"Will they all ___?"**. Reference: MathCa, 12.2M / 902K / 474K in its first 2 weeks,
  1.2-1.9% likes (highest of any channel pulled). Its explainer-only topics flopped. Cheap: deterministic and
  analytic, 30s, no tuning table. Good fit for the experiment lane. Promote to `formats/` once building.
- **Hidden-shape reveal** (an `esc`/`evolve` hybrid). The escalation draws or carves a picture and the viewer
  guesses what it is ("Ball SHRINKS, wall GROWS. Can you guess the SHAPE?"). Reference: Bouncy Physics,
  25.5K / 18.2K, a weak signal from a 128-sub channel.
- `brk` — **themed target**: break a heart / star / egg outline instead of plain rings. Config-level. Bouncy
  Physics, 3-4.6K each (weak).

## Experiments / untested

- Viewer-voted path: pinned comment decides the next video's gate layout
- Physics "characters" with distinct properties (bouncy, heavy, slippery) rather than identical marbles
- Cross-format: a race that becomes a war when contenders collide
- Live premiere with chat picking sides
- Ragdoll/soft-body variant instead of marbles
- Domino / chain-reaction format (satisfying, but is there a winner? needs stakes)
- Plinko-style board with real prize values
- Marble racing on procedurally generated tracks from a seed shown on screen
