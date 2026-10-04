# Block: Seed-Scan Harness

**Status:** `built` (2026-10-04, compiles; first in-Unity run pending) · **Layer:** `Assets/Scripts/Capture/` · **Used by:** every video

## Use it

1. Open the scene. **Simulation Lobby ▸ Seed Scan ▸ Add Scanner To Open Scene**. That adds a
   `SeedScanner` to the `SimulationRunner` and picks the profile named after the runner's config
   (`SeedScan_<ConfigName>`), or the one selected in the Project window.
2. **Play.** Audio is muted and the scan runs in slices so the editor stays responsive. The console
   prints progress, then the top 10 with their metrics and verify status.
3. Full table: `Scans/<scene>_<config>_<first>-<last>.csv`, plus a `.txt` sidecar with Unity
   version / timestep. `Scans/` is gitignored.
4. To use a seed: exit Play, **Seed Scan ▸ Turn Scanner Off In Open Scene**, set
   `SimulationRunner.seed`, and Play. The `[SimulationRunner]` line must match the scan's exactly.
   Record the seed in the video plan and in `PROJECTS.md`.

A new video only needs a new profile asset (`Create ▸ Simulation Lobby ▸ Seed Scan Profile`, in
`Assets/Settings/Scans/`). Requirements and weights name the format's `RunResult` metrics; `duration`
and `finished` always exist, and one `*` sums a family (`attempt_*_bounces`).

**How it stays honest:** each step is `runner.StepOnce()` then `Physics2D.Simulate` /
`Physics.Simulate`, the same order as Unity's own loop, so collision callbacks land where they would
in a render. Rigidbodies are reset to their authored pose before every seed, and the best seeds are
re-run **in reverse order**: anything that leaks between seeds shows up as `DIVERGED`.

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
      Not yet measured. esc-003 runs up to ~17K ticks a seed at a 0.01s step.
- [ ] Re-scanning the same range produces an identical table.
- [ ] A seed picked from the scan, rendered at full quality, matches its logged outcome exactly.
- [ ] Containment assertions from [collision-safety](collision-safety.md) run during scans and flag
      failures rather than silently producing bad rows.

## Gotchas

- Headless runs must use the same fixed timestep as the render, or results won't transfer.
- Don't over-filter. Scoring for maximum drama every time makes the channel feel samey; let some
  variance through.
- Log the Unity version with the table — engine upgrades can invalidate a scan.
