# elim-001: Marble Elimination Show #1

**Format:** `elim` (3D, wraps `race` heats) · **Length:** ~11-12 min (band 10-15) · **Aspect:** 16:9 1920x1080 · **Status:** `built` 2026-10-18 (compiles; not yet played)
**Brief:** `KnowledgeBase/formats/elimination-and-survival.md` → *Elimination* · **Reference:** Crazy Marble Race *24 Marble Race EP. n*
(`reference-channels.md` → *@carsonjay and @crazymarblerace*), MIKAN team elimination series
**Scene:** `Elimination_Show_3D` (**Simulation Lobby ▸ 3D Formats ▸ Elimination (elim) ▸ Elimination Show (elim-001)**, priority 271)
**Config:** `Assets/Settings/Configs/Elimination/EliminationConfig_001.asset` · **Seed:** random per Play, logged at start
**Working title:** *16 Marbles, 6 Races, 1 Champion: Which Marble Survives? Elimination Show #1*

## Premise

Sixteen named marbles. Six races on six different tracks. In every race the last ones across the line are out. Halfway
through, the knocked-out marbles get **one last chance** to fight their way back in. **Which marble survives it all?**

Why this video: the strongest long-form series template we've found (`channel-strategy.md` → *10-18 read*). The rules
never change between episodes, so episode 2 onward costs a new theme and a seed, not a build. That's the cadence the
long-form track needs. It's also the format the brief has always called "long-form native".

## The show (fixed rules: these never change between episodes)

| Heat | Name | Entrants | Rule | After |
|---|---|---|---|---|
| 1 | THE GRAND PRIX | 16 | First 12 across are safe, last 4 out | 12 alive |
| 2 | PEG FALL | 12 | First 8 safe, last 4 out | 8 alive, 8 out |
| 3 | **LAST CHANCE** (midpoint twist) | the 8 that are out | **Winner comes back** | 9 alive |
| 4 | PINWHEELS | 9 | First 6 safe, last 3 out | 6 alive |
| 5 | THE HELIX | 6 | First 3 safe, last 3 out | 3 alive |
| 6 | **FINAL: THE GAUNTLET** | 3 | Winner is the champion | 1 |

**A heat ends the moment the last safe spot is taken** (plus a short grace so the camera sees it). Anyone still on
the track is out. The tension is the race for the last safe spot, not the race for first.

## Storyboard

| Beat | On screen | Caption (one at a time) |
|---|---|---|
| Hook 0:00 | The **board**: 16 name tiles in their colours, 4×4, and the marbles lined up in the pen behind it | **WHICH MARBLE SURVIVES?** · `16 MARBLES · 6 RACES` · `PICK ONE!` |
| Rules 0:12 | The board stays; three rule lines punch in one by one | `LAST ONES OUT` · `ONE LAST CHANCE` · `1 CHAMPION` |
| Heat intro (each) | Fly-in on the heat's track, then the pen | `RACE 1 · THE GRAND PRIX` · `TOP 12 SAFE` |
| Race | Chase camera; leaderboard on the right with a **red cut line** under the last safe spot; rows below it glow red | `SAFE 7 / 12` (live, bottom-left) |
| Last spot | The last safe marble crosses: slow push-in, a gong | `LAST SPOT: TOPAZ!` |
| Out | The board returns; the out tiles get a red **OUT** stamp, one by one, with a thud | `4 OUT` |
| **Twist** (after heat 2, ~4:30) | Out tiles shake and glow | `THE OUT MARBLES...` · `GET ONE LAST CHANCE!` |
| Last Chance | 8 out marbles race, one winner | `WINNER COMES BACK` |
| Return | The winner's tile loses its OUT stamp and flies back into the board | `RUBY IS BACK!` |
| Final | 3 marbles, the longest track | `FINAL · THE GAUNTLET` |
| Payoff | Winner crosses: confetti in its colour, the board with one gold tile | **`CHAMPION: SAPPHIRE!`** · `DID YOURS WIN? 👇` |

Cold-viewer check (sound off):
- [x] The question names things visible from frame one: 16 named marbles on the board and in the pen
- [x] Every cause happens on screen: the cut line on the leaderboard, the last safe marble crossing, the OUT stamps
- [x] One question all video. Every heat raises the stakes; the twist changes who can still win, not the question
- [x] Numbers shown are the answer's support only: `SAFE n / N` during a heat, `n OUT` after it
- [x] Not a curiosity format, so the champion caption is the close

