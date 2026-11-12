# coast-001 — Haunted Coaster: Who Survives the Ride?

**Format:** `coast` · **Length:** ~425 s (7:05; band 6.5-8 min; rail 4,751 m, avg 11.5 m/s, max 30) · **Aspect:** 16:9 · **Status:** `built, not yet run`
**Brief:** `KnowledgeBase/formats/horror-coaster.md`

## Premise

Six riders board a Halloween coaster at midnight. Every scare takes one. Who is still in the cart when it reaches the station?

Title direction: *Haunted Roller Coaster at Midnight: Only ONE Rider Survives (Halloween POV)*. Final copy at publish time.

## Storyboard (write before any code)

| # | Time | Act / place | On screen | Scare / beat | RIDERS |
|---|---|---|---|---|---|
| 0 | 0:00 | Warning card | Photosensitivity + "headphones recommended" card, 3 s | | |
| 1 | 0:03 | Station | POV in the last car. **5 riders ahead plus you = 6.** Skeleton operator pulls a lever, lap bar glows orange | Hook caption: **"6 RIDERS. EVERY SCARE TAKES ONE."** | 6 |
| 2 | 0:20 | Chain lift | Clank-clank climb above a pumpkin-lit town, heartbeat rises | Fake-out: a bat swoops, nothing happens | 6 |
| 3 | 0:45 | **Trick or Treat Street** | Level run along decorated houses. Each porch door swings open as the cart passes, a costumed kid holds a bucket. Caption flashes **TREAT?** or **TRICK?** | Doors 1-2 **TREAT**: candy shower into the cart (warm, safe). Door 3 **TRICK**: screaming ghoul, **Scare 1**, rider taken | 5 |
| 4 | 1:30 | Graveyard | Drop into mist, crooked stones, lanterns | **Scare 2:** hand bursts from a grave, rider taken | 4 |
| 5 | 2:20 | Graveyard, deeper | A lantern goes out | Fake-out, then **Scare 3 (no rider):** ghost crosses the track at eye level | 4 |
| 6 | 3:00 | Dead forest | Tight banked curves, eyes in the trees | **Scare 4:** crow flock explodes up, rider taken | 3 |
| 7 | 3:50 | Tunnel of faces | Carved pumpkin faces light up in sequence ahead | **Scare 5:** the last face is *behind* the cart, rider taken | 2 |
| 8 | 4:40 | **Midpoint: the Trip** | Rails twist into a spiral, sky inverts, kaleidoscope pumpkins, colours cycle, camera rolls through a loop. Trick-or-treat candy swirls in the air | No scare for ~50 s. Music box slows and detunes | 2 |
| 9 | 5:40 | Haunted mansion | Cart bursts through the front doors, portraits' eyes follow, chandelier drops | **Scare 6:** painting lunges out, rider taken | 1 |
| 10 | 6:30 | Asylum corridor | Long straight, flickering lights, wheelchair rolls into the rail | Fake-out x2, **Scare 7 (no rider):** figure beside the cart | 1 |
| 11 | 7:30 | Final climb | Slow lift into the giant pumpkin's mouth. Only the seat ahead is empty. Silence | Fake-out: nothing | 1 |
| 12 | 8:10 | Final drop | Drop through the pumpkin, white-out (single flash, then fade) | **Scare 8 (the big one):** the figure in the pumpkin turns, a skull in your own lap bar's reflection | 1 |
| 13 | 8:35 | Station | Cart pulls in. Operator: "Welcome back." Camera pans down: your hands are bone | End card: **"You were the sixth rider."** | 1 |

Counter rule: `RIDERS` starts at 6 (five visible ahead plus the viewer). Scares 1, 2, 4, 5, 6 take a rider; scares 3, 7, 8 do not.
The viewer is the last survivor, then the reveal says they were already gone.

Read as a cold viewer, sound off:
- [x] The hook names things visible from frame one (six riders, a coaster, a counter).
- [ ] Every rider's disappearance happens on screen with 0.3 s+ of build (verify in capture at 480 px).
- [x] Every beat either changes the count or raises the stakes; the Trip is the one exception on purpose (a breather and the shareable moment).
- [x] The only number shown is `RIDERS n`.
- [x] The ending lands the *why* in one line ("You were the sixth rider").

**Trim rule:** if the first Play runs long, cut scene 9 (Asylum) first; acts 1-8 and 10-12 carry the story.

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md)
- [ ] **REUSE** capture (16:9 long-form profile, offline frame-stepped; see [capture-shorts](../blocks/capture-shorts.md) for the aspect switcher)
- [ ] **REUSE** `ProceduralToneBank`, `MelodySequence`, `AmbientDriftField`, `SlowMotionDirector`
- [ ] **BUILD** `coaster-rail` (Shared candidate): spline rail + banking + arc length + sweep into rail/tie/support meshes. Do NOT reuse `TrackMeshBuilder` (it sweeps a marble floor, not a coaster).
- [ ] **BUILD** `ride-camera` (Presentation): POV pose from rail frame, sway from the seeded wobble, springs per CLAUDE.md *Camera movement*
- [ ] **BUILD** `night-horror-look` (new block, replaces `studio-look-3d` for this video: fog, emissive lanterns, volume profile)
- [ ] **BUILD** `scare-director` (Presentation): reads `ScareSchedule`, drives entities, stings, post FX. Read-only on the sim.

