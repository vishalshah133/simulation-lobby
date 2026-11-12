using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Wide-frame HUD for compilation segments: a full-screen "n / total" card, a "HOW IT WORKS"
    /// panel, the top-bar hook, a right-margin counter and a bottom result caption.
    /// </summary>
    /// <remarks>
    /// Read-only. It is handed a time source (seconds of simulated time) and strings; it never touches
    /// a run. With no source set it looks for a <c>SimulationRunner</c> in the scene, then falls back to
    /// unscaled wall-clock time since it started.
    /// </remarks>
    public sealed class SegmentCardPresenter : MonoBehaviour
    {
        public const float FadeSeconds = 0.5f;

        [SerializeField] SegmentCardConfig _config;
        [SerializeField] AspectMode _mode = AspectMode.Wide;
        [SerializeField] CanvasGroup _cardGroup;
        [SerializeField] TMP_Text _cardNumber;
        [SerializeField] TMP_Text _cardTitle;
        [SerializeField] CanvasGroup _explainerGroup;
        [SerializeField] TMP_Text _explainerHeading;
        [SerializeField] TMP_Text _explainerBody;
        [SerializeField] TMP_Text _hookText;
        [SerializeField] TMP_Text _counterText;
        [SerializeField] TMP_Text _captionText;

        Func<float> _timeSource;
        SimulationLobby.Core.SimulationRunner _runner;
        float _fallbackTime;

        /// <summary>Supply simulated seconds since the segment began (e.g. <c>() =&gt; runner.SimulatedSeconds</c>).</summary>
        public void SetTimeSource(Func<float> secondsSinceStart)
        {
            _timeSource = secondsSinceStart;
        }

        public void SetHook(string text) { Set(_hookText, text); }
        public void SetCounter(string text) { Set(_counterText, text); }
        public void SetResultCaption(string text) { Set(_captionText, text); }

        static void Set(TMP_Text target, string text)
        {
            // TMP rebuilds on every assignment; callers push every frame, so write only on change.
            text = text ?? string.Empty;
            if (target != null && target.text != text)
            {
                target.text = text;
            }
        }

        void Start()
        {
            Apply(0f);
        }

        void Update()
        {
            _fallbackTime += Time.unscaledDeltaTime;
            Apply(CurrentTime());
        }

        float CurrentTime()
        {
            if (_timeSource != null)
            {
                return _timeSource();
            }

            if (_runner == null)
            {
                _runner = FindFirstObjectByType<SimulationLobby.Core.SimulationRunner>();
            }

            return _runner != null && _runner.IsRunning ? _runner.SimulatedSeconds : _fallbackTime;
        }

        void Apply(float t)
        {
            if (_config == null)
            {
                return;
            }

            float card = _config.cardSeconds;
            float explainerEnd = card + _config.explainerSeconds;
            SetAlpha(_cardGroup, FadeOut(t, card));
            float explainerAlpha = t < card ? 0f : Mathf.Min(Mathf.Clamp01((t - card) / FadeSeconds), FadeOut(t, explainerEnd));
            SetAlpha(_explainerGroup, explainerAlpha);
        }

        static float FadeOut(float t, float end)
        {
            return Mathf.Clamp01((end - t) / FadeSeconds);
        }

        static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null && !Mathf.Approximately(group.alpha, alpha))
            {
                group.alpha = alpha;
            }
        }

        // ------------------------------------------------------------------ factory

        static readonly Color Plate = new Color(0.03f, 0.04f, 0.07f, 0.78f);
        static readonly Color CardPlate = new Color(0.02f, 0.03f, 0.06f, 0.96f);

        /// <summary>Builds the overlay canvas and presenter in the active scene. Works in edit and play mode.</summary>
        public static SegmentCardPresenter Build(SegmentCardConfig config, AspectMode mode)
        {
            bool wide = mode == AspectMode.Wide;
            Color accent = config != null ? config.accent : Color.white;

            var root = new GameObject("Segment Card Canvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = WideFrameLayout.ReferenceResolution(mode);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = WideFrameLayout.MatchWidthOrHeight(mode);

            var p = root.AddComponent<SegmentCardPresenter>();
            p._config = config;
            p._mode = mode;

            float hookSize = wide ? 48f : 52f;
            float counterSize = wide ? 72f : 64f;
            float captionSize = wide ? 48f : 52f;
            float bodySize = wide ? 36f : 38f;

            // Top bar: hook, left aligned, one line.
            RectTransform top = Panel(root.transform, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, wide ? -52f : -80f), new Vector2(0f, wide ? 104f : 160f), Plate);
            p._hookText = Text(top, "Hook", config != null ? config.hook : "", hookSize, Color.white, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(p._hookText.rectTransform, 36f, 36f, 4f, 4f);
            p._hookText.textWrappingMode = TextWrappingModes.NoWrap;
            p._hookText.overflowMode = TextOverflowModes.Ellipsis;
            p._hookText.enableAutoSizing = true;
            p._hookText.fontSizeMax = hookSize;
            p._hookText.fontSizeMin = hookSize * 0.8f;

            // Right margin counter (Wide) / just under the hook (Portrait).
            RectTransform counterPlate = wide
                ? Panel(root.transform, "RightMargin", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-205f, 40f), new Vector2(390f, 260f), Plate)
                : Panel(root.transform, "RightMargin", new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-250f, -250f), new Vector2(460f, 130f), Plate);
            p._counterText = Text(counterPlate, "Counter", "", counterSize, accent, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(p._counterText.rectTransform, 12f, 12f, 8f, 8f);
            p._counterText.enableAutoSizing = true;
            p._counterText.fontSizeMax = counterSize;
            p._counterText.fontSizeMin = counterSize * 0.6f;

            // Bottom strip: result caption.
            RectTransform bottom = Panel(root.transform, "BottomStrip", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, wide ? 52f : 90f), new Vector2(0f, wide ? 104f : 150f), Plate);
            p._captionText = Text(bottom, "Caption", "", captionSize, Color.white, FontStyles.Bold, TextAlignmentOptions.Midline);
            Stretch(p._captionText.rectTransform, 36f, 36f, 4f, 4f);
            p._captionText.textWrappingMode = TextWrappingModes.NoWrap;

            // Explainer: covers the bottom strip while shown.
            float explainerHeight = wide ? 150f : 240f;
            RectTransform explainer = Panel(root.transform, "Explainer", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, explainerHeight * 0.5f), new Vector2(0f, explainerHeight), new Color(0.03f, 0.04f, 0.07f, 0.92f));
            p._explainerGroup = explainer.gameObject.AddComponent<CanvasGroup>();
            p._explainerGroup.alpha = 0f;
            p._explainerGroup.blocksRaycasts = false;
            var bar = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(explainer, false);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.sizeDelta = new Vector2(14f, 0f);
            barRect.anchoredPosition = Vector2.zero;
            var barImage = bar.GetComponent<Image>();
            barImage.color = accent;
            barImage.raycastTarget = false;
            p._explainerHeading = Text(explainer, "Heading", "HOW IT WORKS", bodySize * 0.8f, accent, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            p._explainerHeading.characterSpacing = 6f;
            Anchor(p._explainerHeading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-72f, bodySize));
            p._explainerBody = Text(explainer, "Body", config != null ? config.explainer : "", bodySize, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Anchor(p._explainerBody.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            p._explainerBody.rectTransform.offsetMin = new Vector2(36f, 8f);
            p._explainerBody.rectTransform.offsetMax = new Vector2(-36f, -10f - bodySize);
            p._explainerBody.textWrappingMode = TextWrappingModes.Normal;

            // Full-screen card, drawn last so it covers everything.
            RectTransform card = Panel(root.transform, "Card", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, CardPlate);
            card.offsetMin = Vector2.zero;
            card.offsetMax = Vector2.zero;
            p._cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            p._cardGroup.alpha = 1f;
            p._cardGroup.blocksRaycasts = false;
            string number = config != null ? config.index + " / " + config.total : "";
            p._cardNumber = Text(card, "Number", number, wide ? 220f : 200f, accent, FontStyles.Bold, TextAlignmentOptions.Center);
            Anchor(p._cardNumber.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(0f, 260f));
            p._cardTitle = Text(card, "Title", config != null ? config.title.ToUpperInvariant() : "", wide ? 100f : 96f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Anchor(p._cardTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-80f, 140f));
            p._cardTitle.enableAutoSizing = true;
            p._cardTitle.fontSizeMax = wide ? 100f : 96f;
            p._cardTitle.fontSizeMin = 48f;

            return p;
        }

        static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Anchor(rect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), pos, size);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static TMP_Text Text(Transform parent, string name, string text, float size, Color color, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        static void Anchor(RectTransform r, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static void Stretch(RectTransform r, float left, float right, float bottom, float top)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(-right, -top);
        }
    }
}