**HUD rule (easy to consume):** one caption at a time, **at most 4 words**, bold, ≥ 80 px at the 1080x1920 reference
(match height, so it scales for 16:9), on a dark plate. Each caption holds ≥ 1.5 s. The leaderboard shows **names only**
(no times, no gaps), the cut line in red, and **at most 16 rows** with the rows below the cut tinted red. Board tiles show
the name in ≥ 56 px. Nothing else on screen.

**Length check:** intro 0:25 + 6 heats × (intro 0:12 + race ~1:15-1:40 + result 0:15) + twist 0:15 + payoff 0:20 ≈
**11-12 min**. Inside the decided 10-12 target and the 10-15 band. Heat 1 is the longest (16 marbles); the final is
the longest *track*. If a Play runs short, lengthen tracks, not holds.

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md): fixed tick, seeded `System.Random`, `ISimulation`
- [ ] **REUSE** [track-course](../blocks/track-course.md): `TrackLayout`, `TrackMeshBuilder`, `ObstacleBuilder`, `SaveMeshFresh`
- [ ] **REUSE** [studio-look-3d](../blocks/studio-look-3d.md): floor, key + rim + fill, fog/dust baseline (via the race rig)
- [ ] **REUSE** `race` as the heat (`RaceSimulation`, `RacePresenter`, `ChaseCameraRig`, `LeaderboardHud`), through a new interface
- [ ] **BUILD** `Shared/Contenders/IHeatSimulation`: the heat contract `elim` drives. Any future format that can run as a heat implements it
- [ ] **BUILD** `Presentation/UI/EliminationBoardHud`: format-agnostic tile board (name, colour, state)
- [ ] **BUILD** the `elim` format: config, orchestrator simulation, presenter, scene builder, 5 new track layouts

## Architecture (contract between the two build packages)

**The isolation rule for this format:** `elim`'s *runtime* assembly must **not** reference `Race`. It only knows
`IHeatSimulation` and a rig `GameObject`. Only its *editor* assembly references `Race` (to build race rigs into the
scene). That's the CLAUDE.md "elim wraps other formats through `ISimulation`" rule, enforced by the compiler.

### Package C: the heat interface + race adaptation (owns `Shared/Contenders/*`, `Simulations/Race/**`, `Presentation/UI/LeaderboardHud.cs`)

```csharp
namespace SimulationLobby.Shared {
    [System.Serializable] public struct HeatEntrant {
        public string name; public Color color; public int rosterIndex;
        public HeatEntrant(string name, Color color, int rosterIndex);
    }
    /// A format that can run as one heat inside an orchestrator (elim).
    public interface IHeatSimulation : SimulationLobby.Core.ISimulation {
        /// Call before Initialize. entrants == null → the format's own roster. safeCount <= 0 → normal finish rule.
        /// safeCount > 0 → the heat completes once safeCount entrants have finished (+ the format's cut grace);
        /// Result.standings is then finish order for finishers, then progress order for the rest.
        void ConfigureHeat(System.Collections.Generic.IReadOnlyList<HeatEntrant> entrants, int safeCount);
        int SafeCount { get; }
        int EntrantCount { get; }
        int FinishedCount { get; }
    }
}
```

- `RaceSimulation : IHeatSimulation`. `ConfigureHeat` stores entrants/safeCount; `SpawnField` uses them instead of
  `config.roster` when set. With `safeCount > 0`, complete `RaceConfig.cutGraceSeconds` (new, default 1.5) after the
  safe-count-th finish, instead of `finishGraceSeconds`. **With `ConfigureHeat` never called, race behaviour is
  byte-identical to today** (race-001 same seed → same `RunResult` JSON).
- Pen sizing must fit 16 entrants (`gridColumns` 4 → 4 rows); check the start pen in `RaceSceneBuilder` is long enough.
- `LeaderboardHud.SetCutLine(int safeCount)` (0 = off): a red line under row `safeCount`, rows below it tinted red.
  Plus `showNotes` (bool) so a heat can hide the time/gap notes (names only). `RacePresenter` pushes `SafeCount` every
  `Update` from its simulation. Existing race scene unchanged when `SafeCount == 0`.
- **Editor API for the rig builder** (in the existing Race editor assembly), extracted from `RaceSceneBuilder` so the
  Grand Prix scene builds through it too:

