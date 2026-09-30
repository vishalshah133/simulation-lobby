# Format: Wall Survival — "Wall vs Ball" (`surv`)

One wall of blocks takes hit after hit from an escalating ball until nothing is left standing. The
hook is a **guess**: *how many hits will it take to destroy all 300 blocks?* The hit count at the
moment the last block falls is the answer, and the whole video builds toward revealing it.

This is the first `surv` build. It sits under the survival family in
[elimination-and-survival.md](elimination-and-survival.md) — something endures an escalating attack,
a survivor counter is on screen throughout, and the end is given room — with one wall of many blocks
standing in for "many contenders".

> **Origin, 2026-09-30.** This code was built as `impact` (see [impact.md](impact.md)): first as a
> single launch, then as nine waves with the survivors *reformed* into a smaller wall between waves.
> The wave build turned out not to be an `impact` video at all. The reference channels' `impact`
> shape is one dramatic event resolved fast, and a wall surviving repeated escalating hits is
> survival. So it was re-slotted as `surv`, and two things changed to fit: the wall is **never
> rebuilt**, and the run is **open-ended**, with no fixed wave count.

## The hook: a guess, not a yes/no

`esc` asks *will it happen?* (threshold). `race` asks *who wins?* (competitive). This asks **how
many?**, a prediction the viewer commits to in the first three seconds and then checks against a
live tally. It's the same hook as the channel's first-ever upload (*"How many balls will fit?"*,
2025), though that video's numbers are too small to prove anything.

What the guess needs to work:

- **A guessing window.** `openingHoldSeconds` (3s) holds the intact wall with the question on screen
  and "LOCK IN YOUR GUESS" before anything moves.
- **A live tally with no total.** The HUD round line reads `HITS 7`, never `HIT 7 / 18`. A total
  would give the answer away. That's why `VersusScoreboardHud.SetRoundLabel` exists alongside
  `SetRound`.
- **An honest count.** A clean miss (the ball touched nothing) **does not count as a hit.** It
  still escalates the ball, and the HUD shows `MISSED`. Counting a shot that touched nothing would
  make the answer dishonest, and in a guessing format viewers notice.
- **A verifiable answer.** `unseededAim` defaults to **off** here, unlike impact-002. The number
  being revealed is the whole payoff, so the take has to be reproducible from its seed. If the
  answer can't be re-derived, the video's claim can't be checked or re-rendered.
- **A reveal beat.** Which hit is last can't be known until it lands. So the reveal slow-mo keys
  off the *outcome*: `WallSurvivalSimulation.WipeoutUnderway` goes true the tick every remaining
  block is past the elimination line. The presenter then holds `wipeoutSlowMotionSeconds` of
  slow-mo, and the final hold shows `DESTROYED IN N HITS`.

## Why the wall is never rebuilt

The wave build packed survivors back into a fresh, smaller grid before every wave. It had a reason
(a shrinking wall told the story), but for a guessing format it's wrong:

- **The evidence has to stay on screen.** Viewers revise their guess as the run goes. A wall with
  real holes, chewed edges and knocked-askew blocks lets them read the damage. A wall that
  teleports back into a tidy rectangle erases it every 8 seconds.
- **Reforming looks like the game cheating.** Blocks visibly scattered, then snapped back into
  formation, is the exact failure the elimination-threshold note below warns about. Reforming did
  it deliberately, every wave.
- **Damage accumulates honestly.** Elimination is measured from each block's *original* slot
  (`_homePositions`, never updated). A block nudged half a spacing by one hit and most of a spacing
  by the next is out. That matches what the eye sees: it's no longer where the wall was.

Between hits, survivors are frozen where they came to rest (velocity cleared *while still dynamic*,
since Unity rejects velocity writes on kinematic bodies, then set kinematic) so the hold beat is
dead still.

## Aiming: the densest cluster, not the centroid

The wave build aimed at the centroid of the reformed wall. Without a reform, that breaks: the
centroid of a wall full of holes is often *in* a hole, and late in the run, with a dozen scattered
blocks, the centroid can be empty space between them. That gives misses, and misses stall a
guessing format.

