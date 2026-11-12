using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Read-only helpers shared by the set views. Nothing here writes simulation state.</summary>
    public static class SetUtil
    {
        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>Indices into the schedule of every event of one kind (schedule order).</summary>
        public static List<int> EventsOf(ICoasterState s, ScareKind kind)
        {
            var list = new List<int>();
            for (int i = 0; i < s.Events.Count; i++)
            {
                if (s.Events[i].kind == kind)
                {
                    list.Add(i);
                }
            }

            return list;
        }

        /// <summary>True while the director is working on event <paramref name="index"/>; gives its phase and the seconds in it.</summary>
        public static bool InEvent(ICoasterState s, int index, out ScarePhase phase, out float phaseTime)
        {
            phase = s.Phase;
            phaseTime = s.PhaseTime;
            return s.Phase != ScarePhase.Idle && s.CurrentEventIndex == index;
        }

        /// <summary>True once event <paramref name="index"/> has stung (the cart has passed it).</summary>
        public static bool HasStung(ICoasterState s, int index)
        {
            if (index < s.NextEventIndex)
            {
                return true;
            }

            return s.CurrentEventIndex == index && s.Phase != ScarePhase.Idle && s.Phase != ScarePhase.PreQuiet;
        }

        public static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        public static void SetActive(Component c, bool active)
        {
            if (c != null)
            {
                SetActive(c.gameObject, active);
            }
        }

        /// <summary>Rail frame at distance d: position, forward, up and right.</summary>
        public static void Frame(ICoasterRail rail, float d, out Vector3 pos, out Vector3 fwd, out Vector3 up, out Vector3 right)
        {
            rail.Sample(d, out pos, out fwd, out up);
            right = Vector3.Cross(up, fwd).normalized;
        }

        /// <summary>Hash noise in 0..1, stable per integer pair (for flicker and scatter without UnityEngine.Random).</summary>
        public static float Hash(int a, int b = 0)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Smooth flicker 0..1 from sim time (candle-like). Deterministic.</summary>
        public static float Flicker(float time, int seed, float speed = 9f)
        {
            float t = time * speed + seed * 13.37f;
            return 0.55f + 0.25f * Mathf.Sin(t) + 0.12f * Mathf.Sin(t * 2.31f + 1.3f) + 0.08f * Mathf.Sin(t * 5.1f);
        }

        public static Quaternion Look(Vector3 forward, Vector3 up)
        {
            return forward.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(forward, up);
        }
    }
}
