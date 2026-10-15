using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A camera that sits on a home framing with a slow breathing drift, and on request eases in toward
    /// a point of interest, holds, and eases home again. A decaying shake can be layered on top.
    /// Format-agnostic: whoever owns the scene decides what is worth looking at. Reads the world in
    /// <c>LateUpdate</c>, never writes it, and uses only deterministic noise, so a re-render is identical.
    /// </summary>
    public sealed class FocusCameraRig : MonoBehaviour
    {
        [Header("Breathing")]
        [Min(0f)] public float driftAmplitude = 0.35f;
        [Min(0.01f)] public float driftSpeed = 0.11f;

        [Header("Focus")]
        [Tooltip("At full zoom the camera sits this fraction of the home distance from the point.")]
        [Range(0.1f, 0.9f)] public float tightDistanceFraction = 0.35f;
        [Min(0.05f)] public float easeInSeconds = 0.7f;
        [Min(0.05f)] public float easeOutSeconds = 1.0f;

        [Header("Shake")]
        [Min(0f)] public float shakeFrequency = 24f;

        Camera _camera;
        Vector3 _home;
        Vector3 _homeLook;
        bool _hasHome;

        Vector3 _focusPoint;
        float _focusZoom;
        float _holdSeconds;
        float _phaseTimer;
        FocusPhase _phase = FocusPhase.Home;
        float _weight;          // 0 at home, zoom01 when fully focused

        float _shakeAmplitude;
        float _shakeSeconds;
        float _shakeRemaining;

        enum FocusPhase
        {
            Home = 0,
            In = 1,
            Hold = 2,
            Out = 3
        }

        public bool IsFocusing => _phase != FocusPhase.Home;

        public void Bind(Camera camera)
        {
            _camera = camera;
        }

        public void SetHome(Vector3 position, Vector3 lookAt)
        {
            _home = position;
            _homeLook = lookAt;
            _hasHome = true;
            Pose(0f, Time.time);
        }

        /// <summary>Ease toward <paramref name="worldPoint"/> by <paramref name="zoom01"/> (0 = home, 1 = tight), hold, then ease home.</summary>
        public void Focus(Vector3 worldPoint, float zoom01, float holdSeconds)
        {
            _focusPoint = worldPoint;
            _focusZoom = Mathf.Clamp01(zoom01);
            _holdSeconds = Mathf.Max(0f, holdSeconds);
            _phaseTimer = 0f;
            // Re-targeting mid-focus restarts the ease-in from wherever the camera currently is.
            _phase = FocusPhase.In;
            _inStart = _weight;
        }

        public void Shake(float amplitude, float seconds)
        {
            // A new shake only replaces the current one if it is at least as strong right now.
            float current = _shakeSeconds > 0f ? _shakeAmplitude * (_shakeRemaining / _shakeSeconds) : 0f;
            if (amplitude < current)
            {
                return;
            }

            _shakeAmplitude = amplitude;
            _shakeSeconds = Mathf.Max(0.01f, seconds);
            _shakeRemaining = _shakeSeconds;
        }

        float _inStart;

        void LateUpdate()
        {
            if (!_hasHome)
            {
                return;
            }

            float dt = Time.deltaTime;
            switch (_phase)
            {
                case FocusPhase.In:
                    _phaseTimer += dt;
                    float u = Mathf.Clamp01(_phaseTimer / easeInSeconds);
                    _weight = Mathf.Lerp(_inStart, _focusZoom, Smooth(u));
                    if (u >= 1f)
                    {
                        _phase = FocusPhase.Hold;
                        _phaseTimer = 0f;
                    }

                    break;

                case FocusPhase.Hold:
                    _phaseTimer += dt;
                    if (_phaseTimer >= _holdSeconds)
                    {
                        _phase = FocusPhase.Out;
                        _phaseTimer = 0f;
                        _outStart = _weight;
                    }

                    break;

                case FocusPhase.Out:
                    _phaseTimer += dt;
                    float v = Mathf.Clamp01(_phaseTimer / easeOutSeconds);
                    _weight = Mathf.Lerp(_outStart, 0f, Smooth(v));
                    if (v >= 1f)
                    {
                        _phase = FocusPhase.Home;
                        _weight = 0f;
                    }

                    break;
            }

            Pose(dt, Time.time);
        }

        float _outStart;

        void Pose(float dt, float time)
        {
            Transform target = _camera != null ? _camera.transform : transform;

            Vector3 position = _home;
            Vector3 look = _homeLook;

            if (_weight > 0f)
            {
                Vector3 tight = _focusPoint + (_home - _focusPoint) * tightDistanceFraction;
                position = Vector3.Lerp(_home, tight, _weight);
                look = Vector3.Lerp(_homeLook, _focusPoint, _weight);
            }

            // Breathing: slow, smooth, and phase-offset per axis. Perlin on time, never UnityEngine.Random.
            float t = time * driftSpeed;
            position += new Vector3(
                (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(t, 7.1f) - 0.5f) * 2f * 0.6f,
                (Mathf.PerlinNoise(t, 13.7f) - 0.5f) * 2f) * driftAmplitude;

            if (_shakeRemaining > 0f)
            {
                _shakeRemaining = Mathf.Max(0f, _shakeRemaining - dt);
                float decay = _shakeRemaining / _shakeSeconds;
                float s = time * shakeFrequency;
                Vector3 jolt = new Vector3(
                    Mathf.PerlinNoise(s, 21.3f) - 0.5f,
                    Mathf.PerlinNoise(s, 34.9f) - 0.5f,
                    Mathf.PerlinNoise(s, 48.2f) - 0.5f) * 2f;
                position += jolt * (_shakeAmplitude * decay * decay);
            }

            target.position = position;
            Vector3 forward = look - position;
            if (forward.sqrMagnitude > 1e-6f)
            {
                target.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
        }

        static float Smooth(float u)
        {
            return u * u * (3f - 2f * u);
        }
    }
}
