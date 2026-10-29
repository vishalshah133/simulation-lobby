# mach-002 camera rework (Opus plan, 2026-10-29)

Replaces the follow camera in `RaceCameraDirector.cs`. The user rejected it four times ("choppy, not soothing"). This is the planner's spec,
implemented by a Sonnet agent. **Decisions below are final: implement them, don't redesign.**

## A. Diagnosis (ranked by visible impact)

1. **Obstruction clamp pops** (`KeepOutOfGeometry`): sphere-cast hits chute walls, legs and the screw lifts' glass tube colliders, then moves the distance at a
   constant 10 m/s in / 3 m/s out, an instant velocity step toggled by binary hits.
2. **Shot generators flip on noisy conditions.** Distance gate on the station shot is tested every frame; `StationNameOf(leader)` flips at connector midpoints;
   the ski shot re-picks a raw racer every frame; every shot uses a different SmoothDamp constant, and SmoothDamp on a step target accelerates instantly (about 42 m/s² for a 15 m step).
3. **Heading from per-frame velocity** orbits the camera about twice per helix pass and with the vortex swirl.
4. **Noisy followed point and spread**: rank-weighted mean of raw positions (the set changes on every rank swap); raw max-distance spread drives distance and height.
5. Position and look smoothed separately (partial). 6. Teleports/cannon flights: a doomed ski-jump racer stays ranked until the cannon fires, then the subject jumps. 7. Frame-rate/interpolation: refuted (positions are interpolated).

## B. Design: a drone rig that follows an arc-length focus along a smoothed course rail

Everything fed to the camera is smooth by construction.

- **Focus** is a scalar arc length `s` along the course (never a raw position), filtered by a critically damped tracker with velocity feed-forward and rate limits. Leader changes go through hysteresis.
- **Rail**: the world focus is a lookup on a pre-smoothed rail plus a slow-filtered offset toward the real racers.
- **Heading** comes from a pre-smoothed heading table along the rail (analytic, noise-free).
- **Pose state**: look point `Lk` (spring with feed-forward), yaw `psi`, pitch `theta`, distance `d`, each a critically damped spring with rate and acceleration limits.
  Camera position = `Lk + d*dir(psi,theta)`. The camera translates rigidly with the focus like a drone dolly; angular velocity is bounded by the yaw and pitch rate limits whatever the racers do.
- **Set pieces** are s-range zones that blend psi/theta/d/focus-pull with timed smoothsteps, never switches.
- **Phase changes** (grid/countdown, winner, outro) crossfade their target tuples over seconds.
- **Obstruction** is a slow rate-limited pitch/distance bias, never a position clamp. FOV is fixed (38 deg vertical); only distance changes.
- **Inputs**: `IRaceState.RacerPosition` (interpolated transform) plus `Progress`, read in LateUpdate. No 100 Hz history.

### B1. Course rail (`CourseTrack`, pure)
- Vertices: `RaceProgress` vertices `a[0..2n-1]`; cumulative length computed exactly as `RaceProgress` does, so `Length == RaceProgress.TotalLength` (about 173.1 m in the current scene).
- Racer arc length `S_i = Progress(i) * L` (unwrapped across laps). Lookups use `Mod(S, L)`.
- Resample the closed polyline at `N = ceil(L/0.5)` points, `ds = L/N`, giving `raw[j]`.
- `rail[j]` = circular Gaussian average of `raw`, sigma = 4 m (weights `exp(-0.5*(k*ds/sigma)^2)`, |k| <= ceil(3*sigma/ds)).
- `heading[j]` = normalised Gaussian (sigma = 8 m) sum of horizontal tangents `h_k = rail[k+1]-rail[k-1]` with y = 0. If |sum| < 1e-4 copy the previous sample (second pass for index 0).
- Lookups: `Rail(s)`, `Raw(s)` linear; `Heading(s)` normalised lerp; `ProjectNear(p, sGuess, back, ahead)` brute-force over `raw` samples in the window, then linear refine on the best segment, returns s.
- `InteriorOnRight = signedArea(xz of vertices) < 0` with signed area = 0.5*sum(x_i*z_{i+1} - x_{i+1}*z_i). (Expected true; log it.)
- `right(f) = Cross(Vector3.up, f)`; `outside(f) = InteriorOnRight ? -right(f) : right(f)`.

