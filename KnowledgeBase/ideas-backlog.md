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
- `race` — 100 marbles, 3D banked mega-track, full leaderboard
- `elim` — Season: recurring named contenders with a standings table across videos
- `wars` — Conversion vs destruction A/B, same seed, side by side
- `surv` — Escalating hazard gauntlet, 15 min, survivor counter throughout

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

## Experiments / untested

- Viewer-voted path: pinned comment decides the next video's gate layout
- Physics "characters" with distinct properties (bouncy, heavy, slippery) rather than identical marbles
- Cross-format: a race that becomes a war when contenders collide
- Live premiere with chat picking sides
- Ragdoll/soft-body variant instead of marbles
- Domino / chain-reaction format (satisfying, but is there a winner? needs stakes)
- Plinko-style board with real prize values
- Marble racing on procedurally generated tracks from a seed shown on screen
