# Format brief: `coast` — Haunted Coaster

**Tension type:** survival by attrition (*who is still in the cart at the end?*), carried by a ride you can't leave.
**Origin:** Halloween 2026-10-31. Backlog: "Survival roller coaster" (MIKAN, 115M) and *Abstract World* coaster (14.5M).
**First video:** [coast-001](../../plans/videos/coast-001-haunted-coaster.md). Status: `planned`.

## The shape

First-person ride in the last car of a night-time Halloween coaster. Six riders sit in the cars ahead, lit by the lap-bar
glow, so the viewer always sees who is still aboard. Real coaster physics: chain lift, then speed from height lost
(energy conservation plus drag), brake runs, a seeded wobble. At fixed distances along the rail a **scare** fires: the
ride goes quiet, something happens, the sting hits, and a rider is gone. The counter is `RIDERS 6 → 0`, with the last
rider left at the finish being the answer.

## Why it should work

- A cold viewer understands "six people, something takes one each scare" in 2 s, sound off.
- Coaster POV is the genre's biggest title shape; horror gives it a stake the marble versions lack.
- Trippy act = shareable moment, the clip people post.

## Design rules

- **Quiet before loud.** Every jump scare is preceded by 1.5-3 s of near silence and a cue the eye can catch (a lantern goes
  out, a rider turns). The sting is the loudest thing in the video, so master it, don't let it clip.
- **At most one scare per ~40 s, and fake-outs count.** Two real scares back to back reads as noise. Fake-outs (nothing
  happens) are what make the next one land.
- **Every cause on screen.** The rider is taken by something the viewer can see for at least 0.3 s before it hits.
- **Readable at 480 px.** Scare entities are bright against dark: pumpkin orange, bone white, sickly green, one blood-red.
  Dark-on-dark is invisible on a phone.
- **Flash safety.** The trippy act and the stings must not strobe faster than 3 Hz or flash full-frame white. No photosensitive
  triggers; cap flash luminance, and put a one-line warning card at the start.
- **Deterministic.** Ride speed, scare timing, wobble and which rider goes first come from the seed. Scare *distances* are data.
- **Scare sound is recorded, never generated.** Stings, screams, whispers and micro-scare cues are CC0 recordings (`Assets/Resources/CoasterSfx`).
  Procedural audio is kept for the ride's beds only: heartbeat, chain clank, wind, rumble, creaks, drone, music-box Dies Irae.
- **Slow motion follows the loud hit**, never precedes it: every window starts >= 0.28 s after the sting frame.

## Code map (planned)

`Assets/Scripts/Simulations/HorrorCoaster/`: `HorrorCoasterConfig`, `HorrorCoasterSimulation` (ISimulation, `FormatSlug` coast),
`CoasterRail` (spline, arc length, banking), `CoasterDynamics` (pure class, energy model), `ScareSchedule`,
`Editor/HorrorCoasterSceneBuilder`. Presentation: `RideCameraRig`, `FearPostFx` (volume driver), `ScareDirector`, `RiderRoster`.
Reuses `ProceduralToneBank`, `MelodySequence`, `AmbientDriftField`, `SlowMotionDirector`. Anything a second format needs
(rail sweep, ride camera) gets promoted into `Shared` then, not copied.

## Open questions (fill in after the first Plays)

- Do the jump scares land on a video you watch on a phone, or only with headphones?
- Does the `RIDERS` counter hold attention through the trippy act, or does the act need its own mini-stake?
- Does a ride with no marbles fit the channel, or is it a one-off? (Judge by retention in Studio, not like-rate.)
