# terr-001 — Four Colours, One Board (Multiply or Release)

**Format:** `terr` · **Length:** 10-15 min run (decision forced at 20) · **Aspect:** 16:9 · **Status:** `building`
**Brief:** `KnowledgeBase/formats/territory-multiply-release.md`

## Premise

Four colours, four cannons, four Plinko boxes: ×2 doubles your ammo, R fires it all. Which colour takes
the board?

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md): `SimulationRunner`, `SeededRandom`, `RunRecorder`
- [ ] **REUSE** [studio-look-3d](../blocks/studio-look-3d.md): `StudioSetBuilder` glass / chrome / lacquer / brass
- [ ] **REUSE** [material-contact-audio](../blocks/material-contact-audio.md): procedural sound, `ProceduralToneBank`
- [ ] **REUSE** [onscreen-counter](../blocks/onscreen-counter.md): `CaptionHud`, `WorldLabelLayer`, `CountFormat`
- [ ] **BUILD** [territory-grid](../blocks/territory-grid.md): `OwnershipGrid`, `GridBouncer`, `TileGridView`, `ShareBarHud`
- [ ] **BUILD** [slot-board-economy](../blocks/slot-board-economy.md): `SlotEffect`, `CountBank`, `TriggerZone3D`, `BodyPool3D`, `SlotReelHud`
- [ ] **BUILD** (in `Shared/Turrets`) `SweepAim`, plus (in Presentation) `FocusCameraRig`, ready for `wars`/`surv`

## Where everything lives (isolation at concept level)

| Concept | Layer / folder | Why there |
|---|---|---|
| Ownership grid + paint-ball stepping | `Shared/Grids` | Any territory/colour-war format (`wars` variants) |
| Count economy (×n, +n, release, bonus) | `Shared/Economy` | Same idea as `rom` gates. `rom` migrates later (see below) |
| 3D trigger zone, 3D body pool | `Shared/Physics3D`, `Shared/Pooling` | 3D counterparts of the existing 2D pieces |
| Turret sweep | `Shared/Turrets` | Cannons recur (wars, surv) |
| Tile grid renderer, share bar, slot reel, focus camera | `Presentation` | Data-in only. Presentation references **Core only**, never Shared or a format |
| Rules, config, team rigs, binder, audio kit, scene builder | `Simulations/Territory` | Only `terr` |

`rom` keeps its own `GateKind` for now. Migrating it onto `Shared/Economy` is a **follow-up**, because `rom`
is published and stable, and touching it doesn't belong in this build. Logged in `PROJECTS.md`.

Deletion test: delete `Simulations/Territory` and everything else still compiles.

## Video-specific work

### Scene (`Assets/Scenes/Territory_MultiplyRelease_3D.unity`, built by menu)

**Simulation Lobby ▸ Build Scene ▸ Territory_MultiplyRelease_3D** (`TerritorySceneBuilder`). Rebuilding replaces
the scene. It never overwrites `Assets/Settings/Configs/Territory/TerritoryConfig_001.asset` once that exists.

World layout (metres; Y up; the camera looks toward +Z from the front, elevated):

- **Grid:** 64 x 48 cells at `cellSize` 0.25 m, so 16 m (X) x 12 m (Z). The grid origin transform sits at the
  cell-(0,0) corner: `right` is +X (cell x), `forward` is +Z (cell y). Centre the grid on world X = 0. It rests
  on a piano-black lacquer slab with a brass rim.
- **Quadrants, team indices:** 0 GREEN top-left (low x, high y) · 1 YELLOW top-right · 2 BLUE bottom-left ·
  3 RED bottom-right. "Top" means far from the camera (+Z).
- **Plinko boards:** one per team, outside the grid next to its quadrant. GREEN and BLUE are left of the grid,
  YELLOW and RED are right. Each board is about 4.5 m wide and 5.5 m long. It runs along Z and tilts up away from
  the camera (~25°), so balls roll toward the camera into a slot row on the near edge. It has a lacquer back,
  chrome pegs in staggered rows, brass slot dividers, and a clear glass cover pane with a collider so balls
  can't hop out. Each slot has a team-tinted floor strip and a `TriggerZone3D` box.
- **Cannons:** chrome barrel on a brass base, with a team-coloured glass band. Each sits on its base cell.
  It's visual only, with no colliders.
- **Camera:** 16:9, FOV about 40, elevated about 55° and looking at the grid centre, framing the grid
  plus all four boards with the HUD strips clear (like the reference screenshot). `FocusCameraRig` handles
  pushes.
- **3D baseline (CLAUDE.md):** floor plane, key light plus fill/rim, linear fog, and `AmbientDriftField` dust.
  Materials are glass, chrome, lacquer and brass only. No bare URP Lit defaults.

### Simulation / rules (`TerritorySimulation`)

Per tick, in this order (the order is part of the determinism contract):

1. `recorder.AdvanceTick()`. Phase `Hold` counts down `preStartHoldSeconds`. Nothing drops or fires.
2. **Drop:** each live team's board drops a ball every `dropIntervalSeconds × escalation factor`. Board
   phases are staggered (`team / teamCount × interval`). The x offset is seeded, within ±`dropHalfWidth`.
   Balls come from that board's `BodyPool3D`, and a drop is skipped when the pool is empty.
3. **Slots:** drain every board's slot triggers in order (team, then slot index). Sort the drained bodies by
   pool index. Apply `SlotEffect` to the team's `CountBank`, then release the ball to the pool.
   - `Multiply n` / `Add n`: the bank saturates at `bankCap`.
   - `Release`: `queued += bank.Drain(resetTo: bankStart)`. Starts a barrage.
   - `Bonus` (the SPIN slot): enqueue a spin for that team (FIFO, max `maxQueuedSpins`, extras dropped).
