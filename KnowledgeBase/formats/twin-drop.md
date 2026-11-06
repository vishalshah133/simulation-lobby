# Format brief: `twin` — Twin Drop (butterfly effect)

Menu: `Simulation Lobby ▸ 2D Formats ▸ Twin Drop (twin) ▸ Twin Drop (twin-001)` -> `Assets/Scenes/TwinDrop_2D.unity`, config `Assets/Settings/Configs/TwinDropConfig_001.asset` (delete to reset).
Origin: `math-convergence.md` #12 (butterfly effect). An abstract first cut (balls in a billiard table, "when do they split?") was
rejected as impossible to follow; this version uses a Plinko board everyone already understands.

## What the viewer sees (9:16, ~80-95 s)
- "TWO MARBLES. SAME DROP. SAME SLOT OR DIFFERENT?" Blue marble on top, a bigger red one behind it, so a red rim shows while they are stacked.
- Six rounds of two pairs. Round 1: exactly the same release, so they land together. Then the gap is 1 cm, a hair, a bacterium, a molecule, one atom.
- Tally across the top: SAME SLOT vs DIFFERENT. Landing slots light up (green if the pair matches).
- End card (measured): "{apart} OF {n} PAIRS LANDED APART. EVEN WITH AN ATOM'S GAP." / "EVERY PEG MAGNIFIES THE TINIEST DIFFERENCE".

## Why it holds
Real physics in doubles (gravity, round pegs, restitution 0.8). Each peg hit multiplies the gap; identical releases always match. Python replica, 80 pairs per gap: 0 apart at gap 0, then 58-68 of 80 (about 75-85%) apart at every gap from 1 cm to 0.1 nm. So some nudged pairs land together by chance; captions only report counts, never "always".
Board is 1 m wide (0.1 m per unit), so a gap in metres converts directly.

## Code
`Simulations/TwinDrop/`: `Kernel/PegBoardKernel`, `TwinDropConfig`, `TwinDropSimulation`, `View/TwinDropPresenter`, `Editor/TwinDropSceneBuilder`. Reuses `VersusScoreboardHud` (panels trimmed to 400/260 px for a taller board), `ProceduralToneBank`, `AmbientDriftField`.

## Check on first Play
- Marbles about 44 px wide at 1080x1920; bottom 288 px is empty (Instagram safe zone); red rim visible while stacked?
- Length 60-120 s; a fall takes about 4-5 s.
- `[twin] Pair N had not landed` error: a wedge in the board.
- Record the logged seed with the published video.