### B2. Azimuth convention
- For horizontal forward `f` and azimuth phi (deg): `dirH = -f*cos(phi) + outside(f)*sin(phi)`. phi=0 directly behind, 90 outside flank, 180 in front.
- `psi = atan2(dirH.x, dirH.z)` (deg). `dir(psi,theta) = (sin(psi)cos(theta), sin(theta), cos(psi)cos(theta))`. `pos = Lk + d*dir`.
- `aim = Lk + camRight*(compX*d*tanH)`; `rot = LookRotation(aim - pos, up)`. `compX` = 0.15 for aspect > 1 and 0 for portrait (action sits about 15% left of centre, away from the right-hand leaderboard).

### B3. Per-frame signals (Racing/FinalLap; Winner mode reuses them with forced F and K)
1. **Eligibility** `elig_i = smoothstep(8,10, simTime - tRecycle_i)` (1 if never recycled; `tRecycle_i` = simTime when `Recycles(i)` was seen to increase). Multiply by `(1 - smoothstep(5,8, |pos_i - Raw(Mod(S_i))|))`. Set 0 if `FinishTime(i) >= 0`.
   `smoothstep(a,b,x)`: `t = clamp01((x-a)/(b-a)); t*t*(3-2t)`.
2. **Leader hysteresis.** Candidate `c` = argmax S_i over elig >= 0.5. If `F < 0 || elig_F < 0.5 || finished_F` switch to c immediately. Else if `c != F`, `gap = S_c - S_F`:
   gap >= 8 m switch now; gap >= 2 m: `hold += dt` (reset if c differs from holdCandidate), switch when hold >= 1.5 s; otherwise hold = 0. A switch sets `leaderSwitched = true` for this frame.
3. **Pack.** Followers = eligible racers other than F sorted by S descending, first K-1. **K = 6, G = 24 m**; FinalLap K = 4, G = 18; Winner K = 1.
   Start `wsum=1, Rsum=pos_F, Ssum=S_F, tail=front=0`, yMin/yMax from pos_F.y. For follower k (0-based) racer i, `g = S_F - S_i`:
   if g < 0: `front = max(front, -g*elig_i)`, fade = 1; else `fade = 1 - smoothstep(0.65G, G, g)`, `tail = max(tail, g*fade*elig_i)`.
   `w = 0.85^(k+1)*fade*elig_i`; `Rsum += w*pos_i; Ssum += w*S_i; wsum += w`; if w >= 0.25 update yMin/yMax.
   `E_raw = tail + front`; `R = Rsum/wsum`; `zPack = Ssum/wsum`; `O_raw = R - Rail(Mod(zPack))`, horizontal part clamped to <= 6 m, |y| <= 4; `V_raw = yMax - yMin`.
4. **Speed feed-forward.** If `!leaderSwitched && |S_F - S_Fprev| < 2`: `inst = (S_F - S_Fprev)/dt; vff += (inst - vff)*(1 - exp(-dt/0.6))`. Clamp vff to [0, 14].
5. **Focus target** `z = S_F - alpha*E_f` with **alpha = 0.4**: leader sits ahead of centre, pack fills behind.