Every shot now aims at the **densest patch of standing blocks**, measured across the ball's own
footprint (neighbours within `ballRadius + blockSpacing`), centred on that neighbourhood. O(n²),
about 90k distance checks on a full wall, once per shot, which costs nothing. Ties go to the lowest
index, so it's deterministic. `aimJitterBallRadii` (0.5) nudges it off dead-centre so consecutive
hits don't look identical. Keep it under 1 or late hits start missing single blocks.

## Escalation and length

Open-ended means length is set by how fast the wall falls, so escalation is the length dial.

- The ball grows per hit (`radiusGrowthPerHit` 1.07, `massGrowthPerHit` 1.25, `speedGrowthPerHit`
  1.03): hit 10 is ~1.8× the radius and ~7× the mass of hit 1. **Without growth the ending drags.**
  A sparse, scattered wall loses one or two blocks a hit to a fixed ball, and the last dozen blocks
  would take a dozen hits.
- `maxHits` (25) is a **safety cap, not a structure**. It only stops a run from rolling past the
  3-minute Shorts limit. A take that hits the cap never answers its own question: `WALL SURVIVED N
  HITS` is a valid end card, but reject that take in the seed scan.
- Per-hit beats are shorter than the wave build's (hold 1.0s, result 0.9s vs 1.6s / 2.2s), because
  there are 2-3× as many of them. A beat that was fine nine times is padding at twenty.
- **Target band: 12-22 hits, 90-170s on screen.** Fewer than ~10 and the guess is too easy.
  Past ~25 the middle sags.

**The last stand.** Once `lastStandBlocks` (12) or fewer remain, every hit gets extra slow-mo and
the hold line counts down (`ONLY 7 LEFT`). This is the survival brief's "give the final room" rule.
The end is where the guess is decided, so it earns the time.

## The ASMR and drama layer

Carried over unchanged from the `impact` build. All of it is format-agnostic presentation.

- **Sound is continuous, not just impacts.** `ProceduralToneBank.CreateAmbientBed` provides a looping
  low pad under the whole run, so quiet stretches are room tone rather than dead air, and
  `CreateRiser` provides a rising sweep under each anticipation hold. The riser is generated to the
  hold's exact length, so it peaks on the launch, and its pitch now follows *wall destroyed so far*,
  so the run audibly climbs toward the end.
- **A generated knock needs a mid-band partial or it's inaudible where the video is watched.** A
  55 Hz body tone plus filtered noise reads as *silence* on a phone speaker, which rolls off hard
  below ~200 Hz. Every impact sound carries a ~340 Hz click partial. Check generated audio on a
  laptop speaker, never on headphones alone.
- **Discrete knocks aren't enough for a field of 300 objects.** Only the ball's collisions are
  counted, so block-on-block contact made no sound. A looping noise bed (`CreateRumbleLoop`)
  follows `ScatterEnergy01`, the mean speed of the surviving blocks. Scatter is audible for as long
  as it's visible.
- **The elimination cascade is the payoff sound.** Knocked-out blocks are removed a couple per tick
  (`eliminationsPerTick`), each playing the next note up a pentatonic ladder. **The ladder restarts
  from the bottom every hit**, so each hit's cascade is its own rising run, and a big hit audibly
  climbs higher than a small one. A continuous ladder across 300 blocks would just wrap around and
  lose that.
- **Slow-motion belongs to presentation, not the sim.** `SlowMotionDirector` scales `Time.timeScale`
  only. `Time.fixedDeltaTime` and the tick sequence are untouched, so the run is bit-identical and
  simply takes longer to watch.
- **Camera push-in follows destruction, not hit number.** The hit count is unknown in advance, so
  `Progress01` is the fraction of the wall destroyed, and the camera closes in as the wall falls.
  Shake keys off burst size as well as speed.

## Build notes and failure modes

Learned on the `impact` build; all still apply.

