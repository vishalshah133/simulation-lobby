using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Where one scheduled event currently is, as seen by a view. Pure read of <see cref="ICoasterState"/>.</summary>
    public struct EventClock
    {
        public bool valid;
        public ScarePhase phase;       // Idle = not started, Aftermath = stung (also once the director moved on)
        public float t;                // seconds since that phase began (999 once settled in the past)
        public float elapsed;          // seconds since PreQuiet began (0 before, 999 after the sting settled)
        public ScareEvent ev;

        public bool Started => valid && phase != ScarePhase.Idle;
        public bool Stinging => valid && phase == ScarePhase.Sting;
        public bool Stung => valid && (phase == ScarePhase.Sting || phase == ScarePhase.Aftermath);
    }

    /// <summary>One group of renderers sharing a cloned material whose colours cycle orange / purple / toxic green.</summary>
    [Serializable]
    public class HueGroup
    {
        public Renderer[] renderers;
        public int slot;
        public float phase;
        public float speed = 0.08f;     // palette steps per second (0.08 = one key colour every 12 s, far below 3 Hz)
        public float emission = 1.1f;   // HDR multiplier on the emission colour (kept moderate: flash safety)
        public float baseMul = 0.3f;
        public float whiten;
        public float alpha = 1f;

        [NonSerialized] Material _mat;

        public void Bind()
        {
            Release();
            if (renderers == null || renderers.Length == 0 || renderers[0] == null) return;
            Material[] src = renderers[0].sharedMaterials;
            if (slot < 0 || slot >= src.Length || src[slot] == null) return;
            _mat = new Material(src[slot]);
            _mat.EnableKeyword("_EMISSION");
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Material[] arr = r.sharedMaterials;
                if (slot >= arr.Length) continue;
                arr[slot] = _mat;
                r.sharedMaterials = arr;
            }
        }

        public void Apply(float time)
        {
            if (_mat == null) return;
            Color c = ActSetKit.CycleColor(time * speed + phase);
            if (whiten > 0f) c = Color.Lerp(c, Color.white, whiten);
            _mat.SetColor("_BaseColor", new Color(c.r * baseMul, c.g * baseMul, c.b * baseMul, alpha));
            _mat.SetColor("_EmissionColor", new Color(c.r * emission, c.g * emission, c.b * emission, 1f));
        }

        public void Release()
        {
            if (_mat != null) UnityEngine.Object.Destroy(_mat);
            _mat = null;
        }
    }

    /// <summary>Small deterministic helpers shared by the act sets. No UnityEngine.Random anywhere.</summary>
    public static class ActSetKit
    {
        public static float Hash01(int a, int salt = 0)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + salt * 668265263 + 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        public static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        public static float Remap01(float v, float a, float b)
        {
            return Mathf.Approximately(a, b) ? (v >= b ? 1f : 0f) : Mathf.Clamp01((v - a) / (b - a));
        }

        /// <summary>0..1 fade for an act spanning [start,end] (rail metres), ramping over ~leadSeconds of travel either side.</summary>
        public static float ActWeight(ICoasterState s, float start, float end, float leadSeconds = 3f)
        {
            float lead = Mathf.Max(8f, leadSeconds * Mathf.Max(6f, s.Speed));
            float d = s.Distance;
            return Smooth01((d - (start - lead)) / lead) * Smooth01((end + lead - d) / lead);
        }

        static readonly Color Orange = new Color(1f, 0.48f, 0.09f);
        static readonly Color Purple = new Color(0.6f, 0.24f, 1f);
        static readonly Color Green = new Color(0.49f, 1f, 0.31f);

        /// <summary>Slow palette walk: orange to purple to toxic green and round. phase in key-steps.</summary>
        public static Color CycleColor(float phase)
        {
            float p = Mathf.Repeat(phase, 3f);
            int k = Mathf.FloorToInt(p);
            float f = Smooth01(p - k);
            Color a = k == 0 ? Orange : (k == 1 ? Purple : Green);
            Color b = k == 0 ? Purple : (k == 1 ? Green : Orange);
            return Color.Lerp(a, b, f);
        }

        public static void SetActive(GameObject g, bool active)
        {
            if (g != null && g.activeSelf != active) g.SetActive(active);
        }

        public static Vector3 EyePos(ICoasterState s)
        {
            Camera c = Camera.main;
            return c != null ? c.transform.position : s.CartPosition + s.CartUp * 1.3f;
        }

        public static Quaternion EyeRot(ICoasterState s)
        {
            Camera c = Camera.main;
            return c != null ? c.transform.rotation : Quaternion.LookRotation(s.CartForward, s.CartUp);
        }

        /// <summary>Finds the schedule index for a kind: the hint if it matches, else the first of that kind (-1 if none).</summary>
        public static int FindEvent(ICoasterState s, ScareKind kind, int hint)
        {
            if (s == null || s.Events == null) return -1;
            if (hint >= 0 && hint < s.Events.Count && s.Events[hint].kind == kind) return hint;
            for (int i = 0; i < s.Events.Count; i++) if (s.Events[i].kind == kind) return i;
            return -1;
        }

        // Sting length per event, learned from the sting phase itself (the contract does not expose cfg.stingSeconds).
        static readonly Dictionary<int, float> StingLen = new Dictionary<int, float>();

        public static EventClock Clock(ICoasterState s, int idx)
        {
            var c = new EventClock();
            if (s == null || s.Events == null || idx < 0 || idx >= s.Events.Count) return c;
            c.valid = true;
            c.ev = s.Events[idx];
            bool cur = s.Phase != ScarePhase.Idle && s.CurrentEventIndex == idx;
            if (cur)
            {
                c.phase = s.Phase;
                c.t = s.PhaseTime;
                float pre = c.ev.preQuietSeconds;
                if (s.Phase == ScarePhase.Sting)
                {
                    StingLen.TryGetValue(idx, out float known);
                    if (s.PhaseTime > known) StingLen[idx] = s.PhaseTime;
                }
                StingLen.TryGetValue(idx, out float sl);
                c.elapsed = s.Phase == ScarePhase.PreQuiet ? s.PhaseTime
                          : s.Phase == ScarePhase.Sting ? pre + s.PhaseTime
                          : pre + (sl > 0.05f ? sl : 0.6f) + s.PhaseTime;
            }
            else if (s.NextEventIndex > idx)
            {
                c.phase = ScarePhase.Aftermath;
                c.t = 999f;
                c.elapsed = 999f;
            }
            else
            {
                c.phase = ScarePhase.Idle;
            }
            return c;
        }

        /// <summary>Pose a 0..1 "grow in" over seconds, for scare entities that appear in PreQuiet.</summary>
        public static float AppearScale(EventClock c, float seconds = 0.35f)
        {
            if (!c.Started) return 0f;
            return Smooth01(c.elapsed / Mathf.Max(0.05f, seconds));
        }

        /// <summary>Cheap smooth pseudo-noise in about -1..1, slow (fMax Hz or below).</summary>
        public static float Wobble(float t, int seed, float hz)
        {
            float a = Hash01(seed, 1) * 6.2831853f;
            float b = Hash01(seed, 2) * 6.2831853f;
            return 0.6f * Mathf.Sin(t * hz * 6.2831853f + a) + 0.4f * Mathf.Sin(t * hz * 0.53f * 6.2831853f + b);
        }

        // ---------------------------------------------------------------- jump-scare helpers

        public static Vector3 CartRight(ICoasterState s)
        {
            Vector3 r = Vector3.Cross(s.CartUp, s.CartForward);
            return r.sqrMagnitude < 1e-6f ? Vector3.right : r.normalized;
        }

        /// <summary>A point in the cart's frame relative to the real camera: metres ahead, to the LEFT, and up. Stable under the camera's flinch.</summary>
        public static Vector3 LensPoint(ICoasterState s, float ahead, float left, float up)
        {
            return EyePos(s) + s.CartForward * ahead - CartRight(s) * left + s.CartUp * up;
        }

        /// <summary>A point on the rail frame at arc length d: left (m, negative = right) and up (m) from the rail.</summary>
        public static Vector3 RailPoint(ICoasterState s, float d, float left, float up)
        {
            s.Rail.Sample(Mathf.Clamp(d, 0f, s.Rail.Length), out Vector3 p, out Vector3 f, out Vector3 u);
            Vector3 r = Vector3.Cross(u, f);
            r = r.sqrMagnitude < 1e-6f ? Vector3.right : r.normalized;
            return p - r * left + u * up;
        }

        /// <summary>Fast ease-out (cubic), 0..1.</summary>
        public static float EaseOut(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - (1f - x) * (1f - x) * (1f - x);
        }

        /// <summary>Per-entity ownership of <see cref="CoasterLookTarget"/>, so one entity never clears another's target.</summary>
        public static void LookAt(ref bool owned, Vector3 point, float weight)
        {
            CoasterLookTarget.Set(point, weight);
            owned = true;
        }

        public static void LookRelease(ref bool owned)
        {
            if (owned) CoasterLookTarget.Clear();
            owned = false;
        }
    }
}
