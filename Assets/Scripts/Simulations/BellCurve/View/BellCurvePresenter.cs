using System.Globalization;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Simulations.BellCurve.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.BellCurve
{
    /// <summary>
    /// Draws and sounds a <see cref="BellCurveSimulation"/>: the peg board, the spout, the falling marbles, the
    /// bar-per-bin pile, the fitted bell outline, the caption HUD, the ladder and the audio. Reads the run in
    /// <c>LateUpdate</c> and never writes to it. Marbles are pooled sprites posed straight from the simulation's
    /// arrays; the only randomness is cosmetic (click pitch) and comes from a local generator.
    /// </summary>
    [RequireComponent(typeof(BellCurveSimulation))]
    public sealed class BellCurvePresenter : MonoBehaviour
    {
        /// <summary>Everything the scene builder hands over. The presenter creates and drives the board pieces itself.</summary>
        [System.Serializable]
        public sealed class SceneRig
        {
            public Transform arena;
            public Vector2 boardOrigin;   // world position of board space (0, 0): the first peg's marble height
            public Sprite circleSprite;
            public Sprite squareSprite;
            public Sprite glowSprite;
        }

        [Header("Wiring (set by the scene builder via Bind)")]
        [SerializeField] BellCurveSimulation _simulation;
        [SerializeField] CaptionHud _hud;
        [SerializeField] ResultLadderHud _ladder;
        [SerializeField] SceneRig _rig = new SceneRig();

        [Header("Look")]
        [SerializeField] Color _pegColor = new Color(0.78f, 0.84f, 0.95f, 1f);
        [SerializeField] Color _riggedPegColor = new Color(1f, 0.62f, 0.2f, 1f);
        [SerializeField] Color _trayColor = new Color(0.85f, 0.9f, 1f, 0.9f);
        [SerializeField] Color _dividerColor = new Color(1f, 1f, 1f, 0.16f);
        [SerializeField] Color _spoutColor = new Color(0.55f, 0.6f, 0.7f, 1f);
        [SerializeField] Color _curveColor = new Color(1f, 0.82f, 0.25f, 1f);

        [Tooltip("Pegs lean up to this many degrees at pegLean = 1.")]
        [Range(0f, 60f)] [SerializeField] float _maxLeanDegrees = 38f;

        [Tooltip("How fast a bar chases its target height (1/s).")]
        [Range(2f, 40f)] [SerializeField] float _barFollow = 16f;

        [Tooltip("Bar saturation and brightness.")]
        [Range(0.3f, 1f)] [SerializeField] float _binSaturation = 0.78f;

        [Range(8, 600)] [SerializeField] int _marblePool = 360;

        [Tooltip("Wide variant only: when set, bins are coloured along this gradient (left to right) instead of the default hue sweep.")]
        [SerializeField] bool _useBinGradient;
        [SerializeField] Gradient _binGradient = new Gradient();

        [Range(0.02f, 0.2f)] [SerializeField] float _curveWidth = 0.07f;

        [Header("Audio")]
        [Range(0f, 1f)] [SerializeField] float _pegClickVolume = 0.22f;
        [Range(1, 8)] [SerializeField] int _maxClicksPerFrame = 3;
        [Range(0f, 0.25f)] [SerializeField] float _clickPitchJitter = 0.1f;
        [Range(0f, 1f)] [SerializeField] float _landToneVolume = 0.16f;
        [Tooltip("A bin's note can't re-trigger faster than this, seconds.")]
        [Range(0.02f, 0.4f)] [SerializeField] float _toneCooldown = 0.09f;
        [Range(0f, 1f)] [SerializeField] float _resultClinkVolume = 0.9f;
        [Range(0f, 1f)] [SerializeField] float _ladderClinkVolume = 0.35f;
        [Range(0f, 1f)] [SerializeField] float _whooshVolume = 0.55f;
        [SerializeField] float _toneRootHz = 196f;

        [Header("HUD")]
        [Range(0f, 1.5f)] [SerializeField] float _ladderDelaySeconds = 0.5f;
        [SerializeField] Color _resultTint = new Color(1f, 0.82f, 0.25f, 1f);
        [Range(0.4f, 0.9f)] [SerializeField] float _counterCellEm = 0.6f;

        // ---- scene pieces built at Start
        SpriteRenderer[] _pegs;
        int[] _pegRow;
        SpriteRenderer[] _bars;
        SpriteRenderer[] _caps;
        float[] _barHeight;
        SpriteRenderer[] _marbles;
        Color[] _binColors;
        LineRenderer _curve;
        Vector2[] _curvePoints;
        bool _curveReady;
        Material _lineMaterial;
        float _pegDiameter;
        float _lastLean = float.NaN;

        // ---- run-time state
        System.Random _cosmetic;
        bool _started;
        int[] _lastHop = new int[0];
        int[] _prevLanded = new int[0];
        float[] _nextTone = new float[0];
        float _clock;
        bool _warnedPool;

        AudioSource[] _voices;
        int _nextVoice;
        AudioSource _toneSfx;
        AudioSource _sfx;
        AudioClip _pegClick;
        AudioClip[] _tones;
        AudioClip _clink;
        AudioClip _whoosh;

        BellPhase _prevPhase = BellPhase.Done;
        bool _prevFinished;
        int _prevRound = -1;
        int _ladderPushed;
        float _ladderTimer = -1f;
        bool _hudInitialised;
        string _footerShown;
        bool _endLineSet;
        bool _endQuestionSet;

        void Reset()
        {
            _simulation = GetComponent<BellCurveSimulation>();
        }

        /// <summary>
        /// Wide (16:9) variant hook, additive: warm bin gradient, a bigger marble pool for high spawn rates and a
        /// lower tone root for a board with many bins. Call before Start (the scene builder does).
        /// </summary>
        public void ConfigureWide(Gradient binGradient, int marblePool, float toneRootHz)
        {
            _useBinGradient = binGradient != null;
            if (binGradient != null)
            {
                _binGradient = binGradient;
            }

            _marblePool = Mathf.Max(8, marblePool);
            _toneRootHz = toneRootHz;
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(BellCurveSimulation simulation, CaptionHud hud, ResultLadderHud ladder, SceneRig rig)
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
            _cosmetic = new System.Random(seed ^ 0x2545f491);

            BuildBoard();
            BuildAudio();
            _started = true;
        }

        // ---------------------------------------------------------------- board construction

        SpriteRenderer NewSprite(string name, Sprite sprite, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        Vector3 BoardToWorld(float x, float y)
        {
            return new Vector3(_rig.boardOrigin.x + x, _rig.boardOrigin.y + y, 0f);
        }

        void BuildBoard()
        {
            BellCurveConfig config = _simulation.Config;
            if (config == null || _rig == null || _rig.circleSprite == null || _rig.squareSprite == null)
            {
                return;
            }

            Transform parent = _rig.arena != null ? _rig.arena : transform;
            BoardGeometry g = _simulation.Geometry;
            int rows = g.Rows;
            int bins = g.Bins;
            float contact = config.pegRadius + config.marbleRadius; // peg centre sits this far under the marble's centre
            _pegDiameter = config.pegRadius * 2f;

            // Pegs: row r has r+1 of them.
            int pegCount = rows * (rows + 1) / 2;
            _pegs = new SpriteRenderer[pegCount];
            _pegRow = new int[pegCount];
            int n = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int j = 0; j <= r; j++)
                {
                    SpriteRenderer peg = NewSprite("Peg", _rig.circleSprite, 10, parent);
                    peg.transform.position = BoardToWorld(g.PegX(r, j), g.RowY(r) - contact);
                    peg.transform.localScale = new Vector3(_pegDiameter, _pegDiameter, 1f);
                    peg.color = _pegColor;
                    _pegs[n] = peg;
                    _pegRow[n] = r;
                    n++;
                }
            }

            // Spout.
            float spoutY = g.DropHeight + config.marbleRadius + 0.22f;
            SpriteRenderer spout = NewSprite("Spout", _rig.squareSprite, 12, parent);
            spout.transform.position = BoardToWorld(0f, spoutY);
            spout.transform.localScale = new Vector3(0.62f, 0.34f, 1f);
            spout.color = _spoutColor;
            SpriteRenderer lip = NewSprite("Spout Lip", _rig.squareSprite, 13, parent);
            lip.transform.position = BoardToWorld(0f, spoutY - 0.17f);
            lip.transform.localScale = new Vector3(0.86f, 0.08f, 1f);
            lip.color = new Color(0.9f, 0.94f, 1f, 1f);

            // Tray: floor, dividers, one bar and cap per bin.
            float floorY = config.TrayFloorY;
            float left = g.BinX(0) - g.Dx * 0.5f;
            float right = g.BinX(bins - 1) + g.Dx * 0.5f;
            SpriteRenderer floor = NewSprite("Tray Floor", _rig.squareSprite, 3, parent);
            floor.transform.position = BoardToWorld((left + right) * 0.5f, floorY - 0.04f);
            floor.transform.localScale = new Vector3(right - left + 0.08f, 0.08f, 1f);
            floor.color = _trayColor;

            for (int k = 0; k <= bins; k++)
            {
                SpriteRenderer divider = NewSprite("Divider", _rig.squareSprite, 4, parent);
                float x = left + k * g.Dx;
                divider.transform.position = BoardToWorld(x, floorY + config.trayHeight * 0.5f);
                divider.transform.localScale = new Vector3(0.035f, config.trayHeight, 1f);
                divider.color = _dividerColor;
            }

            _bars = new SpriteRenderer[bins];
            _caps = new SpriteRenderer[bins];
            _barHeight = new float[bins];
            _binColors = new Color[bins];
            for (int k = 0; k < bins; k++)
            {
                float t = bins > 1 ? k / (float)(bins - 1) : 0.5f;
                _binColors[k] = _useBinGradient
                    ? _binGradient.Evaluate(t)
                    : Color.HSVToRGB(Mathf.Repeat(0.58f - 0.66f * t, 1f), _binSaturation, 1f);
                _bars[k] = NewSprite("Bar " + k, _rig.squareSprite, 6, parent);
                _bars[k].color = _binColors[k];
                _caps[k] = NewSprite("Bar Cap " + k, _rig.squareSprite, 7, parent);
                _caps[k].color = Color.Lerp(_binColors[k], Color.white, 0.55f);
                _bars[k].enabled = false;
                _caps[k].enabled = false;
            }

            // Marble pool.
            _marbles = new SpriteRenderer[_marblePool];
            float d = config.marbleRadius * 2f;
            for (int i = 0; i < _marbles.Length; i++)
            {
                SpriteRenderer m = NewSprite("Marble", _rig.circleSprite, 20, parent);
                m.transform.localScale = new Vector3(d, d, 1f);
                m.enabled = false;
                _marbles[i] = m;
            }

            // Fitted bell outline.
            _lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "BellCurveLine" };
            var curveObject = new GameObject("Bell Outline");
            curveObject.transform.SetParent(parent, false);
            _curve = curveObject.AddComponent<LineRenderer>();
            _curve.sharedMaterial = _lineMaterial;
            _curve.useWorldSpace = true;
            _curve.sortingOrder = 25;
            _curve.widthMultiplier = _curveWidth;
            _curve.numCapVertices = 4;
            _curve.numCornerVertices = 4;
            _curvePoints = new Vector2[0];
            _curve.enabled = false;

            _lastHop = new int[0];
            _prevLanded = new int[bins];
            _nextTone = new float[bins];
        }

        void BuildAudio()
        {
            int bins = _simulation.Config != null ? _simulation.Config.rows + 1 : 11;
            _pegClick = ProceduralToneBank.CreateClick(1.5f, 5, "PegClick");
            _tones = ProceduralToneBank.CreateScale(bins, _toneRootHz, 0.7f);
            _clink = ProceduralToneBank.CreateGlassClink(1f, 1.4f, 3, "BellClink");
            _whoosh = ProceduralToneBank.CreateWhoosh(1.1f, 2, "BellWhoosh");

            _voices = new AudioSource[6];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = AddSource(1f);
            }

            _toneSfx = AddSource(1f);
            _sfx = AddSource(1f);
        }

        AudioSource AddSource(float volume)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            return source;
        }

        // ---------------------------------------------------------------- frame

        void LateUpdate()
        {
            if (!_started || _simulation == null || _simulation.Config == null || _rig == null || _bars == null)
            {
                return;
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            _clock += dt;
            BellCurveConfig config = _simulation.Config;
            BellPhase phase = _simulation.Phase;

            if (_simulation.MarbleCount != _lastHop.Length || (phase == BellPhase.Run && _prevPhase != BellPhase.Run))
            {
                _lastHop = new int[_simulation.MarbleCount];
                for (int i = 0; i < _lastHop.Length; i++)
                {
                    _lastHop[i] = -2;
                }
            }

            UpdatePegs();
            UpdateMarbles(config);
            UpdateBars(config, dt);
            UpdateCurve(config, phase);
            UpdateTones();
            UpdateCues(phase);
            UpdateHud(config, phase);
            UpdateLadder(config, phase, dt);

            _prevPhase = phase;
            _prevRound = _simulation.RoundIndex;
            _prevFinished = _simulation.RoundFinished;
        }

        void UpdatePegs()
        {
            float lean = _simulation.PegLean;
            if (Mathf.Approximately(lean, _lastLean))
            {
                return;
            }

            _lastLean = lean;
            float abs = Mathf.Abs(lean);
            Quaternion rotation = Quaternion.Euler(0f, 0f, -lean * _maxLeanDegrees);
            Vector3 scale = new Vector3(_pegDiameter * (1f + 1.5f * abs), _pegDiameter * (1f - 0.3f * abs), 1f);
            Color color = Color.Lerp(_pegColor, _riggedPegColor, Mathf.Clamp01(abs * 1.2f));
            for (int i = 0; i < _pegs.Length; i++)
            {
                _pegs[i].transform.rotation = rotation;
                _pegs[i].transform.localScale = scale;
                _pegs[i].color = color;
            }
        }

        void UpdateMarbles(BellCurveConfig config)
        {
            BoardGeometry g = _simulation.Geometry;
            float[] spawn = _simulation.SpawnSeconds;
            uint[] bits = _simulation.PathBits;
            int[] bin = _simulation.BinOf;
            float t = _simulation.RunSeconds;
            bool running = _simulation.Phase == BellPhase.Run;

            int shown = 0;
            int clicks = 0;
            int from = running ? _simulation.LandedCount : _simulation.MarbleCount;
            int to = running ? _simulation.SpawnedCount : _simulation.MarbleCount;

            for (int i = from; i < to; i++)
            {
                if (shown >= _marbles.Length)
                {
                    if (!_warnedPool)
                    {
                        _warnedPool = true;
                        Debug.LogWarning("[bell] Marble pool exhausted; some falling marbles are not drawn. Raise Marble Pool.");
                    }

                    break;
                }

                if (!g.TryPose(bits[i], bin[i], t - spawn[i], out Vector2 pose, out int hop))
                {
                    continue;
                }

                SpriteRenderer m = _marbles[shown++];
                m.enabled = true;
                m.transform.position = BoardToWorld(pose.x, pose.y);
                m.color = _binColors[bin[i]];

                if (hop > _lastHop[i])
                {
                    if (hop >= 0 && hop < g.Rows && clicks < _maxClicksPerFrame)
                    {
                        PlayClick();
                        clicks++;
                    }

                    _lastHop[i] = hop;
                }
            }

            for (int i = shown; i < _marbles.Length; i++)
            {
                if (!_marbles[i].enabled)
                {
                    break;
                }

                _marbles[i].enabled = false;
            }
        }

        void PlayClick()
        {
            if (_voices == null)
            {
                return;
            }

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            float jitter = ((float)_cosmetic.NextDouble() * 2f - 1f) * _clickPitchJitter;
            voice.pitch = 1f + jitter;
            voice.PlayOneShot(_pegClick, _pegClickVolume);
        }

        void UpdateBars(BellCurveConfig config, float dt)
        {
            BoardGeometry g = _simulation.Geometry;
            int[] landed = _simulation.Landed;
            float clear = _simulation.ClearProgress;
            float perMarble = _simulation.BarHeightPerMarble;
            float cap = config.trayHeight * 1.15f;
            float follow = 1f - Mathf.Exp(-_barFollow * dt);
            float floorY = config.TrayFloorY;
            float width = g.Dx - 0.08f;

            for (int k = 0; k < _bars.Length; k++)
            {
                float target = Mathf.Min(cap, landed[k] * perMarble) * (1f - clear);
                _barHeight[k] += (target - _barHeight[k]) * follow;
                float h = _barHeight[k];
                bool visible = h > 0.012f;
                _bars[k].enabled = visible;
                _caps[k].enabled = visible;
                if (!visible)
                {
                    continue;
                }

                float x = g.BinX(k);
                _bars[k].transform.position = BoardToWorld(x, floorY + h * 0.5f);
                _bars[k].transform.localScale = new Vector3(width, h, 1f);
                float capH = Mathf.Min(0.07f, h);
                _caps[k].transform.position = BoardToWorld(x, floorY + h - capH * 0.5f);
                _caps[k].transform.localScale = new Vector3(width, capH, 1f);
            }
        }

        void UpdateCurve(BellCurveConfig config, BellPhase phase)
        {
            if (_curve == null)
            {
                return;
            }

            bool finished = _simulation.RoundFinished && _simulation.DataRound.showCurve;
            if (finished && !_curveReady)
            {
                FitCurve(config);
                _curveReady = true;
            }
            else if (!_simulation.RoundFinished)
            {
                _curveReady = false;
            }

            float alpha = 0f;
            if (_curveReady && _curvePoints.Length > 1)
            {
                float fadeIn = phase == BellPhase.Result ? Mathf.Clamp01(_simulation.PhaseProgress * 4f) : 1f;
                alpha = fadeIn * (1f - _simulation.ClearProgress);
            }

            _curve.enabled = alpha > 0.01f;
            if (_curve.enabled)
            {
                Color c = _curveColor;
                c.a = alpha;
                _curve.startColor = c;
                _curve.endColor = c;
            }
        }

        /// <summary>Normal curve through the pile's own mean and spread, drawn at the bars' scale.</summary>
        void FitCurve(BellCurveConfig config)
        {
            BoardGeometry g = _simulation.Geometry;
            int[] landed = _simulation.Landed;
            int total = _simulation.MarbleCount;
            double mean = 0.0;
            for (int k = 0; k < landed.Length; k++)
            {
                mean += g.BinX(k) * (double)landed[k];
            }

            mean /= System.Math.Max(1, total);
            double variance = 0.0;
            for (int k = 0; k < landed.Length; k++)
            {
                double d = g.BinX(k) - mean;
                variance += d * d * landed[k];
            }

            variance /= System.Math.Max(1, total);
            double sigma = System.Math.Sqrt(variance);
            if (sigma < 1e-4)
            {
                _curvePoints = new Vector2[0];
                _curve.positionCount = 0;
                return;
            }

            const int points = 64;
            float left = g.BinX(0) - g.Dx * 0.5f;
            float right = g.BinX(g.Bins - 1) + g.Dx * 0.5f;
            float scale = _simulation.BarHeightPerMarble * total * g.Dx;
            _curvePoints = new Vector2[points];
            _curve.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float x = Mathf.Lerp(left, right, i / (float)(points - 1));
                double z = (x - mean) / sigma;
                float y = config.TrayFloorY + (float)(scale * System.Math.Exp(-0.5 * z * z) / (sigma * 2.5066282746));
                y = Mathf.Min(y, config.TrayFloorY + config.trayHeight * 1.15f);
                _curvePoints[i] = new Vector2(x, y);
                _curve.SetPosition(i, BoardToWorld(x, y));
            }
        }

        // ---------------------------------------------------------------- audio cues

        void UpdateTones()
        {
            int[] landed = _simulation.Landed;
            if (_prevLanded.Length != landed.Length)
            {
                _prevLanded = new int[landed.Length];
            }

            for (int k = 0; k < landed.Length; k++)
            {
                int delta = landed[k] - _prevLanded[k];
                _prevLanded[k] = landed[k];
                if (delta > 0 && _clock >= _nextTone[k] && _toneSfx != null && k < _tones.Length)
                {
                    _toneSfx.PlayOneShot(_tones[k], _landToneVolume);
                    _nextTone[k] = _clock + _toneCooldown;
                }
            }
        }

        void UpdateCues(BellPhase phase)
        {
            if (_sfx == null)
            {
                return;
            }

            if (phase == BellPhase.Transition && _prevPhase != BellPhase.Transition)
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

        void UpdateHud(BellCurveConfig config, BellPhase phase)
        {
            if (_hud == null)
            {
                return;
            }

            if (!_hudInitialised)
            {
                _hudInitialised = true;
                _hud.SetCaption(config.question, false);
                _hud.SetSubline(config.title);
            }

            if (phase == BellPhase.EndCard && !_endQuestionSet)
            {
                _endQuestionSet = true;
                _hud.SetCaption(config.endQuestion, true);
            }

            string footer = BuildFooter(config, phase);
            if (footer != _footerShown)
            {
                _footerShown = footer;
                _hud.SetFooter(footer);
            }
        }

        string BuildFooter(BellCurveConfig config, BellPhase phase)
        {
            BellRound round = _simulation.CurrentRound;
            string line;
            switch (phase)
            {
                case BellPhase.Hold:
                    line = !string.IsNullOrEmpty(round.holdCaption) ? round.holdCaption : config.holdCaption;
                    break;
                case BellPhase.Transition:
                    line = round.caption;
                    break;
                case BellPhase.Run:
                    line = round.caption;
                    break;
                case BellPhase.Result:
                    line = Tint(round.result, _resultTint);
                    break;
                case BellPhase.EndCard:
                    return Tint(config.endCard, _resultTint);
                default:
                    return string.Empty;
            }

            bool visible = phase != BellPhase.Transition;
            return line + "\n" + CounterLine(config, visible ? _simulation.LandedCount : 0, visible);
        }

        /// <summary>"MARBLES 437" in fixed-width cells, padded with invisible cells to the round's final width.</summary>
        string CounterLine(BellCurveConfig config, int count, bool visible)
        {
            string digits = count.ToString(CultureInfo.InvariantCulture);
            int width = Mathf.Max(digits.Length, _simulation.MarbleCount.ToString(CultureInfo.InvariantCulture).Length);
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

        void UpdateLadder(BellCurveConfig config, BellPhase phase, float dt)
        {
            if (_ladder == null)
            {
                return;
            }

            var rows = _simulation.CompletedRows;
            bool flush = phase == BellPhase.EndCard || phase == BellPhase.Done;
            if (_ladderPushed < rows.Count)
            {
                if (_ladderTimer < 0f)
                {
                    _ladderTimer = _ladderDelaySeconds;
                }

                _ladderTimer -= dt;
                if (flush || _ladderTimer <= 0f)
                {
                    _ladder.Push(Tint(rows[_ladderPushed], _resultTint));
                    _ladderPushed++;
                    _ladderTimer = -1f;
                    if (_sfx != null)
                    {
                        _sfx.PlayOneShot(_clink, _ladderClinkVolume);
                    }
                }
            }

            if (phase == BellPhase.EndCard && !_endLineSet)
            {
                _endLineSet = true;
                _ladder.SetFootnote(config.endLine);
            }
        }
    }
}
