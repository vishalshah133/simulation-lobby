using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall
{
    /// <summary>
    /// Draws <c>fall</c> (Freefall Symphony): the marble(s), the pads, the walls, sky and fog, the descent camera,
    /// the caption HUD, the core and the white-out. Everything runs in <c>Update</c>/<c>LateUpdate</c> and only
    /// reads the simulation (<c>Time</c>, <c>Schedule</c>, <c>Config</c>). It builds world w + 1 when world w is
    /// reached and destroys world w - 2; on the outro it rebuilds the lot under the white-out for the pull-back.
    /// All decoration randomness is a <see cref="System.Random"/> seeded from the run seed and the world index.
    /// </summary>
    [RequireComponent(typeof(FreefallSimulation))]
    public sealed class FreefallPresenter : MonoBehaviour
    {
        sealed class WorldView
        {
            public int index;
            public GameObject root;
            public WorldPads pads;
            public WallView wall;
        }

        struct CaptionEvent
        {
            public float start;
            public float end;
            public int priority;
            public string caption;
            public string sub;
        }

        [Header("Wiring (set by the scene builder)")]
        [SerializeField] FreefallSimulation _simulation;
        [SerializeField] CaptionHud _hud;
        [SerializeField] Camera _camera;
        [SerializeField] FreefallMaterials _materials = new FreefallMaterials();
        [Tooltip("Full-screen white overlay for the ending. Alpha is driven here.")]
        [SerializeField] CanvasGroup _whiteOut;
        [Tooltip("Call FreefallAudio.Bind(sim, config) from Start. Off if something else binds it.")]
        [SerializeField] bool _bindAudio = true;

        [Header("Captions")]
        [SerializeField] string _hookText = "EVERY BOUNCE IS A NOTE";
        [Min(1f)] [SerializeField] float _hookSeconds = 3.2f;
        [Min(1f)] [SerializeField] float _worldLabelSeconds = 3f;
        [Min(0f)] [SerializeField] float _splitDelaySeconds = 2f;
        [Min(1f)] [SerializeField] float _splitSeconds = 2.5f;
        [Tooltip("{n} is the number of worlds. Shown after the last title when there is more than one world.")]
        [SerializeField] string _endLine = "{n} SONGS. 1 MARBLE.";
        [SerializeField] Color _accentTint = new Color(1f, 0.86f, 0.45f, 1f);

        [Header("Descent camera")]
        [Range(40f, 90f)] [SerializeField] float _fieldOfView = 68f;
        [SerializeField] float _heightAboveMarble = 2.2f;
        [SerializeField] float _outwardOfMarble = 1.4f;
        [Tooltip("0 looks at the marble, 1 at the next pad.")]
        [Range(0f, 1f)] [SerializeField] float _lookAhead = 0.42f;
        [Min(0.02f)] [SerializeField] float _smoothSeconds = 0.18f;
        [Tooltip("Pull-back length at the start of a transition fall, as a fraction of the fall.")]
        [Range(0.2f, 0.9f)] [SerializeField] float _revealHoldFraction = 0.6f;
        [Min(0.1f)] [SerializeField] float _revealRampSeconds = 0.9f;

        [Header("Sky and fog")]
        [Tooltip("Scales the sky colour so the white marble stays the brightest thing on screen.")]
        [Range(0.2f, 1f)] [SerializeField] float _skyBrightness = 0.62f;
        [Range(0f, 1f)] [SerializeField] float _fogTint = 0.35f;
        [SerializeField] float _fogStart = 4f;
        [SerializeField] float _fogEnd = 28f;

        [Header("Ending")]
        [SerializeField] float _whiteRiseSeconds = 0.8f;
        [SerializeField] float _whiteHoldSeconds = 1f;
        [SerializeField] float _whiteFallSeconds = 1.2f;
        [SerializeField] float _coreRadius = 0.9f;

        // ---- run-time state
        readonly Dictionary<int, WorldView> _worlds = new Dictionary<int, WorldView>();
        readonly List<CaptionEvent> _captions = new List<CaptionEvent>(32);
        readonly List<FreefallHit> _heroHits = new List<FreefallHit>(256);
        readonly StringBuilder _text = new StringBuilder(96);
        MarbleView[] _marbles;
        Color[] _voiceHue;
        HitEffects _effects;
        FreefallCameraRig _rig;
        System.Random _fx;
        int _seed;
        bool _ready;

        FreefallConfig _config;
        FreefallSchedule _schedule;
        int _worldCount;
        int _hitCursor;
        int _heroHitCursor;
        float _coreTime;
        float _duration;
        bool _allBuilt;

        Vector3 _heroPosition;
        Vector3 _heroVelocity;
        bool _heroValid;
        Vector3 _lastOutward = Vector3.right;
        float _roll;
        float _reveal;
        float _outro;
        int _revealWorld;
        float _time;

        Transform _core;
        MeshRenderer _coreRenderer;
        Transform _coreHalo;
        MeshRenderer _coreHaloRenderer;
        static MaterialPropertyBlock _block;
        // Lazy: Unity forbids creating one while a MonoBehaviour is being constructed (static init runs then).
        static MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

        string _captionShown;
        string _subShown;
        string _footerShown;

        void Reset()
        {
            _simulation = GetComponent<FreefallSimulation>();
        }

        /// <summary>Wiring from the scene builder. Materials and the overlay come in through <see cref="SetRig"/>.</summary>
        public void Bind(FreefallSimulation sim, CaptionHud hud, Camera cam)
        {
            _simulation = sim;
            _hud = hud;
            _camera = cam;
        }

        public void SetRig(FreefallMaterials materials, CanvasGroup whiteOut)
        {
            _materials = materials;
            _whiteOut = whiteOut;
        }

        void Start()
        {
            // The runner has begun in its Awake, so the schedule exists by now. If not, Update keeps trying.
            TryInitialise();
        }

        bool TryInitialise()
        {
            if (_ready)
            {
                return true;
            }

            if (_simulation == null || _simulation.Schedule == null || _simulation.Config == null || _camera == null)
            {
                return false;
            }

            if (_materials == null || !_materials.IsComplete)
            {
                Debug.LogError("[fall] FreefallPresenter materials are missing. Rebuild the scene from the Simulation Lobby menu.", this);
                enabled = false;
                return false;
            }

            _config = _simulation.Config;
            _schedule = _simulation.Schedule;
            _worldCount = _config.worlds.Count;
            var runner = GetComponent<SimulationRunner>();
            _seed = runner != null ? runner.seed : 0;
            _fx = new System.Random(unchecked(_seed * 2654435 + 91));

            _rig = new FreefallCameraRig(_camera);
            _camera.fieldOfView = _fieldOfView;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 2000f;
            _camera.clearFlags = CameraClearFlags.SolidColor;

            _marbles = new MarbleView[Mathf.Max(1, _schedule.MaxVoices)];
            _voiceHue = new Color[_marbles.Length];
            for (int v = 0; v < _marbles.Length; v++)
            {
                _marbles[v] = new MarbleView(transform, v, _config, _materials);
                _voiceHue[v] = Color.white;
            }

            _effects = new HitEffects(transform, _materials);

            IReadOnlyList<FreefallHit> hits = _schedule.Hits;
            _coreTime = hits.Count > 0 ? hits[hits.Count - 1].time : 0f;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].voice == 0)
                {
                    _heroHits.Add(hits[i]);
                }

                if (hits[i].isRunFinal)
                {
                    _coreTime = hits[i].time;
                }
            }

            _duration = _schedule.Duration;
            BuildCore();
            BuildCaptionEvents();
            WarmGlyphs();

            if (_bindAudio)
            {
                FreefallAudio audio = GetComponent<FreefallAudio>();
                if (audio != null)
                {
                    audio.Bind(_simulation, _config);
                }
            }

            UpdateWorldLifecycle(0f);
            _ready = true;
            return true;
        }

        void OnDestroy()
        {
            _effects?.Destroy();
        }

        // ---------------------------------------------------------------- one-time setup

        void BuildCore()
        {
            var core = new GameObject("Core");
            core.transform.SetParent(transform, false);
            core.transform.position = _schedule.CoreCentre;
            core.AddComponent<MeshFilter>().sharedMesh = FreefallMeshes.Sphere();
            _coreRenderer = core.AddComponent<MeshRenderer>();
            _coreRenderer.sharedMaterial = _materials.coreSphere;
            _coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _core = core.transform;
            _core.localScale = Vector3.one * (_coreRadius * 2f);

            var halo = new GameObject("Core Halo");
            halo.transform.SetParent(core.transform, false);
            halo.AddComponent<MeshFilter>().sharedMesh = FreefallMeshes.Quad();
            _coreHaloRenderer = halo.AddComponent<MeshRenderer>();
            _coreHaloRenderer.sharedMaterial = _materials.glowSoft;
            _coreHaloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _coreHalo = halo.transform;
            core.SetActive(false);
        }

        void BuildCaptionEvents()
        {
            _captions.Clear();
            int n = _worldCount;
            IReadOnlyList<FreefallWorldSpan> spans = _schedule.Worlds;
            IReadOnlyList<FreefallHit> hits = _schedule.Hits;

            _captions.Add(new CaptionEvent { start = 0f, end = _hookSeconds, priority = 1, caption = _hookText, sub = string.Empty });

            var starts = new float[n];
            for (int w = 0; w < n; w++)
            {
                starts[w] = w == 0 ? _hookSeconds + 0.2f : spans[w].firstHitTime - 0.8f;
            }

            for (int w = 0; w < n; w++)
            {
                FreefallWorld world = _config.worlds[w];
                string name = world.worldName;
                string label = n > 1
                    ? (w + 1).ToString(CultureInfo.InvariantCulture) + " / " + n.ToString(CultureInfo.InvariantCulture) + " · " + name
                    : name;
                _captions.Add(new CaptionEvent
                {
                    start = starts[w],
                    end = starts[w] + _worldLabelSeconds,
                    priority = 2,
                    caption = label,
                    sub = string.Empty
                });

                string title = world.melody != null && !string.IsNullOrEmpty(world.melody.songTitle) ? world.melody.songTitle : name;
                float titleStart = spans[w].lastHitTime;
                float titleEnd = w + 1 < n ? starts[w + 1] : (n > 1 ? _coreTime + 2.4f : float.MaxValue);
                _captions.Add(new CaptionEvent
                {
                    start = titleStart,
                    end = titleEnd,
                    priority = 3,
                    caption = "♪ " + title,
                    sub = world.composer ?? string.Empty
                });

                if (world.canonVoices > 1)
                {
                    // The second voice appears introDropSeconds before its first hit in the world.
                    float second = -1f;
                    for (int i = spans[w].firstHitIndex; i < spans[w].firstHitIndex + spans[w].hitCount; i++)
                    {
                        if (hits[i].voice == 1)
                        {
                            second = hits[i].time - _config.introDropSeconds;
                            break;
                        }
                    }

                    if (second >= 0f)
                    {
                        _captions.Add(new CaptionEvent
                        {
                            start = second + _splitDelaySeconds,
                            end = second + _splitDelaySeconds + _splitSeconds,
                            priority = 4,
                            caption = "IT SPLITS",
                            sub = string.Empty
                        });
                    }
                }
            }

            if (n > 1)
            {
                _captions.Add(new CaptionEvent
                {
                    start = _coreTime + 2.4f,
                    end = float.MaxValue,
                    priority = 5,
                    caption = _endLine.Replace("{n}", n.ToString(CultureInfo.InvariantCulture)),
                    sub = string.Empty
                });
            }
        }

        /// <summary>
        /// The song titles use the note sign and the middle dot; the progress dots use the black and white circles.
        /// Adding the glyphs up front stops the first caption hitching the frame.
        /// </summary>
        void WarmGlyphs()
        {
            if (_hud == null)
            {
                return;
            }

            const string glyphs = "♪·●○ÜÖÄéè♯";
            try
            {
                foreach (TMP_Text text in _hud.GetComponentsInChildren<TMP_Text>(true))
                {
                    TMP_FontAsset font = text.font;
                    if (font == null)
                    {
                        continue;
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
            }
            catch (System.Exception)
            {
                // Cosmetic warm-up only.
            }
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            if (!TryInitialise() || !_ready)
            {
                return;
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            _time = _simulation.Time;

            UpdateWorldLifecycle(_time);
            ConsumeHits(_time);
            UpdateMarbles(_time, dt);
            UpdateWorlds(_time, dt);
            _effects.Tick(_time);
            UpdateCore(_time);
            UpdateHud(_time);
            UpdateWhiteOut(_time);
        }

        void LateUpdate()
        {
            if (!_ready)
            {
                return;
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            UpdateCamera(_time, dt);
            UpdateSky(_time);

            for (int v = 0; v < _marbles.Length; v++)
            {
                _marbles[v].Billboard(_camera);
            }

            if (_coreHalo != null && _core.gameObject.activeSelf)
            {
                _coreHalo.rotation = _camera.transform.rotation;
            }
        }

        // ---------------------------------------------------------------- worlds

        void UpdateWorldLifecycle(float t)
        {
            int current = Mathf.Clamp(_schedule.WorldAt(t), 0, _worldCount - 1);

            if (!_allBuilt)
            {
                EnsureWorld(current);
                if (current + 1 < _worldCount)
                {
                    EnsureWorld(current + 1);
                }

                var stale = new List<int>(2);
                foreach (KeyValuePair<int, WorldView> pair in _worlds)
                {
                    if (pair.Key < current - 1)
                    {
                        stale.Add(pair.Key);
                    }
                }

                for (int i = 0; i < stale.Count; i++)
                {
                    DestroyWorld(stale[i]);
                }
            }

            // Outro: bring every world back for the pull-back, hidden under the white-out.
            if (!_allBuilt && t >= _coreTime + _whiteRiseSeconds)
            {
                for (int w = 0; w < _worldCount; w++)
                {
                    EnsureWorld(w);
                }

                _allBuilt = true;
            }

            bool lastBuilt = _worlds.ContainsKey(_worldCount - 1);
            if (_core != null && _core.gameObject.activeSelf != lastBuilt)
            {
                _core.gameObject.SetActive(lastBuilt);
            }
        }

        void EnsureWorld(int w)
        {
            if (w < 0 || w >= _worldCount || _worlds.ContainsKey(w))
            {
                return;
            }

            FreefallWorld world = _config.worlds[w];
            FreefallWorldSpan span = _schedule.Worlds[w];
            var rng = new System.Random(unchecked(_seed * 1000003 + w * 7919 + 17));

            var root = new GameObject("World " + (w + 1) + " " + world.worldName);
            root.transform.SetParent(transform, false);
            var view = new WorldView
            {
                index = w,
                root = root,
                pads = new WorldPads(root.transform, world, _schedule.Hits, span.firstHitIndex, span.hitCount, _config, _materials, rng),
                wall = WallView.Create(root.transform, world, span, _materials, rng)
            };
            _worlds[w] = view;
        }

        void DestroyWorld(int w)
        {
            if (_worlds.TryGetValue(w, out WorldView view))
            {
                view.pads?.Destroy();
                view.wall?.Destroy();
                if (view.root != null)
                {
                    Destroy(view.root);
                }

                _worlds.Remove(w);
            }
        }

        void UpdateWorlds(float t, float dt)
        {
            ComputeReveal(t);
            float heroY = _heroPosition.y;
            foreach (KeyValuePair<int, WorldView> pair in _worlds)
            {
                WorldView view = pair.Value;
                // The sculpture shows at full glow during the reveal and the outro.
                float showAll = Mathf.Max(_reveal, _outro);
                view.pads.Update(t, heroY, showAll, _config.padLeadSeconds);
                view.wall.Tick(t, dt, _heroPosition);
            }
        }

        void ConsumeHits(float t)
        {
            IReadOnlyList<FreefallHit> hits = _schedule.Hits;
            while (_hitCursor < hits.Count && hits[_hitCursor].time <= t)
            {
                FreefallHit hit = hits[_hitCursor++];
                if (hit.world < 0 || hit.world >= _worldCount)
                {
                    continue;
                }

                FreefallWorld world = _config.worlds[hit.world];
                Color hue = FreefallColors.Hue(world, hit.pitch01);
                if (hit.voice >= 0 && hit.voice < _voiceHue.Length)
                {
                    _voiceHue[hit.voice] = hue;
                    _marbles[hit.voice].Kick(hue);
                }

                // A scrub or a hitch can skip many hits at once; only the recent ones get effects.
                if (t - hit.time > 0.3f)
                {
                    continue;
                }

                if (_worlds.TryGetValue(hit.world, out WorldView view))
                {
                    view.wall.OnHit(in hit, hue, t);
                }

                _effects.Spawn(in hit, world, _config, hue, t, _fx);
            }
        }

        // ---------------------------------------------------------------- marbles

        void UpdateMarbles(float t, float dt)
        {
            bool heroNow = _schedule.TrySample(0, t, out Vector3 position, out Vector3 velocity);
            if (heroNow)
            {
                _heroPosition = position;
                _heroVelocity = velocity;
                _heroValid = true;
            }

            while (_heroHitCursor < _heroHits.Count - 1 && _heroHits[_heroHitCursor].time <= t)
            {
                _heroHitCursor++;
            }

            int worldNow = Mathf.Clamp(_schedule.WorldAt(t), 0, _worldCount - 1);
            bool converging = t >= _schedule.Worlds[worldNow].lastHitTime;
            float radius = _config.marbleRadius;
            _marbles[0].Set(heroNow, _heroPosition, 1f, _voiceHue[0], dt);
            for (int v = 1; v < _marbles.Length; v++)
            {
                if (!_schedule.TrySample(v, t, out Vector3 p, out Vector3 vel))
                {
                    _marbles[v].Set(false, Vector3.zero, 0f, _voiceHue[v], dt);
                    continue;
                }

                // During a transition the voices converge onto the hero's path and dissolve on contact.
                float fade = 1f;
                if (converging)
                {
                    float distance = Vector3.Distance(p, _heroPosition);
                    fade = FreefallColors.Smooth(radius * 0.4f, radius * 4.5f, distance);
                }

                _marbles[v].Set(true, p, fade, _voiceHue[v], dt);
            }
        }

        // ---------------------------------------------------------------- reveal and outro weights

        /// <summary>
        /// The pull-back that frames a finished world: ramps in over the first <c>_revealRampSeconds</c> of the
        /// fall to the next world, holds, then lets go for the dive. Also finds the outro weight.
        /// </summary>
        void ComputeReveal(float t)
        {
            _reveal = 0f;
            _revealWorld = -1;
            IReadOnlyList<FreefallWorldSpan> spans = _schedule.Worlds;
            for (int w = 0; w + 1 < _worldCount; w++)
            {
                float start = spans[w].lastHitTime;
                float end = spans[w + 1].firstHitTime;
                if (t >= start && t < end)
                {
                    float length = Mathf.Max(0.01f, end - start);
                    float x = t - start;
                    float rampIn = Mathf.Min(_revealRampSeconds, length * 0.3f);
                    float holdEnd = length * _revealHoldFraction;
                    _reveal = FreefallColors.Smooth(0f, rampIn, x) * (1f - FreefallColors.Smooth(holdEnd, length, x));
                    _revealWorld = w;
                    break;
                }
            }

            float outroStart = _coreTime + 0.3f;
            _outro = FreefallColors.Smooth(outroStart, Mathf.Max(outroStart + 1f, _duration - 0.2f), t);
        }

        // ---------------------------------------------------------------- camera

        void UpdateCamera(float t, float dt)
        {
            if (!_heroValid)
            {
                return;
            }

            float tanHalf = Mathf.Tan(_fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = Mathf.Max(0.3f, _camera.aspect);
            int worldNow = Mathf.Clamp(_schedule.WorldAt(t), 0, _worldCount - 1);
            FreefallWorld world = _config.worlds[worldNow];

            // --- descent shot
            Vector3 radial = new Vector3(_heroPosition.x, 0f, _heroPosition.z);
            Vector3 outward = radial.sqrMagnitude > 0.04f ? radial.normalized : _lastOutward;
            _lastOutward = outward;
            float reach = world.wellRadius * 0.94f;
            Vector3 flat = new Vector3(_heroPosition.x, 0f, _heroPosition.z) + outward * _outwardOfMarble;
            if (flat.magnitude > reach)
            {
                flat = flat.normalized * reach;
            }

            Vector3 descentPosition = new Vector3(flat.x, _heroPosition.y + _heightAboveMarble, flat.z);
            Vector3 nextPad = _heroHits.Count > 0 ? _heroHits[Mathf.Clamp(_heroHitCursor, 0, _heroHits.Count - 1)].position : _heroPosition;
            if (_heroHits.Count > 0 && _heroHits[Mathf.Clamp(_heroHitCursor, 0, _heroHits.Count - 1)].time <= t)
            {
                nextPad = _heroPosition;
            }

            Vector3 descentLook = Vector3.Lerp(_heroPosition, nextPad, _lookAhead);

            // Canon: frame the trailing voices too, by lifting the camera and leaning the look target toward them.
            int active = 0;
            Vector3 centroid = Vector3.zero;
            float spread = 0f;
            for (int v = 1; v < _marbles.Length; v++)
            {
                if (_marbles[v].Active)
                {
                    active++;
                    centroid += _marbles[v].Position;
                    spread = Mathf.Max(spread, Mathf.Abs(_marbles[v].Position.y - _heroPosition.y));
                }
            }

            if (active > 0)
            {
                centroid /= active;
                float canon = Mathf.Min(1f, active * 0.5f);
                descentPosition.y += Mathf.Min(spread * 0.5f, 7f) * canon;
                descentLook = Vector3.Lerp(descentLook, centroid, 0.4f * canon);
            }

            Vector3 position = descentPosition;
            Vector3 look = descentLook;

            // --- reveal pull-back (finished world's lit sculpture)
            if (_reveal > 0.0001f && _revealWorld >= 0)
            {
                FreefallWorldSpan span = _schedule.Worlds[_revealWorld];
                FreefallWorld finished = _config.worlds[_revealWorld];
                float height = Mathf.Abs(span.yTop - span.yBottom) + 3f;
                float centreY = (span.yTop + span.yBottom) * 0.5f;
                float fitV = height * 0.5f / tanHalf;
                float fitH = (finished.wellRadius + 1.2f) / (tanHalf * aspect);
                float distance = Mathf.Max(fitV, fitH) * 1.12f + finished.wellRadius;
                float azimuth = (35f + t * 4f) * Mathf.Deg2Rad;
                Vector3 centre = new Vector3(0f, centreY, 0f);
                Vector3 revealPosition = centre + new Vector3(Mathf.Cos(azimuth), 0f, Mathf.Sin(azimuth)) * distance + Vector3.up * (distance * 0.22f);
                position = Vector3.Lerp(position, revealPosition, _reveal);
                look = Vector3.Lerp(look, centre, _reveal);
            }

            // --- outro pull-back (every sculpture stacked down the well)
            if (_outro > 0.0001f)
            {
                float top = _schedule.Worlds[0].yTop;
                float bottom = Mathf.Min(_schedule.CoreCentre.y, _schedule.Worlds[_worldCount - 1].yBottom);
                float height = Mathf.Abs(top - bottom) + 8f;
                float centreY = (top + bottom) * 0.5f;
                float fitV = height * 0.5f / tanHalf;
                float fitH = (world.wellRadius + 1.5f) / (tanHalf * aspect);
                float distance = Mathf.Max(fitV, fitH) * 1.08f + world.wellRadius;
                float azimuth = (35f + t * 3f) * Mathf.Deg2Rad;
                Vector3 centre = new Vector3(0f, centreY, 0f);
                Vector3 outroPosition = centre + new Vector3(Mathf.Cos(azimuth), 0f, Mathf.Sin(azimuth)) * distance + Vector3.up * (distance * 0.12f);
                position = Vector3.Lerp(position, outroPosition, _outro);
                look = Vector3.Lerp(look, centre, _outro);
            }

            // Roll: continuous about the view axis on the descent, unwinding while the camera pulls back.
            _roll += world.cameraRollDegreesPerSecond * dt;
            float wrapped = Mathf.DeltaAngle(0f, _roll);
            float roll = wrapped * (1f - Mathf.Max(_reveal, _outro));

            float smooth = Mathf.Lerp(_smoothSeconds, 0.55f, Mathf.Max(_reveal, _outro));
            _rig.Apply(position, look, roll, dt, smooth);
        }

        // ---------------------------------------------------------------- sky, fog, light

        Color SkyColour(int w, float progress)
        {
            FreefallWorld world = _config.worlds[w];
            Color gradient = Color.Lerp(world.skyTop, world.skyBottom, Mathf.Clamp01(progress));
            Color c = Color.Lerp(gradient, world.fogColor, _fogTint) * _skyBrightness;
            c.a = 1f;
            return c;
        }

        float ProgressInWorld(int w)
        {
            FreefallWorldSpan span = _schedule.Worlds[w];
            float range = span.yTop - span.yBottom;
            if (Mathf.Abs(range) < 0.01f)
            {
                return 0f;
            }

            return Mathf.Clamp01((span.yTop - _heroPosition.y) / range);
        }

        void UpdateSky(float t)
        {
            int w = Mathf.Clamp(_schedule.WorldAt(t), 0, _worldCount - 1);
            Color sky;
            IReadOnlyList<FreefallWorldSpan> spans = _schedule.Worlds;

            int fromTransition = -1;
            for (int i = 0; i + 1 < _worldCount; i++)
            {
                if (t >= spans[i].lastHitTime && t < spans[i + 1].firstHitTime)
                {
                    fromTransition = i;
                    break;
                }
            }

            if (fromTransition >= 0)
            {
                float start = spans[fromTransition].lastHitTime;
                float end = spans[fromTransition + 1].firstHitTime;
                float u = FreefallColors.Smooth(0f, 1f, (t - start) / Mathf.Max(0.01f, end - start));
                sky = Color.Lerp(SkyColour(fromTransition, 1f), SkyColour(fromTransition + 1, 0f), u);
            }
            else if (t >= spans[_worldCount - 1].lastHitTime)
            {
                sky = SkyColour(_worldCount - 1, 1f);
            }
            else
            {
                sky = SkyColour(w, ProgressInWorld(w));
            }

            _camera.backgroundColor = sky;

            // Fog matches the background exactly so the far end of a band melts into the sky instead of cutting off.
            float pull = Mathf.Max(_reveal, _outro);
            float distance = Mathf.Max(8f, _rig.DistanceToLook);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = Mathf.Lerp(_fogStart, distance * 0.9f, pull);
            RenderSettings.fogEndDistance = Mathf.Lerp(_fogEnd, distance * 4f + 400f * _outro, pull);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(sky, Color.white, 0.14f);
        }

        // ---------------------------------------------------------------- core and white-out

        void UpdateCore(float t)
        {
            if (_core == null || !_core.gameObject.activeSelf)
            {
                return;
            }

            float since = t - _coreTime;
            float flash = since >= 0f ? Mathf.Exp(-since * 2.4f) : 0f;
            float breathe = 1f + 0.04f * Mathf.Sin(t * 2.2f);
            float scale = _coreRadius * 2f * breathe * (1f + 1.6f * flash);
            _core.localScale = Vector3.one * scale;

            Block.Clear();
            Block.SetColor(FreefallColors.BaseColor, new Color(3.5f + 8f * flash, 3.5f + 8f * flash, 3.8f + 8f * flash, 1f));
            _coreRenderer.SetPropertyBlock(Block);
            _coreHalo.localScale = Vector3.one * 5f;
            Block.Clear();
            Block.SetColor(FreefallColors.BaseColor, new Color(2.2f, 2.3f, 2.6f, 0.45f + 0.4f * flash));
            _coreHaloRenderer.SetPropertyBlock(Block);
        }

        void UpdateWhiteOut(float t)
        {
            if (_whiteOut == null)
            {
                return;
            }

            float rise = FreefallColors.Smooth(_coreTime, _coreTime + _whiteRiseSeconds, t);
            float fallStart = _coreTime + _whiteRiseSeconds + _whiteHoldSeconds;
            float fall = FreefallColors.Smooth(fallStart, fallStart + _whiteFallSeconds, t);
            float alpha = rise * (1f - fall);
            if (!Mathf.Approximately(_whiteOut.alpha, alpha))
            {
                _whiteOut.alpha = alpha;
            }
        }

        // ---------------------------------------------------------------- HUD

        void UpdateHud(float t)
        {
            if (_hud == null)
            {
                return;
            }

            string caption = string.Empty;
            string sub = string.Empty;
            int priority = 0;
            float latest = float.MinValue;
            for (int i = 0; i < _captions.Count; i++)
            {
                CaptionEvent e = _captions[i];
                if (t < e.start || t >= e.end)
                {
                    continue;
                }

                if (e.priority > priority || (e.priority == priority && e.start > latest))
                {
                    priority = e.priority;
                    latest = e.start;
                    caption = e.caption;
                    sub = e.sub;
                }
            }

            if (caption != _captionShown)
            {
                _captionShown = caption;
                _hud.SetCaption(caption, caption.Length > 0);
            }

            if (sub != _subShown)
            {
                _subShown = sub;
                _hud.SetSubline(sub);
            }

            string footer = BuildFooter(t);
            if (footer != _footerShown)
            {
                _footerShown = footer;
                _hud.SetFooter(footer);
            }
        }

        string BuildFooter(float t)
        {
            if (_worldCount <= 1)
            {
                return string.Empty;
            }

            int current = Mathf.Clamp(_schedule.WorldAt(t), 0, _worldCount - 1);
            if (t >= _coreTime)
            {
                current = _worldCount - 1;
            }

            Color accent = Color.Lerp(FreefallColors.Hue(_config.worlds[current], 0.5f), Color.white, 0.25f);
            string on = "<color=#" + ColorUtility.ToHtmlStringRGB(accent) + ">";
            _text.Length = 0;
            for (int i = 0; i < _worldCount; i++)
            {
                if (i > 0)
                {
                    _text.Append("  ");
                }

                if (i <= current)
                {
                    _text.Append(on).Append('●').Append("</color>");
                }
                else
                {
                    _text.Append('○');
                }
            }

            return _text.ToString();
        }
    }
}
