# trip-001 — Trippy Coaster

**Format:** `trip` · **Length:** ~5 min (+ a 60-90 s Shorts cut) · **Aspect:** 16:9 long form, 9:16 for the cut · **Status:** `built, not yet played`
**Brief:** `KnowledgeBase/formats/trippy-coaster.md`

## Premise

Can you survive the trip? One cart rides through seven impossible worlds of light.

## Storyboard

| Beat | On screen | Caption / counter |
|---|---|---|
| Hook (0-3.5 s) | Cart waits in the neon gate, warning card over it | `CAN YOU SURVIVE THE TRIP?` + flashing-colours warning |
| Lift (4-30 s) | Chain ticks, gates stream past, the city of light falls away | `WORLD 1 / 7 NEON GATE`, speed climbing |
| First drop (~30 s) | Crest, hang, 42-degree plunge | speed jumps to ~100 km/h |
| 2 UV TUNNEL | Spiralling neon stripes in a fluorescent cage | title card |
| 3 KALEIDOSCOPE | Mirrored 6-fold walls, a banked helix | title card |
| 4 INVERTED SKY (midpoint twist) | Floor and ceiling, then a full barrel roll | title card |
| 5 LIQUID LIGHT | Rippling plasma serpentine | title card |
| 6 FRACTAL BLOOM | Open void of recursive crystals, spiral descent | title card |
| 7 SINGULARITY (payoff) | The funnel closes, plunge down the vortex, brakes | `WORLD 7 / 7`, progress bar full |

- [ ] The question names things visible from the first frame (the cart, the gate).
- [ ] Every change is on screen: the title card names the new world as the portal ring passes.
- [ ] The only numbers shown are world n/7, speed and progress.
- [ ] One-line why/close: end card "You made it through all 7."  _(not built; add to HUD if wanted)_
- [ ] Flash-safe pass: no flash above 3 Hz, none full-frame white.

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md)
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md)
- [ ] **BUILD** coaster-core (`Shared/Coaster`: `CoasterRail`, `RailCourse`, `RideDynamics`), promoted out of `coast`

## Video-specific work

- [ ] Build: **Simulation Lobby ▸ 3D Formats ▸ Coasters ▸ Trippy Coaster (trip-001)**. First import must compile
      `Assets/Shaders/Trip/TripPlasma.shader` (check the console for shader errors).
- [ ] Play once: check timing (~320 s), the barrel roll orientation, tunnel flares at world boundaries and the cart nose framing.
- [ ] Look pass at 1080x1920 and 1920x1080: HUD clear of the action, bloom not blowing out the patterns.
- [ ] Tune from the config asset (`Assets/Settings/Configs/TrippyCoaster/TrippyCoasterConfig_001.asset`): per-world palette, `glow`, `intensity`, tempo.
- [ ] Record the logged seed here: _____
- [ ] Publish metadata (title, description, chapters per world, tags, pinned comment) via the long-form template in CLAUDE.md.
