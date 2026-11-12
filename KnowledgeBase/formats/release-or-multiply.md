# Format: Release or Multiply (`rom`)

Marbles fall through gates labeled with operations — ×2, ×5, +10, RELEASE, HALVE, ×0 — and the count
compounds or collapses on the way down. Built for Shorts: the number on screen *is* the hook.

## Built design (rom-001, 2026-10-05; not yet watched in Unity)

- **Layout:** 4 lanes x 6 rows of gates, a peg field under each row. Gates are `×n`, `+n`, `÷2`
  (halve; a lone marble lives on a seeded flip) and `×0` (wipe). The last row is the high-variance one.
- **Exact count, capped bodies:** a `Marble2D` carries a weight. A multiplier adds real clones while the
  body budget (`maxBodies`, 400) lasts, then makes the marble heavier instead. Heavy marbles swell and
  go gold, so the weight is visible. The count saturates at `countCap` (1e12).
- **Random by default:** lane order per row and every drop position come from the seed, and the scene
  randomizes the seed each Play (logged). No seed hunting; paste the logged seed back to re-render.
- **Code split:** `BodyPool2D`, `TriggerZone2D` (Shared), `WorldLabelLayer`, `CountFormat` and the
  `CaptionHud` (Presentation) are format-agnostic; only the gate rules live in
  `Simulations/ReleaseOrMultiply`.
- **Untuned:** length is a first guess (~20-25s). Dials: `startMarbles`, `dropIntervalSeconds`,
  `gravityScale`, number of rows.

## Rules on screen

The number is the rules. A huge running count top-center, gates labeled in the marbles' path. No
explanation line needed beyond the title.

## Build notes

- The count must be **enormous and always visible** — it's the thing viewers watch, not the marbles.
- Spawning must be pooled and capped. Design for a hard ceiling (e.g. 5,000 active) with the
  displayed count continuing past the visual cap, or the run will tank the frame rate.
- Gate layout should make the *final* gate the highest-variance one. The payoff is the last second.
- Tune so the expected value trends up but the variance is real — viewers need to believe it can fail.

## Pacing (Shorts)

| Time | Beat |
|---|---|
| 0–3s | Drop starts, count visible |
| 3–35s | Compounding through gates, count climbing |
| 35–50s | High-variance final gate |
| 50–55s | Final number held on screen |

## Variations

- Elimination gates that can zero the run entirely.
- Viewer choice: two paths, pinned-comment vote decides next video's route.
- Escalating series where each video starts from the previous video's ending number.
- Reverse: start huge and survive the halving gates.

## Failure modes

- Exponential growth destroying performance → pooling and a hard cap, non-negotiable.
- Number climbing so fast it becomes meaningless → cap multiplier magnitude, or use log-scaled pacing.
- Anticlimactic ending — the last gate must matter.

## Open questions

- Does the escalating series (carry-over numbers) actually build return viewership? Test over 5 videos.
- Best final-gate odds for a satisfying-but-tense finish — 70/30?

## Wide variant (comp-001)

Segment 3 of the ten-sim compilation, **Gate Funnel** (16:9, 1920x1080). Menu: `Simulation Lobby ▸ 2D Formats ▸ Release or Multiply (rom) ▸ Wide: Gate Funnel (comp-001)`; scene `Assets/Scenes/Comp001_03_GateFunnel_Wide.unity`. Additive: `RomWideSceneBuilder`, `RomWidePresenter`, `RomWideStarter`, `RomWideSettings` (the 9:16 builder, presenter and `RomConfig_001` are untouched).

- **Run:** `RomConfig_Comp001_03`: 6 lanes x 5 rows of real gate types (x n, +n, half, x0), shuffled per run, **one starting marble**, last row x100/x20/x5/half/x0/x0. Wipes and halves are kept out of the top two rows so the lone marble is not simply deleted. Slow gravity (0.07) and heavy damping stretch the fall; the sparse single peg line per row keeps the gates the readable part.
- **Frame:** hook top-left, `MARBLES n` counter in the right margin, one result caption held >= 1.5 s in the bottom strip (most dramatic gate since the last change), explainer panel via `SegmentCardPresenter`. Gate labels are world-space TextMeshPro decals on each gate band (dark text on bright bars).
- **Ending:** every banked marble adds a gold dot to a mound in a bucket under the funnel (cap 170 dots), then the final count fades in huge over it.
- **Start beat:** `RomWideStarter` holds the run for `startDelaySeconds` (1.8 s, the segment card) so the single drop is seen; the card timeline uses the starter's clock, not the runner's.
- **Untuned:** never run in Unity. Length is an **estimate (~30-45 s)**, not measured: fall time through the pegs is the unknown. Dials: `gravityScale`, `linearDamping`, `finishHoldSeconds` (config), `startDelaySeconds`, `captionHoldSeconds`, pile and final-number sizes (`RomWide_Comp001_03`).
