using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// The payoff of a "guess the song" video. When the run completes, the HUD question becomes the
    /// song's title and the melody plays out the rest of its line.
    /// </summary>
    /// <remarks>
    /// Format-agnostic: it watches <see cref="ISimulation.IsComplete"/>, so any format with a bounce
    /// melody gets the guess-the-song wrapper by adding this component. Format code doesn't change.
    /// Reads the run, never writes it.
    /// </remarks>
    public sealed class SongReveal : MonoBehaviour
    {
        [Tooltip("Any component implementing ISimulation.")]
        [SerializeField] MonoBehaviour _simulation;
        [SerializeField] VersusScoreboardHud _hud;
        [SerializeField] BounceMelodyPlayer _melody;

        ISimulation _run;
        bool _revealed;

        void Awake()
        {
            _run = _simulation as ISimulation;
            if (_simulation != null && _run == null)
            {
                Debug.LogError($"[SongReveal] {_simulation.GetType().Name} does not implement ISimulation.", this);
            }
        }

        void LateUpdate()
        {
            if (_revealed || _run == null || !_run.IsComplete)
            {
                return;
            }

            _revealed = true;

            if (_melody == null || _melody.melody == null)
            {
                return;
            }

            if (_hud != null)
            {
                _hud.SetQuestion(_melody.melody.songTitle);
            }

            _melody.FinishPhrase();
        }

        /// <summary>Assigned by a scene builder.</summary>
        public void Bind(MonoBehaviour simulation, VersusScoreboardHud hud, BounceMelodyPlayer melody)
        {
            _simulation = simulation;
            _hud = hud;
            _melody = melody;
        }
    }
}