### B4. Filters (substepped, see B8)
`CameraSpring.Step(x, v, target, targetVel, omega, vMax, aMax, h)`:
```
a = omega^2*(target - x) + 2*omega*(targetVel - v); a = clamp(a, +-aMax)   // Vector3: clamp by magnitude
v = clamp(v + a*h, +-vMax)   (s tracker: v = clamp(v + a*h, vMin, vMax))
x += v*h
```
| Filter | target | ff | omega (rad/s) | vMax | aMax |
|---|---|---|---|---|---|
| s tracker | z | vff | 1.4 | v in [-2, 16] m/s | 5 m/s^2 |
| E_f (extent) | E_raw | 0 | 0.7 | 4 m/s | 3 |
| O_f (racer offset, Vector3) | O_raw | 0 | 0.9 | 3 m/s | 3 |
| V_f (vertical extent) | V_raw | 0 | 0.7 | 3 m/s | 3 |
| Lk (look point, Vector3) | Lk_t | (Lk_t - Lk_t,prev)/dt, magnitude clamped to 18 | 3.0 | 18 m/s | 8 m/s^2 |
| psi (deg) | psi_t (unwrapped: `psi + DeltaAngle(psi, psi_t)`) | 0 | 1.1 | 18 deg/s (25 deg/s for the first 6 s after GO) | 30 deg/s^2 |
| theta (deg) | theta_t | 0 | 1.0 | 8 deg/s | 15 deg/s^2 |
| d | d_t | 0 | 0.8 when d_t > d, 0.5 when d_t < d | 3 m/s | 2 m/s^2 |

### B5. Follow targets
- `lead = clamp(v_s*0.5, 0, 4)`. `base = Rail(Mod(s + lead)) + O_f*ow + up*0.3`. `Lk_t = base + sum_z w_z*pull_z*(focus_z - base)`.
- `phi_f = lerp(30, 62, clamp01((aspect - 0.5625)/(1.7778 - 0.5625)))`. `psi_f = yaw(dirH(Heading(Mod(s)), phi_f))`. Follow pitch `theta_f = 24`.
- `psi_t = psi_f + sum w_z*D_z`; `theta_t = theta_f + sum w_z*(theta_z - theta_f)`; `ow = 1 + sum w_z*(ow_z - 1)`.
- **Distance fit** (tanV = tan 19 deg = 0.3443, tanH = tanV*aspect): `fracFront = |alpha*E_f - lead|`, `back = (1-alpha)*E_f + lead`.
  `Wh = max(fracFront, back)*max(0.5, |sin(phi_eff)|) + 2.5` where phi_eff is the current azimuth of the camera relative to the heading.
  `d_h = Wh/(tanH*0.72)`, `d_v = (0.5*V_f + 1.0)/(tanV*0.72)`. `dMinEff = 11 + sum w_z*(minDist_z - 11)` (FinalLap uses 10 instead of 11).
  `d_t = clamp(max(d_h, d_v, dMinEff), 11, 32)`, then `d_t *= (1 - 0.2*occ)`. (16:9: E = 0 gives 11 m; E = 20 m gives about 29 m.)
- **Ground guard**: `theta_t = clamp(theta_t + 18*occ, max(8, asin(clamp((0 + 1.0 - Lk.y)/d, -1, 1))*Rad2Deg), 65)`.

### B6. Zones (set pieces)
- Station i range `[s0, s1]` = `[cum(2i), cum(2i) + len(2i)]`; active range `[s0 - pre, s1 + post]`. Overlapping neighbours are cut at the midpoint between A.s1 and B.s0 (compute in Begin).
- `f_z` = horizontal `station.transform.forward`; `focus_z = station.cameraFocus.position` (or `activityBounds` centre); `psi_z = yaw(dirH(f_z, phi_z))` fixed in the world.
- Weights: target on when `Mod(s - lo, L) < (hi - lo)`. On: `u = min(1, u + dt/2.0)`. Off and `activeTime >= 3 s`: `u = max(0, u - dt/2.5)`. `w = u^2(3-2u)`; if sum w > 1 scale all by 1/sum.
- Continuous yaw delta: when u leaves 0, `D_z = DeltaAngle(psi_f, psi_z)`; otherwise `D_z = D_prev + DeltaAngle(D_prev, DeltaAngle(psi_f, psi_z))` (never flips through +-180).

