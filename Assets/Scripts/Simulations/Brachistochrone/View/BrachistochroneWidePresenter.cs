using System.Collections.Generic;
using SimulationLobby.Presentation;
using SimulationLobby.Simulations.Brachistochrone.Kernel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.Brachistochrone
{
    /// <summary>
    /// The 16:9 "Racetrack" view for comp-001 #7: light blueprint-paper backdrop, saturated ink tracks, a static wide
    /// camera (slow side-follow only for photo-finish slow motion), speed-pitched ball ticks, a finish-time ladder
    /// and the winner reveal. Reads <see cref="BrachistochroneSimulation"/> in <c>LateUpdate</c> and never writes to
    /// it. Text goes through the <see cref="SegmentCardPresenter"/> HUD; track labels are world-space TMP decals.
    /// </summary>
    [RequireComponent(typeof(BrachistochroneSimulation))]
    public sealed class BrachistochroneWidePresenter : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Rig
        {
            public Camera camera;
            public Sprite circle;
            public Sprite ring;
            public Sprite square;
            public Sprite checker;
        }

        sealed class TrackView
        {
            public GameObject root;
            public LineRenderer line;
            public LineRenderer glow;
            public Transform ball;
            public SpriteRenderer rim;
            public SpriteRenderer body;
            public SpriteRenderer shine;
            public TrailRenderer trail;
            public TextMeshPro label;
            public AudioSource tick;
            public SpriteRenderer burst;
            public float burstAge = -1f;
            public float tickTimer;
            public int prevPlace;
            public float fade = 1f;
            public bool labelPlaced;
        }

        const int LineSamples = 160;
        const int SlotCount = 8;
        const float Pad = 0.12f;

        [Header("Wiring (set by the scene builder via Bind)")]
        [SerializeField] BrachistochroneSimulation _simulation;
        [SerializeField] SegmentCardPresenter _card;
        [SerializeField] Rig _rig = new Rig();

        [Header("Arena (world units, y down). Everything fits inside this box.")]
        [SerializeField] Vector2 _arenaMin = new Vector2(-0.9f, -0.8f);
        [SerializeField] Vector2 _arenaMax = new Vector2(11.4f, 5.7f);

        [Header("Look")]
        [SerializeField] Color _paper = new Color(0.955f, 0.94f, 0.88f, 1f);
        [SerializeField] Color _gridInk = new Color(0.22f, 0.40f, 0.72f, 1f);
        [SerializeField] Color _ink = new Color(0.07f, 0.09f, 0.17f, 1f);
        [Range(0.08f, 0.3f)] [SerializeField] float _trackWidth = 0.15f;
        [Range(0.2f, 0.6f)] [SerializeField] float _ballDiameter = 0.42f;
        [Range(2.5f, 7f)] [SerializeField] float _labelFontSize = 4.2f;
        [Range(0.02f, 0.4f)] [SerializeField] float _fadedAlpha = 0.1f;

        [Header("Photo-finish side-follow")]
        [Range(0f, 0.5f)] [SerializeField] float _followZoom = 0.28f;
        [Range(0.4f, 0.8f)] [SerializeField] float _followScreenX = 0.58f;
        [Range(0.5f, 6f)] [SerializeField] float _followSpeed = 2.5f;

        [Header("Audio")]
        [Range(0f, 1f)] [SerializeField] float _tickVolume = 0.1f;
        [Range(0f, 1f)] [SerializeField] float _chimeVolume = 0.4f;
        [Range(0f, 1f)] [SerializeField] float _whooshVolume = 0.3f;

        // Fixed ink colour per slot, saturated enough to read on cream paper at 480px.
        static readonly Color[] SlotColors =
        {
            new Color(0.18f, 0.22f, 0.32f), // 0 straight: slate
            new Color(0.86f, 0.10f, 0.17f), // 1 curve: red (the hero)
            new Color(0.95f, 0.50f, 0.04f), // 2 steep: orange
            new Color(0.08f, 0.60f, 0.28f), // 3 circle: green
            new Color(0.52f, 0.20f, 0.80f), // 4 parabola: violet
            new Color(0.00f, 0.58f, 0.66f), // 5 deep: teal
            new Color(0.13f, 0.38f, 0.93f), // 6 sine: blue
            new Color(0.88f, 0.14f, 0.62f)  // 7 wave: magenta
        };

        static readonly float[] LabelFractions = { 0.62f, 0.34f, 0.80f, 0.48f, 0.20f, 0.90f, 0.10f, 0.70f };

        readonly List<TrackView> _views = new List<TrackView>(SlotCount);
        readonly Vector3[] _points = new Vector3[LineSamples];
        bool _started;
        Material _lineMaterial;
        Transform _world;
        Camera _camera;
        Vector3 _baseCamPos;
        float _baseOrtho;
        float _follow;

        TextMeshPro _startLabel;
        TextMeshPro _finishLabel;
        Transform _pole;
        Transform _cloth;
        Transform _startMarker;
        Transform _finishMarker;

        // Ladder (uGUI, under the card canvas).
        CanvasGroup _ladderGroup;
        readonly List<TMP_Text> _ladderNames = new List<TMP_Text>(SlotCount);
        readonly List<TMP_Text> _ladderTimes = new List<TMP_Text>(SlotCount);
        readonly List<Image> _ladderSwatches = new List<Image>(SlotCount);
        readonly List<GameObject> _ladderRows = new List<GameObject>(SlotCount);

        BrachPhase _prevPhase = BrachPhase.Done;
        int _prevRound = -1;
        float _endClock;

        AudioSource _sfx;
        AudioClip _tickClip;
        AudioClip _whoosh;
        AudioClip[] _chimes;

        public void Bind(BrachistochroneSimulation simulation, SegmentCardPresenter card, Rig rig)
        {
            _simulation = simulation;
            _card = card;
            _rig = rig;
        }

        void Reset()
        {
            _simulation = GetComponent<BrachistochroneSimulation>();
        }

        // ---------------------------------------------------------------- setup

        void Initialise()
        {
            _camera = _rig.camera != null ? _rig.camera : Camera.main;
            _world = new GameObject("Racetrack View").transform;
            _world.SetParent(transform, false);
            _lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "RacetrackLine" };

            FrameCamera();
            BuildGrid();
            BuildMarkers();
            for (int i = 0; i < SlotCount; i++)
            {
                _views.Add(CreateView(i));
            }

            BuildLadder();
            BuildAudio();
            if (_card != null)
            {
                _card.SetHook(_simulation.Config.question);
            }

            _started = true;
        }

        void FrameCamera()
        {
            if (_camera == null)
            {
                return;
            }

            Vector2 size = _arenaMax - _arenaMin;
            _baseOrtho = WideFrameLayout.OrthoSizeForArena(size.x, size.y, AspectMode.Wide, 1f);
            float centerX = (_arenaMin.x + _arenaMax.x) * 0.5f + WideFrameLayout.ArenaCameraOffsetX(_baseOrtho, AspectMode.Wide);
            float centerY = -(_arenaMin.y + _arenaMax.y) * 0.5f;
            _baseCamPos = new Vector3(centerX, centerY, -10f);
            _camera.orthographic = true;
            _camera.orthographicSize = _baseOrtho;
            _camera.transform.position = _baseCamPos;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _paper;
        }

        LineRenderer MakeLine(Transform parent, string name, int order, float width, int positions)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = positions;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        void BuildGrid()
        {
            var grid = new GameObject("Blueprint Grid").transform;
            grid.SetParent(_world, false);
            const float minX = -8f, maxX = 20f, minY = -12f, maxY = 5f;
            for (int x = (int)minX; x <= (int)maxX; x++)
            {
                bool major = x % 5 == 0;
                LineRenderer l = MakeLine(grid, "gx" + x, -100, major ? 0.02f : 0.011f, 2);
                Color c = _gridInk;
                c.a = major ? 0.30f : 0.15f;
                l.startColor = c;
                l.endColor = c;
                l.SetPosition(0, new Vector3(x, minY, 0f));
                l.SetPosition(1, new Vector3(x, maxY, 0f));
            }

            for (int y = (int)minY; y <= (int)maxY; y++)
            {
                bool major = y % 5 == 0;
                LineRenderer l = MakeLine(grid, "gy" + y, -100, major ? 0.02f : 0.011f, 2);
                Color c = _gridInk;
                c.a = major ? 0.30f : 0.15f;
                l.startColor = c;
                l.endColor = c;
                l.SetPosition(0, new Vector3(minX, y, 0f));
                l.SetPosition(1, new Vector3(maxX, y, 0f));
            }
        }

        void BuildMarkers()
        {
            // Start: an ink ring at A with a small START tag. Finish: pole + checkered cloth at B with a FINISH tag.
            _startMarker = MakeSprite("Start Ring", _rig.ring, _ink, 0.95f, 20).transform;
            _startLabel = MakeText("Start Label", "START", 3.4f, _ink);
            _startLabel.transform.position = new Vector3(-0.1f, 0.78f, 0f);

            _finishMarker = MakeSprite("Finish Ring", _rig.ring, _ink, 0.95f, 20).transform;
            _pole = MakeSprite("Pole", _rig.square, _ink, 1f, 21).transform;
            _pole.localScale = new Vector3(0.06f, 1.7f, 1f);
            _cloth = MakeSprite("Cloth", _rig.checker, Color.white, 1f, 22).transform;
            _finishLabel = MakeText("Finish Label", "FINISH", 3.4f, _ink);
        }

        SpriteRenderer MakeSprite(string name, Sprite sprite, Color color, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_world, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            return sr;
        }

        TextMeshPro MakeText(string name, string text, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_world, false);
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
            t.GetComponent<MeshRenderer>().sortingOrder = 40;
            return t;
        }

        TrackView CreateView(int i)
        {
            var v = new TrackView();
            Color c = SlotColors[i];
            v.root = new GameObject("Track " + i);
            v.root.transform.SetParent(_world, false);
            v.glow = MakeLine(v.root.transform, "Glow", 2, 0.9f, 2);
            v.line = MakeLine(v.root.transform, "Line", 10 + i, _trackWidth, 2);
            v.line.startColor = c;
            v.line.endColor = c;

            var ball = new GameObject("Ball");
            ball.transform.SetParent(v.root.transform, false);
            v.ball = ball.transform;
            v.rim = ChildSprite(ball.transform, "Rim", _rig.circle, _ink, _ballDiameter * 1.28f, 30 + i);
            v.body = ChildSprite(ball.transform, "Body", _rig.circle, c, _ballDiameter, 31 + i);
            v.shine = ChildSprite(ball.transform, "Shine", _rig.circle, new Color(1f, 1f, 1f, 0.7f), _ballDiameter * 0.3f, 32 + i);
            v.shine.transform.localPosition = new Vector3(-_ballDiameter * 0.18f, _ballDiameter * 0.18f, 0f);

            v.trail = ball.AddComponent<TrailRenderer>();
            v.trail.sharedMaterial = _lineMaterial;
            v.trail.time = 0.35f;
            v.trail.startWidth = _ballDiameter * 0.7f;
            v.trail.endWidth = 0f;
            v.trail.sortingOrder = 28;
            v.trail.emitting = false;
            v.trail.startColor = new Color(c.r, c.g, c.b, 0.55f);
            v.trail.endColor = new Color(c.r, c.g, c.b, 0f);

            v.label = MakeText("Label", "", _labelFontSize, Color.Lerp(c, _ink, 0.25f));
            v.label.transform.SetParent(v.root.transform, false);

            v.burst = ChildSprite(v.root.transform, "Burst", _rig.ring, c, 1f, 35);
            v.burst.enabled = false;

            var audio = new GameObject("Tick").AddComponent<AudioSource>();
            audio.transform.SetParent(v.root.transform, false);
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            v.tick = audio;
            return v;
        }

        SpriteRenderer ChildSprite(Transform parent, string name, Sprite sprite, Color color, float diameter, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            go.transform.localScale = new Vector3(diameter, diameter, 1f);
            return sr;
        }

        void BuildAudio()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;
            _tickClip = ProceduralToneBank.CreateTone(520f, 0.11f, "RacetrackTick");
            _whoosh = ProceduralToneBank.CreateWhoosh(0.6f, 2, "RacetrackWhoosh");
            _chimes = ProceduralToneBank.CreateScale(8, 392f, 0.9f);
        }

        // ---------------------------------------------------------------- ladder

        static readonly Color PlateColor = new Color(0.97f, 0.95f, 0.88f, 0.92f);

        void BuildLadder()
        {
            if (_card == null)
            {
                return;
            }

            const float rowHeight = 35f;
            const float header = 32f;
            const float pad = 10f;
            float height = header + rowHeight * SlotCount + pad * 2f;

            var panel = new GameObject("FinishLadder", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(_card.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(-205f, 112f);
            rect.sizeDelta = new Vector2(390f, height);
            var plate = panel.GetComponent<Image>();
            plate.color = PlateColor;
            plate.raycastTarget = false;
            _ladderGroup = panel.GetComponent<CanvasGroup>();
            _ladderGroup.alpha = 0f;
            _ladderGroup.blocksRaycasts = false;

            // Keep the full-screen segment card on top of the ladder.
            Transform card = _card.transform.Find("Card");
            if (card != null)
            {
                panel.transform.SetSiblingIndex(card.GetSiblingIndex());
            }

            TMP_Text head = Text(rect, "Header", "FINISH TIMES", 24f, new Color(0.45f, 0.1f, 0.14f), TextAlignmentOptions.Center);
            head.fontStyle = FontStyles.Bold;
            head.characterSpacing = 4f;
            Place(head.rectTransform, 0f, -pad, 390f, header);

            for (int i = 0; i < SlotCount; i++)
            {
                float y = -pad - header - i * rowHeight;
                var row = new GameObject("Row" + (i + 1), typeof(RectTransform));
                row.transform.SetParent(rect, false);
                Place((RectTransform)row.transform, 0f, y, 390f, rowHeight);
                _ladderRows.Add(row);

                var sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
                sw.transform.SetParent(row.transform, false);
                var swRect = (RectTransform)sw.transform;
                swRect.anchorMin = new Vector2(0f, 0.5f);
                swRect.anchorMax = new Vector2(0f, 0.5f);
                swRect.pivot = new Vector2(0f, 0.5f);
                swRect.anchoredPosition = new Vector2(16f, 0f);
                swRect.sizeDelta = new Vector2(20f, 20f);
                var swImage = sw.GetComponent<Image>();
                swImage.raycastTarget = false;
                _ladderSwatches.Add(swImage);

                TMP_Text name = Text((RectTransform)row.transform, "Name", "", 28f, _ink, TextAlignmentOptions.MidlineLeft);
                name.fontStyle = FontStyles.Bold;
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.enableAutoSizing = true;
                name.fontSizeMax = 28f;
                name.fontSizeMin = 18f;
                Stretch(name.rectTransform, 48f, 120f, 0f, 0f);
                _ladderNames.Add(name);

                TMP_Text time = Text((RectTransform)row.transform, "Time", "", 28f, _ink, TextAlignmentOptions.MidlineRight);
                time.fontStyle = FontStyles.Bold;
                time.textWrappingMode = TextWrappingModes.NoWrap;
                Stretch(time.rectTransform, 270f, 14f, 0f, 0f);
                _ladderTimes.Add(time);
                row.SetActive(false);
            }
        }

        static TMP_Text Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        static void Place(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        static void Stretch(RectTransform r, float left, float right, float bottom, float top)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(-right, -top);
        }

        // ---------------------------------------------------------------- frame

        void LateUpdate()
        {
            if (_simulation == null || _simulation.Config == null)
            {
                return;
            }

            if (!_started)
            {
                Initialise();
            }

            float dt = Time.deltaTime;
            BrachPhase phase = _simulation.Phase;
            int round = _simulation.RoundIndex;
            bool phaseChanged = phase != _prevPhase || round != _prevRound;
            if (phaseChanged)
            {
                OnPhaseChanged(phase, round);
            }

            bool endCard = phase == BrachPhase.EndCard || phase == BrachPhase.Done;
            if (endCard)
            {
                _endClock += dt;
            }

            DrawTracks(phase, endCard, dt);
            DrawMarkers();
            UpdateTicks(phase, dt);
            UpdateBursts(dt);
            UpdateCamera(phase, dt);
            UpdateLadder(phase, endCard);
            UpdateHud(phase, endCard);

            _prevPhase = phase;
            _prevRound = round;
        }

        void OnPhaseChanged(BrachPhase phase, int round)
        {
            if (phase == BrachPhase.Transition || (phase == BrachPhase.Hold && round != _prevRound))
            {
                foreach (TrackView v in _views)
                {
                    v.prevPlace = 0;
                    v.labelPlaced = false;
                    v.tickTimer = 0f;
                }
            }

            if (phase == BrachPhase.Hold)
            {
                LayoutLabels();
            }

            if (phase == BrachPhase.Run)
            {
                foreach (TrackView v in _views)
                {
                    v.trail.Clear();
                    v.trail.emitting = true;
                }

                if (_sfx != null)
                {
                    _sfx.PlayOneShot(_whoosh, _whooshVolume);
                }
            }
            else
            {
                foreach (TrackView v in _views)
                {
                    v.trail.emitting = false;
                    v.trail.Clear();
                }
            }

            if (phase == BrachPhase.EndCard)
            {
                _endClock = 0f;
                if (_sfx != null)
                {
                    _sfx.PlayOneShot(_chimes[7], _chimeVolume);
                }
            }
        }

        // ---------------------------------------------------------------- tracks and balls

        int WinnerSlot()
        {
            BrachTrack[] tracks = _simulation.Config.rounds[_simulation.RoundTotal - 1].tracks;
            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i].kind == CurveKind.Cycloid || tracks[i].kind == CurveKind.BumpedCycloid)
                {
                    return i;
                }
            }

            return 0;
        }

        void DrawTracks(BrachPhase phase, bool endCard, float dt)
        {
            int count = _simulation.TrackCount;
            int winner = endCard ? WinnerSlot() : -1;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_endClock * 4f);

            for (int i = 0; i < _views.Count; i++)
            {
                TrackView v = _views[i];
                bool active = i < count;
                v.root.SetActive(active);
                if (!active)
                {
                    continue;
                }

                // Fade target: on the end card only the winner stays inked in.
                float targetFade = endCard && i != winner ? _fadedAlpha : 1f;
                v.fade = Mathf.MoveTowards(v.fade, targetFade, dt * 1.6f);

                CurvePath path = _simulation.GetDisplayPath(i);
                float reveal = _simulation.GetTrackReveal(i);
                int shown = Mathf.Clamp(Mathf.CeilToInt(reveal * (LineSamples - 1)) + 1, 2, LineSamples);
                int last = path.Count - 1;
                for (int j = 0; j < shown; j++)
                {
                    int idx = Mathf.RoundToInt((float)j / (LineSamples - 1) * last);
                    _points[j] = new Vector3((float)path.X(idx), -(float)path.Y(idx), 0f);
                }

                v.line.positionCount = shown;
                v.line.SetPositions(_points);
                float width = _trackWidth * (i == winner ? 1.8f : 1f);
                v.line.startWidth = width;
                v.line.endWidth = width;
                Color c = SlotColors[i];
                c.a = v.fade;
                v.line.startColor = c;
                v.line.endColor = c;

                bool glowing = i == winner;
                v.glow.enabled = glowing;
                if (glowing)
                {
                    v.glow.positionCount = shown;
                    v.glow.SetPositions(_points);
                    Color g = SlotColors[i];
                    g.a = 0.22f + 0.2f * pulse;
                    v.glow.startColor = g;
                    v.glow.endColor = g;
                }

                // Ball.
                _simulation.GetBall(i, out float bx, out float by, out float angle, out float speed);
                v.ball.position = new Vector3(bx, -by, 0f);
                float pop = (phase == BrachPhase.Result || endCard) && _simulation.WinnerTrack == i ? 1.25f : 1f;
                if (endCard && i == winner)
                {
                    pop = 1.3f;
                }

                v.ball.localScale = new Vector3(pop, pop, 1f);
                SetAlpha(v.rim, v.fade);
                SetAlpha(v.body, v.fade);
                SetAlpha(v.shine, 0.7f * v.fade);

                // Labels: on the track in Hold, Run and Result; the winner's becomes the reveal on the end card.
                bool showLabel = v.labelPlaced && (phase == BrachPhase.Hold || phase == BrachPhase.Run || phase == BrachPhase.Result);
                if (endCard)
                {
                    showLabel = i == winner;
                }

                v.label.gameObject.SetActive(showLabel);
                if (showLabel && endCard)
                {
                    v.label.text = _simulation.Config.endLabel;
                    v.label.fontSize = Mathf.Lerp(_labelFontSize, 9f, Mathf.Clamp01(_endClock / 0.6f));
                    v.label.color = Color.Lerp(SlotColors[i], _ink, 0.1f);
                    float cx = _simulation.FinishX * 0.5f;
                    float cy = -_simulation.FinishY * 0.42f;
                    v.label.transform.position = new Vector3(cx, cy, 0f);
                }
            }
        }

        static void SetAlpha(SpriteRenderer sr, float alpha)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }

        // ---------------------------------------------------------------- labels

        void LayoutLabels()
        {
            int count = _simulation.TrackCount;
            BrachRound round = _simulation.CurrentRound;
            var placed = new List<Rect>();
            int[] order = { 1, 0, 2, 3, 4, 5, 6, 7 };

            for (int k = 0; k < order.Length; k++)
            {
                int i = order[k];
                if (i >= count)
                {
                    continue;
                }

                TrackView v = _views[i];
                string text = round.tracks[i].label;
                v.label.text = text;
                v.label.fontSize = _labelFontSize;
                v.label.color = Color.Lerp(SlotColors[i], _ink, 0.25f);

                float em = _labelFontSize * 0.1f;
                float w = text.Length * em * 0.62f + 0.2f;
                float h = em * 1.15f;
                Vector2 best = Vector2.zero;
                float bestCost = float.MaxValue;

                for (int f = 0; f < LabelFractions.Length; f++)
                {
                    float x = _simulation.FinishX * LabelFractions[(f + i) % LabelFractions.Length];
                    float yTrack = -TrackYAtX(_simulation.GetPath(i), x);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float y = yTrack + side * (h * 0.5f + 0.22f);
                        var rect = new Rect(x - w * 0.5f, y - h * 0.5f, w, h);
                        float cost = OverlapCost(rect, placed, count, i) + f * 0.02f;
                        if (rect.xMin < _arenaMin.x || rect.xMax > _arenaMax.x || rect.yMin < -_arenaMax.y || rect.yMax > -_arenaMin.y)
                        {
                            cost += 50f;
                        }

                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            best = new Vector2(x, y);
                        }
                    }
                }

                placed.Add(new Rect(best.x - w * 0.5f, best.y - h * 0.5f, w, h));
                v.label.transform.position = new Vector3(best.x, best.y, 0f);
                v.labelPlaced = true;
            }
        }

        float OverlapCost(Rect rect, List<Rect> placed, int count, int self)
        {
            float cost = 0f;
            for (int p = 0; p < placed.Count; p++)
            {
                if (rect.Overlaps(placed[p]))
                {
                    cost += 20f;
                }
            }

            Rect grown = new Rect(rect.x - Pad, rect.y - Pad, rect.width + Pad * 2f, rect.height + Pad * 2f);
            for (int t = 0; t < count; t++)
            {
                CurvePath path = _simulation.GetPath(t);
                int step = Mathf.Max(1, path.Count / 120);
                for (int s = 0; s < path.Count; s += step)
                {
                    if (grown.Contains(new Vector2((float)path.X(s), -(float)path.Y(s))))
                    {
                        cost += t == self ? 0.5f : 1.5f;
                    }
                }
            }

            return cost;
        }

        static float TrackYAtX(CurvePath path, float x)
        {
            for (int i = 1; i < path.Count; i++)
            {
                if (path.X(i) >= x)
                {
                    double x0 = path.X(i - 1);
                    double x1 = path.X(i);
                    double f = x1 > x0 ? (x - x0) / (x1 - x0) : 0.0;
                    return (float)(path.Y(i - 1) + (path.Y(i) - path.Y(i - 1)) * f);
                }
            }

            return (float)path.Y(path.Count - 1);
        }

        // ---------------------------------------------------------------- start and finish

        void DrawMarkers()
        {
            float fx = _simulation.FinishX;
            float fy = -_simulation.FinishY;
            _startMarker.position = Vector3.zero;
            _finishMarker.position = new Vector3(fx, fy, 0f);
            _pole.position = new Vector3(fx, fy + 0.85f, 0f);
            _cloth.position = new Vector3(fx + 0.78f, fy + 1.35f, 0f);
            _finishLabel.transform.position = new Vector3(fx + 0.2f, fy + 2.0f, 0f);
        }

        // ---------------------------------------------------------------- audio

        void UpdateTicks(BrachPhase phase, float dt)
        {
            if (phase != BrachPhase.Run && phase != BrachPhase.Result)
            {
                return;
            }

            float g = _simulation.Config.gravity;
            float vMax = Mathf.Sqrt(2f * g * Mathf.Max(1f, _arenaMax.y));
            int count = _simulation.TrackCount;
            for (int i = 0; i < count; i++)
            {
                TrackView v = _views[i];
                int place = _simulation.GetPlace(i);
                if (place != v.prevPlace)
                {
                    v.prevPlace = place;
                    if (place > 0)
                    {
                        OnFinish(i, place);
                    }
                }

                if (phase != BrachPhase.Run || place > 0)
                {
                    continue;
                }

                _simulation.GetBall(i, out _, out _, out _, out float speed);
                float rel = Mathf.Clamp01(speed / vMax);
                float interval = Mathf.Lerp(0.18f, 0.05f, rel) / Mathf.Max(0.2f, _simulation.ClockRate);
                v.tickTimer -= dt;
                if (v.tickTimer <= 0f && speed > 0.05f)
                {
                    v.tickTimer = interval;
                    v.tick.pitch = 0.7f + 1.5f * rel; // faster ball, higher tick
                    v.tick.PlayOneShot(_tickClip, _tickVolume);
                }
            }
        }

        void OnFinish(int slot, int place)
        {
            TrackView v = _views[slot];
            v.burstAge = 0f;
            v.burst.enabled = true;
            _simulation.GetBall(slot, out float bx, out float by, out _, out _);
            v.burst.transform.position = new Vector3(bx, -by, 0f);
            if (_sfx != null)
            {
                int degree = Mathf.Clamp(6 - place * 2, 0, 7);
                _sfx.PlayOneShot(_chimes[degree], place == 1 ? _chimeVolume : _chimeVolume * 0.6f);
            }
        }

        void UpdateBursts(float dt)
        {
            foreach (TrackView v in _views)
            {
                if (v.burstAge < 0f)
                {
                    continue;
                }

                v.burstAge += dt;
                float t = v.burstAge / 0.6f;
                if (t >= 1f)
                {
                    v.burstAge = -1f;
                    v.burst.enabled = false;
                    continue;
                }

                float s = Mathf.Lerp(0.5f, 1.9f, t);
                v.burst.transform.localScale = new Vector3(s, s, 1f);
                Color c = v.burst.color;
                c.a = 1f - t;
                v.burst.color = c;
            }
        }

        // ---------------------------------------------------------------- camera

        void UpdateCamera(BrachPhase phase, float dt)
        {
            if (_camera == null)
            {
                return;
            }

            float target = 0f;
            if (phase == BrachPhase.Run && _simulation.SlowMoFired)
            {
                target = Mathf.Clamp01(Mathf.InverseLerp(1f, _simulation.Config.slowMoRate, _simulation.ClockRate));
            }

            _follow = Mathf.MoveTowards(_follow, target, dt * _followSpeed);
            float f = _follow * _follow * (3f - 2f * _follow);
            float ortho = _baseOrtho * (1f - _followZoom * f);
            float offset = (_followScreenX - 0.5f) * 2f * ortho * WideFrameLayout.Aspect(AspectMode.Wide);
            Vector3 follow = new Vector3(_simulation.FinishX - offset, -_simulation.FinishY, _baseCamPos.z);
            _camera.orthographicSize = ortho;
            _camera.transform.position = Vector3.Lerp(_baseCamPos, follow, f);
        }

        // ---------------------------------------------------------------- HUD

        void UpdateLadder(BrachPhase phase, bool endCard)
        {
            if (_ladderGroup == null)
            {
                return;
            }

            int count = _simulation.TrackCount;
            int finished = 0;
            for (int i = 0; i < _ladderRows.Count; i++)
            {
                int track = -1;
                for (int t = 0; t < count; t++)
                {
                    if (_simulation.GetPlace(t) == i + 1)
                    {
                        track = t;
                        break;
                    }
                }

                bool show = track >= 0 && (phase == BrachPhase.Run || phase == BrachPhase.Result);
                _ladderRows[i].SetActive(show);
                if (show)
                {
                    finished++;
                    _ladderSwatches[i].color = SlotColors[track];
                    SetText(_ladderNames[i], (i + 1) + "  " + _simulation.CurrentRound.tracks[track].label);
                    SetText(_ladderTimes[i], _simulation.GetFinishTime(track).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s");
                }
            }

            float want = finished > 0 && !endCard ? 1f : 0f;
            _ladderGroup.alpha = Mathf.MoveTowards(_ladderGroup.alpha, want, Time.deltaTime * 6f);
        }

        void UpdateHud(BrachPhase phase, bool endCard)
        {
            if (_card == null)
            {
                return;
            }

            BrachistochroneConfig cfg = _simulation.Config;
            _card.SetCounter("ROUND " + (_simulation.RoundIndex + 1) + "/" + _simulation.RoundTotal);
            if (endCard)
            {
                _card.SetHook(cfg.endCard);
                _card.SetResultCaption(_endClock < 1.75f ? cfg.endLine : cfg.endLine2);
            }
            else
            {
                _card.SetHook(cfg.question);
                _card.SetResultCaption(_simulation.BottomLine);
            }
        }

        static void SetText(TMP_Text target, string text)
        {
            if (target.text != text)
            {
                target.text = text;
            }
        }
    }
}