```csharp
namespace SimulationLobby.Simulations.Race.EditorTools {   // match the existing Race editor namespace if it differs
    public sealed class RaceRig {
        public GameObject root;            // everything for one heat: track, obstacles, pen, gate, finish, marble template,
                                           // RaceSimulation, RacePresenter, ChaseCameraRig + its Camera, leaderboard canvas, audio
        public RaceSimulation simulation; public Camera camera; public Transform startOrigin, finishLine;
    }
    public static class RaceRigBuilder {
        /// Builds one rig under `parent` at `origin`. meshFolder: where SaveMeshFresh writes this rig's meshes
        /// (unique per rig). Does NOT add a SimulationRunner (the orchestrator ticks the heat).
        public static RaceRig BuildRig(TrackLayout layout, RaceConfig config, Vector3 origin, Transform parent,
                                       string title, string meshFolder);
    }
}
```

The lights, floor and environment stay scene-level (the caller builds them once). The rig's camera and leaderboard
canvas are children of `root`, so activating `root` makes that heat the one on screen.

### As built (package C)

Built 2026-10-18. Compile-checked (Race.Editor, Presentation, Survival.Editor, Territory.Editor: 0 errors). Not run in Unity.

**Runtime (`SimulationLobby.Shared`, `SimulationLobby.Simulations.Race`, `SimulationLobby.Presentation`)**

```csharp
// Shared/Contenders/HeatEntrant.cs, IHeatSimulation.cs: exactly as the contract above.
// Race/RaceSimulation.cs
public sealed class RaceSimulation : MonoBehaviour, IHeatSimulation      // RequireComponent(SimulationRunner) REMOVED
{
    public void ConfigureHeat(IReadOnlyList<HeatEntrant> entrants, int safeCount); // ArgumentException on <2 entrants or duplicate names
    public int SafeCount { get; }          // 0 until configured
    public int EntrantCount { get; }       // configured entrants, else config.roster.Length
    public int FinishedCount { get; }
    public int InitializeCount { get; }    // +1 on every Initialize; presentation uses it to detect a new heat
    // existing: Entries, Phase, Leader, ElapsedSeconds, Released / MarbleFinished / LeaderChanged events, Result ...
}
public sealed class RaceEntry { ... public int slot; }  // NEW: index in Entries. identity.Index is the ROSTER index (can exceed the heat size)
// Race/RaceConfig.cs
public float cutGraceSeconds = 1.5f;   // heat mode: run-on after the safeCount-th finish
public int   maxEntrants     = 16;     // pen and leaderboard are sized for this many
// Presentation
LeaderboardHud.SetCutLine(int safeCount);               // 0 = off; red line under row safeCount, rows below tinted red
LeaderboardHud.ShowNotes { get; set; } / SetShowNotes(bool)   // false = names only
LeaderboardHud.Configure(int count)                     // count > fullSizeRowLimit (12) -> row pitch 54 (16 rows fit 1080)
ChaseCameraRig.ResetTracking(); ChaseCameraRig.SetHeading(Vector3);   // new, used by RacePresenter on a new heat
```

- Heat behaviour: with `safeCount > 0` the race completes `cutGraceSeconds` after the tick the `safeCount`-th marble
  finishes (or when everyone is in). `Result.standings` = finishers in finish order, then the rest by progress (the
  existing `RankOrder`). `identity.Index = HeatEntrant.rosterIndex`, so standings names map back to the roster by name and
  `Entries[i].identity.Index` gives the roster index.
- `Initialize` may be called again on the same component: it deactivates then destroys the old marbles (so the new field
  does not collide with them in the same frame), re-collects and re-phases spinners, reopens the gate and resets all
  counters. `ConfigureHeat` persists across `Initialize` calls; `ConfigureHeat(null, 0)` restores stand-alone behaviour.
  Two entrants crossing in the same tick are placed in grid-slot order (existing behaviour).
- `RacePresenter` re-syncs whenever `InitializeCount` changes and on `OnEnable`: it reconfigures the leaderboard, hides any
  banner, resets the camera and per-run flags, and if joined mid-run restores finish notes and chases the front runner. It
  keys leaderboard rows by `RaceEntry.slot`, never by `identity.Index`. Every `Update` it pushes `SetCutLine(SafeCount)` and
  `SetShowNotes(SafeCount <= 0)`.