| match (case-insensitive StartsWith on displayName) | phi | theta | minDist | pull | ow | pre | post |
|---|---|---|---|---|---|---|---|
| Helix | 95 | 24 | 15 | 0.5 | 0.3 | 6 | 3 |
| Screw Lift | 90 | 18 | 16 | 0.25 | 1.0 | 6 | 2 |
| Peg Wall | 130 | 22 | 12 | 0.6 | 0.6 | 6 | 3 |
| Bucket Wheel | 90 | 16 | 14 | 0.5 | 0.6 | 5 | 3 |
| Ski Jump | 80 | 16 | 13 | 0.5 | 0.8 | 15 | 4 |
| Vortex | 55 | 50 | 13 | 0.7 | 0.2 | 6 | 4 |

No Start Gate zone during racing. Current scene: station s ranges Start 0-5.4, Helix 10-20, Screw 1 23-50.8, Peg 56.9-62.7, Wheel 65.8-76.6, Ski 85-95.6, Vortex 106.5-114, Screw 2 137.3-165.1, closing connector to 173.1.

### B7. Modes and phases
- **Opening (Grid/Countdown)**: `Lk_t` = mean of `IStartGate.GridSlot(0..15)` + up*0.2 (static). `openDir`, `dOpen` follow the existing `ComputeOpeningPose` (gate `cameraOffset` direction with y >= 0.35; `max(|offset|, Fit(maxXZ + 1.6, sizeY + 2.4, 1.2))`, over the slots). Snap every state on the first frame with zero velocities.
  Countdown: `d_t = lerp(dOpen, max(min(5, dOpen), 0.62*dOpen), smoothstep01(tc/3))`, `tc` accumulates `Time.deltaTime` from when Countdown is seen; d omega 1.5, vMax 4.
- **Opening -> Follow (GO)**: initialise the s tracker to `s = z, v = 0`; initialise `O_f = Lk - (Rail(Mod(z)) + up*0.3)` so the follow target equals the current look point at t0. Crossfade target tuples over **3.0 s**.
- **Follow -> Winner (Finished)**: F = WinnerIndex, K = 1. `S_w = Laps*L + ProjectNear(pos_w, sPrevLocal, 10, 25)` (start `sPrevLocal` at `Mod(S_F)`; finished Progress is frozen but the ball keeps rolling). Zone targets off. `d_t = max(9, fit)`, phi 60, theta 22. Crossfade **1.5 s**.
- **Winner -> Outro** (after 6 s in Finished): `Lk_t` = grid centre + up*0.2; psi/theta from openDir; `d = 1.1*dOpen`. Crossfade **4 s**, all pose omegas x 0.6, d vMax 5.
- **Crossfade blending** `blend(A, B, w = smoothstep01(t/T))`: look lerp; yaw `A.yaw + w*Dx` (Dx continuity-tracked like D_z); pitch and d lerp. Both tuples computed live during the crossfade.
- **Cannon flight**: no dedicated shot; a teleport never moves the focus (eligibility handles it). **Final lap**: K, G and dMin change; the E spring absorbs it.

### B8. Time handling
- `dt = min(Time.deltaTime, 1/15)`; if dt <= 0 keep the last pose. `n = ceil(dt*120)`, `h = dt/n`. Targets are computed once per frame; all trackers, springs, zone weights and the occlusion integrator step n times by h.
- Offline capture (1/60 -> 2 substeps) is deterministic given the sim; the editor with irregular dt stays stable. Timers use summed dt. Never use `SimTime` for camera timing except recycle age.

### B9. Obstruction
- Probe (adapter, after posing): `SphereCastNonAlloc(origin = Lk + dir*1.5, r = 0.35, dir, len = d - 2.0, mask)`. Ignore triggers, non-kinematic rigidbodies (marbles) and any collider whose GameObject name contains "glass" (case-insensitive). Also `CheckSphere(pos, 0.5)` with the same filter. `blocked = any hit`.
- Core: `occ += blocked ? h/0.6 : -h/2.0`, clamped to [0,1]. It acts only through the theta_t (+18*occ) and d_t (x(1 - 0.2*occ)) targets, so it is rate-limited by the theta/d springs. **The camera position is never clamped.**