4. **Stuck balls:** a ball alive longer than `ballStuckSeconds` is released back to the pool and logged.
5. **Spinner:** if idle and a spin is queued, draw a face by weight (seeded) and run it for
   `spinSeconds`. When it ends, apply that face's `SlotEffect` to that team (Multiply/Add to the bank; Release
   fires the bank). If the team has been eliminated meanwhile, discard it.
6. **Fire:** per live team, step its `SweepAim`. While `queued > 0` and live shots < `maxLiveShotsPerTeam`,
   fire one shot every `fireIntervalSeconds`.
   - Shot health = `max(shotHealth, ceil(queuedAtReleaseStart / shotsPerBarrage))`, where
     `shotsPerBarrage = maxBarrageSeconds / fireIntervalSeconds`.
   - Each shot consumes `health` from `queued` (the last shot takes whatever is left).
   - The shot spawns at the base-cell centre with velocity `aim.Direction × shotSpeedCellsPerSecond`.
7. **Move shots:** in shot-slot order, `GridBouncer.Step` each live shot.
   - Dead shots free their slot.
   - A shot older than `shotMaxLifetimeSeconds` dies.
8. **Eliminations:** a live team is out when the owner of its base cell is no longer that team, or when its
   tile count is 0.
   - Kill its live shots and clear its queue and spins.
   - Stop its board dropping. Balls already on the board finish rolling and their slots are ignored.
   - Log the capturing team (= the base cell's new owner).
9. **Escalation:** starting at `escalateAfterSeconds`, every `escalateEverySeconds`, multiply the drop
   interval by `escalateDropFactor` (floored at `minDropIntervalSeconds`) and add `escalateShotHealth` to the
   base shot health. Log the escalation level.
10. **End:**
    - **Normal end:** one team left → that team wins and the phase becomes `Complete`.
    - **Forced decision:** at `decisionAtSeconds`, the team with the most tiles wins (ties go to the lower index).
    - Either way the run reports `IsComplete` after `endHoldSeconds`.

`Phase`: `Hold → Battle → Finale` (two or fewer teams left) `→ Complete`.

### Config + tuning targets (`TerritoryConfig : SimulationConfig`)

| Knob | Start at | Note |
|---|---|---|
| `maxDurationSeconds` | 1500 | Safety only. `decisionAtSeconds` (1200) ends a stalled run |
| `fixedTimestep` | 1/60 | |
| `gridWidth` x `gridHeight`, `cellSize` | 64 x 48, 0.25 | Board size = video length |
| `baseInsetCells` | 4 | How far in from its outer corner a cannon sits |
| `sweepDegreesPerSecond` | 30 | Each cannon sweeps its quadrant's inward 90° wedge (±5° margin), ping-pong, seeded start phase |
| `shotSpeedCellsPerSecond` | 12 | |
| `shotHealth` | 1 | Base tiles per shot |
| `fireIntervalSeconds` | 0.08 | About 12 shots/s per team |
| `maxBarrageSeconds` | 15 | Bigger banks make heavier shots, not longer barrages |
| `maxLiveShotsPerTeam` | 150 | |
| `shotMaxLifetimeSeconds` | 25 | |
| `boardSlots` | SPIN, R, ×2, ×2, ×2, R, SPIN | Centre slots are the most likely, so ×2 is common and SPIN is rare |
| `ballsPerBoard` | 6 | Pool capacity per board |
| `dropIntervalSeconds` | 1.1 | |
| `bankStart`, `bankCap` | 1, 4096 | |
| `ballStuckSeconds` | 12 | |
| `spinFaces` / weights | ×2:5, ×3:3, ×5:1, +25:4, +100:2, +250:1, RELEASE:2 | |
| `spinSeconds`, `maxQueuedSpins` | 3.5, 3 | |
| `escalateAfterSeconds` / `EverySeconds` / `DropFactor` / `ShotHealth` | 540 / 60 / 0.8 / 1 | |
| `minDropIntervalSeconds` | 0.35 | |
| `preStartHoldSeconds`, `endHoldSeconds` | 5, 10 | |
| Board geometry (`boardWidth`, `boardLength`, `boardTiltDegrees`, `pegRows`, `pegColumns`, `pegRadius`, `ballRadius`) | 4.5, 5.5, 25, 9, 7, 0.09, 0.17 | Read by the builder; changing it means rebuilding the scene |

### Look

Studio kit. Tiles are glossy coloured tiles (owner texture on a URP Lit material, smoothness ~0.8) with
dark gaps. Shots are glass spheres in the team colour; heavier shots swell. Board balls are team glass.
Team colours are saturated and high-contrast: GREEN `#3DDC4A`, YELLOW `#FFD23A`, BLUE `#2E6BFF`, RED `#FF3B3B`.

### Sound (first-class)

All procedural (`ProceduralToneBank`):

- **Tile flip:** a soft tick, each team on its own pentatonic degree. Rate-limited to about 25 voices/s.
- **Cannon:** pop/thud per shot. Throttled, with heavier shots lower.
- **Peg clacks:** from board-ball contacts, reading `CollisionDetector3D`.
- **Slots:** ×2 chime that rises in pitch with log2(bank). R gets a whoosh plus a riser on big banks.
- **Reel:** a click per face passing, then a landing chime.
- **Elimination:** shatter plus a low boom.
- **Ambient bed** under all of it.

## Contracts (the parallel build codes against these; any deviation gets reported, never silently changed)

All runtime code: Unity 6000.5 names (`linearVelocity`, `linearDamping`, `PhysicsMaterial`). No
`UnityEngine.Random` in sim code. Simulation logic only runs from the runner's tick (FixedUpdate);
presentation runs in Update/LateUpdate and never writes sim state.

### A. Shared (`SimulationLobby.Shared`, asmdef refs Core only)

```csharp
// Shared/Grids/OwnershipGrid.cs
public readonly struct CellChange { public readonly int tick, index, previousOwner, newOwner; /* ctor */ }
public sealed class OwnershipGrid
{
    public const int Unowned = -1;
    public OwnershipGrid(int width, int height, int ownerCount);   // all cells Unowned
    public int Width { get; } public int Height { get; } public int CellCount { get; } public int OwnerCount { get; }
    public int Version { get; }                    // +1 on every SetOwner / Fill that changes a cell
    public IReadOnlyList<int> Owners { get; }      // row-major, index = y * Width + x
    public IReadOnlyList<CellChange> Changes { get; } // append-only, only SetOwner(…, logged:true) writes it
    public int Index(int x, int y); public void Coords(int index, out int x, out int y);
    public bool InBounds(int x, int y);
    public int GetOwner(int x, int y); public int GetOwner(int index);
    public void SetOwner(int x, int y, int owner, int tick, bool logged = true); // no-op if unchanged
    public void FillRect(int x0, int y0, int w, int h, int owner);              // setup; not logged
    public int CountOf(int owner);                 // O(1), maintained incrementally
    public float ShareOf(int owner);               // CountOf / CellCount
}

// Shared/Grids/GridFrame.cs — grid space <-> world. Grid units: 1.0 = one cell; cell (x,y) spans [x,x+1)×[y,y+1).
[Serializable] public struct GridFrame
{
    public Vector3 origin, right, forward; public float cellSize;   // right/forward unit length
    public static GridFrame FromTransform(Transform corner, float cellSize);
    public Vector3 ToWorld(Vector2 gridPosition, float height = 0f);
    public Vector2 ToGrid(Vector3 world);
    public Vector3 CellCenterWorld(int x, int y, float height = 0f);
}

// Shared/Grids/GridBouncer.cs
public struct GridBall { public Vector2 position, velocity; public int owner, health; public bool alive; public float age; }
public static class GridBouncer
{
    // Advances ball by velocity*dt in sub-steps of at most maxStep cells. Reflects off the grid's outer edges.
    // Crossing into a cell whose owner != ball.owner: SetOwner(cell, ball.owner, tick), health--, the
    // ball stays in its previous cell, and the velocity component on the crossed axis negates. If a sub-step
    // changes both x and y cell, resolve the x crossing first, then y. health==0 => alive=false, stop.
    // Adds dt to age. Returns the number of cells painted.
    public static int Step(ref GridBall ball, OwnershipGrid grid, float dt, int tick, float maxStep = 0.25f);
}

// Shared/Economy/SlotEffect.cs
public enum SlotEffectKind { Multiply = 0, Add = 1, Release = 2, Bonus = 3 }
[Serializable] public struct SlotEffect
{
    public SlotEffectKind kind; [Min(0)] public int value;
    public SlotEffect(SlotEffectKind kind, int value = 0);
    public string Label { get; }   // "×2", "+25", "R", "SPIN"
    public static SlotEffect Multiply(int n); public static SlotEffect Add(int n);
    public static SlotEffect Release(); public static SlotEffect Bonus();
}
// Shared/Economy/CountBank.cs — saturating, never negative
public sealed class CountBank
{
    public CountBank(long start, long cap);
    public long Value { get; } public long Cap { get; }
    public void Multiply(int factor); public void Add(long amount);
    public long Drain(long resetTo);   // returns the drained value, sets Value = resetTo
    public void Reset(long value);
}

// Shared/Physics3D/TriggerZone3D.cs — 3D counterpart of TriggerZone2D (needs a trigger Collider)
public sealed class TriggerZone3D : MonoBehaviour
{
    public int Count { get; }
    public void Drain(List<Collider> into);  // entries since last drain, in arrival order; clears buffer
    public void Clear();
}

// Shared/Pooling/BodyPool3D.cs — mirror of BodyPool2D, deterministic free-list order
public sealed class BodyPool3D
{
    public BodyPool3D(GameObject template, Transform parent, int capacity); // template inactive, needs Rigidbody
    public int Capacity { get; } public int ActiveCount { get; } public int FreeCount { get; }
    public GameObject TryGet(Vector3 position);   // null if empty; zeroes linear/angular velocity, activates
    public void Release(GameObject body);         // deactivates, back to free list
    public void ReleaseAll();
    public int IndexOf(GameObject body);          // stable 0..Capacity-1, for deterministic sorting
}

// Shared/Turrets/SweepAim.cs — pure ping-pong angle, grid-space degrees (0 = +x, 90 = +y)
[Serializable] public struct SweepAim
{
    public float minDegrees, maxDegrees, degreesPerSecond, angle; public int direction; // +1/-1
    public static SweepAim Create(float minDegrees, float maxDegrees, float degreesPerSecond, float startPhase01);
    public void Step(float dt);
    public Vector2 Direction { get; }   // unit vector in grid space
}
```

### B. Presentation (`SimulationLobby.Presentation`, asmdef refs Core + UI + TMP; never Shared)

```csharp
// Presentation/VFX/TileGridView.cs — one runtime-generated mesh (never saved as an asset), owner texture
public sealed class TileGridView : MonoBehaviour
{
    public void Build(int width, int height, float cellSize, float tileHeight, float gap,
                      Color[] palette, Color neutral, Material material);
      // Local XZ plane, cell (0,0) corner at local origin, x→+X, y→+Z. Bevelled tile tops + sides, 32-bit
      // indices. Point-filtered width×height Texture2D on an instanced copy of `material` (_BaseMap).
    public void SetOwners(IReadOnlyList<int> owners, int version); // re-uploads only when version changed
    public void Pulse(int cellIndex, Color color);   // pooled pop-and-fade overlay tile, capped (~64)
    public Vector3 CellCenterLocal(int x, int y);
}

// Presentation/UI/ShareBarHud.cs — stacked N-way horizontal bar with % labels, animates widths
public sealed class ShareBarHud : MonoBehaviour
{
    public void Bind(RectTransform bar, Image[] segments, TMP_Text[] labels);
    public void Configure(int count, Color[] colors, string[] names);
    public void SetShares(IReadOnlyList<float> shares01);
    public void SetEliminated(int index, bool eliminated);
}

// Presentation/UI/SlotReelHud.cs — vertical reel window + header line ("BLUE is SPINNING")
public sealed class SlotReelHud : MonoBehaviour
{
    public bool IsSpinning { get; }
    public event Action FacePassed;          // one per face passing the window (click audio)
    public event Action<int> Landed;         // result index (landing chime)
    public void Spin(string[] faces, int resultIndex, float seconds, string header, Color headerColor);
    public void Hide(float delaySeconds = 1.5f);
}

// Presentation/Cameras/FocusCameraRig.cs — home framing + slow breathing drift + eased push-ins
public sealed class FocusCameraRig : MonoBehaviour
{
    public void Bind(Camera camera);
    public void SetHome(Vector3 position, Vector3 lookAt);
    public void Focus(Vector3 worldPoint, float zoom01, float holdSeconds); // 0 = home, 1 = tight
    public void Shake(float amplitude, float seconds);
}

// Presentation/Audio/ProceduralToneBank.cs — add:
public static AudioClip CreateClick(float pitch = 1f, int seed = 7, string name = "Click");

// Presentation/Editor (SimulationLobby.Presentation.EditorTools):
public static class ShareBarCanvasBuilder { public static ShareBarHud Build(RectTransform parent, int segments); }
public static class SlotReelCanvasBuilder { public static SlotReelHud Build(RectTransform parent); }
// Both build under an existing canvas panel; anchors are the caller's to set on the returned rect.
```

### C. Territory runtime (`SimulationLobby.Simulations.Territory`, refs Core, Shared, Presentation)

```csharp
// TerritoryTeamRig.cs — scene wiring per team, filled in by the builder (public fields)
public sealed class TerritoryTeamRig : MonoBehaviour
{
    public GameObject ballTemplate;   // inactive: Rigidbody, SphereCollider, CollisionDetector3D, team glass
    public Transform ballParent;
    public Transform dropPoint;       // top-centre of the board; right axis runs across the board
    public float dropHalfWidth;
    public TriggerZone3D[] slots;     // left→right as seen by the camera; length == config.boardSlots.Length
    public Transform[] slotLabelAnchors;
    public Transform counterAnchor;   // where the big bank number floats
    public Transform cannonPivot;     // yaw pivot (visual only)
    public Transform cannonMuzzle;
}

[Serializable] public struct TerritoryTeamDef { public string name; public Color color; }
[Serializable] public struct SpinFace { public SlotEffect effect; [Min(0)] public int weight; }

public sealed class TerritoryConfig : SimulationConfig { /* every knob in the table above */
    public TerritoryTeamDef[] teams;   // 4, in quadrant order 0 TL,1 TR,2 BL,3 BR
    public SlotEffect[] boardSlots; public SpinFace[] spinFaces; /* … */
    public Vector2Int BaseCell(int team);          // derived from quadrant + baseInsetCells
    public Vector2 SweepRange(int team);           // (min,max) degrees, grid space
    public override bool Validate(out string error);
}

public enum TerritoryPhase { Hold = 0, Battle = 1, Finale = 2, Complete = 3 }
public enum TerritoryEventKind { SlotHit, Released, ShotFired, SpinStarted, SpinResolved, Eliminated,
                                 Escalated, BallRecovered, Winner }
public readonly struct TerritoryEvent { public readonly int tick; public readonly TerritoryEventKind kind;
    public readonly int team, slot, other; public readonly long before, after; /* ctor */ }
    // SlotHit: slot, before/after bank. Released: after = amount. SpinStarted/Resolved: slot = face index.
    // Eliminated: other = capturing team. Escalated: slot = level. Winner: other = 1 if decided on tiles.

public sealed class TerritoryTeam {   // read-only to presentation
    public int index; public string name; public Color color; public Vector2Int baseCell;
    public long Bank; public long Queued; public bool Eliminated; public int Tiles; public float Share;
    public float AimDegrees; public int LiveShots; }

[RequireComponent(typeof(SimulationRunner))]
public sealed class TerritorySimulation : MonoBehaviour, ISimulation
{
    public void Bind(TerritoryTeamRig[] rigs, Transform gridOrigin);
    public string FormatSlug => "terr";
    public TerritoryPhase Phase { get; }
    public OwnershipGrid Grid { get; } public GridFrame Frame { get; }
    public TerritoryConfig Config { get; }
    public IReadOnlyList<TerritoryTeam> Teams { get; }
    public IReadOnlyList<TerritoryTeamRig> Rigs { get; }
    public IReadOnlyList<GridBall> Shots { get; }        // fixed capacity; render where alive
    public IReadOnlyList<TerritoryEvent> Events { get; } // append-only
    public bool SpinActive { get; } public int SpinTeam { get; } public int SpinFace { get; }
    public float SpinSecondsRemaining { get; }
    public float ElapsedSeconds { get; } public float SecondsUntilStart { get; }
    public int WinnerIndex { get; }                      // -1 until decided
    public int AliveTeams { get; }
}

public sealed class TerritoryAudioKit : MonoBehaviour   // owns AudioSources + procedural clips
{   public void Build();   // creates clips/sources; called from presenter Start
    public void PlayFlip(int team); public void PlayShot(int team, int health); public void PlayPeg(float speed);
    public void PlaySlot(SlotEffectKind kind, long bankAfter); public void PlayRelease(long amount);
    public void PlayReelClick(); public void PlayReelLand(); public void PlayElimination(); public void PlayWin(); }

[RequireComponent(typeof(TerritorySimulation))]
public sealed class TerritoryPresenter : MonoBehaviour
{
    public void Bind(TileGridView gridView, ShareBarHud shareBar, SlotReelHud reel, CaptionHud caption,
                     WorldLabelLayer labels, FocusCameraRig cameraRig, TerritoryAudioKit audio,
                     GameObject shotTemplate, Transform shotRoot, Material tileMaterial);
}
```

### D. Territory editor (`SimulationLobby.Simulations.Territory.EditorTools`)

`TerritorySceneBuilder` (menu above) builds everything in **Scene** from `TerritoryConfig` geometry, creates
`TerritoryConfig_001` if it's missing, sets `SimulationRunner.randomizeSeedOnStart = true`, and calls
`TerritorySimulation.Bind` and `TerritoryPresenter.Bind`. HUD: one `CaptionCanvasBuilder.Build(…, Style.Dark)`
canvas with the share bar across the top strip, the reel top-centre under it, captions bottom, and a
`WorldLabelLayer` for board counters and slot labels.

## Acceptance criteria

- [ ] All asmdefs compile. Deleting `Simulations/Territory` leaves everything else compiling
- [ ] Same seed twice gives an identical outcome (winner, elimination order, tick of each)
- [ ] Readable at 480px wide: territory bar, bank numbers and reel caption
- [ ] A typical run lasts 10-15 min with at least two lead changes in territory share
- [ ] Sound plays on flips, shots, slots, the reel and eliminations, and passes a listening check

## Render + publish

1920x1080 long form. Thumbnail: the board mid-swing with one huge bank number ("×2 … 1,008"). Pinned comment:
"Pick your colour before it starts." Record the logged seed.

## Retro

`KnowledgeBase/retros/YYYY-MM-DD-terr-four-color-territory.md`, then update `PROJECTS.md`.
Open question: does a 10-15 min four-colour territory run hold Studio retention past the 5-minute mark?

## Look v2: match the reference (2026-10-11, after the first playtest)

The first build followed the dark glass/chrome house style and read as unpolished next to the reference
(@marblejamboree). The user asked to match the reference. **`terr` deliberately departs from the studio kit**:
bright toy look, flat-ish soft lighting, saturated matte-gloss plastics. Pacing was judged fine. Keep it.

Problems seen in the playtest screenshot:
- Slot labels overlapped into an unreadable "SPINR ×2×2×2 RSPIN".
- Boards floated on stilts as dark boxes and overlapped each other.
- The bank counter was a tiny "1".
- Black void with lens-flare blobs.
- Cannons were tiny chrome sticks.
- The HUD looked like debug output.

### Arena (owner: scene/board builders)
- **One table.** A charcoal plinth (`#22232B`, smoothness 0.4) carries everything. The grid has a thick
  rounded charcoal frame (~0.35 m rim, ~0.2 m proud). Boards sit **flush in the same table**: GREEN top-left and
  BLUE bottom-left of the grid, YELLOW top-right and RED bottom-right. Each board spans its quadrant's depth (6 m minus
  frame), so `boardLength` defaults to 5.6. Each board has its own charcoal frame, and the gaps between frames are
  ≤ 0.3 m. No stilts, no supports.
