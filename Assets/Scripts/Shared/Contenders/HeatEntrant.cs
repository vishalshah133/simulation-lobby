using System;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// One named competitor as an orchestrator hands it to a heat: a name, a colour and the slot it holds in the
    /// orchestrator's own roster. The heat keeps <see cref="rosterIndex"/> on the body's identity so results map back.
    /// </summary>
    [Serializable]
    public struct HeatEntrant
    {
        public string name;
        public Color color;
        public int rosterIndex;

        public HeatEntrant(string name, Color color, int rosterIndex)
        {
            this.name = name;
            this.color = color;
            this.rosterIndex = rosterIndex;
        }
    }
}
