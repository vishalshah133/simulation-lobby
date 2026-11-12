using UnityEngine;

namespace SimulationLobby.Simulations.Convergence
{
    /// <summary>
    /// Paints the final round's ball paths into a persistent texture (a "caustic" of every chord drawn on
    /// black). It is shown as the last round ends and holds through the end card, after the live bodies
    /// (and their short trails) are gone. Read-only: it observes positions in <c>LateUpdate</c> and never
    /// touches the run. Cosmetic: the picture depends on the render frame rate (chords between frames),
    /// not on the seed, so it never feeds back into the maths.
    /// </summary>
    [RequireComponent(typeof(ConvergenceSimulation))]
    public sealed class CausticAccumulator : MonoBehaviour
    {
        const int AccScale = 4;

        [SerializeField] ConvergenceSimulation _simulation;
        [SerializeField] SpriteRenderer _renderer;

        [Tooltip("Half size of the painted area around the origin, world units.")]
        [SerializeField] Vector2 _halfExtent = new Vector2(9.1f, 3.3f);

        [Tooltip("Texture pixels per world unit. 100 is about 1:1 with a 1920 wide frame.")]
        [Range(40f, 200f)] [SerializeField] float _pixelsPerUnit = 100f;

        [Tooltip("Brightness added per chord crossing a pixel. Lower = more subtle, needs more overlap to glow.")]
        [Range(1f, 64f)] [SerializeField] float _gain = 16f;

        [Range(0.1f, 1f)] [SerializeField] float _saturation = 0.75f;

        [Tooltip("Upload to the GPU every N rendered frames while painting.")]
        [Range(1, 12)] [SerializeField] int _uploadEveryFrames = 3;

        [Tooltip("Sorting order of the painted sprite: below the dots and balls, above the boundary.")]
        [SerializeField] int _sortingOrder = 3;

        int _width;
        int _height;
        ushort[] _acc;
        Color32[] _pixels;
        Texture2D _texture;
        Sprite _sprite;
        Vector2[] _prev = new Vector2[0];
        Color[] _hue = new Color[0];
        bool _hasPrev;
        bool _dirty;
        bool _painting;
        int _frame;
        ConvergencePhase _lastPhase = ConvergencePhase.Done;

        public void Bind(ConvergenceSimulation simulation, SpriteRenderer renderer, Vector2 halfExtent, float pixelsPerUnit, float gain)
        {
            _simulation = simulation;
            _renderer = renderer;
            _halfExtent = halfExtent;
            _pixelsPerUnit = pixelsPerUnit;
            _gain = gain;
        }

        void Reset()
        {
            _simulation = GetComponent<ConvergenceSimulation>();
        }