- **Boards are visually FLAT** on the table. The slope is faked with **tilted gravity**: the simulation sets
  `Physics.gravity = 9.81 × (0, -cos t, -sin t)` with t = `boardTiltDegrees` (25), so balls roll toward the
  camera (-Z) exactly as on a 25° slope. The original gravity is restored on disable/destroy. Board balls are the
  only rigidbodies in the scene. Pacing is unchanged, and the bottom boards can no longer hide the top boards' slot rows.
- **Board look:** the surface is the team colour darkened to ~0.75. Pegs are short charcoal cylinders.
  The glass cover keeps its collider but has NO renderer. Balls are solid glossy team colour (URP Lit,
  smoothness 0.6, a little lighter than the tiles). The golden ball is tinted at runtime.
- **Slot chips** run along the near edge. The default slots are **5: `×2, ×2, R, ×2, ×2`**, with chips ~0.85 m wide:
  `×2` chips hot pink `#FF4FA0`, `R` chips white `#F4F4F4`, charcoal dividers.
- **SPIN is no longer a slot.** It's a **golden ball**: each drop is golden with `goldenBallChance` (0.07, seeded).
  A golden ball triggers a spin in whatever slot it lands, so viewers watch it roll down.
- **Cannons are chunky:**
  - a charcoal base disc ~0.9 m across
  - a team-coloured barrel ~0.8 m long and ~0.32 m thick, with a dark muzzle ring
  - the pivot's local +Z runs along the barrel, and the barrel and muzzle are children of the pivot (unchanged)
