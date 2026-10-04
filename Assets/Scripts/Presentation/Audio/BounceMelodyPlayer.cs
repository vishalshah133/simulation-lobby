using System.Collections;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Plays one note per bounce: either climbing a pentatonic scale or, when a
    /// <see cref="MelodySequence"/> is assigned, stepping through a song. The retention engine for
    /// threshold formats. The rising melody is what makes a viewer stay for the resolution.
    /// </summary>
    /// <remarks>
    /// Format-agnostic: something else decides when a bounce happened and calls <see cref="PlayNote"/>.
    /// Purely presentational — it observes and makes sound, and never touches simulation state.
    /// <para>
    /// <b>Notes played must equal bounces.</b> A missing note means a missed collision, which makes
    /// this the most sensitive tunneling detector available — see the collision-safety block.
    /// <see cref="NotesPlayed"/> counts bounce-driven notes only, so the check stays exact.
    /// </para>
    /// </remarks>
    public sealed class BounceMelodyPlayer : MonoBehaviour
    {
        [Header("Song")]
        [Tooltip("Optional. Assigned: bounces step through this song, looping at the end, and the " +
                 "Scale settings are ignored. Empty: the pentatonic climb.")]
        public MelodySequence melody;

        [Tooltip("Seconds per beat when FinishPhrase plays the rest of a line on its own.")]
        [Range(0.15f, 1f)] public float finishSecondsPerBeat = 0.36f;

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

        [Tooltip("Climb the scale on each bounce. Off plays the same note every time. Scale mode only.")]
        public bool ascendWithBounces = true;

        AudioClip[] _scale;
        AudioSource[] _voices;
        int _nextVoice;
        int _noteIndex;
        bool _finishing;

        bool IsSong => melody != null && melody.Count > 0;

        /// <summary>Bounce-driven notes played so far. Compare against the format's bounce count.</summary>
        public int NotesPlayed { get; private set; }

        /// <summary>Times the song has played through to its last note. Always 0 in scale mode.</summary>
        public int SongPlaythroughs { get; private set; }

        void Awake()
        {
            _scale = IsSong ? BuildSongClips() : ProceduralToneBank.CreateScale(noteCount, rootHz, noteDuration);

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
        /// One clip per note, rather than one per pitch. The clip length follows the written note
        /// length, so a held note at the end of a line rings longer.
        /// </summary>
        AudioClip[] BuildSongClips()
        {
            var clips = new AudioClip[melody.Count];
            for (int i = 0; i < clips.Length; i++)
            {
                float duration = noteDuration * Mathf.Max(1f, melody.notes[i].beats);
                clips[i] = ProceduralToneBank.CreateTone(melody.FrequencyAt(i), duration, $"{melody.name}_{i:D2}");
            }

            return clips;
        }

        /// <summary>
        /// Play the next note. <paramref name="velocity01"/> is normalised impact strength; pass 1 if
        /// the format does not track it.
        /// </summary>
        public void PlayNote(float velocity01 = 1f)
        {
            if (_scale == null || _scale.Length == 0 || _finishing)
            {
                return;
            }

            NotesPlayed++;
            Sound(_noteIndex, velocity01);
            Advance();
        }

        /// <summary>
        /// Song mode: play out the rest of the current line at <see cref="finishSecondsPerBeat"/>,
        /// with no bounces needed. Call when the run ends, so the tune resolves on the reveal instead
        /// of stopping mid-line. Ignores bounces while it plays. No effect in scale mode.
        /// </summary>
        public void FinishPhrase()
        {
            if (!IsSong || _finishing)
            {
                return;
            }

            StartCoroutine(FinishPhraseRoutine());
        }

        IEnumerator FinishPhraseRoutine()
        {
            _finishing = true;

            // At a line boundary the next line plays in full. One line is short enough (<4s) to sit
            // under the reveal without dragging the video's ending.
            for (int guard = 0; guard < melody.Count; guard++)
            {
                MelodyNote note = melody.notes[_noteIndex];
                Sound(_noteIndex, 1f);
                Advance();

                if (note.endsPhrase)
                {
                    break;
                }

                yield return new WaitForSeconds(finishSecondsPerBeat * Mathf.Max(0.25f, note.beats));
            }

            _finishing = false;
        }

        void Sound(int index, float velocity01)
        {
            AudioClip clip = _scale[Mathf.Clamp(index, 0, _scale.Length - 1)];
            AudioSource source = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            float gain = Mathf.Lerp(1f - velocitySensitivity, 1f, Mathf.Clamp01(velocity01));
            source.volume = volume * gain;
            source.PlayOneShot(clip, gain);
        }

        void Advance()
        {
            if (IsSong)
            {
                // A song loops from the top, since restarting mid-tune would sound like a wrong note.
                _noteIndex++;
                if (_noteIndex >= _scale.Length)
                {
                    _noteIndex = 0;
                    SongPlaythroughs++;
                }

                return;
            }

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

        /// <summary>Restart the melody at the root (or the song at its first note).</summary>
        public void ResetMelody()
        {
            _noteIndex = 0;
        }
    }
}
