using System;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Tunnel of Faces. Carved jack-o'-lantern faces light up in sequence ahead of the cart. In the quiet before the TunnelFace
    /// scare the lit faces dim and the last face, a dark carving in the LEFT wall just ahead, shows two faint green pinpoints. In the sting
    /// it flares up, tears out of the wall and smashes at the lens (~1.2 m, own light), then is left behind. Brightness is set through a MaterialPropertyBlock (no material instancing).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class TunnelSet : CoasterSet
    {
        public override ActId Act => ActId.TunnelOfFaces;

        [Serializable]
        public sealed class Face
        {
            public float distance;
            public Renderer[] lit = new Renderer[0];
            public Light light;
            public float lightBase = 2.5f;
        }

        public Face[] faces = new Face[0];
        [Tooltip("A face lights up when the cart is this many metres before it.")]
        public float litLeadMeters = 42f;
        public float litRampMeters = 8f;
        [ColorUsage(false, true)] public Color litColor = new Color(1f, 0.48f, 0.09f, 1f);
        public float litIntensity = 2.4f;

        [Header("Final face (behind the cart)")]
        public Transform finalFace;
        public Renderer[] finalLit = new Renderer[0];
        public Light finalLight;
        [ColorUsage(false, true)] public Color finalColor = new Color(0.49f, 1f, 0.31f, 1f);
        public float finalIntensity = 3.2f;
        public float finalLightBase = 6f;
        public Transform finalCue;

        int faceEvent = -1;
        readonly EventStage clock = new EventStage();
        readonly LungeTrack track = new LungeTrack();
        readonly LookCue look = new LookCue();
        float[] lastK;
        MaterialPropertyBlock block;
        Vector3 finalHomePos;
        Quaternion finalHomeRot;
        Vector3 finalHomeScale;
        bool finalHomed;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        protected override void ResetSet()
        {
            block = new MaterialPropertyBlock();
            faceEvent = -1;
            var list = SetUtil.EventsOf(State, ScareKind.TunnelFace);
            if (list.Count > 0)
            {
                faceEvent = list[0];
            }

            clock.Reset();
            track.Reset();
            look.Release();
            lastK = new float[faces.Length];
            for (int i = 0; i < lastK.Length; i++)
            {
                lastK[i] = -1f;
            }

            SetUtil.SetActive(finalCue, false);
            if (finalFace != null)
            {
                if (!finalHomed)
                {
                    finalHomePos = finalFace.position;
                    finalHomeRot = finalFace.rotation;
                    finalHomeScale = finalFace.localScale;
                    finalHomed = true;
                }

                finalFace.SetPositionAndRotation(finalHomePos, finalHomeRot);
                finalFace.localScale = finalHomeScale;
            }

            Paint(finalLit, Color.black, 0f);
            if (finalLight != null)
            {
                finalLight.enabled = false;
            }

            foreach (Face f in faces)
            {
                if (f != null)
                {
                    Paint(f.lit, litColor, 0f);
                    if (f.light != null)
                    {
                        f.light.intensity = 0f;
                    }
                }
            }
        }

        void Paint(Renderer[] renderers, Color color, float intensity)
        {
            if (renderers == null)
            {
                return;
            }

            Color c = color * intensity;
            c.a = 1f;
            foreach (Renderer r in renderers)
            {
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, c);
                r.SetPropertyBlock(block);
            }
        }

        protected override void Tick()
        {
            clock.Update(State, faceEvent);
            float dist = State.Distance;

            // The quiet: the lit faces fade down to a hush before the sting.
            float hush = 1f;
            if (clock.Stage == 1 && faceEvent >= 0)
            {
                float pre = Mathf.Max(0.5f, State.Events[faceEvent].preQuietSeconds);
                hush = 1f - 0.8f * SetUtil.Smooth(clock.T / pre);
            }
            else if (clock.Stage >= 2)
            {
                hush = 0.2f + 0.5f * SetUtil.Smooth((clock.SinceSting - 1.5f) / 2f);
            }

            for (int fi = 0; fi < faces.Length; fi++)
            {
                Face f = faces[fi];
                if (f == null)
                {
                    continue;
                }

                float ramp = SetUtil.Smooth((dist - (f.distance - litLeadMeters)) / Mathf.Max(1f, litRampMeters));
                float k = ramp * hush;
                if (Mathf.Abs(k - lastK[fi]) < 0.004f)
                {
                    continue;
                }

                lastK[fi] = k;
                Paint(f.lit, litColor, litIntensity * k);
                if (f.light != null)
                {
                    f.light.intensity = f.lightBase * k;
                    f.light.enabled = k > 0.02f;
                }
            }

        }

        void LateUpdate()
        {
            if (State == null || !Near)
            {
                return;
            }

            TickFinal();
        }

        void TickFinal()
        {
            if (finalFace == null)
            {
                return;
            }

            Vector3 faceLocal = new Vector3(0f, 0f, 0.07f);
            if (clock.Stage == 0)
            {
                Reset();
                return;
            }

            finalDirty = true;
            if (clock.Stage == 1)
            {
                // The cue: two pinpoints flicker in the dark carving, off to the left.
                float t = State.SimTime;
                bool on = clock.T > 0.7f && Mathf.Sin(t * 23f) * Mathf.Sin(t * 7.3f) > -0.2f;
                SetUtil.SetActive(finalCue, on);
                finalFace.SetPositionAndRotation(finalHomePos, finalHomeRot);
                finalFace.localScale = finalHomeScale;
                if (clock.T > 0.7f)
                {
                    look.Quiet(finalHomePos, 0.3f);
                }

                return;
            }

            float since = clock.SinceSting;
            if (since > 2.2f)
            {
                Reset();
                return;
            }

            SetUtil.SetActive(finalCue, false);
            ScareView.Eye(State, out Vector3 eye, out Quaternion eyeRot);
            Vector3 homeFace = finalHomePos + finalHomeRot * faceLocal * finalHomeScale.x;
            Vector3 endFace = eye + eyeRot * new Vector3(-0.05f, 0f, 1.2f);
            Quaternion endRot = eyeRot * Quaternion.Euler(0f, 180f, 0f);
            Vector3 retreat = -State.CartForward + Vector3.Cross(State.CartUp, State.CartForward) * 0.6f;
            track.Sample(since, homeFace, finalHomeRot, endFace, endRot, retreat, out Vector3 facePos, out Quaternion rot);
            float k = ScareView.EaseOutCubic(since / track.lungeSeconds);
            Vector3 scale = finalHomeScale * Mathf.Lerp(1f, 0.45f, k);
            finalFace.rotation = rot;
            finalFace.localScale = scale;
            finalFace.position = ScareView.RootForFace(facePos, rot, faceLocal, scale.x / Mathf.Max(0.0001f, 1f));

            float glare = ScareView.EaseOutCubic(since / 0.1f) * (1f - SetUtil.Smooth((since - 1.4f) / 0.7f));
            Paint(finalLit, finalColor, finalIntensity * glare);
            if (finalLight != null)
            {
                finalLight.enabled = glare > 0.02f;
                finalLight.intensity = 5f * glare;
            }

            if (since < track.holdSeconds)
            {
                look.Sting(facePos);
            }
            else
            {
                look.Release();
            }
        }

        bool finalDirty;

        void Reset()
        {
            if (!finalDirty)
            {
                return;
            }

            finalDirty = false;
            track.Reset();
            look.Release();
            SetUtil.SetActive(finalCue, false);
            if (finalFace != null && finalHomed)
            {
                finalFace.SetPositionAndRotation(finalHomePos, finalHomeRot);
                finalFace.localScale = finalHomeScale;
            }

            Paint(finalLit, Color.black, 0f);
            if (finalLight != null)
            {
                finalLight.enabled = false;
            }
        }
    }
}
