using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Tracks where one scheduled event is in its life: Stage 0 = not active, 1 = PreQuiet, 2 = Sting, 3 = Aftermath.
    /// <see cref="SinceSting"/> is the seconds since the sting began (continues through Aftermath), -1 before it.
    /// Reads only. Call <see cref="Update"/> once per frame.
    /// </summary>
    public sealed class EventStage
    {
        float stingLength;
        public int Stage;
        public float T;
        public float SinceSting = -1f;

        public void Reset()
        {
            stingLength = 0f;
            Stage = 0;
            T = 0f;
            SinceSting = -1f;
        }

        public void Update(ICoasterState s, int index)
        {
            if (index >= 0 && s.Phase != ScarePhase.Idle && s.CurrentEventIndex == index)
            {
                Stage = (int)s.Phase;
                T = s.PhaseTime;
                if (Stage == 2)
                {
                    stingLength = Mathf.Max(stingLength, T);
                    SinceSting = T;
                }
                else if (Stage == 3)
                {
                    SinceSting = stingLength + T;
                }
                else
                {
                    SinceSting = -1f;
                    stingLength = 0f;
                }
            }
            else
            {
                Stage = 0;
                T = 0f;
                SinceSting = -1f;
                if (index < 0 || s.NextEventIndex <= index)
                {
                    stingLength = 0f;
                }
            }
        }
    }
}
