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
        int[] _boostShown;   // factor currently written on each team's Multiply chips (1 = own labels)

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

        bool _holdCleared;
        bool _winnerShown;
        float _captionClearAt = -1f;
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
                names[i] = team.name;
            }

            _shares = new float[teamCount];
            _flipSeen = new bool[teamCount];

            _gridView.Build(_config.gridWidth, _config.gridHeight, _config.cellSize, _config.tileHeight, tileGap,
                _teamColors, new Color(0.07f, 0.07f, 0.09f, 1f), _tileMaterial);

            _shareBar.Configure(teamCount, _hudColors, names);

            _faceLabels = new string[_config.spinFaces.Length];
            for (int i = 0; i < _faceLabels.Length; i++)
            {
                _faceLabels[i] = _config.spinFaces[i].Label;
            }

            BuildLabels(teamCount);
            BuildShotViews();

            var fx = new GameObject("FX");
            fx.transform.SetParent(transform, false);
            _blastFx = fx.AddComponent<BlastFx>();
            _lineFx = fx.AddComponent<LineBeamFx>();

            _audio.Build();
            _drum.SetFaces(_faceLabels);
            _drum.SetIdle(0);
            _drum.SetAccent(drumIdleAccent);
            _drum.FacePassed += OnFacePassed;
            _drum.Landed += OnReelLanded;

            _caption.SetCaption(question);
            _caption.SetSubline(rules);
            _caption.SetFooter(string.Empty);
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

        string ResultText(SpinFace face)
        {
            switch (face.kind)
            {
                case SpinFaceKind.Bomb: return "BOMB ×" + (face.value > 0 ? face.value : _config.bombShots) + "!";
                case SpinFaceKind.PlusLine: return "+ LINE!";
                case SpinFaceKind.SelfDestruct: return "SELF DESTRUCT!";
                case SpinFaceKind.Boost: return "×" + face.value + " slots for " + Mathf.RoundToInt(_config.boostSeconds) + "s!";
            }

            SlotEffect effect = face.effect;
            switch (effect.kind)
            {
                case SlotEffectKind.Multiply: return effect.Label + " to the bank!";
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
            UpdateHud();
            UpdatePegAudio();
            UpdateSlotPunch();
            UpdateLaneGateVisuals();
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

        void UpdateCannons()
        {
            GridFrame frame = _simulation.Frame;
            for (int t = 0; t < _simulation.Teams.Count; t++)
            {
                TerritoryTeam team = _simulation.Teams[t];
                Transform pivot = _simulation.Rigs[t].cannonPivot;
                if (pivot == null || team.Eliminated)
                {
                    continue;
                }

                float radians = team.AimDegrees * Mathf.Deg2Rad;
                Vector3 direction = frame.right * Mathf.Cos(radians) + frame.forward * Mathf.Sin(radians);
                if (direction.sqrMagnitude > 1e-6f)
                {
                    pivot.rotation = Quaternion.LookRotation(direction, Vector3.up);
                }
            }
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

        void ShowCaption(string text, float seconds)
        {
            _caption.SetCaption(text);
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
                    ShowCaption($"{Tag(ev.team)} ×{ev.slot} FOR {Mathf.RoundToInt(_config.boostSeconds)}s!", captionSeconds);
                    break;

                case TerritoryEventKind.BoostEnded:
                    break;   // chips revert in UpdateSlotPunch, which reads the simulation each frame

                case TerritoryEventKind.Eliminated:
                    ShowCaption($"{Tag(ev.team)} IS OUT", captionSeconds + 1.5f);
                    _shareBar.SetEliminated(ev.team, true);
                    _cameraRig.Focus(BaseWorld(ev.team), eliminationZoom, 3f);
                    _cameraRig.Shake(0.35f, 0.7f);
                    _audio.PlayElimination();
                    Transform pivot = _simulation.Rigs[ev.team].cannonPivot;
                    if (pivot != null)
                    {
                        pivot.gameObject.SetActive(false);
                    }

                    break;

                case TerritoryEventKind.Escalated:
                    ShowCaption("SPEED UP!", captionSeconds);
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
                if (boost != _boostShown[t])
                {
                    // Boost started, changed or ended: rewrite the Multiply chips (own label again when it ends).
                    _boostShown[t] = boost;
                    for (int s = 0; s < _slotText[t].Length; s++)
                    {
                        if (_slotText[t][s] != null && _config.boardSlots[s].kind == SlotEffectKind.Multiply)
                        {
                            _slotText[t][s].text = boost > 1 ? "×" + boost : _config.boardSlots[s].Label;
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