- **Tiles:** smoothness ~0.45 (no flare), gap ~0.035.
- **Environment:**
  - **Floor:** an indigo-purple tiled floor (base `#3B3566`, alternating `#46407A`, 1.2 m tiles, thin darker grout, matte)
    filling the frame behind the table.
  - **Background and lights:** the camera background matches the far floor, with subtle linear fog. One soft key
    directional from above-front-left (soft shadows, strength ~0.6), bright flat ambient, **no spotlights**,
    no visible light cards.
  - **Post:** its own volume (`TerritoryPost`): Neutral tonemapping, bloom intensity ≤ 0.25 with threshold ≥ 1.1,
    light vignette, saturation +10. `StudioSetBuilder` is no longer used.
- **Camera:** pitch ~48°, FOV ~38. Frame the whole table between **14% and 84% of screen height** (the bands
  above and below belong to the HUD), with the table spanning ~90% of the width.

### HUD (owner: HUD builder + Presentation UI)
- **Top centre: the drum.** `SlotReelHud`, always visible: a rounded charcoal frame, a hot-pink face panel, white
  bold face text (~110), and a yellow arrow pointing at the face from the left. The header ("BLUE is SPINNING", team
  colour, bold italic ~70, dark outline) sits left of the arrow. Idle shows "SPIN" dimmed. It occupies the top
  ~250 units, centred.
