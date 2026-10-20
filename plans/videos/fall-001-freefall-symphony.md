# fall-001 — Freefall Symphony

**Format:** `fall` (3D) · **Length:** ~10 min (7 worlds × ~75-85 s + transitions) · **Aspect:** 16:9 1920x1080 · **Status:** `built` 2026-10-20 (compiles in Unity, 0 errors/warnings; harness-checked; never run)
**Brief:** `KnowledgeBase/formats/freefall-symphony.md`
**Scene:** `Freefall_Symphony_3D` (**Simulation Lobby ▸ 3D Formats ▸ Freefall (fall) ▸ Freefall Symphony (fall-001)**, priority 281)
**Short cut:** `fall-002` *Mountain King* — same scene builder, one world, 9:16 (`Freefall_MountainKing_3D`, priority 282)
**Config:** `Assets/Settings/Configs/Freefall/FreefallConfig_001.asset` (`_002` for the Short) · **Seed:** random per Play, logged (cosmetic only: angles, jitter, decoration)
**Inspiration, not a template:** MIKAN 3D *Abstract World* (14.5M, a POV ride through abstract worlds) and the music-marble genre
(`reference-channels.md` → @MusicMarble3D, ba11s song-recognition hits). The only things we take are *hypnotic*, *a journey through
worlds* and *sound on*. No coaster, no tunnel ride, no copied world looks.

## Premise

**One glowing marble falls down an endless well through seven worlds, and every bounce plays the next note of a famous song.**
The pads it hits are placed *exactly* where the marble will be when the next note is due, so the rhythm is the composer's, not the
physics'. Every pad stays lit, so each song leaves a glowing spiral sculpture behind it. At the end of a world, the camera pulls
back to show the sculpture as the song's title is revealed.

Working long-form title: **One Marble Falls Through 7 Worlds, and Every Bounce Plays a Song** (no hashtags in long form).

What makes it ours, not MIKAN's: it's vertical freefall, not a horizontal ride; the marble *plays* the world instead of passing
through it; you can *see* the music (pitch → distance from the centre and hue); and the midpoint twist (the marble splits into a
real canon, voices entering one after another).

## Storyboard (cold viewer, sound ON is the main path, but it must still read with sound off)

