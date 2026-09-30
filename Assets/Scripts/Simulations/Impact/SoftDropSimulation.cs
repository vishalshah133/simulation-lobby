using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Impact
{
    /// <summary>
    /// <c>impact</c>, soft-drop variant: an upright capsule released above a lacquer plinth with a
    /// brass-lipped hole in it, narrower than the capsule. How it lands — glass rests in the cup,
    /// softer takes squeeze deeper in, 100% slumps into it — depends only on
    /// <see cref="SoftDropConfig.softness"/>. Meant to be driven by a <see cref="SweepRunner"/>,
    /// which re-initialises it once per take.
    /// </summary>
    /// <remarks>
    /// No PhysX anywhere: the body is a <see cref="SoftBodySolver"/> and the plinth and floor are
    /// analytic colliders built from the same config numbers the scene builder uses for the meshes. Nothing here is random, so the seed is recorded but unused — the takes are the
    /// variation, and they differ visibly in their config assets rather than invisibly in a seed.
    /// <para>
    /// Everything a presenter needs is exposed as read-only properties; nothing presentational
    /// happens here.
    /// </para>
    /// </remarks>
    public sealed class SoftDropSimulation : MonoBehaviour, ISimulation, ISoftBodySource
    {
        SoftDropConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;
        int _ticksSinceImpact;

        public string FormatSlug => "impact";

        public bool IsComplete => false; // a take ends on the sweep's clock, not on a condition

        public RunResult Result
        {
            get
            {
                if (_recorder == null)
                {
                    return null;
                }

                if (Solver != null)
                {
                    _recorder.SetMetric("impacts", ImpactCount);
                    _recorder.SetMetric("volume_ratio", Solver.VolumeRatio);
                    _recorder.SetMetric("final_height", Solver.Centroid.y);
                    _recorder.SetMetric("state_hash", Solver.StateHash());
                }

                return _recorder.Result;
            }
        }

        public SoftBodySolver Solver { get; private set; }

        public SoftDropConfig Config => _config;

        /// <summary>Distinct impacts so far this take (a landing, a second bounce, hitting the floor).</summary>
        public int ImpactCount { get; private set; }

        /// <summary>Approach speed of the most recent impact, m/s.</summary>
        public float LastImpactSpeed { get; private set; }

        public bool HasContacted => ImpactCount > 0;

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = config as SoftDropConfig;
            if (_config == null)
            {
                throw new System.ArgumentException($"SoftDropSimulation needs a SoftDropConfig, got {config?.GetType().Name ?? "null"}.");
            }

            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, _config, _random);
            ImpactCount = 0;
            LastImpactSpeed = 0f;
            _ticksSinceImpact = int.MaxValue / 2;

            SoftBodyShape shape = SoftBodyShapes.Capsule(_config.capsuleRadius, _config.capsuleHalfLength, _config.capsuleSegments);
            Quaternion tilt = Quaternion.Euler(0f, 0f, _config.dropTiltDegrees);
            Solver = new SoftBodySolver(shape, _config.DropCenter, tilt, _config.BuildParameters());

            var colliders = new System.Collections.Generic.List<SdfCollider> { new SdfPlane(Vector3.zero, Vector3.up) };
            SdfCollider plinth = new SdfBox(new Vector3(0f, _config.plinthSize.y * 0.5f, 0f), _config.plinthSize * 0.5f);
            if (_config.holeRadius > 0f)
            {
                // The hole is carved out of the block; the brass lip is its own rounded collider so the
                // skin rolls over a curve instead of catching on a sharp rim.
                float top = _config.PlinthTopY;
                plinth = new SdfSubtract(plinth,
                    new SdfCylinderY(Vector3.zero, _config.holeRadius, top - _config.holeDepth, top + 10f));
                colliders.Add(new SdfTorusY(new Vector3(0f, top, 0f), _config.holeRadius + _config.lipRadius,
                    _config.lipRadius));
            }

            colliders.Add(plinth);
            foreach (SdfCollider collider in colliders)
            {
                collider.staticFriction = _config.staticFriction;
                collider.dynamicFriction = _config.dynamicFriction;
                Solver.AddCollider(collider);
            }

            _recorder.LogEvent("released", _config.softness);
        }

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();
            Solver.Step(fixedDelta);
            _ticksSinceImpact++;

            int cooldownTicks = Mathf.RoundToInt(_config.impactCooldownSeconds / fixedDelta);
            if (Solver.NewContacts > 0 && Solver.PeakImpactSpeed >= _config.impactMinSpeed && _ticksSinceImpact >= cooldownTicks)
            {
                ImpactCount++;
                LastImpactSpeed = Solver.PeakImpactSpeed;
                _ticksSinceImpact = 0;
                _recorder.LogEvent("impact", LastImpactSpeed);
            }
        }
    }
}