- **Bottom edge: the share bar.** Full width (x 0.03–0.97), ~90 tall, ~20 above the bottom, in a rounded charcoal
  frame, with bold "%" labels and dark outlines. Eliminated segments show "OUT".
- **Captions:** event captions and the opening question sit just above the share bar (caption ~84 bold, subline ~44).
  The footer clock moves to the top-right corner (~48).
- **Generated sprites:** a 9-sliced rounded rectangle and an arrow.

### Contracts added for v2
- `SlotReelHud`: `public bool alwaysVisible` (true for terr). `public void SetIdle(string face)` shows the drum at
  rest with that face and an empty header. With `alwaysVisible`, `Hide()` returns to idle instead of fading out.
- `TerritoryConfig`: `goldenBallChance` (0.07). `boardSlots` default `×2, ×2, R, ×2, ×2`. `boardLength` 5.6.
- `TerritorySimulation`: `public bool IsGoldenBall(int team, int poolIndex)`. A golden ball's slot hit applies
  `Bonus` (a spin) instead of the slot's effect. `SlotHit.other = 1` for golden. Gravity is tilted in `Initialize` and
  restored in `OnDisable`/`OnDestroy`.
- **Board builder:** boards are built flat (no tilt). Board balls use a URP Lit material with `_BaseColor`, which the
  presenter tints gold through a property block. The `counterAnchor` sits ON the board surface, ~30% of the length
  in from the far end, centred (the big number reads as painted on the board). Slot label anchors sit at chip centres.