| # | Time | World / song | On screen | Caption (top, ≤ 4 words) |
|---|---|---|---|---|
| Hook | 0:00 | — | Black. A white marble drops into frame, falls, and the first pad fades in under it *just* before contact. *E-D♯-E* | `EVERY BOUNCE IS A NOTE` (0-4 s) |
| 1 | 0:03 | **Glass Bells** · *Für Elise* (Beethoven) | Deep-indigo void, clear glass bell-discs. Each hit sends a light ring rippling down the wall rings | `1 / 7 · GLASS BELLS` |
| reveal | ~1:20 | | Camera pulls up and out: the lit spiral of pads hangs in the dark. Marble drops through a portal | `♪ FÜR ELISE` · sub `BEETHOVEN` |
| 2 | ~1:25 | **Aurora** · *Morning Mood* (Grieg) | Dawn gradient (teal → gold), translucent petal pads, aurora curtains on the walls. Flute timbre | `2 / 7 · AURORA` |
| 3 | ~2:50 | **Moon Pool** · *Gymnopédie No. 1* (Satie) | Low gravity: long, floaty arcs. Big lily pads; every landing sends concentric ripples across the pad. Soft piano | `3 / 7 · MOON POOL` |
| 4 | ~4:15 | **Crystal Cave** · *Ode to Joy* (Beethoven) | Faceted crystal pads, a sparkle burst on each hit, crystal shards lining the walls. Celesta | `4 / 7 · CRYSTAL CAVE` |
| **Twist** | ~5:40 | **Kaleidoscope** · *Canon in D* (Pachelbel) | **The marble splits.** New marbles fall in from above, one every 2 bars, each mirroring the first around the well, so the song plays as a real **canon in rounds**. A 6-fold mandala wall. Harp | `5 / 7 · KALEIDOSCOPE` then `IT SPLITS` |
| 6 | ~7:05 | **Eclipse** · *Moonlight Sonata* (Beethoven) | Near black. The voices merge back into one marble during the fall. A single moonbeam follows it; thin silver crescent pads; the hypnotic triplets | `6 / 7 · ECLIPSE` |
| 7 | ~8:30 | **Singularity** · *In the Hall of the Mountain King* (Grieg) | A spiral vortex that turns faster and faster. Molten hex pads. **Tempo accelerates** (the piece's own accelerando), the arcs get tighter, the camera rolls | `7 / 7 · SINGULARITY` |
| End | ~9:50 | | The last note: the marble hits the white core, a full chord, white-out, silence. Pull back to all seven sculptures stacked down the well | `♪ THE MOUNTAIN KING` → `7 SONGS. 1 MARBLE.` |

Bottom footer for the whole run: world progress dots `● ● ● ○ ○ ○ ○`. That's the long-form stake: *how far down does it go?*
No other numbers on screen.

Cold-viewer checklist (`learnings.md` → *Story legibility*):
- [x] The hook names what's visible from frame one: the marble and the pads (`EVERY BOUNCE IS A NOTE`)
- [x] Every cause is on screen: each note is a visible contact; a pad fades in *before* the marble arrives (anticipation); the split
      in world 5 happens in frame
- [x] Every beat raises the stakes: a new world and song each act, a twist at the midpoint (split/canon), and a climax (accelerando)
- [x] The only number is progress (`n / 7`)
- [x] The ending says what happened in one line (`7 SONGS. 1 MARBLE.`)

Long-form structure check (CLAUDE.md *Content guidelines*): stake up front (hook + progress dots), acts that raise it (7 worlds),
midpoint twist (canon split), and a recurring character (the marble).

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md): `SimulationRunner`, `ISimulation`, `SimulationConfig`
- [ ] **REUSE** [bounce-audio-sequencer](../blocks/bounce-audio-sequencer.md): `MelodySequence` assets (`FurElise.asset` as is, plus 6 new ones)
- [ ] **REUSE** [studio-look-3d](../blocks/studio-look-3d.md): URP post (bloom), but **dark** worlds rather than the bright cyclorama
- [ ] **REUSE** `CaptionCanvasBuilder.Style.Dark` / `CaptionHud`, `ProceduralToneBank.CreateWhoosh` / `CreateRiser` / `CreateAmbientBed`
- [ ] **BUILD** `Presentation/Audio/ProceduralInstrument`: pitched timbres (bell, flute, soft piano, celesta, harp, mallet). Reusable by any format
- [ ] **BUILD** the `fall` format: kernel, config, simulation, view, audio, scene builder

## Architecture (the contract between the three build packages)

New assemblies `SimulationLobby.Simulations.Freefall` (runtime, `Assets/Scripts/Simulations/Freefall/`, same references as
`SimulationLobby.Simulations.PiClacks`) and `SimulationLobby.Simulations.Freefall.Editor` (same references as the PiClacks
editor asmdef, with `Freefall` in place of `PiClacks`). Namespace `SimulationLobby.Simulations.Freefall`, editor
`SimulationLobby.Simulations.Freefall.EditorTools`. Layout mirrors `clack`/`brach`: `Kernel/`, `View/`, `Editor/`.

**No PhysX.** The marble's motion is analytic ballistics between impulse pads, computed once in `Initialize`. That makes the run
exactly reproducible and lets presentation sample a smooth position at any time.

### Package A — simulation (owns `Kernel/*`, `FreefallConfig.cs`, `FreefallSimulation.cs`, runtime `.asmdef`, scratchpad harness)

