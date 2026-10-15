using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Race
{
    /// <summary>Where the race is in its arc. Presentation reads this to pick the camera and the HUD beat.</summary>
    public enum RacePhase
    {
        /// <summary>Marbles resting against the closed gate: the setup beat.</summary>
        Hold = 0,

        /// <summary>Gate open, marbles running.</summary>
        Racing = 1,

        /// <summary>Run over.</summary>
        Complete = 2
    }

    /// <summary>One marble in the race. Read by presentation; only <see cref="RaceSimulation"/> writes it.</summary>
    public sealed class RaceEntry
    {
        public ContenderIdentity identity;
        public Transform transform;
        public Rigidbody body;
        public CollisionDetector3D detector;

        /// <summary>Metres along the track centreline.</summary>
        public float progress;

        /// <summary>0 is the leader.</summary>
        public int rank;

        public bool finished;

        /// <summary>Seconds after the gate opened, interpolated to the sub-tick the line was crossed.</summary>
        public float finishTime;

        /// <summary>1 for the winner. 0 until finished.</summary>
        public int finishPlace;

        public int respawns;

        internal int pathIndex;
        internal float stalledSeconds;
        internal float previousFinishSide;
    }

    /// <summary>
    /// Marble race: a seeded field of marbles released from a gate down a course, first across the line wins.
    /// Everything about the field that varies by seed (which marble starts where, the jitter in the grid)
    /// comes from the run's <see cref="SeededRandom"/>; the track itself is fixed.
    /// </summary>
    /// <remarks>
    /// Marbles are cloned from an inactive template in the scene, coloured from the config roster, so a new
    /// roster is a config change with no scene rebuild. Two safety nets (off-track recovery and a push for a
    /// stalled marble) exist so a run can never hang with a marble lost or stuck. Both are logged as events.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class RaceSimulation : MonoBehaviour, ISimulation
    {
        [Header("Scene wiring")]
        [Tooltip("Inactive marble with Rigidbody, SphereCollider (radius 0.5), CollisionDetector3D, ContenderIdentity, renderer and trail.")]
        [SerializeField] GameObject _marbleTemplate;
        [SerializeField] Transform _marbleParent;

        [Tooltip("Sits at the closed gate. Forward points down the slope, up is the track surface normal.")]
        [SerializeField] Transform _startOrigin;
        [SerializeField] GameObject _gate;

        [Tooltip("Forward is the direction of travel across the line.")]
        [SerializeField] Transform _finishLine;
        [SerializeField] TrackPath _path;

        [Tooltip("Parent of the obstacles. Any KinematicSpinner below it is driven by the tick.")]
        [SerializeField] Transform _obstacleRoot;

        readonly List<KinematicSpinner> _spinners = new List<KinematicSpinner>();

        RaceConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;

        readonly List<RaceEntry> _entries = new List<RaceEntry>();
        static readonly Comparison<RaceEntry> RankOrder = CompareForRank;

        int _holdTicksRemaining;
        int _racingTicks;
        int _firstFinishTick = -1;
        int _finishedCount;
        int _finishIndex;
        float _finishDistance;
        float _fixedDelta;
        RaceEntry _leader;
        int _leadChanges;

        public Transform StartOrigin => _startOrigin;
        public Transform FinishLine => _finishLine;

        /// <summary>Scene wiring, called by the scene builder.</summary>
        public void Bind(GameObject marbleTemplate, Transform marbleParent, Transform startOrigin, GameObject gate,
            Transform finishLine, TrackPath path, Transform obstacleRoot = null)
        {
            _obstacleRoot = obstacleRoot;
            _marbleTemplate = marbleTemplate;
            _marbleParent = marbleParent;
            _startOrigin = startOrigin;
            _gate = gate;
            _finishLine = finishLine;
            _path = path;
        }

        public string FormatSlug => "race";
        public bool IsComplete { get; private set; }
        public RunResult Result => _recorder?.Result;

        public RacePhase Phase { get; private set; } = RacePhase.Hold;

        /// <summary>Every marble, in roster order. Sorted view: <see cref="RaceEntry.rank"/>.</summary>
        public IReadOnlyList<RaceEntry> Entries => _entries;

        /// <summary>Whoever is currently first. Null before <c>Initialize</c>.</summary>
        public RaceEntry Leader => _leader;

        public int FinishedCount => _finishedCount;
        public int LeadChanges => _leadChanges;

        /// <summary>Seconds since the gate opened.</summary>
        public float ElapsedSeconds => _racingTicks * _fixedDelta;

        /// <summary>Seconds until the gate opens. Zero once released.</summary>
        public float SecondsUntilRelease => Phase == RacePhase.Hold ? _holdTicksRemaining * _fixedDelta : 0f;

        public float TrackLength => _path != null ? _path.TotalLength : 0f;

        /// <summary>The gate has opened.</summary>
        public event Action Released;

        /// <summary>A marble crossed the line; the int is its place (1 = winner).</summary>
        public event Action<RaceEntry, int> MarbleFinished;

        /// <summary>The front of the field changed hands.</summary>
        public event Action<RaceEntry> LeaderChanged;

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (RaceConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random);
            _fixedDelta = config.fixedTimestep;

            IsComplete = false;
            Phase = RacePhase.Hold;
            _racingTicks = 0;
            _firstFinishTick = -1;
            _finishedCount = 0;
            _leadChanges = 0;
            _leader = null;

            DestroySpawnedMarbles();
            _entries.Clear();

            if (_gate != null)
            {
                _gate.SetActive(true);
            }

            _finishIndex = _path.Nearest(_finishLine.position, _path.Count - 1, _path.Count, 0, out _);
            _finishDistance = _path.DistanceAt(_finishIndex);

            CollectSpinners();
            SpawnField();
            UpdateProgressAndRanks();
            _leader = _sorted.Count > 0 ? _sorted[0] : null;

            _holdTicksRemaining = Mathf.CeilToInt(_config.preReleaseHoldSeconds / _fixedDelta);
            _recorder.SetMetric("contenders", _entries.Count);
            _recorder.SetMetric("track_length_m", _path.TotalLength);
            _recorder.LogEvent("field_set", _entries.Count);
        }

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();
            StepSpinners();

            switch (Phase)
            {
                case RacePhase.Hold:
                    if (_holdTicksRemaining > 0)
                    {
                        _holdTicksRemaining--;
                    }
                    else
                    {
                        Release();
                    }

                    break;

                case RacePhase.Racing:
                    TickRacing(fixedDelta);
                    break;
            }
        }

        // ------------------------------------------------------------------ setup

        void CollectSpinners()
        {
            _spinners.Clear();
            if (_obstacleRoot == null)
            {
                return;
            }

            // Hierarchy order is stable, so a spinner's seeded phase is the same every time this seed runs.
            _obstacleRoot.GetComponentsInChildren(true, _spinners);
            foreach (KinematicSpinner spinner in _spinners)
            {
                spinner.SetPhase(_random.NextFloat(0f, 360f));
            }
        }

        void StepSpinners()
        {
            // Spinners turn from tick zero, through the setup hold, so the course looks alive in the reveal.
            float seconds = _recorder.CurrentTick * _fixedDelta;
            for (int i = 0; i < _spinners.Count; i++)
            {
                _spinners[i].Step(seconds);
            }
        }

        void DestroySpawnedMarbles()
        {
            for (int i = _marbleParent.childCount - 1; i >= 0; i--)
            {
                Destroy(_marbleParent.GetChild(i).gameObject);
            }
        }

        void SpawnField()
        {
            int count = _config.roster.Length;
            float radius = _config.marbleRadius;
            int columns = Mathf.Max(1, _config.gridColumns);
            float columnSpacing = radius * 2f * 1.4f;

            // Which marble gets which grid slot is the seed's first decision.
            var slotOrder = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                slotOrder.Add(i);
            }

            _random.Shuffle(slotOrder);

            Vector3 origin = _startOrigin.position;
            Vector3 forward = _startOrigin.forward;
            Vector3 right = _startOrigin.right;
            Vector3 up = _startOrigin.up;

            for (int i = 0; i < count; i++)
            {
                int slot = slotOrder[i];
                int row = slot / columns;
                int column = slot % columns;
                int inRow = Mathf.Min(columns, count - row * columns);
                float lateral = (column - (inRow - 1) * 0.5f) * columnSpacing;

                float jitterRange = radius * _config.gridJitter;
                float jitterX = _random.NextFloat(-jitterRange, jitterRange);
                float jitterZ = _random.NextFloat(-jitterRange, jitterRange);

                Vector3 position = origin
                    - forward * (radius + 0.04f + row * _config.gridRowSpacing + jitterZ)
                    + right * (lateral + jitterX)
                    + up * (radius + 0.02f);

                RaceContender contender = _config.roster[i];
                _entries.Add(SpawnMarble(i, contender, position));
            }
        }

        RaceEntry SpawnMarble(int index, RaceContender contender, Vector3 position)
        {
            GameObject marble = Instantiate(_marbleTemplate, position, Quaternion.identity, _marbleParent);
            marble.name = $"Marble {contender.name}";
            marble.transform.localScale = Vector3.one * (_config.marbleRadius * 2f);
            marble.SetActive(true);

            var body = marble.GetComponent<Rigidbody>();
            body.mass = _config.marbleMass;
            body.linearDamping = _config.linearDamping;
            body.angularDamping = _config.angularDamping;
            body.maxDepenetrationVelocity = _config.maxDepenetrationVelocity;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.position = position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            var identity = marble.GetComponent<ContenderIdentity>();
            identity.Set(index, contender.name, contender.color);

            var renderer = marble.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", contender.color);
                block.SetColor("_Color", contender.color);
                renderer.SetPropertyBlock(block);
            }

            var trail = marble.GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.startColor = new Color(contender.color.r, contender.color.g, contender.color.b, 0.9f);
                trail.endColor = new Color(contender.color.r, contender.color.g, contender.color.b, 0f);
                trail.Clear();
            }

            var detector = marble.GetComponent<CollisionDetector3D>();
            if (detector != null)
            {
                detector.Reset();
            }

            var entry = new RaceEntry
            {
                identity = identity,
                transform = marble.transform,
                body = body,
                detector = detector
            };

            entry.pathIndex = _path.Nearest(position, 0, 0, 300, out _);
            entry.previousFinishSide = SideOfFinish(position);
            return entry;
        }

        void Release()
        {
            Phase = RacePhase.Racing;
            if (_gate != null)
            {
                _gate.SetActive(false);
            }

            _recorder.LogEvent("release", 0f);
            Released?.Invoke();
        }

        // ------------------------------------------------------------------ racing

        void TickRacing(float fixedDelta)
        {
            _racingTicks++;
            UpdateProgressAndRanks();

            for (int i = 0; i < _entries.Count; i++)
            {
                RaceEntry entry = _entries[i];
                if (entry.finished)
                {
                    continue;
                }

                CheckFinish(entry);
                if (!entry.finished)
                {
                    GuardOffTrack(entry);
                    GuardStalled(entry, fixedDelta);
                }
            }

            // Ranks again: a finish this tick changes who is where.
            SortRanks();
            TrackLeader();

            bool everyoneIn = _finishedCount >= _entries.Count;
            bool graceOver = _firstFinishTick >= 0 &&
                             (_racingTicks - _firstFinishTick) * fixedDelta >= _config.finishGraceSeconds;
            if (everyoneIn || graceOver)
            {
                Complete();
            }
        }

        void UpdateProgressAndRanks()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                RaceEntry entry = _entries[i];
                if (entry.finished)
                {
                    continue;
                }

                Vector3 position = entry.transform.position;
                entry.pathIndex = _path.Nearest(position, entry.pathIndex, 6, 60, out _);
                entry.progress = _path.ProgressAt(position, entry.pathIndex);
            }

            SortRanks();
        }

        float SideOfFinish(Vector3 position)
        {
            return Vector3.Dot(position - _finishLine.position, _finishLine.forward);
        }

        void CheckFinish(RaceEntry entry)
        {
            float side = SideOfFinish(entry.transform.position);
            float previous = entry.previousFinishSide;
            entry.previousFinishSide = side;

            // Only a marble on the last stretch can finish: the line is a plane, and other parts of the
            // course can pass through it.
            if (previous < 0f && side >= 0f && entry.progress > _finishDistance - 4f)
            {
                float fraction = Mathf.Approximately(previous, side) ? 1f : previous / (previous - side);
                entry.finished = true;
                entry.finishTime = (_racingTicks - 1 + Mathf.Clamp01(fraction)) * _fixedDelta;
                entry.finishPlace = ++_finishedCount;
                if (_firstFinishTick < 0)
                {
                    _firstFinishTick = _racingTicks;
                }

                _recorder.LogEvent($"finish_{entry.identity.DisplayName}", entry.finishTime);
                MarbleFinished?.Invoke(entry, entry.finishPlace);
            }
        }

        void GuardOffTrack(RaceEntry entry)
        {
            _path.Nearest(entry.transform.position, entry.pathIndex, 6, 60, out float sqrDistance);
            float limit = _config.offTrackDistance;
            if (sqrDistance <= limit * limit)
            {
                return;
            }

            // Put it back on the centreline a little upstream, moving gently the right way.
            int index = Mathf.Max(0, entry.pathIndex - 8);
            Vector3 point = _path.PointAt(index) + Vector3.up * (_config.marbleRadius + 0.05f);
            entry.body.position = point;
            entry.transform.position = point;
            entry.body.linearVelocity = _path.TangentAt(index) * 1.5f;
            entry.body.angularVelocity = Vector3.zero;
            entry.pathIndex = index;
            entry.respawns++;
            ClearTrail(entry);
            _recorder.LogEvent($"respawn_{entry.identity.DisplayName}", entry.progress);
        }

        void GuardStalled(RaceEntry entry, float fixedDelta)
        {
            if (entry.body.linearVelocity.sqrMagnitude > _config.stalledSpeed * _config.stalledSpeed)
            {
                entry.stalledSeconds = 0f;
                return;
            }

            entry.stalledSeconds += fixedDelta;
            if (entry.stalledSeconds >= _config.stalledSeconds)
            {
                entry.body.linearVelocity = _path.TangentAt(entry.pathIndex) * _config.stalledNudgeSpeed;
                entry.stalledSeconds = 0f;
                _recorder.LogEvent($"nudge_{entry.identity.DisplayName}", entry.progress);
            }
        }

        static void ClearTrail(RaceEntry entry)
        {
            var trail = entry.transform.GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.Clear();
            }
        }

        // ------------------------------------------------------------------ ranking

        static int CompareForRank(RaceEntry a, RaceEntry b)
        {
            if (a.finished != b.finished)
            {
                return a.finished ? -1 : 1;
            }

            if (a.finished)
            {
                return a.finishPlace.CompareTo(b.finishPlace);
            }

            int byProgress = b.progress.CompareTo(a.progress);
            // Equal progress falls back to roster order, so the sort is a total order and stays deterministic.
            return byProgress != 0 ? byProgress : a.identity.Index.CompareTo(b.identity.Index);
        }

        readonly List<RaceEntry> _sorted = new List<RaceEntry>();

        void SortRanks()
        {
            _sorted.Clear();
            _sorted.AddRange(_entries);
            _sorted.Sort(RankOrder);
            for (int i = 0; i < _sorted.Count; i++)
            {
                _sorted[i].rank = i;
            }
        }

        void TrackLeader()
        {
            RaceEntry front = _sorted.Count > 0 ? _sorted[0] : null;
            if (front == _leader)
            {
                return;
            }

            bool change = _leader != null && _racingTicks * _fixedDelta > 3f;
            _leader = front;
            if (change)
            {
                _leadChanges++;
                _recorder.LogEvent("lead_change", _leadChanges);
                LeaderChanged?.Invoke(front);
            }
        }

        // ------------------------------------------------------------------ finish

        void Complete()
        {
            SortRanks();
            var names = new List<string>(_sorted.Count);
            foreach (RaceEntry entry in _sorted)
            {
                names.Add(entry.identity.DisplayName);
            }

            _recorder.SetStandings(names);

            float winnerTime = 0f;
            float margin = 0f;
            int respawns = 0;
            foreach (RaceEntry entry in _entries)
            {
                respawns += entry.respawns;
            }

            if (_sorted.Count > 0 && _sorted[0].finished)
            {
                winnerTime = _sorted[0].finishTime;
                if (_sorted.Count > 1 && _sorted[1].finished)
                {
                    margin = _sorted[1].finishTime - winnerTime;
                }
            }

            _recorder.SetMetric("winner_time_s", winnerTime);
            _recorder.SetMetric("winning_margin_s", margin);
            _recorder.SetMetric("lead_changes", _leadChanges);
            _recorder.SetMetric("finished", _finishedCount);
            _recorder.SetMetric("respawns", respawns);
            _recorder.LogEvent("race_called", _finishedCount);
            _recorder.Finish(CompletionReason.Finished);

            Phase = RacePhase.Complete;
            IsComplete = true;
        }
    }
}