## Video-specific work

### Scene
`Coaster_Haunted_3D`. One continuous rail, ~1.2 km, built from keyframed control points per act (data on the layout asset).
Each act is a prefab-free procedural set (primitives plus emissive materials; no model imports). Baseline per CLAUDE.md:
ground, key + fill + rim, linear fog (the depth cue), plus per-act lantern clusters. Backdrop near-black blue, not pure black.

### Simulation / rules
- `CoasterDynamics`: speed from `v = sqrt(v0² + 2g(h0 − h))` with drag, chain lift at constant speed, brake zones, fixed timestep.
- `ScareSchedule`: list of `{distance, kind, riderId, preQuietSeconds, cue}`. Scare *fires when the cart reaches the distance*,
  so a change in speed shifts the time but not the order.
- `RiderRoster`: six silhouettes in lit seats, one removed per scare (a seeded order, except the last two, which are fixed for the ending).
- Seed drives wobble, wind gusts, which fake-outs fire, rider order. Same seed → same ride.

### Config + tuning targets

| Knob | Start at | Note |
|---|---|---|
| `liftSpeed` | 3.5 m/s | clank rate follows |
| `maxSpeed` | 28 m/s | cap after drops, keeps camera readable |
| `scarePreQuiet` | 2.0 s | master mix drops 12 dB, heartbeat only |
| `stingPeak` | -3 dBFS | hard limit |
| `fovBase / fovBoost` | 75 / +12 on drops | |
| `tripDuration` | 50 s | |
| `flashRateMax` | 3 Hz | safety rule |

