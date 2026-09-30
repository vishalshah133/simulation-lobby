# impact-003 — "0% vs 100% Soft: Amber Glass Drop"

**Format:** `impact` (parameter-sweep variant) · **Length:** ~19s · **Aspect:** 9:16 @ 1080x1920
· **Status:** `building` (code built and verified headless 2026-09-30, awaiting first playtest)
**Brief:** `KnowledgeBase/formats/impact.md` · **Reference:** RenderZen, `KnowledgeBase/reference-channels.md`
**Scene:** `Assets/Scenes/Impact_SoftSweep_3D.unity`, built in-editor via **Simulation Lobby ▸ Build Scene ▸ Impact_SoftSweep_3D**
**Config:** `Assets/Settings/Configs/SoftSweep/SoftSweep_001` (sweep) + `SoftDrop_000/025/050/075/100` (takes) · **Seed:** `0`

## Premise

> One amber glass capsule, dropped onto a piano-black plinth five times, from 0% soft to 100% soft.

Every take repeats the same drop, and only softness changes. At 0% the glass clinks and hops.
At 100% it squashes and wobbles like jelly. The viewer watches to see what the next setting does.
This is RenderZen's proven structure (their sweeps: median 110K, best 10.2M), rendered with our look
and, above all, **our sound**.

**Changed from the first plan: no spike.** A spike was built and tested. A closed soft body can
only slide off a point, and impaling needs a tearing skin, which doesn't exist yet. Details and
numbers are in `KnowledgeBase/formats/impact.md` ("What building the soft body taught"). A spike
version is a follow-up once a tearing block exists.

## Blocks used

- [x] **REUSE** [determinism-core](../blocks/determinism-core.md): the sweep runs `ISimulation` like any format
- [x] **BUILD** [soft-body-xpbd](../blocks/soft-body-xpbd.md): `Shared/SoftBody3D`
- [x] **BUILD** [parameter-sweep](../blocks/parameter-sweep.md): `Core/Sweep` + `Presentation/UI/CaptionHud`
- [x] **BUILD** [studio-look-3d](../blocks/studio-look-3d.md): `Presentation/Editor/StudioSetBuilder` + `Art/Studio/Shaders/StudioGlass.shader`
- [x] **BUILD** [material-contact-audio](../blocks/material-contact-audio.md): `Presentation/Audio/MaterialContactAudio`
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md): still `planned`. Record with Unity Recorder by hand until it exists
- [ ] ~~seed-scan-harness~~: not needed. The sweep is deterministic and nothing is randomised

Build order changed at the user's request: no separate sound spike. Sounds are judged in the
first playtest of the full video.

## What was verified before handover

A headless harness compiled the real `Shared/SoftBody3D` code outside Unity and ran every take with
the shipped defaults (1m drop, flat plinth):

Re-tuned after the first playtest ("50/75% barely deform, 100% should be fluid"). Height as % of
the capsule's own:

| Take | Squashes to | Settles at | Wobble | Spread (radius) |
|---|---|---|---|---|
| 0% | 100% (hops ~1cm, rigid glass) | 100% | none | 0.45m |
| 25% | 73% | 90% | 2.4s | 0.51m |
| 50% | 60% | 81% | 2.8s | 0.59m |
| 75% | 45% | 65% | 2.8s | 0.75m |
| 100% | 33% | 42% (water-balloon splat) | 2.2s | 0.90m |

All five stay on the (now 2m) plinth, peak particle speed stays at 5.4 m/s or below, and every take is deterministic.

Volume held within 0.3%, every take was deterministic (identical state hash on re-run), and each
step took 1.3–2.0ms (16 substeps, 750 particles). **Not verified:** anything visual or audible —
the shader, lighting, framing, captions and every sound are unseen until the first playtest.

## Video-specific work

### Scene

- **Subject:** an upright capsule (r 0.45, half-length 0.2), **cognac-amber tinted glass** that
  refracts what's behind it.
- **Plinth:** a **piano-black lacquer** block (clear coat) with a hairline **brass trim** under
  its top edge and a thin **brass inlay ring** marking the landing spot.