```csharp
// FreefallConfig.cs
public enum PadShape { Disc, Petal, Lily, Crystal, Crescent, Hex }
public enum WallStyle { RingStack, Aurora, Mist, CrystalShards, Mandala, Starfield, Vortex }

[Serializable] public sealed class FreefallWorld {
    public string worldName = "GLASS BELLS";
    public MelodySequence melody;               // SimulationLobby.Presentation (rhythm comes from note beats)
    public string composer = "BEETHOVEN";
    public float bpm = 120f;                     // quarter-note beats per minute at the world's start
    public float bpmEnd = 120f;                  // > bpm = accelerando; tempo ramps linearly in *beats* across the world
    [Min(1)] public int passes = 2;              // times the melody plays through
    public float gravity = 9.81f;                // m/s², lower = floatier arcs
    public float wellRadius = 3.2f;
    [Range(0f,1f)] public float innerRadiusFraction = 0.25f; // lowest note's distance from the axis, as a fraction of wellRadius
    [Range(0f,1f)] public float outerRadiusFraction = 0.8f;  // highest note's distance
    public float degreesPerSecond = 40f;         // how fast the hit points wind around the axis
    [Range(0.05f,0.95f)] public float dropFraction = 0.5f;   // fraction of ½·g·dt² to drop per note (<1 guarantees an upward bounce)
    public float maxDropPerNote = 1.6f;
    [Min(1)] public int canonVoices = 1;         // >1 = the split: voice k starts k·canonDelayBeats late, rotated k·360/voices degrees
    public float canonDelayBeats = 8f;
    // Look (read by View only; the kernel ignores these)
    public PadShape padShape; public WallStyle wallStyle;
    public ProceduralInstrument.Timbre timbre;   // from Package B
    public Color skyTop, skyBottom, fogColor, wallColor;
    public float hueStart = 0.6f, hueSpan = 0.25f; // pitch → hue: low notes hueStart, high notes hueStart+hueSpan
    public float padRadius = 0.32f;
    public float cameraRollDegreesPerSecond = 4f;
}

public sealed class FreefallConfig : SimulationConfig {   // CreateAssetMenu "Simulation Lobby/Configs/Freefall Config"
    public List<FreefallWorld> worlds;
    public float introDropSeconds = 1.2f;        // marble falls from rest onto the first pad
    public float transitionSeconds = 4f;         // fall between worlds (the reveal pull-back happens here)
    public float worldGap = 6f;                  // extra vertical metres between worlds
    public float outroSeconds = 5f;              // after the final note: core hit, white-out, final pull-back
    public float marbleRadius = 0.12f;
    public float padLeadSeconds = 0.9f;          // a pad fades in this long before it's hit (anticipation)
    public override bool Validate(out string error); // ≥1 world, every world has a melody with ≥2 notes, sane ranges
}
```

```csharp
// Kernel/FreefallSchedule.cs: pure maths, no MonoBehaviour
public readonly struct FreefallHit {
    public readonly float time;          // seconds from run start
    public readonly int voice;           // 0 = the hero marble; 1.. = canon voices
    public readonly int world;
    public readonly int noteIndex;       // index into the world's melody
    public readonly float frequencyHz;   // melody.FrequencyAt(noteIndex)
    public readonly float ringSeconds;   // how long the note should ring: beats × seconds-per-beat at that moment, clamped 0.25..3
    public readonly float pitch01;       // 0 = lowest note in that world's melody, 1 = highest (drives radius and hue)
    public readonly Vector3 position;    // contact point (marble centre at contact)
    public readonly Vector3 normal;      // pad normal = normalize(vOut − vIn)
    public readonly float impulse;       // |vOut − vIn|, for volume
    public readonly bool isWorldFinal;   // last hit of the world (all voices)
    public readonly bool isRunFinal;     // the core hit
}
public readonly struct FreefallWorldSpan {
    public readonly int index; public readonly float startTime, firstHitTime, lastHitTime, endTime; // endTime = start of the next world's first fall
    public readonly float yTop, yBottom; public readonly int firstHitIndex, hitCount; public readonly int voiceCount;
}
public sealed class FreefallSchedule {
    public static FreefallSchedule Build(FreefallConfig config, SeededRandom rng);
    public IReadOnlyList<FreefallHit> Hits { get; }          // sorted by time, then voice
    public IReadOnlyList<FreefallWorldSpan> Worlds { get; }
    public int MaxVoices { get; }
    public float Duration { get; }                           // last hit + outroSeconds
    public Vector3 CoreCentre { get; }                       // the final hit's target, below the last world
    public bool TrySample(int voice, float time, out Vector3 position, out Vector3 velocity); // false = that voice doesn't exist at that time
    public int WorldAt(float time);
}
```

