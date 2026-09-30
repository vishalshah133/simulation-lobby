using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Impact
{
    /// <summary>
    /// Turns a soft-drop sweep into the video: the caption per take, the bell note that climbs one
    /// step per take, the whoosh and the hush before contact, the clink-to-squelch on impact, and
    /// the creak and wobble that follow the body afterwards.
    /// </summary>
    /// <remarks>
    /// Lives in the format so <see cref="CaptionHud"/> and <see cref="MaterialContactAudio"/> stay
    /// ignorant of capsules and spikes. Reads the runner and simulation in <c>LateUpdate</c>, after the
    /// fixed step, and writes to neither.
    /// </remarks>
    public sealed class SoftDropPresenter : MonoBehaviour
    {
        [SerializeField] SweepRunner _runner;
        [SerializeField] SoftDropSimulation _simulation;
        [SerializeField] MaterialContactAudio _audio;
        [SerializeField] CaptionHud _hud;

        [Header("Copy")]
        [Tooltip("Hook under the first caption only. It states the question the rest of the video answers.")]
        public string hookLine = "What happens at 100%?";

        [Tooltip("Small line in the bottom panel for the whole video.")]
        public string footer = "SOUND ON";

        [Header("Music")]
        [Tooltip("Pentatonic degree of the bell note per take. Rising, landing on the octave (5) at the " +
                 "last take, so the sweep resolves musically and the loop back to take 1 reads as 'again'.")]
        public int[] noteDegrees = { 0, 1, 2, 3, 5 };

        [Header("Continuous voices")]
        [Tooltip("Rate of change of surface strain (per second) that plays the creak at full volume.")]
        [Min(0.001f)] public float fullCreakStrainRate = 1.5f;

        [Tooltip("Surface strain that puts the creak at its highest pitch. The 100% splat peaks around 0.45.")]
        [Min(0.001f)] public float fullCreakStrain = 0.4f;

        [Tooltip("RMS jiggle speed (m/s) that plays the wobble at full volume.")]
        [Min(0.001f)] public float fullWobbleSpeed = 0.5f;

        int _lastTake = -1;
        int _lastImpacts;
        int _lastTick;
        float _lastStrain;
        bool _whooshPlayed;

        void LateUpdate()
        {
            if (_runner == null || _simulation == null || _runner.sweep == null)
            {
                return;
            }

            SweepRunner.SweepPhase phase = _runner.Phase;
            if (phase == SweepRunner.SweepPhase.Idle)
            {
                return;
            }

            if (_runner.TakeIndex != _lastTake)
            {
                OnTakeStarted(_runner.TakeIndex);
            }

            SoftDropConfig config = _simulation.Config;
            if (config == null)
            {
                return;
            }

            if (phase == SweepRunner.SweepPhase.Running && !_whooshPlayed)
            {
                _whooshPlayed = true;
                if (_audio != null)
                {
                    _audio.PlayWhoosh(config.FallSeconds + 0.06f);
                    _audio.SetDucked(true);
                }
            }

            int impacts = _simulation.ImpactCount;
            if (impacts > _lastImpacts && _audio != null)
            {
                for (int i = _lastImpacts; i < impacts; i++)
                {
                    _audio.PlayContact(_simulation.LastImpactSpeed, config.softness);
                }
            }

            _lastImpacts = impacts;
            TickContinuousVoices(phase, config);
        }

        void OnTakeStarted(int take)
        {
            _lastTake = take;
            _lastImpacts = 0;
            _whooshPlayed = false;
            _lastTick = _runner.TotalTicks;
            _lastStrain = 0f;

            if (_hud != null)
            {
                _hud.SetCaption(_runner.sweep.Caption(take));
                _hud.SetSubline(take == 0 ? hookLine : string.Empty);
                _hud.SetFooter(footer);
            }

            if (_audio != null)
            {
                _audio.Silence();
                _audio.SetDucked(false);
                if (noteDegrees != null && noteDegrees.Length > 0)
                {
                    _audio.PlayNote(noteDegrees[Mathf.Min(take, noteDegrees.Length - 1)]);
                }
            }
        }

        /// <summary>
        /// Creak follows how fast the skin is stretching (not how stretched it is — a jelly resting
        /// draped is silent); wobble follows how much it is jiggling. Both scale with softness: glass
        /// doesn't creak, and a rigid body's tumbling is not a wobble.
        /// </summary>
        void TickContinuousVoices(SweepRunner.SweepPhase phase, SoftDropConfig config)
        {
            if (_audio == null)
            {
                return;
            }

            SoftBodySolver solver = _simulation.Solver;
            if (phase != SweepRunner.SweepPhase.Running || solver == null)
            {
                _audio.SetStretch(0f, 0f);
                _audio.SetWobble(0f);
                return;
            }

            // Strain only changes on fixed ticks; measure its rate across ticks so a render frame that
            // fell between two ticks doesn't read as "stopped stretching". The baseline is tracked
            // from release, so the first reading after contact is a real rate, not a jump from zero.
            int ticks = _runner.TotalTicks - _lastTick;
            if (ticks <= 0)
            {
                return;
            }

            float strain = solver.SurfaceStrain;
            float rate = Mathf.Abs(strain - _lastStrain) / (ticks * _runner.sweep.FixedTimestep);
            _lastStrain = strain;
            _lastTick = _runner.TotalTicks;

            if (!_simulation.HasContacted)
            {
                _audio.SetStretch(0f, 0f);
                _audio.SetWobble(0f);
                return;
            }

            float softWeight = Mathf.Pow(Mathf.Clamp01(config.softness), 0.7f);
            _audio.SetStretch(rate / fullCreakStrainRate * softWeight, strain / fullCreakStrain);
            _audio.SetWobble(Mathf.Sqrt(solver.WobbleEnergy) / fullWobbleSpeed * softWeight);
        }

        /// <summary>Assigned by the scene builder, so nothing needs hand-wiring in the Inspector.</summary>
        public void Bind(SweepRunner runner, SoftDropSimulation simulation, MaterialContactAudio audio, CaptionHud hud)
        {
            _runner = runner;
            _simulation = simulation;
            _audio = audio;
            _hud = hud;
        }
    }
}