- **Presenter:**
  - Counters: white, bold italic, ~150, dark outline. "FIRING n" (~46) sits under the counter.
  - Slot labels at ~72: white on the pink `×2` chips, charcoal on the white `R` chips.
  - Golden balls are tinted gold, and the tint resets when a ball is reused.
  - Calls `reel.SetIdle("SPIN")` in Start.

## Look v3: camera, decals, drum (2026-10-11, after the second playtest)

The user approved the v2 direction ("decent") and called it sloppy because of the camera angle and overlapping text.
Images: reference `images/3.png`, ours `images/4.png` (session temp folder).

### What's wrong, and the fix

| Problem | Fix |
|---|---|
| The camera is far and flat; the table fills only the middle; lots of empty floor | **Low, close, perspective camera** like the reference: pitch ~40°, FOV ~45, positioned behind the near edge. The grid's near edge sits ~8% above the bottom of the frame, and the drum top ~5% below the top. The side boards may crop at the frame edges (up to ~15% of their width) |
| A landscape grid reads as a flat strip | **Portrait grid: 48 wide × 64 deep** (same 3,072 cells). Quadrants are 6 m × 8 m. Each board spans its quadrant's depth: `boardLength` 7.4, `boardWidth` 4.5 |
| Slot labels overlap ("SPIR×2×2SPIN"); the stale 7-slot config survived because the builder never overwrites | **Config layout versioning:** `TerritoryConfig.layoutVersion` + `ResetLayoutToDefaults()`. The builder migrates an older config (layout fields only, logged). **Chip and board text become world-space decals** (see below) |
| Screen-projected counters float in front of the boards | Counter, "FIRING n" and chip labels are **TextMeshPro 3D decals lying on the surface**: perspective-correct, sized to their object, never overlapping |
| The HUD drum's faces overlap ("SH2bN") | **A physical 3D drum** behind the grid's far edge on a stand (an arch of two posts plus a top bar), tinted in the spinning team's colour. It has a yellow 3D arrow pointing at its face, a hot-pink prism with 7 faces, and decal face labels. "**GREEN** is Spinning" (team-coloured name + white) floats left of it and the result ("×4 to the bank") right of it, both as screen labels anchored to world points beside the drum. The screen-space reel is removed from terr |
| Tall tile cubes show their sides as streaks | **Flat tiles:** `tileHeight` 0.03 (now a config field read by both builder and presenter), small bevel, ~0.03 dark gap |
| Dense thin posts | **Few fat pegs:** `pegRows` 6, `pegColumns` 4, `pegRadius` 0.17, peg height ≈ ball diameter, all charcoal. The pitch rule stays (outer gap ≥ 1.3·D, inter-peg ≥ 1.2·D) |
| Cannons too small | **Cannons ×1.6:** base ~1.4 m, barrel ~1.3 m × 0.5 m |
| Flat checker floor | **Raised floor slabs:** 1.2 m slabs with 0.08 m gaps, small fixed-seed height variance (System.Random, editor-time), static-batched, purple shades, filling the visible floor |

