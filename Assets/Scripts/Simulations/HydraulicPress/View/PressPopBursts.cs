using UnityEngine;

namespace SimulationLobby.Simulations.HydraulicPress
{
    /// <summary>
    /// Pooled white burst rings drawn through one <see cref="PressQuadBatch"/>, no GameObject per ring. A fixed
    /// ring buffer: when it is full the oldest ring is overwritten. The presenter caps how many it spawns per frame.
    /// </summary>
    public sealed class PressPopBursts
    {
        struct Ring
        {
            public float X, Y, Radius, Age, Life;
        }

        readonly PressQuadBatch _batch;
        readonly Ring[] _rings;
        readonly float _growth;
        readonly float _minRadius;
        int _next;

        public PressPopBursts(PressQuadBatch batch, int capacity, float growth, float minRadius)
        {
            _batch = batch;
            _rings = new Ring[Mathf.Max(8, capacity)];
            for (int i = 0; i < _rings.Length; i++)
            {
                _rings[i].Age = float.MaxValue;
            }

            _growth = growth;
            _minRadius = minRadius;
        }

        public void Spawn(float x, float y, float radius, float life)
        {
            _rings[_next] = new Ring { X = x, Y = y, Radius = Mathf.Max(_minRadius, radius), Age = 0f, Life = Mathf.Max(0.05f, life) };
            _next = (_next + 1) % _rings.Length;
        }

        public void Clear()
        {
            for (int i = 0; i < _rings.Length; i++)
            {
                _rings[i].Age = float.MaxValue;
            }
        }

        /// <summary>Advance every live ring by dt seconds and redraw the batch.</summary>
        public void Tick(float dt)
        {
            _batch.Begin();
            for (int i = 0; i < _rings.Length; i++)
            {
                Ring ring = _rings[i];
                if (ring.Age >= ring.Life)
                {
                    continue;
                }

                ring.Age += dt;
                _rings[i] = ring;
                if (ring.Age >= ring.Life)
                {
                    continue;
                }

                float t = ring.Age / ring.Life;
                float size = ring.Radius * (1f + (_growth - 1f) * (1f - (1f - t) * (1f - t)));
                float fade = 1f - t;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(fade * fade * 0.95f) * 255f);
                _batch.Add(ring.X, ring.Y, size, size, new Color32(255, 255, 255, alpha));
            }

            _batch.End();
        }
    }
}