## C. What to build

**Create:**
1. `Assets/Scripts/Presentation/Cameras/CameraSpring.cs`: `namespace SimulationLobby.Presentation`, `public static class CameraSpring` with `Step(ref float x, ref float v, float target, float targetVel, float omega, float vMin, float vMax, float aMax, float h)`, the Vector3 overload (`ref Vector3 x, ref Vector3 v, Vector3 target, Vector3 targetVel, float omega, float vMax, float aMax, float h`, magnitude clamps), `SmoothStep01(float t)`, `SmoothStep(float a, float b, float x)`. Pure, format-agnostic.
2. `Assets/Scripts/Simulations/MarbleMachine/Camera/CourseTrack.cs` (B1): ctor `CourseTrack(IReadOnlyList<Vector3> vertices, float ds = 0.5f, float railSigma = 4f, float headingSigma = 8f)`; members `Length`, `InteriorOnRight`, `Rail`, `Raw`, `Heading`, `ProjectNear`, `VertexDistance(int v)` (cumulative), `static Mod`.
3. `.../Camera/RaceCameraSettings.cs`: `[Serializable] public sealed class RaceCameraSettings` holding every constant above (names as in B, defaults as listed) plus `ZoneShot[] zones = DefaultZones()`; `[Serializable] public struct ZoneShot { string match; float azimuth, elevation, minDistance, focusPull, offsetWeight, preRoll, postRoll; }`.
4. `.../Camera/RaceCameraCore.cs`: pure, no MonoBehaviour, no Physics, no Time.
```
public struct RaceCameraInput { public RacePhase phase; public float simTime; public int count; public Vector3[] pos; public float[] progress; public int[] recycles; public bool[] finished; public int winner; public int laps; }
public struct ZoneDef { public string name; public float lo, hi; public Vector3 forward, focus; public ZoneShot shot; }
public struct CameraPose { public Vector3 position, look, aim; public Quaternion rotation; }
public sealed class RaceCameraCore {
  public RaceCameraCore(CourseTrack track, RaceCameraSettings s, ZoneDef[] zones, Vector3 gridCentre, Vector3 openDir, float openDistance, float fovDeg);
  public CameraPose Step(in RaceCameraInput inp, float dt, float aspect, bool blockedLastFrame);
  public RaceCameraDebug Debug { get; }   // s, v_s, E_f, F, mode, zone weights, occ, psi, theta, d
}
```
   Step order: clamp dt; update tRecycle from recycle counts; mode from phase (Grid/Countdown -> Opening, Racing/FinalLap -> Follow, Finished -> Winner, then Outro after 6 s; start a crossfade on change); follow signals (B3) when mode or crossfade source is Follow/Winner; zone targets; build target tuples and blend; feed-forward for Lk; substep loop (s, E, O, V, zone u, occ, Lk, psi, theta, d); ground guard; compose the pose (B2). On the first call, snap.
5. `.../Camera/RaceTrace.cs`: data and IO. Header (version, L, vertices, per-station {name, forward, focus, cameraOffset, s0, s1}, grid slots, laps, racerCount, fixedDt); per tick (simTime, phase byte, leader short, winner short, per racer {rb.position, progress, rank byte, recycles byte, finished bool}); `Save/Load(path)` via BinaryWriter under `Temp/RaceCamTraces/`; `Sample(float t, RaceCameraInput into)` (tick k = floor(t/fixedDt), discrete fields from tick k, positions `lerp(pos[k-1], pos[k], frac)` to emulate interpolation); `static RaceTrace HeaderFromScene(MachineRaceSimulation sim)` (read-only).
6. `.../Camera/RaceTraceRecorder.cs`: static, play-mode only, takes over the live run (OPTIONAL, see "Do not do" below).
7. `.../Editor/RaceCameraHarness.cs` (`EditorTools` namespace): `Synthetic(int seed, RaceTrace header, int laps = 2)`; `Evaluate(trace, settings, DtProfile {Fixed60, Fixed30, Jitter}, int dtSeed, float aspect) -> CameraMetrics`; `RunSuite(string tracePathOrNull) -> string` (null builds a synthetic trace from the open scene, logs and writes `Temp/RaceCamTraces/report.txt`); `[MenuItem("Simulation Lobby/Tools/Race Camera Smoothness Test (mach-002)", false, 950)]`.

