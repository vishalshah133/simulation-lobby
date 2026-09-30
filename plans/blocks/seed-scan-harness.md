# Block: Seed-Scan Harness

**Status:** `planned` · **Layer:** `Assets/Scripts/Capture/` · **Used by:** every video

Runs N seeds headlessly, logs each outcome, and ranks them so you can pick runs that are good
television instead of rendering whatever the first seed gave you.

## Why

**This is the step that separates a watchable channel from a random one.** Outcomes are curated by
*choosing seeds*, never by scripting physics — the authenticity is what the whole genre runs on, and
viewers detect fakery.

It also makes the marginal cost of a video approach zero: one build, many seeds, many videos.

## Parameterized

| Knob | Notes |
|---|---|
| Seed range | e.g. 1–1000 |
| Config asset | which format + tuning to scan |
| Scoring function | per-format — what "good television" means here |
| Output path | JSON/CSV under the scratchpad or `Renders/` (both gitignored) |

## Steps

1. Batch runner drives `ISimulation` without rendering — no camera, no audio, uncapped timestep
   accumulation. It works for any format precisely because it only knows the interface.
2. Per seed, record `RunResult` → one row: seed, duration, outcome, and the format's drama metrics.
3. **Scoring function per format.** Examples:
   - `esc` — near-misses before resolution; total duration in the 25–50s band
   - `race` — number of lead changes; final-gap closeness
   - `wars` — number of lead changes; minimum population of the eventual winner
4. Output a sorted table. Top 10 get rendered at preview quality for eyeballing.
5. Chosen seed is written into the video's config asset and committed.

## Acceptance criteria

- [ ] 1000 seeds scan in minutes, not hours (if not, rendering is still attached somewhere).
- [ ] Re-scanning the same range produces an identical table.
- [ ] A seed picked from the scan, rendered at full quality, matches its logged outcome exactly.
- [ ] Containment assertions from [collision-safety](collision-safety.md) run during scans and flag
      failures rather than silently producing bad rows.

## Gotchas

- Headless runs must use the same fixed timestep as the render, or results won't transfer.
- Don't over-filter. Scoring for maximum drama every time makes the channel feel samey; let some
  variance through.
- Log the Unity version with the table — engine upgrades can invalidate a scan.
