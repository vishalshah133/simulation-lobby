using System.Collections.Generic;
using System.Globalization;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Simulations.PiClacks.Kernel;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.PiClacks
{
    /// <summary>
    /// Draws and sounds a <see cref="PiClacksSimulation"/>: the wall, the floor, two blocks with mass decals,
    /// contact sparks, the seam glow, the caption HUD, the result ladder and the audio. Reads the run in
    /// <c>LateUpdate</c> and never writes to it. The only randomness is cosmetic (palette, spark height, click
    /// pitch jitter) and comes from local generators seeded from the run seed.
    /// </summary>
    [RequireComponent(typeof(PiClacksSimulation))]
    public sealed class PiClacksPresenter : MonoBehaviour
    {
        /// <summary>One drawn block: a root moved by the presenter and three children built by the scene builder.</summary>
        [System.Serializable]
        public sealed class BlockView
        {
            public Transform root;
            public SpriteRenderer body;
            public SpriteRenderer shadow;
            public TMP_Text label;
        }

        /// <summary>Everything the scene builder creates and hands over. The presenter positions, sizes and colours it.</summary>
        [System.Serializable]
        public sealed class SceneRig
        {
            public BlockView small = new BlockView();
            public BlockView big = new BlockView();
            public SpriteRenderer wallBar;
            public SpriteRenderer wallHatch;
            public SpriteRenderer wallEdge;
            public SpriteRenderer floorLine;
            public SpriteRenderer floorFill;
            public SpriteRenderer seamGlow;
            public Sprite glowSprite;
            public Sprite squareSprite;
            public float wallFaceX;
            public float floorY;
            public float topY;
        }

        [Header("Wiring (set by the scene builder via Bind)")]
        [SerializeField] PiClacksSimulation _simulation;
        [SerializeField] CaptionHud _hud;
        [SerializeField] ResultLadderHud _ladder;
        [SerializeField] SceneRig _rig = new SceneRig();

        [Header("Look")]
        [Tooltip("Saturation of the two block colours.")]
        [Range(0.3f, 1f)] [SerializeField] float _blockSaturation = 0.85f;

        [Tooltip("Hue wobble applied to the seeded colour pair, plus or minus (0..1 hue).")]
        [Range(0f, 0.08f)] [SerializeField] float _hueJitter = 0.025f;

        [Tooltip("Mass decal colour. Dark, because both block colours are bright.")]
        [SerializeField] Color _labelColor = new Color(0.07f, 0.08f, 0.12f, 1f);

        [Tooltip("Drop shadow offset as a fraction of the block's side (x right, y down).")]
        [SerializeField] Vector2 _shadowOffset = new Vector2(0.07f, -0.09f);

        [Range(0f, 1f)] [SerializeField] float _shadowAlpha = 0.45f;

        [SerializeField] Color _wallColor = new Color(0.55f, 0.6f, 0.7f, 1f);
        [SerializeField] Color _hatchColor = new Color(0.1f, 0.12f, 0.18f, 0.55f);
        [SerializeField] Color _wallEdgeColor = new Color(0.95f, 0.97f, 1f, 1f);
        [SerializeField] Color _floorLineColor = new Color(0.85f, 0.9f, 1f, 0.9f);
        [SerializeField] Color _floorFillColor = new Color(1f, 1f, 1f, 0.06f);

        [Header("Sparks and seam glow")]
        [Tooltip("Most sparks spawned per rendered frame. A buzz is shown by the seam glow, not a spark storm.")]
        [Range(1, 12)] [SerializeField] int _maxSparksPerFrame = 4;

        [Tooltip("Spark lifetime, seconds.")]
        [Range(0.04f, 0.5f)] [SerializeField] float _sparkSeconds = 0.16f;

        [Tooltip("Spark glow diameter, world units.")]
        [Range(0.2f, 2f)] [SerializeField] float _sparkSize = 0.9f;

        [SerializeField] int _sparkSortingOrder = 30;

        [Tooltip("Seam glow brightness at its peak.")]
        [Range(0f, 1f)] [SerializeField] float _seamMaxAlpha = 0.85f;

        [Tooltip("The seam starts to glow at this clack rate (log10 of clacks per second)...")]
        [SerializeField] float _seamLowLog = 1.3f;

        [Tooltip("...and is at full brightness at this one.")]
        [SerializeField] float _seamHighLog = 4.5f;

        [Tooltip("Seam glow brightness follows the rate with this lag, seconds.")]
        [Range(0.01f, 0.5f)] [SerializeField] float _seamSmoothSeconds = 0.06f;

        [Header("Audio: clicks")]
        [Tooltip("Most click sounds per rendered frame.")]
        [Range(1, 6)] [SerializeField] int _maxClicksPerFrame = 3;

        [Tooltip("Seeded random pitch variation per click, plus or minus this fraction.")]
        [Range(0f, 0.2f)] [SerializeField] float _clickPitchJitter = 0.03f;

        [Tooltip("Pitch of the wall click; the block-on-block click is higher so the two read apart.")]
        [Range(0.5f, 1.5f)] [SerializeField] float _wallClickPitch = 0.8f;

        [Range(0.5f, 2f)] [SerializeField] float _blockClickPitch = 1.15f;

        [Range(0f, 1f)] [SerializeField] float _clickVolume = 0.8f;

        [Range(2, 12)] [SerializeField] int _clickVoices = 6;

        [Header("Audio: buzz")]
        [Tooltip("Above this smoothed clack rate (per second) the clicks give way to the buzz.")]
        [Min(5f)] [SerializeField] float _buzzOnRate = 90f;

        [Tooltip("Below this rate the buzz hands back to the clicks (lower than On, so it doesn't flutter).")]
        [Min(1f)] [SerializeField] float _buzzOffRate = 60f;

        [Tooltip("Smoothing of the measured rate, seconds. Short = the pitch tracks the clacks tightly.")]
        [Range(0.02f, 0.5f)] [SerializeField] float _rateSmoothSeconds = 0.1f;

        [Tooltip("log10(clacks per second) mapped to the lowest pitch...")]
        [SerializeField] float _pitchLowLog = 1.5f;

        [Tooltip("...and to the highest pitch.")]
        [SerializeField] float _pitchHighLog = 6.5f;

        [Range(0.2f, 1.5f)] [SerializeField] float _pitchLow = 0.7f;

        [Range(1f, 4f)] [SerializeField] float _pitchHigh = 2.4f;

        [Tooltip("Loop clip's base frequency, Hz. The source pitch multiplies this.")]
        [Range(80, 440)] [SerializeField] int _buzzBaseHz = 220;

        [Range(0f, 1f)] [SerializeField] float _buzzVolumeLow = 0.18f;

        [Range(0f, 1f)] [SerializeField] float _buzzVolumeHigh = 0.5f;

        [Tooltip("Seconds for the buzz to fade out once the clacks stop.")]
        [Range(0.02f, 0.4f)] [SerializeField] float _buzzFadeOutSeconds = 0.08f;

        [Range(0.01f, 0.3f)] [SerializeField] float _buzzFadeInSeconds = 0.05f;

        [Header("Audio: result and transition")]
        [Range(0f, 1f)] [SerializeField] float _resultClinkVolume = 0.9f;

        [Range(0f, 1f)] [SerializeField] float _ladderClinkVolume = 0.35f;

        [Tooltip("Pitch of the ladder-row clink (the result clink is 1).")]
        [Range(0.8f, 2f)] [SerializeField] float _ladderClinkPitch = 1.5f;

        [Range(0f, 1f)] [SerializeField] float _whooshVolume = 0.55f;

        [Header("HUD")]
        [Tooltip("Seconds after the result line lands before its row drops onto the ladder, so the two beats " +
                 "read one after the other.")]
        [Range(0f, 1.5f)] [SerializeField] float _ladderDelaySeconds = 0.4f;

        [Tooltip("Colour of each ladder row's last digit, the digit that round added.")]
        [SerializeField] Color _digitTint = new Color(1f, 0.82f, 0.25f, 1f);

        [Tooltip("Colour of the result and end-card lines.")]
        [SerializeField] Color _resultTint = new Color(1f, 0.82f, 0.25f, 1f);

        [Range(0.4f, 0.9f)] [SerializeField] float _counterCellEm = 0.6f;

        // ---- run-time state
        Color _smallColor;
        Color _bigColor;
        System.Random _cosmetic;
        System.Random _clickRandom;
        bool _started;

        long _serialCursor;
        float _rate;
        bool _buzzing;
        float _seamAlpha;

        ClackSparkPool _sparks;

        AudioSource[] _voices;
        int _nextVoice;
        AudioSource _sfx;
        AudioSource _ladderSfx;
        AudioSource _buzz;
        AudioClip _wallClick;
        AudioClip _blockClick;
        AudioClip _clink;
        AudioClip _whoosh;

        PiClacksPhase _prevPhase = PiClacksPhase.Done;
        int _prevRound = -1;
        bool _prevFinished;

        int _ladderPushed;
        readonly List<float> _ladderDue = new List<float>(8);
        bool _hudInitialised;
        string _lastLabelSmall;
        string _lastLabelBig;
        float _lastSmallSize = -1f;
        float _lastBigSize = -1f;

        // HUD caches: TMP rebuilds on every assignment, so only push when the string changed.
        string _footerShown;
        bool _endLineSet;

        void Reset()
        {
            _simulation = GetComponent<PiClacksSimulation>();
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(PiClacksSimulation simulation, CaptionHud hud, ResultLadderHud ladder, SceneRig rig)
        {
            _simulation = simulation;
            _hud = hud;
            _ladder = ladder;
            _rig = rig;
        }

        void Start()
        {
            var runner = GetComponent<SimulationRunner>();
            int seed = runner != null ? runner.seed : 0;
            _cosmetic = new System.Random(seed ^ 0x5bd1e995);
            _clickRandom = new System.Random(seed ^ 0x2545f491);
            PickPalette();

            if (_rig != null && _rig.glowSprite != null && _rig.squareSprite != null)
            {
                _sparks = new ClackSparkPool(transform, _rig.glowSprite, _rig.squareSprite, 16, _sparkSortingOrder,
                                             _sparkSize, 0.06f);
            }

            SetupStaticArt();
            BuildAudio();
            WarmGlyphs();
            _started = true;
        }

        void PickPalette()
        {
            // Complementary, high-contrast hue pairs. The seed picks the pair, which block wears which, and a
            // small wobble. Orange/cyan, pink/lime, yellow/violet, green/magenta, red/azure.
            float[,] pairs =
            {
                { 0.07f, 0.53f }, { 0.92f, 0.27f }, { 0.14f, 0.74f }, { 0.36f, 0.88f }, { 0.00f, 0.58f }
            };
            int pick = _cosmetic.Next(pairs.GetLength(0));
            bool swap = _cosmetic.Next(2) == 1;
            float jitterA = ((float)_cosmetic.NextDouble() * 2f - 1f) * _hueJitter;
            float jitterB = ((float)_cosmetic.NextDouble() * 2f - 1f) * _hueJitter;
            Color a = Color.HSVToRGB(Mathf.Repeat(pairs[pick, 0] + jitterA, 1f), _blockSaturation, 1f);
            Color b = Color.HSVToRGB(Mathf.Repeat(pairs[pick, 1] + jitterB, 1f), _blockSaturation, 1f);
            _smallColor = swap ? b : a;
            _bigColor = swap ? a : b;
        }

        void SetupStaticArt()
        {
            if (_rig == null)
            {
                return;
            }

            float wallWidth = 0.9f;
            float wallBottom = _rig.floorY - 0.5f;
            float wallHeight = Mathf.Max(1f, _rig.topY - wallBottom);

            PlaceBar(_rig.wallBar, _rig.wallFaceX - wallWidth * 0.5f, wallBottom + wallHeight * 0.5f, wallWidth, wallHeight, _wallColor);
            if (_rig.wallHatch != null)
            {
                _rig.wallHatch.drawMode = SpriteDrawMode.Tiled;
                _rig.wallHatch.size = new Vector2(wallWidth, wallHeight);
                _rig.wallHatch.transform.position = new Vector3(_rig.wallFaceX - wallWidth * 0.5f, wallBottom + wallHeight * 0.5f, 0f);
                _rig.wallHatch.color = _hatchColor;
            }

            PlaceBar(_rig.wallEdge, _rig.wallFaceX + 0.02f, wallBottom + wallHeight * 0.5f, 0.07f, wallHeight, _wallEdgeColor);

            float floorLeft = _rig.wallFaceX;
            float floorWidth = 60f;
            PlaceBar(_rig.floorLine, floorLeft + floorWidth * 0.5f, _rig.floorY - 0.03f, floorWidth, 0.07f, _floorLineColor);
            PlaceBar(_rig.floorFill, floorLeft + floorWidth * 0.5f, _rig.floorY - 10f, floorWidth, 20f, _floorFillColor);

            if (_rig.seamGlow != null)
            {
                _rig.seamGlow.color = new Color(1f, 0.95f, 0.8f, 0f);
            }
        }

        static void PlaceBar(SpriteRenderer renderer, float x, float y, float width, float height, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.transform.position = new Vector3(x, y, 0f);
            renderer.transform.localScale = new Vector3(width, height, 1f);
            renderer.color = color;
        }

        /// <summary>
        /// π is not in the static LiberationSans atlas; it comes from the dynamic fallback font asset. Adding
        /// the glyphs up front means the first π on screen doesn't hitch the frame.
        /// </summary>
        void WarmGlyphs()
        {
            const string glyphs = "π×…=.0123456789 ";
            try
            {
                TMP_FontAsset font = _rig != null && _rig.big.label != null ? _rig.big.label.font : null;
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

        void BuildAudio()
        {
            _wallClick = ProceduralToneBank.CreateClick(_wallClickPitch, 7, "WallClick");
            _blockClick = ProceduralToneBank.CreateClick(_blockClickPitch, 8, "BlockClick");
            _clink = ProceduralToneBank.CreateGlassClink(1f, 1.4f, 3, "ClackClink");
            _whoosh = ProceduralToneBank.CreateWhoosh(1.1f, 2, "ClackWhoosh");

            _voices = new AudioSource[_clickVoices];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = AddSource(_clickVolume);
            }

            _sfx = AddSource(1f);
            _ladderSfx = AddSource(1f);
            _ladderSfx.pitch = _ladderClinkPitch;

            _buzz = AddSource(0f);
            _buzz.clip = PiClacksAudio.CreateBuzzLoop(_buzzBaseHz);
            _buzz.loop = true;
            _buzz.pitch = _pitchLow;
            _buzz.Play();
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

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            PiClacksConfig config = _simulation.Config;
            PiClacksPhase phase = _simulation.Phase;

            UpdateBlocks(config);
            UpdateClacks(dt);
            UpdateBuzz(dt);
            UpdateSeam(dt);
            UpdateCues(phase);
            UpdateHud(config, phase);
            UpdateLadder(config, phase, dt);

            _sparks?.Tick(dt);

            _prevPhase = phase;
            _prevRound = _simulation.RoundIndex;
            _prevFinished = _simulation.RoundFinished;
        }

        // ---------------------------------------------------------------- blocks

        void UpdateBlocks(PiClacksConfig config)
        {
            PlaceBlock(_rig.small, _simulation.SmallX, _simulation.SmallSize, _smallColor, config.smallMassLabel,
                       ref _lastLabelSmall, ref _lastSmallSize);
            PlaceBlock(_rig.big, _simulation.BigX, _simulation.BigSize, _bigColor, _simulation.BigMassLabel,
                       ref _lastLabelBig, ref _lastBigSize);
        }

        void PlaceBlock(BlockView view, float x, float size, Color color, string label, ref string lastLabel, ref float lastSize)
        {
            if (view == null || view.root == null)
            {
                return;
            }

            // x is the left face, relative to the wall face; the block sits on the floor.
            view.root.position = new Vector3(_rig.wallFaceX + x + size * 0.5f, _rig.floorY + size * 0.5f, 0f);

            if (!Mathf.Approximately(size, lastSize))
            {
                lastSize = size;
                if (view.body != null)
                {
                    view.body.size = new Vector2(size, size);
                }

                if (view.shadow != null)
                {
                    view.shadow.size = new Vector2(size * 1.02f, size * 1.02f);
                    view.shadow.transform.localPosition = new Vector3(_shadowOffset.x * size, _shadowOffset.y * size, 0f);
                }

                if (view.label != null)
                {
                    // The decal is authored at one unit and scaled with the block, so auto-fit runs once per label.
                    view.label.transform.localScale = new Vector3(size, size, 1f);
                }
            }

            if (view.body != null)
            {
                view.body.color = color;
            }

            if (view.shadow != null)
            {
                view.shadow.color = new Color(0f, 0f, 0f, _shadowAlpha);
            }

            if (view.label != null && label != lastLabel)
            {
                lastLabel = label;
                view.label.text = label ?? string.Empty;
                view.label.color = _labelColor;
            }
        }

        // ---------------------------------------------------------------- clacks: rate, sparks, clicks

        void UpdateClacks(float dt)
        {
            long total = _simulation.ClackSerial;
            if (total < _serialCursor)
            {
                _serialCursor = total;
            }

            long fresh = total - _serialCursor;
            float instant = fresh / dt;
            float blend = 1f - Mathf.Exp(-dt / _rateSmoothSeconds);
            _rate += (instant - _rate) * blend;
            if (_rate < 0.01f)
            {
                _rate = 0f;
            }

            if (_buzzing)
            {
                if (_rate < _buzzOffRate)
                {
                    _buzzing = false;
                }
            }
            else if (_rate >= _buzzOnRate)
            {
                _buzzing = true;
            }

            // Only the most recent 256 clacks can be read back. More than that in a frame is a buzz, which is
            // shown by the seam glow and the tone, so skip the per-clack events entirely.
            if (fresh > 0 && fresh <= 256 && !_buzzing)
            {
                int sparks = 0;
                int clicks = 0;
                for (long serial = _serialCursor; serial < total; serial++)
                {
                    if (sparks >= _maxSparksPerFrame && clicks >= _maxClicksPerFrame)
                    {
                        break;
                    }

                    if (!_simulation.TryGetClack(serial, out ClackEvent clack))
                    {
                        continue;
                    }

                    if (sparks < _maxSparksPerFrame)
                    {
                        SpawnSpark(clack);
                        sparks++;
                    }

                    if (clicks < _maxClicksPerFrame)
                    {
                        PlayClick(clack.Kind);
                        clicks++;
                    }
                }
            }

            _serialCursor = total;
        }

        void SpawnSpark(ClackEvent clack)
        {
            if (_sparks == null)
            {
                return;
            }

            float reach = Mathf.Max(0.3f, _simulation.SmallSize);
            float y = _rig.floorY + 0.1f + (float)_cosmetic.NextDouble() * (reach - 0.2f);
            _sparks.Spawn(new Vector3(_rig.wallFaceX + (float)clack.X, y, 0f), Mathf.Min(1f, reach), _sparkSeconds);
        }

        void PlayClick(ClackKind kind)
        {
            if (_voices == null)
            {
                return;
            }

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            float jitter = ((float)_clickRandom.NextDouble() * 2f - 1f) * _clickPitchJitter;
            voice.pitch = 1f + jitter;
            voice.PlayOneShot(kind == ClackKind.Wall ? _wallClick : _blockClick, 1f);
        }

        void UpdateBuzz(float dt)
        {
            if (_buzz == null)
            {
                return;
            }

            float target = 0f;
            if (_buzzing)
            {
                float unit = PiClacksAudio.RateToUnit(_rate, _pitchLowLog, _pitchHighLog);
                target = Mathf.Lerp(_buzzVolumeLow, _buzzVolumeHigh, unit);
                _buzz.pitch = PiClacksAudio.RateToPitch(_rate, _pitchLowLog, _pitchHighLog, _pitchLow, _pitchHigh);
            }

            // Fade up quickly, down within about 80 ms; the ceiling sets the slope so loud buzzes still fade that fast.
            float step = (target > _buzz.volume ? _buzzVolumeHigh / _buzzFadeInSeconds : _buzzVolumeHigh / _buzzFadeOutSeconds) * dt;
            _buzz.volume = Mathf.MoveTowards(_buzz.volume, target, step);
        }

        void UpdateSeam(float dt)
        {
            if (_rig.seamGlow == null)
            {
                return;
            }

            float target = PiClacksAudio.RateToUnit(_rate, _seamLowLog, _seamHighLog) * _seamMaxAlpha;
            _seamAlpha = Mathf.Lerp(_seamAlpha, target, 1f - Mathf.Exp(-dt / _seamSmoothSeconds));
            if (_seamAlpha < 0.003f)
            {
                _seamAlpha = 0f;
            }

            _rig.seamGlow.enabled = _seamAlpha > 0f;
            float height = Mathf.Max(1.6f, _simulation.SmallSize * 1.8f);
            _rig.seamGlow.transform.position = new Vector3(_rig.wallFaceX + 0.08f, _rig.floorY + height * 0.3f, 0f);
            _rig.seamGlow.transform.localScale = new Vector3(1.2f, height, 1f);
            _rig.seamGlow.color = new Color(1f, 0.95f, 0.8f, _seamAlpha);
        }

        // ---------------------------------------------------------------- one-shot cues

        void UpdateCues(PiClacksPhase phase)
        {
            if (_sfx == null)
            {
                return;
            }

            if (phase == PiClacksPhase.Transition && _prevPhase != PiClacksPhase.Transition)
            {
                _sfx.PlayOneShot(_whoosh, _whooshVolume);
            }

            bool finished = _simulation.RoundFinished;
            if (finished && (!_prevFinished || _simulation.RoundIndex != _prevRound))
            {
                _sfx.PlayOneShot(_clink, _resultClinkVolume);
            }
        }

        // ---------------------------------------------------------------- HUD

        void UpdateHud(PiClacksConfig config, PiClacksPhase phase)
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

        string BuildFooter(PiClacksConfig config, PiClacksPhase phase)
        {
            PiClacksRound round = _simulation.CurrentRound;
            string line;
            bool result = false;
            switch (phase)
            {
                case PiClacksPhase.Hold:
                    line = !string.IsNullOrEmpty(round.holdCaption) ? round.holdCaption : config.holdCaption;
                    break;
                case PiClacksPhase.Transition:
                    line = round.caption;
                    break;
                case PiClacksPhase.Run:
                case PiClacksPhase.Result:
                    if (_simulation.RoundFinished)
                    {
                        line = (round.result ?? string.Empty).Replace("{n}", _simulation.ClackCount.ToString(CultureInfo.InvariantCulture));
                        result = true;
                    }
                    else
                    {
                        line = round.caption;
                    }

                    break;
                case PiClacksPhase.EndCard:
                    return Tint(config.endCard, _resultTint);
                default:
                    return string.Empty;
            }

            if (result)
            {
                line = Tint(line, _resultTint);
            }

            bool live = phase == PiClacksPhase.Run || phase == PiClacksPhase.Result;
            bool zero = phase == PiClacksPhase.Hold;
            string counter = CounterLine(config, live ? _simulation.ClackCount : 0L, live || zero);
            return line + "\n" + counter;
        }

        /// <summary>
        /// "CLACKS 314" with the digits in fixed-width cells, padded with invisible cells out to the round's final
        /// length so the line doesn't shift sideways as digits arrive. A hidden counter keeps its height.
        /// </summary>
        string CounterLine(PiClacksConfig config, long count, bool visible)
        {
            string digits = count.ToString(CultureInfo.InvariantCulture);
            int width = Mathf.Max(digits.Length, _simulation.ExpectedCount.ToString(CultureInfo.InvariantCulture).Length);
            string pad = new string('0', width - digits.Length);
            string cell = _counterCellEm.ToString("0.###", CultureInfo.InvariantCulture);
            string text = config.counterLabel + " <mspace=" + cell + "em>" + digits + "<alpha=#00>" + pad + "</mspace>";
            string sized = "<size=150%><b>" + text + "</b></size>";
            return visible ? sized : "<alpha=#00>" + sized;
        }

        static string Tint(string text, Color color)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
        }

        // ---------------------------------------------------------------- ladder

        void UpdateLadder(PiClacksConfig config, PiClacksPhase phase, float dt)
        {
            if (_ladder == null)
            {
                return;
            }

            IReadOnlyList<long> done = _simulation.CompletedCounts;
            while (_ladderDue.Count + _ladderPushed < done.Count)
            {
                _ladderDue.Add(_ladderDelaySeconds);
            }

            // On the end card nothing may still be waiting: every row is up before the π line fades in.
            bool flush = phase == PiClacksPhase.EndCard || phase == PiClacksPhase.Done;
            for (int i = 0; i < _ladderDue.Count; i++)
            {
                _ladderDue[i] -= dt;
            }

            while (_ladderDue.Count > 0 && (flush || _ladderDue[0] <= 0f))
            {
                _ladderDue.RemoveAt(0);
                _ladder.Push(RowText(done[_ladderPushed]));
                _ladderPushed++;
                _ladderSfx?.PlayOneShot(_clink, _ladderClinkVolume);
            }

            if (phase == PiClacksPhase.EndCard && !_endLineSet)
            {
                _endLineSet = true;
                _ladder.SetFootnote(config.endLine);
            }
        }

        string RowText(long count)
        {
            string digits = count.ToString(CultureInfo.InvariantCulture);
            if (digits.Length == 0)
            {
                return digits;
            }

            // The last digit is the one this round added to π.
            return digits.Substring(0, digits.Length - 1) + "<color=#" + ColorUtility.ToHtmlStringRGB(_digitTint) + ">" +
                   digits[digits.Length - 1] + "</color>";
        }
    }
}
