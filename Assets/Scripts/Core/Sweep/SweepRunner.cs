using System;
using UnityEngine;

namespace SimulationLobby.Core
{
    /// <summary>
    /// Runs one <see cref="ISimulation"/> once per take of a <see cref="SweepConfig"/>: initialise
    /// with the take's config, hold still for the lead-in, tick for a fixed length, move on. The sweep
    /// counterpart to <see cref="SimulationRunner"/>, and the same rules apply — stepped only from
    /// <c>FixedUpdate</c>, everything counted in ticks.
    /// </summary>
    /// <remarks>
    /// A take ends on the clock, never on the simulation's own finish condition. The rhythm is the
    /// format: five takes of equal length teach the viewer the beat. <see cref="ISimulation.IsComplete"/>
    /// only stops the ticking early (the frame then holds until the clock runs out).
    /// <para>
    /// Every take is a full re-initialise with the same seed, so a take rendered alone
    /// (<see cref="soloTake"/>) is identical to the same take inside the full sweep.
    /// </para>
    /// Presentation reads <see cref="Phase"/>, <see cref="TakeIndex"/> and <see cref="PhaseTick"/>
    /// from <c>LateUpdate</c> rather than subscribing to events, so nothing presentational ever runs
    /// inside the fixed step.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SweepRunner : MonoBehaviour
    {
        public enum SweepPhase
        {
            Idle,
            /// <summary>3-2-1 before the first take. The first take is already initialised and on
            /// screen (so its first-frame costs are paid now), but nothing moves.</summary>
            Countdown,
            /// <summary>Take initialised and captioned, nothing moving yet.</summary>
            LeadIn,
            /// <summary>Take ticking.</summary>
            Running,
            /// <summary>Last take over; holding its final frame.</summary>
            EndHold,
            Done
        }

        [Tooltip("Takes, captions and rhythm.")]
        public SweepConfig sweep;

        [Tooltip("One seed for every take. Varying it per take would add a second variable to a " +
                 "comparison that is meant to have one.")]
        public int seed;

        public bool autoStart = true;

        [Tooltip("Run only this take (0-based) — for re-cutting a single take. -1 runs the whole sweep.")]
        public int soloTake = -1;

        public bool logResults = true;

        ISimulation _simulation;
        float _restoreFixedDeltaTime;
        bool _fixedDeltaOverridden;
        int _lastTake;

        public SweepPhase Phase { get; private set; } = SweepPhase.Idle;

        /// <summary>Take currently on screen (0-based).</summary>
        public int TakeIndex { get; private set; } = -1;

        /// <summary>Ticks spent in the current phase.</summary>
        public int PhaseTick { get; private set; }

        /// <summary>Ticks since the sweep began, lead-ins included.</summary>
        public int TotalTicks { get; private set; }

        /// <summary>Result of each take as it finished. Null entries for takes not yet run.</summary>
        public RunResult[] TakeResults { get; private set; } = Array.Empty<RunResult>();

        public SimulationConfig CurrentConfig =>
            sweep != null && TakeIndex >= 0 && TakeIndex < sweep.TakeCount ? sweep.takes[TakeIndex].config : null;

        public void SetSimulation(ISimulation simulation)
        {
            if (Phase == SweepPhase.LeadIn || Phase == SweepPhase.Running)
            {
                Debug.LogError("[SweepRunner] Cannot swap the simulation mid-sweep.", this);
                return;
            }

            _simulation = simulation;
        }

        void Awake()
        {
            if (_simulation == null)
            {
                _simulation = GetComponent<ISimulation>();
            }

            if (autoStart)
            {
                Begin();
            }
        }

        void OnDestroy()
        {
            RestoreFixedDeltaTime();
        }

        public bool Begin()
        {
            if (_simulation == null)
            {
                Debug.LogError("[SweepRunner] No ISimulation assigned or found on this GameObject.", this);
                return false;
            }

            if (sweep == null)
            {
                Debug.LogError("[SweepRunner] No SweepConfig assigned.", this);
                return false;
            }

            if (!sweep.Validate(out string error))
            {
                Debug.LogError($"[SweepRunner] Sweep '{sweep.name}' is invalid: {error}", sweep);
                return false;
            }

            bool solo = soloTake >= 0 && soloTake < sweep.TakeCount;
            _lastTake = solo ? soloTake : sweep.TakeCount - 1;
            TakeResults = new RunResult[sweep.TakeCount];
            TotalTicks = 0;

            if (!_fixedDeltaOverridden)
            {
                _restoreFixedDeltaTime = Time.fixedDeltaTime;
                _fixedDeltaOverridden = true;
            }

            Time.fixedDeltaTime = sweep.FixedTimestep;

            if (logResults)
            {
                Debug.Log($"[SweepRunner] '{sweep.name}': {sweep.TakeCount} takes, " +
                          $"{sweep.TotalSeconds:0.0}s total, seed {seed}" + (solo ? $", solo take {soloTake}" : ""), this);
            }

            StartTake(solo ? soloTake : 0);
            if (sweep.CountdownTicks > 0)
            {
                // The take is initialised (on screen, warming up) but the clock hasn't started: the
                // countdown runs first, then the take's own lead-in. Nothing is ticked, so the take
                // is identical with or without a countdown.
                Phase = SweepPhase.Countdown;
            }

            return true;
        }

        /// <summary>Whole seconds left on the countdown (3, 2, 1), or 0 outside it. For presentation.</summary>
        public int CountdownRemaining =>
            Phase == SweepPhase.Countdown
                ? Mathf.Max(1, Mathf.CeilToInt((sweep.CountdownTicks - PhaseTick) * sweep.FixedTimestep - 1e-4f))
                : 0;

        void StartTake(int take)
        {
            TakeIndex = take;
            PhaseTick = 0;
            _simulation.Initialize(seed, sweep.takes[take].config);
            Phase = SweepPhase.LeadIn;

            // Lead-in length is keyed off the take's position in the video, so a solo re-render of
            // take 3 holds as long as take 3 does inside the sweep.
            if (sweep.LeadInTicks(take) <= 0)
            {
                Phase = SweepPhase.Running;
            }
        }

        void FixedUpdate()
        {
            switch (Phase)
            {
                case SweepPhase.Countdown:
                    Advance();
                    if (PhaseTick >= sweep.CountdownTicks)
                    {
                        PhaseTick = 0;
                        Phase = sweep.LeadInTicks(TakeIndex) > 0 ? SweepPhase.LeadIn : SweepPhase.Running;
                    }

                    break;

                case SweepPhase.LeadIn:
                    Advance();
                    if (PhaseTick >= sweep.LeadInTicks(TakeIndex))
                    {
                        Phase = SweepPhase.Running;
                        PhaseTick = 0;
                    }

                    break;

                case SweepPhase.Running:
                    if (!_simulation.IsComplete)
                    {
                        try
                        {
                            _simulation.Tick(sweep.FixedTimestep);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception, this);
                            EndTake(CompletionReason.Failed);
                            return;
                        }
                    }

                    Advance();
                    if (PhaseTick >= sweep.TakeTicks)
                    {
                        EndTake(CompletionReason.Finished);
                    }

                    break;

                case SweepPhase.EndHold:
                    Advance();
                    if (PhaseTick >= sweep.EndHoldTicks)
                    {
                        Phase = SweepPhase.Done;
                        RestoreFixedDeltaTime();
                        if (logResults)
                        {
                            Debug.Log($"[SweepRunner] '{sweep.name}' complete: {TotalTicks} ticks " +
                                      $"({TotalTicks * sweep.FixedTimestep:0.00}s).", this);
                        }
                    }

                    break;
            }
        }

        void Advance()
        {
            PhaseTick++;
            TotalTicks++;
        }

        void EndTake(CompletionReason reason)
        {
            RunResult result = _simulation.Result;
            if (result != null)
            {
                if (result.completionReason == CompletionReason.Running)
                {
                    result.MarkStopped(reason, result.tickCount);
                }

                TakeResults[TakeIndex] = result;
                if (logResults)
                {
                    Debug.Log($"[SweepRunner] take {TakeIndex} '{sweep.Caption(TakeIndex)}': {result.ToSummaryLine()}", this);
                }
            }

            if (reason == CompletionReason.Failed || TakeIndex >= _lastTake)
            {
                // The last take's final frame stays on screen: the simulation is not re-initialised.
                Phase = SweepPhase.EndHold;
                PhaseTick = 0;
                return;
            }

            StartTake(TakeIndex + 1);
        }

        void RestoreFixedDeltaTime()
        {
            if (!_fixedDeltaOverridden)
            {
                return;
            }

            Time.fixedDeltaTime = _restoreFixedDeltaTime;
            _fixedDeltaOverridden = false;
        }
    }
}
