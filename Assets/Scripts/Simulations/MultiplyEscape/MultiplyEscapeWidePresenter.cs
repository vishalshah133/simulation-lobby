using System.Collections.Generic;
using SimulationLobby.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.MultiplyEscape
{
    /// <summary>
    /// comp-001 #10 binder: pushes IN / OUT numbers, the result caption, the two-colour swarm and the two
    /// melodies from run state. Reads in <c>LateUpdate</c>, never writes to the run. The right-margin counter
    /// is the card presenter's; the left-margin OUT panel is built here because the card has one counter slot.
    /// </summary>
    [RequireComponent(typeof(MultiplyEscapeSimulation))]
    public sealed class MultiplyEscapeWidePresenter : MonoBehaviour
    {
        [SerializeField] MultiplyEscapeSimulation _simulation;
        [SerializeField] SegmentCardPresenter _card;
        [SerializeField] BounceMelodyPlayer _cloneAudio;
        [SerializeField] BounceMelodyPlayer _escapeAudio;
        [SerializeField] TMP_Text _outText;

        [Header("Palette (finale: dark frame, two clear colours)")]
        public Color insideColor = new Color(0.2f, 0.85f, 1f, 1f);
        public Color outsideColor = new Color(1f, 0.55f, 0.1f, 1f);

        [Tooltip("Most notes per rendered frame; a swarm makes dozens of clones in one frame.")]
        [Range(1, 6)] [SerializeField] int _maxNotesPerFrame = 2;

        [Tooltip("Seconds the result caption shows before it swaps to the one-line why.")]
        [Min(0.5f)] [SerializeField] float _resultSeconds = 2.2f;

        int _lastBorn;
        int _lastEscaped;
        float _restStart = -1f;

        const string WhyEmpty = "EVERY ESCAPE WIDENS THE EXIT FOR THE NEXT";
        const string WhyFull = "CLONES OUTRAN THE WIDENING EXIT";
        const string RuleLine = "BOUNCE = MAYBE A CLONE   ·   ESCAPE = WIDER EXIT";

        void Reset()
        {
            _simulation = GetComponent<MultiplyEscapeSimulation>();
        }

        void Start()
        {
            if (_card != null)
            {
                _card.SetHook("FILL THE RING WITH 300, OR ALL ESCAPE?");
            }
        }

        void LateUpdate()
        {
            if (_simulation == null)
            {
                return;
            }

            Paint(_simulation.Inside, insideColor);
            Paint(_simulation.Escaping, outsideColor);
            UpdateAudio();
            UpdateHud();
        }

        static void Paint(IReadOnlyList<SwarmBall> balls, Color color)
        {
            for (int i = 0; i < balls.Count; i++)
            {
                SpriteRenderer r = balls[i].Renderer;
                if (r != null && r.color != color)
                {
                    r.color = color;
                }
            }
        }

        void UpdateAudio()
        {
            if (_simulation.BornThisRound < _lastBorn)
            {
                _lastBorn = 0;
                _lastEscaped = 0;
                if (_cloneAudio != null) { _cloneAudio.ResetMelody(); }
                if (_escapeAudio != null) { _escapeAudio.ResetMelody(); }
            }

            int born = _simulation.BornThisRound;
            Play(_cloneAudio, born - _lastBorn);
            _lastBorn = born;

            int escaped = _simulation.EscapedThisRound;
            Play(_escapeAudio, escaped - _lastEscaped);
            _lastEscaped = escaped;
        }

        void Play(BounceMelodyPlayer player, int pending)
        {
            if (player == null || pending <= 0)
            {
                return;
            }

            int notes = Mathf.Min(pending, _maxNotesPerFrame);
            for (int i = 0; i < notes; i++)
            {
                player.PlayNote(1f);
            }
        }

        void UpdateHud()
        {
            int target = _simulation.FillTarget;
            if (_card != null)
            {
                _card.SetCounter($"IN {_simulation.InsideCount} / {target}");
            }

            if (_outText != null)
            {
                string s = $"OUT {_simulation.EscapedThisRound}\n<size=55%><color=#FFFFFF>EXIT {_simulation.GapDegrees:0}°</color></size>";
                if (_outText.text != s)
                {
                    _outText.text = s;
                }
            }

            if (_card == null)
            {
                return;
            }

            if (!_simulation.IsResting)
            {
                _restStart = -1f;
                _card.SetResultCaption(_simulation.IsHolding ? "WILL IT FILL UP, OR EMPTY OUT?" : RunningCaption(target));
                return;
            }

            if (_restStart < 0f)
            {
                _restStart = Time.unscaledTime;
            }

            bool showWhy = Time.unscaledTime - _restStart > _resultSeconds;
            int peak = _simulation.PeakThisRound;
            string caption;
            switch (_simulation.LastOutcome)
            {
                case RoundOutcome.Full:
                    caption = showWhy ? WhyFull : $"FULL! {target} INSIDE";
                    break;
                case RoundOutcome.Empty:
                    string lead = peak >= target * 0.85f ? "SO CLOSE" : "ALL OUT";
                    caption = showWhy ? WhyEmpty : $"{lead}! PEAKED AT {peak} / {target}";
                    break;
                default:
                    caption = "TIME. NO WINNER";
                    break;
            }

            _card.SetResultCaption(caption);
        }

        // Tells the viewer what is winning right now, so the see-saw is readable without sound.
        string RunningCaption(int target)
        {
            int inside = _simulation.InsideCount;
            if (inside >= target * 0.8f)
            {
                return "ALMOST FULL!";
            }

            if (_simulation.PeakThisRound >= target * 0.35f && inside < _simulation.PeakThisRound * 0.6f)
            {
                return "THE EXIT IS WINNING...";
            }

            return RuleLine;
        }

        /// <summary>Assigned by the scene builder; also builds the left-margin OUT panel under the card canvas.</summary>
        public void Bind(MultiplyEscapeSimulation simulation, SegmentCardPresenter card,
                         BounceMelodyPlayer cloneAudio, BounceMelodyPlayer escapeAudio)
        {
            _simulation = simulation;
            _card = card;
            _cloneAudio = cloneAudio;
            _escapeAudio = escapeAudio;
            _outText = BuildOutPanel(card.transform, outsideColor);
        }

        static TMP_Text BuildOutPanel(Transform canvasRoot, Color color)
        {
            var panel = new GameObject("LeftMargin OUT", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasRoot, false);
            panel.transform.SetAsFirstSibling(); // below the full-screen card, like the other zones
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(130f, 40f);
            rect.sizeDelta = new Vector2(250f, 300f);
            var image = panel.GetComponent<Image>();
            image.color = new Color(0.03f, 0.04f, 0.07f, 0.78f);
            image.raycastTarget = false;

            var go = new GameObject("Out", typeof(RectTransform));
            go.transform.SetParent(rect, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = "OUT 0\n<size=55%><color=#FFFFFF>EXIT 0°</color></size>";
            text.fontSize = 72f;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMax = 72f;
            text.fontSizeMin = 36f;
            var tr = text.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(10f, 8f);
            tr.offsetMax = new Vector2(-10f, -8f);
            return text;
        }
    }
}
