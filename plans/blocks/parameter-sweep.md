# Block: Parameter Sweep ("0% vs 100%")

**Status:** `built` (2026-09-30, awaiting first playtest) · **Layer:** `Assets/Scripts/Core/` (runner) + `Presentation/UI` (caption)
· **Used by:** `impact`

Runs the same simulation N times in a row, changing one config value per take, with a caption naming
the value. It's the structure of RenderZen's "0% vs 100%" series (`KnowledgeBase/reference-channels.md`).
Every take is its own payoff, so a single event carries a whole Short without escalation.

Format-agnostic on purpose: "0% vs 100% bounciness" is a legal `esc` video. Only `ISimulation`
is touched.

## Parameterized

| Knob | Notes |
|---|---|
| Take configs | Ordered list of config assets, one per take. Each is a full config (no runtime patching), so each take is independently re-renderable |
| Caption format | e.g. `SOFT {0}%`, plus the value label per take |
| Lead-in seconds | Caption on screen, scene still, before the event. Our setup/anticipation beat |
| Take seconds | **Fixed** length per take. Rhythm beats efficiency: the viewer learns the beat |
| Cut style | Hard cut (default) or 0.15s dip to black |
| Seed | Shared by all takes. Varying it would add a second variable |

## Steps

1. `SweepRunner` (Core): `for each take → Initialize(seed, takeConfig) → Tick until takeSeconds → Teardown`.
   Raises `TakeStarted(index, label)` / `TakeEnded(index)` events for Presentation.
2. **A take ends on the clock, not on settle.** A settle tracker only *warns* if the body is still
   moving fast at the cut (a tuning problem, not a runtime branch). Gate it on `HasContacted`, per
   the settle gotcha in `CLAUDE.md`.
3. Full teardown/re-init between takes. No state bleeds across takes, and a single take can be
   rendered alone for a re-cut.
4. `CaptionHud` (Presentation): one big centred line in the top panel, driven by `TakeStarted`.
   Built with `ScoreboardCanvasBuilder` conventions (overlay canvas, 1080x1920 ref, match height).
   Only use `VersusScoreboardHud.SetQuestion` if its panel layout reads cleanly with no score row.
   Otherwise make `CaptionHud` its own small component.
5. **Loop seam:** the last frame of the final take should cut cleanly into the first frame of take 1
   (same framing, same idle scene). Shorts replay automatically, and a clean seam turns a finish into a replay.

## Acceptance criteria

- [ ] Take k rendered alone equals take k inside the full sweep (determinism per take).
- [ ] Caption changes on the exact frame the take starts, never mid-event.
- [ ] Total length = lead-in + N × take seconds, ±1 frame.
- [ ] Loop seam: last frame → first frame shows no visible jump in camera or lighting.
