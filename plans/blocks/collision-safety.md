# Block: Collision Safety (no tunneling)

**Status:** `planned` · **Layer:** scene setup + `ProjectSettings` · **Used by:** `esc`, `race`, `rom`

Stops fast-moving objects passing through walls they should have hit.

## Why

In `esc` this is existential: a ball that escapes *through* a wall instead of over the boundary
invalidates the video's entire premise, and viewers notice. In `race` it means marbles falling out
of the track. It's the single most common way a physics Short fails.

## Parameterized

| Knob | Typical | Notes |
|---|---|---|
| Fixed timestep | `0.01` | Drop to `0.005` for very fast/small objects. Costs CPU linearly. |
| Wall thickness | ≥ 2× object radius | Cheapest fix — use it before lowering the timestep. |
| Max velocity clamp | per-format | Hard ceiling so escalation can't outrun the solver. |

## Steps

1. Set `Time.fixedDeltaTime` from config in `SimulationRunner` (not in the Inspector — it must
   travel with the config).
2. Rigidbody collision detection → **Continuous** (`Continuous Dynamic` in 3D) on every moving object.
3. Walls: thick colliders. For a circular boundary, prefer many short segments or a mesh/polygon
   collider over a thin ring.
4. Clamp velocity in `FixedUpdate` after the escalation rule applies. Escalation must never push
   speed past what the timestep can resolve.
5. Lower `Default Contact Offset` if objects visibly float before contact.
6. Add a **containment assertion** during development: if the object's position leaves the boundary
   without the legitimate escape condition firing, log loudly and fail the run. Catches tunneling
   during seed scans instead of during the edit.

## Acceptance criteria

- [ ] 1000-seed scan with the containment assertion on → zero illegitimate escapes.
- [ ] At maximum escalated velocity, the object still registers every wall hit (check via bounce count
      vs. audio note count — they must match exactly).
- [ ] Lowering the timestep further does not change outcomes (proves convergence; if it does, the
      current timestep is too coarse and old seeds are suspect).

## Gotchas

- Perfectly elastic collisions (restitution 1, zero damping) mean energy never bleeds off — that's
  intended for `esc`, but it makes tunneling more likely over long runs.
- Continuous collision detection is significantly more expensive. Apply it to moving objects only,
  never to static walls.
- The bounce-count-vs-note-count check is the best tunneling detector available — see
  [bounce-audio-sequencer](bounce-audio-sequencer.md). A missed note *is* a missed collision.
