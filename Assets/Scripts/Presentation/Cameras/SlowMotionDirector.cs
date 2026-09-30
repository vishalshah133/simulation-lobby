using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Ramps <see cref="Time.timeScale"/> down and back up, so a format can hold on its biggest
    /// moment instead of letting it flash past in four frames. Format-agnostic: something else
    /// decides *when* a moment is worth slowing; this only knows how to slow it.
    /// </summary>
    /// <remarks>
    /// This does not break determinism, and the distinction matters. A simulation here is stepped in
    /// fixed ticks and <c>Time.fixedDeltaTime</c> is untouched — scaling time changes only how many
    /// wall-clock seconds those ticks are spread across, so a run produces the identical tick
    /// sequence and the identical result, and simply takes longer to watch. It is therefore
    /// presentation under this project's rules even though it writes a global: it writes a
    /// <i>rendering</i> global, never simulation state.
    /// <para>
    /// Restores the scale in <c>OnDisable</c>, because a time scale left at 0.25 leaks into the next
    /// scene and into the editor itself.
    /// </para>
    /// </remarks>
    public sealed class SlowMotionDirector : MonoBehaviour
    {
        [Tooltip("Time scale at full slow-motion.")]
        [Range(0.05f, 1f)] public float slowScale = 0.3f;

        [Tooltip("Seconds of unscaled time to ease into slow-motion.")]
        [Range(0f, 2f)] public float easeInSeconds = 0.12f;

        [Tooltip("Seconds of unscaled time to ease back to full speed once the hold is released.")]
        [Range(0f, 3f)] public float easeOutSeconds = 0.6f;

        float _target = 1f;
        float _current = 1f;

        /// <summary>True while a slow-motion hold is requested.</summary>
        public bool IsSlowed => _target < 1f;

        /// <summary>Request slow-motion. Idempotent — calling it every frame is fine.</summary>
        public void Engage()
        {
            _target = Mathf.Clamp(slowScale, 0.05f, 1f);
        }

        /// <summary>Release back to full speed.</summary>
        public void Release()
        {
            _target = 1f;
        }

        void Update()
        {
            // Unscaled delta: an ease measured in scaled time would itself slow down as the ramp
            // progresses, so the ease-out would crawl exactly when it should be recovering.
            float rampSeconds = _target < _current ? easeInSeconds : easeOutSeconds;

            _current = rampSeconds <= 0f
                ? _target
                : Mathf.MoveTowards(_current, _target, Time.unscaledDeltaTime / rampSeconds);

            Time.timeScale = _current;
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            _current = 1f;
            _target = 1f;
        }
    }
}
