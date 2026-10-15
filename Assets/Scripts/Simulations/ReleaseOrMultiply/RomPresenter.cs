using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.ReleaseOrMultiply
{
    /// <summary>
    /// Turns <see cref="RomSimulation"/> state into the video: a huge running count, labelled gates that
    /// flash when crossed, a note per multiplier, marbles that swell and warm in colour as they come to
    /// stand for more.
    /// </summary>
    /// <remarks>
    /// Reads the simulation in <c>LateUpdate</c> and never writes it. Flashes and punches run on
    /// <c>Time.time</c>, which is fine: nothing they drive feeds back into the run.
    /// </remarks>
    [RequireComponent(typeof(RomSimulation))]
    public sealed class RomPresenter : MonoBehaviour
    {
        [SerializeField] RomSimulation _simulation;
        [SerializeField] CaptionHud _hud;
        [SerializeField] WorldLabelLayer _labels;
        [SerializeField] SpriteRenderer[] _gateBars;
        [SerializeField] ShatterBurst2D _burst;
        [SerializeField] Camera _camera;

        [SerializeField] string _question = "HOW MANY WILL REACH BOTTOM?";

        [Header("Feel")]
        [Range(0.05f, 1f)] public float gateFlashSeconds = 0.35f;
        [Tooltip("The count punches when it has grown by this factor since the last punch.")]
        [Range(1.02f, 3f)] public float punchGrowth = 1.25f;
        [Range(0f, 1f)] public float gateVolume = 0.3f;
        [Range(0f, 1f)] public float wipeVolume = 0.55f;
        [Range(1, 12)] public int maxSoundsPerFrame = 4;
        [Range(1, 12)] public int maxBurstsPerFrame = 5;
        [Range(0f, 1f)] public float gateBarAlpha = 0.55f;

        [Tooltip("How fast a marble swells with its weight: scale = 1 + this * log10(weight).")]
        [Range(0f, 1.5f)] public float swellPerDecade = 0.5f;
        [Range(1f, 8f)] public float maxSwell = 3.5f;

        AudioSource _sfx;
        AudioClip[] _scale;
        AudioClip _thud;
        AudioClip _wipe;
        float[] _flashStart;
        Color[] _gateColors;
        int[] _labelIndex;
        int _runId = -1;
        int _eventIndex;
        long _lastPunchCount;
        long _lastCount = -1;
        string _lastGateText = string.Empty;

        void Reset()
        {
            _simulation = GetComponent<RomSimulation>();
        }

        void Start()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;

            // Pentatonic, so any run of multipliers sounds consonant: nothing here can sound sour.
            _scale = ProceduralToneBank.CreateScale(8, 261.63f, 0.55f);
            _thud = ProceduralToneBank.CreateImpactThud(0.8f, 0.2f, 31, "HalveThud");
            _wipe = ProceduralToneBank.CreateShatter(1f, 33, "WipeCrash");

            if (_hud != null)
            {
                _hud.SetSubline(_question);
            }
        }

        void LateUpdate()
        {
            if (_simulation == null || _simulation.RowCount == 0)
            {
                return;
            }

            if (_simulation.RunId != _runId)
            {
                ResetForNewRun();
            }

            PlaceLabels();
            ConsumeEvents();
            RunFlashes();
            StyleMarbles();
            UpdateHud();
        }

        void ResetForNewRun()
        {
            _runId = _simulation.RunId;
            _eventIndex = 0;
            _lastPunchCount = 0;
            _lastCount = -1;
            _lastGateText = string.Empty;

            int lanes = _simulation.Lanes;
            int total = _simulation.RowCount * lanes;
            _flashStart = new float[total];
            _gateColors = new Color[total];

            // Lane order is drawn from the seed, so the labels and colours are rewritten every run.
            if (_labelIndex == null || _labelIndex.Length != total)
            {
                _labelIndex = new int[total];
                for (int i = 0; i < total; i++)
                {
                    _labelIndex[i] = _labels != null ? _labels.Add(string.Empty, 64f, Color.white) : -1;
                }
            }

            for (int row = 0; row < _simulation.RowCount; row++)
            {
                for (int lane = 0; lane < lanes; lane++)
                {
                    int i = row * lanes + lane;
                    GateDef gate = _simulation.GateAt(row, lane);
                    _gateColors[i] = ColorFor(gate);
                    _flashStart[i] = -10f;

                    if (_labels != null)
                    {
                        _labels.SetText(_labelIndex[i], gate.Label);
                        _labels.SetColor(_labelIndex[i], Color.white);
                    }

                    if (_gateBars != null && i < _gateBars.Length)
                    {
                        _gateBars[i].color = WithAlpha(_gateColors[i], gateBarAlpha);
                    }
                }
            }

            if (_burst != null)
            {
                _burst.Clear();
            }
        }

        void PlaceLabels()
        {
            if (_labels == null)
            {
                return;
            }

            int lanes = _simulation.Lanes;
            for (int i = 0; i < _labelIndex.Length; i++)
            {
                _labels.Place(_labelIndex[i], _simulation.GatePosition(i / lanes, i % lanes));
            }
        }

        void ConsumeEvents()
        {
            var events = _simulation.Events;
            int sounds = 0;
            int bursts = 0;

            for (; _eventIndex < events.Count; _eventIndex++)
            {
                GateEvent e = events[_eventIndex];
                int i = e.row * _simulation.Lanes + e.lane;
                _flashStart[i] = Time.time;

                if (e.gate.kind != GateKind.Halve || e.after != e.before)
                {
                    _lastGateText = e.gate.Label;
                }

                if (sounds < maxSoundsPerFrame)
                {
                    sounds++;
                    PlayGate(e);
                }

                if (_burst != null && bursts < maxBurstsPerFrame)
                {
                    bursts++;
                    float intensity = e.gate.kind == GateKind.Wipe ? 1f : Mathf.Clamp01(0.3f + e.clonesAdded / 12f);
                    _burst.Burst(e.position, Vector2.up, _gateColors[i], intensity, (int)e.gate.kind);
                }
            }
        }

        void PlayGate(GateEvent e)
        {
            if (_sfx == null)
            {
                return;
            }

            switch (e.gate.kind)
            {
                case GateKind.Wipe:
                    _sfx.PlayOneShot(_wipe, wipeVolume);
                    break;

                case GateKind.Halve:
                    _sfx.PlayOneShot(_thud, gateVolume);
                    break;

                default:
                    // Bigger multiplier, higher note: the pitch climbs with the stakes.
                    float size = e.gate.kind == GateKind.Multiply ? e.gate.value : 1f + e.gate.value * 0.25f;
                    int note = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(Mathf.Max(2f, size), 2f)), 0, _scale.Length - 1);
                    _sfx.PlayOneShot(_scale[note], gateVolume);
                    break;
            }
        }

        void RunFlashes()
        {
            if (_gateBars == null)
            {
                return;
            }

            for (int i = 0; i < _gateBars.Length && i < _flashStart.Length; i++)
            {
                float t = Mathf.Clamp01((Time.time - _flashStart[i]) / gateFlashSeconds);
                Color bar = Color.Lerp(Color.white, WithAlpha(_gateColors[i], gateBarAlpha), t);
                bar.a = Mathf.Lerp(0.95f, gateBarAlpha, t);
                _gateBars[i].color = bar;

                if (_labels != null)
                {
                    _labels.SetScale(_labelIndex[i], Mathf.Lerp(1.25f, 1f, t));
                }
            }
        }

        /// <summary>A marble standing for more marbles looks bigger and warmer, so the weight is visible.</summary>
        void StyleMarbles()
        {
            float baseDiameter = _simulation.MarbleRadius * 2f;
            var marbles = _simulation.Marbles;
            for (int i = 0; i < marbles.Count; i++)
            {
                Marble2D marble = marbles[i];
                if (marble.Visual == null)
                {
                    continue;
                }

                float decades = Mathf.Log10(Mathf.Max(1f, marble.Weight));
                float swell = Mathf.Min(maxSwell, 1f + swellPerDecade * decades);
                // Weight changes only at gates, so skip the transform and sprite writes for the rest.
                if (marble.StyledWeight == marble.Weight)
                {
                    continue;
                }

                marble.StyledWeight = marble.Weight;
                marble.Visual.localScale = Vector3.one * (baseDiameter * swell);

                if (marble.Renderer != null)
                {
                    marble.Renderer.color = Color.Lerp(Color.white, new Color(1f, 0.78f, 0.2f, 1f), Mathf.Clamp01(decades / 3f));
                }
            }
        }

        void UpdateHud()
        {
            if (_hud == null)
            {
                return;
            }

            long count = _simulation.TotalCount;
            if (count != _lastCount)
            {
                bool punch = count < _lastCount || count >= (long)(_lastPunchCount * punchGrowth) + 1;
                if (punch)
                {
                    _lastPunchCount = count;
                }

                _hud.SetCaption(CountFormat.Compact(count), punch);
                _lastCount = count;
            }

            if (_simulation.IsFinal)
            {
                _hud.SetSubline("FINAL COUNT");
                _hud.SetFooter(_simulation.Banked >= _simulation.PeakCount
                    ? "DID YOU GUESS IT RIGHT?"
                    : $"PEAK {CountFormat.Compact(_simulation.PeakCount)} - LOST {CountFormat.Compact(_simulation.PeakCount - _simulation.Banked)}");
            }
            else
            {
                _hud.SetSubline(_question);
                _hud.SetFooter(string.IsNullOrEmpty(_lastGateText) ? string.Empty : $"LAST GATE  {_lastGateText}");
            }
        }

        static Color ColorFor(GateDef gate)
        {
            switch (gate.kind)
            {
                case GateKind.Multiply:
                    // Hue slides from teal to green with the multiplier, so bigger reads as brighter.
                    return Color.HSVToRGB(Mathf.Lerp(0.5f, 0.33f, Mathf.Clamp01(Mathf.Log(gate.value, 2f) / 7f)), 0.75f, 1f);
                case GateKind.Add:
                    return new Color(1f, 0.85f, 0.25f, 1f);
                case GateKind.Halve:
                    return new Color(1f, 0.55f, 0.2f, 1f);
                default:
                    return new Color(1f, 0.22f, 0.3f, 1f);
            }
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(RomSimulation simulation, CaptionHud hud, WorldLabelLayer labels, SpriteRenderer[] gateBars,
                         ShatterBurst2D burst, Camera camera, string question)
        {
            _simulation = simulation;
            _hud = hud;
            _labels = labels;
            _gateBars = gateBars;
            _burst = burst;
            _camera = camera;
            _question = question;
        }
    }
}
