using UnityEngine;

namespace SimulationLobby.Simulations.Brachistochrone
{
    /// <summary>
    /// A small pool of finish bursts: a ring that expands and fades plus a handful of dots flung outward.
    /// Cosmetic only; owned and ticked by <see cref="BrachistochronePresenter"/>, which supplies the
    /// (seeded) random directions.
    /// </summary>
    public sealed class BrachBurstPool
    {
        sealed class Particle
        {
            public Transform Root;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Age = float.MaxValue;
            public float Life = 0.7f;
            public float Size = 0.3f;
            public bool IsRing;
            public Color Color;
        }

        readonly Particle[] _particles;
        int _next;

        public BrachBurstPool(Transform parent, Sprite dot, Sprite ring, int count, int sortingOrder)
        {
            _particles = new Particle[Mathf.Max(8, count)];
            for (int i = 0; i < _particles.Length; i++)
            {
                var go = new GameObject("Burst_" + i);
                go.transform.SetParent(parent, false);
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = dot;
                renderer.sortingOrder = sortingOrder;
                go.SetActive(false);
                _particles[i] = new Particle { Root = go.transform, Renderer = renderer };
            }

            _ringSprite = ring;
            _dot = dot;
        }

        readonly Sprite _ringSprite;
        readonly Sprite _dot;

        /// <summary>A ring plus <paramref name="dots"/> dots at <paramref name="position"/>. <paramref name="rand"/> in 0..1.</summary>
        public void Spawn(Vector3 position, Color color, int dots, System.Func<float> rand)
        {
            Emit(position, Vector2.zero, color, 1.0f, 0.9f, true);
            for (int i = 0; i < dots; i++)
            {
                float angle = (i + rand() * 0.8f) / dots * Mathf.PI * 2f;
                float speed = 2.2f + rand() * 2.6f;
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                Emit(position, velocity, Color.Lerp(color, Color.white, 0.35f), 0.16f + rand() * 0.14f, 0.55f + rand() * 0.3f, false);
            }
        }

        void Emit(Vector3 position, Vector2 velocity, Color color, float size, float life, bool ring)
        {
            Particle p = _particles[_next];
            _next = (_next + 1) % _particles.Length;
            p.Age = 0f;
            p.Life = life;
            p.Size = size;
            p.Velocity = velocity;
            p.IsRing = ring;
            p.Color = color;
            p.Root.position = position;
            p.Renderer.sprite = ring ? _ringSprite : _dot;
            p.Root.gameObject.SetActive(true);
            Apply(p);
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                Particle p = _particles[i];
                if (p.Age >= p.Life)
                {
                    continue;
                }

                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Root.gameObject.SetActive(false);
                    continue;
                }

                p.Velocity *= Mathf.Exp(-2.2f * dt);
                p.Root.position += (Vector3)(p.Velocity * dt);
                Apply(p);
            }
        }

        static void Apply(Particle p)
        {
            float t = Mathf.Clamp01(p.Age / p.Life);
            float fade = 1f - t;
            float scale = p.IsRing ? Mathf.Lerp(0.6f, 3.4f, 1f - fade * fade) : p.Size * (0.4f + 0.6f * fade);
            p.Root.localScale = new Vector3(scale, scale, 1f);
            p.Renderer.color = new Color(p.Color.r, p.Color.g, p.Color.b, p.IsRing ? fade * 0.9f : fade);
        }
    }
}