### Rule change: arena decals vs HUD
Text that **belongs to a physical object** (a number painted on a board, a slot chip label, a drum face) is
a world-space **TextMeshPro** (3D) decal parented to that object. It moves and scales with the object, so it
can't drift between aspects. HUD text (captions, scores, clock, share bar) stays on the uGUI canvas. Legacy
`TextMesh` is still banned. The rule is recorded in CLAUDE.md → On-screen UI.

### Contracts for v3
- **Presentation `DrumReelView`** (`Presentation/VFX/DrumReelView.cs`), format-agnostic:
  ```csharp
  public sealed class DrumReelView : MonoBehaviour {
      public Renderer[] accentRenderers;                  // stand/rim, tinted by SetAccent
      public Color faceColor = #FF4FA0; public Color textColor = white;
      public void Build(int faceCount, float radius, float width); // prism mesh (runtime Mesh, faces' outward normals),
                                   // one TextMeshPro child per face; works in edit mode (builder calls it)
      public void SetFaces(string[] labels);              // relabel (count must match)
      public void SetIdle(int faceIndex);                 // snap to show that face toward local -Z (the camera side)
      public void Spin(int resultIndex, float seconds);   // rolls about local X, ≥ 2 full turns, ease-out, lands exactly
      public bool IsSpinning { get; }
      public event Action FacePassed; public event Action<int> Landed;
      public void SetAccent(Color color);                 // MaterialPropertyBlock _BaseColor on accentRenderers
  }
  ```
- **`TerritoryConfig`:** `public const int CurrentLayoutVersion = 3; public int layoutVersion;`
  `public void ResetLayoutToDefaults()` resets ONLY the grid, board, peg, slot, tile and golden fields to their v3
  defaults and sets `layoutVersion`. New field `tileHeight` (0.03). Defaults per the table above.
- **`TerritoryTeamRig`:** new fields `TMP_Text counterText`, `TMP_Text firingText`, `TMP_Text[] slotTexts`.
  `counterAnchor` / `slotLabelAnchors` stay (may be unused).
- **`TerritoryPresenter.Bind`:**
  `Bind(TileGridView gridView, ShareBarHud shareBar, DrumReelView drum, Transform drumHeaderAnchor, Transform drumResultAnchor,
  CaptionHud caption, WorldLabelLayer labels, FocusCameraRig cameraRig, TerritoryAudioKit audio, GameObject shotTemplate,
  Transform shotRoot, Material tileMaterial)`.
  - Counters, FIRING text and chip labels are written into the rig's TMP decals.
  - The drum header and result are WorldLabelLayer labels placed at the two anchors.
  - `drum.SetAccent(team colour)` on a spin, and back to neutral charcoal on idle.
- **HUD builder:** no screen reel. It keeps the bottom share bar, the captions above it and the top-right clock.

## Rules v4: spinner slot, power faces (2026-10-12, after the third playtest)

Playtest: release was too rare, so too many balls were live at once and the territory % barely moved. The user asked for
"concrete movement" elements (self-destruct, + line) **on the spinner**, and **a spinner slot**: the spinner only spins
when a ball lands in that slot. The golden ball is removed.

- **Board slots (5):** `SPIN, R, ×2, R, ×2`, index 0 = the board's OUTER edge.
  - Right-hand boards (YELLOW, RED) are mirrored, so SPIN is on the outer side of every board and the table reads symmetric.
  - `rig.slots[i]` always maps to `boardSlots[i]`; the builder assigns triggers in mirrored spatial order on right boards.
  - Fat pegs (radius 0.17) cap the board at 5 slots: 6 or more breaks the funnel gap rule.
  - Chip colours: SPIN gold `#FFC93A` with charcoal text, R white, ×2 pink.
- **Spin queue:** `maxQueuedSpins` 1 per team. A SPIN hit while that team already has one queued is ignored.
- **Golden ball removed:** the code, the config field (`goldenBallChance` deleted), the presenter tint and the audio hook.
  The gold chime is re-used for SPIN-slot hits.
