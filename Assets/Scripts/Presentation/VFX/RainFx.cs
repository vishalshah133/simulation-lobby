using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Pooled falling drops: a glowing streak falls from <c>from</c> to <c>to</c> over <c>seconds</c>, then a small splash
    /// ring and a few droplets at <c>to</c>. Many can be in flight at once. Builds its own materials (URP Unlit), like
    /// <see cref="BlastFx"/>. Presentation only: it never decides where a drop lands; the caller passes that in.
    /// </summary>
    public sealed class RainFx : MonoBehaviour
    {
        const int DropletCount = 5;
        const float SplashSeconds = 0.35f;
        const float Gravity = 9f;

        sealed class Slot
        {
            public GameObject root;
            public Transform streak;
            public Transform ring;
            public Transform[] droplets;
            public Material material;
            public bool active;
            public bool landed;
            public float startTime;
            public float landTime;
            public Vector3 from;
            public Vector3 to;
            public Color color;
            public float radius;
            public float dropletSeed;
        }

        [Range(8, 256)] public int poolSize = 96;

        [Tooltip("Streak thickness in metres.")]
        [Min(0.005f)] public float streakWidth = 0.07f;

        [Tooltip("Longest the streak is allowed to be, in metres (it is also capped at 35% of the fall).")]
        [Min(0.05f)] public float maxStreakLength = 1.1f;

        [Tooltip("Colour multiplier so the additive streak reads as emissive.")]
        [Min(1f)] public float intensity = 1.8f;

        Slot[] _slots;
        Mesh _cubeMesh;
        Mesh _ringMesh;

        /// <summary>Start one drop. It lands exactly <paramref name="seconds"/> from now (Time.time, so slow motion slows the fall).</summary>
        public void Play(Vector3 from, Vector3 to, float seconds, Color color, float splashRadiusWorld)
        {
            EnsurePool();
            if (_slots.Length == 0)
            {
                return;
            }

            Slot slot = null;
            Slot oldest = null;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                if (!s.active) { slot = s; break; }
                if (oldest == null || s.startTime < oldest.startTime) oldest = s;
            }

            if (slot == null)
            {
                slot = oldest;
            }

            float now = Time.time;
            slot.active = true;
            slot.landed = false;
            slot.startTime = now;
            slot.landTime = now + Mathf.Max(0.01f, seconds);
            slot.from = from;
            slot.to = to;
            slot.color = color;
            slot.radius = Mathf.Max(0.02f, splashRadiusWorld);
            slot.dropletSeed = (slot.dropletSeed + 1.618f + from.x * 3.1f + to.z * 7.7f) % 6.2831f;
            slot.root.SetActive(true);
            slot.root.transform.position = to;
            slot.ring.gameObject.SetActive(false);
            for (int i = 0; i < DropletCount; i++) slot.droplets[i].gameObject.SetActive(false);
            slot.streak.gameObject.SetActive(true);
            ApplyFalling(slot, now);
        }

        void Update()
        {
            if (_slots == null)
            {
                return;
            }

            float now = Time.time;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                if (!s.active)
                {
                    continue;
                }

                if (now < s.landTime)
                {
                    ApplyFalling(s, now);
                    continue;
                }

                float k = (now - s.landTime) / SplashSeconds;
                if (k >= 1f)
                {
                    s.active = false;
                    s.root.SetActive(false);
                    continue;
                }

                if (!s.landed)
                {
                    s.landed = true;
                    s.streak.gameObject.SetActive(false);
                    s.ring.gameObject.SetActive(true);
                    for (int d = 0; d < DropletCount; d++) s.droplets[d].gameObject.SetActive(true);
                }

                ApplySplash(s, k);
            }
        }

        void OnDestroy()
        {
            if (_slots != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    FxFactory.Destroy(_slots[i].material);
                    FxFactory.Destroy(_slots[i].root);
                }
            }

            FxFactory.Destroy(_cubeMesh);
            FxFactory.Destroy(_ringMesh);
            _slots = null;
        }

        void ApplyFalling(Slot s, float now)
        {
            float duration = Mathf.Max(0.01f, s.landTime - s.startTime);
            float u = Mathf.Clamp01((now - s.startTime) / duration);
            float e = u * u;   // accelerating, reads as gravity; lands exactly at u = 1

            Vector3 delta = s.to - s.from;
            float dist = delta.magnitude;
            Vector3 dir = dist > 1e-4f ? delta / dist : Vector3.down;
            Vector3 head = s.from + delta * e;
            float len = Mathf.Min(maxStreakLength, dist * 0.35f);
            len = Mathf.Min(len, (head - s.from).magnitude + 0.05f);   // grows out of the start point
            len = Mathf.Max(len, 0.05f);

            Transform t = s.streak;
            t.position = head - dir * (len * 0.5f);
            t.rotation = Quaternion.LookRotation(dir);
            t.localScale = new Vector3(streakWidth, streakWidth, len);
            float a = Mathf.Clamp01(u * 8f);   // fade in over the first part of the fall
            FxFactory.SetColor(s.material, Tint(s.color, a));
        }

        void ApplySplash(Slot s, float k)
        {
            float expand = 1f - (1f - k) * (1f - k);
            float r = Mathf.Max(0.001f, s.radius * (0.3f + 0.7f * expand));
            s.ring.position = s.to + new Vector3(0f, 0.02f, 0f);
            s.ring.localScale = new Vector3(r, 1f, r);

            float t = k * SplashSeconds;
            float size = Mathf.Max(0.025f, s.radius * 0.1f) * (1f - k * 0.5f);
            float speed = s.radius * 1.6f;
            float up = s.radius * 3.2f;
            for (int d = 0; d < DropletCount; d++)
            {
                float ang = s.dropletSeed + d * (Mathf.PI * 2f / DropletCount);
                float h = Mathf.Max(0f, up * t - 0.5f * Gravity * 0.6f * t * t);
                float sp = speed * (0.7f + 0.3f * ((d * 3) % 5) / 4f);
                Transform dt = s.droplets[d];
                dt.position = s.to + new Vector3(Mathf.Cos(ang) * sp * t, 0.03f + h, Mathf.Sin(ang) * sp * t);
                dt.localScale = new Vector3(size, size, size);
            }

            float fade = 1f - k;
            FxFactory.SetColor(s.material, Tint(s.color, fade * fade));
        }

        Color Tint(Color c, float a)
        {
            return new Color(c.r * intensity, c.g * intensity, c.b * intensity, a);
        }

        void EnsurePool()
        {
            if (_slots != null)
            {
                return;
            }

            _cubeMesh = FxFactory.CreateCube("RainStreak (runtime)");
            _ringMesh = FxFactory.CreateAnnulus("RainSplash (runtime)", 0.72f, 40);

            _slots = new Slot[poolSize];
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = new Slot();
                s.root = new GameObject("Drop " + i.ToString("00"));
                s.root.transform.SetParent(transform, false);
                s.material = FxFactory.CreateGlowMaterial("RainDrop (runtime)");

                var streak = new GameObject("Streak");
                streak.transform.SetParent(s.root.transform, false);
                FxFactory.AddMesh(streak, _cubeMesh, s.material);
                s.streak = streak.transform;

                var ring = new GameObject("Splash");
                ring.transform.SetParent(s.root.transform, false);
                FxFactory.AddMesh(ring, _ringMesh, s.material);
                s.ring = ring.transform;

                s.droplets = new Transform[DropletCount];
                for (int d = 0; d < DropletCount; d++)
                {
                    var go = new GameObject("Droplet");
                    go.transform.SetParent(s.root.transform, false);
                    FxFactory.AddMesh(go, _cubeMesh, s.material);
                    s.droplets[d] = go.transform;
                }

                s.root.SetActive(false);
                _slots[i] = s;
            }
        }
    }
}
