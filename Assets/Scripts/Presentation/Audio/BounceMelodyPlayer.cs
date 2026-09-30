using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Plays one note per bounce, climbing a pentatonic scale. The retention engine for threshold
    /// formats — the rising melody is what makes a viewer stay for the resolution.
    /// </summary>
    /// <remarks>
    /// Format-agnostic: something else decides when a bounce happened and calls <see cref="PlayNote"/>.
    /// Purely presentational — it observes and makes sound, and never touches simulation state.
    /// <para>
    /// <b>Notes played must equal bounces.</b> A missing note means a missed collision, which makes
    /// this the most sensitive tunneling detector available — see the collision-safety block.
    /// </para>
    /// </remarks>
    public sealed class BounceMelodyPlayer : MonoBehaviour
    {
        [Header("Scale")]
        [Tooltip("Notes before the melody wraps back down. Also the ceiling on pitch.")]
        [Range(4, 32)] public int noteCount = 16;

        [Tooltip("Root frequency in Hz. 220 (A3) is warmer than middle C on phone speakers.")]
        [Range(110f, 523f)] public float rootHz = 220f;

        [Tooltip("Note length in seconds. Longer overlaps into a pad when bounces come fast.")]
        [Range(0.2f, 2.5f)] public float noteDuration = 1.1f;

        [Header("Playback")]
        [Tooltip("Voices. Too few and fast bounces cut each other off mid-decay.")]
        [Range(2, 16)] public int voiceCount = 8;

        [Range(0f, 1f)] public float volume = 0.55f;

        [Tooltip("How much impact speed varies note loudness. 0 = every note equally loud.")]
        [Range(0f, 1f)] public float velocitySensitivity = 0.35f;

        [Tooltip("Climb the scale on each bounce. Off plays the same note every time.")]
        public bool ascendWithBounces = true;

        AudioClip[] _scale;
        AudioSource[] _voices;
        int _nextVoice;
        int _noteIndex;

        void Awake()
        {
            _scale = ProceduralToneBank.CreateScale(noteCount, rootHz, noteDuration);

            // Round-robin voices rather than one source: a single AudioSource would cut the previous
            // note the instant the next bounce lands, which is exactly when bounces get interesting.
            _voices = new AudioSource[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;   // 2D: the arena is the whole frame, panning adds nothing
                source.volume = volume;
                _voices[i] = source;
            }
        }

        /// <summary>
        /// Play the next note. <paramref name="velocity01"/> is normalised impact strength; pass 1 if
        /// the format does not track it.
        /// </summary>
        public void PlayNote(float velocity01 = 1f)
        {
            if (_scale == null || _scale.Length == 0)
            {
                return;
            }

            AudioClip clip = _scale[Mathf.Clamp(_noteIndex, 0, _scale.Length - 1)];
            AudioSource source = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            float gain = Mathf.Lerp(1f - velocitySensitivity, 1f, Mathf.Clamp01(velocity01));
            source.volume = volume * gain;
            source.PlayOneShot(clip, gain);

            if (ascendWithBounces)
            {
                _noteIndex++;
                if (_noteIndex >= _scale.Length)
                {
                    // Wrap to a third of the way up rather than back to the root — restarting from the
                    // bottom reads as "the music gave up" just as the tension should be peaking.
                    _noteIndex = _scale.Length / 3;
                }
            }
        }

        /// <summary>Restart the melody at the root. Call when a new round begins.</summary>
        public void ResetMelody()
        {
            _noteIndex = 0;
        }
    }
}
