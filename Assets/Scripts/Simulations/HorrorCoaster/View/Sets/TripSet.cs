using System;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    public enum TripStationKind { Ring, FaceRing, Candy, Cluster, Tube }

    /// <summary>One pocket of the Trip decoration anchored at a rail distance. Culled by distance; animated by <see cref="TripSet"/>.</summary>
    public class TripStation : MonoBehaviour
    {
        public TripStationKind kind;
        public float dist;
        public int index;
        public float side = 1f;
        public Transform[] kids;
        public int flapCount;   // Cluster: the first flapCount kids are bats

        [NonSerialized] public Vector3[] kidScale;
        [NonSerialized] public Vector3[] kidPos;
        [NonSerialized] public Quaternion baseRot;
        [NonSerialized] public Vector3 basePos;
        [NonSerialized] public bool captured;

        public void Capture()
        {
            if (captured) return;
            captured = true;
            baseRot = transform.rotation;
            basePos = transform.position;
            int n = kids != null ? kids.Length : 0;
            kidScale = new Vector3[n];
            kidPos = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                kidScale[i] = kids[i].localScale;
                kidPos[i] = kids[i].localPosition;
            }
        }
    }

    /// <summary>
    /// The midpoint Trip (about 50 s, no scares): glowing rings, a kaleidoscope tunnel of jack-o'-lantern faces, orbiting candy,
    /// fractal bat / ghost clusters, an inverted sky and a pulsing starfield. All motion is a pure function of State.SimTime and
    /// State.Distance. Everything is slow (palette 0.08 steps/s, pulses under 1.5 Hz): flash safe, no full-frame bright changes.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class TripSet : ActSetBase
    {
        public override ActId Act => ActId.Trip;

        public float startDist, endDist;
        public GameObject content;
        public TripStation[] stations;
        public HueGroup[] groups;
        public Transform[] stars;
        public float starBox = 70f;
        public float cullRange = 240f;
        public Transform skyRoot;   // inverted ground overhead (fixed in the world)
        public Transform moonRoot;  // pale moon where the ground should be
        public Light fill;          // soft hue-cycling light that travels with the cart so rails and candy are lit

        Vector3[] _starBase;
        Vector3 _skyScale, _moonScale;
        bool _ready;

        protected override void OnBound()
        {
            if (groups != null) foreach (HueGroup g in groups) g.Bind();
            if (stations != null)
                foreach (TripStation s in stations)
                {
                    if (s == null) continue;
                    s.Capture();
                    s.gameObject.SetActive(false);
                }
            if (stars != null)
            {
                _starBase = new Vector3[stars.Length];
                for (int i = 0; i < stars.Length; i++)
                {
                    _starBase[i] = new Vector3(
                        (ActSetKit.Hash01(i, 11) * 2f - 1f) * starBox,
                        (ActSetKit.Hash01(i, 12) * 2f - 1f) * starBox,
                        (ActSetKit.Hash01(i, 13) * 2f - 1f) * starBox);
                }
            }
            if (skyRoot != null) _skyScale = skyRoot.localScale;
            if (moonRoot != null) _moonScale = moonRoot.localScale;
            _ready = true;
            ActSetKit.SetActive(content, false);
        }

        void OnDestroy()
        {
            if (groups != null) foreach (HueGroup g in groups) g.Release();
        }

        void LateUpdate()
        {
            if (!_ready || State == null) return;
            float w = ActSetKit.ActWeight(State, startDist, endDist, 3f);
            bool on = w > 0.001f;
            ActSetKit.SetActive(content, on);
            if (!on) return;

            float t = State.SimTime;
            float d = State.Distance;

            if (groups != null) foreach (HueGroup g in groups) g.Apply(t);

            if (stations != null)
                foreach (TripStation s in stations)
                {
                    if (s == null) continue;
                    float dd = Mathf.Abs(s.dist - d);
                    bool near = dd < cullRange;
                    ActSetKit.SetActive(s.gameObject, near);
                    if (!near) continue;
                    float p = 1f - ActSetKit.Smooth01((dd - (cullRange - 80f)) / 80f);
                    Animate(s, t, w * p);
                }

            UpdateStars(t, w);

            if (fill != null)
            {
                fill.transform.position = State.CartPosition + State.CartUp * 2.5f;
                fill.color = ActSetKit.CycleColor(t * 0.08f + 0.7f);
                fill.intensity = 2.2f * w;
            }

            float sky = ActSetKit.Smooth01((d - startDist) / Mathf.Max(30f, (endDist - startDist) * 0.15f));
            if (skyRoot != null) skyRoot.localScale = _skyScale * (w * sky);
            if (moonRoot != null) moonRoot.localScale = _moonScale * (w * sky);
        }

        void Animate(TripStation s, float t, float k)
        {
            Transform[] kids = s.kids;
            if (kids == null) return;
            switch (s.kind)
            {
                case TripStationKind.Ring:
                {
                    float pulse = 1f + 0.07f * Mathf.Sin(t * 1.3f - s.index * 0.55f);
                    for (int i = 0; i < kids.Length; i++)
                    {
                        kids[i].localScale = s.kidScale[i] * (pulse * k);
                        float dir = (i % 2 == 0) ? 1f : -1f;
                        kids[i].localRotation = Quaternion.Euler(0f, 0f, dir * (t * (12f + 5f * i) + s.index * 23f));
                    }
                    break;
                }
                case TripStationKind.FaceRing:
                {
                    float dir = (s.index % 2 == 0) ? 1f : -1f;
                    s.transform.rotation = s.baseRot * Quaternion.Euler(0f, 0f, dir * t * 30f);
                    for (int i = 0; i < kids.Length; i++)
                        kids[i].localScale = s.kidScale[i] * (k * (1f + 0.05f * Mathf.Sin(t * 1.7f + i)));
                    break;
                }
                case TripStationKind.Tube:
                {
                    // fluorescent tube cage: slow spin, a gentle thickness swell as the "buzz" (well under 1.5 Hz, no on/off flicker)
                    float dir = (s.index % 2 == 0) ? 1f : -1f;
                    s.transform.rotation = s.baseRot * Quaternion.Euler(0f, 0f, dir * (t * 18f + s.index * 31f));
                    for (int i = 0; i < kids.Length; i++)
                    {
                        float swell = 1f + 0.15f * Mathf.Sin(t * 1.1f + i * 1.05f + s.index * 0.7f);
                        Vector3 sc = s.kidScale[i];
                        kids[i].localScale = new Vector3(sc.x * swell * k, sc.y * k, sc.z * swell * k);
                    }
                    break;
                }
                case TripStationKind.Candy:
                {
                    for (int i = 0; i < kids.Length; i++)
                    {
                        float h = ActSetKit.Hash01(s.index * 8 + i, 5);
                        float h2 = ActSetKit.Hash01(s.index * 8 + i, 6);
                        Vector3 p0 = s.kidPos[i];
                        float r = new Vector2(p0.x, p0.y).magnitude;
                        float a0 = Mathf.Atan2(p0.y, p0.x);
                        float omega = ((h < 0.5f) ? -1f : 1f) * (0.5f + h) * 1.4f;
                        float a = a0 + omega * t;
                        kids[i].localPosition = new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), p0.z + Mathf.Sin(t * 0.7f + h * 6.28f) * 1.2f);
                        kids[i].localRotation = Quaternion.Euler(t * (30f + 40f * h), t * (25f + 50f * h2), t * 20f);
                        kids[i].localScale = s.kidScale[i] * k;
                    }
                    break;
                }
                case TripStationKind.Cluster:
                {
                    s.transform.rotation = s.baseRot * Quaternion.Euler(0f, 0f, t * 5f * s.side);
                    s.transform.position = s.basePos + Vector3.up * (Mathf.Sin(t * 0.5f + s.index) * 0.8f);
                    for (int i = 0; i < kids.Length; i++)
                    {
                        Vector3 sc = s.kidScale[i] * k;
                        if (i < s.flapCount)
                        {
                            float flap = 0.5f + 0.5f * Mathf.Sin(t * 6.28f * 1.2f + ActSetKit.Hash01(s.index * 64 + i, 9) * 6.28f);
                            sc.x *= 1f - 0.35f * flap;
                        }
                        kids[i].localScale = sc;
                    }
                    break;
                }
            }
        }

        void UpdateStars(float t, float w)
        {
            if (stars == null || _starBase == null) return;
            Vector3 c = State.CartPosition;
            float box = starBox;
            for (int i = 0; i < stars.Length; i++)
            {
                Transform st = stars[i];
                if (st == null) continue;
                Vector3 v = _starBase[i] - c;
                v.x -= 2f * box * Mathf.Floor((v.x + box) / (2f * box));
                v.y -= 2f * box * Mathf.Floor((v.y + box) / (2f * box));
                v.z -= 2f * box * Mathf.Floor((v.z + box) / (2f * box));
                float edge = Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z))) / box;
                float fade = 1f - ActSetKit.Smooth01((edge - 0.8f) / 0.2f);
                float h = ActSetKit.Hash01(i, 14);
                Vector3 disp = v.normalized * (Mathf.Sin(t * 0.6f + h * 6.28f) * 2.2f);
                st.position = c + v + disp;
                float pulse = 1f + 0.6f * Mathf.Sin(t * 1.4f + h * 6.28f);
                st.localScale = Vector3.one * (0.3f * pulse * fade * w);
            }
        }
    }
}
