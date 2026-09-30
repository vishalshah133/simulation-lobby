# Block: Escalation Rule

**Status:** `planned` · **Layer:** `Assets/Scripts/Shared/` · **Used by:** `esc`, `surv`

A component that changes one property of the simulation on each trigger event (usually a collision),
driving monotonic tension toward a threshold.

## Why

Escalation *is* the structure of threshold-tension formats — there's no competition to carry them.
Making it a parameterized component rather than per-format code means each escalation variant is a
new config asset, not new code. That's what makes a daily Shorts cadence realistic.

## Parameterized

| Knob | Options / typical |
|---|---|
| Target | `BallSize` · `BallSpeed` · `BoundaryScale` · `WallLayerCount` |
| Mode | `Additive` (+0.05/hit) · `Multiplicative` (×1.02/hit) |
| Magnitude | tuned per video; multiplicative is far more aggressive than it looks |
| Trigger | `OnCollision` · `OnInterval` |
| Clamp | hard min/max so escalation can't outrun the physics solver |

## Steps

1. `IEscalationTarget` interface — `Apply(float amount)`. One tiny implementation per target
   (`SizeEscalator`, `SpeedEscalator`, `BoundaryEscalator`). Composition, not a switch statement.
2. `EscalationRule` component: subscribes to the trigger, applies mode+magnitude to its target,
   clamps, and raises `OnEscalated(step, currentValue)` for HUD and audio to observe.
3. All values come from the format's config asset. No inline constants.
4. Escalation runs in `FixedUpdate` only — it's simulation state, not presentation.
5. Expose the current step count as the number the HUD displays.

## Acceptance criteria

- [ ] Swapping target in the config changes the video's premise with zero code changes.
- [ ] Multiplicative mode clamps cleanly and never exceeds the velocity ceiling from
      [collision-safety](collision-safety.md).
- [ ] Same seed → identical escalation curve (log step values, diff two runs).
- [ ] `OnEscalated` fires exactly once per trigger — no double-application on multi-contact frames.

## Gotchas

- **Multi-contact frames are the classic bug.** One collision can report several contact points;
  escalating per contact rather than per collision doubles your curve. Guard with a per-frame flag.
- Multiplicative growth is deceptive — ×1.05 per bounce reaches 10× in under 50 bounces. Start
  smaller than feels right.
- Combining two escalations (grow + speed) shortens runs sharply. Ship one at a time.
- Scaling a collider at runtime can cause interpenetration on the frame it grows. Grow *after* the
  bounce resolves, not during the collision callback.
