using System;
using UnityEngine;

namespace SimulationLobby.Shared
{
    public enum SlotEffectKind
    {
        Multiply = 0,
        Add = 1,
        Release = 2,
        Bonus = 3,
    }

    /// <summary>
    /// What landing in a board slot does to a team's count. Data only: the format decides how each
    /// kind is applied; this just names it and labels it for the screen.
    /// </summary>
    [Serializable]
    public struct SlotEffect
    {
        public SlotEffectKind kind;

        [Min(0)]
        public int value;

        public SlotEffect(SlotEffectKind kind, int value = 0)
        {
            this.kind = kind;
            this.value = Mathf.Max(0, value);
        }

        /// <summary>Short on-screen text: "x2" (multiplication sign), "+25", "R", "SPIN".</summary>
        public string Label
        {
            get
            {
                switch (kind)
                {
                    case SlotEffectKind.Multiply: return "×" + value;
                    case SlotEffectKind.Add: return "+" + value;
                    case SlotEffectKind.Release: return "R";
                    default: return "SPIN";
                }
            }
        }

        public static SlotEffect Multiply(int n) { return new SlotEffect(SlotEffectKind.Multiply, n); }

        public static SlotEffect Add(int n) { return new SlotEffect(SlotEffectKind.Add, n); }

        public static SlotEffect Release() { return new SlotEffect(SlotEffectKind.Release); }

        public static SlotEffect Bonus() { return new SlotEffect(SlotEffectKind.Bonus); }
    }
}