- **Spin faces (9).** `SpinFace` gains `SpinFaceKind kind` (`Economy` uses `effect`; `Bomb`, `PlusLine`, `SelfDestruct`
  use `value`). These kinds are terr-specific, so they live in Territory, not `Shared/Economy`.

  | Face | Label | Weight | Effect |
  |---|---|---|---|
  | ×2 | `×2` | 4 | bank ×2 |
  | ×3 | `×3` | 3 | bank ×3 |
  | ×5 | `×5` | 1 | bank ×5 |
  | +25 | `+25` | 3 | bank +25 |
  | +100 | `+100` | 2 | bank +100 |
  | RELEASE | `FIRE` | 2 | release the bank now |
  | BOMB | `BOMB` | 3 | fire `bombShots` (3) bomb shots at once, regardless of ammo (they don't consume it). A bomb dies on its first paint and paints a disc of radius `bombRadius` (4 cells) around that cell |
  | + LINE | `+LINE` | 3 | pick a seeded enemy-owned cell (spin stream), paint its full row AND column (thickness `lineThickness` 2) to the team |
  | SELF DESTRUCT | `BOOM` | 2 | bank → `bankStart`, queued ammo cleared, live shots kept; a disc of radius `selfDestructRadius` (4) centred on a seeded OWN cell nearest the team's frontier turns `Unowned` (neutral grey, capturable by anyone) |

  In every paint operation, **base cells and their 8 neighbours are protected** (never repainted), so no power face
  eliminates a team instantly; eliminations still come from shots. The face labels on the drum are short
  (`BOOM` reads better than `SELF DESTRUCT` on a face); the captions spell them out.
- **Shared painter:** `Shared/Grids/GridPainter`, static and format-agnostic:
  `PaintDisc(grid, cx, cy, radius, owner, tick, Func<int,bool> isProtected)` and
  `PaintRow` / `PaintColumn(grid, index, thickness, owner, tick, isProtected)`. All return the cells changed and log
  through `SetOwner` (Unowned allowed as the owner).
- **Fewer live balls:** `maxLiveShotsPerTeam` 150 → 60, `maxBarrageSeconds` 15 → 8.
- **Migration:** `CurrentLayoutVersion` 4. `ResetLayoutToDefaults` also resets `boardSlots`, `spinFaces`, `maxQueuedSpins`,
  `maxLiveShotsPerTeam`, `maxBarrageSeconds` and the new bomb, line and self-destruct fields.
- **Presenter:**
  - Captions "{TEAM} BOMBS!", "{TEAM} + LINE!", "{TEAM} SELF DESTRUCTS!" with a focus and shake on the blast centre
    (sim events carry the cell).
  - Bomb shots render as dark spheres with a team-colour rim (`IsBombShot(slot)`), ~1.2 cells.
  - Drum result labels: "BOMB ×3!", "+ LINE!", "SELF DESTRUCT!".
- **Audio:** a low boom for bomb and self-destruct, and a rising sweep for + LINE.

## v5: centre-weighted drops, power-action animation (2026-10-12)

Playtest: drops spread evenly across the board, so the edge SPIN slots caught too many balls. The power actions
(bomb, line, self-destruct) happened with no animation.

- **Drops:**
  - `x = dropHalfWidth × dropSpreadFraction × (mean of dropSpreadSamples uniforms × 2 − 1)`.
  - Config: `dropSpreadSamples` 3 (an Irwin–Hall bell; 1 = uniform), `dropSpreadFraction` 0.7.
  - ALWAYS draw exactly `dropSpreadSamples` values per drop, even when the pool is empty.
  - These become layout-v5 fields, with `CurrentLayoutVersion` 5.
- **Reveal waves (simulation unchanged).** The sim still flips cells instantly. The *view* holds each affected cell's
  old colour until a per-cell delay elapses, then shows the new colour with a pulse.
  The presenter must register the hold BEFORE the frame's `SetOwners` uploads the change: handle events first,
  then upload the owners.

### Contracts (Presentation, format-agnostic)
```csharp
// TileGridView (add)
public void HoldAndReveal(IReadOnlyList<int> cells, IReadOnlyList<float> delays, bool pulse = true);
//   Each listed cell keeps its CURRENTLY DISPLAYED colour until its delay (seconds, from now) elapses, then shows
//   the owner colour from the latest SetOwners (pulsing if asked). Later SetOwners calls must respect pending holds.
//   Holds survive multiple version changes; a newer hold on the same cell replaces the older one.

// Presentation/VFX/BlastFx.cs: pooled, several can play at once, builds its own materials (URP Unlit/Particles)
public sealed class BlastFx : MonoBehaviour {
    public void Play(Vector3 worldCenter, float radiusWorld, Color color, float seconds = 0.7f);
    // expanding ground shockwave ring (to radiusWorld, fading), a short bright flash sphere, and a burst of ~30
    // small cubes in `color` thrown up and falling (a ParticleSystem built in code, gravity, ~1.2 s)
}
// Presentation/VFX/LineBeamFx.cs: pooled, several at once
public sealed class LineBeamFx : MonoBehaviour {
    public void Play(Vector3 from, Vector3 to, float widthWorld, Color color, float seconds = 0.45f);
    // an emissive bar that extends from `from` to `to` over `seconds`, then fades over ~0.3 s
}
```
### Presenter wiring
The presenter creates an `FX` GameObject with `BlastFx` + `LineBeamFx` in Start, so no builder change is needed.

| Event | Animation |
|---|---|
| `BombExploded` | Gather this tick's `Grid.Changes` near the centre. Delay = distance in cells / `blastRevealSpeed` (18 cells/s). `HoldAndReveal`, `BlastFx.Play(team colour)`, shake |
| `SelfDestructed` | Same, `BlastFx.Play` dark red `#8A1020`. The cells fade to neutral grey |
| `LinePainted` | Delay = distance along the line from the target / `lineRevealSpeed` (30 cells/s). Four `LineBeamFx` beams from the target to the row's and column's ends, each lasting distance / speed, in the team colour |
| Bomb shots in flight | Pulsing emissive red rim (or `_BaseColor` pulse) so they read as bombs |

## Pacing v8 (2026-10-12)
Playtest: 20 min (the forced decision) with only 1 elimination. Small banks (R about 40%), one tile per ball, and
3,072 tiles made territory crawl. The changes keep the same 12 × 16 m footprint, so the hand-framed camera still fits:
- grid 48×64 → **36×48** at cellSize 1/3 (1,728 tiles, bigger and more readable)
- `baseInsetCells` 4 → 3 (still ~1 m from the corner)
- `dropIntervalSeconds` 1.1 → 0.85
- `escalateAfterSeconds` 540 → 360

Estimated 2–2.5× faster territory movement; not measured. Next dials if still slow: `fireIntervalSeconds`, `bankStart` 2, or a later `decisionAtSeconds`.
