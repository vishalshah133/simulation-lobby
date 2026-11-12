using System.Globalization;
using System.Text;
using SimulationLobby.Presentation;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.BellCurve
{
    /// <summary>
    /// 16:9 HUD binder for <c>bell</c> (comp-001). Reads the run in <c>LateUpdate</c> and pushes strings into the
    /// format-agnostic <see cref="SegmentCardPresenter"/> (counter in the right margin, result caption in the bottom
    /// strip) plus two small right-margin readouts: the results ladder and the fitted-curve numbers. Never writes
    /// to the simulation. The canvas pieces are built by the editor builder and handed over through <see cref="Bind"/>.
    /// </summary>
    [RequireComponent(typeof(BellCurveSimulation))]
    public sealed class BellWideHud : MonoBehaviour
    {
        [SerializeField] BellCurveSimulation _simulation;
        [SerializeField] SegmentCardPresenter _card;
        [SerializeField] TMP_Text _readoutText;
        [SerializeField] CanvasGroup _readoutGroup;
        [SerializeField] TMP_Text _ladderText;
        [SerializeField] TMP_Text _noteText;
        [SerializeField] CanvasGroup _ladderGroup;
        [SerializeField] Color _resultTint = new Color(1f, 0.82f, 0.25f, 1f);

        [Tooltip("Shown under the ladder on the end card: the one-line why.")]
        [SerializeField] string _whyLine = "WHY: MANY RANDOM LEFT/RIGHT STEPS ADD UP TO A BELL (CENTRAL LIMIT THEOREM)";

        readonly StringBuilder _sb = new StringBuilder(128);
        string _lastLadder;
        string _lastReadout;

        public void Bind(BellCurveSimulation simulation, SegmentCardPresenter card, TMP_Text readout, CanvasGroup readoutGroup,
                         TMP_Text ladder, TMP_Text note, CanvasGroup ladderGroup)
        {
            _simulation = simulation;
            _card = card;
            _readoutText = readout;
            _readoutGroup = readoutGroup;
            _ladderText = ladder;
            _noteText = note;
            _ladderGroup = ladderGroup;
        }

        void Reset()
        {
            _simulation = GetComponent<BellCurveSimulation>();
        }

        void LateUpdate()
        {
            if (_simulation == null || _simulation.Config == null)
            {
                return;
            }

            BellCurveConfig config = _simulation.Config;
            BellPhase phase = _simulation.Phase;
            BellRound round = _simulation.CurrentRound;

            if (_card != null)
            {
                bool counting = phase == BellPhase.Run || phase == BellPhase.Result || phase == BellPhase.EndCard || phase == BellPhase.Done;
                int count = counting ? _simulation.LandedCount : 0;
                if (phase == BellPhase.EndCard || phase == BellPhase.Done)
                {
                    count = _simulation.MarbleCount;
                }

                _card.SetCounter(config.counterLabel + "\n<size=130%>" + count.ToString("N0", CultureInfo.InvariantCulture) + "</size>");
                _card.SetResultCaption(Caption(config, phase, round));
            }

            UpdateLadder(config, phase);
            UpdateReadout(config, phase);
        }

        string Caption(BellCurveConfig config, BellPhase phase, BellRound round)
        {
            switch (phase)
            {
                case BellPhase.Hold:
                    return !string.IsNullOrEmpty(round.holdCaption) ? round.holdCaption : config.holdCaption;
                case BellPhase.Run:
                case BellPhase.Transition:
                    return round.caption;
                case BellPhase.Result:
                    return Tint(round.result);
                case BellPhase.EndCard:
                case BellPhase.Done:
                    return Tint(config.endCard);
                default:
                    return string.Empty;
            }
        }

        void UpdateLadder(BellCurveConfig config, BellPhase phase)
        {
            if (_ladderText == null)
            {
                return;
            }

            var rows = _simulation.CompletedRows;
            _sb.Length = 0;
            if (rows.Count > 0)
            {
                _sb.Append("RESULTS");
                for (int i = 0; i < rows.Count; i++)
                {
                    _sb.Append('\n').Append(Tint(rows[i]));
                }
            }

            string text = _sb.ToString();
            if (text != _lastLadder)
            {
                _lastLadder = text;
                _ladderText.text = text;
            }

            bool ending = phase == BellPhase.EndCard || phase == BellPhase.Done;
            if (_noteText != null)
            {
                string note = ending ? _whyLine : string.Empty;
                if (_noteText.text != note)
                {
                    _noteText.text = note;
                }
            }

            if (_ladderGroup != null)
            {
                _ladderGroup.alpha = rows.Count > 0 ? 1f : 0f;
            }
        }

        /// <summary>The fitted curve's numbers, read from the pile itself (never from a formula the run did not produce).</summary>
        void UpdateReadout(BellCurveConfig config, BellPhase phase)
        {
            if (_readoutText == null)
            {
                return;
            }

            bool show = _simulation.RoundFinished && (phase == BellPhase.Result || phase == BellPhase.EndCard || phase == BellPhase.Done);
            string text = string.Empty;
            if (show)
            {
                int[] landed = _simulation.Landed;
                int total = _simulation.MarbleCount;
                double mean = 0.0;
                for (int k = 0; k < landed.Length; k++)
                {
                    mean += k * (double)landed[k];
                }

                mean /= System.Math.Max(1, total);
                double variance = 0.0;
                for (int k = 0; k < landed.Length; k++)
                {
                    variance += (k - mean) * (k - mean) * landed[k];
                }

                double sd = System.Math.Sqrt(variance / System.Math.Max(1, total));
                string fair = (config.rows * 0.5f).ToString("0.0", CultureInfo.InvariantCulture);
                text = "FITTED CURVE\ncentre: bin " + mean.ToString("0.0", CultureInfo.InvariantCulture) +
                       "\nspread: " + sd.ToString("0.0", CultureInfo.InvariantCulture) + " bins\nfair board centre: " + fair;
            }

            if (text != _lastReadout)
            {
                _lastReadout = text;
                _readoutText.text = text;
            }

            if (_readoutGroup != null)
            {
                _readoutGroup.alpha = show ? 1f : 0f;
            }
        }

        string Tint(string text)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(_resultTint) + ">" + text + "</color>";
        }
    }
}