> **As built (2026-10-20), where it differs from the rules below:**
> - **Harness, seed 12345:** 1,435 hits, **9:35**, 0 warnings. Every world runs 75-80 s from first to last note, the highest arc
>   is 2.33 m (Aurora), and the closest pad clearance is 21 mm. Over 400 seeds per config there were 0 warnings. The Short runs
>   77 s with 261 hits.
> - **Pads are placed by a search, not a pure helix.** A pad only works if the marble arrives at its front face and leaves from
>   it, so the kernel picks each pad from ordered candidates and goes back one pad when it has to. Pitch still sets the radius.
> - **Validation tests each pad as a disc**, one marble radius behind the contact point, with a 5 mm clearance from every
>   other pad.
> - **Transitions fall at 9.81 m/s² whatever the world's gravity**, so worlds sit about 50 m apart (the core is at y ≈ −811).
>   `worldGap` only raises the launch speed. The first hit of each world lands at about 31 m/s, so audio uses the 90th-percentile
>   impulse as full volume, not the maximum.
> - **Canon voices fly to the next world's first pad and arrive together** with voice 0, then fade out. They don't merge onto
>   voice 0's path mid-fall.
> - **Melodies:** *Sugar Plum Fairy* was swapped for *Ode to Joy*, and the lead rewrote Morning Mood, Canon, Moonlight and
>   Mountain King from the score (the note names are in each asset's `source`).

**Kernel rules:**
- Note times: walk the melody `passes` times, advancing `beats × 60/bpm(t)`. With an accelerando, bpm ramps linearly over the world's
  total beat count. Voice k plays the same note list offset by `k·canonDelayBeats`, and **every voice stops at voice 0's last note**
  (later voices are truncated, as a canon ends together). Its last hit is flagged `isWorldFinal`.
- Hit points: angle θ advances by `degreesPerSecond·dt` (seeded start angle; the world's winding direction is seeded too). Radius =
  `lerp(inner, outer, pitch01)·wellRadius`, plus seeded jitter of ±4%. Height drops `min(dropFraction·½·g·dt², maxDropPerNote)`.
- Ballistics between hits i and i+1, with interval `dt`: `vOut = (p₁ − p₀)/dt − ½·gVec·dt`, `vIn(next) = vOut + gVec·dt`, where
  `gVec = (0, −gravity, 0)` uses the world's gravity. Pad normal = `normalize(vOut − vIn)`. That's a frictionless contact impulse,
  so it's physically consistent (the restitution varies, so the pads read as "bumpers").
- Repeated notes with tiny dt (< 0.08 s) still produce a valid, small hop. Don't merge notes.
- Canon voice k = voice 0's geometry rotated by `k·360/canonVoices` around the Y axis and time-shifted. It **appears at the
  world's top** at its start time (falls in from above, `introDropSeconds` of fall before its first hit).
- Transitions: after a world's final hit, every voice falls for `transitionSeconds` to the next world's first hit. Voices > 0 converge
  onto voice 0's path, so presentation can fade them into one. Use the same ballistic formula with the next world's first hit point
  as the target; the drop is `worldGap` plus whatever the formula needs.
- The run's final hit (`isRunFinal`) targets `CoreCentre`, `worldGap` below the last world on the axis.
- **Validation in `Build`:** every hit point is inside `wellRadius − padRadius`; for every pad, the marble's sampled path (every
  1/240 s, excluding ±0.06 s around that pad's own hit) never comes within `padRadius + marbleRadius` of the pad centre. On a
  violation, retry that world with the angle step nudged +7°, at most 8 times, then log a warning naming the world and hit.
- **Scratchpad harness** (a plain C# console project compiling `Kernel/*` with a tiny `Vector3`/`SeededRandom` shim, like `brach`'s):
  print per-world duration, hit count, height span, min clearance and the slowest/fastest marble speed; build twice with the same
  seed and assert identical hits.

```csharp
// FreefallSimulation.cs: MonoBehaviour, ISimulation, FormatSlug "fall"
public FreefallConfig Config { get; }
public FreefallSchedule Schedule { get; }  // built in Initialize
public float Time { get; }                 // ticks × fixedDelta
public int HitsPlayed { get; }             // count of hits with time ≤ Time
public int CurrentWorld { get; }
public bool IsComplete => Time >= Schedule.Duration;
// Result: RunResult with summary "fall seed=… worlds=7 hits=… duration=…s"
```
Config `fixedTimestep` = 1/240 (timing precision only; there's no physics).

### Package B — audio (owns `Presentation/Audio/ProceduralInstrument.cs`, `Settings/Melodies/*` new assets, `View/FreefallAudio.cs`)

```csharp
// Presentation/Audio/ProceduralInstrument.cs, namespace SimulationLobby.Presentation
public static class ProceduralInstrument {
    public enum Timbre { GlassBell, Flute, SoftPiano, Celesta, Harp, Mallet }
    public static AudioClip CreateNote(Timbre timbre, float frequencyHz, float ringSeconds, int seed = 1);
}
```
- Synthesis must be deterministic (seeded `System.Random` for any noise; Karplus-Strong excitation for the harp). Use 44.1 kHz mono,
  with a soft attack (2-5 ms) so nothing clicks, and an exponential decay sized to `ringSeconds` plus a tail. Every timbre must sound
  good on phone speakers: keep real energy above ~250 Hz (add the 2nd/3rd partial for low notes).
  - GlassBell: additive, inharmonic partials (1, 2.0, 2.76, 5.4, 8.93) with per-partial decay
  - Flute: sine + a weak 2nd/3rd partial, gentle 60 ms attack, 5 Hz vibrato fading in, a breath-noise puff at onset
  - SoftPiano: 2-operator FM, ratio 1:1, index decaying from ~2.5 to 0.3, plus a quiet hammer transient
  - Celesta: sine + a strong 4th partial, bright and fast decay (music box)
  - Harp: Karplus-Strong with a lowpass in the loop and seeded noise excitation
  - Mallet: sine + 4th and ~10th partials, short decay (marimba-like)
- **New `MelodySequence` assets** in `Assets/Settings/Melodies/`. Melody line only, `tonicHz` chosen so the melody sits around
  A3-A5, `beats` as written (1 = a quarter note; triplets = 1/3), `endsPhrase` on phrase ends, and `source` stating the work, the
  passage, and that it's public domain:
  - `MorningMood.asset`: Grieg, *Peer Gynt Suite No. 1*, "Morning Mood" (1875), opening flute theme (two statements)
  - `GymnopedieNo1.asset`: Satie, *Gymnopédie No. 1* (1888), melody from bar 5 through the first full phrase pair
  - `OdeToJoy.asset`: Beethoven, Symphony No. 9 finale theme (1824), the full 4-line tune. (It replaced *Sugar Plum Fairy*, whose
    celesta line couldn't be transcribed with confidence. A wrong note in a recognition video is worse than a different song.)
  - `CanonInD.asset`: Pachelbel, *Canon in D*, the famous upper line (the 8-note descending figure and its first variations),
    which **must work as a canon against itself at a 2-bar (8-beat) delay** over the D-A-Bm-F♯m-G-D-G-A ground
  - `MoonlightSonata.asset`: Beethoven, *Piano Sonata No. 14*, 1st mvt (1801), the right-hand triplet figure plus the melody entry
    (bars 1-9), triplets at `beats = 1/3`
  - `HallOfTheMountainKing.asset`: Grieg, *Peer Gynt*, "In the Hall of the Mountain King" (1875), the main theme played through 4×
    (its own accelerando comes from the world's `bpm → bpmEnd`)
  - Be exact: these are recognition payoffs and a wrong note ruins one. Put the note names as a comment line in `source`
    (e.g. `E5 D#5 E5 D#5 E5 B4 D5 C5 A4`) so a reviewer can check them.
- `View/FreefallAudio.cs` (MonoBehaviour, in the format): `Bind(FreefallSimulation sim, FreefallConfig config)`. In `Update`, it
  plays every hit whose time has passed since the last frame (its own cursor; read-only), using a pool of 32 `AudioSource`s and
  clips cached by `(timbre, frequency)` and **pre-generated for the whole world at world start** (no hitches mid-song). Volume:
  `0.45 + 0.25·impulse01`; canon voices are panned slightly apart (`panStereo` ±0.35); and `AudioReverbFilter` is on the listener
  side (one shared reverb, a long hall). It also plays: a per-world drone bed (`ProceduralToneBank.CreateAmbientBed` at the
  melody's tonic, crossfading over 2 s at world changes), a whoosh at each transition, a riser over the last 4 s before the final
  hit, and a big chord at the run-final hit (the root, fifth and octave of the last world's tonic in that world's timbre, plus a
  low thud).

### Package C — view and scene (owns `View/*` except `FreefallAudio`, `Editor/*`, the editor `.asmdef`, config assets)

- `View/FreefallPresenter.cs`: `Bind(FreefallSimulation sim, CaptionHud hud, Camera cam)`. It builds world *w*'s pads and walls
  when world *w−1* starts (pre-build one ahead) and destroys world *w−2*. It runs everything in `Update`/`LateUpdate` and never
  writes simulation state.
  - **Marble(s):** a bright white emissive core inside a glass shell (HDR so it blooms), with a `TrailRenderer` tinted by the last
    note's hue and a small point light that travels with it (lights the nearby pads). Canon voices get the same marble at 85% scale.
    During a transition they converge, and they fade out on contact with voice 0.
  - **Pads** (one per hit, shape from `padShape`, radius `padRadius`, oriented to `normal`): invisible until `time − padLeadSeconds`,
    then scale and fade in (ease-out). They flash HDR on contact (×6 emission, then decay over 0.6 s) and stay **lit at about 35%**
    for the rest of the run, so the song's sculpture builds up. Hue = `hueStart + hueSpan·pitch01`. Use `MaterialPropertyBlock`s,
    not material instances.
  - **Walls** per `WallStyle`, built procedurally around the world's vertical band `[yBottom − 2, yTop + 2]` at `wellRadius + 0.6`:
    RingStack (thin emissive torus rings every 0.5 m; a hit brightens the nearest ring, and the glow travels down the stack),
    Aurora (soft vertical curtains of additive quads, slowly waving), Mist (sparse soft particles plus a faint cylinder; the ripple
    rings live on the pads instead), CrystalShards (instanced faceted shards lining the wall, pointing inward), Mandala (6-fold
    symmetric petal lattice, which rotates slowly), Starfield (points on a large cylinder and a crescent-moon beam), and Vortex
    (spiral strips whose rotation speed follows the world's bpm, so it spins up with the accelerando).
    **All decoration randomness uses a `System.Random` seeded from the run seed + world index.** Never `UnityEngine.Random`.
  - **Sky and fog:** the camera background and linear fog lerp `skyTop/skyBottom/fogColor` across each transition. Fog is the depth
    cue the well needs (CLAUDE.md 3D baseline).
  - **Camera ("descent cam"):** above and slightly outside the hero marble (≈2.2 m up, 1.4 m out), looking down the well at a point
    between the marble and the next pad (look-ahead). Smoothed position, FOV 68, and a constant roll around the well axis at
    `cameraRollDegreesPerSecond`. During each transition, it **pulls back and up for 2.5 s** to frame the finished world's lit sculpture,
    then dives after the marble through the gap. At the outro, it does a slow pull-back revealing all worlds stacked (the fog fades
    out for this shot).
  - **HUD** via `CaptionHud` (built with `CaptionCanvasBuilder.Style.Dark`): caption = the hook for 0-4 s, then `n / 7 · WORLD`
    for 3 s at each world start, then `♪ SONG TITLE` (subline = composer) from the world's final hit until the next world starts;
    `IT SPLITS` 2 s after a canon world's second voice appears; and at the end, `♪ TITLE` → `7 SONGS. 1 MARBLE.` Footer = progress
    dots (`●` done/current, `○` ahead). For a one-world config, hide the dots and the `n / 7` prefix.
  - **Ending:** on the run-final hit, a white core sphere flashes, a full-screen white overlay rises over 0.8 s, holds 1 s, then
    falls to reveal the pull-back.
- `Editor/FreefallSceneBuilder.cs`: menu items **Simulation Lobby ▸ 3D Formats ▸ Freefall (fall) ▸ Freefall Symphony (fall-001)**
  (priority 281) and **… ▸ Mountain King Short (fall-002)** (282), plus **… ▸ Reset Configs (tuning baseline)** (301). Each builds its
  scene from scratch: `SimulationRunner` (`randomizeSeedOnStart = true`), `FreefallSimulation`, `FreefallPresenter`,
  `FreefallAudio`, the caption canvas, a camera with URP post (bloom threshold ~0.9, intensity ~1.1, so emissives glow, plus a light
  vignette) and an `AudioListener`. It creates the config assets if missing, filled with the 7-world table below (or the single
  Mountain King world for `_002`, whose `bpm` starts higher). Materials go in `Assets/Settings/Freefall/`. It logs the scene path.
  Use URP Lit/Unlit shaders; HDR emission needs the camera's `allowHDR` and the pipeline asset's HDR on (check `PC_RPAsset`).

## Config + tuning targets (FreefallConfig_001)

| # | World | Melody | bpm → end | passes | gravity | timbre | pads | walls | hue (start, span) | sky top / bottom |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | GLASS BELLS | FurElise | 140 | 3 | 9.81 | GlassBell | Disc | RingStack | 0.60, 0.15 | #0B0B2E / #000000 |
| 2 | AURORA | MorningMood | 96 | 3 | 7 | Flute | Petal | Aurora | 0.38, 0.22 | #0E3B43 / #F2B33D |
| 3 | MOON POOL | GymnopedieNo1 | 80 | 3 | 0.8 (moon gravity: 6-beat notes would otherwise arc 7 m up) | SoftPiano | Lily | Mist | 0.55, 0.10 | #10203A / #04070F |
| 4 | CRYSTAL CAVE | OdeToJoy | 100 | 2 | 9.81 | Celesta | Crystal | CrystalShards | 0.80, 0.18 | #1A0830 / #050010 |
| 5 | KALEIDOSCOPE | CanonInD | 72 | 3 | 6 | Harp | Petal | Mandala | 0.0, 1.0 (full wheel) | #160A22 / #02040A |
| 6 | ECLIPSE | MoonlightSonata | 60 | 4 | 5 | SoftPiano | Crescent | Starfield | 0.62, 0.05 | #05060C / #000000 |
| 7 | SINGULARITY | HallOfTheMountainKing | 80 → 190 | 2 (melody = 5 statements, 160 beats) | 9.81 → feels tighter | Mallet | Hex | Vortex | 0.02, 0.10 | #2A0600 / #000000 |

World 5: `canonVoices = 3`, `canonDelayBeats = 8`. **Tune `passes` and `bpm` per world so each lands at 75-85 s** (the harness prints
durations; adjust the table, not the code). The total, with transitions, intro and outro, should be about 9:30-10:30.

`FreefallConfig_002` (the Short): world 7 only, `bpm 96 → 200`, 9:16, aiming for 60-80 s. The hook caption is `EVERY BOUNCE IS A NOTE`,
then `♪ THE MOUNTAIN KING` at the end.

## Acceptance criteria

- [ ] Same seed twice → identical hit list (harness) and an identical-looking run
- [ ] Every note audibly lands on a visible contact: no note without a pad, no pad without a note
- [ ] No pad ever clips the marble's path outside its own hit (kernel validation passes for all worlds)
- [ ] All 7 melodies are recognisable within their first ~4 s, on phone speakers
- [ ] The canon in world 5 is audibly in rounds (3 voices), and the voices merge cleanly in the next transition
- [ ] The accelerando in world 7 is obvious; the final hit lands on the core with the chord and white-out
- [ ] No audio hitch at world starts (clips pre-generated)
- [ ] Captions readable at 480 px; nothing ever covers the marble
- [ ] Length 9:30-10:30 (long form) and 60-80 s (the Short)

## Render + publish

1920x1080 60 fps, **audio baked in** (Unity Recorder, audio track on). Thumbnail: the lit seven-sculpture spiral stack with the marble
mid-fall, high contrast, plus 2-3 words max (`7 SONGS`). Chapters = worlds (times come from the harness's world start times).
No external music: every sound is procedural, and all compositions are public domain.

## Retro

The open question this video answers: **does hypnotic, sound-on, music-synced spectacle earn long-form watch time on our channel,
compared with the contest formats (elim, Lava Run)?** And does the Short cut (`fall-002`) behave like the music-marble Shorts?