**Change:**
- `RaceCameraDirector.cs`: rewrite in place. Keep the file, `.meta` GUID, class name and `[DefaultExecutionOrder(-50)]`. Keep serialized wiring names `_simulation`, `_rig`, `_camera` and `public void Bind(MonoBehaviour simulation, TourCameraRig rig, Camera camera)` unchanged (`RacePresenter.Bind` and the saved scene depend on them). Replace every tunable with `public RaceCameraSettings settings = new RaceCameraSettings();` (new names, do not reuse old field names); add `public LayerMask obstructionMask = Physics.DefaultRaycastLayers;` and `public bool logLiveMetrics;`.
  `Start`: same rig/camera resolution as now (`rig.Bind`, `SetLookTracking(null)`, `SetDrift(0,0)`). `Begin` on the first LateUpdate: `new RaceProgress(state.Stations)`, vertices via the new accessor, `new CourseTrack`; zones from the stations plus `settings.zones` with overlap splitting; grid centre and open pose from `(IStartGate)state.Stations[0]`; log `[RaceCam] L=.. interiorRight=.. zones: name lo..hi`.
  `LateUpdate`: fill reused input arrays from IRaceState (`RacerPosition`, `Progress`, `Recycles`, `FinishTime >= 0`, `WinnerIndex`, `Laps`, `Phase`, `SimTime`); `pose = core.Step(input, Time.deltaTime, camera.aspect, _blocked)`; `_rig.SnapTo(pose.position, pose.aim)`; `_blocked = Probe(pose)` (B9). With `logLiveMetrics`, track max acceleration, angular velocity and pops and log every 10 s.
  Delete: `KeepOutOfGeometry`, `TrySkiShot`, `FindStationShot`, `UpdateHeading`, the cut logic, `LimitOffset` and all old state.
- `RaceProgress.cs`: add only `public Vector3 Vertex(int segment) => _a[segment];` (read-only, no behaviour change).
- No change needed to `RacePresenter.cs`, `MachineRaceSceneBuilder.cs`, `TourCameraRig.cs`. No scene rebuild required.

**Dependency rules:** everything race-specific stays in `Simulations/MarbleMachine`; the only Presentation addition is the generic `CameraSpring`. The camera never writes sim state.

## D. Test plan

The core plus `CourseTrack` depend only on UnityEngine math types. Harness:
```
var trace = RaceTrace.Load(path) or RaceCameraHarness.Synthetic(seed, RaceTrace.HeaderFromScene(sim));
var core = new RaceCameraCore(new CourseTrack(trace.vertices), settings, zonesFrom(trace.header), trace.gridCentre, trace.openDir, trace.openDist, 38f);
t = 0; while (t < trace.end) { dt = profile.Next(); t += dt; trace.Sample(t, input); pose = core.Step(input, dt, aspect, false); metrics.Add(t, dt, pose, input); }
```
**Synthetic trace** (seeded `System.Random`, 2 laps, 100 Hz ticks): base speed 0.9 m/s inside Screw Lift ranges, 2.5 Bucket Wheel, 3 Helix and Vortex, 8 Ski Jump, 1.5 Start Gate, 6 elsewhere. Per racer factor `0.92 + 0.012*(shuffled index)` times `1 + 0.08*sin(0.3t + phi_i)`. Position = `Raw(S)` + lateral weave `0.5*sin(2*pi*0.7t + phi)` + Gaussian jitter 0.05/tick + vertical bounce `0.08*|sin(2*pi*3t)|` + in the helix a 2.6 m coil offset (2 turns over the range) + in the vortex a 1.5 -> 0.3 m spiral (3 turns). Progress = S/L max-held; ranks with a 0.002 epsilon. Phases: Grid 0-6 s, Countdown 6-9 s, then Racing; FinalLap when the leader starts lap 2; Finished when the leader reaches 2L; continue 15 s. Stress events: rank 1 at its first Ski Jump exit misses (4 s moving to the cannon with progress held, then recycles++, progress resets to its lap, a 3 s ballistic arc with a 15 m apex to the grid centre, then resumes); rank 3 does the same on lap 2.

