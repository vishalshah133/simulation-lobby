using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Timer
{
    /// <summary>
    /// Thin driver for <see cref="TimerCameraCore"/>: gathers the marbles at the end of the frame, steps the core and applies
    /// its pose to the camera. Reads the simulation; never writes it. Runs in <c>LateUpdate</c>. The run's seed (from the
    /// <see cref="SimulationRunner"/> on the simulation's GameObject) feeds every random camera choice.
    /// </summary>
    public sealed class TimerCameraDirector : MonoBehaviour
    {
        [SerializeField] TimerSimulation sim;
        [SerializeField] Camera cam;
        [SerializeField] TimerCameraSettings settings = new TimerCameraSettings();

        sealed class StateMarbles : ITimerMarbles
        {
            readonly ITimerState _state;

            public StateMarbles(ITimerState state)
            {
                _state = state;
            }

            public int Count => _state.TotalMarbles;

            public bool OnTrack(int i) => _state.MarbleOnTrack(i);

            public Vector3 Position(int i) => _state.MarblePosition(i);
        }

        TimerCameraCore _core;
        CourseRail _rail;
        StateMarbles _marbles;
        SimulationRunner _runner;
        float _finishDistance;
        float _lastElapsed;
        bool _lastReleased;

        public TimerCameraSettings Settings => settings;

        public TimerShotKind CurrentShot => _core != null ? _core.Kind : TimerShotKind.Setup;

        public void Bind(TimerSimulation simulation, Camera camera)
        {
            sim = simulation;
            cam = camera;
            _core = null;
            _runner = null;
        }

        void LateUpdate()
        {
            if (sim == null || cam == null || !EnsureCore())
            {
                return;
            }

            ITimerState state = sim;
            if (state.Elapsed + 0.5f < _lastElapsed || (_lastReleased && !state.Released))
            {
                _core.Reset();
            }

            _lastElapsed = state.Elapsed;
            _lastReleased = state.Released;

            // The runner draws or applies its seed during Awake/start; once released it can no longer change.
            if (!state.Released)
            {
                if (_runner == null)
                {
                    _runner = sim.GetComponent<SimulationRunner>();
                }

                _core.SetSeed(_runner != null ? _runner.seed : 0);
            }

            Vector3 collector = sim.FinishLine != null ? sim.FinishLine.position : _rail.PositionAt(_finishDistance);
            Vector3 hopper = sim.HopperRelease != null ? sim.HopperRelease.position : _rail.PositionAt(0f);
            var observation = new TimerCameraObservation
            {
                dt = Time.deltaTime,
                elapsed = state.Elapsed,
                phase = state.Phase,
                released = state.Released,
                remaining = state.Remaining,
                setupRemaining = state.SetupRemaining,
                marbles = _marbles,
                hopper = hopper,
                collector = collector,
                aspect = cam.aspect,
                verticalFovDeg = cam.fieldOfView
            };

            _core.Step(observation);
            cam.transform.SetPositionAndRotation(_core.Position, _core.Rotation);
        }

        bool EnsureCore()
        {
            if (_core != null)
            {
                return true;
            }

            TrackPath path = sim.Path;
            if (path == null || path.Count < 2 || path.TotalLength < 1f || sim.TotalMarbles <= 0)
            {
                return false;
            }

            float spacing = settings.railSpacing;
            int samples = Mathf.Max(2, Mathf.FloorToInt(path.TotalLength / spacing) + 1);
            var raw = new Vector3[samples];
            for (int i = 0; i < samples; i++)
            {
                raw[i] = path.PositionAtDistance(i * spacing);
            }

            _rail = CourseRail.FromSamples(raw, spacing, settings.railSmoothRadius, settings.railSmoothPasses);
            float runOut = sim.CourseLayout != null ? sim.CourseLayout.runOutLength : 0f;
            _finishDistance = path.TotalLength - runOut;

            float start = sim.CourseLayout != null ? sim.CourseLayout.gateDistance : 0f;
            if (sim.HopperRelease != null)
            {
                int hint = 0;
                start = _rail.Project(sim.HopperRelease.position, ref hint, _rail.Count);
            }

            List<TimerFeature> features = TimerCourseFeatures.Find(sim.CourseLayout, path.TotalLength);
            _marbles = new StateMarbles(sim);
            _core = new TimerCameraCore(settings, _rail, start, _finishDistance, features);
            return true;
        }
    }
}
