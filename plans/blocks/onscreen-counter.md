# Block: On-Screen Counter / HUD

**Status:** `planned` · **Layer:** `Assets/Scripts/Presentation/` · **Used by:** all formats

The number the viewer watches. Bounce count, survivors remaining, team populations, multiplier total.

## Why

In most of these formats the number *is* the story — viewers track it more than the objects. It's
also the progress bar that tells someone how much is left, which is what keeps them past 10 seconds.

## Parameterized

| Knob | Notes |
|---|---|
| Source | which simulation value to observe |
| Position | top-center default for 9:16; keep inside the middle 80% |
| Format | integer · `x2.5` multiplier · `23 left` · percentage |
| Emphasis | pulse/scale on change; color shift near a threshold |

## Steps

1. `CounterDisplay` — observes a simulation value via the `OnEscalated`-style event. **Never polls
   simulation internals, never writes to them.**
2. Huge type. The failure mode is always "too small", never "too big". Test at 480px wide.
3. High contrast with an outline or shadow — backgrounds change as the simulation runs.
4. Keep clear of YouTube's UI chrome: title overlay at the top, progress bar and actions at the
   bottom/right. Middle 80% vertically is the safe zone for 9:16.
5. Optional pulse on change — `LateUpdate`, presentation-only.

## Acceptance criteria

- [ ] Readable in a 1080x1920 frame downscaled to 480px wide.
- [ ] Never overlapped by YouTube Shorts UI (check on a real phone, not the Game view).
- [ ] Disabling the HUD produces an identical `RunResult`.
- [ ] Still legible against every background state the run produces (including flashing/color cycling).

## Gotchas

- Canvas scaler must be set to `Scale With Screen Size` and matched to the target aspect, or the HUD
  will be correct in 16:9 and wrong in 9:16.
- Rapidly-changing numbers become unreadable — for fast counts, ease the displayed value toward the
  true one, or show magnitude rather than exact digits.
- Burn it into the render rather than adding it in the edit; it stays accurate and in sync.
