using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A melody as data: the notes a <see cref="BounceMelodyPlayer"/> steps through, one per trigger.
    /// A "guess the song" video is a scene plus one of these, so a new song is a new asset, not code.
    /// </summary>
    /// <remarks>
    /// <b>Public-domain tunes only.</b> The synth makes every sound, so there is no recording to match,
    /// but the composition itself must be out of copyright. Record where it comes from in
    /// <see cref="source"/>, which the video plan quotes.
    /// <para>
    /// Rhythm isn't stored as timing. In bounce-driven playback the physics sets the rhythm, so
    /// <see cref="MelodyNote.beats"/> only shapes how long a note rings and paces
    /// <see cref="BounceMelodyPlayer.FinishPhrase"/>, which plays out the rest of a line once the run ends.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "Melody",
        menuName = "Simulation Lobby/Audio/Melody Sequence",
        order = 50)]
    public sealed class MelodySequence : ScriptableObject
    {
        [Tooltip("The answer, as revealed on screen. Upper case reads best at 480px wide.")]
        public string songTitle = "SONG TITLE";

        [Tooltip("Provenance, for the licensing note in the video plan.")]
        [TextArea(1, 3)] public string source;

        [Tooltip("Frequency of semitone 0 (the tonic). 220 = A3, warm on phone speakers.")]
        [Range(110f, 523f)] public float tonicHz = 220f;

        public List<MelodyNote> notes = new List<MelodyNote>();

        public int Count => notes.Count;

        public float FrequencyAt(int index)
        {
            return tonicHz * Mathf.Pow(2f, notes[index].semitone / 12f);
        }
    }

    [Serializable]
    public struct MelodyNote
    {
        [Tooltip("Semitones above the tonic. Negative goes below it.")]
        public int semitone;

        [Tooltip("Written length in beats (1 = quarter note). Shapes ring time and phrase-finish pacing.")]
        public float beats;

        [Tooltip("Last note of a line. FinishPhrase plays up to and including the next one of these.")]
        public bool endsPhrase;
    }
}
