using System.Collections.Generic;
using System.Globalization;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Simulations.Brachistochrone.Kernel;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.Brachistochrone
{
    /// <summary>
    /// Draws and sounds a <see cref="BrachistochroneSimulation"/>: the glowing track tubes (growing in, morphing
    /// to a new finish), the spinning balls, the start gate, the checkered finish flag, one-word track labels
    /// placed so none overlap, the round-1 length tags, the winner pulse and burst, the caption HUD and the audio.
    /// Reads the run in <c>LateUpdate</c> and never writes to it. The only randomness is cosmetic (palette start
    /// from the run, burst directions) and comes from a local generator seeded from the run seed.
    /// </summary>
    [RequireComponent(typeof(BrachistochroneSimulation))]
    public sealed class BrachistochronePresenter : MonoBehaviour
    {
        /// <summary>Everything the scene builder hands over. The presenter creates and drives the rest at runtime.</summary>
        [System.Serializable]
        public sealed class SceneRig
        {
            public Camera camera;
            public Sprite circle;
            public Sprite glow;
            public Sprite square;
            public Sprite ring;
            public Sprite checker;
        }

        sealed class TrackView
        {
            public Transform root;
            public LineRenderer line;
            public LineRenderer glow;
            public Transform ball;
            public Transform spin;
            public SpriteRenderer rim;
            public SpriteRenderer body;
            public SpriteRenderer shine;
            public SpriteRenderer bar;
            public TrailRenderer trail;
            public TextMeshPro label;
            public TextMeshPro length;
            public readonly Vector3[] points = new Vector3[PathSamples + 1];
            public Color color = Color.white;
            public float dim = 1f;
            public float labelAlpha;
            public float lengthAlpha;
            public Vector2 labelSize;
            public Vector2 lengthSize;
            public Vector2 labelPos;
            public Vector2 lengthPos;
            public Vector2 lastBall;
            public bool hasBall;
            public float spinAngle;
            public int prevPlace;
        }

        const int PathSamples = 96;
        const int LayoutSamples = 48;

        [Header("Wiring (set by the scene builder via Bind)")]
        [SerializeField] BrachistochroneSimulation _simulation;
        [SerializeField] CaptionHud _hud;
        [SerializeField] SceneRig _rig = new SceneRig();

        [Header("Camera: the band between the HUD panels")]
        [Tooltip("Top of the play band as a fraction of screen height from the top (the question panel ends here).")]
        [Range(0.1f, 0.4f)] [SerializeField] float _bandTop = 0.26f;

        [Tooltip("Bottom of the play band as a fraction of screen height from the top (the bottom line starts here).")]
        [Range(0.6f, 0.95f)] [SerializeField] float _bandBottom = 0.82f;

        [Tooltip("World margin around the tracks, finish flag and start gate when fitting.")]
        [Range(0.2f, 2f)] [SerializeField] float _fitMargin = 0.9f;

        [Tooltip("Seconds for the camera to settle onto a new fit.")]
        [Range(0.1f, 1.5f)] [SerializeField] float _cameraSeconds = 0.5f;

        [Header("Tracks and balls")]
        [Range(0.06f, 0.3f)] [SerializeField] float _trackWidth = 0.14f;
        [Range(0.2f, 1.2f)] [SerializeField] float _glowWidth = 0.5f;
        [Range(0.05f, 0.5f)] [SerializeField] float _glowAlpha = 0.2f;
        [Range(0.1f, 0.4f)] [SerializeField] float _ballRadius = 0.22f;
        [Range(0f, 1f)] [SerializeField] float _saturation = 0.78f;
        [Range(0.05f, 0.8f)] [SerializeField] float _trailSeconds = 0.35f;
        [Range(0.1f, 1f)] [SerializeField] float _loserBrightness = 0.45f;

        [Header("Labels")]
        [Tooltip("World font size; about a tenth of the letter height in world units.")]
        [Range(3f, 9f)] [SerializeField] float _labelFontSize = 5.2f;

        [Tooltip("World units kept clear between a track label and the frame edge.")]
        [Min(0f)] [SerializeField] float _labelFrameMargin = 0.3f;
        [Range(3f, 9f)] [SerializeField] float _lengthFontSize = 4.2f;
        [Range(0f, 0.6f)] [SerializeField] float _labelOutline = 0.32f;
        [SerializeField] Color _lengthTint = new Color(1f, 1f, 1f, 1f);
        [SerializeField] Color _longerTint = new Color(1f, 0.82f, 0.25f, 1f);

        [Header("HUD")]
        [SerializeField] Color _resultTint = new Color(1f, 0.82f, 0.25f, 1f);

        [Header("Audio")]
        [Range(0f, 1f)] [SerializeField] float _humVolume = 0.22f;
        [Range(0f, 1f)] [SerializeField] float _winnerChimeVolume = 0.55f;
        [Range(0f, 1f)] [SerializeField] float _tickVolume = 0.25f;
        [Range(0f, 1f)] [SerializeField] float _whooshVolume = 0.5f;
        [Range(0f, 1f)] [SerializeField] float _clinkVolume = 0.6f;
        [Range(0f, 1f)] [SerializeField] float _swellVolume = 0.32f;

        // ---- run-time state
        bool _started;
        System.Random _cosmetic;
        float _hueStart;

        readonly List<TrackView> _views = new List<TrackView>(6);
        Transform _world;
        Material _lineMaterial;
        BrachBurstPool _burst;

        Transform _gateArm;
        Transform _gatePost;
        SpriteRenderer _gateArmSprite;
        SpriteRenderer _gatePostSprite;
        Transform _flagPole;
        Transform _flagCloth;
        SpriteRenderer _flagRing;
        SpriteRenderer _flagGlow;
        float _gateOpen;

        BrachPhase _prevPhase = BrachPhase.Done;
        bool _revealShown;
        int _prevRound = -1;
        bool _layoutDone;
        float _winnerClock = -1f;
        float _endClock;
        string _footerShown;

        AudioSource[] _hum;
        AudioSource _sfx;
        AudioSource _swell;
        AudioClip _whoosh;
        AudioClip _clink;
        AudioClip[] _chord;
        AudioClip[] _ticks;

        bool _camInitialised;

        static readonly float[] Fractions = { 0.5f, 0.38f, 0.62f, 0.26f, 0.74f, 0.15f, 0.86f, 0.05f, 0.95f };
        static readonly float[] Gaps = { 0.14f, 0.5f, 0.95f, 1.5f };
        static readonly float[] TickHz = { 698.46f, 587.33f, 523.25f, 440f, 392f };
        static readonly int[] HumSteps = { 0, 2, 4, 7, 9, 12 };

        void Reset()
        {
            _simulation = GetComponent<BrachistochroneSimulation>();
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(BrachistochroneSimulation simulation, CaptionHud hud, SceneRig rig)
        {
            _simulation = simulation;
            _hud = hud;
            _rig = rig;
        }

        // ---------------------------------------------------------------- setup

        void Initialise()
        {
            int seed = _simulation.Seed;
            _cosmetic = new System.Random(seed ^ 0x2f6b9d1);
            _hueStart = _simulation.PaletteStart / 6f;

            _world = new GameObject("Brach View").transform;
            _world.SetParent(transform, false);

            Shader shader = Shader.Find("Sprites/Default");
            _lineMaterial = new Material(shader) { name = "BrachLine" };

            BuildGate();
            BuildFlag();
            if (_rig.circle != null && _rig.ring != null)
            {
                _burst = new BrachBurstPool(_world, _rig.circle, _rig.ring, 40, 60);
            }

            BuildAudio();
            _started = true;
        }

        TrackView EnsureView(int i)
        {
            while (_views.Count <= i)
            {
                _views.Add(CreateView(_views.Count));
            }

            return _views[i];
        }

        TrackView CreateView(int index)
        {
            var view = new TrackView();
            var root = new GameObject("Track_" + index);
            root.transform.SetParent(_world, false);
            view.root = root.transform;

            view.glow = NewLine("Glow", 4 + index, _glowWidth);
            view.line = NewLine("Tube", 10 + index, _trackWidth);

            // Ball: dark rim for silhouette, coloured body, a fixed highlight, and a bar that spins with distance.
            var ball = new GameObject("Ball");
            ball.transform.SetParent(view.root, false);
            view.ball = ball.transform;
            int order = 30 + index * 4;
            view.rim = NewSprite(view.ball, "Rim", _rig.circle, order, new Color(0f, 0f, 0f, 0.65f), _ballRadius * 2.35f);
            view.body = NewSprite(view.ball, "Body", _rig.circle, order + 1, Color.white, _ballRadius * 2f);
            var spin = new GameObject("Spin");
            spin.transform.SetParent(view.ball, false);
            view.spin = spin.transform;
            view.bar = NewSprite(view.spin, "Bar", _rig.square, order + 2, new Color(0f, 0f, 0f, 0.3f), 1f);
            view.bar.transform.localScale = new Vector3(_ballRadius * 1.6f, _ballRadius * 0.34f, 1f);
            view.shine = NewSprite(view.ball, "Shine", _rig.circle, order + 3, new Color(1f, 1f, 1f, 0.8f), _ballRadius * 0.7f);
            view.shine.transform.localPosition = new Vector3(-_ballRadius * 0.3f, _ballRadius * 0.32f, 0f);

            var trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(view.ball, false);
            view.trail = trailObject.AddComponent<TrailRenderer>();
            view.trail.sharedMaterial = _lineMaterial;
            view.trail.time = _trailSeconds;
            view.trail.minVertexDistance = 0.03f;
            view.trail.widthCurve = new AnimationCurve(new Keyframe(0f, _ballRadius * 1.5f), new Keyframe(1f, 0f));
            view.trail.sortingOrder = 20 + index;
            view.trail.numCapVertices = 3;
            view.trail.emitting = false;

            view.label = NewLabel("Label", _labelFontSize);
            view.length = NewLabel("Length", _lengthFontSize);
            return view;
        }

        LineRenderer NewLine(string name, int order, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_world, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _lineMaterial;
            line.useWorldSpace = true;
            line.numCapVertices = 8;
            line.numCornerVertices = 6;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = width;
            line.sortingOrder = order;
            line.positionCount = 0;
            return line;
        }

        SpriteRenderer NewSprite(Transform parent, string name, Sprite sprite, int order, Color color, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.color = color;
            go.transform.localScale = new Vector3(size, size, 1f);
            return renderer;
        }

        TextMeshPro NewLabel(string name, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_world, false);
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(6f, 1f);
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.sortingOrder = 50;
            text.outlineWidth = _labelOutline;
            text.outlineColor = new Color32(0, 0, 0, 255);
            text.text = string.Empty;
            text.alpha = 0f;
            return text;
        }

        void BuildGate()
        {
            // A chrome post left of A and an arm across the top of the starting balls, hinged at the post.
            Color chrome = new Color(0.82f, 0.86f, 0.93f, 1f);
            var post = new GameObject("Gate Post");
            post.transform.SetParent(_world, false);
            _gatePost = post.transform;
            _gatePost.position = new Vector3(-0.5f, -0.05f, 0f);
            _gatePostSprite = post.AddComponent<SpriteRenderer>();
            _gatePostSprite.sprite = _rig.square;
            _gatePostSprite.color = chrome;
            _gatePostSprite.sortingOrder = 45;
            _gatePost.localScale = new Vector3(0.13f, 1.35f, 1f);

            var hinge = new GameObject("Gate Hinge");
            hinge.transform.SetParent(_world, false);
            hinge.transform.position = new Vector3(-0.5f, 0.38f, 0f);
            _gateArm = hinge.transform;
            var arm = new GameObject("Gate Arm");
            arm.transform.SetParent(_gateArm, false);
            arm.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            arm.transform.localScale = new Vector3(1.0f, 0.1f, 1f);
            _gateArmSprite = arm.AddComponent<SpriteRenderer>();
            _gateArmSprite.sprite = _rig.square;
            _gateArmSprite.color = new Color(0.95f, 0.97f, 1f, 1f);
            _gateArmSprite.sortingOrder = 46;
        }

        void BuildFlag()
        {
            var glow = new GameObject("Finish Glow");
            glow.transform.SetParent(_world, false);
            _flagGlow = glow.AddComponent<SpriteRenderer>();
            _flagGlow.sprite = _rig.glow;
            _flagGlow.color = new Color(1f, 1f, 1f, 0.35f);
            _flagGlow.sortingOrder = 3;

            var ring = new GameObject("Finish Ring");
            ring.transform.SetParent(_world, false);
            _flagRing = ring.AddComponent<SpriteRenderer>();
            _flagRing.sprite = _rig.ring;
            _flagRing.color = new Color(1f, 1f, 1f, 0.6f);
            _flagRing.sortingOrder = 6;

            var pole = new GameObject("Flag Pole");
            pole.transform.SetParent(_world, false);
            _flagPole = pole.transform;
            SpriteRenderer poleSprite = pole.AddComponent<SpriteRenderer>();
            poleSprite.sprite = _rig.square;
            poleSprite.color = new Color(0.9f, 0.93f, 1f, 1f);
            poleSprite.sortingOrder = 44;
            _flagPole.localScale = new Vector3(0.08f, 1.75f, 1f);

            var hinge = new GameObject("Flag Cloth");
            hinge.transform.SetParent(_world, false);
            _flagCloth = hinge.transform;
            var cloth = new GameObject("Checkers");
            cloth.transform.SetParent(_flagCloth, false);
            cloth.transform.localPosition = new Vector3(0.675f, 0f, 0f);
            cloth.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            SpriteRenderer clothSprite = cloth.AddComponent<SpriteRenderer>();
            clothSprite.sprite = _rig.checker != null ? _rig.checker : _rig.square;
            clothSprite.sortingOrder = 45;
        }

        void BuildAudio()
        {
            _whoosh = ProceduralToneBank.CreateWhoosh(1.0f, 2, "BrachWhoosh");
            _clink = ProceduralToneBank.CreateGlassClink(1.8f, 0.6f, 5, "BrachGateClink");
            _chord = new[]
            {
                ProceduralToneBank.CreateTone(523.25f, 1.1f, "BrachChord1"),
                ProceduralToneBank.CreateTone(659.25f, 1.1f, "BrachChord2"),
                ProceduralToneBank.CreateTone(783.99f, 1.1f, "BrachChord3"),
                ProceduralToneBank.CreateTone(1046.5f, 0.9f, "BrachChord4")
            };
            _ticks = new AudioClip[TickHz.Length];
            for (int i = 0; i < TickHz.Length; i++)
            {
                _ticks[i] = ProceduralToneBank.CreateTone(TickHz[i], 0.3f, "BrachTick" + i);
            }

            AudioClip hum = BrachistochroneAudio.CreateHumLoop(110);
            _hum = new AudioSource[6];
            for (int i = 0; i < _hum.Length; i++)
            {
                _hum[i] = AddSource(0f);
                _hum[i].clip = hum;
                _hum[i].loop = true;
                _hum[i].pitch = 1f;
                _hum[i].Play();
            }

            _sfx = AddSource(1f);
            _swell = AddSource(0f);
            _swell.clip = BrachistochroneAudio.CreateSwellLoop(55);
            _swell.loop = true;
            _swell.Play();
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
            if (_simulation == null || _simulation.Config == null || _rig == null || _rig.camera == null || _rig.circle == null)
            {
                return;
            }

            if (!_started)
            {
                Initialise();
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            BrachPhase phase = _simulation.Phase;
            int count = _simulation.TrackCount;

            if (_simulation.RoundIndex != _prevRound)
            {
                _layoutDone = false;
            }

            if (phase == BrachPhase.EndCard && _prevPhase != BrachPhase.EndCard)
            {
                _endClock = 0f;
            }

            if (phase == BrachPhase.EndCard || phase == BrachPhase.Done)
            {
                _endClock += dt;
            }

            if (_simulation.WinnerTrack >= 0)
            {
                _winnerClock = _winnerClock < 0f ? 0f : _winnerClock + dt;
            }
            else
            {
                _winnerClock = -1f;
            }

            for (int i = 0; i < count; i++)
            {
                EnsureView(i);
            }

            UpdateGate(phase, dt);
            UpdateTracks(phase, count, dt);
            UpdateBalls(phase, count, dt);
            UpdateFlag();
            UpdateLabels(phase, count, dt);
            UpdateCamera(count, dt);
            UpdateHud(phase);
            UpdateAudio(phase, count, dt);
            _burst?.Tick(dt);

            _prevPhase = phase;
            _prevRound = _simulation.RoundIndex;
        }

        // ---------------------------------------------------------------- colours

        Color TrackColor(int index, CurveKind kind)
        {
            // Six hues sixty degrees apart, rotated by the run's palette start. The cycloid keeps hue slot 0 so
            // the answer is always the same colour within a video.
            int slot;
            switch (kind)
            {
                case CurveKind.Cycloid: slot = 0; break;
                case CurveKind.SteepDrop: slot = 1; break;
                case CurveKind.Parabola: slot = 2; break;
                case CurveKind.Straight: slot = 3; break;
                case CurveKind.CircleArc: slot = 4; break;
                default: slot = 5; break;
            }

            float hue = Mathf.Repeat(_hueStart + slot / 6f, 1f);
            return Color.HSVToRGB(hue, _saturation, 1f);
        }

        static Vector2 PointFrac(CurvePath path, float u)
        {
            double x, y, a;
            path.PointAt(u * path.Length, out x, out y, out a);
            return new Vector2((float)x, -(float)y);
        }

        // ---------------------------------------------------------------- tracks

        void UpdateTracks(BrachPhase phase, int count, float dt)
        {
            BrachRound round = _simulation.CurrentRound;
            bool ended = phase == BrachPhase.EndCard || phase == BrachPhase.Done;
            int winner = _simulation.WinnerTrack;

            for (int i = 0; i < _views.Count; i++)
            {
                TrackView v = _views[i];
                bool active = i < count;
                v.line.enabled = active;
                v.glow.enabled = active;
                if (!active)
                {
                    continue;
                }

                CurveKind kind = round.tracks[i].kind;
                v.color = TrackColor(i, kind);

                CurvePath path = _simulation.GetDisplayPath(i);
                float reveal = Mathf.Clamp01(_simulation.GetTrackReveal(i));
                int points = reveal >= 0.999f ? PathSamples + 1 : Mathf.Clamp(Mathf.CeilToInt(reveal * PathSamples) + 1, 2, PathSamples + 1);
                for (int j = 0; j < points; j++)
                {
                    float u = Mathf.Min(j / (float)PathSamples, reveal);
                    Vector2 p = PointFrac(path, u);
                    v.points[j] = new Vector3(p.x, p.y, 0f);
                }

                v.line.positionCount = points;
                v.line.SetPositions(v.points);
                v.glow.positionCount = points;
                v.glow.SetPositions(v.points);

                // Dimming: losers once a winner exists, everyone but the cycloid on the end card.
                float dimTarget = 1f;
                float width = 1f;
                if (ended)
                {
                    bool answer = kind == CurveKind.Cycloid;
                    dimTarget = answer ? 1f : 0.12f;
                    if (answer)
                    {
                        width = 1.35f + 0.25f * Mathf.Sin(_endClock * 4f);
                    }
                }
                else if (winner >= 0 && winner != i)
                {
                    dimTarget = phase == BrachPhase.Result ? _loserBrightness : Mathf.Lerp(1f, _loserBrightness, 0.5f);
                }
                else if (winner == i && _winnerClock >= 0f)
                {
                    width = 1f + 0.9f * Mathf.Exp(-_winnerClock * 2.2f) * (0.5f + 0.5f * Mathf.Sin(_winnerClock * 16f));
                }

                v.dim = Mathf.MoveTowards(v.dim, dimTarget, dt * 3f);
                float brightness = Mathf.Lerp(0.55f, 1f, v.dim);
                var lineColor = new Color(v.color.r * brightness, v.color.g * brightness, v.color.b * brightness, Mathf.Lerp(0.3f, 1f, v.dim));
                v.line.startColor = lineColor;
                v.line.endColor = lineColor;
                v.line.widthMultiplier = _trackWidth * width;

                bool glowing = ended && kind == CurveKind.Cycloid;
                float ga = _glowAlpha * v.dim * (glowing ? 2.4f : 1f);
                var glowColor = new Color(v.color.r, v.color.g, v.color.b, Mathf.Clamp01(ga));
                v.glow.startColor = glowColor;
                v.glow.endColor = glowColor;
                v.glow.widthMultiplier = _glowWidth * (glowing ? 1.4f : 1f) * Mathf.Lerp(1f, width, 0.6f);
            }
        }

        // ---------------------------------------------------------------- balls

        void UpdateBalls(BrachPhase phase, int count, float dt)
        {
            bool ended = phase == BrachPhase.EndCard || phase == BrachPhase.Done;
            for (int i = 0; i < _views.Count; i++)
            {
                TrackView v = _views[i];
                bool active = i < count;
                v.ball.gameObject.SetActive(active);
                if (!active)
                {
                    v.hasBall = false;
                    continue;
                }

                float x, y, angle, speed;
                _simulation.GetBall(i, out x, out y, out angle, out speed);
                var position = new Vector2(x, -y);

                if (v.hasBall)
                {
                    float travelled = (position - v.lastBall).magnitude;
                    if (travelled < 2f)
                    {
                        v.spinAngle -= travelled / _ballRadius * Mathf.Rad2Deg;
                    }
                }

                bool jumped = v.hasBall && (position - v.lastBall).magnitude > 2f;
                v.lastBall = position;
                v.hasBall = true;

                v.ball.position = new Vector3(position.x, position.y, 0f);
                v.spin.localRotation = Quaternion.Euler(0f, 0f, v.spinAngle);

                int place = _simulation.GetPlace(i);
                bool rolling = phase == BrachPhase.Run && place == 0 && speed > 0.15f;
                if (jumped || phase == BrachPhase.Hold)
                {
                    v.trail.Clear();
                }

                v.trail.emitting = rolling;

                Color trailColor = new Color(v.color.r, v.color.g, v.color.b, 0.55f);
                v.trail.startColor = trailColor;
                v.trail.endColor = new Color(v.color.r, v.color.g, v.color.b, 0f);

                float brightness = Mathf.Lerp(0.6f, 1f, v.dim);
                float alpha = ended ? Mathf.Lerp(0.15f, 1f, v.dim) : Mathf.Lerp(0.55f, 1f, v.dim);
                v.body.color = new Color(v.color.r * brightness, v.color.g * brightness, v.color.b * brightness, alpha);
                v.rim.color = new Color(0f, 0f, 0f, 0.65f * alpha);
                v.shine.color = new Color(1f, 1f, 1f, 0.8f * alpha);
                v.bar.color = new Color(0f, 0f, 0f, 0.3f * alpha);

                // A chime per finish and a burst for the winner so a ball reads as "arrived".
                if (place > 0 && place != v.prevPlace)
                {
                    PlayFinish(place);
                    if (place == 1 && _burst != null)
                    {
                        _burst.Spawn(new Vector3(position.x, position.y, 0f), v.color, 14, () => (float)_cosmetic.NextDouble());
                    }
                }

                v.prevPlace = place;
            }
        }

        // ---------------------------------------------------------------- gate and flag

        void UpdateGate(BrachPhase phase, float dt)
        {
            bool open = phase == BrachPhase.Run || phase == BrachPhase.Result;
            _gateOpen = Mathf.MoveTowards(_gateOpen, open ? 1f : 0f, dt * (open ? 5f : 3f));
            float angle = Mathf.SmoothStep(0f, 78f, _gateOpen);
            _gateArm.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        void UpdateFlag()
        {
            float fx = _simulation.FinishX;
            float fy = -_simulation.FinishY;
            _flagPole.position = new Vector3(fx, fy + 0.875f, 0f);
            _flagCloth.position = new Vector3(fx + 0.04f, fy + 1.3f, 0f);
            float t = Time.time;
            _flagCloth.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3f) * 4f);
            _flagCloth.localScale = new Vector3(1f + Mathf.Sin(t * 4.5f) * 0.05f, 1f, 1f);

            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
            _flagRing.transform.position = new Vector3(fx, fy, 0f);
            _flagRing.transform.localScale = Vector3.one * (0.75f + 0.1f * pulse);
            _flagRing.color = new Color(1f, 1f, 1f, 0.45f + 0.25f * pulse);
            _flagGlow.transform.position = new Vector3(fx, fy, 0f);
            _flagGlow.transform.localScale = Vector3.one * 1.9f;
        }

        // ---------------------------------------------------------------- labels

        void UpdateLabels(BrachPhase phase, int count, float dt)
        {
            BrachRound round = _simulation.CurrentRound;
            BrachistochroneConfig config = _simulation.Config;
            bool geometryFinal = phase == BrachPhase.Hold || phase == BrachPhase.Run || phase == BrachPhase.Result;

            if (!_layoutDone && geometryFinal)
            {
                LayoutLabels(round, count);
                _layoutDone = true;
            }

            bool ended = phase == BrachPhase.EndCard || phase == BrachPhase.Done;
            bool showLabels = _layoutDone && geometryFinal;
            bool lengthsOn = round.showLengths && phase == BrachPhase.Result;
            bool longer = lengthsOn && _simulation.PhaseProgress >= 0.4f;

            for (int i = 0; i < _views.Count; i++)
            {
                TrackView v = _views[i];
                if (i >= count)
                {
                    v.label.alpha = 0f;
                    v.length.alpha = 0f;
                    continue;
                }

                float labelTarget = ended ? (round.tracks[i].kind == CurveKind.Cycloid ? 1f : 0f) : (showLabels ? 1f : 0f);
                v.labelAlpha = Mathf.MoveTowards(v.labelAlpha, labelTarget, dt * (labelTarget > v.labelAlpha ? 3.5f : 6f));

                string text = ended && round.tracks[i].kind == CurveKind.Cycloid && !string.IsNullOrEmpty(config.endLabel)
                    ? config.endLabel
                    : round.tracks[i].label ?? string.Empty;
                if (v.label.text != text)
                {
                    v.label.text = text;
                }

                v.label.color = v.color;
                v.label.alpha = v.labelAlpha * Mathf.Lerp(0.5f, 1f, v.dim);
                v.label.transform.position = new Vector3(ClampInsideFrame(v.labelPos.x, v.label.preferredWidth), v.labelPos.y, 0f);

                v.lengthAlpha = Mathf.MoveTowards(v.lengthAlpha, lengthsOn ? 1f : 0f, dt * 2.5f);
                if (lengthsOn)
                {
                    string len = _simulation.GetLength(i).ToString("0.0", CultureInfo.InvariantCulture) + " m";
                    if (v.length.text != len)
                    {
                        v.length.text = len;
                    }
                }

                bool isAnswer = round.tracks[i].kind == CurveKind.Cycloid;
                float emphasis = longer && isAnswer ? 1f + 0.18f * (0.5f + 0.5f * Mathf.Sin(Time.time * 7f)) : 1f;
                v.length.color = longer && isAnswer ? _longerTint : _lengthTint;
                v.length.alpha = v.lengthAlpha;
                v.length.transform.position = new Vector3(v.lengthPos.x, v.lengthPos.y, 0f);
                v.length.transform.localScale = new Vector3(emphasis, emphasis, 1f);
            }
        }

        /// <summary>
        /// Deterministic greedy declutter. Every label tries the same candidate list (fractions along its track, both
        /// sides of the track, growing gaps) and takes the cheapest: overlapping an already placed label is
        /// ruinous, sitting on a track line, the start gate or the finish flag costs a little. Order is track order.
        /// </summary>
        void LayoutLabels(BrachRound round, int count)
        {
            var tracks = new Vector2[count][];
            for (int i = 0; i < count; i++)
            {
                CurvePath path = _simulation.GetPath(i);
                tracks[i] = new Vector2[LayoutSamples + 1];
                for (int j = 0; j <= LayoutSamples; j++)
                {
                    tracks[i][j] = PointFrac(path, j / (float)LayoutSamples);
                }
            }

            float fx = _simulation.FinishX;
            float fy = -_simulation.FinishY;
            var keepOut = new List<Rect>
            {
                new Rect(-1.0f, -1.0f, 2.0f, 2.1f),               // start gate and balls
                new Rect(fx - 0.5f, fy - 0.5f, 1.9f, 2.2f)        // finish flag
            };

            var placed = new List<Rect>(count);
            for (int i = 0; i < count; i++)
            {
                TrackView v = _views[i];
                string label = round.tracks[i].label ?? string.Empty;
                v.label.text = label;
                Vector2 labelSize = Measure(v.label, label, 0.34f);
                Vector2 lengthSize = Vector2.zero;
                if (round.showLengths)
                {
                    lengthSize = Measure(v.length, "00.0 m", 0.3f);
                }

                float width = Mathf.Max(labelSize.x, lengthSize.x);
                float height = labelSize.y + lengthSize.y;
                v.labelSize = labelSize;
                v.lengthSize = lengthSize;

                float bestCost = float.MaxValue;
                Rect bestRect = new Rect(0f, 0f, width, height);
                int rank = 0;
                for (int g = 0; g < Gaps.Length; g++)
                {
                    for (int f = 0; f < Fractions.Length; f++)
                    {
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector2 p = tracks[i][Mathf.Clamp(Mathf.RoundToInt(Fractions[f] * LayoutSamples), 1, LayoutSamples - 1)];
                            int k = Mathf.Clamp(Mathf.RoundToInt(Fractions[f] * LayoutSamples), 1, LayoutSamples - 1);
                            Vector2 tangent = (tracks[i][k + 1] - tracks[i][k - 1]).normalized;
                            Vector2 normal = new Vector2(-tangent.y, tangent.x) * side;
                            float reach = Mathf.Abs(normal.x) * width * 0.5f + Mathf.Abs(normal.y) * height * 0.5f;
                            Vector2 centre = p + normal * (Gaps[g] + reach);
                            var rect = new Rect(centre.x - width * 0.5f, centre.y - height * 0.5f, width, height);

                            float cost = rank * 0.05f + g * 0.4f;
                            for (int q = 0; q < placed.Count; q++)
                            {
                                if (Overlaps(rect, placed[q], 0.12f))
                                {
                                    cost += 100f;
                                }
                            }

                            for (int q = 0; q < keepOut.Count; q++)
                            {
                                if (Overlaps(rect, keepOut[q], 0f))
                                {
                                    cost += 6f;
                                }
                            }

                            for (int t = 0; t < count; t++)
                            {
                                for (int j = 0; j <= LayoutSamples; j++)
                                {
                                    if (Contains(rect, tracks[t][j], 0.12f))
                                    {
                                        cost += t == i ? 1.5f : 0.9f;
                                    }
                                }
                            }

                            rank++;
                            if (cost < bestCost - 0.0001f)
                            {
                                bestCost = cost;
                                bestRect = rect;
                            }
                        }
                    }

                    if (bestCost < 3f)
                    {
                        break;
                    }
                }

                placed.Add(bestRect);
                Vector2 c = bestRect.center;
                v.labelPos = new Vector2(c.x, c.y + lengthSize.y * 0.5f);
                v.lengthPos = new Vector2(c.x, c.y - labelSize.y * 0.5f);
            }
        }

        /// <summary>
        /// Keeps a label's whole width inside the camera frame. The declutter places labels for the round's short
        /// names; the end card's longer name (BRACHISTOCHRONE) near B would otherwise run off the right edge.
        /// </summary>
        float ClampInsideFrame(float x, float width)
        {
            Camera cam = _rig.camera;
            if (cam == null || !cam.orthographic)
            {
                return x;
            }

            float halfFrame = cam.orthographicSize * cam.aspect;
            float half = width * 0.5f + _labelFrameMargin;
            float left = cam.transform.position.x - halfFrame + half;
            float right = cam.transform.position.x + halfFrame - half;
            return left > right ? cam.transform.position.x : Mathf.Clamp(x, left, right);
        }

        Vector2 Measure(TextMeshPro text, string content, float minHeight)
        {
            Vector2 size = text.GetPreferredValues(content);
            if (size.x <= 0.01f)
            {
                size.x = content.Length * text.fontSize * 0.065f;
            }

            size.y = Mathf.Max(size.y, minHeight);
            return new Vector2(size.x + 0.1f, size.y + 0.04f);
        }

        static bool Overlaps(Rect a, Rect b, float pad)
        {
            return a.xMin - pad < b.xMax && a.xMax + pad > b.xMin && a.yMin - pad < b.yMax && a.yMax + pad > b.yMin;
        }

        static bool Contains(Rect r, Vector2 p, float pad)
        {
            return p.x > r.xMin - pad && p.x < r.xMax + pad && p.y > r.yMin - pad && p.y < r.yMax + pad;
        }

        // ---------------------------------------------------------------- camera

        void UpdateCamera(int count, float dt)
        {
            Camera cam = _rig.camera;
            float minX = -1.0f, maxX = 1.0f, minY = -1.0f, maxY = 1.3f;

            for (int i = 0; i < count; i++)
            {
                CurvePath path = _simulation.GetDisplayPath(i);
                for (int j = 0; j <= 24; j++)
                {
                    Vector2 p = PointFrac(path, j / 24f);
                    minX = Mathf.Min(minX, p.x);
                    maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y);
                    maxY = Mathf.Max(maxY, p.y);
                }
            }

            float fx = _simulation.FinishX;
            float fy = -_simulation.FinishY;
            maxX = Mathf.Max(maxX, fx + 1.5f);
            maxY = Mathf.Max(maxY, fy + 1.9f);
            minY = Mathf.Min(minY, fy - 0.6f);

            minX -= _fitMargin;
            maxX += _fitMargin;
            minY -= _fitMargin;
            maxY += _fitMargin;

            float width = maxX - minX;
            float height = maxY - minY;
            float bandFraction = Mathf.Max(0.2f, _bandBottom - _bandTop);
            float size = Mathf.Max(width / (2f * cam.aspect), height / (2f * bandFraction));

            // Centre of the content lands at the middle of the band: below the screen centre when the band is.
            float bandCentre = (_bandTop + _bandBottom) * 0.5f;
            float centreY = (minY + maxY) * 0.5f + (bandCentre - 0.5f) * 2f * size;
            float centreX = (minX + maxX) * 0.5f;

            if (!_camInitialised)
            {
                _camInitialised = true;
                cam.orthographicSize = size;
                cam.transform.position = new Vector3(centreX, centreY, -10f);
                return;
            }

            float blend = 1f - Mathf.Exp(-dt / Mathf.Max(0.02f, _cameraSeconds * 0.45f));
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, blend);
            // The size changes while the position eases, so recompute the band offset from the live size.
            float liveCentreY = (minY + maxY) * 0.5f + (bandCentre - 0.5f) * 2f * cam.orthographicSize;
            Vector3 current = cam.transform.position;
            cam.transform.position = new Vector3(Mathf.Lerp(current.x, centreX, blend), Mathf.Lerp(current.y, liveCentreY, blend), -10f);
        }

        // ---------------------------------------------------------------- HUD

        void UpdateHud(BrachPhase phase)
        {
            if (_hud == null)
            {
                return;
            }

            BrachistochroneConfig config = _simulation.Config;
            // The end card reveals the answer where the question sat: the shape's name on top, its proper name under it.
            bool reveal = phase == BrachPhase.EndCard || phase == BrachPhase.Done;
            if (_footerShown == null || reveal != _revealShown)
            {
                _revealShown = reveal;
                _hud.SetCaption(reveal ? Sized(config.endCard, config.endCardSizePercent) : config.question, reveal);
                _hud.SetSubline(reveal && !string.IsNullOrEmpty(config.endTitle) ? config.endTitle : config.title);
            }

            string footer = BuildFooter(config, phase);
            if (footer != _footerShown)
            {
                _footerShown = footer;
                _hud.SetFooter(footer);
            }
        }

        string BuildFooter(BrachistochroneConfig config, BrachPhase phase)
        {
            BrachRound round = _simulation.CurrentRound;
            switch (phase)
            {
                case BrachPhase.Hold:
                    // Round 1 has no transition, so its hook line shows first, then the guess prompt.
                    if (_simulation.RoundIndex == 0 && !string.IsNullOrEmpty(round.caption) && _simulation.PhaseProgress < 0.5f)
                    {
                        return round.caption;
                    }

                    return _simulation.BottomLine;
                case BrachPhase.Transition:
                    return _simulation.BottomLine;
                case BrachPhase.Run:
                    if (_simulation.WinnerTrack >= 0 && !string.IsNullOrEmpty(round.result))
                    {
                        return Tint(round.result, _resultTint);
                    }

                    return _simulation.BottomLine;
                case BrachPhase.Result:
                    return Tint(_simulation.BottomLine, _resultTint);
                case BrachPhase.EndCard:
                    // The why in two beats: the cause, then what it buys.
                    if (_simulation.PhaseProgress >= 0.5f && !string.IsNullOrEmpty(config.endLine2))
                    {
                        return Tint(config.endLine2, _resultTint);
                    }

                    return Tint(string.IsNullOrEmpty(config.endLine) ? config.endCard : config.endLine, _resultTint);
                default:
                    return Tint(!string.IsNullOrEmpty(config.endLine2) ? config.endLine2 : string.IsNullOrEmpty(config.endLine) ? config.endCard : config.endLine, _resultTint);
            }
        }

        static string Sized(string text, float percent)
        {
            return percent > 0f && percent < 100f
                ? "<size=" + percent.ToString("0", CultureInfo.InvariantCulture) + "%>" + text + "</size>"
                : text;
        }

        static string Tint(string text, Color color)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
        }

        // ---------------------------------------------------------------- audio

        void UpdateAudio(BrachPhase phase, int count, float dt)
        {
            if (_sfx == null)
            {
                return;
            }

            float g = _simulation.Config.gravity;
            float maxSpeed = Mathf.Sqrt(2f * g * 4.2f) + 0.001f;
            float perVoice = _humVolume / Mathf.Sqrt(Mathf.Max(1f, count * 0.5f));
            for (int i = 0; i < _hum.Length; i++)
            {
                float target = 0f;
                float pitch = _hum[i].pitch;
                if (i < count && i < _views.Count && phase == BrachPhase.Run && _simulation.GetPlace(i) == 0)
                {
                    float x, y, angle, speed;
                    _simulation.GetBall(i, out x, out y, out angle, out speed);
                    float unit = Mathf.Clamp01(speed / maxSpeed);
                    target = perVoice * Mathf.Pow(unit, 0.7f);
                    pitch = Mathf.Pow(2f, HumSteps[i] / 12f) * Mathf.Lerp(0.75f, 1.9f, unit);
                }

                _hum[i].volume = Mathf.MoveTowards(_hum[i].volume, target, dt * 1.2f);
                _hum[i].pitch = Mathf.Lerp(_hum[i].pitch, pitch, 1f - Mathf.Exp(-dt * 14f));
            }

            if (phase == BrachPhase.Transition && _prevPhase != BrachPhase.Transition)
            {
                _sfx.PlayOneShot(_whoosh, _whooshVolume);
            }

            if (phase == BrachPhase.Run && _prevPhase != BrachPhase.Run)
            {
                _sfx.PlayOneShot(_clink, _clinkVolume);
            }

            float swellTarget = _simulation.ClockRate < 0.97f && phase == BrachPhase.Run ? _swellVolume : 0f;
            _swell.volume = Mathf.MoveTowards(_swell.volume, swellTarget, dt * 0.9f);
        }

        /// <summary>A bright chord for the winner, a soft descending tick for the rest.</summary>
        void PlayFinish(int place)
        {
            if (_sfx == null)
            {
                return;
            }

            if (place == 1)
            {
                _sfx.PlayOneShot(_chord[0], _winnerChimeVolume);
                _sfx.PlayOneShot(_chord[1], _winnerChimeVolume * 0.8f);
                _sfx.PlayOneShot(_chord[2], _winnerChimeVolume * 0.8f);
                _sfx.PlayOneShot(_chord[3], _winnerChimeVolume * 0.5f);
            }
            else
            {
                int k = Mathf.Clamp(place - 2, 0, _ticks.Length - 1);
                _sfx.PlayOneShot(_ticks[k], _tickVolume);
            }
        }
    }
}
