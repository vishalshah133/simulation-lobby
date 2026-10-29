# Simulation Lobby

Unity project that produces **video content for the YouTube channel "Simulation Lobby"** — 2D and 3D
physics simulation videos: marble races, marble wars, "release or multiply", elimination brackets,
survival sims, and similar satisfying-physics formats. Output is **rendered video**, not a shipped game.

## Prime directive

Every feature exists to make a *watchable video*. Before building anything, ask: does this make the
footage more legible, more tense, or faster to produce? If not, skip it.

Two implications that override normal game-dev instincts:

- **Determinism beats realism.** A run must be reproducible from a seed so a good race can be
  re-rendered at higher quality or re-cut. Fixed timestep, seeded RNG, no frame-rate-dependent physics.
- **Readability beats fidelity.** High-contrast colors, clear silhouettes, readable labels at 480px
  wide (Shorts on a phone). Pretty lighting is worth nothing if the viewer can't tell who's winning.

## Environment

- Unity **6000.5.8f1**, **URP** (`Assets/Settings/` has PC + Mobile render pipeline assets).
- Input System package (new), Timeline, AI Navigation, Visual Scripting available.
- Windows. Shell is PowerShell 5.1 — no `&&`, no ternary, no `??`. Bash tool available for POSIX scripts.
- A Unity MCP server is connected: prefer `Unity_*` tools for scene/GameObject/script operations and
  `Unity_ReadConsole` for compile errors over guessing. Use `Unity_Camera_Capture` to eyeball framing.
- **Confirm which project the bridge is serving before the first `Unity_*` write of a session.**
  The bridge is `Unity.AI.MCP.Editor`, shipped inside **`com.unity.ai.assistant`** — so every project
  with that package serves one, and don't go looking for a third-party MCP package in `manifest.json`.
  Several Unity projects sit side by side (`ChessWorks`, `Chess Labs`, `Chess Billionaire`, …) and the
  bridge serves whichever editor is active — it is *not* pinned to this project, and with two editors
  open it has already answered as the wrong one. So open with `Unity_GetProjectData`:
  - Simulation Lobby looks like `Scripts/Core`, `Settings/PC_RPAsset` + `Mobile_RPAsset`, few assets.
  - `Coach`, `Airy UI`, `CoachAnimatorController` or chess assets mean **wrong project** — stop, say
    so, and do not write. A scene edit landing in the wrong project is expensive to unpick.
- `dotnet build SimulationLobby.<Assembly>.csproj` compile-checks without needing Unity focused
  (output goes to `Temp/`, which Unity ignores). `Logs/Editor.log` is the fallback for Unity's own
  compile errors — it only recompiles when its window regains focus. **A brand-new `.asmdef` has no
  `.csproj` yet** (Unity only generates one once the editor regains focus and re-imports) — until
  then, verify unfamiliar Unity 6 API surface by grepping the installed engine DLLs directly, e.g.
  `grep -a -o "linearVelocity" "<UnityInstallDir>/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsModule.dll"`,
  rather than guessing.
- **Unity 6000.x renamed several physics APIs** — this project targets 6000.5.8f1, so use the new
  names everywhere, in 2D and 3D alike: `Rigidbody(2D).velocity` → `linearVelocity`,
  `.drag`/`.angularDrag` → `linearDamping`/`angularDamping`, and 3D's `PhysicMaterial`/
  `PhysicMaterialCombine` → `PhysicsMaterial`/`PhysicsMaterialCombine` (2D's `PhysicsMaterial2D` was
  never renamed, it was already correct). Confirmed against the installed engine DLLs, not assumed.

## Layout

