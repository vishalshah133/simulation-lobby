using System.Collections.Generic;
using System.Globalization;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Simulations.HydraulicPress.Kernel;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.HydraulicPress
{
    /// <summary>
    /// Draws and sounds a <see cref="HydraulicPressSimulation"/>: the chamber, the press (plate, piston rod,
    /// housing, tonnage decal), every ball in one draw call, pop burst rings, camera shake, the caption HUD,
    /// the result ladder and the audio. Reads the run in <c>LateUpdate</c> and never writes to it. The only
    /// randomness is cosmetic (hue offset, pop pitch jitter) and comes from local generators seeded from the
    /// run seed.
    /// </summary>
    [RequireComponent(typeof(HydraulicPressSimulation))]
    public sealed class HydraulicPressPresenter : MonoBehaviour
    {
        // Geometry shared with the scene builder (it sizes the frame from these).
        public const float WallThickness = 0.6f;
        public const float FloorThickness = 0.35f;
        public const float PlateThickness = 0.9f;
        public const float StripeHeight = 0.26f;
        public const float RodWidth = 0.7f;
        public const float HousingWidth = 2.4f;
        public const float HousingHeight = 0.55f;
        public const float HousingGap = 1.1f;

        /// <summary>Everything the scene builder creates and hands over. The presenter positions, sizes and colours it.</summary>
        [System.Serializable]
        public sealed class SceneRig
        {
            public Transform cameraTransform;
            public SpriteRenderer backPanel;
            public SpriteRenderer wallLeft;
            public SpriteRenderer wallRight;
            public SpriteRenderer edgeLeft;
            public SpriteRenderer edgeRight;
            public SpriteRenderer floorBody;
            public SpriteRenderer floorLine;
            public Transform plateRoot;
            public SpriteRenderer plateBody;
            public SpriteRenderer plateStripes;
            public SpriteRenderer plateShine;
            public TextMeshPro plateLabel;
            public SpriteRenderer rod;
            public SpriteRenderer housing;
            public SpriteRenderer housingShine;
            public TMP_Text subCaption;
            public float floorY;
            public float pixelsPerUnit = 80f;
        }

        [Header("Wiring (set by the scene builder via Bind)")]
        [SerializeField] HydraulicPressSimulation _simulation;
        [SerializeField] CaptionHud _hud;
        [SerializeField] ResultLadderHud _ladder;
        [Tooltip("Optional second column: rows from the halfway round on go here.")]
        [SerializeField] ResultLadderHud _ladderRight;
        [SerializeField] SceneRig _rig = new SceneRig();

        [Header("Balls")]
        [Tooltip("Hue step per generation (0..1). 0.09 gives a full colour wheel in about 11 generations.")]
        [Range(0.02f, 0.25f)] [SerializeField] float _hueStep = 0.09f;

        [Range(0.3f, 1f)] [SerializeField] float _ballSaturation = 0.78f;

        [Tooltip("How much shorter a ball draws at full squeeze (it widens to keep its area).")]
        [Range(0f, 0.6f)] [SerializeField] float _maxSquash = 0.35f;

        [Tooltip("How far a ball whitens at full squeeze.")]
        [Range(0f, 1f)] [SerializeField] float _squeezeFlash = 0.45f;

        [Tooltip("Per-ball brightness variation (by id, deterministic) so a packed pile has texture.")]
        [Range(0f, 0.3f)] [SerializeField] float _brightnessVariation = 0.12f;

        [SerializeField] int _ballSortingOrder = 10;

        [Header("Pop bursts")]
        [Tooltip("Most burst rings spawned per rendered frame.")]
        [Range(1, 40)] [SerializeField] int _maxBurstsPerFrame = 12;

        [Range(0.05f, 0.8f)] [SerializeField] float _burstSeconds = 0.28f;

        [Tooltip("Ring size at the end of its life, as a multiple of the popped ball's radius.")]
        [Range(1.2f, 4f)] [SerializeField] float _burstGrowth = 2.4f;

        [Tooltip("Smallest burst ring radius, world units, so a sand-sized pop still shows.")]
        [Range(0.02f, 0.4f)] [SerializeField] float _burstMinRadius = 0.09f;

        [SerializeField] int _burstCapacity = 96;

        [Header("Camera shake (presentation only)")]
        [Tooltip("Shake when the press first lands on the pile each round, world units.")]
        [Range(0f, 0.3f)] [SerializeField] float _landShake = 0.05f;

        [Tooltip("Heavier shake when the press stops.")]
        [Range(0f, 0.5f)] [SerializeField] float _stopShake = 0.12f;

        [Tooltip("Extra stop shake per round index (later rounds hit harder).")]
        [Range(0f, 0.5f)] [SerializeField] float _stopShakePerRound = 0.15f;

        [Range(0.05f, 0.6f)] [SerializeField] float _shakeDecaySeconds = 0.22f;

        [Header("Chamber colours")]
        [SerializeField] Color _backColor = new Color(0.07f, 0.08f, 0.11f, 0.92f);
        [SerializeField] Color _wallColor = new Color(0.2f, 0.23f, 0.29f, 1f);
        [SerializeField] Color _edgeColor = new Color(0.62f, 0.68f, 0.78f, 0.9f);
        [SerializeField] Color _floorColor = new Color(0.17f, 0.19f, 0.25f, 1f);
        [SerializeField] Color _plateColor = new Color(0.34f, 0.37f, 0.44f, 1f);
        [SerializeField] Color _plateShineColor = new Color(1f, 1f, 1f, 0.28f);
        [SerializeField] Color _rodColor = new Color(0.82f, 0.86f, 0.92f, 1f);
        [SerializeField] Color _housingColor = new Color(0.3f, 0.33f, 0.4f, 1f);
        [SerializeField] Color _labelColor = new Color(1f, 0.9f, 0.45f, 1f);

        [Header("Audio")]
        [Range(1, 3)] [SerializeField] int _maxPopsPerFrame = 3;
        [Range(2, 10)] [SerializeField] int _popVoices = 6;
        [Range(0f, 1f)] [SerializeField] float _popVolume = 0.85f;
        [Tooltip("Seeded random pitch variation per pop, plus or minus this fraction.")]
        [Range(0f, 0.15f)] [SerializeField] float _popPitchJitter = 0.04f;
        [Tooltip("Pop pitch rise per generation, in octaves.")]
        [Range(0f, 0.3f)] [SerializeField] float _popOctavesPerGeneration = 0.1f;
        [Tooltip("Pops per second (smoothed) at which the crackle bed is at its loudest.")]
        [Min(10f)] [SerializeField] float _crackleFullRate = 400f;
        [Range(0f, 1f)] [SerializeField] float _crackleVolume = 0.5f;
        [Range(0.02f, 0.5f)] [SerializeField] float _rateSmoothSeconds = 0.12f;
        [Range(0f, 1f)] [SerializeField] float _humVolume = 0.32f;
        [Tooltip("Plate speed (units/s) that counts as 'moving' for the hum.")]
        [Min(0.01f)] [SerializeField] float _humMoveThreshold = 0.08f;
        [Range(0f, 1f)] [SerializeField] float _landThudVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] float _stopThudVolume = 1f;
        [Range(0f, 1f)] [SerializeField] float _resultClinkVolume = 0.9f;
        [Range(0f, 1f)] [SerializeField] float _ladderClinkVolume = 0.35f;
        [Range(0.8f, 2f)] [SerializeField] float _ladderClinkPitch = 1.5f;
        [Range(0f, 1f)] [SerializeField] float _whooshVolume = 0.55f;

        [Header("HUD")]
        [Range(0f, 1.5f)] [SerializeField] float _ladderDelaySeconds = 0.4f;
        [SerializeField] Color _digitTint = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] Color _resultTint = new Color(1f, 0.82f, 0.25f, 1f);
        [Range(0.4f, 0.9f)] [SerializeField] float _counterCellEm = 0.6f;
        [Tooltip("The counter is padded with invisible digits to this many cells so the line doesn't shift.")]
        [Range(1, 6)] [SerializeField] int _counterCells = 4;

        // ---- run-time state
        System.Random _cosmetic;
        bool _seeded;
        bool _started;
        float _hueOffset;
        Color32[] _palette;

        Material _ballMaterial;
        Material _ringMaterial;
        Texture2D _ballTexture;
        Texture2D _ringTexture;
        PressQuadBatch _balls;
        PressPopBursts _bursts;

        long _popCursor;
        float _popRate;
        PressKernel _lastKernel;
        PressKernel _transitionOldKernel;

        Vector3 _cameraBase;
        float _shake;
        float _shakeAmp;

        float _lastPressY;
        bool _havePressY;
        bool _landed;
        float _humLevel;
        string _plateLabelShown;

        AudioSource[] _voices;
        int _nextVoice;
        AudioSource _sfx;
        AudioSource _ladderSfx;
        AudioSource _hum;
        AudioSource _crackle;
        AudioClip _pop;
        AudioClip _clink;
        AudioClip _whoosh;
        AudioClip _landThud;
        AudioClip _stopThud;

        PressPhase _prevPhase = PressPhase.Done;
        int _prevRound = -1;
        bool _prevFinished;

        int _ladderPushed;
        readonly List<float> _ladderDue = new List<float>(8);
        bool _hudInitialised;
        string _footerShown;
        bool _endLineSet;
        bool _glyphsWarmed;

        void Reset()
        {
            _simulation = GetComponent<HydraulicPressSimulation>();
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(HydraulicPressSimulation simulation, CaptionHud hud, ResultLadderHud ladder, ResultLadderHud ladderRight,
                         SceneRig rig)
        {
            _simulation = simulation;
            _hud = hud;
            _ladder = ladder;
            _ladderRight = ladderRight;
            _rig = rig;
        }

        void Start()
        {
            BuildBalls();
            BuildAudio();
            SetupStaticArt();
            if (_rig != null && _rig.cameraTransform != null)
            {
                _cameraBase = _rig.cameraTransform.position;
            }

            _started = true;
        }

        void OnDestroy()
        {
            _balls?.Destroy();
            if (_ballMaterial != null) Destroy(_ballMaterial);
            if (_ringMaterial != null) Destroy(_ringMaterial);
            if (_ballTexture != null) Destroy(_ballTexture);
            if (_ringTexture != null) Destroy(_ringTexture);
        }

        void BuildBalls()
        {
            _ballTexture = PressTextures.CreateBall();
            _ringTexture = PressTextures.CreateRing();
            _ballMaterial = PressTextures.CreateMaterial(_ballTexture, "PressBalls");
            _ringMaterial = PressTextures.CreateMaterial(_ringTexture, "PressRings");

            var root = new GameObject("Press Balls");
            root.transform.SetParent(transform, false);
            _balls = new PressQuadBatch(root.transform, "Balls (one draw call)", _ballMaterial, _ballSortingOrder, 3000);

            var ringRoot = new GameObject("Press Bursts");
            ringRoot.transform.SetParent(transform, false);
            var ringBatch = new PressQuadBatch(ringRoot.transform, "Burst Rings", _ringMaterial, _ballSortingOrder + 10, 128);
            _bursts = new PressPopBursts(ringBatch, _burstCapacity, _burstGrowth, _burstMinRadius);
        }

        void EnsureSeeded()
        {
            if (_seeded)
            {
                return;
            }

            int seed = _simulation != null ? _simulation.SeedUsed : 0;
            if (seed == 0)
            {
                var runner = GetComponent<SimulationRunner>();
                seed = runner != null ? runner.seed : 0;
            }

            _cosmetic = new System.Random(seed ^ 0x5bd1e995);
            _hueOffset = (float)_cosmetic.NextDouble();

            _palette = new Color32[40];
            for (int g = 0; g < _palette.Length; g++)
            {
                float hue = Mathf.Repeat(_hueOffset + g * _hueStep, 1f);
                _palette[g] = Color.HSVToRGB(hue, _ballSaturation, 1f);
            }

            _seeded = true;
        }

        void SetupStaticArt()
        {
            if (_rig == null || _simulation == null || _simulation.Config == null)
            {
                return;
            }

            HydraulicPressConfig config = _simulation.Config;
            float halfW = config.chamberWidth * 0.5f;
            float height = config.chamberHeight;
            float floorY = _rig.floorY;
            float topY = floorY + height + HousingGap + HousingHeight;
            float wallBottom = floorY - FloorThickness;
            float wallHeight = topY - wallBottom;

            Place(_rig.backPanel, 0f, floorY + (height + HousingGap) * 0.5f, config.chamberWidth, height + HousingGap, _backColor);
            Place(_rig.wallLeft, -(halfW + WallThickness * 0.5f), wallBottom + wallHeight * 0.5f, WallThickness, wallHeight, _wallColor);
            Place(_rig.wallRight, halfW + WallThickness * 0.5f, wallBottom + wallHeight * 0.5f, WallThickness, wallHeight, _wallColor);
            Place(_rig.edgeLeft, -halfW - 0.03f, wallBottom + wallHeight * 0.5f, 0.07f, wallHeight, _edgeColor);
            Place(_rig.edgeRight, halfW + 0.03f, wallBottom + wallHeight * 0.5f, 0.07f, wallHeight, _edgeColor);
            Place(_rig.floorBody, 0f, floorY - FloorThickness * 0.5f, config.chamberWidth + WallThickness * 2f, FloorThickness, _floorColor);
            Place(_rig.floorLine, 0f, floorY + 0.02f, config.chamberWidth, 0.07f, _edgeColor);

            float plateWidth = config.chamberWidth - 0.04f;
            if (_rig.plateBody != null)
            {
                _rig.plateBody.transform.localPosition = Vector3.zero;
                _rig.plateBody.transform.localScale = new Vector3(plateWidth, PlateThickness, 1f);
                _rig.plateBody.color = _plateColor;
            }

            if (_rig.plateStripes != null)
            {
                _rig.plateStripes.drawMode = SpriteDrawMode.Tiled;
                _rig.plateStripes.size = new Vector2(plateWidth, StripeHeight);
                _rig.plateStripes.transform.localPosition = new Vector3(0f, -PlateThickness * 0.5f + StripeHeight * 0.5f, 0f);
                _rig.plateStripes.transform.localScale = Vector3.one;
            }

            if (_rig.plateShine != null)
            {
                _rig.plateShine.transform.localPosition = new Vector3(0f, PlateThickness * 0.5f - 0.05f, 0f);
                _rig.plateShine.transform.localScale = new Vector3(plateWidth, 0.09f, 1f);
                _rig.plateShine.color = _plateShineColor;
            }

            if (_rig.plateLabel != null)
            {
                _rig.plateLabel.transform.localPosition = new Vector3(0f, StripeHeight * 0.35f, 0f);
                _rig.plateLabel.rectTransform.sizeDelta = new Vector2(plateWidth * 0.8f, PlateThickness - StripeHeight - 0.1f);
                _rig.plateLabel.color = _labelColor;
            }

            float housingBottom = floorY + height + HousingGap;
            Place(_rig.housing, 0f, housingBottom + HousingHeight * 0.5f, HousingWidth, HousingHeight, _housingColor);
            Place(_rig.housingShine, -HousingWidth * 0.25f, housingBottom + HousingHeight * 0.5f, HousingWidth * 0.18f, HousingHeight * 0.9f,
                  new Color(1f, 1f, 1f, 0.25f));
            if (_rig.rod != null)
            {
                _rig.rod.color = _rodColor;
            }

            if (_rig.subCaption != null)
            {
                _rig.subCaption.text = config.subCaption ?? string.Empty;
            }
        }

        static void Place(SpriteRenderer renderer, float x, float y, float width, float height, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.transform.position = new Vector3(x, y, 0f);
            renderer.transform.localScale = new Vector3(width, height, 1f);
            renderer.color = color;
        }

        void BuildAudio()
        {
            _pop = HydraulicPressAudio.CreatePop(7);
            _clink = ProceduralToneBank.CreateGlassClink(1f, 1.4f, 3, "PressClink");
            _whoosh = ProceduralToneBank.CreateWhoosh(1.1f, 2, "PressWhoosh");
            _landThud = ProceduralToneBank.CreateImpactThud(0.9f, 0.28f, 5, "PressLand");
            _stopThud = ProceduralToneBank.CreateImpactThud(0.6f, 0.5f, 6, "PressStop");

            _voices = new AudioSource[_popVoices];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = AddSource(_popVolume);
            }

            _sfx = AddSource(1f);
            _ladderSfx = AddSource(1f);
            _ladderSfx.pitch = _ladderClinkPitch;

            _hum = AddSource(0f);
            _hum.clip = ProceduralToneBank.CreateRumbleLoop(2f, 11, "PressHum");
            _hum.loop = true;
            _hum.pitch = 0.7f;
            _hum.Play();

            _crackle = AddSource(0f);
            _crackle.clip = HydraulicPressAudio.CreateCrackleLoop(21);
            _crackle.loop = true;
            _crackle.Play();
        }

        AudioSource AddSource(float volume)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            return source;
        }

        void LateUpdate()
        {
            if (!_started || _simulation == null || _simulation.Config == null || _rig == null)
            {
                return;
            }

            EnsureSeeded();
            if (!_glyphsWarmed)
            {
                _glyphsWarmed = true;
                WarmGlyphs();
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            HydraulicPressConfig config = _simulation.Config;
            PressPhase phase = _simulation.Phase;
            PressKernel kernel = _simulation.Kernel;

            if (phase == PressPhase.Transition && _prevPhase != PressPhase.Transition)
            {
                _transitionOldKernel = kernel;
            }

            if (_simulation.RoundIndex != _prevRound)
            {
                _landed = false;
                _bursts?.Clear();
            }

            DrawBalls(kernel, phase);
            UpdatePress(dt);
            UpdatePops(dt);
            UpdateCues(phase, kernel);
            UpdateAudioBeds(dt);
            UpdateShake(dt);
            UpdateHud(config, phase);
            UpdateLadder(config, phase, dt);

            _bursts?.Tick(dt);

            _lastKernel = kernel;
            _prevPhase = phase;
            _prevRound = _simulation.RoundIndex;
            _prevFinished = _simulation.RoundFinished;
        }

        // ---------------------------------------------------------------- balls: one mesh, one draw call

        void DrawBalls(PressKernel kernel, PressPhase phase)
        {
            if (_balls == null)
            {
                return;
            }

            _balls.Begin();
            if (kernel != null && kernel.Count > 0)
            {
                // During the transition the old pile fades and sinks away; a fresh kernel (the new single ball) is drawn in full.
                float fade = 1f;
                if (phase == PressPhase.Transition && kernel == _transitionOldKernel)
                {
                    float t = Mathf.Clamp01(_simulation.PhaseProgress / 0.5f);
                    fade = 1f - t * t * (3f - 2f * t);
                }

                if (fade > 0.002f)
                {
                    int count = kernel.Count;
                    double[] xs = kernel.X;
                    double[] ys = kernel.Y;
                    double[] rs = kernel.R;
                    double[] squeeze = kernel.Squeeze;
                    int[] generation = kernel.Generation;
                    float floorY = _rig.floorY;
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(fade * 1.2f));

                    for (int i = 0; i < count; i++)
                    {
                        float r = (float)rs[i] * fade;
                        float s = (float)squeeze[i];
                        float h = 1f - _maxSquash * s;
                        float hh = r * h;
                        float hw = r / h;
                        float cx = (float)xs[i];
                        float cy = floorY + (float)ys[i] - r * (1f - h) * 0.5f;

                        Color32 baseColor = _palette[Mathf.Clamp(generation[i], 0, _palette.Length - 1)];
                        // Deterministic per-id brightness variation.
                        uint hash = (uint)i * 2654435761u;
                        float variation = 1f - _brightnessVariation * ((hash >> 24) / 255f);
                        float white = _squeezeFlash * s;
                        byte cr = Shade(baseColor.r, variation, white);
                        byte cg = Shade(baseColor.g, variation, white);
                        byte cb = Shade(baseColor.b, variation, white);
                        _balls.Add(cx, cy, hw, hh, new Color32(cr, cg, cb, alpha));
                    }
                }
            }

            _balls.End();
        }

        static byte Shade(byte channel, float variation, float white)
        {
            float v = channel / 255f * variation;
            v = Mathf.Lerp(v, 1f, white);
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
        }

        // ---------------------------------------------------------------- press

        void UpdatePress(float dt)
        {
            if (_rig.plateRoot == null)
            {
                return;
            }

            HydraulicPressConfig config = _simulation.Config;
            float floorY = _rig.floorY;
            float pressY = _simulation.DisplayPressY;
            float plateCentre = floorY + pressY + PlateThickness * 0.5f;
            _rig.plateRoot.position = new Vector3(0f, plateCentre, 0f);

            if (_rig.rod != null)
            {
                float rodBottom = floorY + pressY + PlateThickness;
                float housingBottom = floorY + config.chamberHeight + HousingGap;
                float length = Mathf.Max(0.05f, housingBottom - rodBottom + 0.05f);
                _rig.rod.transform.position = new Vector3(0f, rodBottom + length * 0.5f - 0.05f, 0f);
                _rig.rod.transform.localScale = new Vector3(RodWidth, length, 1f);
            }

            if (_rig.plateLabel != null)
            {
                string label = _simulation.PressLabel ?? string.Empty;
                if (label != _plateLabelShown)
                {
                    _plateLabelShown = label;
                    _rig.plateLabel.text = label;
                }
            }

            float speed = _havePressY ? Mathf.Abs(pressY - _lastPressY) / dt : 0f;
            _lastPressY = pressY;
            _havePressY = true;
            float moving = speed > _humMoveThreshold ? 1f : 0f;
            _humLevel = Mathf.MoveTowards(_humLevel, moving, dt / 0.12f);
            if (_hum != null)
            {
                _hum.volume = _humLevel * _humVolume;
                _hum.pitch = 0.7f + Mathf.Min(speed, 4f) * 0.08f;
            }
        }

        // ---------------------------------------------------------------- pops: rings and sounds

        void UpdatePops(float dt)
        {
            long total = _simulation.PopSerial;
            if (total < _popCursor)
            {
                _popCursor = total;
            }

            long fresh = total - _popCursor;
            if (fresh > 4096)
            {
                _popCursor = total - 4096;
                fresh = 4096;
            }

            float blend = 1f - Mathf.Exp(-dt / _rateSmoothSeconds);
            _popRate += (fresh / dt - _popRate) * blend;
            if (_popRate < 0.5f)
            {
                _popRate = 0f;
            }

            if (fresh > 0)
            {
                long burstStride = (fresh + _maxBurstsPerFrame - 1) / _maxBurstsPerFrame;
                long soundStride = (fresh + _maxPopsPerFrame - 1) / _maxPopsPerFrame;
                // Rare pops carry the full volume; a flood is carried by the crackle bed.
                float volume = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01((fresh - 1f) / 20f));
                int sounds = 0;
                for (long serial = _popCursor; serial < total; serial++)
                {
                    long index = serial - _popCursor;
                    bool burst = index % burstStride == 0;
                    bool sound = index % soundStride == 0 && sounds < _maxPopsPerFrame;
                    if (!burst && !sound)
                    {
                        continue;
                    }

                    if (!_simulation.TryGetPop(serial, out PopEvent pop))
                    {
                        continue;
                    }

                    if (burst && _bursts != null)
                    {
                        _bursts.Spawn((float)pop.X, _rig.floorY + (float)pop.Y, (float)pop.Radius * 1.1f, _burstSeconds);
                    }

                    if (sound)
                    {
                        PlayPop(pop.Generation, volume);
                        sounds++;
                    }
                }
            }

            _popCursor = total;
        }

        void PlayPop(int generation, float volume)
        {
            if (_voices == null || _voices.Length == 0)
            {
                return;
            }

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            float jitter = ((float)_cosmetic.NextDouble() * 2f - 1f) * _popPitchJitter;
            voice.pitch = HydraulicPressAudio.PopPitch(generation, _popOctavesPerGeneration) * (1f + jitter);
            voice.PlayOneShot(_pop, volume);
        }

        // ---------------------------------------------------------------- one-shot cues

        void UpdateCues(PressPhase phase, PressKernel kernel)
        {
            if (_sfx == null)
            {
                return;
            }

            if (phase == PressPhase.Transition && _prevPhase != PressPhase.Transition)
            {
                _sfx.PlayOneShot(_whoosh, _whooshVolume);
            }

            if (phase == PressPhase.Run && !_landed && kernel != null && kernel.PressResting)
            {
                _landed = true;
                _sfx.PlayOneShot(_landThud, _landThudVolume);
                _shake = 1f;
                _shakeAmp = _landShake;
            }

            if (phase == PressPhase.Hold)
            {
                _landed = false;
            }

            bool finished = _simulation.RoundFinished;
            if (finished && (!_prevFinished || _simulation.RoundIndex != _prevRound))
            {
                _sfx.PlayOneShot(_stopThud, _stopThudVolume);
                _sfx.PlayOneShot(_clink, _resultClinkVolume);
                _shake = 1f;
                _shakeAmp = _stopShake * (1f + _stopShakePerRound * Mathf.Max(0, _simulation.RoundIndex));
            }
        }

        void UpdateAudioBeds(float dt)
        {
            if (_crackle == null)
            {
                return;
            }

            // The crackle only joins in when more pops than the 3 voices can carry are landing.
            float flood = Mathf.Clamp01((_popRate - 60f) / Mathf.Max(1f, _crackleFullRate - 60f));
            float target = flood * _crackleVolume;
            _crackle.volume = Mathf.MoveTowards(_crackle.volume, target, dt / 0.08f * _crackleVolume);
        }

        // ---------------------------------------------------------------- camera shake

        void UpdateShake(float dt)
        {
            if (_rig.cameraTransform == null)
            {
                return;
            }

            if (_shake > 0f)
            {
                _shake = Mathf.Max(0f, _shake - dt / _shakeDecaySeconds);
            }

            float amp = _shake * _shake * _shakeAmp;
            float t = Time.time;
            var offset = new Vector3(Mathf.Sin(t * 83.1f), Mathf.Cos(t * 71.7f), 0f) * amp;
            _rig.cameraTransform.position = _cameraBase + offset;
        }

        // ---------------------------------------------------------------- HUD

        /// <summary>The arrow and the multiplication sign are not in the static font atlas; warm the dynamic fallback.</summary>
        void WarmGlyphs()
        {
            const string glyphs = "→×…=.0123456789 ";
            try
            {
                TMP_FontAsset font = _rig.plateLabel != null ? _rig.plateLabel.font : null;
                if (font == null)
                {
                    return;
                }

                font.TryAddCharacters(glyphs, out _);
                if (font.fallbackFontAssetTable != null)
                {
                    for (int i = 0; i < font.fallbackFontAssetTable.Count; i++)
                    {
                        TMP_FontAsset fallback = font.fallbackFontAssetTable[i];
                        if (fallback != null)
                        {
                            fallback.TryAddCharacters(glyphs, out _);
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                // Cosmetic warm-up only.
            }
        }

        void UpdateHud(HydraulicPressConfig config, PressPhase phase)
        {
            if (_hud == null)
            {
                return;
            }

            if (!_hudInitialised)
            {
                _hudInitialised = true;
                // The question is the big line and is up from frame zero; the title sits small under it.
                _hud.SetCaption(config.question, false);
                _hud.SetSubline(config.title);
            }

            string footer = BuildFooter(config, phase);
            if (footer != _footerShown)
            {
                _footerShown = footer;
                _hud.SetFooter(footer);
            }
        }

        string BuildFooter(HydraulicPressConfig config, PressPhase phase)
        {
            PressRound round = _simulation.CurrentRound;
            int balls = _simulation.BallCount;
            string line;
            switch (phase)
            {
                case PressPhase.Hold:
                case PressPhase.Transition:
                    line = Caption(round, config);
                    break;
                case PressPhase.Run:
                case PressPhase.Result:
                    if (_simulation.RoundFinished)
                    {
                        string result = (round.result ?? string.Empty).Replace("{n}", balls.ToString(CultureInfo.InvariantCulture));
                        line = Tint(result, _resultTint);
                    }
                    else
                    {
                        line = Caption(round, config);
                    }

                    break;
                case PressPhase.EndCard:
                    return Tint("<size=80%>" + (config.endLine ?? string.Empty) + "</size>", _resultTint);
                default:
                    return string.Empty;
            }

            return line + "\n" + CounterLine(config, balls);
        }

        static string Caption(PressRound round, HydraulicPressConfig config)
        {
            return !string.IsNullOrEmpty(round.caption) ? round.caption : config.holdCaption;
        }

        /// <summary>"BALLS 128" with the digits in fixed-width cells, padded with invisible cells so the line doesn't shift sideways.</summary>
        string CounterLine(HydraulicPressConfig config, int count)
        {
            string digits = count.ToString(CultureInfo.InvariantCulture);
            int width = Mathf.Max(digits.Length, _counterCells);
            string pad = new string('0', width - digits.Length);
            string cell = _counterCellEm.ToString("0.###", CultureInfo.InvariantCulture);
            string text = config.counterLabel + " <mspace=" + cell + "em>" + digits + "<alpha=#00>" + pad + "</mspace>";
            return "<size=150%><b>" + text + "</b></size>";
        }

        static string Tint(string text, Color color)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
        }

        // ---------------------------------------------------------------- ladder

        void UpdateLadder(HydraulicPressConfig config, PressPhase phase, float dt)
        {
            if (_ladder == null)
            {
                return;
            }

            IReadOnlyList<int> done = _simulation.CompletedCounts;
            while (_ladderDue.Count + _ladderPushed < done.Count)
            {
                _ladderDue.Add(_ladderDelaySeconds);
            }

            // On the end card nothing may still be waiting: every row is up before the footnote fades in.
            bool flush = phase == PressPhase.EndCard || phase == PressPhase.Done;
            for (int i = 0; i < _ladderDue.Count; i++)
            {
                _ladderDue[i] -= dt;
            }

            while (_ladderDue.Count > 0 && (flush || _ladderDue[0] <= 0f))
            {
                _ladderDue.RemoveAt(0);
                LadderFor(config, _ladderPushed).Push(RowText(config, _ladderPushed, done[_ladderPushed]));
                _ladderPushed++;
                _ladderSfx?.PlayOneShot(_clink, _ladderClinkVolume);
            }

            if (phase == PressPhase.EndCard && !_endLineSet)
            {
                _endLineSet = true;
                if (!string.IsNullOrEmpty(config.ladderFootnote))
                {
                    _ladder.SetFootnote(config.ladderFootnote);
                }
            }
        }

        /// <summary>Rows fill the left column first; the right column (if bound) takes the second half of the rounds.</summary>
        ResultLadderHud LadderFor(HydraulicPressConfig config, int index)
        {
            int rounds = config.rounds != null ? config.rounds.Length : 0;
            int perColumn = (rounds + 1) / 2;
            return _ladderRight != null && index >= perColumn ? _ladderRight : _ladder;
        }

        string RowText(HydraulicPressConfig config, int index, int count)
        {
            string label = config.rounds != null && index < config.rounds.Length ? config.rounds[index].pressLabel : string.Empty;
            return label + " → <color=#" + ColorUtility.ToHtmlStringRGB(_digitTint) + ">" +
                   count.ToString(CultureInfo.InvariantCulture) + "</color>";
        }
    }
}
