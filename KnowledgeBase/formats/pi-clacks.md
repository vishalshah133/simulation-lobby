# Format brief: `clack`, Pi Clacks (colliding blocks count π)

**Tension type:** escalation by prediction. One question ("how many clacks?") is asked again with a block 100×
heavier each round. The answer is exact, and from round 3 it's visibly the digits of π.
**Origin:** the Galperin billiard (G. Galperin, 2003), made famous by 3Blue1Brown. It sits in the maths lane next to
`conv` (`math-convergence.md`, backlog topic 6), and the same MathCa lead applies.
**First video:** [clack-001](../../plans/videos/clack-001-pi-blocks.md).

## The maths (what the sim must reproduce exactly)

A small block (mass m) rests between a wall and a big block (mass M = 100^k · m) that slides toward it. All
collisions are perfectly elastic and there's no friction. The total number of collisions (block-block plus
block-wall) is

    N(k) = ceil(π / atan(sqrt(m / M))) - 1  =  first k+1 digits of π   (3, 31, 314, 3141, …)

**Positions don't affect the count.** Only the velocities do, so start gaps, block sizes and speed are free
tunables for framing and pacing. The dynamics depend only on the free gaps (small block to wall, and small block to
big block), and scaling every gap by s scales every event time by s.

Verified in Python (`scratchpad/clack.py`, 2026-10-17) with an event-driven sim in doubles that snaps positions
at each contact: exact for k = 0..6 (3 … 3,141,592), 1.2 s of CPU for k = 6 in Python.

## Design rules

- **Event-driven and exact, not a physics engine.** Each `Tick(fixedDelta)` resolves every collision inside the step
  analytically, in doubles. Snap the contact position on every event (small block's left face to 0 at the wall,
  small's right face to big's left face at a block hit) so error can't build up. No Rigidbody2D.
- **Every round checks itself.** The sim compares its count to `N(k)` from the formula and logs an error if they
  differ, so a broken round can't ship with a wrong caption.
- **Mass must be seen to change.** The big block grows on screen between rounds (a log scale, since 100× can't be
  drawn), and its painted label ticks up (`1 kg` → `100 kg` → `10K kg` …). The label is a world-space TMP decal on
  the block (the arena-decal exception in CLAUDE.md).
- **The counter is the answer.** `CLACKS n` counts every collision live and never runs ahead of the picture. The
  round result appears only once no more collisions are possible (both blocks moving right, big at least as fast as
  small), which is provable, so the line never gets contradicted.
- **The digit ladder is the twist.** Every finished round's count stacks left-aligned under the question
  (3 / 31 / 314 …), so the π pattern appears on its own around round 3. The end card names it.
- **Seed is cosmetic**: the palette pair, the backdrop theme, a tiny clack-pitch jitter. Still logged.
- **The buzz is the sound.** At high mass, thousands of clacks fall in one frame. Discrete clicks become a pitched
  buzz whose pitch follows the clack rate, so the sound shows the rate too.

## Watch-outs (from the Python model)

- **The last clack can come late.** At k = 2 the final wall touch is at t ≈ 7.9 s, five quiet seconds after the
  buzz. It's a real "is it done? … 314!" beat, so keep it. But the big block is long gone by then, so frame the
  wall side and size each round's start gaps so every contact stays inside the frame.
- **The final block-block hit can happen far right** (k = 5: x ≈ 8.5 with gaps 2/2). Scale the start gaps down per
  round until the harness shows the rightmost contact inside the visible width.
- **Contact gaps get microscopic** (k = 6: big block 2·10⁻⁶ from the small one). That's fine in doubles. Drawn, the
  blocks just look like they touch, which is the classic picture.

## Wide variant (comp-001)

Segment 1 of `comp-001`, **Digit Jump** (16:9, 1920x1080). Menu: `Simulation Lobby ▸ 2D Formats ▸ Pi Clacks (clack) ▸ Wide: Digit Jump (comp-001)`.
Scene `Assets/Scenes/Comp001_01_DigitJump_Wide.unity`, config `Assets/Settings/Configs/PiClacksConfig_comp001_01.asset`,
card `Assets/Settings/SegmentCards/comp001_01.asset`. Code: `Editor/PiClacksWideSceneBuilder.cs` (partial of the portrait
builder), `View/PiClacksWideHud.cs`, plus `PiClacksPresenter.ApplyWideLook` (fixed palette, heavier buzz).

- **Rounds skip**: exponents 0, 2, 4, 6 (mass ratio 1 / 10^4 / 10^8 / 10^12), counts 3 / 314 / 31415 / 3141592, labels
  1 kg / 10,000 kg / 100,000,000 kg / 1,000,000,000,000 kg. Gaps and speed unchanged, so run lengths are the old
  harness numbers (3.0 / 7.9 / 3.1 / 3.5 s). Series estimate about 45 s (not measured in Unity).
- **Look**: black backdrop, fixed magenta small block and cyan big block, violet wall, aqua floor line. Not seed-picked.
- **HUD**: hook top-left, `CLACKS n` in the right margin, digit ladder under it (new digits tinted gold, ends
  `PI = 3.141592`), result caption bottom, `WAIT...` whenever a run is silent for 1.6 s (the k = 2 tail).
- **Hold before round 1 is 7 s** so the title card (1.5 s) and the explainer (5 s) play over a static scene.
- **Open**: the long mass labels (`1,000,000,000,000 kg`) auto-size small on the block; check at 480 px wide.
