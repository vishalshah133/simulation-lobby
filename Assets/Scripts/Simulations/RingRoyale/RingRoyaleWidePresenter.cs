using System.Collections.Generic;
using SimulationLobby.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.RingRoyale
{
    /// <summary>
    /// 16:9 companion to <see cref="RingRoyalePresenter"/> for comp-001 segment 6 ("Six Colours"). It owns the
    /// wide-frame pieces only: a vertical per-colour share column in the right margin, a letter on every
    /// marble (so six hues stay tellable apart at 480px), the segment card counter and result caption, and a
    /// slow camera zoom onto the last two colours. Audio, tinting, ring dressing and confetti stay with
    /// <see cref="RingRoyalePresenter"/>. Runs in <c>LateUpdate</c>, reads run state and never writes it.
    /// </summary>
    [RequireComponent(typeof(RingRoyaleSimulation))]
    public sealed class RingRoyaleWidePresenter : MonoBehaviour
    {
        [Header("Wiring (set by the scene builder)")]
        [SerializeField] RingRoyaleSimulation _simulation;
        [SerializeField] SegmentCardPresenter _card;
        [SerializeField] Camera _camera;
        [SerializeField] RectTransform _column;
        [SerializeField] Image[] _segments = new Image[0];
        [SerializeField] TMP_Text[] _labels = new TMP_Text[0];

        [Header("Marble letters")]
        [Tooltip("One glyph per colour, painted on each marble. Same order as the config colours.")]
        [SerializeField] string[] _glyphs = { "R", "Y", "G", "C", "B", "P" };
        [Tooltip("TextMeshPro font size, world units (about a tenth of a world unit per point).")]
        [SerializeField] float _glyphSize = 3.4f;
        [SerializeField] Color _glyphColour = new Color(0.03f, 0.03f, 0.05f, 1f);

        [Header("Share column")]
        [Range(0f, 0.3f)] [SerializeField] float _minAliveShare = 0.1f;
        [Range(0f, 0.2f)] [SerializeField] float _outShare = 0.05f;
        [Range(0f, 1f)] [SerializeField] float _outAlpha = 0.35f;
        [Range(1f, 30f)] [SerializeField] float _glideSpeed = 8f;

        [Header("Last-two zoom")]
        [Tooltip("Camera ortho size at full zoom, as a fraction of the starting size.")]
        [Range(0.4f, 1f)] [SerializeField] float _zoomFraction = 0.72f;
        [Tooltip("Seconds the zoom-in takes once two colours remain.")]
        [Min(0.5f)] [SerializeField] float _zoomInSeconds = 9f;
        [Tooltip("Seconds the zoom takes to ease back out when the winner is sealed in.")]
        [Min(0.2f)] [SerializeField] float _zoomOutSeconds = 1.4f;
        [Tooltip("How far, as a fraction, the view slides from the ring centre toward the survivors.")]
        [Range(0f, 1f)] [SerializeField] float _focusPull = 0.45f;
        [Tooltip("Most the view may slide from the ring centre, world units.")]
        [Min(0f)] [SerializeField] float _maxFocusOffset = 1.6f;

        [Header("Captions")]
        [SerializeField] string _setupCaption = "6 COLOURS. ONE EXIT. WHO SURVIVES?";
        [SerializeField] string _runningCaption = "GOLD ARC CLONES. THE EXIT DROPS.";
        [SerializeField] string _suddenDeathCaption = "SUDDEN DEATH: THE EXIT KEEPS WIDENING";
        [SerializeField] string _lastTwoCaption = "LAST TWO COLOURS";
        [SerializeField] string _whyCaption = "IT CLONED FASTER THAN THE EXIT DROPPED IT";
        [Range(1.2f, 6f)] [SerializeField] float _outHoldSeconds = 2.5f;

        readonly Dictionary<RoyaleMarble, TMP_Text> _marbleGlyphs = new Dictionary<RoyaleMarble, TMP_Text>();
        readonly Dictionary<RoyaleMarble, int> _marbleGlyphColour = new Dictionary<RoyaleMarble, int>();

        float[] _heights = new float[0];
        float[] _targets = new float[0];
        bool[] _seenEliminated = new bool[0];
        bool _snap = true;
        float _outTimer;
        int _outColour = -1;
        float _zoom;
        Vector3 _basePosition;
        float _baseOrtho;
        bool _baseCaptured;
        Vector2 _focus;
        float _lastGameSeconds;

        void Reset()
        {
            _simulation = GetComponent<RingRoyaleSimulation>();
        }

        void Start()
        {
            CaptureBase();
        }

        void CaptureBase()
        {
            if (_camera == null || _baseCaptured)
            {
                return;
            }

            _basePosition = _camera.transform.position;
            _baseOrtho = _camera.orthographicSize;
            _baseCaptured = true;
        }

        void LateUpdate()
        {
            if (_simulation == null || _simulation.Config == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            RingRoyaleSimulation sim = _simulation;

            if (sim.GameSeconds < _lastGameSeconds || sim.IsHolding)
            {
                // A new run: drop zoom and stale out-banners.
                _zoom = 0f;
                _outTimer = 0f;
                _outColour = -1;
                _focus = Vector2.zero;
                _snap = true;
                if (_seenEliminated.Length != sim.ColourCount)
                {
                    _seenEliminated = new bool[sim.ColourCount];
                }

                for (int i = 0; i < _seenEliminated.Length; i++)
                {
                    _seenEliminated[i] = false;
                }
            }

            _lastGameSeconds = sim.GameSeconds;

            LabelMarbles(sim.Inside);
            LabelMarbles(sim.Escaping);
            UpdateOutBanner(dt);
            UpdateColumn(dt);
            UpdateCard();
            UpdateCamera(dt);
        }

        // ------------------------------------------------------------------ marble letters

        void LabelMarbles(IReadOnlyList<RoyaleMarble> marbles)
        {
            for (int i = 0; i < marbles.Count; i++)
            {
                RoyaleMarble marble = marbles[i];
                if (marble == null || marble.Renderer == null)
                {
                    continue;
                }

                if (!_marbleGlyphs.TryGetValue(marble, out TMP_Text text))
                {
                    text = CreateGlyph(marble);
                    _marbleGlyphs[marble] = text;
                    _marbleGlyphColour[marble] = -1;
                }

                if (_marbleGlyphColour[marble] != marble.Colour)
                {
                    _marbleGlyphColour[marble] = marble.Colour;
                    text.text = GlyphOf(marble.Colour);
                }
            }
        }

        TMP_Text CreateGlyph(RoyaleMarble marble)
        {
            var go = new GameObject("Glyph");
            go.transform.SetParent(marble.transform, false);
            go.transform.localPosition = Vector3.zero;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(1f, 1f);
            tmp.fontSize = _glyphSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = _glyphColour;
            tmp.sortingOrder = 8;
            return tmp;
        }

        string GlyphOf(int colour)
        {
            if (_glyphs != null && colour >= 0 && colour < _glyphs.Length)
            {
                return _glyphs[colour];
            }

            return (colour + 1).ToString();
        }

        // ------------------------------------------------------------------ out banner

        void UpdateOutBanner(float dt)
        {
            RingRoyaleSimulation sim = _simulation;
            for (int c = 0; c < _seenEliminated.Length; c++)
            {
                bool now = sim.IsEliminated(c);
                if (now == _seenEliminated[c])
                {
                    continue;
                }

                _seenEliminated[c] = now;
                if (now && sim.Winner < 0)
                {
                    _outColour = c;
                    _outTimer = _outHoldSeconds;
                }
            }

            if (_outTimer > 0f)
            {
                _outTimer -= dt;
            }

            if (sim.Winner >= 0)
            {
                _outTimer = 0f;
            }
        }

        // ------------------------------------------------------------------ share column

        void UpdateColumn(float dt)
        {
            if (_column == null || _segments.Length == 0)
            {
                return;
            }

            RingRoyaleSimulation sim = _simulation;
            int n = Mathf.Min(sim.ColourCount, _segments.Length);
            if (_heights.Length != _segments.Length)
            {
                _heights = new float[_segments.Length];
                _targets = new float[_segments.Length];
                _snap = true;
            }

            IReadOnlyList<int> counts = sim.Counts;
            long total = 0;
            for (int i = 0; i < n; i++)
            {
                total += Mathf.Max(0, counts[i]);
            }

            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                float share = total > 0 ? (float)((double)Mathf.Max(0, counts[i]) / total) : 1f / n;
                _targets[i] = sim.IsEliminated(i) ? _outShare : Mathf.Max(share, _minAliveShare);
                sum += _targets[i];
            }

            float blend = _snap ? 1f : 1f - Mathf.Exp(-_glideSpeed * dt);
            _snap = false;
            float totalHeight = _column.rect.height;
            float y = 0f;
            Color[] palette = sim.Config.colours;

            // Normalise the *glided* heights so the stack always fills the column exactly.
            float glidedSum = 0f;
            for (int i = 0; i < n; i++)
            {
                _heights[i] = Mathf.Lerp(_heights[i], _targets[i] / Mathf.Max(0.0001f, sum), blend);
                glidedSum += _heights[i];
            }

            for (int i = 0; i < n; i++)
            {
                float h = totalHeight * _heights[i] / Mathf.Max(0.0001f, glidedSum);
                var rect = _segments[i].rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -y);
                rect.sizeDelta = new Vector2(0f, h);
                y += h;

                bool out_ = sim.IsEliminated(i);
                Color colour = palette[i];
                colour.a = out_ ? _outAlpha : 1f;
                _segments[i].color = colour;

                if (_labels != null && i < _labels.Length && _labels[i] != null)
                {
                    string text = out_ ? $"{GlyphOf(i)}  OUT" : $"{GlyphOf(i)}  {counts[i]}";
                    if (_labels[i].text != text)
                    {
                        _labels[i].text = text;
                    }

                    _labels[i].color = out_ ? new Color(0.88f, 0.88f, 0.92f, 1f) : _glyphColour;
                }
            }
        }

        // ------------------------------------------------------------------ card text

        void UpdateCard()
        {
            if (_card == null)
            {
                return;
            }

            RingRoyaleSimulation sim = _simulation;
            RingRoyaleConfig config = sim.Config;
            _card.SetCounter($"{sim.AliveTotal} LEFT");

            string caption;
            if (sim.Winner >= 0)
            {
                caption = $"{Tag(config, sim.Winner)} WINS: {_whyCaption}";
            }
            else if (_outTimer > 0f && _outColour >= 0)
            {
                caption = $"{Tag(config, _outColour)} IS OUT!";
            }
            else if (sim.IsHolding)
            {
                caption = _setupCaption;
            }
            else if (sim.SuddenDeathActive)
            {
                caption = _suddenDeathCaption;
            }
            else if (RemainingColours(out _, out _) == 2)
            {
                caption = _lastTwoCaption;
            }
            else
            {
                caption = _runningCaption;
            }

            _card.SetResultCaption(caption);
        }

        static string Tag(RingRoyaleConfig config, int colour)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(config.colours[colour])}>{config.NameOf(colour)}</color>";
        }

        int RemainingColours(out int first, out int second)
        {
            first = -1;
            second = -1;
            int remaining = 0;
            for (int c = 0; c < _simulation.ColourCount; c++)
            {
                if (_simulation.IsEliminated(c))
                {
                    continue;
                }

                if (remaining == 0)
                {
                    first = c;
                }
                else if (remaining == 1)
                {
                    second = c;
                }

                remaining++;
            }

            return remaining;
        }

        // ------------------------------------------------------------------ camera

        void UpdateCamera(float dt)
        {
            if (_camera == null)
            {
                return;
            }

            CaptureBase();
            RingRoyaleSimulation sim = _simulation;

            bool zoomIn = sim.IsRunning && sim.Winner < 0 && RemainingColours(out int a, out int b) == 2;
            if (zoomIn)
            {
                _zoom = Mathf.Min(1f, _zoom + dt / _zoomInSeconds);
                RemainingColours(out a, out b);
                _focus = Vector2.Lerp(_focus, SurvivorCentroid(a, b), 1f - Mathf.Exp(-3f * dt));
            }
            else
            {
                _zoom = Mathf.Max(0f, _zoom - dt / _zoomOutSeconds);
                _focus = Vector2.Lerp(_focus, Vector2.zero, 1f - Mathf.Exp(-4f * dt));
            }

            float eased = _zoom * _zoom * (3f - 2f * _zoom);
            float ortho = Mathf.Lerp(_baseOrtho, _baseOrtho * _zoomFraction, eased);
            _camera.orthographicSize = ortho;

            // Keep the ring centred in the area left of the right margin as the view tightens.
            float shift = WideFrameLayout.ArenaCameraOffsetX(ortho, AspectMode.Wide);
            Vector2 pull = Vector2.ClampMagnitude(_focus * _focusPull, _maxFocusOffset) * eased;
            _camera.transform.position = new Vector3(shift + pull.x, _basePosition.y + pull.y, _basePosition.z);
        }

        Vector2 SurvivorCentroid(int a, int b)
        {
            IReadOnlyList<RoyaleMarble> inside = _simulation.Inside;
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < inside.Count; i++)
            {
                RoyaleMarble m = inside[i];
                if (m.Colour == a || m.Colour == b)
                {
                    sum += m.Body.position;
                    count++;
                }
            }

            return count > 0 ? sum / count : Vector2.zero;
        }

        // ------------------------------------------------------------------ wiring

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(RingRoyaleSimulation simulation, SegmentCardPresenter card, Camera camera,
                         RectTransform column, Image[] segments, TMP_Text[] labels)
        {
            _simulation = simulation;
            _card = card;
            _camera = camera;
            _column = column;
            _segments = segments;
            _labels = labels;
        }

        /// <summary>Assigned by the scene builder: one glyph per colour.</summary>
        public void SetGlyphs(string[] glyphs)
        {
            _glyphs = glyphs;
        }
    }
}