### Look
- **Palette:** pumpkin orange (#FF7A18), bone (#EDE6D0), toxic green (#7CFF4F), one blood red for scare 7, on deep indigo. Orange lantern pools make the readable islands.
- **Trip act:** a single full-screen post pass (`FearPostFx`): hue cycle, chromatic aberration, lens distortion, radial kaleidoscope. Intensity capped. Everything else uses gentle vignette plus film grain rising with the heartbeat.
- **Riders** are readable at 480 px: head + shoulders silhouette with an orange lap-bar rim light.

### Audio (all procedural)
Heartbeat (rate rises toward each scare), chain clank, wheel rumble and wind scaled to speed, creaks, noise whispers, a detuning music-box *Dies Irae*,
stings as layered noise bursts + a low thud (`CreateImpactThud`, `CreateWhoosh`). Silence is a track: author the quiet gaps in the schedule.

## Acceptance criteria

- [ ] All block acceptance criteria met
- [ ] Same seed twice → identical ride and identical scare times
- [ ] Every scare entity is visible ≥0.3 s before its sting; checked at 480 px wide
- [ ] No flash above `flashRateMax`; no full-frame white flash longer than 2 frames outside the final white-out
- [ ] Ride camera passes the smoothness harness (0 pops, turn rate and jerk within CLAUDE.md limits, scaled for a 28 m/s POV)
- [ ] Length 8-10 min; peak sting not clipping on the mix
- [ ] 30+ fps preview at 1920x1080 on the dev machine

## Seed selection

No scan (house rule). Log the seed on first Play, keep the take if it passes acceptance.

## Render + publish

1920x1080 @ 60, offline frame-stepped. Thumbnail: the last rider's skull face in an orange lantern pool, six-seat cart behind, text `1 SURVIVES`.
Music: procedural. Pinned comment: "Which scare got you? Be honest 👇". Remind: record the logged seed here.
Publish metadata follows CLAUDE.md long-form rules (no hashtags in title, chapters from the table above).

## Retro

Write `KnowledgeBase/retros/YYYY-MM-DD-coast-001-haunted-coaster.md`, update `/PROJECTS.md`.
The open question this video answers: does a themed, story-led ride (no marbles) pull the long-form reach the marble formats haven't?

## Rework 2026-10-31 (after first Play)

User feedback: POV blocked by the cart, nothing scary, ride too slow. Fixes: riders moved to the left lane with low seat backs and the eye in the right seat; ride rebuilt from 540 s at ~3.7 m/s to 425 s at ~11.5 m/s;
8 big scares rewritten as hard jump scares (lunge to ~1 m in <= 0.2 s, own light, sting on frame 0) plus 21 micro scares (spider drop, window face, skeleton pop, claw hand, bat swarm, coffin, shadow rush, mirror face).
Act starts (s): Station 0, ChainLift 8.8, Street 28.3, Graveyard 78.4, DeadForest 134.1, Tunnel 175.5, Trip 215.4, Mansion 264.5, Asylum 312.0, FinalClimb 354.6, FinalDrop 374.4, StationReturn 402.8.
Review keys in Play: N next scare (jumps to 5 s before), P previous, B start.

## Rework 2026-11-01 (scare variety + real sound)

Feedback: jump scares repetitive (everything lunged at the lens), sound more funny than scary. Fixes:
- **Five signature scares, each a different shape of fear** (`SignatureScareSet` + `SignatureScareBuilder`): `SprinterLeap` (beast seen 90 m away, gallops, leaps onto the lens; Asylum 35.5 s),
  `SideLeap` (ghoul on a pillar drops across the cart a hand from the lens; Graveyard 50 s, replaces the ghost), `SnakeStrike` (cobra rises beside a car, strikes and swallows a rider; Graveyard 22 s, replaces the grave hand, TAKES),
  `SpikeWall` (wall fires spikes through a car, then a second volley stops 0.4 m short of the viewer; Tunnel 24 s, replaces the tunnel face, TAKES), `BatFlood` (pumpkin mouth floods bats; FinalClimb 19 s).
  Takes stay at 5 (RIDERS 6 to 1): snake and spikes replaced two taking scares, the seat order logic only has 5 slots.
- Micro scares cut from 21 to 8 (one or two of each kind, none within the new events' windows). Timing test now expects 6-12 micros, each kind >= 1.
- **Sound:** CC0 recordings in `Assets/Resources/CoasterSfx/` (copied from `Assets/Audio/Spooky` and `Assets/Audio/Sourced/Openverse`) now carry screams, roars, hisses, stabs, bat screech, whispers, wolf and bells, plus the `omen_bed` loop. Synth is kept for beds, sting sub-boom and fallback.
  Sources (all CC0, Freesound via Openverse): snake hissing (JesterWhoo, schreibsel), Scary Monster Roar #2 (NicknameLarry), Monster Roar 02 (zerokingfull), Deep Monster Growl (uagadugu), Monster Screech (thegoose09), Monster screech (Khrinx), Sword stab 3 (ihitokage), Sword Impact (TheBuilder15), Bat Screech (Yoyodaman234), bat-wing umbrella (Scottrex05), Female scream 02 (missozzy).
- Not yet run in Unity: rebuild the scene (Simulation Lobby menu, coast builder) so the entities and the new schedule exist, then review with N/P keys.

## Rework 2026-11-01 (b): recorded-only scares, mood layer, title card, slow motion
- **Jump-scare sound is recordings only.** Stings, screams, micro-scare lead-ins, whispers, door groan, treat-door bells, the taken rider and the far screams all come from `Assets/Resources/CoasterSfx` (CC0). The synthesised sting/scream/cue/door/candy/whisper/far-scream clips and their generation jobs were removed from `CoasterAudio`. Procedural audio remains only for the ride's own beds (heartbeat, wind, rumble, chain, creaks, drone, siren, music box). Every real scare now carries a low "impact" boom pitched down under its own voice, so the loudest moment is always the sting frame.
- **Slow motion only after the loud hit.** `ScareSlowMo` now covers every real scare (big 0.5-1.55 s holds, micro scares a shallow 0.38 s hold), all starting >= 0.28 s after the sting frame. While it holds: the recorded voices still ringing drop in pitch (`CoasterAudio.ApplySlowMoPitch`), `FearPostFx` darkens, desaturates and fringes, and the camera pushes in 7 degrees of FOV. All three read `ScareSlowMo.Current` and write presentation state only.
- **Mood layer** (`AtmosphereBuilder`): candle clusters with wax pools, floating candles, jack-o-lanterns in piles and on posts, a cart-following ground mist and rail haze, denser fog (start 3 m, end 95 m). ChainLift, Street, Graveyard, DeadForest and Mansion only; chunk-culled; a few clusters carry flickering lights.
- **Scare look:** wounds, wet blood, ichor, cataracts, tufted fur, crest spines, torn cloth, rust runs and blood pools on the signature scares; hollow-cheeked faces with ichor tears, hair and veins on the micro faces; cracked skull on the skeleton; bristles on the spider; each creature lights its own face (small coloured point light).
- **Title card:** "HAUNTED COASTER" (Creepster, blood red, candle flicker) and "ENTER AT YOUR OWN RISK" (Nosifer) above the existing warning text; the card now holds 5.5 s (hook caption starts at 5.5 s). TRICK?/TREAT? cue, RIDERS counter and end card use Creepster. Fonts: OFL TTFs in `Assets/Resources/CoasterFonts`, turned into TMP assets in memory at startup by `CoasterHud.HorrorFace` (editor-saved font assets rendered as garbled overlapping glyphs, so there are none).
- **Not yet run in Unity.** Let Unity recompile (this creates the font assets), rebuild the scene (coast builder) so the atmosphere, new details and HUD title exist, then review with N/P. Check candle/pumpkin placement against the track at a few acts, and that the 5.5 s title card is not too long for the video's pacing.
