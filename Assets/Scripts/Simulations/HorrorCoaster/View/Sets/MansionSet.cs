using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Haunted mansion: double doors fly open as the cart arrives, portraits' eyes follow the camera, candles flicker (slowly),
    /// and PaintingLunge: a bright-framed portrait appears in PreQuiet, lunges at the cart in Sting while a chandelier drops.
    /// Read-only: every pose is derived from State and the event clock.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MansionSet : ActSetBase
    {
        public override ActId Act => ActId.Mansion;

        public float startDist, endDist, doorDist;
        public GameObject content;
        public Transform doorL, doorR;
        public Transform[] eyePivots;   // parallel with eyeRoots: the portrait each eye belongs to (local +Z = out of the wall)
        public Transform[] eyeRoots;
        public Transform[] flames;
        public Light[] lights;
        public float[] lightDist;
        public float cullRange = 240f;
        public GameObject[] cullObjs;
        public float[] cullDists;

        public int lungeHint = -1;
        public GameObject lungeObj;
        public Transform lungeEyeL, lungeEyeR, lungeMouth;
        public Light lungeLight;
        public Transform lungeFace;     // the face group that leaves the canvas (child of lungeObj)
        public Transform lungeArm, lungeClaw;
        public Transform chandelier;
        public Light chandLight;
        public GameObject chandFlames;
        public float chandFloorY;

        int _evt, _takenSeat = -1;
        bool _ready, _look;
        Vector3 _faceLocal, _faceScale = Vector3.one, _frozenPos, _seatPos;
        Vector3[] _flameScale;
        Vector3 _wallPos, _chandHome, _eyeScale, _mouthScale, _lungeScale;
        Quaternion _wallRot, _chandRot;

        protected override void OnBound()
        {
            _evt = ActSetKit.FindEvent(State, ScareKind.PaintingLunge, lungeHint);
            _flameScale = new Vector3[flames != null ? flames.Length : 0];
            for (int i = 0; i < _flameScale.Length; i++) _flameScale[i] = flames[i].localScale;
            if (lungeObj != null)
            {
                _wallPos = lungeObj.transform.position;
                _wallRot = lungeObj.transform.rotation;
                _lungeScale = lungeObj.transform.localScale;
                lungeObj.SetActive(false);
            }
            if (lungeFace != null)
            {
                _faceLocal = lungeFace.localPosition;
                _faceScale = lungeFace.localScale;
            }
            _takenSeat = -1;
            _look = false;
            if (lungeEyeL != null) _eyeScale = lungeEyeL.localScale;
            if (lungeMouth != null) { _mouthScale = lungeMouth.localScale; lungeMouth.localScale = Vector3.zero; }
            if (chandelier != null)
            {
                _chandHome = chandelier.position;
                _chandRot = chandelier.rotation;
                ActSetKit.SetActive(chandelier.gameObject, true);
            }
            if (doorL != null) doorL.localRotation = Quaternion.identity;
            if (doorR != null) doorR.localRotation = Quaternion.identity;
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

            if (cullObjs != null)
                for (int i = 0; i < cullObjs.Length; i++)
                    if (cullObjs[i] != null) ActSetKit.SetActive(cullObjs[i], Mathf.Abs(cullDists[i] - d) < cullRange);

            // --- doors burst open as the cart arrives (swing inward, away from the cart)
            float open = 1f - Mathf.Pow(1f - ActSetKit.Remap01(d, doorDist - 22f, doorDist - 8f), 3f);
            if (doorL != null) doorL.localRotation = Quaternion.Euler(0f, -110f * open, 0f);
            if (doorR != null) doorR.localRotation = Quaternion.Euler(0f, 110f * open, 0f);

            // --- portraits: eyes follow the viewer (clamped to a believable cone)
            if (eyePivots != null)
                for (int i = 0; i < eyePivots.Length; i++)
                {
                    Transform piv = eyePivots[i];
                    if (piv == null) continue;
                    Vector3 to = eye - piv.position;
                    if (to.sqrMagnitude > 90f * 90f) continue;
                    LookClamped(piv, eyeRoots[i], to);
                }

            // --- candles: slow flicker (<= 2.5 Hz), scale only
            for (int i = 0; i < _flameScale.Length; i++)
            {
                Transform f = flames[i];
                if (f == null) continue;
                float k = 1f + 0.18f * ActSetKit.Wobble(t, i, 2.5f);
                f.localScale = _flameScale[i] * k;
            }

            // --- hall lights only near the cart
            if (lights != null)
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i] != null) lights[i].enabled = Mathf.Abs(lightDist[i] - d) < 50f;

            UpdateLunge(t, d);
        }

        // The painting's hand: shoots out across the hall toward the taken rider's seat, then drags back.
        void UpdateArm(float st, float d, float t)
        {
            if (lungeArm == null || lungeClaw == null) return;
            if (st >= 0f && _takenSeat < 0 && st < 0.2f) _takenSeat = State.TakenSeat;
            bool show = _takenSeat >= 0 && st >= 0f && st < 0.6f;
            lungeArm.gameObject.SetActive(show);
            lungeClaw.gameObject.SetActive(show);
            if (!show) return;

            Vector3 root = _wallPos + _wallRot * new Vector3(0f, -0.4f, 0.5f);
            if (st < 0.3f) _seatPos = ActSetKit.RailPoint(State, d + RiderRoster.SeatSpacing * (_takenSeat + 1), 0f, 0.75f);
            float reach = ActSetKit.EaseOut(st / 0.1f);
            float drag = ActSetKit.Smooth01((st - 0.3f) / 0.25f);
            Vector3 tip = Vector3.Lerp(root, _seatPos, reach);
            tip = Vector3.Lerp(tip, root, drag);
            Vector3 dir = tip - root;
            float len = dir.magnitude;
            if (len < 0.05f) { lungeArm.gameObject.SetActive(false); lungeClaw.gameObject.SetActive(false); return; }
            Quaternion rot = Quaternion.LookRotation(dir / len, Vector3.up);
            lungeArm.SetPositionAndRotation(root + dir * 0.5f, rot * Quaternion.Euler(90f, 0f, 0f));
            lungeArm.localScale = new Vector3(0.22f, len * 0.5f, 0.22f);
            lungeClaw.SetPositionAndRotation(tip, rot);
            lungeClaw.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(t * 30f));
        }

        static void LookClamped(Transform pivot, Transform root, Vector3 worldDir)
        {
            Vector3 l = root.InverseTransformDirection(worldDir.normalized);
            l.z = Mathf.Max(l.z, 0.45f);
            pivot.localRotation = Quaternion.LookRotation(l.normalized, Vector3.up);
        }

        // Jump-scare timings (seconds into the Sting phase)
        const float LungeSecs = 0.18f;   // wall to lens, ease-out
        const float HoldEnd = 0.75f;     // face holds on the lens until here
        const float ReleaseSecs = 0.25f; // then it is left behind / slides away

        void UpdateLunge(float t, float d)
        {
            EventClock c = ActSetKit.Clock(State, _evt);
            bool gone = !c.valid || d > c.ev.distance + 40f;
            bool vis = c.Started && !gone;
            if (lungeObj != null) ActSetKit.SetActive(lungeObj, vis);
            if (!vis)
            {
                ActSetKit.LookRelease(ref _look);
                _takenSeat = -1;
                if (lungeLight != null) lungeLight.enabled = false;
            }
            float pre = c.valid ? c.ev.preQuietSeconds : 0f;
            float st = c.Started ? c.elapsed - pre : -1f; // negative during PreQuiet

            if (lungeObj != null && vis)
            {
                Transform lt = lungeObj.transform;
                float appear = ActSetKit.AppearScale(c, 0.35f);
                lt.position = _wallPos;
                lt.rotation = _wallRot;
                lt.localScale = _lungeScale * appear;

                Vector3 wallFace = _wallPos + _wallRot * Vector3.Scale(_faceLocal, _lungeScale * appear);
                Vector3 eye = ActSetKit.EyePos(State);
                Vector3 facePos = wallFace;
                float faceScale = 1f, zStretch = 1f, snarl = 0f, lightK = 0.25f * appear;

                if (st < 0f)
                {
                    // cue: the portrait's eyes smoulder and the smile creeps open; the head eases toward it
                    float cue = ActSetKit.Smooth01(c.elapsed / Mathf.Max(0.5f, pre));
                    snarl = 0.25f * cue;
                    lightK = 0.35f * appear * (0.4f + 0.6f * cue);
                    ActSetKit.LookAt(ref _look, wallFace, 0.2f + 0.15f * cue);
                }
                else
                {
                    float follow = ActSetKit.EaseOut(st / LungeSecs);
                    float creep = Mathf.Clamp01((st - LungeSecs) / 0.5f);
                    Vector3 lens = ActSetKit.LensPoint(State, Mathf.Lerp(0.95f, 0.82f, creep), 0.3f, 0.04f);
                    facePos = Vector3.Lerp(wallFace, lens, follow);
                    faceScale = Mathf.Lerp(1f, 0.26f, follow);
                    float sk = LungeSecs * 1.3f;
                    zStretch = st < sk ? 1f + 1.8f * Mathf.Sin(Mathf.PI * st / sk) : 1f;
                    snarl = Mathf.Lerp(0.25f, 1f, ActSetKit.Smooth01(st / 0.15f));
                    lightK = Mathf.Lerp(0.35f, 5.5f / 3f, follow);
                    if (st > LungeSecs && st < HoldEnd)
                        facePos += new Vector3(Mathf.Sin(t * 41f), Mathf.Sin(t * 37f + 1f), Mathf.Sin(t * 29f)) * 0.012f; // shiver, position only

                    if (st < HoldEnd)
                    {
                        _frozenPos = facePos;
                        if (st < 0.5f) ActSetKit.LookAt(ref _look, facePos, 1f);
                        else ActSetKit.LookRelease(ref _look);
                    }
                    else
                    {
                        // released: left behind in the world, sliding off the cart's line and shrinking away
                        float rel = ActSetKit.Smooth01((st - HoldEnd) / ReleaseSecs);
                        facePos = _frozenPos - ActSetKit.CartRight(State) * (2.4f * rel);
                        faceScale *= 1f - rel;
                        lightK *= 1f - rel;
                        ActSetKit.LookRelease(ref _look);
                    }
                }

                if (lungeFace != null)
                {
                    Vector3 toEye = eye - facePos;
                    Quaternion fr = st < 0f || toEye.sqrMagnitude < 1e-4f
                        ? _wallRot
                        : Quaternion.Slerp(_wallRot, Quaternion.LookRotation(toEye.normalized, State.CartUp), Mathf.Clamp01(st / 0.1f));
                    lungeFace.SetPositionAndRotation(facePos, fr);
                    lungeFace.localScale = Vector3.Scale(_faceScale, new Vector3(faceScale, faceScale, faceScale * zStretch));
                    lungeFace.gameObject.SetActive(faceScale > 0.001f);
                }
                if (lungeEyeL != null) lungeEyeL.localScale = _eyeScale * (1f + 1.2f * snarl);
                if (lungeEyeR != null) lungeEyeR.localScale = _eyeScale * (1f + 1.2f * snarl);
                if (lungeMouth != null) lungeMouth.localScale = Vector3.Scale(_mouthScale, new Vector3(1f + 0.3f * snarl, 0.05f + 2.4f * snarl, 1f));
                if (lungeLight != null)
                {
                    lungeLight.enabled = lightK > 0.01f;
                    Vector3 toward = eye - facePos;
                    toward = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.up;
                    lungeLight.transform.position = facePos + toward * 0.55f + State.CartUp * 0.12f;
                    lungeLight.intensity = 3f * lightK;
                    lungeLight.range = 6f;
                }

                UpdateArm(st, d, t);
            }

            // --- chandelier: creaks in PreQuiet, drops at the sting, shatters on the floor
            if (chandelier != null)
            {
                if (d > (c.valid ? c.ev.distance : 1e9f) + 40f) { ActSetKit.SetActive(chandelier.gameObject, false); return; }
                Vector3 p = _chandHome;
                Quaternion r = _chandRot;
                bool landed = false;
                if (c.Started)
                {
                    if (st < 0.05f)
                    {
                        float sway = Mathf.Sin(t * 5f) * 1.5f * ActSetKit.Smooth01(c.elapsed / 1.0f);
                        r = _chandRot * Quaternion.Euler(sway, 0f, sway * 0.6f);
                    }
                    else
                    {
                        float fall = st - 0.05f;
                        float y = _chandHome.y - 0.5f * 35f * fall * fall;
                        if (y <= chandFloorY)
                        {
                            y = chandFloorY;
                            landed = true;
                        }
                        p.y = y;
                        r = _chandRot * Quaternion.Euler(0f, 0f, landed ? 14f : 0f);
                    }
                }
                chandelier.position = p;
                chandelier.rotation = r;
                if (chandFlames != null) ActSetKit.SetActive(chandFlames, !landed);
                if (chandLight != null) chandLight.intensity = landed ? 0.4f : 2.5f;
            }
        }
    }
}
