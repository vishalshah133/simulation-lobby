using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.Convergence
{
    /// <summary>
    /// Draws and sounds a <see cref="ConvergenceSimulation"/>: the swarm (one hue per launch angle),
    /// the morphing boundary, the start and target dots, the meet flash, the HUD and the audio. Reads in
    /// <c>LateUpdate</c> and never writes to the run.
    /// </summary>
    [RequireComponent(typeof(ConvergenceSimulation))]
    public sealed class ConvergencePresenter : MonoBehaviour
    {
        [Header("Wiring (set by the scene builder via Bind)")]
        [SerializeField] ConvergenceSimulation _simulation;
        [SerializeField] VersusScoreboardHud _hud;
        [SerializeField] GameObject _bodyTemplate;
        [SerializeField] LineRenderer _boundary;
        [SerializeField] SpriteRenderer _startDot;
        [SerializeField] SpriteRenderer _startRing;
        [SerializeField] SpriteRenderer _targetDot;
        [SerializeField] SpriteRenderer _targetRing;
        [SerializeField] SpriteRenderer _ghostRing;

        [Tooltip("World-space TextMeshPro decal beside the ghost ring. The presenter picks the side that fits.")]
        [SerializeField] TMP_Text _ghostLabel;

        [Tooltip("Gap between the ghost ring's edge and the label, world units.")]
        [SerializeField] float _ghostLabelGap = 0.15f;

        Vector2 _ghostLabelSize;
        [SerializeField] Sprite _glowSprite;
        [SerializeField] Sprite _ringSprite;
        [SerializeField] Material _flashMaterial;

        [Header("Swarm")]
        [Tooltip("Hue of body 0. Body i is launched at 2*pi*i/N, so the hue wheel is the launch angle.")]
        [Range(0f, 1f)] [SerializeField] float _hueStart = 0f;

        [Range(0f, 1f)] [SerializeField] float _saturation = 0.75f;

        [Tooltip("Trail width as a fraction of the body's diameter.")]
        [Range(0.05f, 1f)] [SerializeField] float _trailWidthFraction = 0.35f;

        [SerializeField] int _bodySortingOrder = 10;

        [Header("Boundary")]
        [SerializeField] Color _boundaryColor = new Color(0.95f, 0.97f, 1f, 1f);

        [Min(0.005f)] [SerializeField] float _boundaryWidth = 0.05f;

        [Header("Dots")]
        [Tooltip("Dot diameter, world units.")]
        [Min(0.05f)] [SerializeField] float _dotSize = 0.42f;

        [Tooltip("White ring diameter as a multiple of the dot's. Also the ghost ring's size.")]
        [Range(1f, 2f)] [SerializeField] float _ringScale = 1.35f;

        [SerializeField] Color _startColor = new Color(1f, 0.25f, 0.3f, 1f);
        [SerializeField] Color _targetColor = new Color(0.25f, 0.6f, 1f, 1f);
        [SerializeField] Color _ringColor = new Color(1f, 1f, 1f, 0.9f);

        [Tooltip("Where the start dot began, shown faintly while the dot is elsewhere.")]
        [Range(0f, 1f)] [SerializeField] float _ghostAlpha = 0.35f;

        [Tooltip("Pulses per second while holding.")]
        [Range(0.2f, 4f)] [SerializeField] float _dotPulseHz = 1.2f;

        [Tooltip("Below the balls, so the fan is seen leaving the dots.")]
        [SerializeField] int _dotSortingOrder = 4;

        [Header("Meet flash")]
        [SerializeField] int _flashSortingOrder = 20;

        [Header("Audio")]
        [Tooltip("Most wall-hit notes per rendered frame. A fan of bodies hits the wall in one frame; " +
                 "playing every one is noise, not a sweep.")]
        [Range(1, 6)] [SerializeField] int _maxNotesPerFrame = 2;

        [Tooltip("Notes in the sweep scale (pentatonic, about 2 octaves at 11).")]
        [Range(5, 16)] [SerializeField] int _scaleNotes = 11;

        [Tooltip("Sweep root, Hz.")]
        [Range(110f, 523f)] [SerializeField] float _scaleRootHz = 220f;

        [Min(0.1f)] [SerializeField] float _noteSeconds = 0.55f;

        [Range(2, 12)] [SerializeField] int _voiceCount = 6;

        [Range(0f, 1f)] [SerializeField] float _noteVolume = 0.28f;

        [Range(0f, 1f)] [SerializeField] float _meetVolume = 0.9f;

        [Range(0f, 1f)] [SerializeField] float _missVolume = 0.7f;

        [Range(0f, 0.5f)] [SerializeField] float _ambientVolume = 0.12f;

        sealed class BodyView
        {
            public GameObject Object;
            public Transform Transform;
            public SpriteRenderer Renderer;
            public TrailRenderer Trail;
            public int ColourTotal;
            public bool TrailOn = true;
        }

        readonly List<BodyView> _views = new List<BodyView>(128);
        Transform _swarmRoot;
        int _shownCount;
        float _appliedTrailSeconds = -1f;

        ConvergencePhase _prevPhase = ConvergencePhase.Done;
        int _prevCount;
        int _prevRound = -1;

        Vector3[] _outlinePositions = new Vector3[0];
        int _outlineCount = -1;

        ConvergenceFlash _flash;
        float _dotUnit = 1f;
        float _ringUnit = 1f;

        long _hitCursor;
        int _meetSeen;
        int _missSeen;

        AudioClip[] _scale;
        AudioSource[] _voices;
        int _nextVoice;
        AudioSource _sfx;
        AudioClip _glassClink;
        AudioClip _meetLow;
        AudioClip _meetFifth;
        AudioClip _missLow;
        AudioClip _missDetuned;
        AudioClip _missThud;

        // HUD caches: only push when something changed, since a TMP rebuild is not free.
        bool _hudInitialised;
        bool _prewarmed;
        int _hudCounterShown = -2;
        int _hudTotalShown = -2;
        int _hudResultKey = -1;
        int _hudVerdictCount = -1;
        string _hudTitleShown;

        void Reset()
        {
            _simulation = GetComponent<ConvergenceSimulation>();
        }

        /// <summary>Assigned by the scene builder.</summary>
        /// <param name="simulation">The run to observe (same GameObject).</param>
        /// <param name="hud">Canvas scoreboard built by ScoreboardCanvasBuilder.</param>
        /// <param name="bodyTemplate">
        /// An INACTIVE GameObject with a SpriteRenderer (white circle sprite, any pixel size; it is
        /// scaled to Config.ballRadius) and a TrailRenderer (particle-style material, emitting on,
        /// autodestruct off, alignment View; time, width, colours and sorting are overridden here).
        /// Cloned lazily, never edited.
        /// </param>
        /// <param name="boundary">A LineRenderer with a material; the presenter sets points, loop, colour and width.</param>
        /// <param name="startDot">Solid disc SpriteRenderer for the start point (circle sprite, any pixel size).</param>
        /// <param name="startRing">White ring SpriteRenderer around the start dot (ring sprite).</param>
        /// <param name="targetDot">Solid disc SpriteRenderer for the target point.</param>
        /// <param name="targetRing">White ring SpriteRenderer around the target dot.</param>
        /// <param name="ghostRing">Faint ring at the focus while red is off it; fades out by launch.</param>
        /// <param name="glowSprite">SoftGlow sprite for the meet bloom.</param>
        /// <param name="ringSprite">Ring sprite for the expanding meet ring.</param>
        /// <param name="flashMaterial">Optional additive sprite material for the flash. Null = default sprite material.</param>
        public void Bind(ConvergenceSimulation simulation, VersusScoreboardHud hud, GameObject bodyTemplate,
                         LineRenderer boundary, SpriteRenderer startDot, SpriteRenderer startRing,
                         SpriteRenderer targetDot, SpriteRenderer targetRing, SpriteRenderer ghostRing,
                         Sprite glowSprite, Sprite ringSprite, Material flashMaterial, TMP_Text ghostLabel = null)
        {
            _ghostLabel = ghostLabel;
            _simulation = simulation;
            _hud = hud;
            _bodyTemplate = bodyTemplate;
            _boundary = boundary;
            _startDot = startDot;
            _startRing = startRing;
            _targetDot = targetDot;
            _targetRing = targetRing;
            _ghostRing = ghostRing;
            _glowSprite = glowSprite;
            _ringSprite = ringSprite;
            _flashMaterial = flashMaterial;
        }

        void Start()
        {
            if (_boundary != null)
            {
                _boundary.useWorldSpace = true;
                _boundary.startColor = _boundaryColor;
                _boundary.endColor = _boundaryColor;
                _boundary.widthMultiplier = _boundaryWidth;
                _boundary.numCornerVertices = 2;
                _boundary.enabled = false;
            }

            if (_startDot != null)
            {
                _dotUnit = SpriteUnit(_startDot.sprite);
            }

            if (_startRing != null)
            {
                _ringUnit = SpriteUnit(_startRing.sprite);
            }

            SetupDot(_startDot, _startColor, _dotSortingOrder);
            SetupDot(_startRing, _ringColor, _dotSortingOrder + 1);
            SetupDot(_targetDot, _targetColor, _dotSortingOrder);
            SetupDot(_targetRing, _ringColor, _dotSortingOrder + 1);
            SetupDot(_ghostRing, new Color(_ringColor.r, _ringColor.g, _ringColor.b, _ghostAlpha), _dotSortingOrder - 1);

            var swarm = new GameObject("Swarm");
            swarm.transform.SetParent(transform, false);
            _swarmRoot = swarm.transform;

            if (_glowSprite != null || _ringSprite != null)
            {
                _flash = ConvergenceFlash.Create(transform, _glowSprite, _ringSprite, _flashMaterial, _flashSortingOrder);
            }

            BuildAudio();

            var runner = GetComponent<SimulationRunner>();
            Prewarm(runner != null ? runner.config as ConvergenceConfig : null);
        }

        /// <summary>
        /// Instantiate the pool to the largest round's ball count up front (inactive), so a 600-ball round
        /// never allocates mid-video. Runs from Start, or the first LateUpdate if the config isn't known yet.
        /// </summary>
        void Prewarm(ConvergenceConfig config)
        {
            if (_prewarmed || config == null || _bodyTemplate == null || _swarmRoot == null)
            {
                return;
            }

            _prewarmed = true;
            int max = 0;
            for (int i = 0; i < config.RoundCount; i++)
            {
                max = Mathf.Max(max, config.RoundBallCount(i));
            }

            _views.Capacity = Mathf.Max(_views.Capacity, max);
            while (_views.Count < max)
            {
                _views.Add(CreateView(_views.Count, config, config.RoundBallCount(0)));
            }
        }

        static void SetupDot(SpriteRenderer renderer, Color color, int order)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.enabled = false;
        }

        static float SpriteUnit(Sprite sprite)
        {
            return sprite != null && sprite.bounds.size.x > 0f ? 1f / sprite.bounds.size.x : 1f;
        }

        void BuildAudio()
        {
            _scale = ProceduralToneBank.CreateScale(_scaleNotes, _scaleRootHz, _noteSeconds);
            _glassClink = ProceduralToneBank.CreateGlassClink(1f, 1.4f, 3, "MeetClink");
            _meetLow = ProceduralToneBank.CreateTone(196f, 1.5f, "MeetLow");
            _meetFifth = ProceduralToneBank.CreateTone(293.66f, 1.5f, "MeetFifth");
            _missLow = ProceduralToneBank.CreateTone(110f, 1.2f, "MissLow");
            _missDetuned = ProceduralToneBank.CreateTone(116.5f, 1.2f, "MissDetuned");
            _missThud = ProceduralToneBank.CreateImpactThud(0.7f, 0.22f, 41, "MissThud");

            _voices = new AudioSource[_voiceCount];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = AddSource(_noteVolume);
            }

            _sfx = AddSource(1f);

            AudioSource ambient = AddSource(_ambientVolume);
            ambient.clip = ProceduralToneBank.CreateAmbientBed(4f, 55f, "ConvAmbient");
            ambient.loop = true;
            ambient.Play();
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
            if (_simulation == null || _simulation.Config == null)
            {
                return;
            }

            Prewarm(_simulation.Config);
            UpdateSwarm();
            UpdateBoundary();
            UpdateDots();
            UpdateEvents();
            UpdateHud();

            _prevPhase = _simulation.Phase;
            _prevCount = _simulation.ParticleCount;
            _prevRound = _simulation.RoundIndex;
        }

        // ---------------------------------------------------------------- swarm

        void UpdateSwarm()
        {
            ConvergenceConfig config = _simulation.Config;
            int roundTotal = _simulation.RoundBallCount;
            int count = Mathf.Min(_simulation.ParticleCount, roundTotal);

            // A new round's bodies appear at the start; clear trails so no streak shoots in from the old spot.
            bool launched = (_simulation.Phase == ConvergencePhase.Running && _prevPhase != ConvergencePhase.Running) ||
                            (count > 0 && _prevCount == 0) ||
                            _simulation.RoundIndex != _prevRound;

            if (_bodyTemplate != null)
            {
                while (_views.Count < count)
                {
                    _views.Add(CreateView(_views.Count, config, roundTotal));
                }
            }

            float trailSeconds = _simulation.CurrentRound.trailSeconds;
            bool trailsOn = trailSeconds > 0f;
            bool trailChanged = !Mathf.Approximately(trailSeconds, _appliedTrailSeconds);

            int active = Mathf.Min(count, _views.Count);
            for (int i = 0; i < active; i++)
            {
                BodyView view = _views[i];
                if (!view.Object.activeSelf)
                {
                    view.Object.SetActive(true);
                    launched = true;
                }
            }

            for (int i = 0; i < active; i++)
            {
                BodyView view = _views[i];
                Vector2 p = _simulation.GetParticlePosition(i);
                view.Transform.position = new Vector3(p.x, p.y, 0f);
                if (view.ColourTotal != roundTotal)
                {
                    // The hue wheel is the launch angle, so it follows this round's ball count.
                    ApplyColour(view, i, roundTotal, config);
                }

                if (view.Trail != null && view.TrailOn != trailsOn)
                {
                    view.TrailOn = trailsOn;
                    view.Trail.emitting = trailsOn;
                    if (!trailsOn)
                    {
                        view.Trail.Clear();
                    }
                }

                if (trailsOn && trailChanged && view.Trail != null)
                {
                    view.Trail.time = trailSeconds;
                }

                if ((launched || !trailsOn) && view.Trail != null)
                {
                    view.Trail.Clear();
                }
            }

            for (int i = active; i < _shownCount && i < _views.Count; i++)
            {
                _views[i].Object.SetActive(false);
            }

            if (trailChanged)
            {
                _appliedTrailSeconds = trailSeconds;
            }

            _shownCount = active;
        }

        BodyView CreateView(int index, ConvergenceConfig config, int total)
        {
            GameObject clone = Instantiate(_bodyTemplate, _swarmRoot);
            clone.name = "Body_" + index;
            clone.SetActive(false);

            var view = new BodyView
            {
                Object = clone,
                Transform = clone.transform,
                Renderer = clone.GetComponent<SpriteRenderer>(),
                Trail = clone.GetComponent<TrailRenderer>()
            };

            float diameter = config.ballRadius * 2f;
            if (view.Renderer != null)
            {
                view.Transform.localScale = Vector3.one * (diameter * SpriteUnit(view.Renderer.sprite));
                view.Renderer.sortingOrder = _bodySortingOrder;
            }

            if (view.Trail != null)
            {
                // Trail width is in world units, independent of the transform scale.
                view.Trail.widthMultiplier = diameter * _trailWidthFraction;
                view.Trail.sortingOrder = _bodySortingOrder - 1;
                view.Trail.time = Mathf.Max(0.05f, _simulation.CurrentRound.trailSeconds);
                view.Trail.emitting = true;
            }

            ApplyColour(view, index, total, config);
            return view;
        }

        void ApplyColour(BodyView view, int index, int total, ConvergenceConfig config)
        {
            view.ColourTotal = total;
            float span = config.hueSpan;
            float fraction = span >= 1f
                ? index / (float)Mathf.Max(1, total)
                : (total <= 1 ? 0f : span * index / (total - 1));
            float hue = Mathf.Repeat(_hueStart + fraction, 1f);
            Color body = Color.HSVToRGB(hue, _saturation, 1f);
            if (view.Renderer != null)
            {
                view.Renderer.color = body;
            }

            if (view.Trail != null)
            {
                view.Trail.startColor = body;
                Color clear = body;
                clear.a = 0f;
                view.Trail.endColor = clear;
            }
        }

        // ---------------------------------------------------------------- boundary and marker

        void UpdateBoundary()
        {
            if (_boundary == null)
            {
                return;
            }

            IReadOnlyList<Vector2> outline = _simulation.Outline;
            int n = outline.Count;
            if (n < 2)
            {
                _boundary.enabled = false;
                return;
            }

            _boundary.enabled = true;
            if (n != _outlineCount)
            {
                _outlinePositions = new Vector3[n];
                _boundary.positionCount = n;
                _outlineCount = n;
            }

            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i];
                _outlinePositions[i] = new Vector3(p.x, p.y, 0f);
            }

            _boundary.SetPositions(_outlinePositions);

            bool closed = _simulation.OutlineClosed;
            if (_boundary.loop != closed)
            {
                _boundary.loop = closed;
            }
        }

        void UpdateDots()
        {
            // Shown from the first frame through the end card; only the end of the run hides them.
            bool show = _simulation.Phase != ConvergencePhase.Done;

            // Presentation time on purpose: the hold is a still frame, the pulse shows it is alive.
            float pulse = 1f;
            if (_simulation.Phase == ConvergencePhase.Hold)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * _dotPulseHz * Mathf.PI * 2f);
                pulse = 0.92f + 0.16f * wave;
            }

            PlaceDot(_startDot, _startRing, _simulation.StartPoint, show && _simulation.Config.showStartPoint, pulse);
            PlaceDot(_targetDot, _targetRing, _simulation.TargetPoint, show, pulse);

            if (_ghostRing != null)
            {
                // Marks the focus, only while red is off it: the spot red has to be on for the trick to
                // work. Red riding the focus (stretch, squash) shows none. Fades over the guess beat and
                // is gone by launch.
                ConvergencePhase phase = _simulation.Phase;
                Vector2 from = _simulation.IdealStartPoint;
                bool offFocus = Vector2.Distance(from, _simulation.StartPoint) > 0.05f;
                bool ghost = offFocus && (phase == ConvergencePhase.Transition || phase == ConvergencePhase.Hold);
                _ghostRing.enabled = ghost;
                UpdateGhostLabel(ghost, from, phase);
                if (ghost)
                {
                    float fade = phase == ConvergencePhase.Hold ? 1f - _simulation.PhaseProgress : 1f;
                    _ghostRing.transform.position = new Vector3(from.x, from.y, 0f);
                    _ghostRing.transform.localScale = Vector3.one * (_dotSize * _ringScale * _ringUnit);
                    Color c = _ghostRing.color;
                    c.a = _ghostAlpha * fade;
                    _ghostRing.color = c;
                }
            }
        }

        /// <summary>
        /// The ghost's decal. Tries above, left, right and below the ring, and takes the first spot whose
        /// box sits wholly inside the outline without covering red. A narrow outline (a stretched ellipse
        /// near its tip) fits none, so the label then sits just outside the wall at the ring's height, on
        /// the side away from red. It never straddles the line.
        /// </summary>
        void UpdateGhostLabel(bool ghost, Vector2 at, ConvergencePhase phase)
        {
            if (_ghostLabel == null)
            {
                return;
            }

            string label = _simulation.Config.idealSpotLabel;
            bool show = ghost && !string.IsNullOrEmpty(label);
            _ghostLabel.enabled = show;
            if (!show)
            {
                return;
            }

            if (_ghostLabel.text != label || _ghostLabelSize == Vector2.zero)
            {
                _ghostLabel.text = label;
                _ghostLabelSize = _ghostLabel.GetPreferredValues(label);
                _ghostLabel.rectTransform.sizeDelta = _ghostLabelSize;
            }

            Vector2 size = _ghostLabelSize;
            Vector2 half = size * 0.5f;
            float ringRadius = _dotSize * _ringScale * 0.5f;
            float reach = ringRadius + _ghostLabelGap;
            Vector2 red = _simulation.StartPoint;

            Vector2 centre = at + new Vector2(0f, reach + half.y);
            bool placed = false;
            for (int k = 0; k < 4 && !placed; k++)
            {
                Vector2 candidate;
                switch (k)
                {
                    case 0: candidate = at + new Vector2(0f, reach + half.y); break;
                    case 1: candidate = at + new Vector2(-(reach + half.x), 0f); break;
                    case 2: candidate = at + new Vector2(reach + half.x, 0f); break;
                    default: candidate = at + new Vector2(0f, -(reach + half.y)); break;
                }

                if (BoxInsideOutline(candidate, half) && !BoxNear(candidate, half, red, ringRadius))
                {
                    centre = candidate;
                    placed = true;
                }
            }

            if (!placed)
            {
                // Outside the wall at the ring's height, on the side away from red.
                bool left = red.x >= at.x;
                float edge = OutlineExtentX(at.y, half.y, left);
                centre = new Vector2(left ? edge - _ghostLabelGap - half.x : edge + _ghostLabelGap + half.x, at.y);
            }

            // The builder pivots the label on its right edge; place by centre regardless.
            Vector2 pivot = _ghostLabel.rectTransform.pivot;
            Vector2 pivotOffset = new Vector2((pivot.x - 0.5f) * size.x, (pivot.y - 0.5f) * size.y);
            _ghostLabel.transform.position = new Vector3(centre.x + pivotOffset.x, centre.y + pivotOffset.y, 0f);

            float fade = phase == ConvergencePhase.Hold ? 1f - _simulation.PhaseProgress : 1f;
            Color c = _ghostLabel.color;
            c.a = fade;
            _ghostLabel.color = c;
        }

        bool BoxInsideOutline(Vector2 centre, Vector2 half)
        {
            return InsideOutline(centre + new Vector2(-half.x, -half.y)) &&
                   InsideOutline(centre + new Vector2(half.x, -half.y)) &&
                   InsideOutline(centre + new Vector2(-half.x, half.y)) &&
                   InsideOutline(centre + new Vector2(half.x, half.y));
        }

        static bool BoxNear(Vector2 centre, Vector2 half, Vector2 point, float radius)
        {
            float dx = Mathf.Max(Mathf.Abs(point.x - centre.x) - half.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(point.y - centre.y) - half.y, 0f);
            return dx * dx + dy * dy < radius * radius;
        }

        /// <summary>Even-odd point-in-polygon against the current outline.</summary>
        bool InsideOutline(Vector2 p)
        {
            IReadOnlyList<Vector2> outline = _simulation.Outline;
            int n = outline.Count;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>The outline's leftmost (or rightmost) x within a horizontal band, so a label clears the wall.</summary>
        float OutlineExtentX(float y, float halfHeight, bool left)
        {
            IReadOnlyList<Vector2> outline = _simulation.Outline;
            float extent = left ? float.MaxValue : float.MinValue;
            bool any = false;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 p = outline[i];
                if (Mathf.Abs(p.y - y) <= halfHeight)
                {
                    extent = left ? Mathf.Min(extent, p.x) : Mathf.Max(extent, p.x);
                    any = true;
                }
            }

            return any ? extent : 0f;
        }

        void PlaceDot(SpriteRenderer dot, SpriteRenderer ring, Vector2 at, bool show, float pulse)
        {
            Vector3 position = new Vector3(at.x, at.y, 0f);
            if (dot != null)
            {
                dot.enabled = show;
                dot.transform.position = position;
                dot.transform.localScale = Vector3.one * (_dotSize * _dotUnit * pulse);
            }

            if (ring != null)
            {
                ring.enabled = show;
                ring.transform.position = position;
                ring.transform.localScale = Vector3.one * (_dotSize * _ringScale * _ringUnit * pulse);
            }
        }

        // ---------------------------------------------------------------- events (flash + audio)

        void UpdateEvents()
        {
            // Wall hits: the cursor always advances to the end, but only a few are voiced.
            long total = _simulation.WallHitCount;
            if (total < _hitCursor)
            {
                _hitCursor = total;
            }

            int played = 0;
            for (long serial = _hitCursor; serial < total && played < _maxNotesPerFrame; serial++)
            {
                if (_simulation.TryGetWallHit(serial, out var hit))
                {
                    PlayNote(hit.Pitch01);
                    played++;
                }
            }

            _hitCursor = total;

            int meet = _simulation.MeetSerial;
            if (meet > _meetSeen)
            {
                _meetSeen = meet;
                if (_flash != null)
                {
                    _flash.Play(_simulation.TargetPoint);
                }

                PlayMeet();
            }
            else
            {
                _meetSeen = meet;
            }

            int miss = _simulation.MissSerial;
            if (miss > _missSeen)
            {
                _missSeen = miss;
                PlayMiss();
            }
            else
            {
                _missSeen = miss;
            }
        }

        void PlayNote(double pitch01)
        {
            if (_voices == null || _scale == null)
            {
                return;
            }

            int index = Mathf.Clamp(Mathf.RoundToInt((float)pitch01 * (_scale.Length - 1)), 0, _scale.Length - 1);
            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.PlayOneShot(_scale[index], 1f);
        }

        void PlayMeet()
        {
            if (_sfx == null)
            {
                return;
            }

            _sfx.PlayOneShot(_glassClink, _meetVolume);
            _sfx.PlayOneShot(_meetLow, _meetVolume);
            _sfx.PlayOneShot(_meetFifth, _meetVolume * 0.6f);
        }

        void PlayMiss()
        {
            if (_sfx == null)
            {
                return;
            }

            // A minor-second pair beats against itself: deliberately sour next to the pentatonic sweep.
            _sfx.PlayOneShot(_missLow, _missVolume);
            _sfx.PlayOneShot(_missDetuned, _missVolume);
            _sfx.PlayOneShot(_missThud, _missVolume);
        }

        // ---------------------------------------------------------------- HUD

        void UpdateHud()
        {
            if (_hud == null)
            {
                return;
            }

            ConvergenceConfig config = _simulation.Config;
            ConvergencePhase phase = _simulation.Phase;

            if (!_hudInitialised)
            {
                _hudInitialised = true;
                _hud.SetQuestion(config.question);
                _hud.SetScoreVisible(false);
                _hud.SetRoundLabel(string.Empty);
            }

            string title = phase == ConvergencePhase.EndCard && !string.IsNullOrEmpty(config.endTitle)
                ? config.endTitle
                : config.title;
            if (title != _hudTitleShown)
            {
                _hudTitleShown = title;
                _hud.SetTitle(title);
            }

            UpdateCounter(config, phase);
            UpdateCaption(config, phase);
        }

        void UpdateCounter(ConvergenceConfig config, ConvergencePhase phase)
        {
            // -1 = blank. Live until the verdict closes, then the locked verdict count (DisplayCount).
            int shown = -1;
            if (phase == ConvergencePhase.Running || phase == ConvergencePhase.Result)
            {
                shown = _simulation.DisplayCount;
            }

            int total = _simulation.RoundBallCount;
            if (shown == _hudCounterShown && total == _hudTotalShown)
            {
                return;
            }

            _hudCounterShown = shown;
            _hudTotalShown = total;
            // alert is the HUD's failure red; even a full hit stays calm here.
            _hud.SetCounterText(shown < 0 ? string.Empty : config.counterLabel + "  " + shown + " / " + total, false);
        }

        void UpdateCaption(ConvergenceConfig config, ConvergencePhase phase)
        {
            // Phase and round pick the line; the verdict count picks the Result text.
            // The answer lands the moment the verdict closes, while the trails are still drawing.
            bool showResult = phase == ConvergencePhase.Result ||
                              (phase == ConvergencePhase.Running && _simulation.HasVerdict);
            int verdictCount = showResult ? _simulation.VerdictCount : -1;
            int key = (showResult ? 9 : (int)phase) + 16 * (_simulation.RoundIndex + 1);
            if (key == _hudResultKey && verdictCount == _hudVerdictCount)
            {
                return;
            }

            _hudResultKey = key;
            _hudVerdictCount = verdictCount;

            switch (phase)
            {
                case ConvergencePhase.Hold:
                    _hud.ShowNeutral(string.IsNullOrEmpty(config.holdCaption)
                        ? _simulation.CurrentRound.caption
                        : config.holdCaption);
                    break;
                case ConvergencePhase.Transition:
                case ConvergencePhase.Running:
                case ConvergencePhase.Result:
                    if (showResult)
                    {
                        string line = (_simulation.CurrentRound.result ?? string.Empty)
                            .Replace("{n}", verdictCount.ToString())
                            .Replace("{total}", _simulation.RoundBallCount.ToString());
                        _hud.ShowResult(line, _simulation.Verdict == ConvergenceAnswer.Meet);
                    }
                    else
                    {
                        _hud.ShowNeutral(_simulation.CurrentRound.caption);
                    }

                    break;
                case ConvergencePhase.EndCard:
                    _hud.ShowResult(config.endCard, true);
                    break;
                default:
                    _hud.ShowResult(null, false);
                    break;
            }
        }
    }
}