```
Assets/
  Scenes/          One scene per simulation format (MarbleRace_Spiral, MarbleWars_Arena, ...)
  Scripts/
    Core/          Seeded RNG, deterministic sim tick, run recorder, config base types
    Shared/        Reusable building blocks: spawners, pools, contender identity, arena pieces
    Presentation/  Cameras, leaderboards, counters, commentary triggers, VFX hooks
    Capture/       Recording, resolution/aspect switching, batch run harness
    Simulations/
      Race/        Isolated per-format logic + its config type
      Wars/
      ReleaseOrMultiply/
      Elimination/
      Survival/
      Cricket/
  Settings/        URP assets — PC_* for long form, Mobile_* as the cheap preview path
KnowledgeBase/     Format briefs, channel strategy, production notes (NOT imported by Unity)
plans/
  blocks/          Reusable sub-plans — composed into video plans, never copied
  videos/          One plan per video
PROJECTS.md        Live index: what's built, what's published, what it earned, what's next
```

Keep `KnowledgeBase/` outside `Assets/` on purpose — no `.meta` churn, no import cost.

## Architecture: modular, reusable, isolated by format

This is the governing technical constraint. The channel lives or dies on being able to ship a new
video cheaply *without* a change to one format breaking another. Three layers, one direction of
dependency:

```
Core  ←  Shared  ←  Simulations/<Format>
  ↑         ↑
  └── Presentation, Capture ──┘
```

**Rules, in priority order:**

1. **Dependencies point downward only.** `Core` knows nothing. `Shared`/`Presentation`/`Capture` may
   use `Core`. A format may use anything below it. Nothing below ever references a format.
2. **Formats never reference each other.** `Wars` must not `using SimulationLobby.Simulations.Race`.
   If two formats need the same thing, it gets **promoted into `Shared`** — that is the only legal
   way code travels between formats. Copy-paste into a second format is a signal to promote, not a fix.
3. **Enforce isolation with assembly definitions.** One `.asmdef` per folder above. The compiler,
   not discipline, keeps formats apart; a wrong reference should fail to build. `elim` is the one
   deliberate exception — it *wraps* other formats, so it may reference them (see below).
4. **Compose behavior, don't subclass it.** A contender is a GameObject with small, single-purpose
   components (identity/color, trail, collision response, elimination). New formats are new
   *combinations*, not new inheritance branches.
5. **Every format exposes the same lifecycle**, so `Capture` can batch-run any of them blind:
   `Initialize(seed, config) → Tick(fixedDelta) → IsComplete → Result`. Drive it through one
   interface (`ISimulation`) and the batch harness never needs a per-format branch.
6. **Config is data, per format.** Each format defines its own `ScriptableObject` config deriving
   from a shared base. A published video = scene + config asset + seed. No inline tunables.
7. **Presentation reads, never writes.** Leaderboards, counters, and cameras observe simulation
   state; they must not mutate it. This keeps determinism intact whether rendering or headless.

**On `elim`:** it's an orchestrator, not a peer. It composes heats from `Race`/`Wars` through
`ISimulation` and the bracket logic never touches their internals. If it needs to reach inside one,
the interface is wrong — fix the interface.

**The test for "is this in the right place":** could I delete an entire `Simulations/<Format>` folder
and have everything else still compile? If not, something leaked.

## Conventions

- Namespace everything under `SimulationLobby.<Area>` (e.g. `SimulationLobby.Simulations`).
- Simulation logic lives in `FixedUpdate`; camera, UI, and VFX live in `Update`/`LateUpdate`. Never mix.
- Tunables go in `ScriptableObject` config assets, not inline constants — a format is re-used across
  dozens of videos and only the config should change between them.
- All randomness routes through the run's seeded `System.Random`. Never call `UnityEngine.Random`
  in simulation code; it breaks reproducibility.
- Two aspect targets: **9:16 @ 1080x1920** (Shorts) and **16:9 @ 1920x1080** (long form).
  Anything that must be read on screen gets checked at both.
- **Editor menu layout.** Every `[MenuItem]` goes under `Simulation Lobby/2D Formats/<Format> (<slug>)/`,
  `Simulation Lobby/3D Formats/<Format> (<slug>)/` or `Simulation Lobby/Tools/`. Use readable item names
  (`Rotating Gap`, `Ellipse Stretch (conv-002)`), not scene file names; the builder logs the scene path. Pass a
  priority: builds first (2D at 1xx, 3D at 2xx, consecutive within a format), then that format's tools (reset,
  asset helpers) 20+ higher, so Unity draws a separator. Tools sit at 9xx. Docs write paths with `▸`.
