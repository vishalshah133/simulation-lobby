using SimulationLobby.Core;
using SimulationLobby.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.Breaker
{
    /// <summary>
    /// 16:9 HUD glue for the comp-001 "Three Rings" segment. Feeds <see cref="SegmentCardPresenter"/>
    /// (counter + result caption) and draws the one thing it has no slot for: a big countdown clock
    /// with a draining bar in the right margin.
    /// </summary>
    /// <remarks>
    /// Read-only toward the simulation. It does start the run (after the segment card has faded), because
    /// the clock is simulated time and must not burn while the title card covers the frame. The card and
    /// explainer are timed on wall-clock for the same reason, so they are not tied to the run starting.
    /// </remarks>
    public sealed class BreakerWideHud : MonoBehaviour
    {
        [SerializeField] SimulationRunner _runner;
        [SerializeField] BreakerSimulation _simulation;
        [SerializeField] SegmentCardPresenter _card;
        [SerializeField] float _startDelay = 1.5f;
        [SerializeField] float _clockLimit = 45f;
        [SerializeField] bool _randomizeSeed = true;
        [SerializeField] int _seed = 1;
        [SerializeField] float _ringCaptionSeconds = 1.4f;

        [SerializeField] TMP_Text _clockText;
        [SerializeField] RectTransform _barFill;
        [SerializeField] Image _barFillImage;

        static readonly Color Gold = new Color(1f, 0.82f, 0.2f, 1f);
        static readonly Color Alarm = new Color(1f, 0.28f, 0.22f, 1f);

        float _elapsed;
        bool _started;
        int _runId = -1;
        int _lastBroken;
        float _ringCaptionUntil;
        string _ringCaption;

        void Start()
        {
            if (_card != null)
            {
                // Wall-clock, so the card and explainer fade on schedule whether or not the run has begun.
                _card.SetTimeSource(() => _elapsed);
            }
        }

        void Update()
        {
            _elapsed += Time.unscaledDeltaTime;

            if (!_started && _elapsed >= _startDelay && _runner != null)
            {
                _started = true;
                int seed = _randomizeSeed ? System.Guid.NewGuid().GetHashCode() : _seed;
                Debug.Log($"[brk-wide] Seed {seed}. To re-render this take: set Seed on {nameof(BreakerWideHud)} to this value, untick Randomize Seed, Play.", this);
                _runner.Begin(seed);
            }

            if (_simulation == null || _simulation.RingCount == 0)
            {
                return;
            }

            if (_simulation.RunId != _runId)
            {
                _runId = _simulation.RunId;
                _lastBroken = 0;
                _ringCaptionUntil = 0f;
            }

            UpdateClock();
            UpdateCardText();
        }

        void UpdateClock()
        {
            bool live = _started && _simulation.HasClock;
            float left = live ? _simulation.SecondsLeft : _clockLimit;
            float fraction = _clockLimit > 0f ? Mathf.Clamp01(left / _clockLimit) : 0f;
            int whole = Mathf.CeilToInt(left);
            bool urgent = live && left <= 10f && !_simulation.Won;

            if (_clockText != null)
            {
                string text = $"{whole / 60}:{whole % 60:00}";
                if (_clockText.text != text)
                {
                    _clockText.text = text;
                }

                _clockText.color = urgent ? Alarm : Color.white;
            }

            if (_barFill != null)
            {
                _barFill.anchorMax = new Vector2(fraction, 1f);
            }

            if (_barFillImage != null)
            {
                _barFillImage.color = urgent ? Alarm : Gold;
            }
        }

        void UpdateCardText()
        {
            if (_card == null)
            {
                return;
            }

            int total = _simulation.RingCount;
            _card.SetCounter($"<size=45%>RINGS BROKEN</size>\n{_simulation.RingsBroken}/{total}");

            if (_simulation.RingsBroken != _lastBroken)
            {
                _lastBroken = _simulation.RingsBroken;
                _ringCaption = $"RING {_lastBroken} BROKEN!";
                _ringCaptionUntil = Time.time + _ringCaptionSeconds;
            }

            if (_simulation.TimeUp)
            {
                _card.SetResultCaption($"TIME UP! RING {_simulation.CurrentRing + 1} OF {total} IS STILL STANDING");
            }
            else if (_simulation.Won)
            {
                _card.SetResultCaption($"ALL {total} RINGS BROKEN WITH {_simulation.SecondsLeft:0.0}s LEFT!");
            }
            else if (Time.time < _ringCaptionUntil)
            {
                _card.SetResultCaption(_ringCaption);
            }
            else
            {
                _card.SetResultCaption(string.Empty);
            }
        }

        /// <summary>Builds the clock panel (right margin, under the counter) and wires everything up.</summary>
        public static BreakerWideHud Build(SimulationRunner runner, BreakerSimulation simulation, SegmentCardPresenter card,
                                           float startDelay, float clockLimit)
        {
            var root = new GameObject("Clock Canvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = WideFrameLayout.ReferenceResolution(AspectMode.Wide);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = WideFrameLayout.MatchWidthOrHeight(AspectMode.Wide);

            var hud = root.AddComponent<BreakerWideHud>();
            hud._runner = runner;
            hud._simulation = simulation;
            hud._card = card;
            hud._startDelay = startDelay;
            hud._clockLimit = clockLimit;

            // Same column and plate colour as the counter above it (SegmentCardPresenter right margin).
            RectTransform plate = Rect(root.transform, "ClockPlate", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-205f, -250f), new Vector2(390f, 270f));
            Image plateImage = plate.gameObject.AddComponent<Image>();
            plateImage.color = new Color(0.03f, 0.04f, 0.07f, 0.78f);
            plateImage.raycastTarget = false;

            TMP_Text label = Text(plate, "Label", "TIME LEFT", 34f, Gold, TextAlignmentOptions.Center);
            SetAnchors(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(0f, 44f));
            label.characterSpacing = 6f;

            hud._clockText = Text(plate, "Clock", "0:45", 150f, Color.white, TextAlignmentOptions.Center);
            SetAnchors(hud._clockText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            hud._clockText.rectTransform.offsetMin = new Vector2(10f, 66f);
            hud._clockText.rectTransform.offsetMax = new Vector2(-10f, -56f);
            hud._clockText.enableAutoSizing = true;
            hud._clockText.fontSizeMax = 150f;
            hud._clockText.fontSizeMin = 90f;

            RectTransform track = Rect(plate, "BarTrack", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-48f, 32f));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(1f, 1f, 1f, 0.16f);
            trackImage.raycastTarget = false;

            hud._barFill = Rect(track, "BarFill", Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            hud._barFill.offsetMin = Vector2.zero;
            hud._barFill.offsetMax = Vector2.zero;
            hud._barFillImage = hud._barFill.gameObject.AddComponent<Image>();
            hud._barFillImage.color = Gold;
            hud._barFillImage.raycastTarget = false;

            return hud;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            SetAnchors(rect, min, max, pivot, pos, size);
            return rect;
        }

        static void SetAnchors(RectTransform r, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static TMP_Text Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = FontStyles.Bold;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }
    }
}
