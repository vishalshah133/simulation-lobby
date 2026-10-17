using UnityEngine;

namespace SimulationLobby.Simulations.PiClacks
{
    /// <summary>
    /// A small pool of white contact sparks: a soft glow plus a thin vertical flash line, each living a
    /// fraction of a second. Cosmetic only; owned and ticked by <see cref="PiClacksPresenter"/>.
    /// </summary>
    public sealed class ClackSparkPool
    {
        sealed class Spark
        {
            public Transform Root;
            public SpriteRenderer Glow;
            public SpriteRenderer Line;
            public float Age = float.MaxValue;
            public float Life = 0.16f;
            public float Height = 1f;
        }

        readonly Spark[] _sparks;
        readonly float _glowSize;
        readonly float _lineWidth;
        int _next;

        public ClackSparkPool(Transform parent, Sprite glow, Sprite square, int count, int sortingOrder,
                              float glowSize, float lineWidth)
        {
            _glowSize = glowSize;
            _lineWidth = lineWidth;
            _sparks = new Spark[Mathf.Max(1, count)];
            for (int i = 0; i < _sparks.Length; i++)
            {
                var root = new GameObject("Spark_" + i).transform;
                root.SetParent(parent, false);

                var glowObject = new GameObject("Glow");
                glowObject.transform.SetParent(root, false);
                SpriteRenderer glowRenderer = glowObject.AddComponent<SpriteRenderer>();
                glowRenderer.sprite = glow;
                glowRenderer.sortingOrder = sortingOrder;

                var lineObject = new GameObject("Flash");
                lineObject.transform.SetParent(root, false);
                SpriteRenderer lineRenderer = lineObject.AddComponent<SpriteRenderer>();
                lineRenderer.sprite = square;
                lineRenderer.sortingOrder = sortingOrder + 1;

                root.gameObject.SetActive(false);
                _sparks[i] = new Spark { Root = root, Glow = glowRenderer, Line = lineRenderer };
            }
        }

        /// <summary>Flash at <paramref name="position"/>; <paramref name="height"/> is the flash line's length.</summary>
        public void Spawn(Vector3 position, float height, float life)
        {
            Spark spark = _sparks[_next];
            _next = (_next + 1) % _sparks.Length;
            spark.Age = 0f;
            spark.Life = Mathf.Max(0.02f, life);
            spark.Height = height;
            spark.Root.position = position;
            spark.Root.gameObject.SetActive(true);
            Apply(spark);
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _sparks.Length; i++)
            {
                Spark spark = _sparks[i];
                if (spark.Age >= spark.Life)
                {
                    continue;
                }

                spark.Age += dt;
                if (spark.Age >= spark.Life)
                {
                    spark.Root.gameObject.SetActive(false);
                    continue;
                }

                Apply(spark);
            }
        }

        void Apply(Spark spark)
        {
            float t = Mathf.Clamp01(spark.Age / spark.Life);
            float fade = 1f - t;
            float glowScale = _glowSize * Mathf.Lerp(0.5f, 1.3f, t);
            spark.Glow.transform.localScale = new Vector3(glowScale, glowScale, 1f);
            spark.Glow.color = new Color(1f, 1f, 1f, fade);

            spark.Line.transform.localScale = new Vector3(_lineWidth * fade, spark.Height, 1f);
            spark.Line.color = new Color(1f, 1f, 1f, fade);
        }
    }
}