        void Start()
        {
            _width = Mathf.Max(8, Mathf.CeilToInt(_halfExtent.x * 2f * _pixelsPerUnit));
            _height = Mathf.Max(8, Mathf.CeilToInt(_halfExtent.y * 2f * _pixelsPerUnit));
            _acc = new ushort[_width * _height * 3];
            _pixels = new Color32[_width * _height];
            _texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false)
            {
                name = "CausticTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);

            if (_renderer != null)
            {
                _sprite = Sprite.Create(_texture, new Rect(0f, 0f, _width, _height), new Vector2(0.5f, 0.5f), _pixelsPerUnit);
                _renderer.sprite = _sprite;
                _renderer.sortingOrder = _sortingOrder;
                _renderer.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        void OnDestroy()
        {
            if (_sprite != null)
            {
                Destroy(_sprite);
            }

            if (_texture != null)
            {
                Destroy(_texture);
            }
        }

        void LateUpdate()
        {
            if (_simulation == null || _simulation.Config == null || _texture == null)
            {
                return;
            }

            ConvergencePhase phase = _simulation.Phase;
            bool lastRound = _simulation.RoundIndex == _simulation.RoundTotal - 1;
            bool running = phase == ConvergencePhase.Running && lastRound;

            if (running && _lastPhase != ConvergencePhase.Running)
            {
                ClearPaint();
                PrepareHues(_simulation.RoundBallCount);
                _hasPrev = false;
                _painting = true;
            }

            if (running && _painting)
            {
                Paint();
                _frame++;
                if (_dirty && _frame % _uploadEveryFrames == 0)
                {
                    Upload();
                }
            }
            else if (_dirty)
            {
                Upload();
            }

            float alpha = 0f;
            if (_painting && lastRound)
            {
                if (phase == ConvergencePhase.Result)
                {
                    alpha = _simulation.PhaseProgress;
                }
                else if (phase == ConvergencePhase.EndCard || phase == ConvergencePhase.Done)
                {
                    alpha = 1f;
                }
            }

            if (_renderer != null && !Mathf.Approximately(_renderer.color.a, alpha))
            {
                _renderer.color = new Color(1f, 1f, 1f, alpha);
            }

            _lastPhase = phase;
        }

        void PrepareHues(int total)
        {
            ConvergenceConfig config = _simulation.Config;
            _hue = new Color[total];
            _prev = new Vector2[total];
            for (int i = 0; i < total; i++)
            {
                float fraction = config.hueSpan >= 1f
                    ? i / (float)Mathf.Max(1, total)
                    : (total <= 1 ? 0f : config.hueSpan * i / (total - 1));
                _hue[i] = Color.HSVToRGB(Mathf.Repeat(fraction, 1f), _saturation, 1f);
            }
        }

        void ClearPaint()
        {
            System.Array.Clear(_acc, 0, _acc.Length);
            System.Array.Clear(_pixels, 0, _pixels.Length);
            _dirty = true;
            _frame = 0;
        }

        void Paint()
        {
            int count = Mathf.Min(_simulation.ParticleCount, _hue.Length);
            for (int i = 0; i < count; i++)
            {
                Vector2 p = _simulation.GetParticlePosition(i);
                if (_hasPrev)
                {
                    Line(ToPixel(_prev[i]), ToPixel(p), _hue[i]);
                }

                _prev[i] = p;
            }

            _hasPrev = true;
        }

        Vector2Int ToPixel(Vector2 world)
        {
            return new Vector2Int(
                Mathf.RoundToInt(world.x * _pixelsPerUnit + _width * 0.5f),
                Mathf.RoundToInt(world.y * _pixelsPerUnit + _height * 0.5f));
        }

        void Line(Vector2Int a, Vector2Int b, Color colour)
        {
            int dx = Mathf.Abs(b.x - a.x);
            int dy = Mathf.Abs(b.y - a.y);
            if (dx > 600 || dy > 600)
            {
                return;
            }

            int sx = a.x < b.x ? 1 : -1;
            int sy = a.y < b.y ? 1 : -1;
            int err = dx - dy;
            int x = a.x;
            int y = a.y;
            float add = _gain * AccScale;
            int addR = Mathf.RoundToInt(colour.r * add);
            int addG = Mathf.RoundToInt(colour.g * add);
            int addB = Mathf.RoundToInt(colour.b * add);

            while (true)
            {
                if (x >= 0 && x < _width && y >= 0 && y < _height)
                {
                    Stamp(x, y, addR, addG, addB);
                }

                if (x == b.x && y == b.y)
                {
                    break;
                }

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }

            _dirty = true;
        }

        void Stamp(int x, int y, int addR, int addG, int addB)
        {
            int index = y * _width + x;
            int k = index * 3;
            int r = Mathf.Min(65535, _acc[k] + addR);
            int g = Mathf.Min(65535, _acc[k + 1] + addG);
            int b = Mathf.Min(65535, _acc[k + 2] + addB);
            _acc[k] = (ushort)r;
            _acc[k + 1] = (ushort)g;
            _acc[k + 2] = (ushort)b;

            int m = Mathf.Max(r, Mathf.Max(g, b));
            if (m <= 0)
            {
                return;
            }

            float s = 255f / m;
            int alpha = Mathf.Min(255, m / AccScale);
            _pixels[index] = new Color32((byte)(r * s), (byte)(g * s), (byte)(b * s), (byte)alpha);
        }

        void Upload()
        {
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
            _dirty = false;
        }
    }
}
