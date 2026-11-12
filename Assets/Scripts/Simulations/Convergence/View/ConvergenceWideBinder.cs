using SimulationLobby.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.Convergence
{
    /// <summary>
    /// Wide (16:9) HUD binder for the <c>conv</c> format: pushes the live counter and the result caption
    /// into the format-agnostic <see cref="SegmentCardPresenter"/>. Read-only; mirrors the caption logic
    /// of <see cref="ConvergencePresenter"/> (which runs with no portrait HUD in wide scenes).
    /// </summary>
    [RequireComponent(typeof(ConvergenceSimulation))]
    public sealed class ConvergenceWideBinder : MonoBehaviour
    {
        [SerializeField] ConvergenceSimulation _simulation;
        [SerializeField] SegmentCardPresenter _card;

        [Tooltip("Background plate of the counter; hidden while the counter text is empty.")]
        [SerializeField] Image _counterPlate;

        void Reset()
        {
            _simulation = GetComponent<ConvergenceSimulation>();
        }

        public void Bind(ConvergenceSimulation simulation, SegmentCardPresenter card, Image counterPlate)
        {
            _simulation = simulation;
            _card = card;
            _counterPlate = counterPlate;
        }

        void LateUpdate()
        {
            if (_simulation == null || _card == null || _simulation.Config == null)
            {
                return;
            }

            ConvergenceConfig config = _simulation.Config;
            ConvergencePhase phase = _simulation.Phase;

            string counter = string.Empty;
            if (phase == ConvergencePhase.Running || phase == ConvergencePhase.Result)
            {
                counter = config.counterLabel + "  " + _simulation.DisplayCount + " / " + _simulation.RoundBallCount;
            }

            _card.SetCounter(counter);
            if (_counterPlate != null && _counterPlate.enabled != (counter.Length > 0))
            {
                _counterPlate.enabled = counter.Length > 0;
            }

            _card.SetResultCaption(Caption(config, phase));
        }

        string Caption(ConvergenceConfig config, ConvergencePhase phase)
        {
            ConvergenceRoundInfo round = _simulation.CurrentRound;
            switch (phase)
            {
                case ConvergencePhase.Hold:
                    return string.IsNullOrEmpty(config.holdCaption) ? round.caption : config.holdCaption;
                case ConvergencePhase.Transition:
                case ConvergencePhase.Running:
                case ConvergencePhase.Result:
                    bool showResult = phase == ConvergencePhase.Result ||
                                      (phase == ConvergencePhase.Running && _simulation.HasVerdict);
                    if (showResult)
                    {
                        return (round.result ?? string.Empty)
                            .Replace("{n}", _simulation.VerdictCount.ToString())
                            .Replace("{total}", _simulation.RoundBallCount.ToString());
                    }

                    return round.caption;
                case ConvergencePhase.EndCard:
                    return config.endCard;
                default:
                    return string.Empty;
            }
        }
    }
}
