using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Final climb and drop: lanterns along the lift, the giant jack-o'-lantern whose glow breathes, the FakeEmptySeat cold draught
    /// (lanterns and a hanging lamp sway harder, then settle) and the SkullFinale figure (turns at the sting and fills the frame, with a
    /// small reflection on a chrome lap-bar strip near the camera). The white-out afterwards belongs to the post FX, not to this set.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class FinalSet : ActSetBase
    {
        public override ActId Act => ActId.FinalClimb;

        public float startDist, endDist, topDist;
        public GameObject content;

        public Transform[] lanterns;        // pivots; the lantern hangs below along -Y
        public Transform lampPivot;
        public Light lampLight;
        public int draughtHint = -1;

        public Light pumpkinLight;
        public float pumpkinLightBase = 4f;

        public int skullHint = -1;
        public GameObject skull;
        public Light skullLight;
        public Transform halo;
        public GameObject lapBar;
        public Transform miniSkull;
        public Transform jaw;               // hinged jaw pivot of the main skull (rotating about X by +40 opens it)

        int _draught, _skullEvt;
        bool _ready, _look;
        Vector3 _frozenPos;
        Quaternion[] _lanternRot;
        Quaternion _lampRot;
        Vector3 _skullAnchor, _haloScale, _miniScale;
        Vector3 _skullScale;

        protected override void OnBound()
        {
            _draught = ActSetKit.FindEvent(State, ScareKind.FakeEmptySeat, draughtHint);
            _skullEvt = ActSetKit.FindEvent(State, ScareKind.SkullFinale, skullHint);
            _lanternRot = new Quaternion[lanterns != null ? lanterns.Length : 0];
            for (int i = 0; i < _lanternRot.Length; i++) _lanternRot[i] = lanterns[i].localRotation;
            if (lampPivot != null) _lampRot = lampPivot.localRotation;
            if (skull != null)
            {
                _skullAnchor = skull.transform.position;
                _skullScale = skull.transform.localScale;
                skull.SetActive(false);
            }
            if (halo != null) _haloScale = halo.localScale;
            if (miniSkull != null) _miniScale = miniSkull.localScale;
            if (lapBar != null) lapBar.SetActive(false);
            if (skullLight != null) skullLight.enabled = false;
            _look = false;
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

            // --- cold draught: sway grows through the quiet, peaks at the nothing, then settles
            EventClock dc = ActSetKit.Clock(State, _draught);
            float push = 0f;
            if (dc.Started)
            {
                float pre = Mathf.Max(0.5f, dc.ev.preQuietSeconds);
                push = ActSetKit.Smooth01(dc.elapsed / pre) * (1f - ActSetKit.Smooth01((dc.elapsed - pre - 0.25f) / 1.4f));
            }
            float amp = 1.2f + 12f * push;
            if (lanterns != null)
                for (int i = 0; i < lanterns.Length; i++)
                {
                    float ph = ActSetKit.Hash01(i, 81) * 6.2831853f;
                    float a = amp * Mathf.Sin(t * 0.55f * 6.2831853f + ph);
                    lanterns[i].localRotation = _lanternRot[i] * Quaternion.Euler(a * 0.4f, 0f, a);
                }
            if (lampPivot != null)
            {
                float a = (2f + 24f * push) * Mathf.Sin(t * 0.45f * 6.2831853f);
                lampPivot.localRotation = _lampRot * Quaternion.Euler(a * 0.3f, 0f, a);
                if (lampLight != null) lampLight.intensity = 2.5f * (1f - 0.55f * push);
            }

            // --- pumpkin glow breathes (about 0.4 Hz, plus or minus 15 percent)
            if (pumpkinLight != null)
                pumpkinLight.intensity = pumpkinLightBase * (1f + 0.15f * Mathf.Sin(t * 0.4f * 6.2831853f) + 0.05f * ActSetKit.Wobble(t, 3, 1.1f));

            UpdateSkull(t);
        }

        // SkullFinale. PreQuiet (2 s of dead silence, audio is B2's): a hooded figure stands with its back to the cart, off to the left of the
        // track in a blood-red glow. Sting: it is already in place; it turns (0.12 s) and its skull rushes the lens (0.2 s, ease-out),
        // jaw dropping open, filling the frame, held until HoldEnd. Then it slides aside and is left behind.
        const float RushSecs = 0.2f;
        const float HoldEnd = 1.1f;
        const float ReleaseSecs = 0.25f;

        void UpdateSkull(float t)
        {
            EventClock c = ActSetKit.Clock(State, _skullEvt);
            bool vis = c.Started && State.Distance < c.ev.distance + 45f;
            if (skull == null) return;
            ActSetKit.SetActive(skull, vis);
            if (skullLight != null) skullLight.enabled = vis;
            if (!vis)
            {
                if (lapBar != null) lapBar.SetActive(false);
                ActSetKit.LookRelease(ref _look);
                return;
            }

            float pre = c.ev.preQuietSeconds;
            float st = c.elapsed - pre;
            float appear = ActSetKit.AppearScale(c, 0.4f);
            Vector3 eye = ActSetKit.EyePos(State);
            Transform sk = skull.transform;
            // anchor derived from the event distance and the rail frame: where the cart will be about 1 s after the sting, off to the left
            Vector3 anchor = ActSetKit.RailPoint(State, c.ev.distance + 11f, 2.2f, 1.9f);

            if (st < 0f)
            {
                sk.position = anchor;
                sk.rotation = Quaternion.LookRotation(State.CartForward, Vector3.up);
                sk.localScale = _skullScale * appear;
                if (jaw != null) jaw.localRotation = Quaternion.identity;
                if (halo != null) halo.localScale = _haloScale * 0.0001f;
                if (lapBar != null) lapBar.SetActive(false);
                if (skullLight != null) { skullLight.intensity = 3f * appear; skullLight.transform.position = anchor + Vector3.up * 0.5f; }
                ActSetKit.LookAt(ref _look, anchor, 0.2f + 0.15f * ActSetKit.Smooth01(c.elapsed / Mathf.Max(0.5f, pre)));
                return;
            }

            float turn = ActSetKit.Smooth01(st / 0.12f);
            float rush = ActSetKit.EaseOut((st - 0.04f) / RushSecs);
            float creep = Mathf.Clamp01((st - RushSecs) / 0.8f);
            Vector3 lens = ActSetKit.LensPoint(State, Mathf.Lerp(1.2f, 1.05f, creep), 0.05f, 0.0f);
            Vector3 pos = Vector3.Lerp(anchor, lens, rush);
            float scaleK = Mathf.Lerp(1f, 0.66f, rush);   // 1.6 hooded figure at range -> about 1.05 skull at the lens (about 70 percent of frame height)
            if (st < HoldEnd)
            {
                _frozenPos = pos;
                if (st < 0.5f) ActSetKit.LookAt(ref _look, pos, 1f);
                else ActSetKit.LookRelease(ref _look);
                pos += new Vector3(Mathf.Sin(t * 43f), Mathf.Sin(t * 33f + 1f), Mathf.Sin(t * 39f)) * 0.01f;
            }
            else
            {
                float rel = ActSetKit.Smooth01((st - HoldEnd) / ReleaseSecs);
                pos = _frozenPos - ActSetKit.CartRight(State) * (3f * rel);
                scaleK *= 1f - rel;
                ActSetKit.LookRelease(ref _look);
            }
            sk.position = pos;
            Vector3 toEye = (eye - pos).sqrMagnitude > 1e-4f ? (eye - pos).normalized : -State.CartForward;
            Vector3 dir = Vector3.Slerp(State.CartForward, toEye, turn);
            sk.rotation = Quaternion.LookRotation(dir, Vector3.up);
            sk.localScale = _skullScale * Mathf.Max(0.001f, scaleK);
            float haloK = ActSetKit.Smooth01(st / 0.35f);
            if (halo != null) halo.localScale = _haloScale * Mathf.Max(0.0001f, haloK);
            // jaw drops open (hinge at the back), with a small tremor while held
            if (jaw != null)
            {
                float open = ActSetKit.EaseOut((st - 0.08f) / 0.3f);
                float tremor = st > 0.4f && st < HoldEnd ? Mathf.Sin(t * 22f) * 2.5f : 0f;
                jaw.localRotation = Quaternion.Euler(42f * open + tremor, 0f, 0f);
            }
            if (skullLight != null)
            {
                // blood-red light between the skull and the lens so the face is lit from the front
                skullLight.transform.position = pos + (eye - pos) * 0.45f + State.CartUp * 0.1f;
                skullLight.intensity = Mathf.Lerp(3f, 6f, rush) * (1f - ActSetKit.Smooth01((st - HoldEnd) / ReleaseSecs));
                skullLight.range = 7f;
            }

            // polished lap-bar strip near the camera carrying a small reflection of the skull
            if (lapBar != null)
            {
                bool showBar = rush > 0.3f && st < HoldEnd;
                ActSetKit.SetActive(lapBar, showBar);
                if (showBar)
                {
                    Quaternion er = ActSetKit.EyeRot(State);
                    lapBar.transform.position = eye + er * Vector3.forward * 0.8f + er * Vector3.up * -0.5f;
                    lapBar.transform.rotation = er;
                    if (miniSkull != null) miniSkull.localScale = _miniScale * ActSetKit.Smooth01((rush - 0.3f) / 0.4f);
                }
            }
        }
    }
}
