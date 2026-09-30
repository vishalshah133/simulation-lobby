using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Survival
{
    /// <summary>
    /// "Wall vs Ball" survival: one wall of blocks takes hit after hit from an escalating ball until
    /// nothing is left standing. The video's question is "how many hits will it take?", so the run is
    /// open-ended — it ends when the wall is gone, and the hit count at that moment is the answer.
    /// </summary>
    /// <remarks>
    /// Grew out of the <c>impact</c> prototype (a single launch, which settled in three or four seconds)
    /// and its nine-wave rebuild, which reformed the survivors into a fresh, smaller wall before every
    /// wave. This version never rebuilds the wall: damage stays where it lands, so the viewer can read
    /// the whole history of the run off any single frame, and a block's elimination is measured from
    /// its original slot, so small knocks accumulate into a knockout exactly as they read on screen.
    /// <para>
    /// Blocks are spawned procedurally rather than pre-placed because their jitter must come from the
    /// run's seeded RNG; a pre-placed grid could not vary reproducibly by seed. The ball is a fixed
    /// scene object that gets rescaled and relaunched, the same pattern <c>esc</c> uses.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class WallSurvivalSimulation : MonoBehaviour, ISimulation
    {
        /// <summary>Where the run is in its arc. Presentation reads this to know which beat to play.</summary>
        public enum RunPhase
        {
            /// <summary>The guessing window: the intact wall and the question, nothing moving yet.</summary>
            Opening,

            /// <summary>A shot's anticipation hold — ball placed and still, riser building.</summary>
            HitHold,

            /// <summary>The ball is in flight and the debris is still moving.</summary>
            HitFlight,

            /// <summary>Everything has settled; knocked-out blocks are being removed one by one.</summary>
            HitResult,

            /// <summary>The reveal: the demolished (or surviving) wall and the final hit count.</summary>
            FinalHold,

            /// <summary>Run over.</summary>
            Complete
        }

        [Header("Scene wiring")]
        [SerializeField] Rigidbody _ball;
        [SerializeField] CollisionDetector3D _ballDetector;

        [Tooltip("Defines the wall's plane and facing. Blocks tile across its local X/Y; the ball " +
                 "launches from in front of its local +Z, back toward its position.")]
        [SerializeField] Transform _fieldCenter;

        [SerializeField] Transform _blocksParent;
        [SerializeField] Material _blockMaterial;
        [SerializeField] Material _ballMaterial;

        WallSurvivalConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;
        RigidbodyFieldSettleTracker _settleTracker;

        /// <summary>Every block still standing, in a fixed order. Eliminated blocks leave this list.</summary>
        readonly List<Rigidbody> _survivors = new List<Rigidbody>();

        /// <summary>
        /// Each survivor's original slot in the wall. Never updated after the wall is built — distance
        /// from here is the elimination test, so damage accumulates across hits.
        /// </summary>
        readonly List<Vector3> _homePositions = new List<Vector3>();

        /// <summary>Survivor indices knocked out this shot, drained a few at a time to stagger the cascade.</summary>
        readonly List<int> _pendingEliminations = new List<int>();

        Vector3 _launchDirection;
        float _launchSpeed;
        int _phaseTicksRemaining;
        int _flightTicksRemaining;
        float _eliminationCarry;

        public string FormatSlug => "surv";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by the HUD, audio and camera. Presentation observes; it never writes. ---

        /// <summary>Which beat the run is on.</summary>
        public RunPhase Phase { get; private set; } = RunPhase.Opening;

        /// <summary>
        /// Zero-based index of the shot in flight or being set up. Drives escalation, and counts misses
        /// — unlike <see cref="HitsLanded"/>, which is what the viewer is guessing.
        /// </summary>
        public int ShotIndex { get; private set; }

        /// <summary>
        /// Shots that touched at least one block — the answer to the video's question. A clean miss
        /// does not count: a shot that touched nothing is not a hit, and counting it would make the
        /// answer to "how many hits?" dishonest.
        /// </summary>
        public int HitsLanded { get; private set; }

        public int MaxShots => _config != null ? _config.maxHits : 0;

        /// <summary>Blocks still standing. The wall's score.</summary>
        public int SurvivorCount => _survivors.Count;

        /// <summary>Blocks knocked out so far. The ball's score.</summary>
        public int EliminatedTotal { get; private set; }

        /// <summary>Blocks the shot in progress has knocked out.</summary>
        public int EliminatedThisShot { get; private set; }

        /// <summary>Blocks in the wall before anything was hit.</summary>
        public int StartingBlockCount { get; private set; }

        /// <summary>True from the moment the current shot's ball is moving.</summary>
        public bool HasLaunched { get; private set; }

        /// <summary>True once the current shot's ball has touched anything — the slow-motion trigger.</summary>
        public bool HasContactThisShot { get; private set; }

        /// <summary>
        /// True once few enough blocks remain that the run is in its final stretch — the cue for extra
        /// slow-motion and a countdown on the HUD.
        /// </summary>
        public bool IsLastStand => _config != null && _survivors.Count > 0 && _survivors.Count <= _config.lastStandBlocks;

        /// <summary>
        /// True the moment every remaining block has been knocked clear during a flight — the wall is
        /// coming down on this hit. Presentation keys the reveal's slow-motion off this: it is the only
        /// point at which "this is the last hit" is actually known.
        /// </summary>
        public bool WipeoutUnderway =>
            Phase == RunPhase.HitFlight && _survivors.Count > 0 && _pendingEliminations.Count >= _survivors.Count;

        /// <summary>Ball-on-block collisions counted so far this run — what the knock audio tracks.</summary>
        public int ImpactCount { get; private set; }

        /// <summary>Relative speed of the most recent counted collision, for knock loudness and shake strength.</summary>
        public float LastImpactSpeed { get; private set; }

        /// <summary>
        /// How much of the wall is in motion right now, 0..1 — mean survivor speed against the
        /// current shot's launch speed. Read by the audio for the scatter rumble; it is a derived
        /// reading of state, never state of its own.
        /// </summary>
        public float ScatterEnergy01
        {
            get
            {
                if (_survivors.Count == 0 || _launchSpeed <= 0f)
                {
                    return 0f;
                }

                float total = 0f;
                for (int i = 0; i < _survivors.Count; i++)
                {
                    Rigidbody body = _survivors[i];
                    if (body != null && !body.isKinematic)
                    {
                        total += body.linearVelocity.magnitude;
                    }
                }

                // Against a fraction of launch speed, not the whole of it: blocks never travel
                // anywhere near as fast as the ball, so normalising against launch speed itself would
                // keep the rumble permanently near silent.
                return Mathf.Clamp01(total / _survivors.Count / (_launchSpeed * 0.25f));
            }
        }

        /// <summary>
        /// How much of the wall is gone, 0..1. Drives the camera push-in and the riser's pitch — the
        /// run's progress is the wall's destruction, since the number of hits is unknown in advance.
        /// </summary>
        public float Progress01 => StartingBlockCount <= 0 ? 0f : EliminatedTotal / (float)StartingBlockCount;

        /// <summary>True if the wall was wiped out rather than outlasting the shot cap.</summary>
        public bool WallDestroyed => IsComplete && _survivors.Count == 0;

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (WallSurvivalConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random);

            IsComplete = false;
            HasLaunched = false;
            HasContactThisShot = false;
            EliminatedTotal = 0;
            EliminatedThisShot = 0;
            HitsLanded = 0;
            ImpactCount = 0;
            LastImpactSpeed = 0f;
            ShotIndex = 0;
            _eliminationCarry = 0f;
            _pendingEliminations.Clear();

            SpawnWall();
            StartingBlockCount = _survivors.Count;

            if (_ballDetector != null)
            {
                _ballDetector.Reset();
            }

            if (_ballMaterial != null && _ball != null)
            {
                var renderer = _ball.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = _ballMaterial;
                }
            }

            // The ball stays out of the scene through the guessing window: the opening shot is the
            // wall, and the viewer should size up the wall, not the ball.
            SetBallVisible(false);

            Phase = RunPhase.Opening;
            _phaseTicksRemaining = SecondsToTicks(_config.openingHoldSeconds);

            _recorder.SetMetric("blocks_total", StartingBlockCount);
            _recorder.SetMetric("shots_cap", _config.maxHits);
        }

        int SecondsToTicks(float seconds) => Mathf.Max(0, Mathf.RoundToInt(seconds / _config.fixedTimestep));

        // --- Wall construction ------------------------------------------------------------------

        /// <summary>
        /// Builds the wall once, for the whole run. It is never laid out again: every later hit lands
        /// on whatever the previous hits left behind.
        /// </summary>
        void SpawnWall()
        {
            ClearWall();

            float spacing = _config.BlockSpacing;
            float halfWidth = (_config.columns - 1) * spacing * 0.5f;
            float halfHeight = (_config.rows - 1) * spacing * 0.5f;

            for (int row = 0; row < _config.rows; row++)
            {
                for (int col = 0; col < _config.columns; col++)
                {
                    // Jitter drawn per block in a fixed order, so a seed always produces the same wall.
                    Vector3 jitter = new Vector3(
                        _random.NextFloat(-1f, 1f),
                        _random.NextFloat(-1f, 1f),
                        _random.NextFloat(-1f, 1f)) * _config.JitterAmplitude;

                    Vector3 local = new Vector3(col * spacing - halfWidth, row * spacing - halfHeight, 0f);
                    Vector3 world = _fieldCenter.TransformPoint(local + jitter);

                    var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    block.name = $"Block_{row:D2}_{col:D2}";
                    block.transform.SetParent(_blocksParent, false);
                    block.transform.localScale = Vector3.one * _config.blockSize;
                    block.transform.SetPositionAndRotation(world, _fieldCenter.rotation);

                    if (_blockMaterial != null)
                    {
                        block.GetComponent<Renderer>().sharedMaterial = _blockMaterial;
                    }

                    var body = block.AddComponent<Rigidbody>();
                    body.mass = _config.blockMass;
                    body.useGravity = false; // blocks hold position until hit — see WallSurvivalConfig.blockLinearDamping
                    body.linearDamping = _config.blockLinearDamping;
                    body.angularDamping = _config.blockLinearDamping;
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    // Without this, resolving an overlap is itself an explosion — see
                    // WallSurvivalConfig.maxDepenetrationVelocity.
                    body.maxDepenetrationVelocity = _config.maxDepenetrationVelocity;
                    body.isKinematic = true; // dead still through the guessing window

                    _survivors.Add(body);
                    _homePositions.Add(world);
                }
            }
        }

        void ClearWall()
        {
            _survivors.Clear();
            _homePositions.Clear();

            for (int i = _blocksParent.childCount - 1; i >= 0; i--)
            {
                Destroy(_blocksParent.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// Stops every survivor where it came to rest and pins it there for the next hold. Velocity is
        /// cleared while the body is still dynamic — Unity rejects velocity writes on kinematic bodies.
        /// A wall visibly drifting under residual velocity would kill the held beat.
        /// </summary>
        void FreezeSurvivors()
        {
            for (int i = 0; i < _survivors.Count; i++)
            {
                Rigidbody body = _survivors[i];
                if (body == null)
                {
                    continue;
                }

                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.isKinematic = true;
            }
        }

        // --- Shot lifecycle ---------------------------------------------------------------------

        void BeginShot(int shotIndex)
        {
            ShotIndex = shotIndex;
            HasLaunched = false;
            HasContactThisShot = false;
            EliminatedThisShot = 0;
            _eliminationCarry = 0f;
            _pendingEliminations.Clear();

            FreezeSurvivors();
            PrepareBall(shotIndex);

            Phase = RunPhase.HitHold;
            _phaseTicksRemaining = SecondsToTicks(_config.preLaunchHoldSeconds);
        }

        /// <summary>
        /// Positions and scales this shot's ball but does not move it — the hold beat needs it
        /// visibly at rest in frame before <see cref="Launch"/> sets it going.
        /// </summary>
        void PrepareBall(int shotIndex)
        {
            float radius = _config.RadiusForHit(shotIndex);
            float speed = _random.NextFloat(_config.ballSpeedMin, _config.ballSpeedMax) *
                          _config.SpeedScaleForHit(shotIndex);

            Vector3 target = DensestClusterCentre(radius);

            // Jitter is always drawn from the seeded stream, even when it is about to be discarded,
            // so flipping unseededAim does not shift every later draw in the run.
            float seededX = _random.NextFloat(-1f, 1f);
            float seededY = _random.NextFloat(-1f, 1f);

            float jitterX = _config.unseededAim ? UnityEngine.Random.Range(-1f, 1f) : seededX;
            float jitterY = _config.unseededAim ? UnityEngine.Random.Range(-1f, 1f) : seededY;

            float jitterScale = radius * _config.aimJitterBallRadii;
            Vector3 aimPoint = target +
                               _fieldCenter.right * (jitterX * jitterScale) +
                               _fieldCenter.up * (jitterY * jitterScale);

            Vector3 spawn = aimPoint + _fieldCenter.forward * _config.ballSpawnDistance;

            _launchDirection = (aimPoint - spawn).normalized;
            _launchSpeed = speed;

            _ball.isKinematic = true; // held still through the anticipation beat
            _ball.transform.position = spawn;
            _ball.transform.localScale = Vector3.one * (radius * 2f);
            _ball.mass = _config.MassForHit(shotIndex);

            SetBallVisible(true);

            if (_ballDetector != null)
            {
                _ballDetector.ConsumeCollisionCount();
            }
        }

        /// <summary>
        /// Centre of the most crowded patch of blocks still standing, measured across the ball's own
        /// footprint. Because the wall is never rebuilt it fills with holes, and aiming at the overall
        /// centroid would increasingly send the ball through a hole it already made; aiming at the
        /// densest patch is the shot that breaks the most, and in the late game it is guaranteed to be
        /// aimed at a real block rather than empty space between scattered ones.
        /// </summary>
        /// <remarks>
        /// O(n²) over survivors — about 90,000 distance checks for a full 300-block wall, once per
        /// shot. Ties go to the lowest index, so the choice is deterministic.
        /// </remarks>
        Vector3 DensestClusterCentre(float ballRadius)
        {
            if (_survivors.Count == 0)
            {
                return _fieldCenter.position;
            }

            float reach = ballRadius + _config.BlockSpacing;
            float reachSqr = reach * reach;

            int bestIndex = 0;
            int bestCount = -1;

            for (int i = 0; i < _survivors.Count; i++)
            {
                Vector3 p = _survivors[i].position;
                int count = 0;

                for (int j = 0; j < _survivors.Count; j++)
                {
                    if ((_survivors[j].position - p).sqrMagnitude <= reachSqr)
                    {
                        count++;
                    }
                }

                if (count > bestCount)
                {
                    bestCount = count;
                    bestIndex = i;
                }
            }

            // Centre on the neighbourhood rather than the single seed block, so the shot lands in the
            // middle of the cluster instead of on its edge.
            Vector3 anchor = _survivors[bestIndex].position;
            Vector3 sum = Vector3.zero;
            int members = 0;

            for (int j = 0; j < _survivors.Count; j++)
            {
                Vector3 q = _survivors[j].position;
                if ((q - anchor).sqrMagnitude <= reachSqr)
                {
                    sum += q;
                    members++;
                }
            }

            return members > 0 ? sum / members : anchor;
        }

        void SetBallVisible(bool visible)
        {
            if (_ball != null && _ball.gameObject.activeSelf != visible)
            {
                _ball.gameObject.SetActive(visible);
            }
        }

        void Launch()
        {
            for (int i = 0; i < _survivors.Count; i++)
            {
                _survivors[i].isKinematic = false;
            }

            _ball.isKinematic = false;
            _ball.linearVelocity = Vector3.zero;
            _ball.angularVelocity = Vector3.zero;
            _ball.linearVelocity = _launchDirection * _launchSpeed;

            HasLaunched = true;
            Phase = RunPhase.HitFlight;
            _flightTicksRemaining = SecondsToTicks(_config.maxHitFlightSeconds);

            _settleTracker = new RigidbodyFieldSettleTracker(
                _survivors, _config.settleVelocityThreshold, _config.settleTicksRequired);

            _recorder.LogEvent($"shot_{ShotIndex + 1}_launch", _launchSpeed);
        }

        // --- Tick -------------------------------------------------------------------------------

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();

            switch (Phase)
            {
                case RunPhase.Opening:
                    TickCountdown(() => BeginShot(0));
                    break;

                case RunPhase.HitHold:
                    TickCountdown(Launch);
                    break;

                case RunPhase.HitFlight:
                    TickFlight();
                    break;

                case RunPhase.HitResult:
                    TickResult();
                    break;

                case RunPhase.FinalHold:
                    TickCountdown(FinishRun);
                    break;
            }
        }

        void TickCountdown(System.Action onElapsed)
        {
            if (_phaseTicksRemaining > 0)
            {
                _phaseTicksRemaining--;
                return;
            }

            onElapsed();
        }

        void TickFlight()
        {
            if (_ballDetector != null)
            {
                int newImpacts = _ballDetector.ConsumeCollisionCount();
                if (newImpacts > 0)
                {
                    ImpactCount += newImpacts;
                    LastImpactSpeed = _ballDetector.LastImpactSpeed;

                    if (!HasContactThisShot)
                    {
                        HasContactThisShot = true;
                        HitsLanded++;
                    }
                }
            }

            MarkKnockedOutBlocks();

            // Settle tracking cannot start until the shot has actually happened to the wall. The wall
            // stands motionless for the whole second the ball is in flight, so a tracker running from
            // the launch tick sees "every body below threshold" immediately and calls the hit over a
            // quarter-second in — before contact. "HasLaunched" is not the gate; "the ball is spent" is.
            if (BallIsSpent)
            {
                _settleTracker.Tick();
            }

            _flightTicksRemaining--;

            // The flight cap is a safety net, not a normal exit: a hit whose last block never quite
            // stops would otherwise stall the run and leave the video hanging on a static frame.
            if (_settleTracker.IsSettled || _flightTicksRemaining <= 0)
            {
                EnterResultPhase();
            }
        }

        /// <summary>
        /// True once the ball can no longer change the outcome: it has hit something and slowed to a
        /// crawl, or it missed entirely and flown out the back of the wall. Both cases must count — a
        /// clean miss still has to end its shot.
        /// </summary>
        bool BallIsSpent
        {
            get
            {
                if (_ball == null)
                {
                    return true;
                }

                // Distance along the field's facing: positive is the ball's own side, negative means
                // it has passed through the wall's plane and is heading away behind it.
                float depth = Vector3.Dot(_ball.position - _fieldCenter.position, _fieldCenter.forward);
                if (depth < -_config.RadiusForHit(ShotIndex) * 2f)
                {
                    return true;
                }

                return HasContactThisShot &&
                       _ball.linearVelocity.sqrMagnitude <= _launchSpeed * _launchSpeed * 0.04f;
            }
        }

        /// <summary>
        /// Flags every survivor that has been knocked clear of its original slot, or knocked crooked.
        /// Scanned in index order and queued rather than removed on the spot, so the cascade that
        /// follows is deterministic and can be paced out over several ticks.
        /// </summary>
        void MarkKnockedOutBlocks()
        {
            float threshold = _config.EliminationDistance;
            float thresholdSqr = threshold * threshold;
            float tilt = _config.eliminationTiltDegrees;
            Quaternion wallRotation = _fieldCenter.rotation;

            for (int i = 0; i < _survivors.Count; i++)
            {
                Rigidbody body = _survivors[i];
                if (body == null || _pendingEliminations.Contains(i))
                {
                    continue;
                }

                bool displaced = (body.position - _homePositions[i]).sqrMagnitude >= thresholdSqr;
                bool tilted = tilt > 0f && Quaternion.Angle(body.rotation, wallRotation) >= tilt;

                if (displaced || tilted)
                {
                    _pendingEliminations.Add(i);
                }
            }

            if (_config.minNeighboursToStand > 0)
            {
                MarkUnsupportedBlocks();
            }
        }

        /// <summary>
        /// Neighbour reach in block spacings: past the diagonal (√2 ≈ 1.41) so all 8 surrounding slots
        /// count, short of two spacings so a block across a one-block hole does not.
        /// </summary>
        const float NeighbourReachSpacings = 1.6f;

        /// <summary>Scratch flags for <see cref="MarkUnsupportedBlocks"/>, reused to avoid a per-tick allocation.</summary>
        bool[] _goneScratch = new bool[0];

        /// <summary>
        /// Flags every block left with too few standing neighbours. A block whose neighbours are all
        /// already flagged counts as unsupported too — a neighbour on its way out is holding nothing up.
        /// Repeats until nothing changes, because at thresholds above 1 clearing one block can strand
        /// the next. Uses current positions, so a neighbour counts only if it is still actually there.
        /// </summary>
        void MarkUnsupportedBlocks()
        {
            int count = _survivors.Count;
            if (_goneScratch.Length < count)
            {
                _goneScratch = new bool[count];
            }

            System.Array.Clear(_goneScratch, 0, count);
            for (int p = 0; p < _pendingEliminations.Count; p++)
            {
                _goneScratch[_pendingEliminations[p]] = true;
            }

            float reach = _config.BlockSpacing * NeighbourReachSpacings;
            float reachSqr = reach * reach;
            int needed = _config.minNeighboursToStand;

            bool changed = true;
            while (changed)
            {
                changed = false;

                for (int i = 0; i < count; i++)
                {
                    if (_goneScratch[i] || _survivors[i] == null)
                    {
                        continue;
                    }

                    Vector3 position = _survivors[i].position;
                    int neighbours = 0;

                    for (int j = 0; j < count && neighbours < needed; j++)
                    {
                        if (j != i && !_goneScratch[j] && _survivors[j] != null &&
                            (_survivors[j].position - position).sqrMagnitude <= reachSqr)
                        {
                            neighbours++;
                        }
                    }

                    if (neighbours < needed)
                    {
                        _goneScratch[i] = true;
                        _pendingEliminations.Add(i);
                        changed = true;
                    }
                }
            }
        }

        void EnterResultPhase()
        {
            MarkKnockedOutBlocks();
            _pendingEliminations.Sort(); // index order: the cascade sweeps the wall rather than popping at random

            Phase = RunPhase.HitResult;
            _phaseTicksRemaining = SecondsToTicks(_config.hitResultHoldSeconds);
            _eliminationCarry = 0f;

            SetBallVisible(false);
            if (!_ball.isKinematic)
            {
                _ball.linearVelocity = Vector3.zero;
                _ball.angularVelocity = Vector3.zero;
            }

            _ball.isKinematic = true;
        }

        void TickResult()
        {
            // The cascade: knocked-out blocks vanish a few per tick rather than all at once, so the
            // chime ladder the presenter walks turns a scoring update into the format's ASMR payoff.
            if (_pendingEliminations.Count > 0)
            {
                _eliminationCarry += _config.eliminationsPerTick;
                int toRemove = _config.eliminationsPerTick <= 0f
                    ? _pendingEliminations.Count
                    : Mathf.FloorToInt(_eliminationCarry);

                if (toRemove > 0)
                {
                    _eliminationCarry -= toRemove;
                    RemoveEliminated(toRemove);
                }

                return; // the result hold starts only once the cascade has finished
            }

            if (_phaseTicksRemaining > 0)
            {
                _phaseTicksRemaining--;
                return;
            }

            _recorder.LogEvent($"shot_{ShotIndex + 1}_result", EliminatedThisShot);
            _recorder.SetMetric($"shot_{ShotIndex + 1}_survivors", _survivors.Count);

            bool wallDown = _survivors.Count == 0;
            bool shotsExhausted = ShotIndex + 1 >= _config.maxHits;

            if (wallDown || shotsExhausted)
            {
                Phase = RunPhase.FinalHold;
                _phaseTicksRemaining = SecondsToTicks(_config.finalHoldSeconds);
                return;
            }

            BeginShot(ShotIndex + 1);
        }

        /// <summary>
        /// Removes up to <paramref name="count"/> queued blocks, highest index first so the remaining
        /// queued indices stay valid as the survivor list shrinks under them.
        /// </summary>
        void RemoveEliminated(int count)
        {
            for (int n = 0; n < count && _pendingEliminations.Count > 0; n++)
            {
                int last = _pendingEliminations.Count - 1;
                int index = _pendingEliminations[last];
                _pendingEliminations.RemoveAt(last);

                if (index < 0 || index >= _survivors.Count)
                {
                    continue;
                }

                Rigidbody body = _survivors[index];
                _survivors.RemoveAt(index);
                _homePositions.RemoveAt(index);

                if (body != null)
                {
                    Destroy(body.gameObject);
                }

                EliminatedThisShot++;
                EliminatedTotal++;
            }
        }

        void FinishRun()
        {
            SetBallVisible(false);

            _recorder.SetMetric("blocks_eliminated", EliminatedTotal);
            _recorder.SetMetric("blocks_surviving", _survivors.Count);
            _recorder.SetMetric("shots_fired", ShotIndex + 1);
            _recorder.SetMetric("hits_landed", HitsLanded);
            _recorder.SetMetric("impacts_total", ImpactCount);
            _recorder.SetMetric("final_tick", _recorder.CurrentTick);
            _recorder.LogEvent(_survivors.Count == 0 ? "wall_destroyed" : "wall_survived", HitsLanded);
            _recorder.Finish(CompletionReason.Finished);

            Phase = RunPhase.Complete;
            IsComplete = true;
        }
    }
}
