# Block: Shorts Capture (9:16)

**Status:** `planned` · **Layer:** `Assets/Scripts/Capture/` · **Used by:** every Short

Renders a chosen seed to a 1080x1920 @ 60fps video file suitable for YouTube Shorts.

## Parameterized

| Knob | Value |
|---|---|
| Resolution | 1080x1920 (9:16) |
| Frame rate | 60 |
| Pipeline asset | `PC_RPAsset` for final; `Mobile_RPAsset` for previews |
| Duration cap | Shorts must stay under 60s; target 25–55s |

## Steps

1. Aspect switcher that sets resolution and the URP asset from a capture profile, so the same scene
   renders to both 9:16 and 16:9 without hand-editing.
2. Offline/deterministic capture: render frames at a fixed step rather than real time, so a heavy
   frame never drops or duplicates. **Never screen-record** — it couples output to machine load.
3. Bake audio in sync with the frames (see [bounce-audio-sequencer](bounce-audio-sequencer.md)).
4. Output to `Renders/` — gitignored. Filename: `<slug>-<nnn>-seed<N>.mp4`.
5. Preview path: 540x960 @ 30 with the Mobile asset, for checking framing cheaply.

## Acceptance criteria

- [ ] Output is exactly 1080x1920, 60fps, no dropped or duplicated frames.
- [ ] Re-rendering the same seed produces identical footage.
- [ ] Audio stays in sync through the fastest section of the run.
- [ ] Verified on a phone: HUD clear of Shorts UI, subject readable, melody audible on phone speakers.
- [ ] Under 60 seconds.

## Gotchas

- Unity Recorder is not in `Packages/manifest.json` yet — add `com.unity.recorder` before this block
  can be built, or implement frame capture manually.
- Real-time capture drops frames under load; use Recorder's offline mode or step the simulation
  manually per captured frame.
- Check framing in a real 9:16 frame, not a maximized Game view — it lies about safe areas.