- **Length targets are in *Content guidelines* below** (Shorts 1-2 min, long form well past that). Within
  that band, length is whatever keeps the footage tense. For escalation formats, tune for *event count*
  first (bounces, near-misses, rounds) and total length second: bounces are the content.

## 3D scenes and "settled" completion conditions

Learned building the first 3D format — prototyped as `impact`, shipped as `surv` *Wall vs Ball*
(`KnowledgeBase/formats/survival-wall.md`):

- **Bare primitives on a flat background color look like a placeholder, not a video.** A 3D scene
  needs, at minimum: a floor/ground plane (grounding + a shadow-catching surface), a fill/rim light
  in addition to the key light, and a depth cue (linear fog, or `AmbientDriftField` reused from
  `esc` for ambient dust) — a single directional light against a solid background reads as flat and
  empty. Treat these as part of every 3D scene's baseline, not optional polish.
- **A "run until everything settles" completion condition needs an explicit start gate.** A field of
  rigidbodies at rest *before anything has happened to it* already satisfies "all velocities below
  threshold" — without gating the settle-tracker on the triggering event actually having occurred
  (e.g. an `HasLaunched`-style flag), the run can complete before the interesting part even starts.
  This is a general gotcha for any future format using a settle/rest condition, not just `surv`.
  Sharper still: gate on the *outcome* (the ball is spent — hit and slowed, or missed and gone),
  not the trigger — the field is motionless for the whole flight.
- **Give every video a setup/anticipation beat before its payoff**, not just an instant cut to the
  action — a config-driven hold (e.g. `preLaunchHoldSeconds`) where the scene sits static and lit
  before the triggering event reads as a deliberate choice, and gives an approach/whoosh audio cue
  somewhere to build.
- **Procedural audio is the house style for *all* sound, not just melodies.** `Presentation/Audio/
  ProceduralToneBank` generates tones (`CreateTone`/`CreateScale`) *and* percussive/noise effects
  (`CreateImpactThud`/`CreateThudBank`/`CreateWhoosh`) at runtime — nothing sampled, nothing to
  license, same reasoning as the bounce melody. Reach for it before reaching for an audio asset.
- **3D collision counting needs its own detector**, `Shared/Physics3D/CollisionDetector3D` — the 3D
  counterpart to `BounceDetector2D`, but deliberately *not* a literal port: it counts every distinct
  collision within a tick rather than collapsing each tick to at most one, because a fast object
  plowing through a packed field can hit several different bodies in a single fixed step, and
  `esc`'s "one bounce per tick" assumption (built for a single ball against a single wall) would
  silently drop most of the hits a multi-object format needs to react to.

- **A generated mesh that changes shape must be saved as a fresh asset, never overwritten in place.**
  `StudioSetBuilder.SaveMesh` copies into the existing asset to keep its GUID; after a layout change the
  editor kept *drawing the previous build's mesh* while physics and obstacles used the new data, so pegs and
  marbles hovered over a track that was no longer where it was drawn (`race` course v2). `race` uses its own
  delete-then-create `SaveMeshFresh`. If a scene's visuals and physics disagree, suspect this first: read the
  mesh back with `Instantiate(mesh)` on a temp camera render to tell stale display data from bad geometry.

## On-screen UI

All HUD, scores and counters use a **uGUI Canvas with TextMeshPro** — never world-space `TextMesh`,
never text parented into the arena. World-space text has to be re-placed by hand for each aspect and
renders soft when scaled; a canvas anchored to screen edges reads correctly at both targets from the
same scene.

