# Block: Studio Look 3D (elegant materials + lighting)

**Status:** `built` (2026-09-30, awaiting first playtest) · **Layer:** `Assets/Art/Studio/` (materials, Shader Graphs, prefab) +
`Presentation/Editor` (scene-builder helper) · **Used by:** `impact`; retrofit candidate for `surv`

The house look for every 3D video: **elegant and classy**, meaning transparency, high gloss and reflection.
It's a dark product-photography studio, not a game level. This extends the 3D baseline in `CLAUDE.md`
(ground, fill/rim light, depth cue) into a reusable kit so no 3D video ships on default URP Lit.

**Readability still wins.** One hue per video, one subject, dark backdrop. Glass that reads as
"clear nothing" at 480px has failed. Tint and rim highlights must give it a silhouette.

## The material kit

| Material | Built as | Notes |
|---|---|---|
| **Tinted glass / jelly** | Shader Graph, transparent | Refraction by sampling *Scene Color* (Opaque Texture is already on in `PC_RPAsset`) offset by view-space normal × strength. Fresnel rim, tint absorption stronger at grazing angles, smoothness 0.95+. The hero material |
| **Chrome** | URP Lit, metallic 1, smoothness 0.92 | Needs things to reflect. See studio reflections |
| **Piano-black lacquer** | URP Complex Lit, clear coat on | Plinths and bases. Black base + sharp clear-coat reflections reads as luxury |
| **Brushed brass / gold trim** | URP Lit, metallic 1, smoothness ~0.7, anisotropy faked with normal map | Thin accents only: bevels, rings |
| **Polished floor** | Lacquer variant, darker, fading into fog | Catches the subject's reflection and shadow |

**One hue per video**, set on the glass tint and echoed in the key light temperature. Trim stays
neutral (chrome/brass). Starter palette: cognac amber, emerald, sapphire, rose gold, smoked grey.

## Studio lighting + reflections

- **Cyclorama backdrop** (curved floor-to-wall, no horizon line) with a vertical gradient, near-black
  at top to charcoal at the floor. Matches the reference grammar.
- **Key:** large soft area-style light high front-left. **Two rim strips** behind and either side
  put the thin bright edge on glass and chrome that makes it read as "expensive".
- **Softbox cards for reflections:** emissive quads on a `StudioReflect` layer, **excluded from the
  main camera's culling mask** but included in a baked **box-projected Reflection Probe**. Chrome and
  glass reflect clean studio panels the viewer never sees directly. This is the core trick for
  reflective surfaces in URP. Check this URP version for SSR before relying on it. None was found in the
  package runtime on 2026-09-30, so plan on probes.
- **Real-time reflections for the moving subject** are out of scope. Probe reflections are static,
  which is fine because the subject reflects the studio, not itself.
- Shadows: soft, high-res shadow map, contact under the subject is mandatory (grounding).

## Post (Volume profile `StudioPost`)

Tonemapping Neutral (or ACES, pick one house-wide), bloom with a **high threshold** so only specular
glints bloom, subtle vignette, light linear fog for depth. **Anti-aliasing: SMAA High on the camera**.
Thin specular edges crawl without it. `PC_RPAsset` has MSAA off. Test 4× MSAA only if SMAA isn't
enough, and note that it's a global asset change affecting every scene.

## Steps

1. Build the glass Shader Graph and the four Lit materials. Material sphere lineup test scene.
2. `StudioSet` prefab: cyclorama, floor, lights, softbox cards, reflection probe, post volume.
3. Editor helper `StudioSetBuilder.Apply(scene, hue)` so a format's scene builder drops it in with one call.
4. Capture with `Unity_Camera_Capture` at 1080x1920 **and** check at 480px wide.

## Acceptance criteria

- [ ] Glass subject has a clear silhouette (rim + tint) at 480px wide on the dark backdrop.
- [ ] Chrome shows the softbox cards in reflection; cards are invisible to the main camera.
- [ ] No specular crawl / aliasing on a slow camera move.
- [ ] Swapping the hue is a single parameter.
- [ ] Holds ≥30 fps preview at 1080x1920 on the dev machine.