- **Studio:** a curved cyclorama with a charcoal-to-black gradient, key, two rim lights, a warm
  halo on the backdrop, and softbox cards behind the camera for reflections.
- Camera: a 28° long lens, looking slightly down, solved so the drop and plinth sit between the
  caption and the footer at 9:16.

### Config + tuning targets

| Knob | Default | Note |
|---|---|---|
| `takeSeconds` | 3.4 | 5 × 3.4 + leads + hold ≈ 19s |
| `firstLeadInSeconds` / `leadInSeconds` | 1.2 / 0.35 | Take 1 shows the hook line |
| `dropHeight` | 1.0 | 0.45s fall |
| `shapeRateAtHalf` / `shapeRateExponent` | 0.3 / 1.26 | Shape memory; lower = more jelly. 100% always 0 |
| `edgeCompliance` | 1 | Skin stretch. Higher = flatter splat, wider spread (watch the plinth edge) |
| `wobbleDampingStiff` / `Soft` | 5 / 3 | Lower = longer jiggle |
| `softRestitution` / `rigidRestitution` | 0.05 / 0.3 | Soft bounce cap / glass hop |
| `staticFriction` / `dynamicFriction` | 1.0 / 0.7 | High, so the splat spreads evenly instead of skating |
| `plinthSize` | 2 × 0.75 × 2 | Wide enough for the 100% splat |
| `dropOffsetX`, `dropTiltDegrees` | 0 | **Keep 0**: any tilt topples an upright capsule off the plinth |
| Glass look | `Settings/Studio/SoftSweepGlass.mat` | `_Tint`, `_Refraction`, `_RimStrength` |

### Sound

Per [material-contact-audio](../blocks/material-contact-audio.md), plus:

- **Ambient bed** under the whole video, ducked through each fall.
- **Caption bell** rising one pentatonic degree per take (0-1-2-3-5), landing on the octave at 100%.
- **Fall whoosh** timed to the 0.45s fall, then the contact: pure clink at 0%, blended through 25–75%,
  squelch at 100%, then creak (while the surface stretches) and the low wobble tail.

## First playtest checklist (user)

1. Run **Simulation Lobby ▸ Build Scene ▸ Impact_SoftSweep_3D**, then Play.
2. **Look:** Does the glass read as glass (refraction + rim), or does it fall back to plain transparent
   Lit? A Console warning names the shader if it failed. Is chrome/lacquer reflecting the softboxes?
3. **Sound:** Do the clink, squelch, creak and wobble sound like materials, and are the takes
   distinguishable with eyes closed?
4. **Framing:** Check at 9:16 and 16:9 in the Game view.

## Acceptance criteria

- [ ] All block acceptance criteria met
- [ ] Each take rendered alone (`SweepRunner.soloTake`) = the same take in the full sweep
- [ ] The 5 takes are visually *and* audibly distinct, in order
- [ ] Glass capsule has a clear silhouette at 480px wide; captions readable at 480px
- [ ] Nothing behind HUD text at 9:16; also checked at 16:9
- [ ] 14–22s total; clean loop seam from the last frame to the first
- [ ] Renders at 1080x1920 with audio via Recorder, including the continuous creak/wobble voices

## Render + publish

- **Title** (copy the shape, not the words): *"0% vs 100% Soft 🔥 Amber Glass Drop"*.
- **Thumbnail:** the 100% take at peak squash, "100%" in large type.
- **Pinned comment:** "Which % was the most satisfying? 👇"
- **Audio:** fully procedural. Mention "sound on 🔊" in the description.
- **Follow-ups are config-only:** swap hue or capsule proportions, or sweep another knob (drop height,
  bounce) in the same scene.

## Retro

Write `KnowledgeBase/retros/YYYY-MM-DD-impact-soft-sweep.md`, update `/PROJECTS.md`.
The open question this video answers: **can a URP real-time render with strong sound compete in a
genre led by offline Blender renders, and does the sweep structure give `impact` enough footage?**
