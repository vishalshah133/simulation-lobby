using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// The colour sorter: a splitter feeds <c>sorterLanes</c> parallel single-file lanes, each metered by a paddle-wheel
    /// pocket. One scanner gantry spans the lanes (a ring per lane flashes), and every lane has a flap gate over every
    /// tube. Unmatched balls roll off the lane ends (recycle). Built by <c>SorterBuilder</c>.
    /// Lane maths run in the lane frame: x across (lane centres in <see cref="laneCentresX"/>), y up, z (u) along the lanes.
    /// Flap arrays are indexed <c>lane * gateCount + tube</c>; <c>gateHingeU</c> and <c>dropPoints</c> are per tube.
    /// </summary>
    public sealed class SorterStation : MachineStation, ISorter
    {
        [Header("Lanes (built)")]
        public Transform laneFrame;
        public float laneWidth = 0.17f;
        public float laneLength = 12.3f;
        public float scanU = 1.15f;
        public float[] laneCentresX = { 0f };

        [Header("Singulator (built)")]
        public Rigidbody singulator;
        public Vector3 singulatorRestPos;

        [Header("Flap gates (built, lane * gateCount + tube)")]
        public Rigidbody[] flapBodies;
        public Vector3[] flapRestPos;
        public Quaternion[] flapRestRot;
        public Vector3[] flapHinge;
        public float[] gateHingeU;
        public float flapLength = 0.3f;
        public float flapOpenDegrees = 70f;
        public float flapOpenTime = 0.06f;

        [Tooltip("Point each ball falls from, centred over its tube. One per colour, shared by all lanes.")]
        public Transform[] dropPoints;

        [Header("Scanner (built)")]
        public Renderer[] scannerRings;
        public Color ringIdleColour = new Color(0.2f, 0.2f, 0.22f);

        [SerializeField] Color[] colours;

        public event Action<int, Vector3> Scanned;

        // Run state.
        float _spinDegPerSec, _openHold, _phase;
        int _lanes = 1, _gates;
        Matrix4x4 _worldToLane;
        Bounds _laneBounds;
        float _halfAcross;
        bool[] _scanned = new bool[0];
        float[] _open, _lastMatch, _applied;
        bool[] _target;
        float[] _bu = new float[64], _bpu = new float[64];
        int[] _bc = new int[64], _bl = new int[64];
        int _bn;
        float _firstGateU;

        // Presentation (written in FixedUpdate, read in Update).
        int[] _scanCount, _seenScan;
        Color[] _laneColour;
        float[] _flash;
        MaterialPropertyBlock _block;
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            _spinDegPerSec = config.singulatorDegreesPerSecond;
            _openHold = config.sorterFlapOpenSeconds;
            _phase = rng.Branch(0x50A7).NextFloat(0f, 360f);
            colours = new Color[config.ColourCount];
            for (int i = 0; i < colours.Length; i++) colours[i] = config.colours[i].color;

            _lanes = laneCentresX != null && laneCentresX.Length > 0 ? laneCentresX.Length : 1;
            _gates = gateHingeU != null ? gateHingeU.Length : 0;
            int flaps = flapBodies != null ? flapBodies.Length : 0;
            _open = new float[flaps];
            _applied = new float[flaps];
            _lastMatch = new float[flaps];
            _target = new bool[flaps];
            for (int i = 0; i < flaps; i++)
            {
                _lastMatch[i] = -999f;
                _applied[i] = -1f;
            }

            _scanCount = new int[_lanes];
            _seenScan = new int[_lanes];
            _flash = new float[_lanes];
            _laneColour = new Color[_lanes];
            for (int i = 0; i < _lanes; i++) _laneColour[i] = Color.white;

            _firstGateU = _gates > 0 ? gateHingeU[0] : 0f;
            _scanned = new bool[Mathf.Max(64, config.ballCount)];
            _filtered = new bool[Mathf.Max(64, config.ballCount)];
            _flapColliders = null;

            _halfAcross = 0.05f;
            for (int i = 0; i < _lanes; i++) _halfAcross = Mathf.Max(_halfAcross, Mathf.Abs(laneCentresX[i]) + laneWidth * 0.5f + 0.05f);

            if (laneFrame != null)
            {
                _worldToLane = laneFrame.worldToLocalMatrix;
                float hw = _halfAcross + 0.3f;
                var corners = new Vector3[8];
                int k = 0;
                for (int ix = 0; ix < 2; ix++)
                    for (int iy = 0; iy < 2; iy++)
                        for (int iz = 0; iz < 2; iz++)
                            corners[k++] = laneFrame.TransformPoint(new Vector3(ix == 0 ? -hw : hw, iy == 0 ? -0.5f : 1.2f,
                                iz == 0 ? -0.2f : laneLength + 0.2f));
                _laneBounds = new Bounds(corners[0], Vector3.zero);
                for (int i = 1; i < 8; i++) _laneBounds.Encapsulate(corners[i]);
            }
        }

        Collider[] _flapColliders;
        bool[] _filtered = new bool[0];

        public override void Step(float simTime, float dt)
        {
            if (singulator != null)
            {
                float angle = -(_spinDegPerSec * simTime + _phase);
                singulator.MovePosition(transform.TransformPoint(singulatorRestPos));
                singulator.MoveRotation(transform.rotation * Quaternion.Euler(angle, 0f, 0f));
            }

            // Nothing else moves: the gates are fixed plates and the sorting is done by collision filtering (see StepSort).
        }

        public void StepSort(float simTime, IReadOnlyList<MachineBall> balls)
        {
            if (laneFrame == null || flapBodies == null || _gates == 0) return;
            int count = balls.Count;
            float halfLane = laneWidth * 0.5f + 0.06f;
            _bn = 0;

            for (int k = 0; k < count; k++)
            {
                MachineBall b = balls[k];
                if (b == null) continue;
                int id = b.id;
                if (id >= _scanned.Length) Array.Resize(ref _scanned, id * 2 + 16);
                Vector3 p = b.body != null ? b.body.position : b.transform.position;

                bool inside = false;
                Vector3 l = default;
                int lane = -1;
                if (_laneBounds.Contains(p))
                {
                    l = _worldToLane.MultiplyPoint3x4(p);
                    if (l.y > -0.08f && l.y < 0.6f && l.z > 0f && l.z < laneLength && Mathf.Abs(l.x) < _halfAcross)
                    {
                        for (int i = 0; i < _lanes; i++)
                        {
                            if (Mathf.Abs(l.x - laneCentresX[i]) < halfLane)
                            {
                                lane = i;
                                break;
                            }
                        }

                        inside = lane >= 0;
                    }
                }

                if (!inside)
                {
                    _scanned[id] = false;
                    continue;
                }

                if (!_scanned[id] && l.z >= scanU)
                {
                    _scanned[id] = true;
                    if (b.colour >= 0 && b.colour < colours.Length) _laneColour[lane] = colours[b.colour];
                    _scanCount[lane]++;
                    Scanned?.Invoke(b.colour, p);
                }

                // Sorting by collider: the gate plates under the lane never move. A ball ignores the plates of its own colour (in every
                // lane), so it drops through its tube's window; every other ball meets a solid plate and just rolls on to its own.
                // Set once, the first time the ball is in a lane; the filter stays for good, so a recycled ball is still sorted.
                if (id >= _filtered.Length) Array.Resize(ref _filtered, id * 2 + 16);
                if (!_filtered[id])
                {
                    _filtered[id] = true;
                    FilterBall(b);
                }
            }
        }

        void FilterBall(MachineBall b)
        {
            if (b.colour < 0 || b.colour >= _gates) return;
            Collider ball = b.body != null ? b.body.GetComponent<Collider>() : b.GetComponent<Collider>();
            if (ball == null) return;
            if (_flapColliders == null)
            {
                _flapColliders = new Collider[flapBodies.Length];
                for (int i = 0; i < flapBodies.Length; i++) _flapColliders[i] = flapBodies[i] != null ? flapBodies[i].GetComponent<Collider>() : null;
            }

            for (int lane = 0; lane < _lanes; lane++)
            {
                Collider plate = _flapColliders[lane * _gates + b.colour];
                if (plate != null) Physics.IgnoreCollision(ball, plate, true);
            }
        }

        // Visual only: flash each lane's scanner ring in its last scanned ball's colour.
        void Update()
        {
            if (scannerRings == null || _scanCount == null) return;
            for (int i = 0; i < scannerRings.Length && i < _lanes; i++)
            {
                if (scannerRings[i] == null) continue;
                if (_seenScan[i] != _scanCount[i])
                {
                    _seenScan[i] = _scanCount[i];
                    _flash[i] = 1f;
                }
                else if (_flash[i] <= 0f)
                {
                    continue;
                }
                else
                {
                    _flash[i] = Mathf.Max(0f, _flash[i] - Time.deltaTime * 6f);
                }

                if (_block == null) _block = new MaterialPropertyBlock();
                _block.SetColor(BaseColorId, Color.Lerp(ringIdleColour, _laneColour[i], _flash[i]));
                _block.SetColor(EmissionColorId, _laneColour[i] * (_flash[i] * 4f));
                scannerRings[i].SetPropertyBlock(_block);
            }
        }
    }
}
