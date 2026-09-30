# Block: Material Contact Audio

**Status:** `built` (2026-09-30, awaiting first playtest) · **Layer:** `Assets/Scripts/Presentation/Audio/` · **Used by:** `impact`;
reusable by any 3D format with a contact event

Sound for *what the material is doing*, synthesized procedurally and driven by simulation metrics.
A hard glass body should *clink*, a jelly should *squelch*, a stretched skin should *creak*, and every
point in between should blend. Sound is half of the "satisfying" genre and often the part viewers
replay for (the genre tags itself "ASMR"). House style still applies: **procedural, nothing sampled**
(`ProceduralToneBank`).

It sits next to [bounce-audio-sequencer](bounce-audio-sequencer.md), which does melody per collision.
This block does *material timbre* per contact.

## Voices

| Voice | When | Synthesis | Driven by |
|---|---|---|---|
| **Clink** | Contact, hard end | 4–6 inharmonic sine partials (glass-like ratios), fast exponential decay, slight detune | `ContactImpulse` → gain; `1-softness` → brightness and decay |
| **Squelch** | Contact, soft end | Band-passed noise burst with a downward filter sweep + a low sine body thump | `ContactImpulse` → gain; `softness` → sweep length and depth |
| **Stretch creak** | Skin under strain | Thin, slightly rough sine/saw whose pitch rises with strain (rubber-squeak) | `MaxStrain` over threshold → gain; strain → pitch |
| **Wobble** | After contact | Very low sine "wub", gain following jiggle energy. Felt more than heard | `WobbleEnergy` |
| **Whoosh** | During the fall | Reuse `CreateWhoosh`, gain from `CentroidVelocity` | fall speed |

Clink and squelch **crossfade by softness**, not switch. At 50% you hear both, which is what makes
the sweep audibly progress.

## Architecture

- **Presentation, read-only.** Reads `SoftBody3D` metrics in `Update`, never writes. Removing
  audio must leave the run identical.
- One-shots (clink, squelch, whoosh) generated into `AudioClip`s by new `ProceduralToneBank`
  functions (`CreateGlassClink`, `CreateSquelch`), parameterized per contact and cached by quantized
  parameters.
- Continuous voices (creak, wobble) render in `OnAudioFilterRead` on a dedicated `AudioSource`,
  from parameters copied on the main thread (single floats, smoothed per sample to avoid zipper noise).
- **Recorder check:** confirm `com.unity.recorder` (installed) captures `OnAudioFilterRead` output
  in the render. Do this in the first spike, not at publish time.
- **Deterministic audio:** noise voices use a seeded `System.Random`, so a re-render sounds identical.

## Mix rules

- **~150ms of near-silence before contact.** Duck the ambient bed as the body falls. The contrast
  is the payoff.
- Contact voice peaks around −6 dBFS, and the ambient bed sits ~20 dB under it.
- Master limiter so a hard 0% clink never clips.
- Mono-compatible. Phones play one speaker.

## Steps

1. **Listening spike first, before visuals.** An editor window with sliders for softness/impulse/strain
   that plays each voice. Procedural squelch is the biggest risk in this block: it can easily sound like
   a synth patch rather than jelly. **User signs off on the sound here.**
2. Implement the voices and the `SoftBody3D` metric binding.
3. Tune against the real sim at softness 0 / 0.5 / 1.

## Acceptance criteria

- [ ] Blindfolded (audio only), 0%, 50% and 100% takes are distinguishable and in order.
- [ ] No clicks at voice start/stop; no clipping on the hardest contact.
- [ ] Same seed twice → bit-identical audio track in the render.
- [ ] Disabling this block produces an identical `RunResult`.
- [ ] Recorder output contains the continuous voices.
