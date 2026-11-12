# Format brief: `fit` — Perfect Fit (oddly satisfying, 3D)

Menu: `Simulation Lobby ▸ 3D Formats ▸ Perfect Fit (fit) ▸ Spinning Wall (fit-001)` -> `Assets/Scenes/PerfectFit_SpinningWall_3D.unity`, config `Assets/Settings/Configs/PerfectFitConfig_001.asset` (menu `Reset Config` restores the rounds).
Origin: the "oddly satisfying" brainstorm (2026-11-06): the near-miss that fits exactly, perfect sync, a smooth pass. No race, no score.

## What the viewer sees (9:16, ~66 s)
- A pearl wall disc turning, a hole cut in it that glows amber (a lamp behind it), and a teal shape hanging in front, tumbling out of step. "WILL THE TRIANGLE FIT?" / "GAP 30 MM".
- The shape flies in decelerating. Its tumble and spin offset close to exactly zero just before it reaches the wall (it locks onto the wall's spin), then it slides through. The camera pushes in on the pass.
- With the shape in the hole, the only light left is the gap: a glowing hairline that gets thinner every round. Result line: "FITS." then a rising glass note (one step of a scale per round).
- Seven rounds: triangle 30 mm, cross 15, star 8, gear 4, flower 2, ninja star 1, spike star 0.5 mm; the spin climbs 40 -> 210 deg/s.
- End card: "7 SHAPES. ZERO TOUCHES." / "SAME SHAPE, SAME SPIN: THE GAP NEVER CLOSES."

## Why it holds
The tension is the *out-of-step* start: for 2-3 s it looks like it will clip, then it doesn't. Nothing can fail, so the payoff is purely the lock-on and the shrinking gap. Risk: with no chance of failure, repetition. Check it is varied enough on first Play; a final "fails" round is the obvious lever (not built).

## Code
`Simulations/PerfectFit/`: `Kernel/FitProfile` (outlines, gap offset, clearance measure), `PerfectFitConfig`, `PerfectFitSimulation` (closed-form `PoseAt(round, seconds)`; seed picks spin direction, start offset, tumble), `View/ProfileMeshBuilder` (meshes built at runtime, never saved), `View/PerfectFitPresenter`, `View/PerfectFitAudio`, `Editor/PerfectFitSceneBuilder`.
`Initialize` measures each round's real gap (outline vs hole) and logs `round_N_measured_gap_mm`; a mismatch with the caption is a `[fit]` error. Verified 2026-11-06: measured = stated for all 7 shapes.

## Check on first Play
- Set the Game view to 1080x1920. Shape ~38% of the width at the wall? Caption on 1-2 lines without clipping the subline?
- Is the glowing gap visible at 480 px on rounds 5-7 (2 / 1 / 0.5 mm)? If not, push the camera in harder (`closePosition`) or raise the lamp / bloom.
- Does the wall look like a floating disc? Floor reflections are the default skybox probe (flat); add a reflection probe if it looks dull.
- Edges have no bevel yet; add one if the shape loses its outline against the white wall.
- Bottom 288 px empty. Length 60-90 s. Record the logged seed with the published video.
