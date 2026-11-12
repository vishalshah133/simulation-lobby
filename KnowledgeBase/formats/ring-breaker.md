# Format brief: `brk` — Ring Breaker

**Tension type:** threshold (*will it break out?*), like `esc`, but with progress you can see
ring by ring.
**First video:** [brk-001](../../plans/videos/brk-001-ring-breaker-fur-elise.md).

## The shape

A ball inside nested rings made of segments. Touches break segments, holes let the ball out, and
each ring explodes when the ball escapes it. The ball speeds up per ring; the outer rings take
more hits.

## Why it works (genre evidence, 2026-10-04)

- CodeCraftedPhysics: *"Can it break the whole spiral?"* 10.7M, *"When it finally reached the
  center"* 25.7M (the inverse shape). satisfying2dsims: *"Can Pac-Man reach the core?"* 670K.
  See `reference-channels.md`.
- **Visible progress** (5 → 4 → 3 rings left) gives a long run a structure, which the 2D genre's
  60–70s hits rely on (`channel-strategy.md` → *Length*).
- **Every touch is an event** (a note plus a crack or a shatter), so there's no dead time.

## Design rules

- **Holes only from physics.** A segment breaks because it was touched; the ball leaves only
  through an opening that exists. The sim fails a seed that violates this.
- **Instant hook:** the innermost ring breaks on the first touch.
- **Never launch from dead centre:** the ball would bounce along one diameter forever.
- **One-segment hole must fit the ball** in every ring (`BreakerConfig.Validate`), or escape needs
  two adjacent breaks, which is a much slower game.

## Variants to try

- Song per video (the song-series route): one melody asset each.
- Ring count / spin speed as the one-knob change.
- "Guess how many hits" instead of a song (comments + a different hook).
- Two balls racing to break out (competitive tension), which needs a second ball and a per-ball score.

## Learned

- **2026-10-04: it needs a way to lose.** Without one the ball always breaks free eventually, and
  the hook question has a guaranteed answer (user's catch). First scan: 188/200 finished, the rest
  only timed out on the safety cap. Fixed with a 60s clock (`timeLimitSeconds`).
- **2026-10-04: the Python pacing model was 2.7× optimistic** (46s modelled vs 123s measured).
  Tune from real scan numbers, not the model.
- **2026-10-04: spin rings with a kinematic Rigidbody2D.** Moving static colliders by transform
  made a 200-seed scan take 38 minutes.

## Wide variant (comp-001 #8 "Three Rings")

16:9 (1920x1080) segment for `comp-001`. Menu: **Simulation Lobby ▸ 2D Formats ▸ Ring Breaker (brk) ▸ Wide: Three Rings (comp-001)**
-> `Assets/Scenes/Comp001_08_ThreeRings_Wide.unity`, config `Assets/Settings/Configs/BreakerConfig_Comp001_08.asset`,
card `Assets/Settings/SegmentCards/comp001_08.asset`. Sim code untouched; new files `BreakerWideHud.cs` (runtime) and
`Editor/BreakerWideSceneBuilder.cs`; the Breaker asmdef gained `UnityEngine.UI` + `Unity.TextMeshPro` references.

- **Variation:** 3 fat rings (r 1.7/2.7/3.7, thickness 0.42, 10/12/14 segments), hit points 3/4/4, ball 7.0 u/s +1.2 per ring,
  spin 20 deg/s, **45 s clock**, cyan ball on navy, gold rings, *In the Hall of the Mountain King* (one note per touch).
- **Real loss state:** clock at 0 with a ring up -> sim freezes ball and rings, caption `TIME UP! RING n OF 3 IS STILL STANDING`.
- **HUD:** hook top-left, `RINGS BROKEN n/3` in the counter slot, big `TIME LEFT m:ss` plate with a draining bar under it
  (red under 10 s), caption bottom. The run starts after the 1.5 s title card so the clock does not burn under it.
- **Outcome UNVERIFIED.** Tuned from a rough billiard model (median ~65 s for 3/4/4, discounted ~1.4x because the model ran
  slower than the real brk-001), not from Unity. Run several takes. Tune in the config: `innerHitPoints`/`outerHitPoints`,
  `startSpeed`/`speedPerRing`, `innerSegments`/`extraSegmentsPerRing` (rebuild after segment changes), `timeLimitSeconds`.
  Too many wins -> raise hit points or lower speed; too many losses -> the reverse.
