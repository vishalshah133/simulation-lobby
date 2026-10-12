using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Pooled emissive bar that extends from one world point to another, then fades. Several can play at once.
    /// Presentation only. Positions are WORLD space, so this component may sit anywhere.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LineBeamFx : MonoBehaviour
    {
        const float FadeSeconds = 0.3f;

        sealed class Slot
        {
            public Transform transform;
            public Material material;
            public bool active;
            public float birth;
            public float seconds;
            public Vector3 from;
            public Vector3 dir;
            public float length;
            public float width;
            public Color color;
        }

        [Range(1, 32)] public int poolSize = 12;
        [Min(1f)] public float intensity = 1.8f;
        [Tooltip("World height the beam is raised above the given points.")]
        public float lift = 0.06f;

        Slot[] _slots;
        int _next;
        Mesh _cube;

        public void Play(Vector3 from, Vector3 to, float widthWorld, Color color, float seconds = 0.45f)
        {
            EnsurePool();
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (_slots.Length == 0 || length < 0.0001f)
            {
                return;
            }

            Slot slot = null;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].active) { slot = _slots[i]; break; }
            }

            if (slot == null)
            {
                slot = _slots[_next];
                _next = (_next + 1) % _slots.Length;
            }

            slot.active = true;
            slot.birth = Time.time;
            slot.seconds = Mathf.Max(0.05f, seconds);
            slot.from = from + Vector3.up * lift;
            slot.dir = delta / length;
            slot.length = length;
            slot.width = Mathf.Max(0.005f, widthWorld);
            slot.color = color;
            slot.transform.rotation = Quaternion.LookRotation(slot.dir, Vector3.up);
            slot.transform.gameObject.SetActive(true);
            Apply(slot, 0f);
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

                float elapsed = now - s.birth;
                if (elapsed >= s.seconds + FadeSeconds)
                {
                    s.active = false;
                    s.transform.gameObject.SetActive(false);
                    continue;
                }

                Apply(s, elapsed);
            }
        }

        void OnDestroy()
        {
            if (_slots != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    FxFactory.Destroy(_slots[i].material);
                    if (_slots[i].transform != null) FxFactory.Destroy(_slots[i].transform.gameObject);
                }
            }

            FxFactory.Destroy(_cube);
            _slots = null;
        }

        void Apply(Slot s, float elapsed)
        {
            float grow = Mathf.Clamp01(elapsed / s.seconds);
            float len = Mathf.Max(0.001f, s.length * grow);
            s.transform.position = s.from + s.dir * (len * 0.5f);
            s.transform.localScale = new Vector3(s.width, s.width * 0.5f, len);

            float a = elapsed <= s.seconds ? 1f : Mathf.Clamp01(1f - (elapsed - s.seconds) / FadeSeconds);
            FxFactory.SetColor(s.material, new Color(s.color.r * intensity, s.color.g * intensity, s.color.b * intensity, a * 0.95f));
        }

        void EnsurePool()
        {
            if (_slots != null)
            {
                return;
            }

            _cube = FxFactory.CreateCube("Beam (runtime)");
            _slots = new Slot[poolSize];
            for (int i = 0; i < _slots.Length; i++)
            {
                var go = new GameObject("Beam " + i.ToString("00"));
                go.transform.SetParent(transform, false);
                var slot = new Slot { transform = go.transform, material = FxFactory.CreateGlowMaterial("Beam (runtime)") };
                FxFactory.AddMesh(go, _cube, slot.material);
                go.SetActive(false);
                _slots[i] = slot;
            }
        }
    }
}
