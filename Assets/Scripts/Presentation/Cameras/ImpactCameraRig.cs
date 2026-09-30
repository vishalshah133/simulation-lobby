using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Camera motion for a wave-based spectacle format: a slow push toward the action as a run
    /// progresses, plus a decaying shake punched on demand. Read-only over the simulation — it is
    /// told "a big hit happened, strength 0.8", never asked what hit what.
    /// </summary>
    /// <remarks>
    /// Runs in <c>LateUpdate</c> so it moves the camera after everything else has finished moving,
    /// and it offsets a <i>child</i> transform: the rig's own position stays the authored framing and
    /// the camera underneath carries only the offset, so the scene builder's framing math never has
    /// to account for accumulated shake.
    /// </remarks>
    public sealed class ImpactCameraRig : MonoBehaviour
    {
        [Header("Push-in")]
        [Tooltip("How far, in world units, the camera creeps toward its look target across a full run. " +
                 "Small is the point: it should read as tension, not as a zoom.")]
        [Min(0f)] public float pushInDistance = 6f;

        [Tooltip("Seconds of unscaled time the push takes to catch up to a new progress value.")]
        [Range(0.05f, 5f)] public float pushSmoothing = 1.5f;

        [Header("Shake")]
        [Tooltip("World-unit amplitude of a full-strength shake.")]
        [Min(0f)] public float shakeAmplitude = 0.45f;

        [Tooltip("How fast a shake decays, per second. Higher = snappier.")]
        [Min(0.1f)] public float shakeDecay = 4.5f;

        [Tooltip("Shake oscillation frequency in Hz.")]
        [Min(0.1f)] public float shakeFrequency = 22f;

        [Header("Wiring")]
        [Tooltip("Transform that carries the offset — normally the camera, parented under this rig.")]
        [SerializeField] Transform _camera;

        [Tooltip("Point the push-in moves toward. Normally the field centre.")]
        [SerializeField] Transform _lookTarget;

        Vector3 _restPosition;
        float _shake;
        float _shakePhase;
        float _progress;
        float _smoothedProgress;

        void Awake()
        {
            if (_camera == null && transform.childCount > 0)
            {
                _camera = transform.GetChild(0);
            }

            if (_camera != null)
            {
                _restPosition = _camera.localPosition;
            }
        }

        /// <summary>Assigned by a scene builder, so nothing needs hand-wiring in the Inspector.</summary>
        public void Bind(Transform cameraTransform, Transform lookTarget)
        {
            _camera = cameraTransform;
            _lookTarget = lookTarget;
            _restPosition = cameraTransform != null ? cameraTransform.localPosition : Vector3.zero;
        }

        /// <summary>How far through the run we are, 0..1. Drives the push-in.</summary>
        public void SetProgress(float progress01)
        {
            _progress = Mathf.Clamp01(progress01);
        }

        /// <summary>Punch a shake. <paramref name="strength01"/> scales <see cref="shakeAmplitude"/>.</summary>
        public void Shake(float strength01)
        {
            // Take the max rather than accumulating: a burst of hits inside one frame should read as
            // one big shake, not multiply into a screen that flies apart.
            _shake = Mathf.Max(_shake, Mathf.Clamp01(strength01));
        }

        void LateUpdate()
        {
            if (_camera == null)
            {
                return;
            }

            // Unscaled, so slow-motion does not also freeze the camera move — the push continuing at
            // normal speed through a slowed impact is a large part of why the moment reads as big.
            float unscaled = Time.unscaledDeltaTime;

            _smoothedProgress = pushSmoothing <= 0f
                ? _progress
                : Mathf.MoveTowards(_smoothedProgress, _progress, unscaled / pushSmoothing);

            Vector3 position = _restPosition;

            if (_lookTarget != null && pushInDistance > 0f)
            {
                Vector3 toTarget = transform.InverseTransformPoint(_lookTarget.position) - _restPosition;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    position += toTarget.normalized * (pushInDistance * _smoothedProgress);
                }
            }

            if (_shake > 0.0001f)
            {
                _shakePhase += unscaled * shakeFrequency;
                float amplitude = shakeAmplitude * _shake * _shake; // squared: weak hits barely register
                position += new Vector3(
                    Mathf.Sin(_shakePhase * 2.17f),
                    Mathf.Sin(_shakePhase * 3.11f + 1.3f),
                    0f) * amplitude;

                _shake = Mathf.Max(0f, _shake - shakeDecay * unscaled);
            }

            _camera.localPosition = position;
        }
    }
}
