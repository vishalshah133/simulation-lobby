using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A camera with three jobs: orbit a point (the setup beat), chase a moving target from behind and
    /// above (the race), or hold a fixed frame on a point (the finish). Format-agnostic: whoever owns the
    /// scene tells it which job and what to look at. Reads the world in <c>LateUpdate</c>, never writes it.
    /// </summary>
    public sealed class ChaseCameraRig : MonoBehaviour
    {
        public enum Mode
        {
            Orbit = 0,
            Chase = 1,
            Hold = 2
        }

        [Header("Chase")]
        [Min(0.5f)] public float chaseDistance = 5.5f;
        [Min(0f)] public float chaseHeight = 2.6f;
        [Tooltip("Sideways offset from the line of travel, so the view is three-quarter and not dead behind.")]
        public float chaseSide = 1.4f;
        [Min(0f)] public float lookAhead = 2.2f;
        [Range(0.02f, 2f)] public float positionSmoothing = 0.35f;
        [Range(0.02f, 2f)] public float headingSmoothing = 0.6f;

        [Header("Orbit")]
        [Min(0.5f)] public float orbitRadius = 7f;
        [Min(0f)] public float orbitHeight = 3f;
        public float orbitDegreesPerSecond = 9f;

        [Header("Look")]
        [Range(0.02f, 1f)] public float lookSmoothing = 0.18f;

        Camera _camera;
        Mode _mode = Mode.Orbit;
        Transform _target;
        Vector3 _focus;
        Vector3 _holdPosition;
        float _orbitAngle = 35f;

        Vector3 _velocity;
        Vector3 _lookVelocity;
        Vector3 _smoothedFocus;
        Vector3 _heading = Vector3.forward;
        Vector3 _lastTargetPosition;
        bool _hasTarget;

        public Mode CurrentMode => _mode;

        public void Bind(Camera camera)
        {
            _camera = camera;
        }

        /// <summary>Slowly circle <paramref name="focus"/>.</summary>
        public void Orbit(Vector3 focus)
        {
            _mode = Mode.Orbit;
            _focus = focus;
        }

        /// <summary>Follow <paramref name="target"/> from behind and above.</summary>
        public void Chase(Transform target)
        {
            _mode = Mode.Chase;
            if (_target != target)
            {
                _target = target;
                _hasTarget = false;
            }
        }

        /// <summary>Stay at <paramref name="position"/> and look at <paramref name="focus"/>.</summary>
        public void Hold(Vector3 position, Vector3 focus)
        {
            _mode = Mode.Hold;
            _holdPosition = position;
            _focus = focus;
        }

        /// <summary>Snap straight to the current mode's frame. Use once at the start, so the first frame is composed.</summary>
        public void Snap()
        {
            ComputeDesired(0f, out Vector3 position, out Vector3 look);
            transform.position = position;
            _smoothedFocus = look;
            transform.rotation = Quaternion.LookRotation((look - position).normalized, Vector3.up);
            _velocity = Vector3.zero;
            _lookVelocity = Vector3.zero;
        }

        void LateUpdate()
        {
            ComputeDesired(Time.deltaTime, out Vector3 position, out Vector3 look);

            transform.position = Vector3.SmoothDamp(transform.position, position, ref _velocity,
                _mode == Mode.Chase ? positionSmoothing : 0.8f);
            _smoothedFocus = Vector3.SmoothDamp(_smoothedFocus, look, ref _lookVelocity, lookSmoothing);

            Vector3 toFocus = _smoothedFocus - transform.position;
            if (toFocus.sqrMagnitude > 1e-4f)
            {
                transform.rotation = Quaternion.LookRotation(toFocus.normalized, Vector3.up);
            }
        }

        void ComputeDesired(float deltaTime, out Vector3 position, out Vector3 look)
        {
            switch (_mode)
            {
                case Mode.Chase when _target != null:
                    Vector3 targetPosition = _target.position;
                    if (!_hasTarget)
                    {
                        _lastTargetPosition = targetPosition;
                        _hasTarget = true;
                    }

                    // Heading comes from where the target has been going, smoothed, so the camera
                    // swings round a bend instead of snapping to each frame's direction.
                    Vector3 moved = targetPosition - _lastTargetPosition;
                    _lastTargetPosition = targetPosition;
                    moved.y = 0f;
                    if (moved.sqrMagnitude > 1e-6f && deltaTime > 0f)
                    {
                        float blend = 1f - Mathf.Exp(-deltaTime / headingSmoothing);
                        _heading = Vector3.Slerp(_heading, moved.normalized, blend);
                    }

                    Vector3 flat = new Vector3(_heading.x, 0f, _heading.z).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, flat);
                    position = targetPosition - flat * chaseDistance + Vector3.up * chaseHeight + side * chaseSide;
                    look = targetPosition + flat * lookAhead;
                    break;

                case Mode.Hold:
                    position = _holdPosition;
                    look = _focus;
                    break;

                default:
                    _orbitAngle += orbitDegreesPerSecond * deltaTime;
                    float radians = _orbitAngle * Mathf.Deg2Rad;
                    position = _focus + new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * orbitRadius
                               + Vector3.up * orbitHeight;
                    look = _focus;
                    break;
            }
        }
    }
}
