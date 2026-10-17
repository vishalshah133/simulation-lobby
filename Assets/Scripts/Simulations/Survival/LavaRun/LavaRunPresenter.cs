using System.Collections.Generic;
using SimulationLobby.Presentation;
using SimulationLobby.Shared;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.Survival.LavaRun
{
    /// <summary>
    /// Everything on screen for Lava Run: HUD, captions, lava ribbons, effects, camera and sound. Reads the
    /// simulation (state plus the append-only event list) in <c>Update</c>/<c>LateUpdate</c> and never writes it.
    /// </summary>
    public sealed class LavaRunPresenter : MonoBehaviour
    {
        public LavaRunSimulation simulation;
        public FocusCameraRig cameraRig;
        [Tooltip("4: stage 1, 2, 3 (bridge), island. Position + forward; look target = position + forward * poseLookDistance.")]
        public Transform[] stageCameraPoses;
        public float poseLookDistance = 20f;
        [Tooltip("Whole volcano, used for the hook and between stages.")]
        public Transform wideCameraPose;
        [Tooltip("Close on the four pens with the crater behind (6-8 m). Used during the Hold; falls back to the wide pose.")]
        public Transform hookCameraPose;
        [Tooltip("caption = beat text, subline = rules, footer = STAGE n / 4 - NAME.")]
        public CaptionHud caption;
        public ShareBarHud counterBar;
        public TMP_Text questionText;
        [Tooltip("One per stage path (3).")]
        public PathRibbonView[] lavaRibbons;
        public Material lavaMaterial;
        public float lavaRibbonWidth = 2.6f;
        public BlastFx blastFx;
        public SmokePlumeFx smokeFx;
        public EmberBurst embers;
        public ConfettiBurst3D confetti;
        [Tooltip("Eruption location (stage 1); stages 2-3 erupt at their path start.")]
        public Transform craterPoint;
        [Tooltip("4 visual-only marble copies (no collider or rigidbody needed) for sink animations.")]
        public GameObject[] ghostTemplates;
        public LavaRunAudioKit audioKit;
        [Tooltip("Lava lake surface. A runtime copy of its material is made and scrolled slowly.")]
        public Renderer lakeRenderer;
        [Tooltip("Alternative to lakeRenderer: a runtime copy of this material is scrolled and assigned to lakeRenderer if set.")]
        public Material lakeMaterial;
        public Vector2 lakeScrollSpeed = new Vector2(0.02f, 0.012f);

        [Header("Tuning (optional)")]
        [Tooltip("Height of the lava ribbon above the track centreline.")]
        public float lavaRibbonHeight = 0.06f;
        [Tooltip("Lava texture scroll, UV units per second.")]
        public Vector2 lavaScrollSpeed = new Vector2(0.12f, 0.03f);
        [Tooltip("0 = static stage pose, 1 = the camera tracks fully along the lava-to-pack midpoint.")]
        [Range(0f, 1f)] public float cameraFollow = 0.55f;
        [Min(0.05f)] public float cameraSmoothSeconds = 1.4f;

        [Header("Chase camera (stages 1-3)")]
        [Tooltip("Seconds of the static stage pose at the start of each stage before the chase takes over.")]
        public float chaseDelaySeconds = 2f;
        [Min(0.1f)] public float chaseSmoothSeconds = 0.8f;
        public float chaseMinDistance = 5f;
        public float chaseMaxDistance = 12f;
        public float chaseDistancePerGap = 0.9f;
        [Range(0f, 1f)] public float chaseFrontBlend = 0.3f;
        [Tooltip("Camera offset relative to the path tangent: side, up, back (normalised, then scaled by the distance).")]
        public Vector3 chaseOffset = new Vector3(0.6f, 0.45f, 0.65f);
        [Tooltip("Camera never sinks below the track point's Y plus this.")]
        public float chaseMinHeight = 2f;

        [Header("Island camera")]
        [Tooltip("Island phase: the camera sits far enough back that the whole remaining platform (outermost unsunk ring) fits on screen at both 9:16 and 16:9, inside the band the HUD panels leave free.")]
        [Range(1f, 1.5f)] public float islandFitMargin = 1.12f;
        [Tooltip("Fraction of screen height the top HUD (share bar + footer) covers; the platform is kept below it.")]
        [Range(0f, 0.4f)] public float islandHudTopFraction = 0.17f;
        [Tooltip("Fraction of screen height the bottom HUD (caption + rules) covers; the platform is kept above it.")]
        [Range(0f, 0.4f)] public float islandHudBottomFraction = 0.14f;
        [Tooltip("Island phase: how far the look target leans toward the pack (0 = dead centre of the platform).")]
        [Range(0f, 1f)] public float islandFitPackBias = 0.15f;
        [Tooltip("Island phase: the look target never moves further than this from the island centre, in metres.")]
        public float islandFitMaxShift = 2.5f;
        [Tooltip("Island radius in metres, used if the ring meshes cannot be measured (scene builder: 15).")]
        public float islandFallbackRadius = 15f;
        [Tooltip("Smoothing for the pull-out into the island view and for the frame tightening as rings sink.")]
        [Min(0.05f)] public float islandSmoothSeconds = 1.6f;
        [Range(5f, 60f)] public float islandElevationDegrees = 28f;
        [Tooltip("Payoff / complete only: base camera distance for the push-in on the winner (scaled by 0.8).")]
        public float islandMinDistance = 10f;
        [Tooltip("Payoff / complete only: the camera looks this far toward the pack from the island centre (0 = centre, 1 = the pack).")]
        [Range(0f, 1f)] public float islandPackBias = 0.7f;
        [Tooltip("Payoff / complete only: the look target never strays further than this from the island centre.")]
        public float islandViewRadius = 13f;
        [Tooltip("The camera stays at least this high above the island deck.")]
        public float islandMinHeight = 4f;

        [Header("Course flyover")]
        [Tooltip("Seconds on the wide pose at the very start of the flyover (the hook question is on screen).")]
        [Min(0f)] public float flyoverOpeningSeconds = 2f;
        [Tooltip("Seconds orbiting the island at the end of the flyover. The stages share what is left of trackPreviewSeconds, in proportion to path length.")]
        [Min(0f)] public float flyoverIslandSeconds = 3f;
        [Tooltip("Camera distance behind the flown point, along the track.")]
        public float flyoverDistance = 11f;
        [Tooltip("Camera height above the flown track point.")]
        public float flyoverHeight = 7f;
        [Tooltip("Sideways offset from the track line, as a fraction of flyoverDistance.")]
        [Range(0f, 1f)] public float flyoverSideFraction = 0.3f;
        [Tooltip("The camera looks this many metres further down the track than the point it is over.")]
        public float flyoverLookAhead = 9f;
        [Tooltip("0 = constant speed along each stage, 1 = fully eased in and out.")]
        [Range(0f, 1f)] public float flyoverEase = 0.35f;
        [Tooltip("Smoothing while flying a stage. Short, so it glides without lagging behind.")]
        [Min(0.02f)] public float flyoverSmoothSeconds = 0.3f;
        [Tooltip("Smoothing for the glide from the wide opening into stage 1.")]
        [Min(0.02f)] public float flyoverEntrySmoothSeconds = 1f;
        [Tooltip("Island orbit: elevation and speed. The distance is fitted so the whole island shows (see Island camera).")]
        [Range(5f, 60f)] public float flyoverIslandElevationDegrees = 30f;
        public float flyoverIslandOrbitDegreesPerSecond = 14f;
        [Tooltip("Orbit start angle, degrees either side of the far-side bearing.")]
        public float flyoverIslandStartDegrees = -20f;

        [Header("Misc")]
        [Min(1)] public int maxEmberPopsPerFrame = 6;
        [Min(1)] public int maxGhosts = 120;
        [Min(0.2f)] public float ghostSeconds = 1.5f;
        [Min(0.5f)] public float captionMinSeconds = 2.2f;
        public int bridgeJamDeaths = 15;
        [Min(0.5f)] public float bridgeJamSeconds = 3f;

        const string DefaultRules = "×2 doubles you · lava kills · smallest color each stage is OUT";
        static readonly string[] FallbackStageNames = { "THE SLOPES", "THE SWITCHBACKS", "THE BRIDGE" };

        sealed class Ghost
        {
            public GameObject go;
            public Vector3 start;
            public float birth;
            public Vector3 scale;
        }

        // Caption sequencing.
        string _shownCaption;
        int _shownPriority;
        float _captionHoldUntil;
        string _pendingCaption;
        int _pendingPriority;
        float _pendingSeconds;

        // Event cursor.
        int _cursor;
        int _popsThisFrame;

        // Camera.
        Vector3 _homePos;
        Vector3 _homeLook;
        Vector3 _homeVelPos;
        Vector3 _homeVelLook;
        bool _homeInitialised;
        int _flyoverSegment = -1;
        Vector3 _followShift;
        Vector3 _followShiftVel;

        // HUD / cosmetics.
        Material _lavaInstance;
        Material _lakeInstance;
        float _runStartTime;
        int _runStartStage = -1;
        int _countdownShown = -1;
        bool _hudConfigured;
        readonly int[] _counts = new int[LavaRunConfig.TeamCount];
        readonly List<Ghost> _ghosts = new List<Ghost>();
        readonly List<int> _bridgeDeathTicks = new List<int>();
        bool _bridgeJamShown;
        readonly List<int> _plumeHandles = new List<int>();
        float _lastGateFocus = -100f;
        readonly float[] _gateFlashTime = new float[64];
        bool _wonShown;

        void Start()
        {
            if (audioKit != null)
            {
                audioKit.Build();
            }

            if (lavaMaterial != null)
            {
                // A runtime copy so scrolling the offset never dirties the shared asset.
                _lavaInstance = new Material(lavaMaterial);
            }

            Material lakeSource = lakeRenderer != null ? lakeRenderer.sharedMaterial : null;
            if (lakeMaterial != null)
            {
                lakeSource = lakeMaterial;
            }

            if (lakeSource != null)
            {
                _lakeInstance = new Material(lakeSource);
                if (lakeRenderer != null)
                {
                    lakeRenderer.sharedMaterial = _lakeInstance;
                }
            }

            if (questionText != null)
            {
                questionText.text = "WHICH COLOR SURVIVES THE LAVA?";
            }
        }

        void OnDestroy()
        {
            if (_lavaInstance != null)
            {
                Destroy(_lavaInstance);
            }

            if (_lakeInstance != null)
            {
                Destroy(_lakeInstance);
            }

            for (int i = 0; i < _ghosts.Count; i++)
            {
                if (_ghosts[i].go != null)
                {
                    Destroy(_ghosts[i].go);
                }
            }
        }

        // ------------------------------------------------------------------ setup

        bool Ready => simulation != null && simulation.Config != null && simulation.Rig != null;

        void BindRibbons()
        {
            LavaRunRig rig = simulation.Rig;
            Material material = _lavaInstance != null ? _lavaInstance : lavaMaterial;
            if (lavaRibbons == null)
            {
                return;
            }

            for (int i = 0; i < lavaRibbons.Length && i < rig.stages.Length; i++)
            {
                PathRibbonView ribbon = lavaRibbons[i];
                if (ribbon == null || rig.stages[i].path == null)
                {
                    continue;
                }

                ribbon.Bind(rig.stages[i].path, lavaRibbonWidth, lavaRibbonHeight, material);
                AnimationCurve curve = rig.stages[i].widthByDistance;
                ribbon.SetWidthByDistance(curve != null && curve.length > 0 ? curve : null, 1.05f);
                ribbon.Hide();
            }
        }

        void ConfigureHud()
        {
            LavaRunConfig config = simulation.Config;
            int n = config.teams.Length;
            if (counterBar != null)
            {
                var colors = new Color[n];
                var names = new string[n];
                for (int i = 0; i < n; i++)
                {
                    colors[i] = config.teams[i].color;
                    names[i] = config.teams[i].Short;
                }

                counterBar.showNames = false;
                counterBar.eliminatedShowsZero = true;
                counterBar.Configure(n, colors, names);
            }

            if (caption != null)
            {
                caption.SetSubline(DefaultRules);
            }

            BindRibbons();
            _hudConfigured = true;
            ShowCaption("Pick your color.", 0, 99f);
        }

        void ResetRun()
        {
            _cursor = 0;
            _bridgeDeathTicks.Clear();
            _bridgeJamShown = false;
            _wonShown = false;
            _countdownShown = -1;
            _flyoverSegment = -1;
            _followShift = Vector3.zero;
            _followShiftVel = Vector3.zero;
            _shownPriority = 0;
            _captionHoldUntil = 0f;
            _pendingCaption = null;
            for (int i = 0; i < _plumeHandles.Count; i++)
            {
                if (smokeFx != null)
                {
                    smokeFx.End(_plumeHandles[i]);
                }
            }

            _plumeHandles.Clear();
            if (counterBar != null)
            {
                for (int i = 0; i < LavaRunConfig.TeamCount; i++)
                {
                    counterBar.SetEliminated(i, false);
                }
            }

            ShowCaption("Pick your color.", 0, 99f);
        }

        // ------------------------------------------------------------------ frame loop

        void Update()
        {
            if (!Ready)
            {
                return;
            }

            if (!_hudConfigured)
            {
                ConfigureHud();
            }

            if (simulation.Events.Count < _cursor)
            {
                ResetRun();
            }

            _popsThisFrame = 0;
            ConsumeEvents();
            UpdateHud();
            UpdateLava();
            UpdateAudioLoops();
            UpdateGhosts();
            FlushCaption();
        }

        void LateUpdate()
        {
            if (!Ready)
            {
                return;
            }

            UpdateCamera();
        }

        // ------------------------------------------------------------------ HUD

        string StageName(int stage)
        {
            if (stage >= LavaRunConfig.StageCount)
            {
                return "THE ISLAND";
            }

            string rigName = simulation.Rig.stages[stage].name;
            if (!string.IsNullOrWhiteSpace(rigName) && rigName != "Stage")
            {
                return rigName.ToUpperInvariant();
            }

            return FallbackStageNames[Mathf.Clamp(stage, 0, FallbackStageNames.Length - 1)];
        }

        string Footer()
        {
            if (simulation.Phase == LavaRunPhase.Hold)
            {
                int segment = FlyoverState(out _);
                if (segment == 0)
                {
                    return "THE COURSE";
                }

                if (segment >= 1 && segment <= LavaRunConfig.StageCount)
                {
                    return $"STAGE {segment} / 4 · {StageName(segment - 1)}";
                }

                if (segment == LavaRunConfig.StageCount + 1)
                {
                    return "FINAL · THE ISLAND";
                }

                int seconds = Mathf.CeilToInt(simulation.HoldSecondsRemaining);
                return seconds > 0 ? $"STARTS IN {seconds}" : "GO!";
            }

            if (simulation.IsIsland)
            {
                return "FINAL · THE ISLAND";
            }

            return $"STAGE {simulation.CurrentStage + 1} / 4 · {StageName(simulation.CurrentStage)}";
        }

        void UpdateHud()
        {
            if (caption != null)
            {
                caption.SetFooter(Footer());
            }

            if (simulation.Phase == LavaRunPhase.Hold)
            {
                int seconds = Mathf.CeilToInt(simulation.HoldSecondsRemaining);
                if (seconds != _countdownShown)
                {
                    if (_countdownShown >= 0 && seconds >= 1 && seconds <= 3 && audioKit != null)
                    {
                        audioKit.PlayCountdownTick(false);
                    }

                    _countdownShown = seconds;
                }
            }

            if (counterBar != null)
            {
                int n = Mathf.Min(_counts.Length, simulation.TeamCount);
                for (int i = 0; i < n; i++)
                {
                    _counts[i] = simulation.AliveCount(i);
                }

                counterBar.SetCounts(_counts);
            }
        }

        string TeamTag(int team)
        {
            LavaRunTeamDef def = simulation.Config.teams[Mathf.Clamp(team, 0, simulation.Config.teams.Length - 1)];
            return $"<color=#{ColorUtility.ToHtmlStringRGB(def.color)}>{def.name}</color>";
        }

        // ------------------------------------------------------------------ captions

        /// <summary>Show now if the current caption has had its minimum time or this one outranks it; else queue it.</summary>
        void ShowCaption(string text, int priority, float seconds = -1f)
        {
            if (caption == null)
            {
                return;
            }

            if (seconds < 0f)
            {
                seconds = captionMinSeconds;
            }

            float now = Time.unscaledTime;
            if (now >= _captionHoldUntil || priority > _shownPriority)
            {
                caption.SetCaption(text);
                _shownCaption = text;
                _shownPriority = priority;
                _captionHoldUntil = now + seconds;
                _pendingCaption = null;
            }
            else if (_pendingCaption == null || priority >= _pendingPriority)
            {
                _pendingCaption = text;
                _pendingPriority = priority;
                _pendingSeconds = seconds;
            }
        }

        void FlushCaption()
        {
            if (_pendingCaption != null && Time.unscaledTime >= _captionHoldUntil)
            {
                string text = _pendingCaption;
                int priority = _pendingPriority;
                float seconds = _pendingSeconds;
                _pendingCaption = null;
                ShowCaption(text, priority, seconds);
            }
        }

        // ------------------------------------------------------------------ events

        void ConsumeEvents()
        {
            IReadOnlyList<LavaRunEvent> events = simulation.Events;
            while (_cursor < events.Count)
            {
                Handle(events[_cursor]);
                _cursor++;
            }
        }

        Vector3 StagePathPoint(int stage, float distance)
        {
            LavaStageRig rig = simulation.Rig.stages[Mathf.Clamp(stage, 0, LavaRunConfig.StageCount - 1)];
            return rig.path != null ? rig.path.PositionAtDistance(Mathf.Max(0f, distance)) : Vector3.zero;
        }

        Vector3 BasinPoint(int stage)
        {
            LavaStageRig rig = simulation.Rig.stages[Mathf.Clamp(stage, 0, LavaRunConfig.StageCount - 1)];
            if (rig.basinZone != null)
            {
                return rig.basinZone.transform.position;
            }

            return StagePathPoint(stage, rig.basinEntryDistance);
        }

        Vector3 IslandCentre()
        {
            LavaIslandRig island = simulation.Rig.island;
            return island != null && island.centre != null ? island.centre.position : Vector3.zero;
        }

        void Pop(Vector3 position, int count)
        {
            if (embers == null || _popsThisFrame >= maxEmberPopsPerFrame)
            {
                return;
            }

            _popsThisFrame++;
            embers.Pop(position, count);
        }

        void Handle(LavaRunEvent e)
        {
            switch (e.kind)
            {
                case LavaRunEventKind.StageStarted:
                    if (e.stage == 2)
                    {
                        ShowCaption("ONE GATE. ONE BRIDGE.", 3, 3f);
                    }

                    _bridgeDeathTicks.Clear();
                    break;

                case LavaRunEventKind.Released:
                    if (audioKit != null && e.stage == 0)
                    {
                        audioKit.PlayCountdownTick(true);
                    }

                    ShowCaption(e.stage == 0 ? "GO!" : "RACE!", 1, 1.2f);
                    break;

                case LavaRunEventKind.Erupted:
                    OnErupted(e);
                    break;

                case LavaRunEventKind.GateTriggered:
                    OnGate(e);
                    break;

                case LavaRunEventKind.Swallowed:
                case LavaRunEventKind.FellIn:
                    OnDeath(e);
                    break;

                case LavaRunEventKind.Sunk:
                    OnSunk(e);
                    break;

                case LavaRunEventKind.DoorClosing:
                    ShowCaption("THE DOOR IS CLOSING!", 4, 2.5f);
                    if (cameraRig != null)
                    {
                        cameraRig.Focus(BasinPoint(e.stage), 0.4f, 2.2f);
                    }

                    break;

                case LavaRunEventKind.DoorClosed:
                    if (audioKit != null)
                    {
                        audioKit.PlayDoorSlam();
                    }

                    if (cameraRig != null)
                    {
                        cameraRig.Shake(0.18f, 0.5f);
                    }

                    break;

                case LavaRunEventKind.TeamEliminated:
                    if (counterBar != null)
                    {
                        counterBar.SetEliminated(e.team, true);
                    }

                    ShowCaption($"{TeamTag(e.team)} IS OUT", 6, 3.2f);
                    if (audioKit != null)
                    {
                        audioKit.PlayElimination();
                    }

                    if (cameraRig != null)
                    {
                        cameraRig.Focus(BasinPoint(e.stage), 0.55f, 2.8f);
                    }

                    break;

                case LavaRunEventKind.TeamOut:
                    if (counterBar != null)
                    {
                        counterBar.SetEliminated(e.team, true);
                    }

                    ShowCaption($"{TeamTag(e.team)} IS WIPED OUT", 6, 3.2f);
                    if (audioKit != null)
                    {
                        audioKit.PlayElimination();
                    }

                    if (cameraRig != null && e.position != Vector3.zero)
                    {
                        cameraRig.Focus(e.position, 0.5f, 2.2f);
                    }

                    break;

                case LavaRunEventKind.TrapdoorOpened:
                    if (audioKit != null)
                    {
                        audioKit.PlayDoorSlam();
                    }

                    EndPlumes();
                    break;

                case LavaRunEventKind.IslandStarted:
                    ShowCaption("THE ISLAND IS SINKING!", 5, 3.5f);
                    EndPlumes();
                    break;

                case LavaRunEventKind.RingSinking:
                    if (cameraRig != null)
                    {
                        cameraRig.Focus(IslandCentre(), 0.3f, 2.5f);
                        cameraRig.Shake(0.15f, 1.4f);
                    }

                    if (audioKit != null)
                    {
                        audioKit.PlayRingCrack();
                    }

                    break;

                case LavaRunEventKind.SlowdownStarted:
                    ShowCaption("LAST ONES STANDING", 5, 3f);
                    break;

                case LavaRunEventKind.ForcedDecision:
                    ShowCaption($"{TeamTag(e.team)} WINS ON NUMBERS", 8, 6f);
                    break;

                case LavaRunEventKind.Winner:
                    OnWinner(e);
                    break;
            }
        }

        void OnErupted(LavaRunEvent e)
        {
            ShowCaption(e.stage == 0 ? "THE LAVA IS COMING!" : "LAVA SPEED UP!", 5, 3f);

            Vector3 origin = e.stage == 0 && craterPoint != null ? craterPoint.position : StagePathPoint(e.stage, 0f);
            if (blastFx != null)
            {
                blastFx.Play(origin, e.stage == 0 ? 6f : 3.5f, new Color(1f, 0.55f, 0.1f), 1.1f);
            }

            if (smokeFx != null)
            {
                int handle = smokeFx.Begin(origin, e.stage == 0 ? 3f : 1.8f);
                if (handle >= 0)
                {
                    _plumeHandles.Add(handle);
                }
            }

            if (cameraRig != null)
            {
                cameraRig.Shake(e.stage == 0 ? 0.7f : 0.45f, 1.6f);
            }

            if (audioKit != null)
            {
                audioKit.PlayEruption();
            }
        }

        void EndPlumes()
        {
            if (smokeFx == null)
            {
                _plumeHandles.Clear();
                return;
            }

            for (int i = 0; i < _plumeHandles.Count; i++)
            {
                smokeFx.End(_plumeHandles[i]);
            }

            _plumeHandles.Clear();
        }

        void OnGate(LavaRunEvent e)
        {
            if (audioKit != null)
            {
                audioKit.PlayGate(simulation.AliveCount(e.team), e.value + 1);
            }

            if (e.value <= 0)
            {
                return;
            }

            // Gate arch flash in the team colour: a small BlastFx at the anchor, rate-limited per gate.
            int key = Mathf.Clamp(e.stage * 16 + e.index, 0, _gateFlashTime.Length - 1);
            float now = Time.unscaledTime;
            if (now - _gateFlashTime[key] < 0.35f)
            {
                return;
            }

            _gateFlashTime[key] = now;
            Vector3 at = e.position;
            LavaStageRig stage = simulation.Rig.stages[Mathf.Clamp(e.stage, 0, LavaRunConfig.StageCount - 1)];
            if (stage.gates != null && e.index >= 0 && e.index < stage.gates.Length && stage.gates[e.index] != null &&
                stage.gates[e.index].flashAnchor != null)
            {
                at = stage.gates[e.index].flashAnchor.position;
            }

            if (blastFx != null)
            {
                blastFx.Play(at, 1.4f, simulation.Config.teams[e.team].color, 0.45f);
            }

            // Camera: sparingly. The big multiplier gates, at most once every 14 s.
            if (cameraRig != null && e.value >= 3 && now - _lastGateFocus > 14f)
            {
                _lastGateFocus = now;
                cameraRig.Focus(at, 0.3f, 1.4f);
            }
        }

        void OnDeath(LavaRunEvent e)
        {
            Pop(e.position, 12);
            if (audioKit != null)
            {
                audioKit.PlayDeath(e.kind == LavaRunEventKind.Swallowed);
            }

            if (e.stage == 2 && !_bridgeJamShown)
            {
                _bridgeDeathTicks.Add(e.tick);
                int window = Mathf.Max(1, Mathf.RoundToInt(bridgeJamSeconds / Mathf.Max(0.0001f, Time.fixedDeltaTime)));
                while (_bridgeDeathTicks.Count > 0 && e.tick - _bridgeDeathTicks[0] > window)
                {
                    _bridgeDeathTicks.RemoveAt(0);
                }

                if (_bridgeDeathTicks.Count >= bridgeJamDeaths)
                {
                    _bridgeJamShown = true;
                    ShowCaption("TOO MANY ON THE BRIDGE!", 7, 3.5f);
                    if (cameraRig != null)
                    {
                        cameraRig.Focus(e.position, 0.45f, 3f);
                        cameraRig.Shake(0.2f, 0.8f);
                    }
                }
            }
        }

        void OnSunk(LavaRunEvent e)
        {
            if (audioKit != null)
            {
                audioKit.PlaySink();
            }

            if (ghostTemplates == null || e.team < 0 || e.team >= ghostTemplates.Length || ghostTemplates[e.team] == null ||
                _ghosts.Count >= maxGhosts)
            {
                Pop(e.position, 10);
                return;
            }

            GameObject copy = Instantiate(ghostTemplates[e.team], e.position, Quaternion.identity);
            foreach (Collider c in copy.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }

            Rigidbody rb = copy.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Destroy(rb);
            }

            copy.SetActive(true);
            _ghosts.Add(new Ghost { go = copy, start = e.position, birth = Time.time, scale = copy.transform.localScale });
            Pop(e.position, 8);
        }

        void OnWinner(LavaRunEvent e)
        {
            if (_wonShown)
            {
                return;
            }

            _wonShown = true;
            ShowCaption($"{TeamTag(e.team)} SURVIVES THE LAVA!", 10, 60f);
            Vector3 centre = simulation.PackCentroid;
            if (centre == Vector3.zero)
            {
                centre = IslandCentre();
            }

            if (confetti != null)
            {
                confetti.Burst(centre + Vector3.up * 1.5f, simulation.Config.teams[e.team].color, 260);
            }

            if (cameraRig != null)
            {
                cameraRig.Focus(centre, 0.7f, 30f);
            }

            if (audioKit != null)
            {
                audioKit.PlayWinner();
            }
        }

        // ------------------------------------------------------------------ lava ribbons

        void UpdateLava()
        {
            if (_lavaInstance != null)
            {
                Vector2 offset = lavaScrollSpeed * Time.time;
                if (_lavaInstance.HasProperty("_BaseMap"))
                {
                    _lavaInstance.SetTextureOffset("_BaseMap", offset);
                }
                else
                {
                    _lavaInstance.mainTextureOffset = offset;
                }
            }

            if (_lakeInstance != null)
            {
                Vector2 lakeOffset = lakeScrollSpeed * Time.time;
                string prop = _lakeInstance.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (_lakeInstance.HasProperty(prop))
                {
                    _lakeInstance.SetTextureOffset(prop, lakeOffset);
                }
            }

            if (lavaRibbons == null)
            {
                return;
            }

            LavaRunConfig config = simulation.Config;
            int current = simulation.CurrentStage;
            for (int i = 0; i < lavaRibbons.Length && i < LavaRunConfig.StageCount; i++)
            {
                PathRibbonView ribbon = lavaRibbons[i];
                if (ribbon == null)
                {
                    continue;
                }

                float from = Mathf.Max(0f, config.lavaStartDistance);
                if (i < current)
                {
                    // The lava stays where it flowed: filled to the basin.
                    ribbon.SetSpan(from, simulation.Rig.stages[i].basinEntryDistance);
                }
                else if (i == current && simulation.HasErupted && !simulation.IsIsland)
                {
                    ribbon.SetSpan(from, simulation.LavaFrontDistance);
                }
                else
                {
                    ribbon.SetSpan(0f, 0f);
                }
            }
        }

        void UpdateAudioLoops()
        {
            if (audioKit == null)
            {
                return;
            }

            if (simulation.IsIsland)
            {
                audioKit.SetLakeRumble(0.35f);
                audioKit.SetSweeper(simulation.SweeperDegreesPerSecond);
            }
            else
            {
                bool active = simulation.HasErupted &&
                              (simulation.Phase == LavaRunPhase.Running || simulation.Phase == LavaRunPhase.DoorClosing);
                audioKit.SetLavaGap(active, simulation.LavaGapMeters);
                audioKit.SetSweeper(0f);
            }
        }

        // ------------------------------------------------------------------ ghosts

        void UpdateGhosts()
        {
            float now = Time.time;
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                Ghost g = _ghosts[i];
                if (g.go == null)
                {
                    _ghosts.RemoveAt(i);
                    continue;
                }

                float t = (now - g.birth) / ghostSeconds;
                if (t >= 1f)
                {
                    Destroy(g.go);
                    _ghosts.RemoveAt(i);
                    continue;
                }

                float sink = t * t;
                g.go.transform.position = g.start + Vector3.down * (sink * 0.9f);
                g.go.transform.localScale = g.scale * Mathf.Max(0.02f, 1f - sink);
                if (embers != null && _popsThisFrame < maxEmberPopsPerFrame && ((int)(t * 100f) + i) % 23 == 0)
                {
                    _popsThisFrame++;
                    embers.Pop(g.go.transform.position, 3);
                }
            }
        }

        // ------------------------------------------------------------------ camera

        static void PoseOf(Transform pose, float lookDistance, out Vector3 position, out Vector3 look)
        {
            position = pose.position;
            look = pose.position + pose.forward * lookDistance;
        }

        void UpdateCamera()
        {
            if (cameraRig == null)
            {
                return;
            }

            LavaRunPhase phase = simulation.Phase;
            int stage = simulation.CurrentStage;

            if (phase == LavaRunPhase.Running && _runStartStage != stage)
            {
                _runStartStage = stage;
                _runStartTime = Time.time;
            }
            else if (phase == LavaRunPhase.PenSettle || phase == LavaRunPhase.Hold)
            {
                _runStartStage = -1;
            }

            Vector3 targetPos;
            Vector3 targetLook;
            float smooth = cameraSmoothSeconds;

            bool snap = false;
            float flyU = 0f;
            int flySegment = FlyoverState(out flyU);
            if (flySegment < 0 && _flyoverSegment >= 0)
            {
                // Flyover finished: hard cut to the hook pose for the countdown.
                snap = phase == LavaRunPhase.Hold;
                _flyoverSegment = -1;
            }

            if (flySegment >= 0 && FlyoverFraming(flySegment, flyU, out targetPos, out targetLook))
            {
                // Hard cut between segments (separate parts of the volcano), except the glide from the wide opening.
                bool entryGlide = flySegment == 1 && _flyoverSegment == 0;
                smooth = entryGlide ? flyoverEntrySmoothSeconds : flyoverSmoothSeconds;
                snap = flySegment != _flyoverSegment && !entryGlide;
                _flyoverSegment = flySegment;
            }
            else if (phase == LavaRunPhase.Island || phase == LavaRunPhase.Payoff || phase == LavaRunPhase.Complete)
            {
                IslandFraming(phase, out targetPos, out targetLook);
                if (phase == LavaRunPhase.Island)
                {
                    smooth = islandSmoothSeconds;
                }
            }
            else if (!simulation.IsIsland && (phase == LavaRunPhase.Running || phase == LavaRunPhase.DoorClosing) &&
                     Time.time - _runStartTime >= chaseDelaySeconds && ChaseFraming(stage, out targetPos, out targetLook))
            {
                smooth = chaseSmoothSeconds;
            }
            else
            {
                Transform pose;
                switch (phase)
                {
                    case LavaRunPhase.Hold:
                        pose = hookCameraPose != null ? hookCameraPose : wideCameraPose;
                        break;
                    case LavaRunPhase.Drop:
                        pose = wideCameraPose;
                        break;
                    default:
                        pose = PoseFor(stage);
                        break;
                }

                if (pose == null)
                {
                    pose = wideCameraPose != null ? wideCameraPose : PoseFor(0);
                }

                if (pose == null)
                {
                    return;
                }

                PoseOf(pose, poseLookDistance, out targetPos, out targetLook);
            }

            if (!_homeInitialised || snap)
            {
                _homePos = targetPos;
                _homeLook = targetLook;
                _homeVelPos = Vector3.zero;
                _homeVelLook = Vector3.zero;
                _homeInitialised = true;
            }
            else
            {
                _homePos = Vector3.SmoothDamp(_homePos, targetPos, ref _homeVelPos, smooth);
                _homeLook = Vector3.SmoothDamp(_homeLook, targetLook, ref _homeVelLook, smooth);
            }

            cameraRig.SetHome(_homePos, _homeLook);
        }

        /// <summary>
        /// Where the Hold is in the course flyover: -1 when there is none (preview 0, not in Hold, or finished),
        /// 0 = wide opening, 1-3 = stages 1-3, 4 = island. <paramref name="u"/> is 0-1 progress within the segment.
        /// Derived only from the simulation's Hold clock, so a re-render frames identically.
        /// </summary>
        int FlyoverState(out float u)
        {
            u = 0f;
            float preview = simulation.Config.trackPreviewSeconds;
            if (simulation.Phase != LavaRunPhase.Hold || preview <= 0f)
            {
                return -1;
            }

            float t = simulation.HoldSecondsElapsed;
            if (t >= preview)
            {
                return -1;
            }

            float opening = Mathf.Min(flyoverOpeningSeconds, preview * 0.3f);
            float island = Mathf.Min(flyoverIslandSeconds, preview * 0.3f);
            float stagesTime = preview - opening - island;
            if (t < opening)
            {
                u = opening > 0f ? t / opening : 0f;
                return 0;
            }

            if (t >= preview - island)
            {
                u = island > 0f ? (t - (preview - island)) / island : 0f;
                return LavaRunConfig.StageCount + 1;
            }

            float total = 0f;
            for (int i = 0; i < LavaRunConfig.StageCount; i++)
            {
                total += StageLength(i);
            }

            float local = t - opening;
            if (total <= 0.01f)
            {
                u = Mathf.Clamp01(local / Mathf.Max(0.01f, stagesTime));
                return 1;
            }

            float cursor = 0f;
            for (int i = 0; i < LavaRunConfig.StageCount; i++)
            {
                float span = stagesTime * StageLength(i) / total;
                if (local < cursor + span || i == LavaRunConfig.StageCount - 1)
                {
                    u = span > 0f ? Mathf.Clamp01((local - cursor) / span) : 1f;
                    return i + 1;
                }

                cursor += span;
            }

            return LavaRunConfig.StageCount;
        }

        float StageLength(int stage)
        {
            TrackPath path = simulation.Rig.stages[stage].path;
            return path != null ? path.TotalLength : 0f;
        }

        bool FlyoverFraming(int segment, float u, out Vector3 position, out Vector3 look)
        {
            position = look = Vector3.zero;
            Camera cam = Camera.main;
            float portrait = cam != null && cam.aspect < 1f ? Mathf.Min(1.4f, 1f / Mathf.Sqrt(cam.aspect)) : 1f;

            if (segment == 0)
            {
                Transform pose = wideCameraPose != null ? wideCameraPose : (hookCameraPose != null ? hookCameraPose : PoseFor(0));
                if (pose == null)
                {
                    return false;
                }

                PoseOf(pose, poseLookDistance, out position, out look);
                return true;
            }

            if (segment == LavaRunConfig.StageCount + 1)
            {
                Vector3 centre = IslandCentre();
                if (centre == Vector3.zero)
                {
                    return false;
                }

                // Far side of the island from the volcano axis (as IslandFraming), then a slow orbit.
                Vector3 bearing = new Vector3(centre.x, 0f, centre.z);
                bearing = bearing.sqrMagnitude > 1e-4f ? bearing.normalized : Vector3.back;
                float islandTime = Mathf.Min(flyoverIslandSeconds, simulation.Config.trackPreviewSeconds * 0.3f);
                float angle = flyoverIslandStartDegrees + flyoverIslandOrbitDegreesPerSecond * u * islandTime;
                bearing = Quaternion.AngleAxis(angle, Vector3.up) * bearing;
                float elevation = flyoverIslandElevationDegrees * Mathf.Deg2Rad;
                Vector3 flyDir = bearing * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation);
                FitIsland(centre, centre, flyDir, FullIslandRadius(), out position, out look);
                return true;
            }

            TrackPath path = simulation.Rig.stages[segment - 1].path;
            if (path == null || path.TotalLength < 0.01f)
            {
                return false;
            }

            float eased = Mathf.Lerp(u, u * u * (3f - 2f * u), flyoverEase);
            float d = eased * path.TotalLength;
            Vector3 point = path.PositionAtDistance(d);
            Vector3 tangent = path.PositionAtDistance(Mathf.Min(path.TotalLength, d + 1.5f)) -
                              path.PositionAtDistance(Mathf.Max(0f, d - 1.5f));
            if (tangent.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            tangent.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, tangent);
            side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
            float back = flyoverDistance * portrait;
            position = point - tangent * back + side * (back * flyoverSideFraction) + Vector3.up * (flyoverHeight * portrait);
            position.y = Mathf.Max(position.y, point.y + chaseMinHeight);
            look = path.PositionAtDistance(Mathf.Min(path.TotalLength, d + flyoverLookAhead));
            return true;
        }

        /// <summary>Close 3/4 view on the tail of the pack with the lava lip in frame.</summary>
        bool ChaseFraming(int stage, out Vector3 position, out Vector3 look)
        {
            position = look = Vector3.zero;
            LavaStageRig rig = simulation.Rig.stages[Mathf.Clamp(stage, 0, LavaRunConfig.StageCount - 1)];
            TrackPath path = rig.path;
            if (path == null || simulation.PackCentroid == Vector3.zero)
            {
                return false;
            }

            float tail = simulation.PackMinDistance;
            float front = simulation.HasErupted ? simulation.LavaFrontDistance : simulation.Config.lavaStartDistance;
            float gap = Mathf.Max(0f, tail - front);
            float near = 1f - Mathf.Clamp01((gap - 8f) / 12f);
            float focusDistance = Mathf.Lerp(tail, Mathf.Max(front, 0f), chaseFrontBlend * near);
            focusDistance = Mathf.Clamp(focusDistance, 0f, path.TotalLength);

            Vector3 point = path.PositionAtDistance(focusDistance);
            Vector3 tangent = path.PositionAtDistance(Mathf.Min(path.TotalLength, focusDistance + 1.5f)) -
                              path.PositionAtDistance(Mathf.Max(0f, focusDistance - 1.5f));
            if (tangent.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            tangent.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, tangent);
            side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;

            float distance = Mathf.Clamp(gap * chaseDistancePerGap, chaseMinDistance, chaseMaxDistance);
            Vector3 offset = (Vector3.up * chaseOffset.y + side * chaseOffset.x - tangent * chaseOffset.z).normalized * distance;
            position = point + offset;
            position.y = Mathf.Max(position.y, point.y + chaseMinHeight);
            look = point + tangent * 0.5f;
            return true;
        }

        void IslandFraming(LavaRunPhase phase, out Vector3 position, out Vector3 look)
        {
            // Island centre, with a fallback: the island pose is built to look at the centre from poseLookDistance.
            Vector3 centre = IslandCentre();
            if (centre == Vector3.zero)
            {
                Transform islandPose = PoseFor(3);
                centre = islandPose != null ? islandPose.position + islandPose.forward * poseLookDistance : Vector3.zero;
            }

            // The marbles land on the bridge-side rim (the volcano side), so a camera on that side ends up with the
            // pack behind it. Sit on the far side instead: away from the volcano axis, looking back across the island
            // at the pack. The island is far outside the cone, so nothing can stand between camera and island there.
            Vector3 bearing = new Vector3(centre.x, 0f, centre.z);
            bearing = bearing.sqrMagnitude > 1e-4f ? bearing.normalized : Vector3.back;

            float angle = Mathf.Sin(Time.time * 0.07f) * 12f;
            bearing = Quaternion.AngleAxis(angle, Vector3.up) * bearing;
            float elevation = islandElevationDegrees * Mathf.Deg2Rad;
            Vector3 dir = bearing * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation);
            Vector3 pack = simulation.PackCentroid;

            if (phase == LavaRunPhase.Island)
            {
                // Frame the platform, not the pack: look at the centre (a small, clamped lean toward the pack) and
                // back off until the circle of the outermost unsunk ring fits the free band of the screen.
                Vector3 shift = Vector3.zero;
                if (pack != Vector3.zero)
                {
                    shift = pack - centre;
                    shift.y = 0f;
                    shift = Vector3.ClampMagnitude(shift * islandFitPackBias, islandFitMaxShift);
                }

                FitIsland(centre, centre + shift, dir, CurrentIslandRadius(), out position, out look);
                return;
            }

            // Payoff / complete: push in on the winner, between the island centre and the pack.
            Vector3 focus = centre;
            if (pack != Vector3.zero)
            {
                Vector3 offset = pack - centre;
                offset.y = 0f;
                float maxOffset = islandViewRadius;
                if (offset.sqrMagnitude > maxOffset * maxOffset)
                {
                    offset = offset.normalized * maxOffset;
                }

                focus = centre + offset * islandPackBias;
            }

            float distance = islandMinDistance * 0.8f;

            // Portrait (Shorts) frames are narrow: back off so the same width of island still fits.
            Camera cam = Camera.main;
            if (cam != null && cam.aspect < 1f)
            {
                distance *= Mathf.Min(1.4f, 1f / Mathf.Sqrt(cam.aspect));
            }

            position = focus + dir * distance;

            // Never at or below the island deck / lava surface.
            position.y = Mathf.Max(position.y, centre.y + islandMinHeight);
            look = focus + Vector3.up * 0.3f;
        }

        float[] _ringRadii;

        /// <summary>Outer radius of every island ring (outermost first), measured from the ring meshes' bounds.</summary>
        float[] RingRadii()
        {
            LavaIslandRig island = simulation.Rig.island;
            int count = island != null && island.rings != null ? island.rings.Length : 0;
            if (_ringRadii != null && _ringRadii.Length == count)
            {
                return _ringRadii;
            }

            _ringRadii = new float[count];
            for (int i = 0; i < count; i++)
            {
                Renderer renderer = island.rings[i] != null ? island.rings[i].GetComponentInChildren<Renderer>() : null;
                if (renderer != null)
                {
                    Vector3 e = renderer.bounds.extents;
                    _ringRadii[i] = Mathf.Max(e.x, e.z);
                }
            }

            if (count > 0 && _ringRadii[0] < 1f)
            {
                _ringRadii[0] = islandFallbackRadius;
            }

            return _ringRadii;
        }

        float FullIslandRadius()
        {
            float[] radii = RingRadii();
            return radii.Length > 0 && radii[0] > 1f ? radii[0] : islandFallbackRadius;
        }

        /// <summary>
        /// Radius of the platform still standing: the outermost ring that has not sunk, easing in toward the next
        /// ring as it lowers. The innermost ring never sinks, so this never drops below it.
        /// </summary>
        float CurrentIslandRadius()
        {
            float[] radii = RingRadii();
            int count = Mathf.Min(radii.Length, simulation.RingCount);
            if (count < 2 || radii[1] < 1f)
            {
                return FullIslandRadius();
            }

            for (int i = 0; i < count; i++)
            {
                if (i < count - 1 && simulation.RingStateOf(i) == LavaRingState.Sunk)
                {
                    continue;
                }

                float r = radii[i];
                if (i < count - 1 && radii[i + 1] > 1f)
                {
                    float blend = Mathf.Clamp01((simulation.RingProgress(i) - 0.5f) * 2f);
                    r = Mathf.Lerp(r, radii[i + 1], blend * blend * (3f - 2f * blend));
                }

                return r;
            }

            return radii[count - 1];
        }

        /// <summary>
        /// Puts the camera on <paramref name="dir"/> (unit, away from <paramref name="focus"/>) at the closest distance
        /// where a circle of <paramref name="radius"/> on the island deck fits the screen: horizontally from the
        /// camera's real aspect, vertically inside the band the HUD panels leave free, with <c>islandFitMargin</c>
        /// to spare. Projects the actual circle (so elevation and perspective are exact) and bisects the distance.
        /// </summary>
        void FitIsland(Vector3 centre, Vector3 focus, Vector3 dir, float radius, out Vector3 position, out Vector3 look)
        {
            Camera cam = Camera.main;
            float fov = cam != null ? cam.fieldOfView : 60f;
            float aspect = cam != null && cam.aspect > 0.01f ? cam.aspect : 9f / 16f;
            float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * aspect;
            float yMin = -1f + 2f * islandHudBottomFraction;
            float yMax = 1f - 2f * islandHudTopFraction;
            float yCentre = (yMin + yMax) * 0.5f;
            float yHalf = Mathf.Max(0.1f, (yMax - yMin) * 0.5f);
            float margin = Mathf.Max(1f, islandFitMargin);

            Vector3 lookShift = Vector3.zero;
            float distance = radius * 2f;
            for (int pass = 0; pass < 3; pass++)
            {
                float lo = radius * 0.5f;
                float hi = radius * 40f;
                for (int it = 0; it < 28; it++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (IslandFits(centre, focus, dir, mid, lookShift, radius, tanH, tanV, yCentre, yHalf, margin, out _))
                    {
                        hi = mid;
                    }
                    else
                    {
                        lo = mid;
                    }
                }

                distance = hi;
                IslandFits(centre, focus, dir, distance, lookShift, radius, tanH, tanV, yCentre, yHalf, margin, out float yMid);

                // Re-centre the circle in the free band by nudging the look point along the camera's up axis.
                Vector3 pos = IslandCameraPosition(centre, focus, dir, distance);
                Vector3 fwd = (focus + lookShift + Vector3.up * 0.3f - pos).normalized;
                Vector3 up = Vector3.Cross(fwd, Vector3.Cross(Vector3.up, fwd).normalized);
                lookShift += up * ((yMid - yCentre) * tanV * Vector3.Distance(pos, centre));
            }

            position = IslandCameraPosition(centre, focus, dir, distance);
            look = focus + lookShift + Vector3.up * 0.3f;
        }

        Vector3 IslandCameraPosition(Vector3 centre, Vector3 focus, Vector3 dir, float distance)
        {
            Vector3 pos = focus + dir * distance;
            pos.y = Mathf.Max(pos.y, centre.y + islandMinHeight);
            return pos;
        }

        bool IslandFits(Vector3 centre, Vector3 focus, Vector3 dir, float distance, Vector3 lookShift, float radius,
            float tanH, float tanV, float yCentre, float yHalf, float margin, out float yMid)
        {
            Vector3 pos = IslandCameraPosition(centre, focus, dir, distance);
            Vector3 fwd = (focus + lookShift + Vector3.up * 0.3f - pos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 up = Vector3.Cross(fwd, right);

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float maxX = 0f;
            const int Samples = 32;
            yMid = 0f;
            for (int i = 0; i < Samples; i++)
            {
                float a = i * Mathf.PI * 2f / Samples;
                Vector3 v = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius - pos;
                float z = Vector3.Dot(v, fwd);
                if (z < 0.05f)
                {
                    return false;
                }

                maxX = Mathf.Max(maxX, Mathf.Abs(Vector3.Dot(v, right) / (z * tanH)));
                float y = Vector3.Dot(v, up) / (z * tanV);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            yMid = (minY + maxY) * 0.5f;
            return maxX * margin <= 1f && Mathf.Max(maxY - yCentre, yCentre - minY) * margin <= yHalf;
        }

        Transform PoseFor(int index)
        {
            if (stageCameraPoses == null || stageCameraPoses.Length == 0)
            {
                return wideCameraPose;
            }

            return stageCameraPoses[Mathf.Clamp(index, 0, stageCameraPoses.Length - 1)];
        }
    }
}
