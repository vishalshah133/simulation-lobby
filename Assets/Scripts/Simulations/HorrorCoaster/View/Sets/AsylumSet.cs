using System;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>One FakeWheelchair: a chair that rolls out of a cell door into the rail path, brakes, and does nothing.</summary>
    [Serializable]
    public class WheelchairSlot
    {
        public int hint = -1;
        public GameObject chair;
        public Transform door;
        public Transform wheelL, wheelR;
        public Vector3 from, to;
        public Quaternion rot = Quaternion.identity;
        public float doorAngle = 85f;
        [NonSerialized] public int evt = -1;
        [NonSerialized] public bool look;
    }

    /// <summary>
    /// Asylum corridor. Ceiling lights flicker slowly (each between 30 and 100 percent, at most ~2 Hz: never a strobe, never fully dark
    /// except the one last light that dies once before the figure); a small pool of real lights follows the nearest fixtures.
    /// Two FakeWheelchair events (short and fast) and one AsylumFigure jump scare. Read-only: every pose is derived from State.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class AsylumSet : ActSetBase
    {
        public override ActId Act => ActId.Asylum;

        public float startDist, endDist;
        public GameObject content;
        public float cullRange = 240f;
        public GameObject[] cullObjs;
        public float[] cullDists;

        public Renderer[] fixtures;
        public float[] fixtureDist;
        public Vector3[] fixturePos;
        public Color fixtureEmission = Color.white;
        public Light[] pool;
        public float poolIntensity = 2.2f;

        public WheelchairSlot[] chairs;

        public int figureHint = -1;
        public GameObject figure;
        public Transform figureHead;
        public Transform figureArmL, figureArmR;   // shoulder pivots; rotating about X by -90 raises the arm forward
        public Light figureLamp;
        public float lastLightDist;
        public float floorDrop = 1.2f;

        // Jump-scare timings (seconds into the Sting phase)
        const float LungeSecs = 0.16f;     // behind the shoulder to face to face, ease-out
        const float HoldEnd = 0.7f;
        const float ReleaseSecs = 0.2f;
        // Fake-out: the roll starts RollSecs before the (silent) sting moment
        const float RollSecs = 1.1f;

        int _figEvt;
        bool _ready, _look;
        Vector3 _standPos, _lampHome, _frozenPos;
        Quaternion _standRot;
        MaterialPropertyBlock _mpb;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        protected override void OnBound()
        {
            _mpb = new MaterialPropertyBlock();
            _figEvt = ActSetKit.FindEvent(State, ScareKind.AsylumFigure, figureHint);
            if (figure != null)
            {
                _standPos = figure.transform.position;
                _standRot = figure.transform.rotation;
                figure.SetActive(false);
            }
            if (figureLamp != null)
            {
                _lampHome = figureLamp.transform.position;
                figureLamp.enabled = false;
            }
            _look = false;
            if (chairs != null)
                foreach (WheelchairSlot c in chairs)
                {
                    c.evt = ActSetKit.FindEvent(State, ScareKind.FakeWheelchair, c.hint);
                    c.look = false;
                    if (c.chair != null) c.chair.SetActive(false);
                    if (c.door != null) c.door.localRotation = Quaternion.identity;
                }
            _ready = true;
            ActSetKit.SetActive(content, false);
        }

        void LateUpdate()
        {
            if (!_ready || State == null) return;
            bool on = ActSetKit.ActWeight(State, startDist, endDist, 3f) > 0.001f;
            ActSetKit.SetActive(content, on);
            if (!on) return;

            float d = State.Distance;
            float t = State.SimTime;

            if (cullObjs != null)
                for (int i = 0; i < cullObjs.Length; i++)
                    if (cullObjs[i] != null) ActSetKit.SetActive(cullObjs[i], Mathf.Abs(cullDists[i] - d) < cullRange);

            EventClock fc = ActSetKit.Clock(State, _figEvt);
            float dim = 0f, kick = 0f;
            if (fc.Started)
            {
                float pre = fc.ev.preQuietSeconds;
                dim = 0.6f * ActSetKit.Smooth01(fc.elapsed / 1.5f);
                float st = fc.elapsed - pre;
                // the last light fades out over 0.35 s, is dark 0.15 s before the sting, and fades back after 0.7 s: one dip, never a strobe
                kick = 0.97f * ActSetKit.Smooth01((st + 0.5f) / 0.35f) * (1f - ActSetKit.Smooth01((st - 0.7f) / 0.5f));
            }

            UpdateLights(t, d, dim, kick);
            for (int i = 0; chairs != null && i < chairs.Length; i++) UpdateChair(chairs[i], d);
            UpdateFigure(fc, d, t);
        }

        float Brightness(int i, float t, float dim, float kick)
        {
            bool last = i == fixtures.Length - 1;
            float hz = 0.4f + 1.8f * ActSetKit.Hash01(i, 71);
            float ph = ActSetKit.Hash01(i, 72) * 6.2831853f;
            float wave = ActSetKit.Smooth01(0.5f + 0.6f * Mathf.Sin(t * hz * 6.2831853f + ph));
            float b = Mathf.Lerp(0.3f, 1f, wave);
            return last ? b * (1f - kick) : b * (1f - dim);
        }

        void UpdateLights(float t, float d, float dim, float kick)
        {
            if (fixtures == null || fixtures.Length == 0) return;
            int first = 0;
            while (first < fixtureDist.Length - 1 && fixtureDist[first] < d - 3f) first++;
            // emission on the fixtures near the cart only
            for (int i = Mathf.Max(0, first - 4); i < Mathf.Min(fixtures.Length, first + 28); i++)
            {
                if (fixtures[i] == null) continue;
                float b = Brightness(i, t, dim, kick);
                fixtures[i].GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, fixtureEmission * b);
                fixtures[i].SetPropertyBlock(_mpb);
            }
            if (pool != null)
                for (int k = 0; k < pool.Length; k++)
                {
                    int i = first + k;
                    Light l = pool[k];
                    if (l == null) continue;
                    if (i >= fixtures.Length) { l.enabled = false; continue; }
                    l.enabled = true;
                    l.transform.position = fixturePos[i] + Vector3.down * 0.5f;
                    l.intensity = poolIntensity * Brightness(i, t, dim, kick);
                }
        }

        // Fake-out: the cell door bangs open and the chair shoots out of the dark, brakes beside the rail, rocks once and is left behind.
        void UpdateChair(WheelchairSlot c, float d)
        {
            if (c == null || c.chair == null) return;
            EventClock ck = ActSetKit.Clock(State, c.evt);
            bool vis = ck.Started && d < ck.ev.distance + 28f;
            ActSetKit.SetActive(c.chair, vis);
            if (!vis)
            {
                ActSetKit.LookRelease(ref c.look);
                return;
            }
            float pre = ck.ev.preQuietSeconds;
            float roll = ck.elapsed - Mathf.Max(0f, pre - RollSecs);          // 0 at the door slam
            float x = Mathf.Clamp01(roll / RollSecs);
            float run = 1f - (1f - x) * (1f - x);                              // quick start, hard brake
            c.chair.transform.position = Vector3.Lerp(c.from, c.to, run);
            float rock = roll > RollSecs ? Mathf.Exp(-(roll - RollSecs) * 5f) * Mathf.Sin((roll - RollSecs) * 22f) * 6f : 0f;
            c.chair.transform.rotation = c.rot * Quaternion.Euler(0f, rock, 0f);
            float travel = (c.to - c.from).magnitude * run;
            Quaternion spin = Quaternion.Euler(travel / 0.33f * Mathf.Rad2Deg, 0f, 0f);
            if (c.wheelL != null) c.wheelL.localRotation = spin;
            if (c.wheelR != null) c.wheelR.localRotation = spin;
            if (c.door != null) c.door.localRotation = Quaternion.Euler(0f, c.doorAngle * ActSetKit.EaseOut(roll / 0.25f), 0f);
            // the eye is drawn gently toward the bang of the door
            if (roll > 0f && ck.phase == ScarePhase.PreQuiet) ActSetKit.LookAt(ref c.look, c.chair.transform.position + Vector3.up * 0.6f, 0.3f);
            else ActSetKit.LookRelease(ref c.look);
        }

        // AsylumFigure. PreQuiet: a pale figure stands in the last light, far down the corridor, head swaying. About 0.5 s before the
        // sting that light dies and the figure is hidden in the dark. Sting: it is at the cart's side behind the camera's shoulder and
        // within LungeSecs it is face to face, inches from the lens, arms thrown forward. Then it slides aside, left behind.
        void UpdateFigure(EventClock c, float d, float t)
        {
            bool vis = c.Started && d < c.ev.distance + 40f;
            if (!vis || figure == null)
            {
                if (figure != null) ActSetKit.SetActive(figure, false);
                if (figureLamp != null) figureLamp.enabled = false;
                ActSetKit.LookRelease(ref _look);
                return;
            }
            float pre = c.ev.preQuietSeconds;
            float st = c.elapsed - pre;
            bool dark = c.elapsed > pre - 0.12f;
            Transform ft = figure.transform;

            if (st < 0f)
            {
                ActSetKit.SetActive(figure, !dark);
                float appear = ActSetKit.AppearScale(c, 0.4f);
                ft.position = _standPos;
                ft.rotation = _standRot;
                ft.localScale = new Vector3(1f, appear, 1f);
                if (figureHead != null)
                {
                    figureHead.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f) * 6f);
                    figureHead.localScale = Vector3.one;
                }
                if (figureArmL != null) figureArmL.localRotation = Quaternion.identity;
                if (figureArmR != null) figureArmR.localRotation = Quaternion.identity;
                if (figureLamp != null)
                {
                    figureLamp.enabled = !dark;
                    figureLamp.transform.position = _lampHome;
                    figureLamp.intensity = 3.5f * appear;
                    figureLamp.range = 6f;
                }
                float cue = ActSetKit.Smooth01(c.elapsed / Mathf.Max(0.5f, pre));
                if (!dark) ActSetKit.LookAt(ref _look, _standPos + Vector3.up * 1.6f, 0.2f + 0.15f * cue);
                else ActSetKit.LookRelease(ref _look);
                return;
            }

            // ---- sting
            ActSetKit.SetActive(figure, true);
            Vector3 up = State.CartUp;
            Vector3 eye = ActSetKit.EyePos(State);
            Vector3 side = ActSetKit.LensPoint(State, -0.5f, 1.7f, 0.05f);      // behind the shoulder, out of frame
            float follow = ActSetKit.EaseOut(st / LungeSecs);
            float creep = Mathf.Clamp01((st - LungeSecs) / 0.5f);
            Vector3 lens = ActSetKit.LensPoint(State, Mathf.Lerp(0.8f, 0.7f, creep), 0.22f, 0.02f);
            Vector3 head = Vector3.Lerp(side, lens, follow);
            float scale = 1f;
            float lightK = Mathf.Lerp(0.2f, 1f, follow);
            if (st < HoldEnd)
            {
                _frozenPos = head;
                if (st < 0.5f) ActSetKit.LookAt(ref _look, head, 1f);
                else ActSetKit.LookRelease(ref _look);
                head += new Vector3(Mathf.Sin(t * 47f), Mathf.Sin(t * 31f + 2f), Mathf.Sin(t * 37f)) * 0.01f;
            }
            else
            {
                float rel = ActSetKit.Smooth01((st - HoldEnd) / ReleaseSecs);
                head = _frozenPos + ActSetKit.CartRight(State) * (2.6f * rel);   // slides to the right wall, left behind
                scale = 1f - rel;
                lightK *= 1f - rel;
                ActSetKit.LookRelease(ref _look);
            }

            Vector3 toEye = eye - head;
            toEye.y = 0f;
            Quaternion yaw = toEye.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toEye.normalized, Vector3.up) : _standRot;
            Quaternion body = yaw * Quaternion.Euler(Mathf.Lerp(0f, 22f, follow), 0f, 0f);
            const float headY = 1.95f;
            ft.rotation = body;
            ft.position = head - body * new Vector3(0f, headY * Mathf.Max(0.001f, scale), 0.02f);
            ft.localScale = Vector3.one * Mathf.Max(0.001f, scale);
            if (figureHead != null)
            {
                figureHead.localRotation = Quaternion.Euler(8f, 0f, -22f * ActSetKit.Smooth01(st / 0.15f) * (1f - ActSetKit.Smooth01(st / 0.5f)));
                figureHead.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, follow);   // the face swells as it arrives
            }
            // arms thrown forward and up, a beat after the face lands
            float reach = ActSetKit.EaseOut((st - 0.05f) / 0.14f);
            float flail = st > 0.2f && st < HoldEnd ? Mathf.Sin(t * 26f) * 7f : 0f;
            if (figureArmL != null) figureArmL.localRotation = Quaternion.Euler(-105f * reach + flail, 14f * reach, 0f);
            if (figureArmR != null) figureArmR.localRotation = Quaternion.Euler(-105f * reach - flail, -14f * reach, 0f);

            if (figureLamp != null)
            {
                figureLamp.enabled = lightK > 0.01f;
                Vector3 toward = eye - head;
                toward = toward.sqrMagnitude > 1e-4f ? toward.normalized : up;
                figureLamp.transform.position = head + toward * 0.5f + up * 0.15f;
                figureLamp.intensity = 5.5f * lightK;
                figureLamp.range = 5f;
            }
        }
    }
}
