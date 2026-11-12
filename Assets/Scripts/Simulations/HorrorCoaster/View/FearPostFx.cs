using SimulationLobby.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Drives a runtime-created global URP Volume from the ride state: grain and vignette rise with fear, a short flash-safe
    /// punch on each sting, a slow hue / chromatic / lens "trip" look during the Trip act, and the one permitted white-out.
    /// </summary>
    /// <remarks>
    /// <b>Read-only.</b> All timing comes from <see cref="ICoasterState.SimTime"/> and <see cref="ICoasterState.PhaseTime"/>,
    /// so an offline frame-stepped render reproduces it. Runs in <c>Update</c>.
    /// <para>
    /// <b>Flash safety.</b> A real scare is 2 dark frames (exposure dip, never white) then a 0.3 s chromatic/lens punch, at most +0.25 EV (never a white frame) and a second punch arriving
    /// closer than <c>1 / safetyMaxFlashHz</c> is dropped. Every Trip term is a slow sine (period 6 s or longer). The only
    /// white frame is the single end-of-ride white-out (fade up ~1.2 s, down ~1.5 s), drawn as a full-screen overlay Image.
    /// </para>
    /// Needs the camera's <c>renderPostProcessing</c> on (this component switches it on in Awake).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FearPostFx : MonoBehaviour
    {
        [Header("Flash safety")]
        [Tooltip("A sting punch arriving sooner than 1/this after the previous one is dropped.")]
        [SerializeField, Range(0.5f, 3f)] float safetyMaxFlashHz = 3f;
        [Tooltip("Hard cap on the exposure bump of a punch (EV). Kept small: the sting is a dark dip plus a chromatic punch, never white.")]
        [SerializeField, Range(0f, 1f)] float maxPunchExposure = 0.25f;

        [Header("Base look")]
        [SerializeField] Color vignetteColor = new Color(0.02f, 0f, 0.05f, 1f);
        [SerializeField, Range(0f, 0.6f)] float vignetteBase = 0.22f;
        [SerializeField, Range(0f, 0.6f)] float vignetteFearGain = 0.33f;
        [SerializeField, Range(0f, 0.6f)] float grainBase = 0.12f;
        [SerializeField, Range(0f, 0.9f)] float grainFearGain = 0.55f;
        [SerializeField] float baseExposure = -0.15f;
        [SerializeField, Range(0f, 2f)] float bloomIntensity = 0.3f;

        [Header("Sting punch")]
        [SerializeField, Range(0.15f, 0.35f)] float punchSeconds = 0.3f;
        [Tooltip("A real scare opens with a DARK dip (never white) of this many rendered frames, then the chromatic/lens punch.")]
        [SerializeField, Range(1, 4)] int dipFrames = 2;
        [Tooltip("Exposure drop (EV) during the dip frames.")]
        [SerializeField, Range(0f, 6f)] float dipExposure = 4f;
        [SerializeField, Range(0.01f, 0.08f)] float punchAttackSeconds = 0.03f;

        [Header("Trip look")]
        [Tooltip("Seconds for the hue to make one full cycle during the Trip act.")]
        [SerializeField, Min(4f)] float hueCycleSeconds = 14f;
        [SerializeField, Min(6f)] float chromaPulseSeconds = 6f;
        [SerializeField, Min(6f)] float lensBreathSeconds = 9f;
        [SerializeField, Range(0f, 80f)] float tripSaturation = 38f;
        [Tooltip("Time constant (s) of the Trip look blending in and out.")]
        [SerializeField, Min(0.1f)] float tripBlendSeconds = 1.4f;

        [Header("White-out (once, at the end of the final drop)")]
        [SerializeField] bool autoWhiteOut = true;
        [Tooltip("Seconds after the skull finale sting begins its aftermath before the white-out starts.")]
        [SerializeField, Min(0f)] float whiteOutDelay = 1f;
        [SerializeField, Min(0.2f)] float whiteRiseSeconds = 1.2f;
        [SerializeField, Min(0.2f)] float whiteFallSeconds = 1.5f;

        ICoasterState _state;
        VolumeProfile _profile;
        Volume _volume;
        Vignette _vignette;
        FilmGrain _grain;
        ChromaticAberration _chroma;
        LensDistortion _lens;
        ColorAdjustments _color;
        Bloom _bloom;
        Tonemapping _tone;

        GameObject _overlayRoot;
        Image _whiteImage;

        float _fear;
        float _trip;
        float _hue;
        float _lastSimTime = -1f;
        ScarePhase _lastPhase;
        int _lastIndex = -1;

        float _punchStart = -100f;
        float _punchStrength;
        float _punchLength = 0.2f;
        float _lastPunchStart = -100f;
        int _dipLeft;

        bool _whiteFired;
        float _whiteStart = -100f;

        /// <summary>Punches dropped by the flash limiter this run (for the test log).</summary>
        public int DroppedPunches { get; private set; }

        public void Bind(ICoasterState state)
        {
            _state = state;
            ResetRuntime();
        }

        /// <summary>Starts the single white-out now (no-op when it already ran). The auto trigger calls this.</summary>
        public void TriggerWhiteOut()
        {
            if (_whiteFired || _state == null)
            {
                return;
            }

            _whiteFired = true;
            _whiteStart = _state.SimTime;
        }

        void Awake()
        {
            Camera cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.allowHDR = true;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "CoasterFearProfile (runtime)";
            _vignette = _profile.Add<Vignette>(true);
            _grain = _profile.Add<FilmGrain>(true);
            _chroma = _profile.Add<ChromaticAberration>(true);
            _lens = _profile.Add<LensDistortion>(true);
            _color = _profile.Add<ColorAdjustments>(true);
            _bloom = _profile.Add<Bloom>(true);
            _tone = _profile.Add<Tonemapping>(true);

            _vignette.color.value = vignetteColor;
            _vignette.smoothness.value = 0.5f;
            _vignette.rounded.value = false;
            _grain.type.value = FilmGrainLookup.Medium1;
            _grain.response.value = 0.7f;
            _bloom.threshold.value = 1.05f;
            _bloom.scatter.value = 0.65f;
            _bloom.clamp.value = 6f;
            _tone.mode.value = TonemappingMode.ACES;

            var go = new GameObject("Coaster Post Volume (runtime)");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 1f;
            _volume.sharedProfile = _profile;

            BuildOverlay();
            Apply(0f, 0f, 0f, 0f);
        }

        void OnDestroy()
        {
            if (_profile != null)
            {
                Destroy(_profile);
            }

            if (_volume != null)
            {
                Destroy(_volume.gameObject);
            }

            if (_overlayRoot != null)
            {
                Destroy(_overlayRoot);
            }
        }

        void BuildOverlay()
        {
            _overlayRoot = new GameObject("Coaster WhiteOut (runtime)");
            _overlayRoot.transform.SetParent(transform, false);
            var canvas = _overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var imageGo = new GameObject("White", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(_overlayRoot.transform, false);
            var rt = (RectTransform)imageGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _whiteImage = imageGo.GetComponent<Image>();
            _whiteImage.color = new Color(1f, 1f, 1f, 0f);
            _whiteImage.raycastTarget = false;
            _whiteImage.enabled = false;
        }

        void ResetRuntime()
        {
            _fear = _state != null ? Mathf.Clamp01(_state.Fear01) : 0f;
            _trip = 0f;
            _hue = 0f;
            _lastSimTime = _state != null ? _state.SimTime : -1f;
            _lastPhase = ScarePhase.Idle;
            _lastIndex = -1;
            _punchStart = -100f;
            _punchStrength = 0f;
            _lastPunchStart = -100f;
            _dipLeft = 0;
            _whiteFired = false;
            _whiteStart = -100f;
            DroppedPunches = 0;
        }

        void Update()
        {
            if (_state == null || _profile == null)
            {
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            float simT = _state.SimTime;
            if (simT < _lastSimTime - 0.01f)
            {
                ResetRuntime();
            }

            float dSim = Mathf.Clamp(simT - _lastSimTime, 0f, 0.25f);
            _lastSimTime = simT;

            _fear += (Mathf.Clamp01(_state.Fear01) - _fear) * (1f - Mathf.Exp(-dt / 0.35f));
            if (_fear < 0.0005f && _state.Fear01 <= 0f)
            {
                _fear = 0f;
            }
            float tripTarget = _state.Act == ActId.Trip ? 1f : 0f;
            _trip += (tripTarget - _trip) * (1f - Mathf.Exp(-dt / tripBlendSeconds));
            // An exponential approach never reaches its target; left alone it decays into denormal floats (about 1e-44) some two minutes after the
            // Trip, and URP's lens distortion treats any non-zero intensity as active and renders a blank frame. Snap tiny values to exactly zero.
            if (_trip < 0.0005f && tripTarget <= 0f)
            {
                _trip = 0f;
            }

            // Hue: cycles only inside the Trip (one revolution per hueCycleSeconds, scaled by the blend so it starts gently),
            // then walks back to 0 by the shortest path. Wrapping +180 -> -180 is the same hue, so it is seamless.
            if (_state.Act == ActId.Trip)
            {
                _hue += 360f / hueCycleSeconds * _trip * dSim;
                if (_hue >= 180f)
                {
                    _hue -= 360f;
                }
            }
            else
            {
                _hue = Mathf.MoveTowards(_hue, 0f, 30f * dt);
            }

            DetectSting(simT);
            TryAutoWhiteOut();

            float punch = PunchEnvelope(simT) * _punchStrength;
            float dip = 0f;
            if (_dipLeft > 0)
            {
                dip = 1f;
                _dipLeft--;
            }

            Apply(simT, punch, WhiteAlpha(simT), dip);
        }

        void DetectSting(float simT)
        {
            ScarePhase phase = _state.Phase;
            int index = _state.CurrentEventIndex;
            bool entered = phase == ScarePhase.Sting && (_lastPhase != ScarePhase.Sting || index != _lastIndex);
            _lastPhase = phase;
            _lastIndex = index;
            if (!entered)
            {
                return;
            }

            float strength;
            float length = punchSeconds;
            switch (_state.CurrentEvent.kind)
            {
                case ScareKind.TreatDoor: strength = 0f; break;
                case ScareKind.FakeBat:
                case ScareKind.FakeLantern:
                case ScareKind.FakeWheelchair:
                case ScareKind.FakeEmptySeat: strength = 0.25f; length = 0.15f; break;
                case ScareKind.SkullFinale: strength = 1f; length = 0.35f; break;
                case ScareKind.CrowBurst: strength = 0.9f; break;
                case ScareKind.GraveyardGhost:
                case ScareKind.AsylumFigure: strength = 0.6f; break;
                default: strength = 0.8f; break;
            }

            if (strength <= 0f)
            {
                return;
            }

            if (simT - _lastPunchStart < 1f / Mathf.Max(0.5f, safetyMaxFlashHz))
            {
                DroppedPunches++;
                return;
            }

            _lastPunchStart = simT;
            _punchStart = simT;
            _punchStrength = strength;
            _punchLength = Mathf.Clamp(length, 0.15f, 0.35f);
            _dipLeft = strength >= 0.5f ? dipFrames : 0;
        }

        float PunchEnvelope(float simT)
        {
            float a = simT - _punchStart;
            if (a < 0f || a > _punchLength)
            {
                return 0f;
            }

            float attack = Mathf.Min(punchAttackSeconds, _punchLength * 0.5f);
            if (a < attack)
            {
                return a / attack;
            }

            float u = (a - attack) / Mathf.Max(1e-3f, _punchLength - attack);
            return 1f - CameraSpring.SmoothStep01(u);
        }

        void TryAutoWhiteOut()
        {
            if (!autoWhiteOut || _whiteFired)
            {
                return;
            }

            bool afterSkull = _state.Act == ActId.FinalDrop
                              && _state.Phase == ScarePhase.Aftermath
                              && _state.CurrentEvent.kind == ScareKind.SkullFinale
                              && _state.PhaseTime >= whiteOutDelay;
            if (afterSkull || _state.Act == ActId.StationReturn)
            {
                TriggerWhiteOut();
            }
        }

        float WhiteAlpha(float simT)
        {
            if (!_whiteFired)
            {
                return 0f;
            }

            float a = simT - _whiteStart;
            if (a <= 0f)
            {
                return 0f;
            }

            if (a < whiteRiseSeconds)
            {
                return CameraSpring.SmoothStep01(a / whiteRiseSeconds);
            }

            float f = (a - whiteRiseSeconds) / whiteFallSeconds;
            return f >= 1f ? 0f : 1f - CameraSpring.SmoothStep01(f);
        }

        void Apply(float simT, float punch, float white, float dip)
        {
            float fear = _fear;
            float trip = _trip;
            float tau = 2f * Mathf.PI;
            // Slow-motion grade: ScareSlowMo only ever drops time AFTER the sting's loud hit, so this look arrives after it too. Smooth by
            // construction (the scale itself eases in and out), no flashing: the frame darkens, drains of colour and fringes.
            float slow = Mathf.Clamp01(1f - ScareSlowMo.Current);

            _vignette.intensity.value = Mathf.Clamp(vignetteBase + vignetteFearGain * fear + 0.1f * punch + 0.2f * slow, 0f, 0.75f);
            _grain.intensity.value = Mathf.Clamp01(grainBase + grainFearGain * fear + 0.15f * trip);

            float chroma = 0.04f + 0.08f * fear + 0.85f * punch + 0.3f * slow
                           + trip * (0.2f + 0.12f * Mathf.Sin(tau * simT / chromaPulseSeconds));
            _chroma.intensity.value = Mathf.Clamp(chroma, 0f, 0.9f);

            float lens = -0.4f * punch - 0.14f * slow + trip * 0.22f * Mathf.Sin(tau * simT / lensBreathSeconds);
            // Exactly zero when negligible: the lens override is treated as active for any non-zero value, and a denormal one blanks the frame.
            _lens.intensity.value = Mathf.Abs(lens) < 0.002f ? 0f : Mathf.Clamp(lens, -0.5f, 0.5f);

            _color.hueShift.value = Mathf.Clamp(_hue, -180f, 180f);
            _color.saturation.value = Mathf.Clamp(-12f + tripSaturation * trip - 8f * fear - 42f * slow, -100f, 100f);
            _color.contrast.value = 12f + 14f * fear + 14f * slow;
            _color.postExposure.value = baseExposure + Mathf.Min(maxPunchExposure, 0.3f * punch) - dipExposure * dip - 0.25f * slow;

            _bloom.intensity.value = bloomIntensity + 0.35f * trip;

            if (_whiteImage != null)
            {
                bool on = white > 0.001f;
                if (_whiteImage.enabled != on)
                {
                    _whiteImage.enabled = on;
                }

                if (on)
                {
                    _whiteImage.color = new Color(1f, 1f, 1f, white);
                }
            }
        }
    }
}
