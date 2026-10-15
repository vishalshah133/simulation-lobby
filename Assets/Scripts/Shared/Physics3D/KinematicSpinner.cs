using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// A kinematic body that turns at a constant rate about one of its own axes. Its angle is a pure
    /// function of the time the simulation hands it, so a spinner is reproducible from the seed: the
    /// simulation calls <see cref="Step"/> from its fixed tick and nothing here reads the clock.
    /// </summary>
    /// <remarks>
    /// Moved with <c>MoveRotation</c> rather than by setting the transform, so Unity derives the body's
    /// angular velocity and a marble that hits an arm is struck by a moving object, not a teleporting one.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KinematicSpinner : MonoBehaviour
    {
        [Tooltip("Axis of rotation in the spinner's own space.")]
        public Vector3 localAxis = Vector3.up;

        [Tooltip("Degrees per second. Negative turns the other way.")]
        public float degreesPerSecond = 90f;

        Rigidbody _body;
        Quaternion _restRotation;
        float _phaseDegrees;
        bool _captured;

        void Awake()
        {
            Capture();
        }

        void Capture()
        {
            if (_captured)
            {
                return;
            }

            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _restRotation = transform.rotation;
            _captured = true;
        }

        /// <summary>Set the starting angle. Called once from the simulation's Initialize, with a seeded value.</summary>
        public void SetPhase(float degrees)
        {
            Capture();
            _phaseDegrees = degrees;
            _body.rotation = _restRotation * Quaternion.AngleAxis(_phaseDegrees, localAxis);
        }

        /// <summary>Put the spinner at its angle for <paramref name="seconds"/> of simulated time.</summary>
        public void Step(float seconds)
        {
            Capture();
            float angle = _phaseDegrees + degreesPerSecond * seconds;
            _body.MoveRotation(_restRotation * Quaternion.AngleAxis(angle, localAxis));
        }
    }
}