- **Default-path guarantee (ConfigureHeat never called):** `_entrants == null` and `_safeCount == 0` select the old code
  everywhere. The roster is `config.roster`, `identity.Index == i`, the slot-shuffle / jitter / spinner-phase RNG draws are
  the same calls in the same order (no draw added or moved), the completion test is the old
  `everyoneIn || finishGraceSeconds` expression, and events and metrics are untouched. The only added behaviour on that path
  is `SetActive(false)` on children before `Destroy` (none exist on a first `Initialize`). The leaderboard is unchanged at
  `SafeCount == 0`: no cut-line object is created, notes stay on, row pitch stays 62 for up to 12 rows.
- Pen: 16 marbles need 4 rows, about 1.5 m behind the gate. The Grand Prix `gateDistance` is 4.4 m, so no geometry changed.
  `RaceRigBuilder` uses `max(layout.gateDistance, RequiredPenLength(config))` and logs a warning if it had to lengthen it.
  **Package D layouts should keep `gateDistance` at the default 4.4 (anything >= 1.6 is fine).**

**Editor (`SimulationLobby.Simulations.Race.EditorTools`, in the Race editor assembly)**

```csharp
public sealed class RaceRig {
    public GameObject root;                 // carries RaceSimulation (IHeatSimulation) + RacePresenter; NO SimulationRunner
    public RaceSimulation simulation; public Camera camera;
    public Transform startOrigin, finishLine;
    public RacePresenter presenter; public LeaderboardHud hud; public ChaseCameraRig cameraRig;
    public Bounds bounds;                   // of the centreline, world space
    public List<TrackMeshBuilder.Sample> samples; public float totalLength, finishDistance, lift;
}
public static class RaceRigBuilder {
    public static RaceRig BuildRig(TrackLayout layout, RaceConfig config, Vector3 origin, Transform parent,
        string title, string meshFolder, string meshPrefix = "Race", int leaderboardRows = 0);
    public static float RequiredPenLength(RaceConfig config);
    public static void ConfigureCamera(Camera camera);   // HDR, SMAA, post, solid backdrop, hides the reflection-only layer
    public static readonly Color Hue; public const string GlassName; public const int ReflectionOnlyLayer = 31;
}
public static class RaceStudioEnvironment {              // scene-level look: build ONCE per scene
    public static void Build(Camera camera, Bounds bounds, Vector3 floorCenter, float floorScale = 500f, float fogStart = 45f, float fogEnd = 190f);
    public static void BuildFloor(Vector3 center, float scale = 500f); public static void BuildLights(); public static void BuildReflections(Bounds bounds);
}
```

- `BuildRig` works in world coordinates: the track start is at `origin`, lifted by `rig.lift` so the lowest point is 1.5 m
  above y = 0. `parent` must have an identity transform (or be null). Space rigs by `origin.x`; 400 m apart is fine.
  For several rigs call `RaceStudioEnvironment.Build` once with a `bounds` that encapsulates all of them, a `floorCenter`
  in the middle and a larger `floorScale` (the floor disc is `floorScale` x 2 metres across).
- **Rig root contents** (children of `rig.root`): `Rig Camera` (Camera + AudioListener + `ChaseCameraRig`, tag MainCamera),
  `Course` (Track with MeshCollider + `TrackPath`, Brass Pinstripes, Start Origin, Start Gate, Start Back Wall, Finish Line,
  Finish Gantry, End Wall, Supports, obstacles), `Templates/Marble Template` (inactive), `Marbles`, `Audio`
  (`MaterialContactAudio` + roll source), `Leaderboard Canvas` (`LeaderboardHud`, screen-space overlay).
  `RaceSimulation` and `RacePresenter` are components ON the root, so `rig.root.GetComponent<IHeatSimulation>()` works.
  Deactivate `root` to hide a heat (camera, canvas and colliders all go with it). Activate it, then `ConfigureHeat` +
  `Initialize`, to run it. Each rig has its own camera, so only the active rig's camera renders.
- **Rig title:** the `title` argument becomes `RacePresenter.title` and the leaderboard heading (e.g. `"THE GRAND PRIX"`).
  The presenter re-applies `title` on every new heat, so change the field (`rig.presenter.title`) rather than calling
  `SetTitle` on the HUD.
- **Leaderboard canvas:** `rig.hud` (a child of `rig.root`), capacity `leaderboardRows` or
  `max(roster.Length, maxEntrants)` (16 for a default config). `RaceSceneBuilder` passes 14, so the Grand Prix canvas is as before.