**Profiles:** Fixed60 (dt = 1/60), Fixed30, Jitter (seeded uniform [1/90, 1/30] plus a 0.1 s hitch every ~5 s). Aspects 16/9, and 9/16 report-only.

**Metrics** (double, racing phases unless noted): `v_k = dp/dt`, `a_k = dv/mean(dt)`, `j_k = da/mean(dt)`; angular speed `angle(fwd_k, fwd_{k-1})/dt` and angular acceleration. Pops = frames where |a| > 20 m/s^2, angular acceleration > 200 deg/s^2, or |dp| > 0.6 m. Framing: project racers with FOV 38 and the aspect (`v = Inverse(rot)*(p - pos)`, `ndc = (v.x/(v.z*tanH), v.y/(v.z*tanV))`); safe area for 16:9 x in [-0.92, 0.60], y in [-0.85, 0.70], v.z > 0.5; report leader-in-safe, all-top-3, mean top-6 fractions (exclude 2 s after a leader switch and racers with elig < 0.5). Distance/height: min |pos - look|, min pos.y, leader on-screen diameter in px at 1080. Cross-profile: after resampling to Fixed60 timestamps, position p99 and rotation p99 deviation.

| Metric | Pass threshold |
|---|---|
| Pops | 0 |
| Max angular velocity | <= 25 deg/s (p95 <= 15) |
| Max acceleration | <= 10 m/s^2 (p95 <= 4) |
| Jerk p99 | <= 40 m/s^3 |
| Max camera speed | <= 18 m/s |
| Leader in safe area | >= 97% |
| All of top-3 in frame | >= 85% |
| Mean top-6 in frame | >= 75% |
| Min distance to look point | >= 7.5 m |
| Camera y | >= 1.0 |
| Leader on-screen diameter | >= 14 px for 95% of frames |
| Jitter and Fixed30 runs | same thresholds x 1.25 |
| Deviation from Fixed60 | position p99 <= 0.5 m, rotation p99 <= 1.5 deg |

## E. Risks and things to check first
1. Rail vs real chutes: `RaceProgress` uses chords, so `O_f` is clamped to 6 m; print per 5 m bucket the max |O_raw| from a live trace; if > 5 m anywhere (suspect the 23 m Vortex -> Screw Lift 2 chord), bake the real connector polylines later.
2. Doomed ski-jump racers stay ranked until the cannon fires (sim behaviour); the camera bounds the effect through pack weights, hysteresis and the off-course fade.
3. Progress staircases in the helix/funnel should be absorbed by the s tracker; confirm a smooth s(t).
4. Opening shot direction: the gate's authored offset looks back at the grid; GO then swings up to ~110 deg at <= 25 deg/s. If it reads badly set the opening azimuth to ~35 deg (behind-outside).
5. Peg Wall/Ski Jump positions may clip the bucket wheel or cannon; check `occ` at s ~57-63 and 85-96.
6. Glass-name filter is heuristic. 7. 9:16 has no threshold (d often sits at max 32 m). 8. Safe-area numbers assume the board width.
After a rebuild/Play, check the `[RaceCam]` Begin log (L ~173.1, interiorRight = true, non-overlapping zones), the harness report, then a live play with `logLiveMetrics` on: helix (no orbiting), Screw Lift 1, the Ski Jump miss, the GO swing.
