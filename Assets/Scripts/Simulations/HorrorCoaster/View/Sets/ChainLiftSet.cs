using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// The climb above a pumpkin-lit town. The FakeBat: a big bat hovers ahead during the quiet, then swoops past the cart and
    /// away. Nothing is taken. Ambient bats and lantern strings are cosmetic components.
    /// </summary>
    public sealed class ChainLiftSet : CoasterSet
    {
        public override ActId Act => ActId.ChainLift;

        public GameObject bat;
        [Tooltip("Metres ahead and above the cart where the bat hovers in the quiet.")]
        public Vector3 hoverOffset = new Vector3(3f, 5.5f, 16f);   // right, up, forward
        public Vector3 passOffset = new Vector3(1.0f, 0.9f, 1.5f);
        public Vector3 exitOffset = new Vector3(-3f, 9f, -10f);
        public float swoopSeconds = 0.9f;

        int eventIndex = -1;
        readonly EventStage clock = new EventStage();

        protected override void ResetSet()
        {
            eventIndex = -1;
            var list = SetUtil.EventsOf(State, ScareKind.FakeBat);
            if (list.Count > 0)
            {
                eventIndex = list[0];
            }

            clock.Reset();
            Show(bat, false);
        }

        protected override void Tick()
        {
            clock.Update(State, eventIndex);
            if (clock.Stage == 0 || bat == null)
            {
                Show(bat, false);
                return;
            }

            Vector3 c = State.CartPosition;
            Vector3 f = State.CartForward;
            Vector3 u = State.CartUp;
            Vector3 r = Vector3.Cross(u, f);
            Vector3 hover = c + r * hoverOffset.x + u * hoverOffset.y + f * hoverOffset.z;
            Vector3 pos;
            Vector3 look;
            float scale = 1f;

            if (clock.Stage == 1)
            {
                float t = clock.T;
                pos = hover + r * (3f * Mathf.Sin(t * 0.9f)) + u * (0.6f * Mathf.Sin(t * 1.9f));
                look = c - pos;
                scale = SetUtil.Smooth(t / 0.5f);
            }
            else
            {
                float s = Mathf.Clamp01(clock.SinceSting / swoopSeconds);
                if (clock.SinceSting > swoopSeconds + 0.5f)
                {
                    Show(bat, false);
                    return;
                }

                Vector3 p0 = hover;
                Vector3 p1 = c + r * passOffset.x + u * passOffset.y + f * passOffset.z;
                Vector3 p2 = c + r * exitOffset.x + u * exitOffset.y + f * exitOffset.z;
                float e = s * s * (3f - 2f * s);
                pos = Bezier(p0, p1, p2, e);
                look = Bezier(p0, p1, p2, Mathf.Min(1f, e + 0.04f)) - pos;
                if (look.sqrMagnitude < 1e-6f)
                {
                    look = -f;
                }

                scale = 1f + 0.5f * Mathf.Sin(e * Mathf.PI);
            }

            Show(bat, true);
            bat.transform.position = pos;
            bat.transform.rotation = SetUtil.Look(look, u);
            bat.transform.localScale = Vector3.one * scale;
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float k = 1f - t;
            return k * k * a + 2f * k * t * b + t * t * c;
        }
    }
}