- `Screen Space - Overlay` canvas, `CanvasScaler` set to *Scale With Screen Size*, reference
  resolution **1080x1920**, `matchWidthOrHeight = 1` (match height — the Shorts frame is
  height-dominant; matching width shrinks everything when the same scene renders 16:9).
- **Top panel:** the question (the hook, stated as a question) and the versus score.
- **Bottom panel:** the current round's result and the live counter.
- **Arena decals are the one exception.** Text that belongs to a physical object (a number painted on a board,
  a slot-chip label, a drum face) is a world-space **TextMeshPro** (3D) decal parented to that object, so it stays
  perspective-correct and sized to its object. Screen-projected labels over busy 3D boards overlapped in `terr` v2.
  Legacy `TextMesh` stays banned, and HUD text (captions, scores, clocks, bars) stays on the canvas.
- Keeping the arena clear between the two panels is the point — the ball must never be behind text.
- **Versus scores use a `HorizontalLayoutGroup` with `childControlWidth`, `childControlHeight`,
  `childForceExpandWidth` and `childForceExpandHeight` all on.** Equal-width sides keep the score
  symmetrical instead of shifting as digits change.
- Score is framed as a **contest between two named sides** (`WALL vs BALL`), not a bare tally — it
  gives the viewer someone to root for in a format with no competitors.
- Presentation reads simulation state in `Update`/`LateUpdate` and never writes it. The HUD component
  lives in `Presentation` and stays format-agnostic ("left side", "right side", "a question"); a thin
  binder in the format pushes values into it, because `Presentation` must never reference a format.

## Camera movement (learned on `mach-002`, rejected four times before it was smooth)

A camera that follows physics bodies looks choppy unless every input to it is smooth by construction. The `mach-002` rework
(`plans/videos/mach-002-camera-rework.md`, `RaceCameraCore`/`CourseTrack`/`CameraSpring`) fixed what four hand-tuned versions could not.
Reuse these rules for any new follow camera:

- **Never aim at raw body positions.** Marbles bounce, tumble in helices and swap rank every frame, so a target built from them steps by
  metres. Follow a smoothed position along the course (arc length on a pre-smoothed rail) and add only a heavily filtered offset
  toward the real bodies.
- **Take heading from the course tangent, never from per-frame velocity.** Differenced velocity makes the camera orbit with every
  helix and vortex swirl.
- **One pose state, critically damped springs, hard limits.** Position, yaw, pitch and distance each get their own spring with a velocity
  and acceleration cap, so no input can cause a jerk. Never run separate `SmoothDamp`s with different time constants per shot: a
  `SmoothDamp` on a step target accelerates instantly (a 15 m step with a 1.2 s time constant is about 40 m/s², which reads as a lurch).
- **Blend, never switch.** Set pieces (a lift, a jump, a funnel) are zones defined as ranges along the course, blended in and out over
  1.5-3 s in pose space with smoothstep weights and a minimum dwell. Hysteresis on the leader: it only changes when another body has led by
  a margin for a second or so. Bodies that teleport or fly (a recycle cannon) are excluded from the focus for several seconds.
- **No per-frame obstruction clamp.** A sphere-cast that pulls the camera in at a fixed speed pops constantly in a machine full of glass
  and walls, and it is what pulled the camera too close. Choose the side and elevation per zone up front; let obstruction only nudge
  pitch and distance through the same rate-limited springs; ignore glass; never move the camera position directly.
- **Distance from the spread, not from a guess.** Fit the framing to the pack's extent with the real FOV and aspect, so followers stay
  visible; clamp it (here 11-32 m for 0.4 m marbles on a 50 m machine). A 0.4 m marble is about 57 px at 11 m and 20 px at 32 m on 1080p.
- **Frame-rate independent.** Step the filters in fixed substeps (about 120 Hz) from a clamped `dt`, with exact critically damped
  solutions while no limit binds, and build the target from the end-of-frame filter state (building it from the start-of-frame state adds
  a dt-dependent lag that shows as jerk under irregular editor frames). Offline 60 fps capture then replays identically.
