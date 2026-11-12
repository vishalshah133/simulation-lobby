using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// The departure platform: a skeleton operator pulls the lever just before the cart leaves, watches it go.
    /// Lantern sway and flicker are separate cosmetic components on the lanterns.
    /// </summary>
    public sealed class StationSet : CoasterSet
    {
        public override ActId Act => ActId.Station;

        [Tooltip("Lever pivot; rotates about its local X axis.")]
        public Transform lever;
        [Tooltip("Operator's lever arm pivot (shoulder).")]
        public Transform armPivot;
        public Transform head;
        [Tooltip("Sim time (s) at which the pull starts; set from the config's station dwell.")]
        public float pullStartSeconds = 3.5f;
        public float pullSeconds = 1.0f;

        Quaternion headRest;
        bool haveRest;

        protected override void ResetSet()
        {
            if (head != null)
            {
                if (!haveRest)
                {
                    headRest = head.localRotation;
                    haveRest = true;
                }

                head.localRotation = headRest;
            }

            Apply(0f);
        }

        protected override void Tick()
        {
            float pull = SetUtil.Smooth((State.SimTime - pullStartSeconds) / Mathf.Max(0.1f, pullSeconds));
            Apply(pull);

            // The operator turns to watch the cart leave, within a comfortable neck angle.
            if (head != null && haveRest && State.SimTime > pullStartSeconds)
            {
                Vector3 to = State.CartPosition - head.position;
                if (to.sqrMagnitude > 0.01f)
                {
                    Quaternion look = Quaternion.LookRotation(to, Vector3.up);
                    Quaternion rest = head.parent != null ? head.parent.rotation * headRest : headRest;
                    head.rotation = Quaternion.RotateTowards(rest, look, 70f);
                }
            }
        }

        void Apply(float pull)
        {
            if (lever != null)
            {
                lever.localRotation = Quaternion.Euler(Mathf.Lerp(-35f, 35f, pull), 0f, 0f);
            }

            if (armPivot != null)
            {
                armPivot.localRotation = Quaternion.Euler(Mathf.Lerp(-25f, -75f, pull), 0f, 0f);
            }
        }
    }
}
