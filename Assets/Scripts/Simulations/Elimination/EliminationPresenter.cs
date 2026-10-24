using System.Collections.Generic;
using SimulationLobby.Presentation;
using SimulationLobby.Shared;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.Elimination
{
    /// <summary>
    /// Binds the elimination show to its presentation: the caption, the tile board, the overview and heat
    /// cameras, and the sound. Reads the simulation in <c>Update</c>/<c>LateUpdate</c> and never writes it.
    /// </summary>
    /// <remarks>
    /// <b>HUD rule.</b> One caption at a time, at most four words, large bold white text on a dark plate, each held
    /// for at least <see cref="captionMinHold"/> seconds. Live counters (SAFE n / N) change digits without
    /// restarting the hold, but never replace an event caption that is still holding.
    /// <para>
    /// <b>Cameras.</b> The overview camera is on only when no heat rig is (board beats). During a heat intro the
    /// overview stays on for two frames while the new rig's own camera settles, then the rig camera takes over.
    /// Exactly one AudioListener is enabled at any time.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(EliminationSimulation))]
    public sealed class EliminationPresenter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] EliminationBoardHud _board;
        [SerializeField] CaptionHud _caption;
        [SerializeField] TMP_Text _captionText;
        [SerializeField] CanvasGroup _captionPlate;
        [SerializeField] Camera _overviewCamera;
        [SerializeField] Transform _stageRing;
        [SerializeField] Renderer[] _stageMarbles = new Renderer[0];
        [SerializeField] AudioSource _sfx;
        [SerializeField] ConfettiBurst3D _confetti;
        [SerializeField] ReflectionProbe[] _heatProbes = new ReflectionProbe[0];

        [Header("Captions")]
        [Tooltip("Every event caption stays up at least this long (HUD rule: at least 1.5 s).")]
        [Min(1.5f)] public float captionMinHold = 1.6f;

        [Tooltip("A live counter changes no faster than this.")]
        [Min(0.1f)] public float counterInterval = 0.35f;

        [Header("Board beats")]
        [Min(0f)] public float firstStampDelay = 1.0f;
        [Min(0.2f)] public float stampInterval = 0.8f;
        [Min(0f)] public float returnDelay = 2.0f;
        [Min(0f)] public float championDelay = 2.2f;

        [Header("Heat intro fly-in")]
        [Tooltip("Camera height above the track during the fly-in.")]
        [Min(2f)] public float flyHeight = 11f;
        [Min(0f)] public float flySide = 6f;
        [Range(0.2f, 0.95f)] public float flyEndFraction = 0.74f;

        [Header("Last-spot push-in")]
        [Min(1f)] public float pushInDistance = 3.4f;
        [Min(0.5f)] public float pushInHeight = 1.7f;

        static readonly Color Gold = new Color(1f, 0.84f, 0.28f, 1f);
        static readonly Color StageOut = new Color(0.10f, 0.10f, 0.12f, 1f);

        struct Reveal
        {
            public int index;
            public TileState target;
            public float time;
        }

        EliminationSimulation _sim;
        EliminationAudioKit _audio;
        bool _boardReady;

        // Phase tracking.
        ElimPhase _lastPhase = (ElimPhase)(-1);
        int _lastHeat = -2;
        int _phaseFrames;
        readonly List<Reveal> _reveals = new List<Reveal>();
        int _nextReveal;
        bool _championShown;
        bool _gongPlayed;
        bool _confettiPlayed;
        int _thudCount;

        // Caption director.
        string _shown = string.Empty;
        bool _shownIsEvent;
        float _shownSince;

        // The current heat rig, cached on entry.
        GameObject _rig;
        Camera _rigCamera;
        AudioListener _rigListener;
        ChaseCameraRig _rigChase;
        TrackPath _rigPath;
        LeaderboardHud _rigHud;
        Transform _finishLine;
        float _chaseDistance, _chaseHeight;
        bool _flying;
        bool _flyDone;
        Vector3 _flyVelocity, _flyLookVelocity, _flyLook;
        bool _probeDone;

        AudioListener _overviewListener;
        readonly Dictionary<int, int> _stageApplied = new Dictionary<int, int>();
        MaterialPropertyBlock _block;

        public void Bind(EliminationBoardHud board, CaptionHud caption, TMP_Text captionText, CanvasGroup captionPlate,
            Camera overviewCamera, Transform stageRing, Renderer[] stageMarbles, AudioSource sfx,
            ConfettiBurst3D confetti, ReflectionProbe[] heatProbes)
        {
            _board = board;
            _caption = caption;
            _captionText = captionText;
            _captionPlate = captionPlate;
            _overviewCamera = overviewCamera;
            _stageRing = stageRing;
            _stageMarbles = stageMarbles ?? new Renderer[0];
            _sfx = sfx;
            _confetti = confetti;
            _heatProbes = heatProbes ?? new ReflectionProbe[0];
        }

        void Start()
        {
            _sim = GetComponent<EliminationSimulation>();
            _audio = EliminationAudioKit.Create();
            _block = new MaterialPropertyBlock();
            if (_overviewCamera != null)
            {
                _overviewListener = _overviewCamera.GetComponent<AudioListener>();
            }

            TrySetupBoard();
        }

        void TrySetupBoard()
        {
            if (_boardReady || _sim == null || _sim.Roster.Count == 0 || _board == null)
            {
                return;
            }

            var names = new List<string>(_sim.Roster.Count);
            var colors = new List<Color>(_sim.Roster.Count);
            foreach (HeatEntrant entrant in _sim.Roster)
            {
                names.Add(entrant.name);
                colors.Add(entrant.color);
            }

            _board.SetTiles(names, colors);
            _board.SetVisible(true, 0f);
            _boardReady = true;
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (_sim == null)
            {
                return;
            }

            TrySetupBoard();
            if (!_boardReady)
            {
                return;
            }

            ElimPhase phase = _sim.Phase;
            if (phase != _lastPhase || _sim.HeatIndex != _lastHeat)
            {
                EnterPhase(phase);
                _lastPhase = phase;
                _lastHeat = _sim.HeatIndex;
                _phaseFrames = 0;
            }

            _phaseFrames++;

            // The heat's reflection probe is set to render on demand; do it once the rig is on and has had a few frames.
            if (phase == ElimPhase.HeatIntro && !_probeDone && _phaseFrames >= 5)
            {
                _probeDone = true;
                int heatIndex = _sim.HeatIndex;
                if (heatIndex >= 0 && heatIndex < _heatProbes.Length && _heatProbes[heatIndex] != null)
                {
                    _heatProbes[heatIndex].RenderProbe();
                }
            }

            UpdateCameras(phase);
            UpdateBoard(phase);
            UpdateHeatEvents(phase);
            UpdateCaption(phase);
            UpdateStage();
        }

        void LateUpdate()
        {
            if (_sim == null || !_boardReady)
            {
                return;
            }

            if (_sim.Phase == ElimPhase.HeatIntro)
            {
                UpdateFlyIn();
            }

            // The rig's own leaderboard shows a centre banner for the winner ("X WINS"); in a heat that line would
            // sit over the cut and fight the caption, so it is hidden once anyone has finished.
            if (_sim.Phase == ElimPhase.Heat && _rigHud != null && _sim.CurrentHeatSim != null && _sim.CurrentHeatSim.FinishedCount > 0)
            {
                _rigHud.HideBanner();
            }
        }

        // ------------------------------------------------------------------ phase entry

        void EnterPhase(ElimPhase phase)
        {
            switch (phase)
            {
                case ElimPhase.Intro:
                    _board.SetVisible(true, 0.6f);
                    break;

                case ElimPhase.HeatIntro:
                    CacheRig(_sim.HeatIndex);
                    _board.ClearHighlights();
                    _board.SetVisible(false, 0.4f);
                    _flying = false;
                    _flyDone = false;
                    _probeDone = false;
                    _gongPlayed = false;
                    _confettiPlayed = false;
                    Play(_audio.whoosh, 0.7f);
                    break;

                case ElimPhase.Heat:
                    break;

                case ElimPhase.HeatResult:
                    _board.SetVisible(true, 0.4f);
                    BuildReveals();
                    break;

                case ElimPhase.Twist:
                    _board.SetVisible(true, 0.4f);
                    for (int i = 0; i < _sim.Roster.Count; i++)
                    {
                        _board.SetHighlight(i, _sim.GetState(i) == ContenderState.Out);
                    }

                    Play(_audio.riser, 0.55f);
                    break;

                case ElimPhase.Payoff:
                case ElimPhase.Done:
                    _board.SetVisible(true, 0.4f);
                    for (int i = 0; i < _sim.Roster.Count; i++)
                    {
                        TileState wanted = ToTile(_sim.GetState(i));
                        if (wanted == TileState.Champion)
                        {
                            wanted = TileState.Alive;
                        }

                        if (_board.GetState(i) != wanted)
                        {
                            _board.SetState(i, wanted, false);
                        }
                    }

                    _championShown = false;
                    break;
            }
        }

        static TileState ToTile(ContenderState state)
        {
            switch (state)
            {
                case ContenderState.Out: return TileState.Out;
                case ContenderState.Returned: return TileState.Returned;
                case ContenderState.Champion: return TileState.Champion;
                default: return TileState.Alive;
            }
        }

        void CacheRig(int heat)
        {
            _rig = _sim.HeatRig(heat);
            _rigCamera = null;
            _rigListener = null;
            _rigChase = null;
            _rigPath = null;
            _rigHud = null;
            _finishLine = null;
            if (_rig == null)
            {
                return;
            }

            _rigCamera = _rig.GetComponentInChildren<Camera>(true);
            if (_rigCamera != null)
            {
                _rigListener = _rigCamera.GetComponent<AudioListener>();
                _rigChase = _rigCamera.GetComponent<ChaseCameraRig>();
            }

            if (_rigChase != null)
            {
                _chaseDistance = _rigChase.chaseDistance;
                _chaseHeight = _rigChase.chaseHeight;
            }

            _rigPath = _rig.GetComponentInChildren<TrackPath>(true);
            _rigHud = _rig.GetComponentInChildren<LeaderboardHud>(true);
            _finishLine = FindDeep(_rig.transform, "Finish Line");

            // Names only, no clock: the heat is about the cut, not the time.
            if (_rigHud != null)
            {
                Transform timer = _rigHud.transform.Find("Timer");
                if (timer != null)
                {
                    timer.gameObject.SetActive(false);
                }
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ cameras

        void UpdateCameras(ElimPhase phase)
        {
            bool rigPhase = phase == ElimPhase.HeatIntro || phase == ElimPhase.Heat;
            bool rigWarm = phase == ElimPhase.HeatIntro && _phaseFrames < 3;
            bool overview = !rigPhase || rigWarm || _rigCamera == null;

            if (_overviewCamera != null)
            {
                _overviewCamera.enabled = overview;
            }

            if (_overviewListener != null)
            {
                _overviewListener.enabled = overview;
            }

            if (_rigCamera != null && _rig != null && _rig.activeInHierarchy)
            {
                _rigCamera.enabled = !overview;
                if (_rigListener != null)
                {
                    _rigListener.enabled = !overview;
                }
            }
        }

        /// <summary>
        /// The heat intro tour: from the finish gantry back up the track to the pen, high above it, then a glide
        /// into the pen orbit the heat's own presenter set up. Drives the rig camera directly while the chase rig
        /// is switched off.
        /// </summary>
        void UpdateFlyIn()
        {
            if (_rigCamera == null || _rigPath == null || _flyDone || _phaseFrames < 3 || !_rig.activeInHierarchy)
            {
                return;
            }

            float total = _rigPath.TotalLength;
            float duration = Mathf.Max(1f, _sim.PhaseSeconds);
            float u = Mathf.Clamp01(_sim.PhaseElapsedSeconds / duration);
            float v = Mathf.Clamp01(u / flyEndFraction);
            float smooth = v * v * (3f - 2f * v);
            float gate = 4.4f;
            float d = Mathf.Lerp(total * 0.97f, gate + 12f, smooth);

            Vector3 here = _rigPath.PositionAtDistance(d);
            Vector3 behind = _rigPath.PositionAtDistance(Mathf.Max(0f, d - 1.5f));
            Vector3 tangent = here - behind;
            tangent.y = 0f;
            tangent = tangent.sqrMagnitude > 1e-4f ? tangent.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, tangent);
            Vector3 desired = here + Vector3.up * flyHeight + side * flySide;
            Vector3 look = _rigPath.PositionAtDistance(Mathf.Max(0f, d - 9f));
            Transform cam = _rigCamera.transform;

            if (!_flying)
            {
                _flying = true;
                if (_rigChase != null)
                {
                    _rigChase.enabled = false;
                }

                cam.position = desired;
                _flyLook = look;
                _flyVelocity = Vector3.zero;
                _flyLookVelocity = Vector3.zero;
            }

            cam.position = Vector3.SmoothDamp(cam.position, desired, ref _flyVelocity, 0.28f);
            _flyLook = Vector3.SmoothDamp(_flyLook, look, ref _flyLookVelocity, 0.22f);
            Vector3 toLook = _flyLook - cam.position;
            if (toLook.sqrMagnitude > 1e-4f)
            {
                cam.rotation = Quaternion.LookRotation(toLook.normalized, Vector3.up);
            }

            if (u >= flyEndFraction)
            {
                _flyDone = true;
                if (_rigChase != null)
                {
                    _rigChase.enabled = true;
                }
            }
        }

        // ------------------------------------------------------------------ board

        void BuildReveals()
        {
            _reveals.Clear();
            _nextReveal = 0;
            EliminationHeat heat = _sim.CurrentHeat;
            if (heat.rule == HeatRule.LastChance)
            {
                if (_sim.ReturnedIndex >= 0)
                {
                    _reveals.Add(new Reveal { index = _sim.ReturnedIndex, target = TileState.Returned, time = returnDelay });
                }

                return;
            }

            for (int i = 0; i < _sim.OutThisHeatCount; i++)
            {
                _reveals.Add(new Reveal
                {
                    index = _sim.OutThisHeat(i),
                    target = TileState.Out,
                    time = firstStampDelay + i * stampInterval
                });
            }
        }

        void UpdateBoard(ElimPhase phase)
        {
            float elapsed = _sim.PhaseElapsedSeconds;
            if (phase == ElimPhase.HeatResult)
            {
                while (_nextReveal < _reveals.Count && elapsed >= _reveals[_nextReveal].time)
                {
                    Reveal reveal = _reveals[_nextReveal++];
                    _board.SetState(reveal.index, reveal.target);
                    if (reveal.target == TileState.Out)
                    {
                        Play(_audio.thuds[_thudCount++ % _audio.thuds.Length], 0.9f);
                    }
                    else
                    {
                        Play(_audio.returnNotes[0], 0.7f);
                        Play(_audio.returnNotes[1], 0.55f);
                    }
                }
            }
            else if ((phase == ElimPhase.Payoff || phase == ElimPhase.Done) && !_championShown && _sim.ChampionIndex >= 0
                     && (phase == ElimPhase.Done || elapsed >= championDelay))
            {
                _championShown = true;
                _board.SetState(_sim.ChampionIndex, TileState.Champion);
                Color color = _sim.Roster[_sim.ChampionIndex].color;
                if (_stageRing != null && _confetti != null)
                {
                    Vector3 centre = _stageRing.position + Vector3.up * 1.2f;
                    _confetti.Burst(centre + Vector3.left * 2.5f, color, 200);
                    _confetti.Burst(centre + Vector3.right * 2.5f, Gold, 200);
                    _confetti.Burst(centre, color, 160);
                }

                PlayChord();
            }
        }

        // ------------------------------------------------------------------ heat events

        void UpdateHeatEvents(ElimPhase phase)
        {
            if (phase != ElimPhase.Heat)
            {
                return;
            }

            IHeatSimulation heat = _sim.CurrentHeatSim;
            if (heat == null)
            {
                return;
            }

            int safe = _sim.CurrentHeat.EffectiveSafeCount;
            if (!_gongPlayed && heat.FinishedCount >= safe)
            {
                _gongPlayed = true;
                Play(_audio.gong, 0.9f);
            }

            if (_sim.HeatCalled)
            {
                // Slow push-in on the last-spot moment.
                if (_rigChase != null)
                {
                    float p = _sim.PhaseProgress;
                    _rigChase.chaseDistance = Mathf.Lerp(_chaseDistance, pushInDistance, p);
                    _rigChase.chaseHeight = Mathf.Lerp(_chaseHeight, pushInHeight, p);
                }

                if (!_confettiPlayed && _sim.CurrentHeat.rule == HeatRule.Final && _finishLine != null && _confetti != null
                    && _sim.HeatWinnerIndex >= 0)
                {
                    _confettiPlayed = true;
                    Color color = _sim.Roster[_sim.HeatWinnerIndex].color;
                    Vector3 at = _finishLine.position + Vector3.up * 0.8f;
                    _confetti.Burst(at - _finishLine.right * 1.4f, color, 220);
                    _confetti.Burst(at + _finishLine.right * 1.4f, Gold, 220);
                    _confetti.Burst(at, color, 160);
                    PlayChord();
                }
            }
        }

        void PlayChord()
        {
            for (int i = 0; i < _audio.chord.Length; i++)
            {
                Play(_audio.chord[i], 0.55f);
            }
        }

        void Play(AudioClip clip, float volume)
        {
            if (_sfx != null && clip != null)
            {
                _sfx.PlayOneShot(clip, volume);
            }
        }

        // ------------------------------------------------------------------ captions

        void UpdateCaption(ElimPhase phase)
        {
            string desired = ComputeCaption(phase, out bool isEvent, out Color color);
            float now = Time.unscaledTime;

            if (desired != _shown)
            {
                float age = now - _shownSince;
                bool canReplace;
                if (_shown.Length == 0)
                {
                    canReplace = true;
                }
                else if (_shownIsEvent)
                {
                    // An event caption holds its minimum, whatever wants to replace it.
                    canReplace = age >= captionMinHold;
                }
                else
                {
                    // A counter yields at once to an event, and to another counter value once the interval has passed.
                    canReplace = isEvent || age >= counterInterval;
                }

                if (canReplace)
                {
                    Apply(desired, isEvent, color, now);
                }
            }

            if (_captionPlate != null)
            {
                float target = _shown.Length > 0 ? 1f : 0f;
                _captionPlate.alpha = Mathf.MoveTowards(_captionPlate.alpha, target, Time.unscaledDeltaTime * 6f);
            }
        }

        void Apply(string text, bool isEvent, Color color, float now)
        {
            _shown = text;
            _shownIsEvent = isEvent;
            _shownSince = now;
            if (_captionText != null)
            {
                _captionText.color = color;
            }

            if (_caption != null)
            {
                _caption.SetCaption(text, text.Length > 0 && isEvent);
            }
        }

        string ComputeCaption(ElimPhase phase, out bool isEvent, out Color color)
        {
            isEvent = true;
            color = Color.white;
            int heatCount = _sim.HeatCount;
            float elapsed = _sim.PhaseElapsedSeconds;
            float p = _sim.PhaseSeconds > 0f ? Mathf.Clamp01(elapsed / _sim.PhaseSeconds) : 0f;
            EliminationHeat heat = _sim.CurrentHeat;

            switch (phase)
            {
                case ElimPhase.Intro:
                    if (p < 0.24f) return _sim.Question;
                    if (p < 0.46f) return $"{_sim.Roster.Count} MARBLES, {heatCount} RACES";
                    if (p < 0.56f) return "PICK ONE!";
                    if (p < 0.70f) return "LAST ONES OUT";
                    if (p < 0.84f) return "ONE LAST CHANCE";
                    return "1 CHAMPION";

                case ElimPhase.HeatIntro:
                    if (p < 0.34f)
                    {
                        return heat.rule == HeatRule.Final ? "FINAL RACE" : $"RACE {_sim.HeatIndex + 1}";
                    }

                    if (p < 0.67f) return Upper(heat.title);
                    switch (heat.rule)
                    {
                        case HeatRule.CutLast: return $"TOP {heat.EffectiveSafeCount} SAFE";
                        case HeatRule.LastChance: return "WINNER COMES BACK";
                        default: return "WINNER TAKES ALL";
                    }

                case ElimPhase.Heat:
                    if (_sim.HeatCalled)
                    {
                        if (heat.rule == HeatRule.CutLast && _sim.LastSafeIndex >= 0)
                        {
                            return $"LAST SPOT: {_sim.Roster[_sim.LastSafeIndex].name}!";
                        }

                        return _sim.HeatWinnerIndex >= 0 ? $"{_sim.Roster[_sim.HeatWinnerIndex].name} WINS!" : string.Empty;
                    }

                    isEvent = false;
                    if (heat.rule == HeatRule.CutLast)
                    {
                        int safe = heat.EffectiveSafeCount;
                        int taken = _sim.CurrentHeatSim != null ? Mathf.Min(_sim.CurrentHeatSim.FinishedCount, safe) : 0;
                        return $"SAFE {taken} / {safe}";
                    }

                    return heat.rule == HeatRule.LastChance ? "FIRST HOME COMES BACK" : "FIRST HOME WINS";

                case ElimPhase.HeatResult:
                    if (heat.rule == HeatRule.LastChance)
                    {
                        if (elapsed < returnDelay || _sim.ReturnedIndex < 0) return "WHO COMES BACK?";
                        return $"{_sim.Roster[_sim.ReturnedIndex].name} IS BACK!";
                    }

                    if (p >= 0.55f && heat.rule == HeatRule.CutLast)
                    {
                        return $"{_sim.AliveCount} MARBLES LEFT";
                    }

                    return $"{_sim.OutThisHeatCount} OUT";

                case ElimPhase.Twist:
                    return p < 0.47f ? "THE OUT MARBLES..." : "GET ONE LAST CHANCE!";

                case ElimPhase.Payoff:
                    if (_sim.ChampionIndex >= 0 && elapsed >= championDelay && p < 0.6f)
                    {
                        color = Gold;
                        return $"CHAMPION: {_sim.Roster[_sim.ChampionIndex].name}!";
                    }

                    if (p < 0.6f) return "THE CHAMPION...";
                    return "DID YOURS WIN?";

                default:
                    return "DID YOURS WIN?";
            }
        }

        static string Upper(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.ToUpperInvariant();
        }

        // ------------------------------------------------------------------ stage

        /// <summary>The decorative ring of marbles behind the board: bright when in, dark and small when out, big and gold for the champion.</summary>
        void UpdateStage()
        {
            if (_stageRing == null || _stageMarbles.Length == 0)
            {
                return;
            }

            float now = Time.time;
            _stageRing.Rotate(0f, 6f * Time.deltaTime, 0f, Space.Self);
            for (int i = 0; i < _stageMarbles.Length && i < _board.TileCount; i++)
            {
                Renderer marble = _stageMarbles[i];
                if (marble == null)
                {
                    continue;
                }

                TileState state = _board.GetState(i);
                Transform t = marble.transform;
                float baseScale = 1f;
                if (state == TileState.Out)
                {
                    baseScale = 0.6f;
                }
                else if (state == TileState.Champion)
                {
                    baseScale = 1.9f;
                }

                float scale = Mathf.Lerp(t.localScale.x, baseScale, 1f - Mathf.Exp(-6f * Time.deltaTime));
                t.localScale = Vector3.one * scale;
                Vector3 local = t.localPosition;
                local.y = 0.5f + Mathf.Sin(now * 1.6f + i * 0.9f) * 0.12f + (state == TileState.Champion ? 0.9f : 0f);
                t.localPosition = local;

                int applied = (int)state;
                if (!_stageApplied.TryGetValue(i, out int last) || last != applied)
                {
                    _stageApplied[i] = applied;
                    Color color = state == TileState.Out ? StageOut : state == TileState.Champion ? Gold : _sim.Roster[i].color;
                    marble.GetPropertyBlock(_block);
                    _block.SetColor("_BaseColor", color);
                    _block.SetColor("_Color", color);
                    marble.SetPropertyBlock(_block);
                }
            }
        }

        void OnDestroy()
        {
            if (_overviewCamera != null)
            {
                _overviewCamera.enabled = true;
            }
        }
    }
}
