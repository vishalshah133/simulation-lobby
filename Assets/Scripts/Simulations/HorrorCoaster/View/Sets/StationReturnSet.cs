using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// The station again, changed: dust, dead flowers, cobwebs, the skeleton operator whose jaw clatters under a "Welcome back" sign,
    /// and a "SEAT 6: YOU" board that lights up as the cart pulls in. Everything slow; the board ramps over seconds, never flashes.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class StationReturnSet : ActSetBase
    {
        public override ActId Act => ActId.StationReturn;

        public float startDist, endDist, operatorDist;
        public GameObject content;

        public Transform opRoot, opHead, opJaw, opArm;
        public Transform speechRoot;

        public TMP_Text seatText;
        public Renderer[] seatBulbs;
        public Light seatLight;
        public Color seatBulbEmission = Color.white;
        public Color seatTextDim = new Color(0.28f, 0.26f, 0.22f, 1f);
        public Color seatTextLit = new Color(1f, 0.86f, 0.4f, 1f);

        public Light[] lamps;
        public float[] lampBase;
        public float[] lampDist;

        public Transform[] dust;
        public Vector3[] dustBase;

        bool _ready;
        Vector3 _speechScale;
        MaterialPropertyBlock _mpb;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        protected override void OnBound()
        {
            _mpb = new MaterialPropertyBlock();
            if (speechRoot != null) { _speechScale = speechRoot.localScale; speechRoot.localScale = Vector3.zero; }
            if (seatText != null) seatText.color = seatTextDim;
            if (seatLight != null) seatLight.intensity = 0f;
            _ready = true;
            ActSetKit.SetActive(content, false);
        }

        void LateUpdate()
        {
            if (!_ready || State == null) return;
            bool on = ActSetKit.ActWeight(State, startDist, endDist, 3f) > 0.001f;
            ActSetKit.SetActive(content, on);
            if (!on) return;

            float t = State.SimTime;
            float d = State.Distance;
            Vector3 eye = ActSetKit.EyePos(State);

            // --- lamps: slow, shallow flicker (30 percent at most, under 2 Hz)
            if (lamps != null)
                for (int i = 0; i < lamps.Length; i++)
                {
                    Light l = lamps[i];
                    if (l == null) continue;
                    l.enabled = Mathf.Abs(lampDist[i] - d) < 60f;
                    l.intensity = lampBase[i] * (0.85f + 0.15f * ActSetKit.Wobble(t, i + 5, 1.2f));
                }

            // --- dust motes drift
            if (dust != null && dustBase != null)
                for (int i = 0; i < dust.Length; i++)
                {
                    float h1 = ActSetKit.Hash01(i, 91), h2 = ActSetKit.Hash01(i, 92), h3 = ActSetKit.Hash01(i, 93);
                    dust[i].position = dustBase[i] + new Vector3(Mathf.Sin(t * 0.2f + h1 * 6.28f) * 0.7f, Mathf.Sin(t * 0.3f + h2 * 6.28f) * 0.4f,
                                                                  Mathf.Cos(t * 0.25f + h3 * 6.28f) * 0.7f);
                }

            // --- operator: faces the cart, jaw clatters while the welcome is up
            float speech = ActSetKit.Smooth01((d - (operatorDist - 34f)) / 8f);
            if (speechRoot != null) speechRoot.localScale = _speechScale * speech;
            if (opRoot != null && opHead != null)
            {
                Vector3 to = eye - opHead.position;
                if (to.sqrMagnitude < 90f * 90f)
                {
                    Vector3 l = opRoot.InverseTransformDirection(to.normalized);
                    l.z = Mathf.Max(l.z, 0.25f);
                    opHead.localRotation = Quaternion.LookRotation(l.normalized, Vector3.up);
                }
            }
            if (opJaw != null)
            {
                float open = speech > 0.5f ? 16f * (0.5f + 0.5f * Mathf.Sin(t * 2.4f * 6.2831853f)) : 0f;
                opJaw.localRotation = Quaternion.Euler(open, 0f, 0f);
            }
            if (opArm != null)
            {
                float raise = 120f * ActSetKit.Smooth01((d - (operatorDist - 30f)) / 10f);
                opArm.localRotation = Quaternion.Euler(0f, 0f, raise + 12f * Mathf.Sin(t * 1.2f * 6.2831853f) * (raise > 1f ? 1f : 0f));
            }

            // --- SEAT 6: YOU lights up as the cart pulls in (ramp over ~4 s of travel)
            float lit = ActSetKit.Smooth01((d - (endDist - 42f)) / 30f);
            if (seatText != null) seatText.color = Color.Lerp(seatTextDim, seatTextLit, lit);
            if (seatLight != null) seatLight.intensity = 3f * lit;
            if (seatBulbs != null)
                for (int i = 0; i < seatBulbs.Length; i++)
                {
                    if (seatBulbs[i] == null) continue;
                    seatBulbs[i].GetPropertyBlock(_mpb);
                    _mpb.SetColor(EmissionId, seatBulbEmission * lit);
                    seatBulbs[i].SetPropertyBlock(_mpb);
                }
        }
    }
}
