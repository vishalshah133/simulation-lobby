# Block: countdown-clock

**Status:** `planned` · **Layer:** `Shared` (pure kernel) · **Used by:** `timer`

A clock that reaches zero at exactly `Duration`: a monotone progress profile `p(t)` with `p(0)=0`, `p(Duration)=1` and a speed that is continuous (no jerks),
sampled at the fixed timestep. Used to drive a kinematic marble along a `TrackPath` and to set act boundaries (fractions of the duration).
Pure C#, no `UnityEngine.Time`. Acceptance: `p(Duration) == 1` within 1e-6 at 60 Hz and 50 Hz.
