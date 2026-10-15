using System;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A one-reel slot machine: a masked window onto a vertical strip of faces, with a header line above
    /// ("BLUE is SPINNING"). The strip scrolls down, decelerates, and lands exactly on the requested
    /// face at exactly the requested time. Format-agnostic ("faces", "a header"); a thin binder in the
    /// format supplies the strings and plays the audio off the events. Presentation only: the result is
    /// decided by the caller, and the reel merely animates toward it.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SlotReelHud : MonoBehaviour
    {
        enum Phase
        {
            Idle = 0,
            Spinning = 1,
            Holding = 2,
            Fading = 3
        }

        [SerializeField] CanvasGroup _group;
        [SerializeField] TMP_Text _header;
        [SerializeField] RectTransform _window;
        [SerializeField] RectTransform _strip;
        [SerializeField] TMP_Text _faceTemplate;

        [Header("Reel")]
        [Min(20f)] public float faceHeight = 190f;
        [Tooltip("At least this many faces scroll past before the reel lands.")]
        [Min(4)] public int minimumPasses = 22;
        [Range(2f, 6f)] public float easePower = 3.2f;
        [Range(0.3f, 1f)] public float edgeScale = 0.62f;
        [Range(0f, 1f)] public float edgeAlpha = 0.45f;

        [Header("Landing")]
        [Range(0f, 0.6f)] public float punchAmount = 0.22f;
        [Min(0.05f)] public float punchSeconds = 0.4f;
        [Min(0.05f)] public float fadeSeconds = 0.4f;

        [Header("Idle")]
        [Tooltip("Keep the drum on screen: Hide() returns to the dimmed idle face instead of fading out.")]
        public bool alwaysVisible;
        [Range(0f, 1f)] public float idleAlpha = 0.6f;
        [Tooltip("Colour the landed face with the header colour. Off keeps it white (for coloured face panels).")]
        public bool tintLandedFace = true;

        const int RowCount = 5; // centre, two above, two below

        public bool IsSpinning => _phase == Phase.Spinning;

        /// <summary>One per face crossing the centre of the window.</summary>
        public event Action FacePassed;

        /// <summary>The result index, once, at the moment the reel stops.</summary>
        public event Action<int> Landed;

        TMP_Text[] _rows = new TMP_Text[0];
        string[] _faces = new string[0];
        Phase _phase;
        int _result;
        int _target;           // final strip position, in faces; _target % faces.Length == _result
        int _lastCentre;
        float _elapsed;
        float _duration;
        float _position;       // current strip position, in faces
        float _holdTimer;
        float _hideDelay;
        bool _hideRequested;
        float _landedAt;
        Color _headerColor = Color.white;
        bool _idle;
        string _idleFace = string.Empty;

        public void Bind(CanvasGroup group, TMP_Text header, RectTransform window, RectTransform strip, TMP_Text faceTemplate)
        {
            _group = group;
            _header = header;
            _window = window;
            _strip = strip;
            _faceTemplate = faceTemplate;
            if (_faceTemplate != null)
            {
                _faceTemplate.gameObject.SetActive(false);
            }

            if (_group != null)
            {
                _group.alpha = alwaysVisible ? idleAlpha : 0f;
            }
        }

        /// <summary>
        /// Shows the drum at rest on <paramref name="face"/>, dimmed, with an empty header. Safe to call in
        /// edit mode (no Update needed). With <see cref="alwaysVisible"/>, Hide() returns here.
        /// </summary>
        public void SetIdle(string face)
        {
            _idleFace = face ?? string.Empty;
            if (_strip == null || _faceTemplate == null)
            {
                return;
            }

            EnsureRows();
            _faces = new[] { _idleFace };
            _phase = Phase.Idle;
            _idle = true;
            _hideRequested = false;
            _position = 0f;
            _headerColor = Color.white;
            if (_header != null)
            {
                _header.text = string.Empty;
            }

            if (_group != null)
            {
                _group.alpha = idleAlpha;
            }

            if (_window != null)
            {
                _window.localScale = Vector3.one;
            }

            Layout();
        }

        public void Spin(string[] faces, int resultIndex, float seconds, string header, Color headerColor)
        {
            if (faces == null || faces.Length == 0 || _strip == null || _faceTemplate == null)
            {
                return;
            }

            EnsureRows();
            _faces = faces;
            _result = Mathf.Clamp(resultIndex, 0, faces.Length - 1);
            _headerColor = headerColor;

            // Target is the smallest position past the minimum that shows the result face. The strip
            // "cycles through" the faces by index modulo, so the strip is as long as the spin needs.
            int n = faces.Length;
            int loops = Mathf.CeilToInt(Mathf.Max(0f, minimumPasses - _result) / n);
            _target = _result + loops * n;

            _position = 0f;
            _lastCentre = 0;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, seconds);
            _phase = Phase.Spinning;
            _hideRequested = false;
            _idle = false;

            if (_header != null)
            {
                _header.text = header;
                _header.color = headerColor;
            }

            if (_group != null)
            {
                _group.alpha = 1f;
            }

            if (_window != null)
            {
                _window.localScale = Vector3.one;
            }

            Layout();
        }

        public void Hide(float delaySeconds = 1.5f)
        {
            if (_phase == Phase.Idle)
            {
                return;
            }

            _hideDelay = Mathf.Max(0f, delaySeconds);
            _hideRequested = true;
            if (_phase == Phase.Spinning)
            {
                return; // the delay starts when the reel lands
            }

            _holdTimer = 0f;
            _phase = Phase.Holding;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            switch (_phase)
            {
                case Phase.Spinning:
                    _elapsed += dt;
                    float u = Mathf.Clamp01(_elapsed / _duration);
                    // Ease-out: fast at the start, crawling into the result. Exactly _target at u == 1.
                    float eased = 1f - Mathf.Pow(1f - u, easePower);
                    _position = u >= 1f ? _target : _target * eased;
                    CountPasses();
                    Layout();
                    if (u >= 1f)
                    {
                        _phase = Phase.Holding;
                        _holdTimer = 0f;
                        _landedAt = Time.unscaledTime;
                        Landed?.Invoke(_result);
                    }

                    break;

                case Phase.Holding:
                    ApplyPunch();
                    _holdTimer += dt;
                    if (_hideRequested && _holdTimer >= _hideDelay)
                    {
                        _phase = Phase.Fading;
                        _holdTimer = 0f;
                    }

                    break;

                case Phase.Fading:
                    ApplyPunch();
                    _holdTimer += dt;
                    if (_group != null)
                    {
                        _group.alpha = Mathf.Lerp(1f, alwaysVisible ? idleAlpha : 0f, Mathf.Clamp01(_holdTimer / fadeSeconds));
                    }

                    if (_holdTimer >= fadeSeconds)
                    {
                        _phase = Phase.Idle;
                        if (alwaysVisible)
                        {
                            SetIdle(_idleFace);
                        }
                    }

                    break;
            }
        }

        void ApplyPunch()
        {
            if (_window == null)
            {
                return;
            }

            float t = (Time.unscaledTime - _landedAt) / punchSeconds;
            float punch = t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI) * (1f - t) * punchAmount * 2f;
            _window.localScale = Vector3.one * (1f + punch);
        }

        void CountPasses()
        {
            // A face crosses the centre each time the nearest whole position changes.
            int centre = Mathf.FloorToInt(_position + 0.5f);
            while (_lastCentre < centre)
            {
                _lastCentre++;
                FacePassed?.Invoke();
            }
        }

        void Layout()
        {
            int n = _faces.Length;
            int centre = Mathf.FloorToInt(_position + 0.5f);
            int half = RowCount / 2;
            bool landed = _phase != Phase.Spinning || _position >= _target;
            for (int r = 0; r < RowCount; r++)
            {
                // Row k sits above the centre when k > position: faces enter at the top, travel down.
                int k = centre + half - r;
                float offset = (k - _position) * faceHeight;
                float distance = Mathf.Abs(_position - k);
                float closeness = Mathf.Clamp01(1f - distance);

                TMP_Text row = _rows[r];
                row.rectTransform.anchoredPosition = new Vector2(0f, offset);
                int face = ((k % n) + n) % n;
                if (row.text != _faces[face])
                {
                    row.text = _faces[face];
                }

                float scale = Mathf.Lerp(edgeScale, 1f, closeness);
                row.rectTransform.localScale = new Vector3(scale, scale, 1f);
                Color c = tintLandedFace && landed && distance < 0.01f ? _headerColor : Color.white;
                c.a = _idle && distance > 0.5f ? 0f : Mathf.Lerp(edgeAlpha, 1f, closeness);
                row.color = c;
            }
        }

        void EnsureRows()
        {
            if (_rows.Length == RowCount)
            {
                return;
            }

            _rows = new TMP_Text[RowCount];
            for (int i = 0; i < RowCount; i++)
            {
                GameObject go = Instantiate(_faceTemplate.gameObject, _strip, false);
                go.name = $"Face {i}";
                go.SetActive(true);
                var text = go.GetComponent<TMP_Text>();
                RectTransform rect = text.rectTransform;
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(0f, faceHeight);
                _rows[i] = text;
            }
        }
    }
}
