using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// Escalation target that scales a 2D object uniformly — the growing-ball premise.
    /// </summary>
    /// <remarks>
    /// Scales the transform rather than the collider radius so the visual and the collider can never
    /// disagree about how big the ball is. A viewer spotting a ball pass through a gap it visibly did
    /// not fit through is the single most damaging bug this format has.
    /// </remarks>
    public sealed class SizeEscalationTarget2D : IEscalationTarget
    {
        readonly Transform _transform;
        readonly CircleCollider2D _collider;

        public string TargetName => "BallSize";

        public float CurrentValue { get; private set; }

        /// <summary>World-space radius, accounting for the applied scale. What gap checks compare against.</summary>
        public float WorldRadius => _collider.radius * Mathf.Abs(_transform.localScale.x);

        public SizeEscalationTarget2D(Transform transform, CircleCollider2D collider)
        {
            _transform = transform;
            _collider = collider;
            CurrentValue = transform.localScale.x;
        }

        public void SetValue(float value)
        {
            CurrentValue = value;
            _transform.localScale = new Vector3(value, value, 1f);
        }
    }
}
