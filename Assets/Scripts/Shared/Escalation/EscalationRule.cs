using System;
using UnityEngine;

namespace SimulationLobby.Shared
{
    public enum EscalationMode
    {
        /// <summary>value += magnitude per step.</summary>
        Additive = 0,

        /// <summary>value *= magnitude per step. Far more aggressive than it looks — ×1.05 is 10× in under 50 steps.</summary>
        Multiplicative = 1
    }

    /// <summary>
    /// Config block for an escalation. Lives inside a format's config asset, so an escalation variant
    /// is a new asset rather than new code.
    /// </summary>
    [Serializable]
    public class EscalationSettings
    {
        [Tooltip("Additive adds magnitude per step; Multiplicative multiplies by it.")]
        public EscalationMode mode = EscalationMode.Multiplicative;

        [Tooltip("Per-step amount. Multiplicative wants values just above 1 — 1.03 is already aggressive.")]
        public float magnitude = 1.03f;

        [Tooltip("Starting value for the driven property.")]
        public float startValue = 1f;

        [Tooltip("Hard ceiling. Also what stops escalation outrunning the physics solver.")]
        public float maxValue = 10f;

        [Tooltip("Hard floor, for escalations that shrink rather than grow.")]
        public float minValue = 0.01f;

        /// <summary>
        /// Copy, for when a simulation needs to rescale these values into world units at Initialize.
        /// Mandatory: these live on a <see cref="ScriptableObject"/> asset, and writing to them
        /// directly would edit the config on disk and silently change every future run of that video.
        /// </summary>
        public EscalationSettings Clone()
        {
            return new EscalationSettings
            {
                mode = mode,
                magnitude = magnitude,
                startValue = startValue,
                maxValue = maxValue,
                minValue = minValue
            };
        }
    }

    /// <summary>
    /// Applies an <see cref="EscalationSettings"/> to an <see cref="IEscalationTarget"/>, one step per
    /// trigger. Drives the monotonic tension that threshold formats are built on.
    /// </summary>
    /// <remarks>
    /// Deliberately a plain class, not a MonoBehaviour with its own update: the simulation calls
    /// <see cref="Step"/> from its fixed tick, which keeps escalation inside the deterministic path and
    /// makes "grow after the bounce resolves" the caller's explicit choice rather than a callback race.
    /// </remarks>
    public sealed class EscalationRule
    {
        readonly EscalationSettings _settings;
        readonly IEscalationTarget _target;

        /// <summary>Fires after each applied step: (stepCount, newValue). HUD and audio observe this.</summary>
        public event Action<int, float> Escalated;

        /// <summary>How many steps have been applied — the number the HUD usually shows.</summary>
        public int StepCount { get; private set; }

        /// <summary>Current driven value.</summary>
        public float CurrentValue { get; private set; }

        /// <summary>True once the value has reached its clamp and can no longer move.</summary>
        public bool IsClamped { get; private set; }

        public string TargetName => _target.TargetName;

        public EscalationRule(EscalationSettings settings, IEscalationTarget target)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _target = target ?? throw new ArgumentNullException(nameof(target));

            CurrentValue = Mathf.Clamp(settings.startValue, settings.minValue, settings.maxValue);
            _target.SetValue(CurrentValue);
        }

        /// <summary>
        /// Apply exactly one escalation step. Call once per *collision*, not once per contact point —
        /// a single bounce reporting several contacts is the classic way to double the curve.
        /// Returns false if the value was already clamped, so callers can detect "can't grow further".
        /// </summary>
        public bool Step()
        {
            if (IsClamped)
            {
                return false;
            }

            float next = _settings.mode == EscalationMode.Multiplicative
                ? CurrentValue * _settings.magnitude
                : CurrentValue + _settings.magnitude;

            float clamped = Mathf.Clamp(next, _settings.minValue, _settings.maxValue);
            if (!Mathf.Approximately(clamped, next))
            {
                IsClamped = true;
            }

            CurrentValue = clamped;
            StepCount++;
            _target.SetValue(CurrentValue);
            Escalated?.Invoke(StepCount, CurrentValue);
            return true;
        }
    }
}