- **Meshes:** `{meshPrefix}TrackBody` and `{meshPrefix}TrackInlay`, saved with `TrackMeshAssets.SaveMeshFresh` into
  `meshFolder`. Give each rig its own `meshFolder` or its own `meshPrefix`, or they overwrite each other.
- `RaceSimulation.Initialize(seed, config)` takes the heat seed. The grid order and spinner phases come from it. Pass the
  `RaceConfig` asset as `EliminationConfig.heatSimulationConfig`.
- `RaceSceneBuilder` (Grand Prix) now builds through `RaceRigBuilder.BuildRig` + `RaceStudioEnvironment.Build` and adds the
  `SimulationRunner` to `rig.root` (renamed `Simulation`; the camera is renamed `Main Camera`). The same objects as before,
  but the camera and HUD are now children of `Simulation`. Re-run **Grand Prix** from the menu to rebuild the scene. The asset
  helpers moved to `RaceAssets` (internal). Untested in Unity.

### Package D: the `elim` format (owns `Simulations/Elimination/**`, `Presentation/UI/EliminationBoardHud.cs`, the elim layouts + config assets, the scene)

Runtime asmdef `SimulationLobby.Simulations.Elimination` (refs Core, Shared, Presentation; **not Race**). Editor asmdef
`SimulationLobby.Simulations.Elimination.Editor` (refs Core, Shared, Shared.Editor, Presentation, Race, Race's editor
assembly, Elimination). Namespace `SimulationLobby.Simulations.Elimination` (editor `…Elimination.EditorTools`).

```csharp
public enum HeatRule { CutLast, LastChance, Final }   // CutLast: top safeCount stay. LastChance: entrants are all
                                                      // currently-out marbles, the winner returns. Final: winner = champion.
[System.Serializable] public struct EliminationHeat {
    public string title;         // "THE GRAND PRIX"
    public HeatRule rule;
    public int safeCount;        // CutLast only. LastChance and Final use 1
    public TrackLayout layout;   // baked into the rig by the scene builder
}
public sealed class EliminationConfig : SimulationConfig {   // [CreateAssetMenu]
    public HeatEntrant[] roster;               // 16, unique names, jewel colours (RaceConfig.DefaultRoster + 4 more)
    public EliminationHeat[] heats;            // the 6-heat table above
    public SimulationConfig heatSimulationConfig; // passed to every heat's Initialize. Typed as the base (runtime can't
                                               // see Race); the builder assigns a RaceConfig asset
    public float introSeconds = 25f, heatIntroSeconds = 12f, resultSeconds = 15f, twistSeconds = 15f, payoffSeconds = 20f;
    public string question = "WHICH MARBLE SURVIVES?";
    public override int MaxTicks { get; }      // sum of phases + heats × heat max + margin
}
public enum ElimPhase { Intro, HeatIntro, Heat, HeatResult, Twist, Payoff, Done }

public sealed class EliminationSimulation : MonoBehaviour, ISimulation {    // FormatSlug "elim"
    public void Bind(GameObject[] heatRigs);   // one rig per heat, in heat order; each rig root carries an IHeatSimulation
    // Each heat: activate its rig (all others inactive), ConfigureHeat(entrants, safeCount), Initialize(heatSeed,
    // heatSimulationConfig) where heatSeed comes from the run's SeededRandom, then Tick it until IsComplete.
    // Standings map back to roster by name. Eliminations, returns and the champion are logged as RunEvents.
    public ElimPhase Phase { get; } public float PhaseProgress { get; }
    public int HeatIndex { get; } public EliminationHeat CurrentHeat { get; } public IHeatSimulation CurrentHeatSim { get; }
    public IReadOnlyList<HeatEntrant> Roster { get; }
    public ContenderState GetState(int rosterIndex);   // Alive, Out, Returned, Champion
    public int OutThisHeat(int i);                     // rosterIndex of the i-th marble knocked out by the last heat
    public int OutThisHeatCount { get; }
    public int ReturnedIndex { get; }                  // −1 unless LastChance just finished
    public int ChampionIndex { get; }                  // −1 until the end
}
public enum ContenderState { Alive, Out, Returned, Champion }
```

