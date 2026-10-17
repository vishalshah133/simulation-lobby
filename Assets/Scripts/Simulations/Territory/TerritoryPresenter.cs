using System.Collections.Generic;
using SimulationLobby.Presentation;
using SimulationLobby.Shared;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.Territory
{
    /// <summary>
    /// Thin binder from the territory war to the format-agnostic presentation pieces: tile view, share bar,
    /// reel, captions, world labels, camera and audio. Reads simulation state in Update/LateUpdate and never
    /// writes it; the only things it moves are its own views (shot spheres, cannon pivots, labels).
    /// </summary>
    [RequireComponent(typeof(TerritorySimulation))]
    public sealed class TerritoryPresenter : MonoBehaviour
    {
        [Header("Wiring (filled by the scene builder)")]
        [SerializeField] TileGridView _gridView;
        [SerializeField] ShareBarHud _shareBar;
        [SerializeField] DrumReelView _drum;
        [SerializeField] Transform _drumHeaderAnchor;
        [SerializeField] Transform _drumResultAnchor;
        [SerializeField] CaptionHud _caption;
        [SerializeField] WorldLabelLayer _labels;
        [SerializeField] FocusCameraRig _cameraRig;
        [SerializeField] TerritoryAudioKit _audio;
        [SerializeField] GameObject _shotTemplate;
        [SerializeField] Transform _shotRoot;
        [SerializeField] Material _tileMaterial;

        [Header("Text")]
        public string question = "WHICH COLOUR TAKES THE BOARD?";
        public string rules = "×2 doubles it  ·  R fires it all  ·  SPIN spins the drum  ·  lose your cannon, you're out";
        [Min(0.5f)] public float captionSeconds = 3.2f;

        [Tooltip("Releases smaller than this get no caption (a release of 1 is not news).")]
        [Min(1)] public long captionReleaseMin = 16;

        [Header("Tiles")]
        [Min(0f)] public float tileGap = 0.03f;   // tile height comes from config.tileHeight
        [Min(1)] public int pulsesPerFrame = 40;

        [Header("Shots")]
        [Tooltip("Extra height added to the shot spheres' underside, on top of config.tileHeight (the tile top).")]
        public float shotHeightOffset = 0f;

        [Tooltip("Sphere diameter of a shot, in cells (0.8 = a bit under one tile). One count = one ball, so it never varies with health.")]
        [Min(0.1f)] public float shotDiameterCells = 0.8f;

        [Tooltip("Bomb shots are dark spheres of this diameter, in cells.")]
        [Min(0.1f)] public float bombDiameterCells = 1.2f;

        public Color bombColor = new Color(0.102f, 0.102f, 0.125f, 1f);

        [Tooltip("While a Boost is active, the team's Multiply chips pulse at this rate (Hz).")]
        [Min(0.1f)] public float boostPulseHz = 2f;

        [Tooltip("Peak extra scale of a pulsing boosted chip (0.12 = up to 12% bigger).")]
        [Range(0f, 0.5f)] public float boostPulseAmount = 0.12f;

        [Tooltip("Alpha of a team's Multiply and Release chips while its board is locked (the team is firing).")]
        [Range(0f, 1f)] public float lockedChipAlpha = 0.35f;

        [Tooltip("Bomb shots in flight pulse between the bomb colour and the team colour at this rate (Hz).")]
        [Min(0f)] public float bombPulseHz = 4f;

        [Header("Power actions")]
        [Tooltip("Bomb and self-destruct reveal wave speed, in cells per second.")]
        [Min(1f)] public float blastRevealSpeed = 18f;

        [Tooltip("+ LINE reveal wave speed along the line, in cells per second.")]
        [Min(1f)] public float lineRevealSpeed = 30f;

        public Color selfDestructColor = new Color(0.541f, 0.063f, 0.125f, 1f);

        [Header("Slot chips")]
        [Range(0f, 1.5f)] public float slotPunch = 0.6f;
        [Min(0.05f)] public float slotPunchSeconds = 0.35f;

        public Color eliminatedCounterColor = new Color(0.55f, 0.55f, 0.6f, 1f);

        [Header("Drum labels")]
        [Min(10f)] public float drumHeaderFontSize = 84f;
        [Min(10f)] public float drumResultFontSize = 84f;
        public Color drumOutlineColor = new Color(0.078f, 0.082f, 0.102f, 1f);
        [Range(0f, 1f)] public float drumOutlineWidth = 0.3f;
        public Color drumIdleAccent = new Color(0.133f, 0.137f, 0.169f, 1f);

        [Tooltip("Seconds the header and result stay up after the drum lands.")]
        [Min(0f)] public float drumHoldSeconds = 2.5f;

        [Header("Camera")]
        [Tooltip("A release of at least this many shots-worth of ammo pushes the camera in.")]
        [Min(1)] public long bigReleaseThreshold = 256;
        [Range(0f, 1f)] public float releaseZoom = 0.45f;
        [Range(0f, 1f)] public float eliminationZoom = 0.75f;
        [Range(0f, 1f)] public float blastZoom = 0.5f;
        [Min(0f)] public float bombShake = 0.1f;
        [Min(0f)] public float powerShake = 0.3f;

        [Header("Audio")]
        [Min(1)] public int maxPegClacksPerFrame = 3;

        [Header("Cannon down (BOOM)")]
        [Tooltip("Barrel droop while the cannon is down, in degrees.")]
        [Range(0f, 80f)] public float downDroopDegrees = 35f;

        [Tooltip("Seconds the droop, squash and tint take to settle.")]
        [Min(0.05f)] public float downEaseSeconds = 0.25f;

        [Tooltip("Cap squash while down (0.2 = 20% shorter).")]
        [Range(0f, 0.6f)] public float downSquash = 0.2f;

        [Tooltip("The flag mast tips over this far, outward, while the cannon is down.")]
        [Range(0f, 90f)] public float mastTipDegrees = 70f;

        [Min(0.5f)] public float cannonBlastRadius = 1.2f;
        [Min(0.1f)] public float downRingRadius = 0.85f;
        [Min(0.02f)] public float downRingThickness = 0.14f;
        public Color downRingTrack = new Color(0.05f, 0.05f, 0.07f, 0.85f);

        [Tooltip("Peak extra scale of the countdown number when it changes.")]
        [Range(0f, 1f)] public float downNumberPunch = 0.4f;

        [Min(0.05f)] public float repairPopSeconds = 0.35f;
        [Range(1f, 2f)] public float repairPopScale = 1.25f;

        [Header("Rain")]
        [Tooltip("A drop starts falling this long before it lands (clamped to config.rainLeadSeconds).")]
        [Min(0.05f)] public float rainFallSeconds = 0.6f;

        [Tooltip("Height drops fall from, in metres above the tile top.")]
        [Min(1f)] public float rainFallHeight = 6f;

        [Tooltip("Cloud height above the tiles, in metres.")]
        [Min(0f)] public float cloudHeight = 4f;

        [Range(0.1f, 1f)] public float cloudRadiusFraction = 0.45f;

        [Header("Identity")]
        [Range(0f, 1f)] public float eliminatedNameAlpha = 0.35f;

        [Tooltip("Fraction of the mast height the flag drops when its team is out (half-mast).")]
        [Range(0f, 1f)] public float halfMastFraction = 0.45f;

        TerritorySimulation _simulation;

        // Lane gate visuals (the simulation owns the collider; this only drives the renderer and its scale).
        const float GatePopSeconds = 0.15f;
        const float GatePopFrom = 0.85f;
        Vector3[] _gateBaseScale;
        bool[] _gateWasOpen;
        float[] _gatePopTimer;
        TerritoryConfig _config;

        Color[] _teamColors;
        Color[] _hudColors;
        string[] _faceLabels;
        float[] _shares;

        int _changeIndex;
        int _eventIndex;
        bool[] _flipSeen;

        TMP_Text[] _counterText;
        TMP_Text[] _firingText;
        TMP_Text[][] _slotText;
        Vector3[][] _slotBaseScale;
        float[][] _slotPunchLeft;
        Color[][] _slotBaseColor;   // chip colour as built, restored exactly when the board unlocks
        bool[] _chipsDimmed;
        bool[][] _slotScaleDirty;
        int[] _boostShown;   // boost * 1000 + multiplier bonus last written on each team's Multiply chips (1000 = own labels)

        int _headerLabel = -1;
        int _resultLabel = -1;
        float _drumClearAt = -1f;

        Transform[] _shotViews;
        int[] _shotTeam;
        bool[] _shotShown;
        bool[] _shotBombView;
        MaterialPropertyBlock _block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        float _shotDiameter;

        BlastFx _blastFx;
        LineBeamFx _lineFx;
        readonly HashSet<int> _held = new HashSet<int>();
        readonly List<int> _holdCells = new List<int>();
        readonly List<float> _holdDelays = new List<float>();

        // terr-002 per-team views (null / unused when the rig has no cannon root, flag or name: terr-001).
        TerritoryTheme _theme;
        RainFx _rainFx;
        CloudFx _cloudFx;
        SmokePlumeFx _smokeFx;
        bool[] _visDown;                 // BOOM visuals currently applied (set by events, cleared on repair or elimination)
        float[] _downK;                  // 0 up .. 1 fully drooped, eased
        float[] _popLeft;                // repair pop timer
        float[] _tipK, _halfK, _flagApplied;
        Vector3[] _pivotBaseScale;
        Quaternion[] _flagBaseRot;
        Vector3[] _flagBasePos;          // WavingFlag child, local to the mast
        Transform[] _flagChild;
        Vector3[] _tipAxis;
        Color[][] _origTint;
        RadialTimerView[] _ring;
        int[] _smokeHandle, _cloudHandle;
        int[] _downNumber;
        float[] _numberPunchLeft;
        Vector3[] _downTextBase;
        bool[] _nameDimmed;
        bool[] _pivotDirty;
        Vector3 _gridCentre;
        float _gridDiagonal;
        bool[] _eliminatedSeen;
        bool[] _dropLaunched = new bool[64];
        int _rainScan;

        bool _holdCleared;
        bool _winnerShown;
        float _captionClearAt = -1f;
        float _showdownCaptionAt = -1f;
        int _lastCountdown = -1;
        int _lastClock = -1;

        public void Bind(TileGridView gridView, ShareBarHud shareBar, DrumReelView drum, Transform drumHeaderAnchor,
            Transform drumResultAnchor, CaptionHud caption, WorldLabelLayer labels, FocusCameraRig cameraRig,
            TerritoryAudioKit audio, GameObject shotTemplate, Transform shotRoot, Material tileMaterial)
        {
            _gridView = gridView;
            _shareBar = shareBar;
            _drum = drum;
            _drumHeaderAnchor = drumHeaderAnchor;
            _drumResultAnchor = drumResultAnchor;
            _caption = caption;
            _labels = labels;
            _cameraRig = cameraRig;
            _audio = audio;
            _shotTemplate = shotTemplate;
            _shotRoot = shotRoot;
            _tileMaterial = tileMaterial;
        }

        // ------------------------------------------------------------------ setup

        void Start()
        {
            _simulation = GetComponent<TerritorySimulation>();
            _config = _simulation.Config;
            if (_config == null || _simulation.Grid == null)
            {
                Debug.LogError("[terr] The simulation is not initialised. Check the config, rigs and the SimulationRunner.", this);
                enabled = false;
                return;
            }

            // The theme's values win over the presenter's own colour and text fields. The classic theme's
            // defaults equal the fields' defaults, so terr-001 is unchanged.
            _theme = _config.Theme;
            bombColor = _theme.bombColor;
            selfDestructColor = _theme.selfDestructColor;
            eliminatedCounterColor = _theme.eliminatedCounter;
            drumIdleAccent = _theme.drumIdleAccent;
            drumOutlineColor = _theme.drumOutline;
            question = _theme.question;
            rules = _theme.rules;

            int teamCount = _simulation.Teams.Count;
            _gateBaseScale = new Vector3[teamCount];
            _gateWasOpen = new bool[teamCount];
            _gatePopTimer = new float[teamCount];
            for (int i = 0; i < teamCount && i < _simulation.Rigs.Count; i++)
            {
                Renderer gateRenderer = _simulation.Rigs[i].laneGateRenderer;
                if (gateRenderer != null)
                {
                    _gateBaseScale[i] = gateRenderer.transform.localScale;
                }
            }

            _teamColors = new Color[teamCount];
            _hudColors = new Color[teamCount];
            var names = new string[teamCount];
            for (int i = 0; i < teamCount; i++)
            {
                TerritoryTeam team = _simulation.Teams[i];
                _teamColors[i] = team.color;
                _hudColors[i] = Color.Lerp(team.color, Color.white, 0.22f);
                names[i] = _theme.shareBarShowNames && i < _config.teams.Length ? _config.teams[i].Short : team.name;
            }

            _shares = new float[teamCount];
            _flipSeen = new bool[teamCount];

            _gridView.Build(_config.gridWidth, _config.gridHeight, _config.cellSize, _config.tileHeight, tileGap,
                _teamColors, _theme.tileNeutral, _tileMaterial);

            _shareBar.showNames = _theme.shareBarShowNames;
            _shareBar.Configure(teamCount, _hudColors, names);

            _faceLabels = new string[_config.spinFaces.Length];
            RelabelFaces();

            BuildLabels(teamCount);
            BuildShotViews();

            var fx = new GameObject("FX");
            fx.transform.SetParent(transform, false);
            _blastFx = fx.AddComponent<BlastFx>();
            _lineFx = fx.AddComponent<LineBeamFx>();
            _rainFx = fx.AddComponent<RainFx>();
            _cloudFx = fx.AddComponent<CloudFx>();
            _smokeFx = fx.AddComponent<SmokePlumeFx>();
            BuildTeamViews(teamCount);

            _audio.Build();
            _drum.SetFaces(_faceLabels);
            _drum.SetIdle(0);
            _drum.SetAccent(drumIdleAccent);
            _drum.FacePassed += OnFacePassed;
            _drum.Landed += OnReelLanded;

            _caption.SetCaption(question);
            // The prompt rides under the rules during the setup beat; only a scene with its own theme shows it.
            bool showPrompt = _config.theme != null && !string.IsNullOrEmpty(_theme.prompt);
            _caption.SetSubline(showPrompt ? rules + "\n" + _theme.prompt : rules);
            _caption.SetFooter(string.Empty);
        }

        /// <summary>Caches the per-team cannon, flag and name state. Every rig reference may be null (terr-001).</summary>
        void BuildTeamViews(int teamCount)
        {
            _visDown = new bool[teamCount];
            _downK = new float[teamCount];
            _popLeft = new float[teamCount];
            _tipK = new float[teamCount];
            _halfK = new float[teamCount];
            _flagApplied = new float[teamCount];
            _pivotBaseScale = new Vector3[teamCount];
            _flagBaseRot = new Quaternion[teamCount];
            _flagBasePos = new Vector3[teamCount];
            _flagChild = new Transform[teamCount];
            _tipAxis = new Vector3[teamCount];
            _origTint = new Color[teamCount][];
            _ring = new RadialTimerView[teamCount];
            _smokeHandle = new int[teamCount];
            _cloudHandle = new int[teamCount];
            _downNumber = new int[teamCount];
            _numberPunchLeft = new float[teamCount];
            _downTextBase = new Vector3[teamCount];
            _nameDimmed = new bool[teamCount];
            _pivotDirty = new bool[teamCount];

            GridFrame frame = _simulation.Frame;
            OwnershipGrid grid = _simulation.Grid;
            Vector3 cornerA = frame.CellCenterWorld(0, 0, 0f);
            Vector3 cornerB = frame.CellCenterWorld(grid.Width - 1, grid.Height - 1, 0f);
            Vector3 centre = 0.5f * (cornerA + cornerB);
            _gridCentre = centre + Vector3.up * (_config.tileHeight + cloudHeight);
            _gridDiagonal = Vector3.Distance(cornerA, cornerB);

            for (int t = 0; t < teamCount; t++)
            {
                _smokeHandle[t] = -1;
                _cloudHandle[t] = -1;
                _downNumber[t] = -1;
                TerritoryTeamRig rig = _simulation.Rigs[t];

                if (rig.cannonPivot != null)
                {
                    _pivotBaseScale[t] = rig.cannonPivot.localScale;
                }

                Renderer[] tint = rig.cannonTintRenderers;
                if (tint != null)
                {
                    _origTint[t] = new Color[tint.Length];
                    for (int r = 0; r < tint.Length; r++)
                    {
                        Material m = tint[r] != null ? tint[r].sharedMaterial : null;
                        _origTint[t][r] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                    }
                }

                if (rig.downText != null)
                {
                    _downTextBase[t] = rig.downText.transform.localScale;
                    rig.downText.text = string.Empty;
                }

                if (rig.flagRoot != null)
                {
                    _flagBaseRot[t] = rig.flagRoot.rotation;
                    WavingFlag waving = rig.flagRoot.GetComponentInChildren<WavingFlag>(true);
                    if (waving != null)
                    {
                        _flagChild[t] = waving.transform;
                        _flagBasePos[t] = waving.transform.localPosition;
                    }

                    // Tip outward: away from the board centre, horizontal.
                    Vector3 outward = rig.flagRoot.position - centre;
                    outward.y = 0f;
                    if (outward.sqrMagnitude < 1e-6f)
                    {
                        outward = Vector3.right;
                    }

                    _tipAxis[t] = Vector3.Cross(Vector3.up, outward.normalized);
                }
            }
        }

        void BuildLabels(int teamCount)
        {
            _counterText = new TMP_Text[teamCount];
            _firingText = new TMP_Text[teamCount];
            _slotText = new TMP_Text[teamCount][];
            _slotBaseScale = new Vector3[teamCount][];
            _slotPunchLeft = new float[teamCount][];
            _slotBaseColor = new Color[teamCount][];
            _chipsDimmed = new bool[teamCount];
            _boostShown = new int[teamCount];
            _slotScaleDirty = new bool[teamCount][];
            int slots = _config.boardSlots.Length;

            for (int t = 0; t < teamCount; t++)
            {
                TerritoryTeamRig rig = _simulation.Rigs[t];
                _counterText[t] = rig.counterText;
                _firingText[t] = rig.firingText;
                _slotText[t] = new TMP_Text[slots];
                _slotBaseScale[t] = new Vector3[slots];
                _slotPunchLeft[t] = new float[slots];
                _slotBaseColor[t] = new Color[slots];
                _slotScaleDirty[t] = new bool[slots];

                bool missing = rig.counterText == null || rig.firingText == null;
                for (int s = 0; s < slots; s++)
                {
                    TMP_Text text = rig.slotTexts != null && s < rig.slotTexts.Length ? rig.slotTexts[s] : null;
                    if (text == null)
                    {
                        missing = true;
                        continue;
                    }

                    _slotText[t][s] = text;
                    _slotBaseScale[t][s] = text.transform.localScale;
                    _slotBaseColor[t][s] = text.color;
                    text.text = _config.boardSlots[s].Label;
                }

                if (missing)
                {
                    Debug.LogWarning($"[terr] Team {t} rig is missing a counter, FIRING or slot text decal; those are skipped.", rig);
                }
            }

            _headerLabel = _labels.Add(string.Empty, drumHeaderFontSize, Color.white, FontStyles.Bold | FontStyles.Italic);
            _labels.SetPivot(_headerLabel, 1f);
            _labels.SetOutline(_headerLabel, drumOutlineColor, drumOutlineWidth);
            _resultLabel = _labels.Add(string.Empty, drumResultFontSize, Color.white, FontStyles.Bold | FontStyles.Italic);
            _labels.SetPivot(_resultLabel, 0f);
            _labels.SetOutline(_resultLabel, drumOutlineColor, drumOutlineWidth);
        }

        void BuildShotViews()
        {
            int capacity = _simulation.Shots.Count;
            _shotViews = new Transform[capacity];
            _shotTeam = new int[capacity];
            _shotShown = new bool[capacity];
            _block = new MaterialPropertyBlock();
            _shotBombView = new bool[capacity];

            _shotDiameter = shotDiameterCells * _config.cellSize;

            Transform root = _shotRoot != null ? _shotRoot : transform;
            for (int i = 0; i < capacity; i++)
            {
                GameObject view = Instantiate(_shotTemplate, root);
                view.name = $"Shot_{i:000}";
                view.SetActive(false);
                _shotViews[i] = view.transform;
                _shotTeam[i] = -1;
            }
        }

        void OnDestroy()
        {
            if (_drum != null)
            {
                _drum.FacePassed -= OnFacePassed;
                _drum.Landed -= OnReelLanded;
            }
        }

        void OnFacePassed()
        {
            _audio.PlayReelClick();
        }

        void OnReelLanded(int result)
        {
            _audio.PlayReelLand();
            if (result >= 0 && result < _config.spinFaces.Length)
            {
                _labels.SetText(_resultLabel, ResultText(_config.spinFaces[result]));
            }

            _drumClearAt = Time.unscaledTime + drumHoldSeconds;
        }

        /// <summary>Drum face labels at the current multiplier bonus (×2 reads ×3 once the ramp has stepped once).</summary>
        void RelabelFaces()
        {
            for (int i = 0; i < _faceLabels.Length; i++)
            {
                _faceLabels[i] = FaceLabel(_config.spinFaces[i]);
            }
        }

        string FaceLabel(SpinFace face)
        {
            int bonus = _simulation.MultiplierBonus;
            if (bonus > 0)
            {
                if (face.kind == SpinFaceKind.Boost)
                {
                    return "×" + (face.value + bonus);
                }

                if (face.kind == SpinFaceKind.Economy && face.effect.kind == SlotEffectKind.Multiply)
                {
                    return "×" + (face.effect.value + bonus);
                }
            }

            return face.Label;
        }

        string ResultText(SpinFace face)
        {
            switch (face.kind)
            {
                case SpinFaceKind.Bomb: return "BOMB ×" + (face.value > 0 ? face.value : _config.bombShots) + "!";
                case SpinFaceKind.PlusLine: return "+ LINE!";
                case SpinFaceKind.SelfDestruct: return "SELF DESTRUCT!";
                case SpinFaceKind.CannonStrike: return "CANNON STRIKE!";
                case SpinFaceKind.Rain: return "RAIN!";
                case SpinFaceKind.Boost: return FaceLabel(face) + " slots for " + Mathf.RoundToInt(_config.boostSeconds) + "s!";
            }

            SlotEffect effect = face.effect;
            switch (effect.kind)
            {
                case SlotEffectKind.Multiply: return FaceLabel(face) + " to the bank!";
                case SlotEffectKind.Add: return effect.Label + " ammo!";
                case SlotEffectKind.Release: return "RELEASE NOW!";
                default: return effect.Label;
            }
        }

        void ClearDrumLabels()
        {
            _drumClearAt = -1f;
            _labels.SetText(_headerLabel, string.Empty);
            _labels.SetText(_resultLabel, string.Empty);
            if (!_drum.IsSpinning)
            {
                _drum.SetAccent(drumIdleAccent);
            }
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (_simulation == null || !enabled)
            {
                return;
            }

            // Events first: they register reveal holds, which must exist before SetOwners uploads the new owners.
            _held.Clear();
            ConsumeEvents();
            _gridView.SetOwners(_simulation.Grid.Owners, _simulation.Grid.Version);
            ConsumeChanges();
            UpdateShots();
            UpdateCannons();
            UpdateRain();
            UpdateHud();
            UpdatePegAudio();
            UpdateSlotPunch();
            UpdateLaneGateVisuals();
            UpdateShowdownCaption();
        }

        void UpdateLaneGateVisuals()
        {
            IReadOnlyList<TerritoryTeamRig> rigs = _simulation.Rigs;
            for (int t = 0; t < rigs.Count && t < _gateWasOpen.Length; t++)
            {
                Renderer view = rigs[t].laneGateRenderer;
                if (view == null)
                {
                    continue;
                }

                Transform xf = view.transform;
                bool open = _simulation.IsLaneGateOpen(t);
                if (open)
                {
                    view.enabled = false;
                    xf.localScale = _gateBaseScale[t];
                    _gatePopTimer[t] = 0f;
                }
                else
                {
                    if (_gateWasOpen[t])
                    {
                        _gatePopTimer[t] = GatePopSeconds;   // just closed: pop in
                    }

                    view.enabled = true;
                    if (_gatePopTimer[t] > 0f)
                    {
                        _gatePopTimer[t] = Mathf.Max(0f, _gatePopTimer[t] - Time.deltaTime);
                        float k = 1f - _gatePopTimer[t] / GatePopSeconds;
                        xf.localScale = _gateBaseScale[t] * Mathf.Lerp(GatePopFrom, 1f, k);
                        if (_gatePopTimer[t] <= 0f)
                        {
                            xf.localScale = _gateBaseScale[t];
                        }
                    }
                }

                _gateWasOpen[t] = open;
            }
        }

        void LateUpdate()
        {
            if (_simulation == null || !enabled)
            {
                return;
            }

            // Labels follow anchors after the camera has moved this frame.
            if (_drumHeaderAnchor != null)
            {
                _labels.Place(_headerLabel, _drumHeaderAnchor.position);
            }

            if (_drumResultAnchor != null)
            {
                _labels.Place(_resultLabel, _drumResultAnchor.position);
            }
        }

        void ConsumeChanges()
        {
            IReadOnlyList<CellChange> changes = _simulation.Grid.Changes;
            int count = changes.Count;
            if (count <= _changeIndex)
            {
                return;
            }

            System.Array.Clear(_flipSeen, 0, _flipSeen.Length);

            // Over budget: only the newest few get a pulse; the tiles themselves are always right.
            // Held cells are pulsed by HoldAndReveal when their delay elapses, so they neither pulse here nor spend budget.
            int pulsed = 0;
            for (int i = count - 1; i >= _changeIndex; i--)
            {
                CellChange change = changes[i];
                if (change.newOwner < 0 || change.newOwner >= _teamColors.Length)
                {
                    continue;
                }

                _flipSeen[change.newOwner] = true;
                if (pulsed < pulsesPerFrame && !_held.Contains(change.index))
                {
                    _gridView.Pulse(change.index, _teamColors[change.newOwner]);
                    pulsed++;
                }
            }

            _changeIndex = count;
            for (int t = 0; t < _flipSeen.Length; t++)
            {
                if (_flipSeen[t])
                {
                    _audio.PlayFlip(t);
                }
            }
        }

        void UpdateShots()
        {
            IReadOnlyList<GridBall> shots = _simulation.Shots;
            GridFrame frame = _simulation.Frame;
            for (int i = 0; i < shots.Count; i++)
            {
                GridBall ball = shots[i];
                bool alive = ball.alive;
                if (!alive)
                {
                    if (_shotShown[i])
                    {
                        _shotViews[i].gameObject.SetActive(false);
                        _shotShown[i] = false;
                    }

                    continue;
                }

                Transform view = _shotViews[i];
                if (!_shotShown[i])
                {
                    view.gameObject.SetActive(true);
                    _shotShown[i] = true;
                }

                // Bombs are dark; the tint is reset whenever a slot flips between bomb and normal shot.
                bool bomb = _simulation.IsBombShot(i);
                if (_shotTeam[i] != ball.owner || _shotBombView[i] != bomb)
                {
                    _shotTeam[i] = ball.owner;
                    _shotBombView[i] = bomb;
                    Tint(view.gameObject, bomb ? bombColor : _teamColors[ball.owner]);
                }

                if (bomb)
                {
                    // Presentation-only pulse so bombs read as armed.
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * bombPulseHz * Mathf.PI * 2f);
                    Tint(view.gameObject, Color.Lerp(bombColor, _teamColors[ball.owner], pulse));
                }

                float size = bomb ? bombDiameterCells * _config.cellSize : _shotDiameter;
                view.position = frame.ToWorld(ball.position, _config.tileHeight + shotHeightOffset + size * 0.5f);
                view.localScale = new Vector3(size, size, size);
            }
        }

        void Tint(GameObject view, Color color)
        {
            Renderer renderer = view.GetComponentInChildren<Renderer>(true);
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>Showdown: a survivor's base plate eases out to the simulation's showdown radius (the built disc is the normal plate).</summary>
        void UpdatePlate(TerritoryTeam team, TerritoryTeamRig rig, float dt)
        {
            Transform plate = rig.basePlate;
            if (plate == null)
            {
                return;
            }

            float grown = 1f;
            if (_simulation.IsShowdown && !team.Eliminated)
            {
                float showdown = _config.showdownPlateRadiusCells;
                grown = _config.basePlateRadiusCells > 0f
                    ? Mathf.Max(1f, showdown / _config.basePlateRadiusCells)
                    : Mathf.Max(1f, showdown * 2f * _config.cellSize / 1.4f);   // the classic disc is 1.4 m
            }

            float s = Mathf.MoveTowards(plate.localScale.x, grown, dt * 1.5f);
            plate.localScale = new Vector3(s, 1f, s);
        }

        void UpdateCannons()
        {
            GridFrame frame = _simulation.Frame;
            float dt = Time.unscaledDeltaTime;
            for (int t = 0; t < _simulation.Teams.Count; t++)
            {
                TerritoryTeam team = _simulation.Teams[t];
                TerritoryTeamRig rig = _simulation.Rigs[t];
                UpdateDownReadout(t, rig, dt);
                UpdateFlag(t, team, rig, dt);
                UpdatePlate(team, rig, dt);

                Transform pivot = rig.cannonPivot;
                if (pivot == null || team.Eliminated)
                {
                    continue;
                }

                // BOOM: the barrel droops and the cap squashes, eased; a repair pops the whole pivot.
                _downK[t] = Mathf.MoveTowards(_downK[t], _visDown[t] ? 1f : 0f, dt / downEaseSeconds);
                float down = Smooth(_downK[t]);

                float radians = team.AimDegrees * Mathf.Deg2Rad;
                Vector3 direction = frame.right * Mathf.Cos(radians) + frame.forward * Mathf.Sin(radians);
                if (direction.sqrMagnitude > 1e-6f)
                {
                    Quaternion aim = Quaternion.LookRotation(direction, Vector3.up);
                    pivot.rotation = down > 0f ? aim * Quaternion.Euler(downDroopDegrees * down, 0f, 0f) : aim;
                }

                float pop = 1f;
                if (_popLeft[t] > 0f)
                {
                    _popLeft[t] = Mathf.Max(0f, _popLeft[t] - dt);
                    float inverse = _popLeft[t] / repairPopSeconds;
                    pop = 1f + (repairPopScale - 1f) * inverse * inverse;
                }

                if (down > 0f || pop != 1f || _pivotDirty[t])
                {
                    Vector3 scale = _pivotBaseScale[t] * pop;
                    scale.y *= 1f - downSquash * down;
                    pivot.localScale = scale;
                    _pivotDirty[t] = down > 0f || _popLeft[t] > 0f;
                    if (!_pivotDirty[t])
                    {
                        pivot.localScale = _pivotBaseScale[t];   // exact base back
                    }
                }
            }
        }

        static float Smooth(float k)
        {
            return k * k * (3f - 2f * k);
        }

        /// <summary>The ring drains on the down fraction and the number counts the seconds left, with a punch per change.</summary>
        void UpdateDownReadout(int t, TerritoryTeamRig rig, float dt)
        {
            if (!_visDown[t])
            {
                return;
            }

            if (_ring[t] != null)
            {
                _ring[t].SetFraction(_simulation.CannonDownFraction(t));
            }

            TMP_Text text = rig.downText;
            if (text == null)
            {
                return;
            }

            int seconds = Mathf.Max(1, Mathf.CeilToInt(_simulation.CannonDownSecondsLeft(t)));
            if (seconds != _downNumber[t])
            {
                _downNumber[t] = seconds;
                text.text = seconds.ToString();
                _numberPunchLeft[t] = 0.25f;
            }

            if (_numberPunchLeft[t] > 0f)
            {
                _numberPunchLeft[t] = Mathf.Max(0f, _numberPunchLeft[t] - dt);
                float k = _numberPunchLeft[t] / 0.25f;
                text.transform.localScale = _downTextBase[t] * (1f + downNumberPunch * k * k);
            }
        }

        /// <summary>The mast tips outward while the cannon is down; the flag drops to half-mast when the team is out.</summary>
        void UpdateFlag(int t, TerritoryTeam team, TerritoryTeamRig rig, float dt)
        {
            Transform mast = rig.flagRoot;
            if (mast == null)
            {
                return;
            }

            _tipK[t] = Mathf.MoveTowards(_tipK[t], _visDown[t] ? 1f : 0f, dt / 0.5f);
            _halfK[t] = Mathf.MoveTowards(_halfK[t], team.Eliminated ? 1f : 0f, dt);
            float key = _tipK[t] + _halfK[t] * 3f;
            if (key == _flagApplied[t])
            {
                return;   // unchanged: leave the flag alone (and the waving flag undisturbed)
            }

            _flagApplied[t] = key;
            mast.rotation = Quaternion.AngleAxis(mastTipDegrees * Smooth(_tipK[t]), _tipAxis[t]) * _flagBaseRot[t];
            if (_flagChild[t] != null)
            {
                Vector3 p = _flagBasePos[t];
                p.y -= _flagBasePos[t].y * halfMastFraction * Smooth(_halfK[t]);
                _flagChild[t].localPosition = p;
            }
        }

        // ------------------------------------------------------------------ BOOM and repair

        static string Possessive(string name)
        {
            return name.Length > 0 && char.ToUpperInvariant(name[name.Length - 1]) == 'S' ? "'" : "'S";
        }

        Vector3 CannonWorld(int team)
        {
            Transform root = _simulation.Rigs[team].cannonRoot;
            return root != null ? root.position : _simulation.Frame.CellCenterWorld(
                _simulation.Teams[team].baseCell.x, _simulation.Teams[team].baseCell.y, _config.tileHeight);
        }

        void OnCannonDestroyed(TerritoryEvent ev)
        {
            int t = ev.team;
            if (t < 0 || t >= _visDown.Length || _simulation.Teams[t].Eliminated)
            {
                return;
            }

            string owner = _simulation.Teams[t].name;
            if (ev.other == t || ev.other < 0 || ev.other >= _visDown.Length)
            {
                ShowCaption($"{Tag(t)}{Possessive(owner)} CANNON BLOWS UP!", captionSeconds);
            }
            else
            {
                ShowCaption($"{Tag(ev.other)} BLOWS UP {Tag(t)}{Possessive(owner)} CANNON!", captionSeconds);
            }

            Vector3 pos = CannonWorld(t);
            _blastFx.Play(pos + Vector3.up * 0.3f, cannonBlastRadius, _theme.cannonBlastColor);
            _blastFx.Play(pos + Vector3.up * 0.2f, cannonBlastRadius * 0.6f, new Color(0.1f, 0.09f, 0.08f, 1f), 0.5f);
            _cameraRig.Focus(pos, blastZoom, 1.5f);
            _cameraRig.Shake(powerShake, 0.4f);
            _audio.PlayCannonDown();

            // A second strike on a cannon that is already down just restarts the visuals.
            EndSmoke(t);
            _visDown[t] = true;
            _popLeft[t] = 0f;
            ApplyTint(t, true);
            if (_smokeFx != null)
            {
                _smokeHandle[t] = _smokeFx.Begin(pos + Vector3.up * 0.3f, 1f);
            }

            if (_ring[t] == null)
            {
                var ringObject = new GameObject("DownTimer_" + t);
                ringObject.transform.SetParent(transform, false);
                Vector2Int cell = _simulation.Teams[t].baseCell;
                ringObject.transform.position = _simulation.Frame.CellCenterWorld(cell.x, cell.y, _config.tileHeight + 0.03f);
                _ring[t] = ringObject.AddComponent<RadialTimerView>();
                _ring[t].Build(downRingRadius, downRingThickness, _teamColors[t], downRingTrack);
            }

            _ring[t].SetFraction(1f);
            _ring[t].SetVisible(true);
        }

        void OnCannonRepaired(int t)
        {
            if (t < 0 || t >= _visDown.Length)
            {
                return;
            }

            ClearDown(t);
            _popLeft[t] = repairPopSeconds;
            _pivotDirty[t] = true;
            _blastFx.Play(CannonWorld(t) + Vector3.up * 0.3f, cannonBlastRadius * 0.55f, Color.white, 0.5f);
            ShowCaption($"{Tag(t)}{Possessive(_simulation.Teams[t].name)} CANNON IS BACK!", captionSeconds);
            _audio.PlayCannonRepair();
        }

        /// <summary>Puts every BOOM visual back to up: tint, smoke, ring and countdown. Safe to call when not down.</summary>
        void ClearDown(int t)
        {
            if (!_visDown[t])
            {
                return;
            }

            _visDown[t] = false;
            ApplyTint(t, false);
            EndSmoke(t);
            if (_ring[t] != null)
            {
                _ring[t].SetVisible(false);
            }

            TMP_Text text = _simulation.Rigs[t].downText;
            if (text != null)
            {
                text.text = string.Empty;
                text.transform.localScale = _downTextBase[t];
            }

            _downNumber[t] = -1;
            _numberPunchLeft[t] = 0f;
        }

        void EndSmoke(int t)
        {
            if (_smokeHandle[t] >= 0 && _smokeFx != null)
            {
                _smokeFx.End(_smokeHandle[t]);
            }

            _smokeHandle[t] = -1;
        }

        void ApplyTint(int t, bool down)
        {
            Renderer[] renderers = _simulation.Rigs[t].cannonTintRenderers;
            if (renderers == null)
            {
                return;
            }

            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] == null)
                {
                    continue;
                }

                renderers[r].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, down ? _theme.cannonDownTint : _origTint[t][r]);
                renderers[r].SetPropertyBlock(_block);
            }
        }

        // ------------------------------------------------------------------ RAIN

        void OnRainStarted(TerritoryEvent ev)
        {
            int t = ev.team;
            if (t < 0 || t >= _cloudHandle.Length)
            {
                return;
            }

            if (ev.slot <= 0)
            {
                ShowCaption($"{Tag(t)}{Possessive(_simulation.Teams[t].name)} RAIN FIZZLES", captionSeconds * 0.6f);
                return;
            }

            ShowCaption($"{Tag(t)} MAKES IT RAIN!", captionSeconds);
            StopCloud(t);   // a second rain from the same team replaces its cloud
            if (_cloudFx != null)
            {
                _cloudHandle[t] = _cloudFx.Show(_gridCentre, _gridDiagonal * cloudRadiusFraction, _teamColors[t]);
            }

            _audio.PlayRainStart();
        }

        void StopCloud(int t)
        {
            if (t >= 0 && t < _cloudHandle.Length && _cloudHandle[t] >= 0 && _cloudFx != null)
            {
                _cloudFx.Hide(_cloudHandle[t]);
            }

            if (t >= 0 && t < _cloudHandle.Length)
            {
                _cloudHandle[t] = -1;
            }
        }

        /// <summary>Starts each scheduled drop's fall so it lands on the tick the simulation paints.</summary>
        void UpdateRain()
        {
            IReadOnlyList<RainDrop> drops = _simulation.RainDrops;
            int count = drops.Count;
            if (count <= _rainScan)
            {
                return;
            }

            if (_dropLaunched.Length < count)
            {
                System.Array.Resize(ref _dropLaunched, Mathf.Max(count, _dropLaunched.Length * 2));
            }

            float fall = Mathf.Min(rainFallSeconds, _config.rainLeadSeconds);
            int tick = _simulation.CurrentTick;
            float tickSeconds = _simulation.TickSeconds;
            for (int i = _rainScan; i < count; i++)
            {
                if (_dropLaunched[i])
                {
                    continue;
                }

                RainDrop drop = drops[i];
                float remaining = (drop.landTick - tick) * tickSeconds;
                if (remaining > fall)
                {
                    continue;
                }

                _dropLaunched[i] = true;
                if (!_simulation.Teams[drop.team].Eliminated)
                {
                    LaunchDrop(i, drop, Mathf.Max(remaining, 0.05f));
                }
            }

            while (_rainScan < count && _dropLaunched[_rainScan])
            {
                _rainScan++;
            }
        }

        void LaunchDrop(int index, RainDrop drop, float seconds)
        {
            GridFrame frame = _simulation.Frame;
            _simulation.Grid.Coords(drop.cell, out int x, out int y);
            Vector3 to = frame.CellCenterWorld(x, y, _config.tileHeight);
            float angle = index * 2.39996f;   // golden angle: a small deterministic sideways spread
            Vector3 side = (frame.right * Mathf.Cos(angle) + frame.forward * Mathf.Sin(angle)) * (0.5f * _config.cellSize);
            Vector3 from = to + Vector3.up * rainFallHeight + side;
            _rainFx.Play(from, to, seconds, _teamColors[drop.team], (_config.rainSplashRadius + 0.5f) * _config.cellSize);
        }

        // ------------------------------------------------------------------ events

        void ConsumeEvents()
        {
            IReadOnlyList<TerritoryEvent> events = _simulation.Events;
            while (_eventIndex < events.Count)
            {
                Handle(events[_eventIndex]);
                _eventIndex++;
            }
        }

        string Tag(int team)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(_hudColors[team]) + ">" + _simulation.Teams[team].name + "</color>";
        }

        string CannonsFasterLine()
        {
            return $"cannons fire ×{_simulation.CannonSpeedup:0.##} faster";
        }

        /// <summary>The delayed FINAL SHOWDOWN caption (see ShowdownStarted).</summary>
        void UpdateShowdownCaption()
        {
            if (_showdownCaptionAt < 0f || Time.unscaledTime < _showdownCaptionAt || _winnerShown)
            {
                return;
            }

            _showdownCaptionAt = -1f;
            ShowCaption("FINAL SHOWDOWN!", captionSeconds + 1.5f, "bigger targets, SPIN lanes stay open");
            _cameraRig.Shake(powerShake, 0.5f);
            _audio.PlayLine();
        }

        void ShowCaption(string text, float seconds, string subline = "")
        {
            _caption.SetCaption(text);
            _caption.SetSubline(subline);
            _captionClearAt = seconds > 0f ? Time.unscaledTime + seconds : -1f;
        }

        Vector3 BaseWorld(int team)
        {
            Vector2Int cell = _simulation.Teams[team].baseCell;
            return _simulation.Frame.CellCenterWorld(cell.x, cell.y, 0f);
        }

        /// <summary>Camera focus and shake on a grid cell index (a negative index, meaning no blast, is ignored).</summary>
        void FocusCell(int cellIndex, float shake)
        {
            if (cellIndex < 0)
            {
                return;
            }

            _simulation.Grid.Coords(cellIndex, out int x, out int y);
            _cameraRig.Focus(_simulation.Frame.CellCenterWorld(x, y, 0f), blastZoom, 1.5f);
            _cameraRig.Shake(shake, 0.35f);
        }

        void Handle(TerritoryEvent ev)
        {
            switch (ev.kind)
            {
                case TerritoryEventKind.SlotHit:
                    if (ev.other == 2)
                    {
                        break;   // ignored, team was firing: no punch, no chime
                    }

                    if (ev.slot >= 0 && ev.slot < _slotPunchLeft[ev.team].Length)
                    {
                        _slotPunchLeft[ev.team][ev.slot] = slotPunchSeconds;
                    }

                    if (_config.boardSlots[ev.slot].kind == SlotEffectKind.Bonus)
                    {
                        _audio.PlayGold(); // the gold chime now marks the SPIN slot
                    }
                    else
                    {
                        _audio.PlaySlot(_config.boardSlots[ev.slot].kind, ev.after);
                    }

                    break;

                case TerritoryEventKind.ShotFired:
                    _audio.PlayShot(ev.team, (int)ev.before);
                    break;

                case TerritoryEventKind.Released:
                    _audio.PlayRelease(ev.after);
                    if (ev.other == 1)
                    {
                        ShowCaption($"{Tag(ev.team)} MAXED OUT: FIRES {CountFormat.Compact(ev.after)}!", captionSeconds);
                    }
                    else if (ev.after >= captionReleaseMin)
                    {
                        ShowCaption($"{Tag(ev.team)} RELEASES {CountFormat.Compact(ev.after)}!", captionSeconds);
                    }

                    if (ev.after >= bigReleaseThreshold)
                    {
                        _cameraRig.Focus(BaseWorld(ev.team), releaseZoom, 2.5f);
                    }

                    break;

                case TerritoryEventKind.SpinStarted:
                    _drum.SetAccent(_teamColors[ev.team]);
                    _drum.Spin(ev.slot, _config.spinSeconds);
                    _drumClearAt = -1f;
                    _labels.SetText(_headerLabel, Tag(ev.team) + " is Spinning");
                    _labels.SetText(_resultLabel, string.Empty);
                    break;

                case TerritoryEventKind.SpinResolved:
                    if (ev.other == 0 && ev.slot >= 0 && ev.slot < _faceLabels.Length)
                    {
                        SpinFace face = _config.spinFaces[ev.slot];
                        switch (face.kind)
                        {
                            case SpinFaceKind.Bomb:
                                ShowCaption($"{Tag(ev.team)} BOMBS!", captionSeconds);
                                break;
                            case SpinFaceKind.PlusLine:
                                ShowCaption($"{Tag(ev.team)} + LINE!", captionSeconds);
                                break;
                            case SpinFaceKind.SelfDestruct:
                                ShowCaption($"{Tag(ev.team)} SELF DESTRUCTS!", captionSeconds);
                                break;
                            case SpinFaceKind.Boost:
                                break;   // BoostStarted carries the caption (it reports the factor actually in force)
                            case SpinFaceKind.CannonStrike:
                            case SpinFaceKind.Rain:
                                break;   // CannonDestroyed / RainStarted carry the caption
                            default:
                                if (face.effect.kind != SlotEffectKind.Release)
                                {
                                    ShowCaption($"{Tag(ev.team)} SPINS {_faceLabels[ev.slot]}!", captionSeconds);
                                }

                                break;
                        }
                    }

                    break;

                case TerritoryEventKind.BombExploded:
                    _audio.PlayBoom();
                    FocusCell(ev.slot, bombShake);
                    AnimateBlast(ev, false);
                    break;

                case TerritoryEventKind.LinePainted:
                    _audio.PlayLine();
                    FocusCell(ev.slot, powerShake);
                    AnimateLine(ev);
                    break;

                case TerritoryEventKind.SelfDestructed:
                    _audio.PlayBoom();
                    FocusCell(ev.slot, powerShake);
                    AnimateBlast(ev, true);
                    break;

                case TerritoryEventKind.BoostStarted:
                    _audio.PlayLine();   // the rising sweep
                    ShowCaption($"{Tag(ev.team)} ×{ev.slot + _simulation.MultiplierBonus} FOR {Mathf.RoundToInt(_config.boostSeconds)}s!", captionSeconds);
                    break;

                case TerritoryEventKind.BoostEnded:
                    break;   // chips revert in UpdateSlotPunch, which reads the simulation each frame

                case TerritoryEventKind.Eliminated:
                    ShowCaption($"{Tag(ev.team)} IS OUT", captionSeconds + 1.5f,
                        _config.cannonSpeedupPerElimination > 0f ? CannonsFasterLine() : string.Empty);
                    _shareBar.SetEliminated(ev.team, true);
                    _cameraRig.Focus(BaseWorld(ev.team), eliminationZoom, 3f);
                    _cameraRig.Shake(0.35f, 0.7f);
                    _audio.PlayElimination();
                    ClearDown(ev.team);
                    StopCloud(ev.team);
                    Transform pivot = _simulation.Rigs[ev.team].cannonPivot;
                    if (pivot != null)
                    {
                        pivot.gameObject.SetActive(false);
                    }

                    break;

                case TerritoryEventKind.CannonDestroyed:
                    OnCannonDestroyed(ev);
                    break;

                case TerritoryEventKind.CannonRepaired:
                    OnCannonRepaired(ev.team);
                    break;

                case TerritoryEventKind.RainStarted:
                    OnRainStarted(ev);
                    break;

                case TerritoryEventKind.RainDropLanded:
                    _audio.PlayRainDrop();   // the tile pulse comes from ConsumeChanges
                    break;

                case TerritoryEventKind.RainEnded:
                    StopCloud(ev.team);
                    break;

                case TerritoryEventKind.Escalated:
                    ShowCaption("SPEED UP!", captionSeconds);
                    break;

                case TerritoryEventKind.MultipliersRaised:
                    RelabelFaces();
                    _drum.SetFaces(_faceLabels);
                    ShowCaption($"MULTIPLIERS UP! ×2 IS NOW ×{2 + ev.slot}", captionSeconds + 1f);
                    _cameraRig.Shake(bombShake, 0.3f);
                    _audio.PlayLine();   // the rising sweep
                    break;

                case TerritoryEventKind.KnockoutReward:
                {
                    // Rides under the "IS OUT" caption from the same tick.
                    string loot = $"{Tag(ev.team)} TAKES THE BANK +{CountFormat.Compact(ev.after - ev.before)}" +
                                  (ev.other == 1 ? " + A FREE SPIN" : string.Empty);
                    if (_config.cannonSpeedupPerElimination > 0f)
                    {
                        loot += "\n" + CannonsFasterLine();
                    }

                    _caption.SetSubline(loot);
                    _audio.PlayLine();
                    break;
                }

                case TerritoryEventKind.ShowdownStarted:
                    // Usually the same tick as the third elimination: let "IS OUT" read first.
                    _showdownCaptionAt = Time.unscaledTime + captionSeconds + 1.5f;
                    break;

                case TerritoryEventKind.HeavyShotsStarted:
                    ShowCaption("HEAVY SHOTS!", captionSeconds + 1f, "every hit paints wider");
                    _cameraRig.Shake(bombShake, 0.3f);
                    _audio.PlayLine();
                    break;

                case TerritoryEventKind.Winner:
                    _winnerShown = true;
                    ClearDrumLabels();
                    ShowCaption($"{Tag(ev.team)} WINS!", 0f);
                    _caption.SetSubline(ev.other == 1 ? "decided on territory" : string.Empty);
                    _cameraRig.Focus(BaseWorld(ev.team), releaseZoom, 6f);
                    _audio.PlayWin();
                    break;
            }
        }

        // ------------------------------------------------------------------ power-action animation

        void AnimateBlast(TerritoryEvent ev, bool selfDestruct)
        {
            if (ev.slot < 0)
            {
                return;
            }

            OwnershipGrid grid = _simulation.Grid;
            grid.Coords(ev.slot, out int cx, out int cy);
            int radius = selfDestruct ? _config.selfDestructRadius : _config.bombRadius;
            float maxDistance = radius + 1f;
            int wantedOwner = selfDestruct ? OwnershipGrid.Unowned : ev.team;

            _holdCells.Clear();
            _holdDelays.Clear();
            IReadOnlyList<CellChange> changes = grid.Changes;
            for (int i = changes.Count - 1; i >= 0; i--)
            {
                CellChange change = changes[i];
                if (change.tick < ev.tick)
                {
                    break;
                }

                if (change.tick != ev.tick || change.newOwner != wantedOwner)
                {
                    continue;
                }

                grid.Coords(change.index, out int x, out int y);
                float distance = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (distance > maxDistance)
                {
                    continue;
                }

                _holdCells.Add(change.index);
                _holdDelays.Add(distance / blastRevealSpeed);
                _held.Add(change.index);
            }

            if (_holdCells.Count > 0)
            {
                _gridView.HoldAndReveal(_holdCells, _holdDelays, true);
            }

            Vector3 centre = _simulation.Frame.CellCenterWorld(cx, cy, _config.tileHeight);
            Color color = selfDestruct ? selfDestructColor : _teamColors[ev.team];
            _blastFx.Play(centre, (radius + 0.5f) * _config.cellSize, color);
        }

        void AnimateLine(TerritoryEvent ev)
        {
            OwnershipGrid grid = _simulation.Grid;
            grid.Coords(ev.slot, out int tx, out int ty);
            int thickness = Mathf.Max(1, _config.lineThickness);
            int rowStart = ty - thickness / 2;
            int colStart = tx - thickness / 2;

            _holdCells.Clear();
            _holdDelays.Clear();
            IReadOnlyList<CellChange> changes = grid.Changes;
            for (int i = changes.Count - 1; i >= 0; i--)
            {
                CellChange change = changes[i];
                if (change.tick < ev.tick)
                {
                    break;
                }

                if (change.tick != ev.tick || change.newOwner != ev.team)
                {
                    continue;
                }

                grid.Coords(change.index, out int x, out int y);
                bool inRow = y >= rowStart && y < rowStart + thickness;
                bool inColumn = x >= colStart && x < colStart + thickness;
                if (!inRow && !inColumn)
                {
                    continue;
                }

                float distance = float.MaxValue;
                if (inRow)
                {
                    distance = Mathf.Abs(x - tx);
                }

                if (inColumn)
                {
                    distance = Mathf.Min(distance, Mathf.Abs(y - ty));
                }

                _holdCells.Add(change.index);
                _holdDelays.Add(distance / lineRevealSpeed);
                _held.Add(change.index);
            }

            if (_holdCells.Count > 0)
            {
                _gridView.HoldAndReveal(_holdCells, _holdDelays, true);
            }

            GridFrame frame = _simulation.Frame;
            float height = _config.tileHeight;
            Vector3 origin = frame.CellCenterWorld(tx, ty, height);
            float width = thickness * _config.cellSize;
            Color color = _teamColors[ev.team];
            PlayBeam(origin, frame.CellCenterWorld(0, ty, height), tx, width, color);
            PlayBeam(origin, frame.CellCenterWorld(grid.Width - 1, ty, height), grid.Width - 1 - tx, width, color);
            PlayBeam(origin, frame.CellCenterWorld(tx, 0, height), ty, width, color);
            PlayBeam(origin, frame.CellCenterWorld(tx, grid.Height - 1, height), grid.Height - 1 - ty, width, color);
        }

        void PlayBeam(Vector3 from, Vector3 to, int cells, float width, Color color)
        {
            if (cells <= 0)
            {
                return;
            }

            _lineFx.Play(from, to, width, color, cells / lineRevealSpeed);
        }

        // ------------------------------------------------------------------ HUD

        void UpdateHud()
        {
            IReadOnlyList<TerritoryTeam> teams = _simulation.Teams;
            for (int t = 0; t < teams.Count; t++)
            {
                TerritoryTeam team = teams[t];
                _shares[t] = team.Share;

                if (team.Eliminated)
                {
                    TMP_Text nameText = _simulation.Rigs[t].nameText;
                    if (nameText != null && !_nameDimmed[t])
                    {
                        _nameDimmed[t] = true;
                        Color nameColor = nameText.color;
                        nameColor.a = eliminatedNameAlpha;
                        nameText.color = nameColor;
                    }

                    if (_counterText[t] != null)
                    {
                        _counterText[t].text = "OUT";
                        _counterText[t].color = eliminatedCounterColor;
                    }

                    if (_firingText[t] != null)
                    {
                        _firingText[t].text = string.Empty;
                    }
                }
                else
                {
                    if (_counterText[t] != null)
                    {
                        _counterText[t].text = CountFormat.Compact(team.Bank);
                    }

                    if (_firingText[t] != null)
                    {
                        string firing = team.Queued > 0 ? "FIRING " + CountFormat.Compact(team.Queued) : string.Empty;
                        int boost = _simulation.BoostFactor(t);
                        if (boost > 1)
                        {
                            string timer = "×" + boost + " · " + Mathf.CeilToInt(_simulation.BoostSecondsLeft(t)) + "s";
                            firing = firing.Length > 0 ? firing + "\n" + timer : timer;
                        }

                        _firingText[t].text = firing;
                    }
                }
            }

            if (_drumClearAt > 0f && Time.unscaledTime >= _drumClearAt)
            {
                ClearDrumLabels();
            }

            _shareBar.SetShares(_shares);

            if (_simulation.Phase == TerritoryPhase.Hold)
            {
                int seconds = Mathf.CeilToInt(_simulation.SecondsUntilStart);
                if (seconds != _lastCountdown)
                {
                    _lastCountdown = seconds;
                    _caption.SetFooter($"STARTS IN {seconds}");
                }
            }
            else
            {
                if (!_holdCleared)
                {
                    _holdCleared = true;
                    _caption.SetCaption(string.Empty, false);
                    _caption.SetSubline(string.Empty);
                }

                int clock = Mathf.FloorToInt(_simulation.ElapsedSeconds);
                if (clock != _lastClock)
                {
                    _lastClock = clock;
                    _caption.SetFooter($"{clock / 60}:{clock % 60:00}");
                }

                if (!_winnerShown && _captionClearAt > 0f && Time.unscaledTime >= _captionClearAt)
                {
                    _captionClearAt = -1f;
                    _caption.SetCaption(string.Empty, false);
                    _caption.SetSubline(string.Empty);
                }
            }
        }

        void UpdateSlotPunch()
        {
            float dt = Time.unscaledDeltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * boostPulseHz * Mathf.PI * 2f);
            for (int t = 0; t < _slotPunchLeft.Length; t++)
            {
                int boost = _simulation.BoostFactor(t);
                int bonus = _simulation.MultiplierBonus;
                int shownKey = boost * 1000 + bonus;   // 1 = no boost, no ramp: the chips' own labels
                if (shownKey != _boostShown[t])
                {
                    // Boost or multiplier ramp changed: rewrite the Multiply chips with the factor actually in force.
                    _boostShown[t] = shownKey;
                    for (int s = 0; s < _slotText[t].Length; s++)
                    {
                        SlotEffect slot = _config.boardSlots[s];
                        if (_slotText[t][s] != null && slot.kind == SlotEffectKind.Multiply)
                        {
                            _slotText[t][s].text = boost > 1 || bonus > 0
                                ? "×" + ((boost > 1 ? boost : slot.value) + bonus)
                                : slot.Label;
                        }
                    }
                }

                bool locked = _simulation.IsBoardLocked(t);
                if (locked != _chipsDimmed[t])
                {
                    // Board locked/unlocked: dim Multiply and Release chips, restoring the built colour exactly.
                    _chipsDimmed[t] = locked;
                    for (int s = 0; s < _slotText[t].Length; s++)
                    {
                        SlotEffectKind kind = _config.boardSlots[s].kind;
                        if (_slotText[t][s] != null && (kind == SlotEffectKind.Multiply || kind == SlotEffectKind.Release))
                        {
                            Color c = _slotBaseColor[t][s];
                            if (locked)
                            {
                                c.a = lockedChipAlpha;
                            }

                            _slotText[t][s].color = c;
                        }
                    }
                }

                for (int s = 0; s < _slotPunchLeft[t].Length; s++)
                {
                    TMP_Text text = _slotText[t][s];
                    if (text == null)
                    {
                        continue;
                    }

                    bool pulsing = !locked && boost > 1 && _config.boardSlots[s].kind == SlotEffectKind.Multiply;
                    float left = _slotPunchLeft[t][s];
                    if (left <= 0f && !pulsing)
                    {
                        if (_slotScaleDirty[t][s])
                        {
                            _slotScaleDirty[t][s] = false;
                            text.transform.localScale = _slotBaseScale[t][s];   // exact base back
                        }

                        continue;
                    }

                    float k = 0f;
                    if (left > 0f)
                    {
                        left = Mathf.Max(0f, left - dt);
                        _slotPunchLeft[t][s] = left;
                        k = left / slotPunchSeconds;
                    }

                    float scale = (1f + slotPunch * k * k) * (pulsing ? 1f + boostPulseAmount * pulse : 1f);
                    text.transform.localScale = _slotBaseScale[t][s] * scale;
                    _slotScaleDirty[t][s] = true;
                }
            }
        }

        void UpdatePegAudio()
        {
            int clacks = 0;
            for (int t = 0; t < _simulation.Teams.Count && clacks < maxPegClacksPerFrame; t++)
            {
                int capacity = _simulation.BoardBallCapacity(t);
                for (int i = 0; i < capacity; i++)
                {
                    if (_simulation.GetBoardBall(t, i) == null)
                    {
                        continue;
                    }

                    CollisionDetector3D detector = _simulation.GetBoardBallDetector(t, i);
                    if (detector == null)
                    {
                        continue;
                    }

                    if (detector.ConsumeCollisionCount() > 0 && clacks < maxPegClacksPerFrame)
                    {
                        clacks++;
                        _audio.PlayPeg(detector.LastImpactSpeed);
                    }
                }
            }
        }
    }
}