- **Never lay rigidbodies out at exactly their own size.** Blocks packed at `blockSize` have
  touching colliders, and jitter pushes them into overlap. Kinematic, they sit still. The instant
  they go dynamic, Unity shoves them apart and the wall detonates before the ball arrives. Fix:
  `blockGapFraction` air gap, jitter clamped to half of it. Also cap `maxDepenetrationVelocity`
  (~1.5) on every block; the default (10) launches overlapping pairs at speeds nothing imparted.
  **A first-hit wipeout is this bug until proven otherwise.**
- **Settle gating belongs on the outcome, not the trigger.** Gating settle tracking on *launch*
  isn't enough, because the wall is motionless for the whole flight and the tracker ends the hit
  before contact. Gate on the ball being **spent**: it hit something and slowed, or it missed and
  flew out the back. The miss case matters even more here, because a miss must still end its shot.
- **Every hit needs a flight cap** (`maxHitFlightSeconds`), or one block drifting forever stalls the
  video.
- **The elimination threshold has to match what the eye calls destroyed**, and the eye is strict.
  Measured in block spacings. The first playback at 1.25 spacings looked wrong: visibly shoved
  blocks kept counting as standing, and 0.5 still felt loose. Now **0.2 spacings**
  (`eliminationDistanceBlocks`): any block visibly out of line counts. Placement jitter is baked
  into each block's home slot, so it never counts. The real floor is blocks merely brushed by a
  neighbour. A
  **tilt test** (`eliminationTiltDegrees` 25°) catches blocks knocked crooked but still in their
  slot, which read as broken and which a distance test alone never flags.
- **Orphans count as destroyed.** Without a rebuild, hits leave single blocks floating alone in
  holes, and on playback those read as leftovers, not wall. A block with fewer than
  `minNeighboursToStand` (1) standing neighbours is destroyed. The 8 surrounding slots count,
  diagonals included, and a neighbour already flagged holds nothing up. It's checked during flight
  too, so the reveal slow-mo still fires when the final hit strands the last few. Raise it to 2 if
  loose pairs or dangling strands look wrong too.
- **Frame for full push-in, at 9:16.** The camera views the wall from the right, so the wall's
  near end lands on the *left* of the frame. The first build put it just outside a 9:16 frame, and
  the push-in carried it further out. The scene builder now solves the framing itself
  (`FrameShot`): it centres the wall plus the launch point, then pulls back until they fit with a
  7% margin at full push-in. Don't hand-place the camera again; change the margin instead.
- **Blocks float (gravity off).** That's deliberate: it keeps the wall a readable wall rather than a
  collapsing pile, and damping (`blockLinearDamping` 1.5) is the only thing bleeding off energy.
  Gravity would be a different, messier video, and would make "destroyed" ambiguous.
- **3D determinism is still unverified.** `determinism-core` has only been proven against
  `Physics2D`. Same seed twice → same hit count is this format's first real acceptance check, and
  it matters more here than it did for `impact`, because the number *is* the content.

## What makes a good run

- **The answer surprises.** The first hits punch big holes, so viewers guess low. The scattered
  late game takes longer than it looks. A seed where the last few blocks survive two or three
  extra hits is the ideal take. Scan for it.
- **No first-hit wipeout, no cap-out.** Both leave the guess with no content.
- **Hits land somewhere visibly different each time.** Densest-cluster aiming plus jitter does this.
  If three hits in a row go to the same spot, raise jitter slightly.

## Title / hook patterns

State the question with the number: *"How Many Hits to Destroy 300 Blocks?"*, *"Guess How Many Hits
This Wall Survives"*. The thumbnail is the intact wall, the ball, and a big "?". The pinned comment
asks for guesses before the reveal. The comments are where this format earns its keep.

## Open questions

- Does the guess hook retain better than `esc`'s yes/no? This is the first chance to compare two
  tension types on this channel, but only once both have day-7 snapshots.
- What's the right block count? 300 is a good title number. More blocks means a longer run and a
  harder guess, but more colliders. Check performance before going past ~500.
- Variants to try one knob at a time: ball never grows (pure attrition), a thicker wall
  (`rows`/depth), two walls (`WALL A vs WALL B`, guess which falls first).
