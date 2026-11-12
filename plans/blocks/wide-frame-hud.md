# Block: wide-frame-hud (16:9 framing for formats built 9:16)

**Maps to:** `Presentation` (HUD layout) + a per-format framing hook in each scene builder.
**Status:** `built` (not yet exercised in Unity), first user `comp-001`. Extract here instead of ten copies of the same anchor maths.

Every format builder hard-codes 9:16 (1080x1920 reference, ortho size from `width * 1920 / 1080`). A 16:9 render needs the
same scene, a different frame. One block, not one fork per format.

## Contract

- Canvas stays `Scale With Screen Size`, reference **1920x1080** for 16:9 scenes, `matchWidthOrHeight = 0.5`. (The 1080x1920 /
  match-height rule in `CLAUDE.md` is for Shorts; wide scenes flip the dominant axis.)
- Three anchored zones, same in every wide scene so ten segments read as one video:
  - **Top bar:** the question (hook), left-aligned, one line.
  - **Right margin:** the live counter, large, plus a ladder or share bar when the format has rounds.
  - **Bottom strip:** result caption, one line, held for at least 1.2s.
- Arena sits in the middle ~75% of frame height. Nothing the viewer must follow goes behind a zone.
- Each builder takes an `AspectMode { Portrait, Wide }` and only changes camera ortho size, arena offset and HUD anchors. No sim code.
- Presentation reads, never writes. The HUD binder stays in the format.

## Acceptance

- [ ] Same seed + config renders identical outcome in Portrait and Wide.
- [ ] Question, counter and caption readable at 480px wide (scale the 1920 frame down to check).
- [ ] No zone overlaps the arena.

## Usage (built)

All in `SimulationLobby.Presentation` (`Assets/Scripts/Presentation/UI/`), editor helper in `.EditorTools` (`Presentation/Editor/`).

- `enum AspectMode { Portrait, Wide }`
- `WideFrameLayout.ReferenceResolution(AspectMode) : Vector2`, `.MatchWidthOrHeight(AspectMode) : float`, `.Aspect(AspectMode) : float`
- `WideFrameLayout.OrthoSizeForArena(float arenaW, float arenaH, AspectMode mode, float padding = 1f) : float` (arena in middle 75% of height, left of the 22% right margin in Wide)
- `WideFrameLayout.ArenaCameraOffsetX(float orthoSize, AspectMode mode) : float` (shift camera +x to centre the arena in the usable area)
- `SegmentCardConfig` (ScriptableObject): `index, total, title, hook, explainer, cardSeconds=1.5, explainerSeconds=6, accent`
- `SegmentCardPresenter.Build(SegmentCardConfig, AspectMode) : SegmentCardPresenter` (runtime-safe canvas factory)
- `SegmentCardPresenter.SetTimeSource(Func<float>)`, `SetHook(string)`, `SetCounter(string)`, `SetResultCaption(string)`. With no time source it uses the scene's `SimulationRunner.SimulatedSeconds`, else wall-clock.
- `SegmentCardFactory.CreateConfigAsset(string path, int index, int total, string title, string hook, string explainer) : SegmentCardConfig` (writes under `Assets/Settings/SegmentCards/`)
- `SegmentCardFactory.CreateInScene(SegmentCardConfig, AspectMode) : SegmentCardPresenter`

```csharp
var cfg = SegmentCardFactory.CreateConfigAsset("Assets/Settings/SegmentCards/comp001_01.asset", 1, 10, "Digit Jump", "HOW MANY CLACKS?", "Blocks 10,000x heavier each time...");
var card = SegmentCardFactory.CreateInScene(cfg, AspectMode.Wide);
card.SetTimeSource(() => runner.SimulatedSeconds); // each frame: card.SetCounter("CLACKS 314");
```