- **Keep the math a pure class.** `RaceCameraCore` has no MonoBehaviour, `Physics` or `Time`, so it can be run over a recorded or
  synthetic trace. A thin `MonoBehaviour` feeds it `IRaceState` and applies the pose. Presentation reads, never writes.
- **Prove smoothness with numbers, not by eye.** Run the offline harness (Simulation Lobby ▸ Tools ▸ Race Camera Smoothness Test) at 60 fps,
  30 fps and jittery frame times and require: 0 pops, max turn rate <= 25 deg/s, max acceleration <= 10 m/s², jerk p99 <= 40 m/s³, the
  leader in the safe area >= 97% of frames, all of the top 3 in frame >= 85%. The first version passing these also looked smooth live.
- **Tune from the course, not from one clip.** The real course turns about 2 deg per metre; a yaw cap of 11 deg/s was needed where the plan's
  18 deg/s kept saturating. Re-run the harness on several seeds after any change.

Related 3D lessons from the same build: **size every station for the real marble** (`mach` was authored for 0.13 m balls; a 0.4 m
racer needs gaps, throats and pitches rescaled), **fillet chute corners to a radius over half the chute width** (a tighter bend folds the
inner wall into a hole), and **test with a stream of fast balls in an isolated physics scene**
(`SceneManager.CreateScene(..., LocalPhysicsMode.Physics3D)` plus `PhysicsScene.Simulate`; `TrackContinuityTest` does this for chutes)
instead of waiting for the user to spot a jam. The game freezes while the Unity window is unfocused, so live state can be read but not sampled over time.

## Content guidelines

Every video is judged by one thing: **can a viewer who swiped in 2 seconds ago, sound off, follow it?**

- **Length.** **Shorts: 1-2 minutes.** Shorter than 1 min means the idea needs more rounds, variants or
  escalation, not a faster cut. **Long form: substantially longer**, well past 3 min (our first, `terr`, is
  15 min). It's built around one long arc with several acts, not a Short stretched out or a compilation
  (compilations on young channels get nothing: `KnowledgeBase/learnings.md`).
- **Narrative and plot come first.** The viewer must always know what they're watching for, what just changed
  and why the result happened. Follow the *Story legibility* rules in `KnowledgeBase/learnings.md`:
  - The question names things that are visible from frame one.
  - Every cause happens on screen (animated, big enough at 480px, before/after visible).
  - One question per video. Rounds repeat or escalate it; they never add a new question.
  - Show only the number that is the answer. A live counter never contradicts the result.
  - Close with the one-line *why* when the format is a curiosity.
- **Storyboard before code.** Every plan opens with the storyboard table and cold-viewer checklist from
  `plans/TEMPLATE.md`. Read it as a stranger before building.
- **Shorts structure:** hook question in the first second, a guess or tension beat before each payoff, and a
  payoff every 5-10 s. **Long form:** a stated stake up front, acts that raise it, a midpoint twist, and
  recurring characters or teams to follow.

## Publishing metadata (title, description, tags)

When a video is ready, or whenever asked for its title or description, give **copy-paste code blocks**
without being asked twice. The user pastes them straight into YouTube Studio.

- **Standalone.** Write as if this is the only video that exists. Never reference other videos, earlier
  versions or internal names ("same question", "part 2 of", slugs like `conv-002`, "v2").
- **Verify lengths with a script** (`python -c "print(len(...))"`) before handing over. Don't estimate.

**Shorts**
1. **Title**, max 100 chars, hashtags included. A question naming what's on screen, a hook word in CAPS at
   most once, one emoji at most, and ending in 2-3 hashtags, `#shorts` last
   (e.g. `... Do They STILL All Hit Blue? 🎯 #physics #math #shorts`).
