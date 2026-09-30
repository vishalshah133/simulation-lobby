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
- **Shorts can run up to 3 minutes** (the old 60s cap is gone). Don't compress a format to fit 60s —
  length should be whatever keeps the footage tense. For escalation formats this means tuning for
  *event count* first (bounces, near-misses, rounds) and total length second: bounces are the content,
  and starving a round of them to hit an obsolete limit trades away the whole appeal.

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
- Keeping the arena clear between the two panels is the point — the ball must never be behind text.
- **Versus scores use a `HorizontalLayoutGroup` with `childControlWidth`, `childControlHeight`,
  `childForceExpandWidth` and `childForceExpandHeight` all on.** Equal-width sides keep the score
  symmetrical instead of shifting as digits change.
- Score is framed as a **contest between two named sides** (`WALL vs BALL`), not a bare tally — it
  gives the viewer someone to root for in a format with no competitors.
- Presentation reads simulation state in `Update`/`LateUpdate` and never writes it. The HUD component
  lives in `Presentation` and stays format-agnostic ("left side", "right side", "a question"); a thin
  binder in the format pushes values into it, because `Presentation` must never reference a format.

## Content formats (shorthand used in commits and docs)

| Slug | Format | Typical length |
|---|---|---|
| `race` | Marble race down a track, first-past-post | Short or long |
| `wars` | Teams/factions colliding, last team standing | Long |
| `rom` | Release or Multiply — choice gates that split or merge counts | Short |
| `elim` | Bracket / round-based elimination | Long |
| `surv` | Survival — something endures an escalating attack (first build: *Wall vs Ball*, guess the hits) | Either |
| `esc` | Containment/escape — one ball vs a boundary, escalating each bounce | Short |
| `impact` | Single-event spectacle — one thing meets another, once (`idea`; rigid scatter proved too short) | Short |

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

## Working agreements

- Read the relevant `KnowledgeBase/` doc before building a format; write findings back into it when
  a run teaches something (what retained, what confused viewers, what tuning felt right).
- Start from `plans/TEMPLATE.md` for a new video. Check its blocks exist before starting to build.
- Prefer adding a new scene over branching an existing format with flags. A flag that means "behave
  like the other format" is the clearest sign a piece belongs in `Shared`.
- Don't commit rendered video — `.gitignore` excludes it. Commit seeds and configs instead; footage
  is regenerable.
- When asked to "make a video", the deliverable is a scene + config + seed that renders correctly,
  not just code that compiles.
