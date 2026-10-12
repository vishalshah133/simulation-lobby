using System.Collections.Generic;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.Cricket
{
    /// <summary>
    /// Turns <see cref="CricketSimulation"/> state into the video: the score and target up top, labelled
    /// gates that flash, the ball swelling as it gathers distance, a wagon wheel that fills with shot
    /// lines, and a crowd that reacts to every ball.
    /// </summary>
    /// <remarks>
    /// Reads the simulation in <c>LateUpdate</c> and never writes it. Flashes, line draw-ins and
    /// delayed sounds run on <c>Time.time</c>, which is fine: nothing they drive feeds back into the run.
    /// </remarks>
    [RequireComponent(typeof(CricketSimulation))]
    public sealed class CricketPresenter : MonoBehaviour
    {
        [SerializeField] CricketSimulation _simulation;
        [SerializeField] CaptionHud _hud;
        [SerializeField] WorldLabelLayer _labels;
        [SerializeField] SpriteRenderer[] _gateBars;
        [SerializeField] RadialHitChart _chart;
        [SerializeField] Transform _ballVisual;
        [SerializeField] SpriteRenderer _ballRenderer;
        [SerializeField] ShatterBurst2D _burst;
        [SerializeField] CricketAudioKit _audio;
        [SerializeField] SpriteRenderer _readoutPill;
        [SerializeField] ConfettiBurst2D _confetti;

        [Header("Feel")]
        [Range(0.05f, 1f)] public float gateFlashSeconds = 0.35f;
        [Range(0f, 1f)] public float gateBarAlpha = 0.6f;
        [Tooltip("How fast the ball swells with its distance: scale = 1 + this * log2(distance / base).")]
        [Range(0f, 1f)] public float swellPerDoubling = 0.28f;
        [Range(1f, 4f)] public float maxSwell = 2.2f;

        [Header("Crowd")]
        [Range(0f, 1f)] public float cheerVolume = 0.7f;
        [Range(0f, 1f)] public float batVolume = 0.8f;
        [Range(0f, 1f)] public float gateVolume = 0.3f;

        static readonly Color DotColor = new Color(0.62f, 0.66f, 0.74f, 1f);
        static readonly Color SingleColor = new Color(0.78f, 0.9f, 1f, 1f);
        static readonly Color TwoColor = new Color(0.3f, 0.85f, 1f, 1f);
        static readonly Color ThreeColor = new Color(0.4f, 1f, 0.55f, 1f);
        static readonly Color FourColor = new Color(1f, 0.82f, 0.2f, 1f);
        static readonly Color SixColor = new Color(1f, 0.3f, 0.65f, 1f);
        static readonly Color OutColor = new Color(1f, 0.25f, 0.3f, 1f);

        struct Pending
        {
            public float time;
            public AudioClip clip;
            public float volume;
        }

        AudioSource _sfx;
        AudioClip[] _scale;
        AudioClip _crack;
        AudioClip _stumps;
        AudioClip _groan;
        AudioClip _applause;
        AudioClip _applauseSoft;
        AudioClip _cheerMild;
        AudioClip _cheerBig;
        AudioClip _cheerRoar;
        AudioClip _cheerFinal;
        readonly List<Pending> _pending = new List<Pending>();

        float[] _flashStart;
        Color[] _gateColors;
        int[] _labelIndex;
        int _readout = -1;
        float _readoutPunch = -10f;
        string _readoutLast = string.Empty;
        Color _pillRest;
        bool _pillRestCaptured;
        bool _celebrated;
        int _runId = -1;
        int _deliveryId = -1;
        int _multiplyIndex;
        int _deliveryIndex;
        int _innings;
        float _outFlashStart = -10f;
        bool _finalPlayed;
        string _footerText = string.Empty;
        string _lastScore = string.Empty;

        void Reset()
        {
            _simulation = GetComponent<CricketSimulation>();
        }

        void Start()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;

            // Pentatonic, so any run of multipliers is consonant: nothing here can sound sour.
            _scale = ProceduralToneBank.CreateScale(8, 261.63f, 0.5f);
            // Bat and stumps are short tonal knocks, so they stay procedural unless the kit overrides them.
            // The crowd comes only from recorded clips; an empty slot is silent, never a fake.
            CricketAudioKit kit = _audio;
            _crack = kit != null && kit.bat != null ? kit.bat : ProceduralCrowd.CreateBatCrack(1f, 25);
            _stumps = kit != null && kit.stumps != null ? kit.stumps : ProceduralCrowd.CreateStumpsClatter(26);
            if (kit != null)
            {
                _groan = kit.groan;
                // A three is a ripple, not a roar: with no small cheer it gets applause instead.
                _cheerMild = kit.cheerSmall != null ? kit.cheerSmall : kit.applause;
                _cheerBig = kit.cheerBig;
                _cheerRoar = kit.cheerRoar != null ? kit.cheerRoar : kit.cheerBig;
                _cheerFinal = kit.cheerFinal != null ? kit.cheerFinal : _cheerRoar;
                _applause = kit.applause != null ? kit.applause : kit.cheerBig;
                _applauseSoft = kit.applauseSoft != null ? kit.applauseSoft : _applause;
            }
            else
            {
                Debug.LogWarning("[cric] No CricketAudioKit assigned: the crowd will be silent. " +
                                 "Rebuild the scene or drag a kit onto CricketPresenter.", this);
            }
        }

        void LateUpdate()
        {
            if (_simulation == null || _simulation.ColumnCount == 0 || _simulation.Match == null)
            {
                return;
            }

            if (_simulation.RunId != _runId)
            {
                ResetForNewRun();
            }

            if (_simulation.DeliveryId != _deliveryId)
            {
                RefreshGates();
            }

            PlaceLabels();
            UpdateReadout();
            ConsumeMultiplies();
            ConsumeDeliveries();
            WatchInnings();
            RunFlashes();
            StyleBall();
            UpdateHud();
            PlayPending();
        }

        void ResetForNewRun()
        {
            _runId = _simulation.RunId;
            _deliveryId = -1;
            _multiplyIndex = 0;
            _deliveryIndex = 0;
            _innings = 0;
            _finalPlayed = false;
            _footerText = string.Empty;
            _lastScore = string.Empty;
            _pending.Clear();

            int total = _simulation.ColumnCount * _simulation.Lanes;
            _flashStart = new float[total];
            _gateColors = new Color[total];

            if (_labelIndex == null || _labelIndex.Length != total)
            {
                _labelIndex = new int[total];
                for (int i = 0; i < total; i++)
                {
                    _labelIndex[i] = _labels != null ? _labels.Add(string.Empty, 40f, Color.white) : -1;
                }
            }

            // The big readout: distance while the ball is live, the result once it is hit. It lives in its
            // own band between the ground and the strip, so it never overlaps either.
            if (_labels != null && _readout < 0)
            {
                _readout = _labels.Add(string.Empty, 130f, Color.white);
            }

            _readoutLast = string.Empty;
            _celebrated = false;
            if (_confetti != null)
            {
                _confetti.Clear();
            }

            if (_chart != null)
            {
                _chart.Clear();
            }

            ApplyTeamColors();
        }

        /// <summary>Lane order is drawn per delivery, so labels and colours are rewritten for every ball.</summary>
        void RefreshGates()
        {
            _deliveryId = _simulation.DeliveryId;
            int lanes = _simulation.Lanes;

            for (int column = 0; column < _simulation.ColumnCount; column++)
            {
                for (int lane = 0; lane < lanes; lane++)
                {
                    int i = column * lanes + lane;
                    GateDef gate = _simulation.GateAt(column, lane);
                    _gateColors[i] = ColorFor(gate);
                    _flashStart[i] = -10f;

                    if (_labels != null && _labelIndex[i] >= 0)
                    {
                        _labels.SetText(_labelIndex[i], gate.Label);
                        _labels.SetColor(_labelIndex[i], Color.white);
                    }

                    if (_gateBars != null && i < _gateBars.Length)
                    {
                        _gateBars[i].color = WithAlpha(_gateColors[i], gate.kind == GateKind.Pass ? 0.12f : gateBarAlpha);
                    }
                }
            }
        }

        void PlaceLabels()
        {
            if (_labels == null || _labelIndex == null)
            {
                return;
            }

            int lanes = _simulation.Lanes;
            for (int i = 0; i < _labelIndex.Length; i++)
            {
                if (_labelIndex[i] >= 0)
                {
                    _labels.Place(_labelIndex[i], _simulation.GatePosition(i / lanes, i % lanes));
                }
            }
        }

        /// <summary>
        /// The big number under the ground. While the ball is live it is the distance, coloured from white
        /// through gold to red as it grows; after a ball it is the result, popped in.
        /// </summary>
        void UpdateReadout()
        {
            if (_labels == null || _readout < 0)
            {
                return;
            }

            CricketConfig config = _simulation.Config;
            CricketMatch match = _simulation.Match;
            string text;
            Color color = Color.white;

            switch (_simulation.Phase)
            {
                case CricketPhase.PreBall:
                case CricketPhase.Falling:
                    text = $"{_simulation.Distance:0} m";
                    float growth = Mathf.Clamp01(Mathf.Log(Mathf.Max(1f, _simulation.Distance / config.baseDistance), 2f) / 3f);
                    color = _simulation.Phase == CricketPhase.PreBall
                        ? new Color(0.75f, 0.8f, 0.9f, 1f)
                        : Color.Lerp(Color.white, growth < 0.5f ? FourColor : SixColor, growth < 0.5f ? growth * 2f : (growth - 0.5f) * 2f);
                    break;

                case CricketPhase.InningsBreak:
                    text = $"TARGET {match.Target}";
                    color = FourColor;
                    break;

                case CricketPhase.Final:
                    // The winner, on the pill, in their colour. The margin is in the footer.
                    if (match.Winner >= 0)
                    {
                        CricketTeamDef winner = match.Winner == 0 ? config.teamA : config.teamB;
                        text = $"{config.TeamName(match.Winner)} WON!";
                        color = winner != null ? Color.Lerp(winner.primary, Color.white, 0.35f) : FourColor;
                    }
                    else
                    {
                        text = "MATCH TIED";
                        color = Color.white;
                    }

                    break;

                default:
                    // Result: the last ball, in its own colour.
                    if (_simulation.Deliveries.Count > 0)
                    {
                        BallOutcome outcome = _simulation.Deliveries[_simulation.Deliveries.Count - 1].outcome;
                        text = BallOutcomes.Label(outcome);
                        color = ColorForOutcome(outcome);
                    }
                    else
                    {
                        text = string.Empty;
                    }

                    break;
            }

            if (text != _readoutLast)
            {
                // A new word pops in; a changing distance just ticks up, so it is not a punch every frame.
                bool distanceTick = _simulation.Phase == CricketPhase.Falling && _readoutLast.EndsWith(" m");
                if (!distanceTick)
                {
                    _readoutPunch = Time.time;
                }

                _readoutLast = text;
                _labels.SetText(_readout, text);
            }

            _labels.SetColor(_readout, color);

            // A long winning line shrinks to fit the pill; short readouts stay at full size.
            bool decided = _simulation.Phase == CricketPhase.Final;
            float fit = decided ? Mathf.Clamp(10f / Mathf.Max(1, text.Length), 0.45f, 0.85f) : 1f;
            float pop = Mathf.Clamp01((Time.time - _readoutPunch) / 0.3f);
            _labels.SetScale(_readout, fit * Mathf.Lerp(1.3f, 1f, pop));
            TintPill(decided && match.Winner >= 0 ? (match.Winner == 0 ? config.teamA : config.teamB) : null);
            _labels.Place(_readout, new Vector3(config.readoutCenter.x, config.readoutCenter.y, 0f));
        }

        /// <summary>The pill takes a dark wash of the winner's colour at the end, and goes back after.</summary>
        void TintPill(CricketTeamDef winner)
        {
            if (_readoutPill == null)
            {
                return;
            }

            if (!_pillRestCaptured)
            {
                _pillRest = _readoutPill.color;
                _pillRestCaptured = true;
            }

            Color target = winner != null ? Color.Lerp(_pillRest, winner.primary, 0.5f) : _pillRest;
            target.a = _pillRest.a;
            _readoutPill.color = Color.Lerp(_readoutPill.color, target, 0.15f);
        }

        void ConsumeMultiplies()
        {
            var events = _simulation.Multiplies;
            int sounds = 0;
            for (; _multiplyIndex < events.Count; _multiplyIndex++)
            {
                MultiplyEvent e = events[_multiplyIndex];
                int i = e.column * _simulation.Lanes + e.lane;
                _flashStart[i] = Time.time;

                if (sounds++ < 2)
                {
                    // Bigger multiplier, higher note: the pitch climbs with the stakes.
                    int note = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(Mathf.Max(1.5f, e.value), 2f) * 2f), 0, _scale.Length - 1);
                    _sfx.PlayOneShot(_scale[note], gateVolume);
                }

                if (_burst != null)
                {
                    _burst.Burst(e.position, Vector2.up, _gateColors[i], 0.4f, 0);
                }
            }
        }

        void ConsumeDeliveries()
        {
            var deliveries = _simulation.Deliveries;
            for (; _deliveryIndex < deliveries.Count; _deliveryIndex++)
            {
                BallRecord ball = deliveries[_deliveryIndex];
                OnBall(ball);
            }
        }

        void OnBall(BallRecord ball)
        {
            _footerText = BallOutcomes.Label(ball.outcome);

            if (ball.outcome == BallOutcome.Wicket)
            {
                _outFlashStart = Time.time;
                Play(_stumps, batVolume);
                Queue(_groan, 0.18f, cheerVolume * 0.6f);
                // The fielding side's fans erupt a beat later.
                Queue(_cheerBig, 0.55f, cheerVolume * 0.75f);
                if (_burst != null)
                {
                    _burst.Burst(ball.position, Vector2.down, OutColor, 1f, 1);
                }

                return;
            }

            if (_chart != null)
            {
                _chart.AddShot(ball.angle, LengthFor(ball.outcome), ColorForOutcome(ball.outcome),
                    ball.outcome == BallOutcome.Six, ball.outcome == BallOutcome.Dot ? 0.8f : 1f);
            }

            if (_burst != null)
            {
                _burst.Burst(ball.position, Vector2.up, ColorForOutcome(ball.outcome),
                    BallOutcomes.IsBoundary(ball.outcome) ? 1f : 0.5f, 2);
            }

            switch (ball.outcome)
            {
                case BallOutcome.Dot:
                    // A small collective "aww": the same groan, much quieter.
                    Queue(_groan, 0.1f, cheerVolume * 0.28f);
                    break;

                case BallOutcome.Single:
                    Play(_crack, batVolume * 0.45f);
                    Queue(_applauseSoft, 0.15f, cheerVolume * 0.55f);
                    break;

                case BallOutcome.Two:
                    Play(_crack, batVolume * 0.6f);
                    Queue(_applause, 0.15f, cheerVolume * 0.7f);
                    break;

                case BallOutcome.Three:
                    Play(_crack, batVolume * 0.7f);
                    Queue(_cheerMild, 0.15f, cheerVolume * 0.8f);
                    break;

                case BallOutcome.Four:
                    Play(_crack, batVolume * 0.9f);
                    Queue(_cheerBig, 0.18f, cheerVolume);
                    break;

                case BallOutcome.Six:
                    Play(_crack, batVolume);
                    Queue(_cheerRoar, 0.2f, cheerVolume);
                    Queue(_applause, 1.4f, cheerVolume * 0.6f);
                    break;
            }
        }

        void WatchInnings()
        {
            CricketMatch match = _simulation.Match;
            if (match.InningsIndex != _innings)
            {
                _innings = match.InningsIndex;
                if (_chart != null)
                {
                    _chart.Clear();
                }

                ApplyTeamColors();
                // The first side's innings is over: the ground applauds before the chase.
                Queue(_applause, 0.1f, cheerVolume * 0.6f);
            }

            if (_simulation.Phase == CricketPhase.Final && !_celebrated && match.Winner >= 0)
            {
                _celebrated = true;
                CricketConfig config = _simulation.Config;
                CricketTeamDef winner = match.Winner == 0 ? config.teamA : config.teamB;
                if (_confetti != null)
                {
                    // The winner's colours, plus a few festive ones so it never reads as one flat colour.
                    Color first = winner != null ? winner.primary : FourColor;
                    Color second = winner != null ? winner.secondary : SixColor;
                    _confetti.Celebrate(first, second, first, Color.white, FourColor, SixColor, TwoColor, ThreeColor);
                }
            }

            if (match.IsMatchOver && !_finalPlayed && _simulation.Phase == CricketPhase.Result)
            {
                _finalPlayed = true;
                Queue(_cheerFinal, 1.2f, cheerVolume);
                Queue(_applause, 2.6f, cheerVolume * 0.7f);
            }
        }

        void ApplyTeamColors()
        {
            CricketConfig config = _simulation.Config;
            CricketTeamDef team = _simulation.BattingSide == 0 ? config.teamA : config.teamB;
            if (_chart != null && team != null)
            {
                _chart.SetRingColor(Color.Lerp(Color.white, team.primary, 0.55f));
            }
        }

        void RunFlashes()
        {
            if (_gateBars != null && _flashStart != null)
            {
                for (int i = 0; i < _gateBars.Length && i < _flashStart.Length; i++)
                {
                    float t = Mathf.Clamp01((Time.time - _flashStart[i]) / gateFlashSeconds);
                    float rest = _simulation.GateAt(i / _simulation.Lanes, i % _simulation.Lanes).kind == GateKind.Pass ? 0.12f : gateBarAlpha;
                    Color bar = Color.Lerp(Color.white, WithAlpha(_gateColors[i], rest), t);
                    bar.a = Mathf.Lerp(0.95f, rest, t);
                    _gateBars[i].color = bar;

                    if (_labels != null && _labelIndex[i] >= 0)
                    {
                        _labels.SetScale(_labelIndex[i], Mathf.Lerp(1.25f, 1f, t));
                    }
                }
            }

            if (_chart != null && Time.time - _outFlashStart < 0.5f)
            {
                _chart.SetRingColor(Color.Lerp(OutColor, Color.white, (Time.time - _outFlashStart) / 0.5f));
            }
            else if (_outFlashStart > 0f)
            {
                _outFlashStart = -10f;
                ApplyTeamColors();
            }
        }

        /// <summary>The ball swells and warms as it gathers distance, so the stakes are visible on the ball.</summary>
        void StyleBall()
        {
            if (_ballVisual == null || !_simulation.BallActive)
            {
                return;
            }

            CricketConfig config = _simulation.Config;
            float doublings = Mathf.Log(Mathf.Max(1f, _simulation.Distance / config.baseDistance), 2f);
            float swell = Mathf.Min(maxSwell, 1f + swellPerDoubling * doublings);
            _ballVisual.localScale = Vector3.one * (_simulation.BallRadius * 2f * swell);

            if (_ballRenderer != null)
            {
                _ballRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f, 1f), Mathf.Clamp01(doublings / 3f));
            }
        }

        void UpdateHud()
        {
            if (_hud == null)
            {
                return;
            }

            CricketConfig config = _simulation.Config;
            CricketMatch match = _simulation.Match;
            int side = match.InningsIndex;
            CricketTeamDef team = side == 0 ? config.teamA : config.teamB;
            string shortName = team != null ? team.shortName : (side == 0 ? "A" : "B");

            string score = $"{shortName} {match.CurrentRuns}/{match.CurrentWickets}";
            // Punch only when the score moves; a wicket or a run is the moment worth emphasising.
            _hud.SetCaption(score, score != _lastScore && !string.IsNullOrEmpty(_lastScore));
            _lastScore = score;

            if (match.IsMatchOver && _simulation.Phase != CricketPhase.Result)
            {
                _hud.SetSubline($"{config.TeamName(0)} {match.Runs(0)}/{match.Wickets(0)}  ·  {config.TeamName(1)} {match.Runs(1)}/{match.Wickets(1)}");
                _hud.SetFooter(match.ResultText(config.TeamName(0), config.TeamName(1)));
                return;
            }

            if (match.IsChasing)
            {
                _hud.SetSubline(match.RunsNeeded > 0
                    ? $"NEED {match.RunsNeeded} OFF {match.BallsLeft}"
                    : "TARGET PASSED");
            }
            else
            {
                _hud.SetSubline($"{match.CurrentOversText} / {config.oversPerInnings} OV  ·  {match.BallsLeft} BALLS LEFT");
            }

            if (_simulation.Phase == CricketPhase.InningsBreak)
            {
                _hud.SetSubline($"{config.TeamName(1)} NEED {match.Target} TO WIN");
                _hud.SetFooter($"TARGET {match.Target}");
            }
            else if (_simulation.Phase == CricketPhase.Falling)
            {
                // The big number above is the distance; this says what it would score if hit now.
                BallOutcome now = config.OutcomeFor(_simulation.Distance);
                _hud.SetFooter(now == BallOutcome.Dot ? "WORTH NOTHING YET" : $"WORTH {BallOutcomes.Runs(now)} IF HIT NOW");
            }
            else if (_simulation.Phase == CricketPhase.PreBall)
            {
                _hud.SetFooter(_deliveryIndex == 0 ? "RELEASE THE BALL" : $"BALL {match.CurrentBalls + 1}");
            }
            else
            {
                _hud.SetFooter(_footerText);
            }
        }

        void Play(AudioClip clip, float volume)
        {
            if (clip != null)
            {
                _sfx.PlayOneShot(clip, volume);
            }
        }

        void Queue(AudioClip clip, float delay, float volume)
        {
            if (clip == null)
            {
                return;
            }

            _pending.Add(new Pending { time = Time.time + delay, clip = clip, volume = volume });
        }

        void PlayPending()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.time >= _pending[i].time)
                {
                    Play(_pending[i].clip, _pending[i].volume);
                    _pending.RemoveAt(i);
                }
            }
        }

        static float LengthFor(BallOutcome outcome)
        {
            switch (outcome)
            {
                case BallOutcome.Dot: return 0.12f;
                case BallOutcome.Single: return 0.36f;
                case BallOutcome.Two: return 0.5f;
                case BallOutcome.Three: return 0.66f;
                default: return 1f;
            }
        }

        static Color ColorForOutcome(BallOutcome outcome)
        {
            switch (outcome)
            {
                case BallOutcome.Single: return SingleColor;
                case BallOutcome.Two: return TwoColor;
                case BallOutcome.Three: return ThreeColor;
                case BallOutcome.Four: return FourColor;
                case BallOutcome.Six: return SixColor;
                case BallOutcome.Wicket: return OutColor;
                default: return DotColor;
            }
        }

        static Color ColorFor(GateDef gate)
        {
            switch (gate.kind)
            {
                case GateKind.Multiply:
                    // Hue slides from teal to green with the multiplier, so bigger reads as brighter.
                    return Color.HSVToRGB(Mathf.Lerp(0.5f, 0.33f, Mathf.Clamp01(Mathf.Log(Mathf.Max(1f, gate.value), 2f) / 2f)), 0.75f, 1f);
                case GateKind.Release:
                    return new Color(1f, 0.82f, 0.2f, 1f);
                case GateKind.Out:
                    return OutColor;
                default:
                    return new Color(0.5f, 0.55f, 0.7f, 1f);
            }
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(CricketSimulation simulation, CaptionHud hud, WorldLabelLayer labels, SpriteRenderer[] gateBars,
                         RadialHitChart chart, Transform ballVisual, SpriteRenderer ballRenderer, ShatterBurst2D burst,
                         CricketAudioKit audio, SpriteRenderer readoutPill, ConfettiBurst2D confetti)
        {
            _simulation = simulation;
            _hud = hud;
            _labels = labels;
            _gateBars = gateBars;
            _chart = chart;
            _ballVisual = ballVisual;
            _ballRenderer = ballRenderer;
            _burst = burst;
            _audio = audio;
            _readoutPill = readoutPill;
            _confetti = confetti;
        }
    }
}