2. **Description**, under 500 chars, hashtags included:
   - Line 1 restates the hook in plain words, without spoiling the twist.
   - Then 1-2 sentences of *why* (real search terms: the concept's proper name).
   - Then a comment prompt ("How many did you guess right? 👇").
   - Then 5-8 hashtags; the first 3 show above the title, so put the most relevant first.
3. **Tags** (the Studio Tags field), comma-separated, under 500 chars. Lead with the concept's search
   terms and misspellings, then genre terms (`physics simulation`, `satisfying`), and end with `simulation lobby`.

**Long form**
1. **Title**, max 100 chars, keeping the hook in the first ~60 (that's what shows in browse). **No hashtags** in the
   title and no `#shorts`. A stake or a contest ("Which Color Takes the Board?") beats a label.
2. **Description**, up to ~1,500 chars:
   - The first 2 lines are the hook (shown above "more").
   - A short paragraph on what happens, without spoiling the ending.
   - **Chapters** (`0:00 Intro`, ... from the plan's beat times).
   - A comment prompt.
   - 3-5 hashtags at the end.
3. **Tags**, comma-separated, under 500 chars. Same rules as for Shorts.

Also give a **pinned comment** suggestion. Remind the user to record the logged seed in the video's plan.

## Content formats (shorthand used in commits and docs)

| Slug | Format | Typical length |
|---|---|---|
| `race` | Marble race down a track, first-past-post | Short or long |
| `wars` | Teams/factions colliding, last team standing | Long |
| `rom` | Release or Multiply — choice gates that split or merge counts | Short |
| `elim` | Bracket / round-based elimination | Long |
| `surv` | Survival — something endures an escalating attack (first build: *Wall vs Ball*, guess the hits) | Either |
| `esc` | Containment/escape — one ball vs a boundary, escalating each bounce | Short |
| `brk` | Ring breaker — ball breaks out through nested rings of breakable segments, one ring at a time | Short |
| `impact` | Single-event spectacle — one thing meets another, once (`idea`; rigid scatter proved too short) | Short |
| `terr` | Territory — four colours, cannons fed by multiply/release Plinko boards, last colour standing (`building`, 3D) | Long |
| `cric` | Cricket match on a multiply/release board, two international teams, wagon-wheel shots (`built`, first sport, experiment) | Short |
| `mve` | Multiply vs Escape — every wall bounce clones the ball, every escape widens the exit; fill up or empty out (`built`, experiment) | Short |
| `conv` | Math convergence — many balls, one "will they all meet?" prediction with a certain, surprising answer (`built`, experiment, new genre) | Short |
| `brach` | Brachistochrone — same start, same finish, which track wins? The cycloid, always (`built`, maths) | Short |
| `clack` | Pi clacks — a big block slides into a small one against a wall; the clack count spells π as mass goes ×100 (`built`, experiment, maths) | Short |
| `fall` | Freefall Symphony — a marble falls through worlds and every bounce plays the next note of a song; pads are solved from the music (`building`, 3D, hypnotic/sound-on) | Long + Short cuts |
| `press` | Hydraulic press — every crushed ball splits in 2; how many balls before the press stops, as it gets 2× stronger (`built`, experiment) | Short |
| `royale` | Color Rumble — four colours in one spinning ring; the EXIT drops marbles out, the gold x2 arc clones them; last colour standing wins (`built`, experiment) | Short |
| `bell` | Bell Curve Drop — marbles through a coin-flip peg board pile into a bell curve; 100 vs 1,000 vs a rigged board (`built`, maths) | Short |
| `mach` | Marble Machine — a giant 3D marble machine loop (lifts, drops, a ski jump, a colour sorter); which colour fills its glass tube first? (`building`, 3D, style from @ball_simulations) | Long |

Details and per-format hooks live in `KnowledgeBase/formats/`.

## Planning: compose, don't rewrite

Every video gets a plan in `plans/videos/`. Plans are **composed from reusable blocks** in
`plans/blocks/` — the same share-downward/isolate-sideways rule the code follows, applied to process.

- A video plan holds only what's unique to that video. Shared steps are *referenced*, never copied.
- Writing the same steps into a second video plan means **extract a block** — same move as promoting
  code into `Shared`.
- Plans should get shorter over time. The first video of a format is long because it builds blocks;
  the fifth should be mostly checkboxes. If plan length isn't dropping, blocks aren't being extracted.
- Blocks map onto code layers (`Core`/`Shared`/`Presentation`/`Capture`). A block that maps to no
  reusable code location is usually video-specific work in disguise.

`PROJECTS.md` is the live index — build state per format, shared-block state, published videos and
their views. Update it at every build milestone and after every retro; it's what "what do we build
next" gets answered from.

## Channel data

The channel is **[@simulationlobby](https://youtube.com/@simulationlobby)** (`UCRyMyxpvggZYYIpwn-DtrlQ`).

Two skills pull live data via the YouTube Data API (key in `.env`, gitignored — never print it):

- **`youtube`** — our channel's stats, and `report` output for `PROJECTS.md`
- **`youtube-research`** — reference channels in the genre, for `KnowledgeBase/reference-channels.md`

**An API key returns public data only.** Views, likes, comments — yes. Retention, avg % viewed,
drop-off, traffic sources — **no**, those need the Analytics API with OAuth (not set up). When asked
about retention, say it needs YouTube Studio; don't substitute like-rate as a proxy.

The channel is small (2 videos, 26 views, dormant since May 2025). **Differences at this scale are
noise.** Don't build format conclusions on single-digit view deltas.

**Learnings feed strategy.** Whenever `KnowledgeBase/learnings.md` is updated, check whether the new finding
changes the monetisation goal or the strategy. If it does, update `KnowledgeBase/channel-strategy.md` (and the
*Next pick* notes in `PROJECTS.md`) in the same pass and tell the user what changed and why. Write it as a dated,
tiered note (lead / validated, like `learnings.md`). Don't silently overwrite a decision the user made: propose
the change, mark it *proposed*, and leave the decided text in place until they confirm.

**Fresh stats trigger the whole chain.** Whenever latest stats are fetched (`youtube` or `youtube-research`),
don't stop at reporting numbers. Work through these in order, in the same pass, and only touch a doc when the new
data actually earns a change:

1. **`PROJECTS.md`**: views and earnings per published video, and the stats snapshot.
2. **`KnowledgeBase/learnings.md`**: new findings (dated, tiered lead / validated). Respect the noise rule above:
   single-digit deltas are not findings.
3. **`KnowledgeBase/channel-strategy.md`**: per *Learnings feed strategy*.
4. **Goals** (`channel-strategy.md` → *The goal: monetization*): progress toward YPP (subs, Shorts views/90d,
   watch hours) and whether the timeline still holds.
5. **`KnowledgeBase/ideas-backlog.md`**: promote ideas the data favours, mark `[dead]` the ones it rules out,
   add new ones the reference channels suggest.

Finish with a short summary of what changed in each doc and why, including any that were checked and left as is.
User-made decisions are still proposed, never overwritten.

## Working agreements

- Read the relevant `KnowledgeBase/` doc before building a format; write findings back into it when
  a run teaches something (what retained, what confused viewers, what tuning felt right).
- Start from `plans/TEMPLATE.md` for a new video. Check its blocks exist before starting to build.
- Prefer adding a new scene over branching an existing format with flags. A flag that means "behave
  like the other format" is the clearest sign a piece belongs in `Shared`.
- Don't commit rendered video — `.gitignore` excludes it. Commit seeds and configs instead; footage
  is regenerable.
- **Seeds are random by default; we don't hunt for a best seed.** Scenes ship with
  `randomizeSeedOnStart` on, so each Play is a different run, and the runner logs the drawn seed at
  the start. Determinism stays (fixed timestep, seeded RNG) for one reason only: a take worth keeping
  can be re-rendered by pasting its logged seed back in. The seed-scan harness is opt-in tooling, not
  a step in making a video. Record the logged seed with the published video; don't scan for one.
- When asked to "make a video", the deliverable is a scene + config + seed that renders correctly,
  not just code that compiles.
