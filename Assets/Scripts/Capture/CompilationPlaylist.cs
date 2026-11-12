using System;
using UnityEngine;

namespace SimulationLobby.Capture
{
    /// <summary>The cover used to hide a scene cut. All are uGUI-only (no custom shader).</summary>
    public enum CompilationTransition
    {
        FadeBlack,
        WipeLeft,
        WipeRight,
        FlashWhite,
        IrisClose
    }

    /// <summary>One segment of a compilation: a scene found BY NAME (never by reference to a format).</summary>
    [Serializable]
    public sealed class CompilationEntry
    {
        [Tooltip("Scene name (file name without .unity). Must be in Build Settings.")]
        public string sceneName;

        [Tooltip("Shown under the 'n / N' banner while the cut covers the screen.")]
        public string label;

        [Tooltip("Hard cap on the segment, in real seconds from reveal. Hit = warning in the console, then cut.")]
        [Min(5f)] public float maxSeconds = 70f;

        [Tooltip("Seconds to keep playing after the segment's SimulationRunner reports complete.")]
        [Min(0f)] public float tailSeconds = 2.5f;

        [Tooltip("The cover used on the way INTO this segment.")]
        public CompilationTransition transitionIn = CompilationTransition.FadeBlack;

        [Tooltip("Seconds for EACH half of the cut (cover, then reveal). A cut costs about 2x this plus a short hold.")]
        [Min(0.1f)] public float transitionSeconds = 0.6f;
    }

    /// <summary>
    /// Data for <see cref="CompilationPlayer"/>: the ordered scenes, the transitions between them and the
    /// intro/outro cards. A published compilation = master scene + this asset (+ each scene's logged seed).
    /// </summary>
    [CreateAssetMenu(fileName = "CompilationPlaylist", menuName = "Simulation Lobby/Compilation Playlist")]
    public sealed class CompilationPlaylist : ScriptableObject
    {
        [Header("Timeline")]
        [Tooltip("Black lead-in so the Recorder can be started first. Trim this off the front of the clip.")]
        [Min(0f)] public float startDelaySeconds = 3f;

        [Tooltip("Stop Play mode after the outro (ends a Recorder 'Play mode' recording cleanly).")]
        public bool exitPlayModeWhenDone = true;

        [Tooltip("Seconds to hold black after the outro before stopping.")]
        [Min(0f)] public float endHoldSeconds = 1.5f;

        [Header("Intro card")]
        public string introTitle = "SIMULATION LOBBY";
        public string introHook = "10 SIMULATIONS. GUESS EACH ONE.";
        [Min(1f)] public float introSeconds = 5f;

        [Header("Outro card")]
        public string outroText = "WHICH ONE WAS THE MOST SATISFYING?";
        public string outroSub = "COMMENT BELOW";
        [Min(1f)] public float outroSeconds = 8f;

        [Header("Segments")]
        public CompilationEntry[] entries = new CompilationEntry[0];
    }
}
