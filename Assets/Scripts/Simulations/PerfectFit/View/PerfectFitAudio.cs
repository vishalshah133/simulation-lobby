using System.Collections.Generic;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.PerfectFit.View
{
    /// <summary>
    /// The sound of <c>fit</c>, all procedural: a quiet bed, a riser that peaks as the shape meets the wall, a tiny
    /// click when it locks onto the spin, and a glass note per round (the next step of a rising scale) once it is
    /// through. Presentation only: it is told what happened and never looks at the simulation.
    /// </summary>
    public sealed class PerfectFitAudio : MonoBehaviour
    {
        [SerializeField] AudioSource _bed;
        [SerializeField] AudioSource _effects;

        [Range(0f, 1f)] public float bedVolume = 0.16f;
        [Range(0f, 1f)] public float riserVolume = 0.55f;
        [Range(0f, 1f)] public float lockVolume = 0.5f;
        [Range(0f, 1f)] public float noteVolume = 0.8f;

        AudioClip[] _scale;
        AudioClip _lock;
        readonly Dictionary<int, AudioClip> _risers = new Dictionary<int, AudioClip>();
        readonly Dictionary<int, AudioClip> _clinks = new Dictionary<int, AudioClip>();

        public void Bind(AudioSource bed, AudioSource effects)
        {
            _bed = bed;
            _effects = effects;
        }

        /// <summary>Starts the bed and builds the clips that don't depend on a round.</summary>
        public void Begin(int roundCount)
        {
            _scale = ProceduralToneBank.CreateScale(Mathf.Max(1, roundCount), 261.63f, 1.6f);
            _lock = ProceduralToneBank.CreateClick(1.5f, 21, "FitLock");

            if (_bed != null)
            {
                _bed.clip = ProceduralToneBank.CreateAmbientBed(4f, 55f, "FitBed");
                _bed.loop = true;
                _bed.volume = bedVolume;
                _bed.Play();
            }
        }

        /// <summary>The riser, timed to peak at contact. Rises in pitch a little every round.</summary>
        public void PlayRiser(int round, float seconds)
        {
            if (_effects == null)
            {
                return;
            }

            if (!_risers.TryGetValue(round, out AudioClip clip))
            {
                clip = ProceduralToneBank.CreateRiser(Mathf.Max(0.3f, seconds), 90f + round * 12f, 230f + round * 30f,
                                                      100 + round, "FitRiser" + round);
                _risers[round] = clip;
            }

            _effects.PlayOneShot(clip, riserVolume);
        }

        public void PlayLock()
        {
            if (_effects != null && _lock != null)
            {
                _effects.PlayOneShot(_lock, lockVolume);
            }
        }

        /// <summary>The payoff: this round's note of the scale with a glass clink over it.</summary>
        public void PlayThrough(int round)
        {
            if (_effects == null || _scale == null)
            {
                return;
            }

            _effects.PlayOneShot(_scale[Mathf.Clamp(round, 0, _scale.Length - 1)], noteVolume);
            if (!_clinks.TryGetValue(round, out AudioClip clink))
            {
                clink = ProceduralToneBank.CreateGlassClink(1f + round * 0.06f, 1.4f, 30 + round, "FitClink" + round);
                _clinks[round] = clink;
            }

            _effects.PlayOneShot(clink, noteVolume * 0.5f);
        }
    }
}
