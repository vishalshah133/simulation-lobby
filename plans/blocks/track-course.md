# Block: Track Course

**Status:** `built` (untested in Unity; promotion paid for by surv-002) · **Layer:** `Shared/Tracks` + a new `Shared/Editor` assembly · **Used by:** `race`; `surv` (Lava Run) next; `elim` heats later

A course built from data: `TrackSection`s laid end to end (straights, banked curves, helices, width changes), with
obstacles placed by section + metres (pegs, plinko, pinwheel, fork divider, bottleneck, chicane, bumps), swept into
meshes and colliders, plus a `TrackPath` for progress along it. Built for `race`. A second format needs it, so it's
promoted, per CLAUDE.md rule 2. Copying it into `surv` isn't allowed.

## Pieces

| Piece | From (today) | To | Job |
|---|---|---|---|
| `TrackSection`, `TrackSectionKind`, `ObstacleKind`, obstacle spec, the layout `ScriptableObject` | `Simulations/Race/RaceTrackLayout.cs` | `Shared/Tracks` (rename the SO `TrackLayout`) | Course data |
| `TrackMeshBuilder` | `Race/Editor` | `Shared/Editor/Tracks` | Sweeps the layout into floor + rail meshes and colliders |
| `ObstacleBuilder` | `Race/Editor` | `Shared/Editor/Tracks` | Places obstacles. Pinwheels are `KinematicSpinner`s stepped by the sim |
| `TrackPath` | already `Shared/Tracks` | unchanged | Arc-length progress, nearest point |
| `SaveMeshFresh` | `Race/Editor` | `Shared/Editor/Tracks` | Delete-then-create mesh save (the stale-mesh gotcha in CLAUDE.md) |

## Rules

- **Keep the existing race assets loading.** Put `[MovedFrom(true, "SimulationLobby.Simulations.Race", "SimulationLobby.Simulations.Race", "RaceTrackLayout")]`
  on the moved `ScriptableObject` (and `[MovedFrom]` on moved serialized structs/enums) so `RaceTrackLayout_GrandPrix`
  keeps its data. Verify by opening the asset after the move: section and obstacle counts must match.
- `Shared/Editor` assembly: editor-only, references `Core` + `Shared`, never a format.
- Anything race-only (finish gantry, leaderboard ordering, chase camera choice) stays in `Race`.
- New shared pieces this block adds for `surv-002`: a **basin with door + trapdoor** section end, and a **railless
  bridge** section flag (rail height 0). Both are data on the layout, not a format flag.

## Acceptance

- [ ] `race` scene rebuilds from the menu and plays the same seed to the same result as before the move
- [ ] `Simulations/Race` deleted (in a scratch branch) → `Shared`, `surv` and everything else still compile
- [ ] No `using SimulationLobby.Simulations.Race` anywhere outside `Simulations/Race`

## Built (2026-10-16)

- Moved with `.meta` files (GUIDs kept): `TrackLayout.cs` (was `RaceTrackLayout`, namespace `SimulationLobby.Shared.Tracks`),
  `Shared/Editor/Tracks/TrackMeshBuilder.cs`, `ObstacleBuilder.cs`. Both builders are now `public static` in
  `SimulationLobby.Shared.EditorTools`. New `TrackMeshAssets.SaveMeshFresh(mesh, name, folder)` (Race keeps a one-line wrapper).
- New assembly `SimulationLobby.Shared.Editor` (Core + Shared only; does not reference Presentation or any format).
- Changed from plan: **railless is `TrackSection.railless` + `TrackSection.railHeight`** (0 = layout default), plus
  `TrackLayout.defaultRailHeight` (0.34). `RailHeightFor(section)` resolves it; `Sample.railHeight` carries it into
  `BuildBody`, which drops the inner rail walls on railless rings (floor edge trim stays, no rail collider).
- Still to do for Lava Run: the basin with door + trapdoor section end (not built in this pass).
- Verify in Unity: open `RaceTrackLayout_GrandPrix.asset` (17 sections, 9 obstacles) and rebuild the race scene.
