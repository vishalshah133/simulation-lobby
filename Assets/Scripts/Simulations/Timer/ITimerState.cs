using UnityEngine;

namespace SimulationLobby.Simulations.Timer
{
    /// <summary>Setup hold, the stream running, the 0:00 finale, then done.</summary>
    public enum TimerPhase { Setup, Running, Finale, Done }

    /// <summary>
    /// Read-only view of a marble-run timer for Presentation (HUD, camera, audio). Presentation reads this and never writes the
    /// simulation. The clock is exact by construction: <see cref="Remaining"/> reaches 0 at exactly Duration simulated seconds
    /// after release, whatever the marbles are doing. The marbles are the show: a hopper releases a steady stream down the course
    /// into a glass collector, which is close to full at 0:00.
    /// </summary>
    public interface ITimerState
    {
        float Duration { get; }          // seconds, from config
        float Elapsed { get; }           // simulated seconds since release, clamped to 0..Duration
        float Remaining { get; }         // Duration - Elapsed, >= 0
        float SetupRemaining { get; }    // seconds left in the setup hold, 0 once released
        bool Released { get; }
        bool IsComplete { get; }
        TimerPhase Phase { get; }

        int TotalMarbles { get; }        // pool size
        int ReleasedCount { get; }
        int CollectedCount { get; }      // crossed the finish line into the collector
        int OnTrackCount { get; }        // released and not yet collected
        float Fill { get; }              // CollectedCount / TotalMarbles, 0..1

        Vector3 MarblePosition(int i);
        bool MarbleOnTrack(int i);
        Color MarbleColor(int i);

        float StreamHeadProgress { get; } // 0..1 along the course: the furthest-along marble still on the track
        float StreamTailProgress { get; } // 0..1: the least-along marble still on the track
        Vector3 StreamHead { get; }
        Vector3 StreamTail { get; }
    }
}
