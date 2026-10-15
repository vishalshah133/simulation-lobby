# Format: Cricket (`cric`)

A cricket match played on a falling-ball board. Each delivery drops one ball through rows of gates:
a **multiplier** scales the distance it carries, a **HIT** gate sends it away (the distance so far
becomes runs) and an **OUT** gate is a wicket. Two international sides bat in turn; a wagon wheel
fills with the shots. The first sports format, and an **experiment**: nothing in our data says
cricket sims perform (see *Evidence*).

## Built design (cric-001, 2026-10-06; never run in Unity)

- **Layout (reworked after the first look):** the ground is the main picture, centred high and
  clear of the score; the board is a **short, wide strip along the bottom** (modelled on a reference Short
  where marbles drift through pegs toward gates at the left end). The ball launches at the right, is pulled
  left by a constant force (`flowAcceleration`, no gravity) and meets **4 columns x 5 lanes** of gates:
  `×1.5`-`×3`, `HIT`, `OUT` and blank pass lanes. Only the last column has the wicket. A ball that gets
  through everything is released at what it carries. Margins: score text top 3 units, footer bottom 1.2;
  ground radius 3.6 at y 2.5, a **big distance/result readout (130 pt) in its own band at y -2.5**, strip centred y -6.05 (moved down 1 on request; its bottom now reaches the footer text zone, -7.2..-8.4); nothing overlaps. The caption HUD's footer panel occupies the bottom 1.2-2.4 units, so keep the strip above it. Gate labels are 40 pt and the colour
  bars carry the meaning, so check them at 480px.
- **Look:** a bright, sunlit outfield seen from above: mown green stripes across the whole frame, a warm sun
  bloom from the upper right and soft cloud shadows drifting over (`DriftingShadows`). No particles. The ground is a
  deliberately darker green with a cream rope edge so the shot lines pop; the strip is a dark board in a cream frame;
  the big readout sits on a dark pill. Because the backdrop is bright, the HUD uses the `Studio` caption style
  (dark text, light halo). Check HUD legibility on the stripes at 480px.
- **Finish:** when the match is decided the readout pill shows `<TEAM> WON!` (text shrinks to fit, pill washes toward
  the winner's colour) and `ConfettiBurst2D` (Presentation/VFX, generic) fires two cannons up and inward from the sides of
  the readout band plus a short rain from the top, in the winner's colours. The margin ("WON BY 2 WICKETS") stays in the
  footer. A tie shows `MATCH TIED` with no confetti.
- **Distance to runs:** the ball starts at 6 m. Bands (config): 0 m dot, 8 m single, 14 m two, 20 m three,
  30 m four, 60 m six. Only 0/1/2/3/4/6 exist; config validation rejects anything else.
- **Shot direction:** the height at which the ball leaves the strip decides the angle (top and bottom
  edges = +-150 degrees off straight, plus a seeded wobble). Fours run along the ground, sixes are drawn as an arc over the rope.
- **Match:** default is a **super over**: 6 balls and 2 wickets per side. Overs, balls per over and
  wickets are config. The chase ends the instant side B passes the target. A tie is a tie.
- **Teams:** `CricketTeamDef` assets (name, 3 letters, two colours; nothing else). Ten sides are made by
  **Simulation Lobby ▸ 2D Formats ▸ Cricket (cric) ▸ Create Team Assets**. Assign any two to a config's Team A (bats first) and
  Team B. Country names and plain colours only: no crests, kits or player names.
- **Random by default:** lane order per delivery, drop point and shot angle are seeded; the scene
  randomizes the seed each Play (logged).
- **Code split:** `CricketMatch` (scorebook, pure C#, checked headless), `CricketSimulation` (board physics,
  `ISimulation`), `CricketPresenter`. Format-agnostic parts: `RadialHitChart` (Presentation/VFX, the
  wagon wheel), `ProceduralCrowd` (Presentation/Audio, bat and stumps only), `TriggerZone2D` (Shared). Cricket does **not**
  reference `rom`: it uses only the Shared primitives, so no promotion refactor was needed.

## Audio (recorded crowd, procedural knocks)

A synthesized crowd (filtered noise, then a formant choir) sounded wrong twice, so **crowd sounds are
recorded clips**: a `CricketAudioKit` asset with a slot per moment (cheer_small / big / roar / final, groan,
applause, applause_soft). Drop royalty-free files in `Assets/Audio/Cricket/` named as the README there says,
then **Simulation Lobby ▸ 2D Formats ▸ Cricket (cric) ▸ Fill Audio Kit** (the scene builder does it too). An empty slot is silent,
never a fake. Bat and stumps stay procedural (`ProceduralCrowd`, short tonal knocks) unless the kit overrides
them. This is a deliberate exception to the procedural-audio house style, for the crowd only. **Keep each
clip's licence note beside it and confirm monetised-YouTube use is allowed.**

## Tuning model (Python, not Unity)

With a centre-biased lane choice the default board gives roughly: dot 34%, single 19%, two 8%, three 7%,
four 13%, six 8.5%, wicket 10%. The lane-choice model is a guess, so measure real runs and adjust gates
and bands. Dials: gate rows, `bands`, `baseDistance`, `gravityScale`, pacing holds.

## Pacing

~0.7 s hold + a few seconds of flow + 2 s result per ball is ~7 s, so a super over is ~100 s (12 balls plus the break).
`resultHoldSeconds` and `preBallHoldSeconds` are the first things to trim.

## Evidence

A web search found **no reference cricket-simulation Shorts** and the API cannot search without spending
quota, so this format is untested against the genre. Before a second video: find 3+ sports or cricket
simulation channels and run `youtube-research` on them.

## Later

Other sports reuse the same board with a different outcome map and field view (`RadialHitChart` is the
generic piece). Do not generalise until a second sport exists.
