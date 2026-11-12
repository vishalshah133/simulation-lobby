using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Dramatic slow motion that starts only AFTER a scare's loud hit: the sting lands at full speed, then (0.28 s or more later, once the high-volume
    /// audio has peaked) time drops, holds on the aftermath (a rider pinned on spikes, a cobra lifting its prey, a beast thrashing on the lens),
    /// and eases back. The scale is a pure function of simulated time since the sting began, so a re-render is identical. This writes only
    /// <see cref="Time.timeScale"/> (a rendering global: the simulation steps in fixed ticks and its result is unchanged, it just takes longer
    /// to watch). Restores the scale on disable so it never leaks into the editor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScareSlowMo : MonoBehaviour
    {
        /// <summary>The scale currently applied (1 = normal). Audio reads this to drop pitch.</summary>
        public static float Current { get; private set; } = 1f;

        struct Window
        {
            public float start;   // sim seconds after the sting began (always after the loud hit)
            public float easeIn;
            public float hold;
            public float easeOut;
            public float scale;
        }

        [SerializeField] bool enableSlowMo = true;
        [Tooltip("Multiplies how deep every slow-motion goes (1 = as authored, 0 = off).")]
        [SerializeField, Range(0f, 1f)] float intensity = 1f;

        HorrorCoasterHost _host;

        static bool TryWindow(ScareKind k, out Window w)
        {
            switch (k)
            {
                // the spike wall gets the longest, deepest hold: the rider pinned, then the second volley stopping short of your face
                case ScareKind.SpikeWall: w = new Window { start = 0.30f, easeIn = 0.10f, hold = 1.55f, easeOut = 0.45f, scale = 0.10f }; return true;
                case ScareKind.SnakeStrike: w = new Window { start = 0.34f, easeIn = 0.10f, hold = 1.1f, easeOut = 0.4f, scale = 0.16f }; return true;
                case ScareKind.SprinterLeap: w = new Window { start = 0.34f, easeIn = 0.08f, hold = 0.55f, easeOut = 0.3f, scale = 0.16f }; return true;
                case ScareKind.SideLeap: w = new Window { start = 0.30f, easeIn = 0.08f, hold = 0.5f, easeOut = 0.3f, scale = 0.2f }; return true;
                case ScareKind.BatFlood: w = new Window { start = 0.36f, easeIn = 0.12f, hold = 1.2f, easeOut = 0.5f, scale = 0.22f }; return true;
                case ScareKind.DoorGhoul: w = new Window { start = 0.34f, easeIn = 0.08f, hold = 0.6f, easeOut = 0.3f, scale = 0.25f }; return true;
                case ScareKind.CrowBurst: w = new Window { start = 0.34f, easeIn = 0.08f, hold = 0.7f, easeOut = 0.35f, scale = 0.25f }; return true;
                case ScareKind.PaintingLunge: w = new Window { start = 0.32f, easeIn = 0.08f, hold = 0.55f, easeOut = 0.3f, scale = 0.25f }; return true;
                case ScareKind.SkullFinale: w = new Window { start = 0.45f, easeIn = 0.12f, hold = 1.0f, easeOut = 0.5f, scale = 0.2f }; return true;
                // the rest: every window starts >= 0.28 s after the sting frame, i.e. after the loud hit has peaked
                case ScareKind.GraveHand: w = new Window { start = 0.30f, easeIn = 0.08f, hold = 0.7f, easeOut = 0.35f, scale = 0.2f }; return true;
                case ScareKind.GraveyardGhost: w = new Window { start = 0.32f, easeIn = 0.10f, hold = 0.8f, easeOut = 0.4f, scale = 0.2f }; return true;
                case ScareKind.TunnelFace: w = new Window { start = 0.30f, easeIn = 0.08f, hold = 0.7f, easeOut = 0.35f, scale = 0.2f }; return true;
                case ScareKind.AsylumFigure: w = new Window { start = 0.32f, easeIn = 0.10f, hold = 0.9f, easeOut = 0.4f, scale = 0.18f }; return true;
                // micro scares: a short, shallower beat after the hit
                case ScareKind.SpiderDrop:
                case ScareKind.WindowFace:
                case ScareKind.SkeletonPop:
                case ScareKind.ClawHand:
                case ScareKind.BatSwarm:
                case ScareKind.CoffinLid:
                case ScareKind.ShadowRush:
                case ScareKind.MirrorFace: w = new Window { start = 0.28f, easeIn = 0.07f, hold = 0.38f, easeOut = 0.3f, scale = 0.35f }; return true;
                default: w = default; return false;
            }
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            Current = 1f;
        }

        void LateUpdate()
        {
            if (_host == null)
            {
                _host = FindAnyObjectByType<HorrorCoasterHost>();
            }

            float target = 1f;
            if (enableSlowMo && _host != null && _host.Ready && _host.State != null)
            {
                ICoasterState s = _host.State;
                if ((s.Phase == ScarePhase.Sting || s.Phase == ScarePhase.Aftermath) && TryWindow(s.CurrentEvent.kind, out Window w))
                {
                    float t = s.Phase == ScarePhase.Sting ? s.PhaseTime : 0.6f + s.PhaseTime;
                    float a = Mathf.Clamp01((t - w.start) / Mathf.Max(0.01f, w.easeIn));
                    float b = Mathf.Clamp01((t - (w.start + w.easeIn + w.hold)) / Mathf.Max(0.01f, w.easeOut));
                    float depth = Smooth(a) * (1f - Smooth(b));
                    target = Mathf.Lerp(1f, Mathf.Lerp(1f, w.scale, intensity), depth);
                }
            }

            Current = target;
            Time.timeScale = target;
        }

        static float Smooth(float x)
        {
            return x * x * (3f - 2f * x);
        }
    }
}
