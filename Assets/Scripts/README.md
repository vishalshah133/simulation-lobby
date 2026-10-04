# Assets/Scripts — layer map

Navigation aid. The reasoning lives in `KnowledgeBase/architecture.md`; this is just "where does my
file go", plus the current build state.

```
Core  ←  Shared  ←  Simulations/<Format>
  ↑         ↑
  └── Presentation, Capture ──┘
```

Dependencies point **downward only**. Formats sit side by side and **never** reference each other —
if two formats need the same thing, it gets promoted into `Shared`. One `.asmdef` per folder below,
so a wrong reference is a compile error rather than a code-review catch.

| Folder | Assembly | References | State |
|---|---|---|---|
| `Core/` | `SimulationLobby.Core` | **nothing** | built |
| `Core/Editor/` | `SimulationLobby.Core.Editor` | `Core` | built |
| `Shared/` | `SimulationLobby.Shared` | `Core` | built (escalation, 2D physics, arena) |
| `Presentation/` | `SimulationLobby.Presentation` | `Core` | not built |
| `Capture/` | `SimulationLobby.Capture` | `Core` | built (`SeedScanner`, `SeedScanProfile`) |
| `Simulations/Escape/` | `SimulationLobby.Simulations.Escape` | `Core`, `Shared` | built (`esc`) |
| `Simulations/Breaker/` | `SimulationLobby.Simulations.Breaker` | `Core`, `Shared`, `Presentation` | built (`brk`) |
| `Simulations/<X>/` | `SimulationLobby.Simulations.<X>` | `Core`, `Shared`, `Presentation` | not built |
| `Simulations/Elimination/` | `SimulationLobby.Simulations.Elimination` | the above + wrapped formats | not built |

**The test for "is this in the right place":** could you delete an entire `Simulations/<Format>/`
folder and have everything else still compile? If not, something leaked.

## Core — what's in it

| File | Role |
|---|---|
| `SeededRandom.cs` | The only legal randomness in simulation code. `DrawCount` is the divergence tripwire. |
| `ISimulation.cs` | `Initialize(seed, config)` → `Tick(delta)` → `IsComplete` → `Result`. Lets `Capture` run a format it has never heard of. |
| `SimulationConfig.cs` | Base `ScriptableObject`: fixed timestep, max duration. Formats derive their own. |
| `SimulationRunner.cs` | The one place a simulation is stepped. `FixedUpdate` in a scene, `StepOnce()` headless — same code path. |
| `RunResult.cs` | Outcome data, JSON-serializable. No wall-clock, no frame count, by design. |
| `RunRecorder.cs` | Builds a `RunResult` as the run progresses; owns the tick counter. |
| `DeterminismVerifier.cs` | Runs a seed twice and compares. The regression check after touching `Core`/`Shared`. |
| `Editor/CoreSelfCheck.cs` | Menu: **Simulation Lobby ▸ Verify Deterministic Core**. Proves the above with no scene and no physics. |

## Writing a format

1. Derive a config from `SimulationConfig`, add `[CreateAssetMenu]`.
2. Implement `ISimulation`. In `Initialize`, build a `SeededRandom` and a `RunRecorder`; return
   `_recorder.Result` from `Result`.
3. In `Tick`, call `recorder.AdvanceTick()` first, then the format's rules. Call
   `recorder.Finish(CompletionReason.Finished)` the tick the win condition trips.
4. Drop a `SimulationRunner` in the scene, point it at the config, set a seed.

Rules that break determinism if ignored: no `UnityEngine.Random`, no `Time.deltaTime` /
`Time.time` in simulation code, no static mutable state, and `Presentation` reads state but never
writes it.