- **`EliminationPresenter`** (`Update` only, reads, never writes): the caption canvas (`CaptionHud`), `SAFE n / N`
  from `CurrentHeatSim.FinishedCount`, the board (`EliminationBoardHud`) during Intro, HeatResult, Twist and Payoff,
  an overview camera for the board beats (the active rig's camera otherwise), gong/thud/confetti, procedural audio.
- **`EliminationBoardHud`** (Presentation, format-agnostic): a 4×4 grid of tiles (swatch, name, a state stamp),
  `SetTiles(names, colors)`, `SetState(i, TileState)` with animated stamps (OUT drops in with a shake; RETURNED peels
  it off; CHAMPION turns gold), centred, max 16 tiles.
- **Scene builder** (menu above): environment once (studio look: floor, key + rim + fill, fog, dust), then 6 rigs
  via `RaceRigBuilder.BuildRig`, spaced far apart (e.g. 400 m on x) so no rig sees another. Heat 1 uses the Grand Prix
  layout (`RaceTrackLayout_GrandPrix`). Heats 2-6 get **five new `TrackLayout` assets** in
  `Assets/Settings/Configs/Elimination/` (a `Reset Elimination Layouts` tools item at priority 291), each with one
  signature feature so no two heats look alike: **PEG FALL** (plinko grid + peg pyramids), **LAST CHANCE** (short,
  fast, banked curves + a bottleneck), **PINWHEELS** (3-4 pinwheels + a chicane), **THE HELIX** (2-loop helix + fork
  divider), **THE GAUNTLET** (longest: helix, pegs, pinwheels, bottleneck, bumps). Lengths are tuned so each race runs
  ~75-100 s (from the race's ~2 min Grand Prix; read `RaceTrackLayout_GrandPrix` and LavaRun's stage layouts for how
  sections and obstacles are authored).
- Determinism: everything in `Tick`. Rig activation is done by the simulation (it changes the physics scene). No
  `UnityEngine.Random`.

### As built (package D)

Built 2026-10-18. Compile-checked from scratch csprojs (Elimination runtime **without** a Race reference, Elimination.Editor, Presentation: 0 errors). Not run in Unity.

**Files** (`Assets/Scripts/`): `Simulations/Elimination/` (asmdef, `EliminationConfig`, `EliminationSimulation`, `EliminationPresenter`, `EliminationAudioKit`), `Simulations/Elimination/Editor/` (asmdef, `EliminationSceneBuilder`, `EliminationLayouts`), `Presentation/UI/EliminationBoardHud.cs`.

- **Contract as specified**, plus these read-only members the presenter needs: `EliminationSimulation.PhaseElapsedSeconds`, `PhaseSeconds`, `HeatCount`, `HeatRig(i)`, `HeatCalled`, `HeatEntrants`, `LastSafeIndex`, `HeatWinnerIndex`, `AliveCount`, `Question`.
- **Heat phase** = race + `heatOutroSeconds` (new config field, default 2.5 s) after the heat is called, so the last-spot caption and push-in have time. The heat is initialised at the **start of HeatIntro** (field settles in the pen under physics) and ticked only in the Heat phase. `OutThisHeat` and `ReturnedIndex` are reset when the next heat is set up.
- **Standings/events:** `heat_N_start`, `heat_N_winner_NAME`, `heat_N_out_NAME` (value = place), `returned_NAME`, `champion_NAME`. Final standings = champion, then reverse elimination order (best of a heat's outs ranks above its worst).
- **Editor asmdef** also references `Presentation.Editor`, `UnityEngine.UI`, `Unity.TextMeshPro`, URP runtime (needed by the builder).
- **Board:** `EliminationBoardHud` builds its tiles at runtime (no prefab). `SetTiles`, `SetState(i, TileState, animate)`, `SetHighlight` (twist shake + glow), `SetVisible(fade)`. `TileState` lives in Presentation (Alive/Out/Returned/Champion); the presenter maps `ContenderState` onto it. Tiles 400x230 reference units on a 1080x1920 match-height canvas (so 4x4 fits 16:9), name auto-sizes 56-84.
- **Captions** (bottom-centre, 112 ref px bold white on a dark plate, one at a time, events hold >= 1.6 s, live counter exempt but throttled 0.35 s): intro question / `16 MARBLES, 6 RACES` / `PICK ONE!` / the three rule lines; per heat `RACE n` > title > `TOP n SAFE`; live `SAFE n / N`; `LAST SPOT: NAME!` (after the call); `n OUT`, `n MARBLES LEFT`; twist `THE OUT MARBLES...` > `GET ONE LAST CHANCE!`; `NAME IS BACK!`; `CHAMPION: NAME!` > `DID YOURS WIN?`. Captions sit bottom-centre, **not** where the storyboard puts SAFE n/N (bottom-left): the race leaderboard is at the **left** edge (not right as the storyboard assumed) and 16 rows reach the bottom-left, so the counter is the centred caption instead.
- **Cameras:** overview camera (stage) only when no rig is on; the first 2 frames of each heat intro keep the overview while the rig settles; the heat intro flies the rig camera from the finish back up the track (11 m above, `ChaseCameraRig` switched off) then glides into the pen orbit; last-spot push-in tweaks the chase rig's distance/height. Every rig's AudioListener is saved **disabled**; the presenter enables exactly one.
- **Rig clean-up:** the presenter hides each rig's leaderboard **Timer** panel (names only) and hides the "X WINS" banner once anyone has finished (the 3-2-1-GO banners stay).
- **Stage:** 16 glass marbles in a slow ring at x = -400 behind the board (the storyboard's "marbles in the pen" is replaced by this; no rig is on during board beats). They dim/shrink when out and go big and gold for the champion.
- **Audio:** `EliminationAudioKit`: custom gong, thuds (ProceduralToneBank), return two-note, C-major chord, whoosh per heat intro, riser at the twist.
- **Environment:** one `RaceStudioEnvironment.Build` (floor scale 3400, fog 45-190) for rig 1's bounds, then `BuildReflections` per extra rig and for the stage. Heat probes are set to `ViaScripting`; the presenter renders each once its rig is on (frame 5 of the heat intro). If a heat's glass looks black, that call did not run.
- **Rigs** at x = 0, 400 ... 2000 (stage at -400); builder warns if two track bounds are closer than fog end + 40 m. Meshes in `Assets/Settings/Studio/Meshes/Elimination`, prefix `Elim1`..`Elim6`.
- **Assets the builder makes** (never overwrites): `Assets/Settings/Configs/Elimination/EliminationConfig_001.asset`, `ElimHeatRaceConfig.asset` (RaceConfig, 16-marble roster, maxEntrants 16, 1/120 s), `ElimTrack_PegFall|LastChance|Pinwheels|Helix|Gauntlet.asset`. Heat 1 loads `RaceTrackLayout_GrandPrix.asset` untouched. Scene `Assets/Scenes/Elimination_Show_3D.unity`. Roster = race's 12 + BRONZE, SILVER, MAGENTA, MINT.

**Layouts** (metres; all start width 1.6, gate 4.4; checked headless for self-crossings: none; x extents under 80 m so rigs are > 300 m apart):

| Heat | Track | Length | Drop | Signature | Model s |
|---|---|---|---|---|---|
| 1 | THE GRAND PRIX | 269 | 30 | (existing course) | 30 |
| 2 | PEG FALL | 127 | 13 | 3 peg pyramids (9/7/9 rows) + 24-row plinko grid + lower pyramid/plinko, on a wide straight waterfall (4.4 m) with one jog | 20 |
| 3 | LAST CHANCE | 112 | 14 | three banked (20 deg) S-curves + a bottleneck; fast, 8-10 deg slope | 17 |
| 4 | PINWHEELS | 125 | 11 | 4 pinwheels + 4-baffle chicane on a staircase of 90 deg turns | 21 |
| 5 | THE HELIX | 157 | 21 | two 720 deg helix towers (r 4.2 and -3.2) with a fork divider between | 20 |
| 6 | THE GAUNTLET | 217 | 21 | helix, pegs, two pinwheels, bottleneck, 8 bumps, last spinner | 27 |

**Estimated length** (builder logs it; scales the layout model by the Grand Prix's known ~105 s race, so it is a model, not a measurement): races ~100 / 65 / 60 / 69 / 68 / 95 s; heats (intro 12 + hold/grace 5.5 + race + outro 2.5 + result 15) ~135 / 100 / 95 / 104 / 103 / 130 s; fixed 25 + 15 + 20. **Total ~728 s = 12.1 min** (band 10-15, target 11-12). The model ignores obstacle drag, so peg and pinwheel heats may run longer than shown.

**Deviations:** captions centred bottom (see above); no pen behind the board (ring of stage marbles instead); `LAST SPOT: NAME!` appears when the heat is called (the cut grace after the last safe marble crosses), the gong plays the moment the last safe spot is taken; extra `heatOutroSeconds`; `Phase`/rig activation as above.

**Check on first Play (in addition to the list below):** the heat intro fly-in does not clip the helix towers (raise `flyHeight` on the presenter if it does); no "2 AudioListeners" warning; glass is not black in heats 2-6 (probe render); captions never overlap the leaderboard in heat 1 (16 rows); the timer panel is gone; 16 stage marbles visible beside the board.

### Fix 2026-10-18: marbles stuck between pegs and the wall

First Play: PEG FALL's pegs were too dense, and marbles wedged between the outermost peg and the rail. Cause: the
shared peg builder packs as many pegs across as fit the lane, so an edge peg can sit less than a marble's width from
the rail. Fix: new `TrackLayout.pegWallClearance` (rail-to-peg gap; 0 = the old packing, so the published Grand Prix
and Lava Run courses and their seeds are unchanged). Every elim layout sets **0.4 m** (~1.8 marble diameters).
PEG FALL drops from 6 peg fields (24-row grid at 0.55 m) to 4 (10-row grid at 0.75 m, pyramids at 0.7 m). THE
GAUNTLET's pegs go from 0.46/0.50 m to 0.65/0.70 m spacing. The scene builder re-applies any elim layout with
clearance 0, so a rebuild picks it up.

**Second pass (same day, from the paused Play):** heat 1 had jammed: all 16 marbles were in the Grand Prix plinko
grid after 26 s, three of them pinned between the outer pegs and the rail. Heat 1 was loading the *published*
`RaceTrackLayout_GrandPrix`, so the first fix never reached it. Two more causes: the peg garden is nearly flat (5°),
and `TrackMeshBuilder.At` snaps each peg row to the next track sample, which left the last row only 0.25 m behind the
one before it (a wall). Fixes: heat 1 now uses its own `ElimTrack_GrandPrix` (the default Grand Prix course, peg
garden at 12°, pyramid 5 rows at 0.65 m, grid at 0.7 m, 0.4 m wall clearance). Every elim peg section is 12°
(`EliminationLayouts.PegSlope`), so marbles cascade like a plinko. Rows are placed with the new
`TrackMeshBuilder.AtExact` whenever clearance is on. The published race and Lava Run courses are untouched.

## Acceptance criteria

- [ ] `race-001` builds and plays the same seed to the same result as before Package C
- [ ] `SimulationLobby.Simulations.Elimination` (runtime) has no reference to Race; deleting `Simulations/Race` breaks only the elim *editor* assembly
- [ ] Same seed twice → identical champion and elimination order
- [ ] Every heat has a different track; heat 1 eliminates exactly 4, heat 3 returns exactly 1, the final crowns 1
- [ ] Readable at 480 px wide: captions, board tiles and leaderboard names; HUD clear of the track action
- [ ] Length 10-15 min (target 11-12)
- [ ] Holds ≥ 50 fps in the editor at 1080p with 16 marbles

## Check on first Play

- [ ] Build **Simulation Lobby ▸ 3D Formats ▸ Elimination (elim) ▸ Elimination Show (elim-001)**. The console logs 6 heats, track lengths and the estimated length
- [ ] Only the current heat's rig is visible; the camera never shows another rig
- [ ] The red cut line sits under the last safe spot; rows below glow red
- [ ] The heat stops when the last safe spot is taken; OUT stamps match the leaderboard's red rows
- [ ] Last Chance runs only the out marbles, and its winner's tile comes back
- [ ] Champion caption and confetti in the winner's colour

## Seed selection

None. Random per Play, logged. Re-render a good take from its seed.

## Render + publish

1920x1080, PC pipeline. Thumbnail: the board with 15 OUT stamps and one gold tile, "WHO SURVIVES?". Pinned comment:
"Which marble did you pick? Tell us and we'll track it next episode 👇". Title, description, chapters (one per heat)
and tags per CLAUDE.md → *Publishing metadata (Long form)* once the cut exists. Record the logged seed here.

## Retro

Write `KnowledgeBase/retros/YYYY-MM-DD-elim-show-1.md`, update `/PROJECTS.md`.
The open question: **does a fixed-rules elimination show hold viewers (avg view duration) and climb like `terr`, so
episode 2 is just a new theme?**
