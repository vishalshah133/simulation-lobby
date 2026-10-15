using UnityEngine;

namespace SimulationLobby.Simulations.Convergence
{
    /// <summary>
    /// One reusable "they met" burst: a soft bloom plus an expanding ring, fading over a fraction of a
    /// second. Presentation only; runs on game time, which Unity Recorder locks to the capture frame rate.
    /// </summary>
    public sealed class ConvergenceFlash : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _bloom;
        [SerializeField] SpriteRenderer _ring;

        [Tooltip("Seconds the burst lasts.")]
        [Range(0.1f, 2f)] [SerializeField] float _duration = 0.5f;

        [Tooltip("Bloom diameter at its peak, world units.")]
        [Min(0.1f)] [SerializeField] float _bloomSize = 1.6f;

        [Tooltip("Ring diameter at the end of its expansion, world units.")]
        [Min(0.1f)] [SerializeField] float _ringSize = 2.6f;

        [SerializeField] Color _color = new Color(1f, 0.97f, 0.8f, 1f);

        float _age = float.MaxValue;
        float _bloomUnit = 1f;
        float _ringUnit = 1f;

        /// <summary>Creates the renderers under a new child of <paramref name="parent"/>.</summary>
        public static ConvergenceFlash Create(Transform parent, Sprite glow, Sprite ring, Material material, int sortingOrder)
        {
            var root = new GameObject("MeetFlash");
            root.transform.SetParent(parent, false);
            var flash = root.AddComponent<ConvergenceFlash>();
            flash._bloom = flash.MakeRenderer("Bloom", glow, material, sortingOrder);
            flash._ring = flash.MakeRenderer("Ring", ring, material, sortingOrder + 1);
            flash._bloomUnit = SpriteUnit(glow);
            flash._ringUnit = SpriteUnit(ring);
            flash.Hide();
            return flash;
        }

        static float SpriteUnit(Sprite sprite)
        {
            return sprite != null && sprite.bounds.size.x > 0f ? 1f / sprite.bounds.size.x : 1f;
        }

        SpriteRenderer MakeRenderer(string childName, Sprite sprite, Material material, int sortingOrder)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return renderer;
        }

        /// <summary>Start (or restart) the burst at a world position.</summary>
        public void Play(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            _age = 0f;
            SetVisible(true);
        }

        void Update()
        {
            if (_age >= _duration)
            {
                return;
            }

            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _duration);
            if (t >= 1f)
            {
                Hide();
                return;
            }

            float fade = 1f - t;
            float burst = 1f - (1f - t) * (1f - t);

            float bloomDiameter = _bloomSize * Mathf.Lerp(0.5f, 1f, burst);
            _bloom.transform.localScale = Vector3.one * (bloomDiameter * _bloomUnit);
            Color bloom = _color;
            bloom.a = fade * fade;
            _bloom.color = bloom;

            float ringDiameter = _ringSize * burst;
            _ring.transform.localScale = Vector3.one * (ringDiameter * _ringUnit);
            Color ring = _color;
            ring.a = fade;
            _ring.color = ring;
        }

        void Hide()
        {
            _age = float.MaxValue;
            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            if (_bloom != null)
            {
                _bloom.enabled = visible;
            }

            if (_ring != null)
            {
                _ring.enabled = visible;
            }
        }
    }
}
