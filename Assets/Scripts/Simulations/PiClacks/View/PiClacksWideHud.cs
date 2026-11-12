using System.Globalization;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.PiClacks
{
    /// <summary>
    /// 16:9 HUD binder for the <c>clack</c> wide variant (comp-001 segment 1, Digit Jump). Pushes the live
    /// clack count into the right-margin counter, the round caption into the bottom strip and builds the digit
    /// ladder (3 / 314 / 31415 / 3141592) as a column on the right edge. Reads the simulation in
    /// <c>LateUpdate</c> and never writes to it; the segment card/explainer overlay is the shared
    /// <see cref="SegmentCardPresenter"/>.
    /// </summary>
    [RequireComponent(typeof(PiClacksSimulation))]
    public sealed class PiClacksWideHud : MonoBehaviour
    {
        [SerializeField] SegmentCardPresenter _card;

        [Tooltip("Silence (no clack) longer than this during a run shows the WAIT... caption, seconds.")]
        [Min(0.5f)] [SerializeField] float _waitAfterSeconds = 1.6f;

        [Tooltip("Seconds after a round finishes before its digits drop onto the ladder.")]
        [Range(0f, 1.5f)] [SerializeField] float _ladderDelaySeconds = 0.4f;

        [SerializeField] Color _digitTint = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField] Color _resultTint = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField] float _ladderFontSize = 50f;
        [SerializeField] string _waitCaption = "WAIT...";
        [Range(0.4f, 0.9f)] [SerializeField] float _counterCellEm = 0.6f;

        PiClacksSimulation _simulation;
        SimulationRunner _runner;
        TMP_Text _rows;
        TMP_Text _footer;
        readonly System.Collections.Generic.List<float> _due = new System.Collections.Generic.List<float>(4);
        int _pushed;
        string _rowsText = string.Empty;
        long _lastSerial;
        float _silence;
        bool _footerSet;

        /// <summary>Wired by the wide scene builder.</summary>
        public void Bind(SegmentCardPresenter card)
        {
            _card = card;
        }

        void Awake()
        {
            _simulation = GetComponent<PiClacksSimulation>();
            _runner = GetComponent<SimulationRunner>();
            BuildLadder();
        }

        void Start()
        {
            if (_card != null && _runner != null)
            {
                _card.SetTimeSource(() => _runner.SimulatedSeconds);
            }
        }

        void LateUpdate()
        {
            if (_simulation == null || _simulation.Config == null || _card == null)
            {
                return;
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            PiClacksConfig config = _simulation.Config;
            PiClacksPhase phase = _simulation.Phase;

            UpdateSilence(phase, dt);
            _card.SetCounter(CounterText(config, phase));
            _card.SetResultCaption(CaptionText(config, phase));
            UpdateLadder(config, phase, dt);
        }

        void UpdateSilence(PiClacksPhase phase, float dt)
        {
            long serial = _simulation.ClackSerial;
            if (phase != PiClacksPhase.Run || _simulation.RoundFinished || serial != _lastSerial)
            {
                _silence = 0f;
            }
            else
            {
                _silence += dt;
            }

            _lastSerial = serial;
        }

        string CounterText(PiClacksConfig config, PiClacksPhase phase)
        {
            bool live = phase == PiClacksPhase.Run || phase == PiClacksPhase.Result;
            long count = live ? _simulation.ClackCount : 0L;
            string digits = count.ToString(CultureInfo.InvariantCulture);
            int width = Mathf.Max(digits.Length, _simulation.ExpectedCount.ToString(CultureInfo.InvariantCulture).Length);
            string pad = new string('0', width - digits.Length);
            string cell = _counterCellEm.ToString("0.###", CultureInfo.InvariantCulture);
            return config.counterLabel + "\n<mspace=" + cell + "em>" + digits + "<alpha=#00>" + pad + "</mspace>";
        }

        string CaptionText(PiClacksConfig config, PiClacksPhase phase)
        {
            PiClacksRound round = _simulation.CurrentRound;
            switch (phase)
            {
                case PiClacksPhase.Hold:
                    return !string.IsNullOrEmpty(round.holdCaption) ? round.holdCaption : config.holdCaption;
                case PiClacksPhase.Transition:
                    return round.caption;
                case PiClacksPhase.Run:
                case PiClacksPhase.Result:
                    if (_simulation.RoundFinished)
                    {
                        string line = (round.result ?? string.Empty).Replace("{n}", _simulation.ClackCount.ToString(CultureInfo.InvariantCulture));
                        return "<color=#" + ColorUtility.ToHtmlStringRGB(_resultTint) + ">" + line + "</color>";
                    }

                    return _silence >= _waitAfterSeconds ? _waitCaption : round.caption;
                case PiClacksPhase.EndCard:
                    return "<color=#" + ColorUtility.ToHtmlStringRGB(_resultTint) + ">" + config.endCard + "</color>";
                default:
                    return string.Empty;
            }
        }

        // ---------------------------------------------------------------- digit ladder

        void UpdateLadder(PiClacksConfig config, PiClacksPhase phase, float dt)
        {
            var done = _simulation.CompletedCounts;
            while (_due.Count + _pushed < done.Count)
            {
                _due.Add(_ladderDelaySeconds);
            }

            bool flush = phase == PiClacksPhase.EndCard || phase == PiClacksPhase.Done;
            for (int i = 0; i < _due.Count; i++)
            {
                _due[i] -= dt;
            }

            while (_due.Count > 0 && (flush || _due[0] <= 0f))
            {
                _due.RemoveAt(0);
                string previous = _pushed > 0 ? done[_pushed - 1].ToString(CultureInfo.InvariantCulture) : string.Empty;
                string line = RowText(done[_pushed].ToString(CultureInfo.InvariantCulture), previous.Length);
                _rowsText = _rowsText.Length == 0 ? line : _rowsText + "\n" + line;
                if (_rows != null)
                {
                    _rows.text = _rowsText;
                }

                _pushed++;
            }

            if (phase == PiClacksPhase.EndCard && !_footerSet && _footer != null)
            {
                _footerSet = true;
                _footer.text = config.endLine;
            }
        }

        /// <summary>The digits this round added to the previous count are tinted.</summary>
        string RowText(string digits, int previousLength)
        {
            int keep = Mathf.Clamp(previousLength, 0, digits.Length);
            if (keep == digits.Length)
            {
                return digits;
            }

            return digits.Substring(0, keep) + "<color=#" + ColorUtility.ToHtmlStringRGB(_digitTint) + ">" +
                   digits.Substring(keep) + "</color>";
        }

        /// <summary>
        /// A transparent text-only column on the right edge, below the counter plate and above the bottom strip.
        /// Its own canvas sits under the segment card canvas (sorting 90 vs 100) so the title card and the
        /// explainer cover it.
        /// </summary>
        void BuildLadder()
        {
            var root = new GameObject("Digit Ladder Canvas");
            root.transform.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = WideFrameLayout.ReferenceResolution(AspectMode.Wide);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = WideFrameLayout.MatchWidthOrHeight(AspectMode.Wide);

            // Right margin is 390 wide (counter plate), centred 205 px in from the right edge. The column sits
            // from 165 px above the bottom (clear of the 150 px explainer) up to 450 px, under the counter plate.
            var column = new GameObject("Column", typeof(RectTransform));
            column.transform.SetParent(root.transform, false);
            var rect = (RectTransform)column.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(-205f, 165f);
            rect.sizeDelta = new Vector2(390f, 285f);

            _rows = CreateText(rect, "Rows", _ladderFontSize, new Color(0.9f, 0.96f, 1f, 1f), TextAlignmentOptions.TopRight);
            _rows.rectTransform.anchorMin = Vector2.zero;
            _rows.rectTransform.anchorMax = Vector2.one;
            _rows.rectTransform.offsetMin = new Vector2(10f, 60f);
            _rows.rectTransform.offsetMax = new Vector2(-30f, 0f);

            _footer = CreateText(rect, "Footer", _ladderFontSize * 0.9f, _digitTint, TextAlignmentOptions.BottomRight);
            _footer.rectTransform.anchorMin = new Vector2(0f, 0f);
            _footer.rectTransform.anchorMax = new Vector2(1f, 0f);
            _footer.rectTransform.pivot = new Vector2(0.5f, 0f);
            _footer.rectTransform.anchoredPosition = Vector2.zero;
            _footer.rectTransform.sizeDelta = new Vector2(-40f, 56f);
            _footer.enableAutoSizing = true;
            _footer.fontSizeMax = _ladderFontSize * 0.9f;
            _footer.fontSizeMin = 24f;
        }

        static TMP_Text CreateText(Transform parent, string name, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.fontStyle = FontStyles.Bold;
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }
    }
}
