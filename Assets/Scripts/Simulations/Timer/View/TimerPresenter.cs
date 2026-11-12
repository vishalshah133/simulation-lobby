using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.Timer
{
    [Serializable]
    public sealed class TimerHudSettings
    {
        [Header("Text")]
        public string setupFormat = "STARTING IN {0}";
        public string goText = "GO!";
        public string chipLabel = "IN THE BUCKET";
        public string chipFormat = "{0} / {1}";
        [Tooltip("Used only when the config's wording fields are empty.")]
        public string fallbackQuestionFormat = "HOW MANY OF {0} MARBLES MAKE IT IN {1}?";
        public string fallbackAllInFormat = "ALL {0} MADE IT!";
        public string fallbackResultFormat = "{0} OF {1} MADE IT";

        [Header("Countdown")]
        [Tooltip("Digit advance in em, so M:SS never jitters as digits change.")]
        public float digitAdvanceEm = 0.6f;
        [Min(1f)] public float urgentSeconds = 10f;
        [Min(0f)] public float pulseScale = 0.06f;
        public Color textColor = Color.white;
        public Color urgentColor = new Color(1f, 0.4f, 0.34f, 1f);
        public Color fillColor = new Color(0.93f, 0.96f, 1f, 0.92f);
        [Min(0.1f)] public float colorLerpPerSecond = 6f;

        [Header("Pop animations (real seconds)")]
        [Min(0.1f)] public float goSeconds = 1f;
        [Min(0.05f)] public float timesUpPopSeconds = 0.45f;
        [Min(0.1f)] public float questionPopSeconds = 0.5f;
        [Min(0.1f)] public float questionShrinkSeconds = 0.7f;
        [Min(0.1f)] public float allInPopSeconds = 0.7f;
        [Range(0f, 1f)] public float flashPeak = 0.3f;
    }

    /// <summary>
    /// The restrained corner HUD for <c>timer</c>: a compact countdown chip with a thin progress bar (top-right), a small marble
    /// counter chip (top-left), a small setup line under the countdown and a modest TIME'S UP chip (top-centre). Every element is
    /// anchored to its corner so nothing can leave the view at any aspect, and the bottom of the frame stays empty. Reads
    /// <see cref="ITimerState"/> in <c>LateUpdate</c> and never writes the run. Values come from the state; only the pop and
    /// pulse animations use real time. The scene builder wires the references through <c>TimerHudBuilder</c>.
    /// </summary>
    public sealed class TimerPresenter : MonoBehaviour
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        [Header("Wiring (set by TimerHudBuilder)")]
        [SerializeField] TimerSimulation sim;
        [SerializeField] RectTransform badgeRoot;
        [SerializeField] TMP_Text countdownText;
        [SerializeField] RectTransform barFill;
        [SerializeField] Image barFillImage;
        [SerializeField] TMP_Text chipText;
        [SerializeField] RectTransform setupRoot;
        [SerializeField] CanvasGroup setupGroup;
        [SerializeField] TMP_Text setupText;
        [SerializeField] RectTransform timesUpRoot;
        [SerializeField] CanvasGroup timesUpGroup;
        [SerializeField] TMP_Text timesUpText;

        [SerializeField] RectTransform questionRoot;
        [SerializeField] CanvasGroup questionGroup;
        [SerializeField] TMP_Text questionText;
        [SerializeField] RectTransform subtitleRoot;
        [SerializeField] CanvasGroup subtitleGroup;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] RectTransform allInRoot;
        [SerializeField] CanvasGroup allInGroup;
        [SerializeField] TMP_Text allInText;
        [SerializeField] Image flashImage;

        [SerializeField] TimerHudSettings settings = new TimerHudSettings();

        int _shownSeconds = -1;
        int _shownCollected = -1;
        int _shownSetup = -1;
        float _lastElapsed;
        TimerPhase _lastPhase = TimerPhase.Setup;
        float _pulseAge = 10f;
        float _setupAge = 10f;
        float _goAge = -1f;
        float _timesUpAge = -1f;
        float _questionAge;
        float _releaseAge = -1f;
        float _allInAge = -1f;
        float _subFade = 1f;
        int _shownResult = -1;
        float _layoutWidth = -1f;
        bool _layoutDirty = true;
        Color _text;
        Color _fill;

        public TimerHudSettings Settings => settings;

        void OnEnable()
        {
            _text = settings.textColor;
            _fill = settings.fillColor;
        }

        void LateUpdate()
        {
            if (sim == null || countdownText == null)
            {
                return;
            }

            ITimerState state = sim;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (state.Elapsed + 0.5f < _lastElapsed || (_lastPhase != TimerPhase.Setup && state.Phase == TimerPhase.Setup))
            {
                ResetState();
            }

            _lastElapsed = state.Elapsed;
            if (_lastPhase == TimerPhase.Setup && state.Phase == TimerPhase.Running)
            {
                _goAge = 0f;
                if (setupText != null)
                {
                    setupText.text = settings.goText;
                }
            }

            if (_lastPhase != TimerPhase.Finale && _lastPhase != TimerPhase.Done
                && (state.Phase == TimerPhase.Finale || state.Phase == TimerPhase.Done))
            {
                if (_allInAge < 0f)
                {
                    _timesUpAge = 0f;
                }
            }

            if (_allInAge < 0f && state.Released && state.TotalMarbles > 0 && state.CollectedCount >= state.TotalMarbles)
            {
                _allInAge = 0f;
                _timesUpAge = -1f;
                _layoutDirty = true;
                if (allInText != null)
                {
                    allInText.text = string.Format(Invariant, AllInFormat(), state.TotalMarbles);
                }
            }

            if (_releaseAge < 0f && state.Phase != TimerPhase.Setup)
            {
                _releaseAge = 0f;
            }

            _lastPhase = state.Phase;

            DrawCountdown(state, dt);
            DrawBar(state);
            DrawChip(state);
            DrawSetup(state, dt);
            DrawTimesUp(state, dt);
            DrawQuestion(state, dt);
            DrawAllIn(state, dt);
        }

        void ResetState()
        {
            _shownSeconds = -1;
            _shownCollected = -1;
            _shownSetup = -1;
            _pulseAge = 10f;
            _setupAge = 10f;
            _goAge = -1f;
            _timesUpAge = -1f;
            _questionAge = 0f;
            _releaseAge = -1f;
            _allInAge = -1f;
            _subFade = 1f;
            _shownResult = -1;
            _layoutDirty = true;
            _text = settings.textColor;
            _fill = settings.fillColor;
        }

        void DrawCountdown(ITimerState state, float dt)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(state.Remaining - 0.001f));
            bool finished = state.Phase == TimerPhase.Finale || state.Phase == TimerPhase.Done;
            if (finished)
            {
                seconds = 0;
            }

            bool urgent = state.Released && seconds <= settings.urgentSeconds;
            if (seconds != _shownSeconds)
            {
                if (urgent)
                {
                    _pulseAge = 0f;
                }

                _shownSeconds = seconds;
                countdownText.text = "<mspace=" + settings.digitAdvanceEm.ToString("0.###", Invariant) + "em>"
                    + (seconds / 60).ToString(Invariant) + ":" + (seconds % 60).ToString("00", Invariant) + "</mspace>";
            }

            float k = 1f - Mathf.Exp(-settings.colorLerpPerSecond * dt);
            _text = Color.Lerp(_text, urgent ? settings.urgentColor : settings.textColor, k);
            _fill = Color.Lerp(_fill, urgent ? settings.urgentColor : settings.fillColor, k);
            countdownText.color = _text;

            // One gentle swell per second while urgent: 1.0 -> 1.06 -> 1.0.
            _pulseAge += dt;
            float scale = 1f + settings.pulseScale * Mathf.Sin(Mathf.PI * Mathf.Clamp01(_pulseAge));
            if (badgeRoot != null)
            {
                badgeRoot.localScale = new Vector3(scale, scale, 1f);
            }
        }

        void DrawBar(ITimerState state)
        {
            if (barFill == null)
            {
                return;
            }

            float p = state.Duration > 0f ? Mathf.Clamp01(state.Elapsed / state.Duration) : 1f;
            barFill.anchorMax = new Vector2(p, 1f);
            if (barFillImage != null)
            {
                barFillImage.color = _fill;
            }
        }

        void DrawChip(ITimerState state)
        {
            if (chipText == null || state.CollectedCount == _shownCollected)
            {
                return;
            }

            _shownCollected = state.CollectedCount;
            chipText.text = "<size=55%>" + settings.chipLabel + "</size>\n"
                + string.Format(Invariant, settings.chipFormat, state.CollectedCount, state.TotalMarbles);
        }

        void DrawSetup(ITimerState state, float dt)
        {
            if (setupGroup == null)
            {
                return;
            }

            float alpha;
            float scale = 1f;
            if (state.Phase == TimerPhase.Setup)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(state.SetupRemaining - 0.001f));
                if (n != _shownSetup)
                {
                    _shownSetup = n;
                    _setupAge = 0f;
                    setupText.text = string.Format(Invariant, settings.setupFormat, n);
                }

                _setupAge += dt;
                scale = 1f + 0.08f * Mathf.Exp(-_setupAge * 8f) * Mathf.Cos(_setupAge * 15f);
                alpha = 1f;
            }
            else if (_goAge >= 0f && _goAge < settings.goSeconds)
            {
                _goAge += dt;
                scale = 1f + 0.08f * Mathf.Exp(-_goAge * 7f) * Mathf.Cos(_goAge * 14f);
                alpha = Mathf.Clamp01((settings.goSeconds - _goAge) / 0.3f);
            }
            else
            {
                alpha = 0f;
            }

            setupGroup.alpha = alpha;
            if (setupRoot != null)
            {
                setupRoot.localScale = new Vector3(scale, scale, 1f);
            }
        }

        // The 0:00 result card ("247 OF 300 MADE IT"). Holds to the end; updates if a late marble lands, and is replaced by
        // the all-in card if the last marble arrives.
        void DrawTimesUp(ITimerState state, float dt)
        {
            if (timesUpGroup == null || timesUpRoot == null)
            {
                return;
            }

            if (_timesUpAge < 0f || _allInAge >= 0f)
            {
                timesUpGroup.alpha = 0f;
                return;
            }

            if (state.CollectedCount != _shownResult)
            {
                _shownResult = state.CollectedCount;
                timesUpText.text = string.Format(Invariant, ResultFormat(), state.CollectedCount, state.TotalMarbles);
                _layoutDirty = true;
            }

            _timesUpAge += dt;
            float u = Mathf.Clamp01(_timesUpAge / settings.timesUpPopSeconds);
            float scale = Mathf.LerpUnclamped(0.7f, 1f, EaseOutBack(u));
            timesUpGroup.alpha = Mathf.Clamp01(_timesUpAge / 0.15f);
            timesUpRoot.localScale = new Vector3(scale, scale, 1f);
        }

        // The question: a big card through the setup hold, then a small persistent subtitle at the same top-centre spot.
        void DrawQuestion(ITimerState state, float dt)
        {
            if (questionRoot == null || questionGroup == null || questionText == null)
            {
                return;
            }

            if (questionText.text.Length == 0)
            {
                string text = string.Format(Invariant, QuestionFormat(), state.TotalMarbles, DurationLabel(state.Duration));
                questionText.text = text;
                if (subtitleText != null)
                {
                    subtitleText.text = text;
                }

                _layoutDirty = true;
            }

            _questionAge += dt;
            float pop = Mathf.Clamp01(_questionAge / settings.questionPopSeconds);
            float bigScale = Mathf.LerpUnclamped(0.7f, 1f, EaseOutBack(pop));
            float bigAlpha = Mathf.Clamp01(_questionAge / 0.15f);
            float subAlpha = 0f;
            float subScale = 1f;

            if (_releaseAge >= 0f)
            {
                _releaseAge += dt;
                float t = Mathf.Clamp01(_releaseAge / settings.questionShrinkSeconds);
                float e = t * t * (3f - 2f * t);
                bigAlpha *= 1f - e;
                bigScale = Mathf.Lerp(bigScale, 0.55f, e);
                subAlpha = e;
                subScale = Mathf.Lerp(1.2f, 1f, e);
            }

            // The subtitle yields the spot to the result and all-in cards.
            bool bannerUp = _allInAge >= 0f || _timesUpAge >= 0f;
            _subFade = Mathf.MoveTowards(_subFade, bannerUp ? 0f : 1f, dt * 4f);
            subAlpha *= _subFade;

            questionGroup.alpha = bigAlpha;
            questionRoot.localScale = new Vector3(bigScale, bigScale, 1f);
            if (subtitleGroup != null && subtitleRoot != null)
            {
                subtitleGroup.alpha = subAlpha;
                subtitleRoot.localScale = new Vector3(subScale, subScale, 1f);
            }

            Layout();
        }

        void DrawAllIn(ITimerState state, float dt)
        {
            if (allInGroup == null || allInRoot == null)
            {
                return;
            }

            if (_allInAge < 0f)
            {
                allInGroup.alpha = 0f;
                if (flashImage != null)
                {
                    flashImage.color = new Color(1f, 0.9f, 0.5f, 0f);
                }

                return;
            }

            _allInAge += dt;
            float u = Mathf.Clamp01(_allInAge / settings.allInPopSeconds);
            float scale = Mathf.LerpUnclamped(0.4f, 1f, EaseOutBack(u));
            // A little wiggle that settles, then a slow gentle breathing swell so the card stays alive to the end.
            float wiggle = 7f * Mathf.Exp(-_allInAge * 3.2f) * Mathf.Sin(_allInAge * 20f);
            scale *= 1f + 0.025f * Mathf.Sin(_allInAge * 3f) * Mathf.Clamp01((_allInAge - 1f) * 0.5f);
            allInGroup.alpha = Mathf.Clamp01(_allInAge / 0.12f);
            allInRoot.localScale = new Vector3(scale, scale, 1f);
            allInRoot.localRotation = Quaternion.Euler(0f, 0f, wiggle);

            if (flashImage != null)
            {
                float a = settings.flashPeak * Mathf.Clamp01(_allInAge / 0.08f) * Mathf.Exp(-Mathf.Max(0f, _allInAge - 0.08f) * 3.5f);
                // A softer second pulse a beat later.
                a += settings.flashPeak * 0.5f * Mathf.Exp(-Mathf.Abs(_allInAge - 0.7f) * 9f);
                flashImage.color = new Color(1f, 0.9f, 0.5f, Mathf.Clamp01(a));
            }
        }

        // Sizes and places the top-centre cards from the canvas width: between the corner chips when there is room (16:9),
        // otherwise just below the chip row at a width that fits. Nothing goes below about 40% of the height.
        void Layout()
        {
            var canvasRect = transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            float width = canvasRect.rect.width;
            if (!_layoutDirty && Mathf.Abs(width - _layoutWidth) < 0.5f)
            {
                return;
            }

            _layoutDirty = false;
            _layoutWidth = width;
            float free = width - 2f * (40f + 280f + 20f);
            bool wide = free >= 760f;
            float maxWidth = wide ? free : width - 80f;
            float top = wide ? 40f : 240f;

            Fit(questionRoot, questionText, Mathf.Min(1200f, maxWidth), top);
            Fit(subtitleRoot, subtitleText, Mathf.Min(1100f, maxWidth), top);
            Fit(timesUpRoot, timesUpText, Mathf.Min(1000f, maxWidth), top);
            Fit(allInRoot, allInText, Mathf.Min(1000f, maxWidth), top);
        }

        static void Fit(RectTransform root, TMP_Text text, float maxWidth, float top)
        {
            if (root == null || text == null)
            {
                return;
            }

            float inner = maxWidth - 56f;
            Vector2 preferred = text.GetPreferredValues(text.text, inner, 0f);
            float w = Mathf.Min(maxWidth, preferred.x + 56f);
            float h = preferred.y + 28f;
            root.sizeDelta = new Vector2(w, h);
            root.anchoredPosition = new Vector2(0f, -(top + h * 0.5f));
        }

        string QuestionFormat()
        {
            string f = sim != null && sim.Config != null ? sim.Config.questionFormat : null;
            return string.IsNullOrEmpty(f) ? settings.fallbackQuestionFormat : f;
        }

        string AllInFormat()
        {
            string f = sim != null && sim.Config != null ? sim.Config.allInFormat : null;
            return string.IsNullOrEmpty(f) ? settings.fallbackAllInFormat : f;
        }

        string ResultFormat()
        {
            string f = sim != null && sim.Config != null ? sim.Config.resultFormat : null;
            return string.IsNullOrEmpty(f) ? settings.fallbackResultFormat : f;
        }

        /// <summary>"5 MINUTES", "1 MINUTE", "1 MINUTE 30 SECONDS" or "45 SECONDS".</summary>
        public static string DurationLabel(float seconds)
        {
            int total = Mathf.Max(1, Mathf.RoundToInt(seconds));
            if (total < 60)
            {
                return total.ToString(Invariant) + (total == 1 ? " SECOND" : " SECONDS");
            }

            int m = total / 60;
            int s = total % 60;
            string label = m.ToString(Invariant) + (m == 1 ? " MINUTE" : " MINUTES");
            return s == 0 ? label : label + " " + s.ToString(Invariant) + (s == 1 ? " SECOND" : " SECONDS");
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
