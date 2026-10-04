using TMPro;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Canvas scoreboard for a two-sided contest: a question and a <c>left vs right</c> score in the
    /// top panel, and the current round's result and counter in the bottom panel.
    /// </summary>
    /// <remarks>
    /// Format-agnostic — it knows "left side", "right side" and "a question", not walls or balls, so
    /// any threshold format can drive it. Strictly read-only over simulation state: it is handed
    /// values and never queries or mutates a run.
    /// <para>
    /// Built on uGUI rather than world-space text so the layout is resolution-independent and reads
    /// correctly at both 1080x1920 and 1920x1080 without re-placing anything by hand.
    /// </para>
    /// </remarks>
    public sealed class VersusScoreboardHud : MonoBehaviour
    {
        [Header("Top panel — title, question and score")]
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _questionText;
        [SerializeField] TMP_Text _leftLabel;
        [SerializeField] TMP_Text _leftScore;
        [SerializeField] TMP_Text _rightLabel;
        [SerializeField] TMP_Text _rightScore;

        [Header("Bottom panel — round result and counter")]
        [SerializeField] TMP_Text _roundText;
        [SerializeField] TMP_Text _counterText;
        [SerializeField] TMP_Text _resultText;

        [Header("Result colours")]
        public Color successColor = new Color(0.35f, 1f, 0.55f, 1f);
        public Color failureColor = new Color(1f, 0.35f, 0.42f, 1f);
        public Color neutralColor = new Color(0.85f, 0.87f, 0.95f, 1f);

        /// <summary>Assigned by a scene builder, so construction needs no serialized wiring.</summary>
        public void Bind(TMP_Text title, TMP_Text question, TMP_Text leftLabel, TMP_Text leftScore,
            TMP_Text rightLabel, TMP_Text rightScore, TMP_Text round, TMP_Text counter, TMP_Text result)
        {
            _titleText = title;
            _questionText = question;
            _leftLabel = leftLabel;
            _leftScore = leftScore;
            _rightLabel = rightLabel;
            _rightScore = rightScore;
            _roundText = round;
            _counterText = counter;
            _resultText = result;
        }

        /// <summary>The matchup headline, e.g. "WALL VS BALL". Set once.</summary>
        public void SetTitle(string title)
        {
            if (_titleText != null)
            {
                _titleText.text = title;
            }
        }

        /// <summary>The hook, stated as a question — set once at the start of a run.</summary>
        public void SetQuestion(string question)
        {
            if (_questionText != null)
            {
                _questionText.text = question;
            }
        }

        /// <summary>Names of the two sides, e.g. "WALL" and "BALL". Set once.</summary>
        public void SetSides(string leftName, string rightName)
        {
            if (_leftLabel != null)
            {
                _leftLabel.text = leftName;
            }

            if (_rightLabel != null)
            {
                _rightLabel.text = rightName;
            }
        }

        public void SetScore(int left, int right)
        {
            if (_leftScore != null)
            {
                _leftScore.text = left.ToString();
            }

            if (_rightScore != null)
            {
                _rightScore.text = right.ToString();
            }
        }

        public void SetRound(int current, int total)
        {
            if (_roundText != null)
            {
                _roundText.text = $"ROUND {current} / {total}";
            }
        }

        /// <summary>
        /// Free-text round line, for runs with no known total — e.g. "HIT 7" when the number of
        /// rounds is the answer the viewer is guessing and printing it would give the game away.
        /// </summary>
        public void SetRoundLabel(string text)
        {
            if (_roundText != null)
            {
                _roundText.text = text ?? string.Empty;
            }
        }

        /// <summary>The per-round number, e.g. bounce count.</summary>
        public void SetCounter(string label, int value)
        {
            if (_counterText != null)
            {
                _counterText.text = $"{value}  {label}";
            }
        }

        /// <summary>
        /// Free-text counter line, e.g. a countdown "0:07". <paramref name="alert"/> switches it to the
        /// failure colour, for the last seconds of a clock.
        /// </summary>
        public void SetCounterText(string text, bool alert = false)
        {
            if (_counterText == null)
            {
                return;
            }

            if (!_counterColorCaptured)
            {
                _counterBaseColor = _counterText.color;
                _counterColorCaptured = true;
            }

            _counterText.text = text ?? string.Empty;
            _counterText.color = alert ? failureColor : _counterBaseColor;
        }

        Color _counterBaseColor;
        bool _counterColorCaptured;

        /// <summary>Flash a round's outcome. Pass null to clear it.</summary>
        public void ShowResult(string text, bool success)
        {
            if (_resultText == null)
            {
                return;
            }

            _resultText.text = text ?? string.Empty;
            _resultText.color = string.IsNullOrEmpty(text)
                ? neutralColor
                : success ? successColor : failureColor;
        }
    }
}
