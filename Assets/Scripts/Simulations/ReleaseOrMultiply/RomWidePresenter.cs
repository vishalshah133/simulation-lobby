using SimulationLobby.Presentation;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.ReleaseOrMultiply
{
    /// <summary>
    /// 16:9 presenter for the "Gate Funnel" segment. Same rules as <see cref="RomPresenter"/> (gates flash,
    /// marbles swell and go gold) but the numbers go to the wide-frame HUD: counter in the right margin,
    /// one held result caption in the bottom strip, and a huge final number over a gold pile at the end.
    /// </summary>
    /// <remarks>Reads the simulation in <c>LateUpdate</c> and never writes it.</remarks>
    [RequireComponent(typeof(RomSimulation))]
    public sealed class RomWidePresenter : MonoBehaviour
    {
        [SerializeField] RomSimulation _simulation;
        [SerializeField] RomWideSettings _settings;
        [SerializeField] SegmentCardPresenter _card;
        [SerializeField] RomWideStarter _starter;
        [SerializeField] Camera _camera;
        [SerializeField] SpriteRenderer[] _gateBars;
        [SerializeField] TMP_Text[] _gateLabels;
        [SerializeField] ShatterBurst2D _burst;
        [SerializeField] Sprite _dotSprite;
        [SerializeField] int _openingCount = 1;
        [SerializeField] Vector2 _pileCenterBottom;
        [SerializeField] float _pileWidth = 10f;
        [SerializeField] RectTransform _finalRoot;
        [SerializeField] CanvasGroup _finalGroup;
        [SerializeField] TMP_Text _finalNumber;
        [SerializeField] TMP_Text _finalNote;

        [Header("Feel")]
        [Range(0.05f, 1f)] public float gateFlashSeconds = 0.4f;
        [Range(0f, 1f)] public float gateVolume = 0.35f;
        [Range(0f, 1f)] public float wipeVolume = 0.55f;
        [Range(1, 12)] public int maxSoundsPerFrame = 4;
        [Range(1, 12)] public int maxBurstsPerFrame = 5;
        [Range(0f, 1f)] public float gateBarAlpha = 0.92f;
        [Tooltip("scale = 1 + this * log10(weight): a marble standing for more looks bigger.")]
        [Range(0f, 1.5f)] public float swellPerDecade = 0.55f;
        [Range(1f, 8f)] public float maxSwell = 3.2f;

        static readonly Color Gold = new Color(1f, 0.78f, 0.2f, 1f);
        static readonly Color Cream = new Color(1f, 0.93f, 0.7f, 1f);

        AudioSource _sfx;
        AudioClip[] _scale;
        AudioClip _thud;
        AudioClip _wipe;
        float[] _flashStart;
        Color[] _gateColors;
        int _runId = -1;
        int _eventIndex;
        long _lastCount = -1;
        bool _hasPending;
        GateEvent _pending;
        float _captionFreeAt;
        float _finalStart = -1f;
        bool _finalPlaced;

        SpriteRenderer[] _pile;
        int _pileShown;

        void Reset()
        {
            _simulation = GetComponent<RomSimulation>();
        }

        void Start()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;

            // Pentatonic, so any run of multipliers sounds consonant.
            _scale = ProceduralToneBank.CreateScale(8, 261.63f, 0.55f);
            _thud = ProceduralToneBank.CreateImpactThud(0.8f, 0.2f, 31, "HalveThud");
            _wipe = ProceduralToneBank.CreateShatter(1f, 33, "WipeCrash");

            BuildPile();
            if (_finalGroup != null)
            {
                _finalGroup.alpha = 0f;
            }

            if (_card != null)
            {
                // The card timeline runs on the starter's clock, which does not reset when the run begins.
                if (_starter != null)
                {
                    _card.SetTimeSource(() => _starter.Elapsed);
                }

                _card.SetCounter(CounterText(_openingCount));
                _card.SetResultCaption(_settings != null ? _settings.openingCaption : string.Empty);
            }
        }

        void LateUpdate()
        {
            if (_simulation == null || _simulation.RowCount == 0)
            {
                return; // the run has not begun (start delay): gates stay blank and hidden
            }

            if (_simulation.RunId != _runId)
            {
                ResetForNewRun();
            }

            ConsumeEvents();
            RunFlashes();
            StyleMarbles();
            UpdateHud();
            UpdatePile();
            UpdateFinal();
        }

        void ResetForNewRun()
        {
            _runId = _simulation.RunId;
            _eventIndex = 0;
            _lastCount = -1;
            _hasPending = false;
            _captionFreeAt = 0f;
            _finalStart = -1f;
            _pileShown = 0;

            int lanes = _simulation.Lanes;
            int total = _simulation.RowCount * lanes;
            _flashStart = new float[total];
            _gateColors = new Color[total];

            for (int row = 0; row < _simulation.RowCount; row++)
            {
                for (int lane = 0; lane < lanes; lane++)
                {
                    int i = row * lanes + lane;
                    GateDef gate = _simulation.GateAt(row, lane);
                    _gateColors[i] = ColorFor(gate);
                    _flashStart[i] = -10f;

                    if (_gateLabels != null && i < _gateLabels.Length && _gateLabels[i] != null)
                    {
                        _gateLabels[i].text = gate.Label;
                        _gateLabels[i].color = gate.kind == GateKind.Wipe ? Color.white : new Color(0.03f, 0.04f, 0.08f, 1f);
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

            if (_pile != null)
            {
                for (int i = 0; i < _pile.Length; i++)
                {
                    _pile[i].gameObject.SetActive(false);
                }
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

                // The caption shows the most dramatic crossing since it last changed.
                if (!_hasPending || Mathf.Abs(e.after - e.before) >= Mathf.Abs(_pending.after - _pending.before))
                {
                    _pending = e;
                    _hasPending = true;
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
                bar.a = Mathf.Lerp(1f, gateBarAlpha, t);
                _gateBars[i].color = bar;

                if (_gateLabels != null && i < _gateLabels.Length && _gateLabels[i] != null)
                {
                    float s = Mathf.Lerp(1.2f, 1f, t);
                    _gateLabels[i].transform.localScale = new Vector3(s, s, 1f);
                }
            }
        }

        /// <summary>A marble standing for more marbles looks bigger and more golden, so the weight is visible.</summary>
        void StyleMarbles()
        {
            float baseDiameter = _simulation.MarbleRadius * 2f;
            var marbles = _simulation.Marbles;
            for (int i = 0; i < marbles.Count; i++)
            {
                Marble2D marble = marbles[i];
                if (marble.Visual == null || marble.StyledWeight == marble.Weight)
                {
                    continue;
                }

                marble.StyledWeight = marble.Weight;
                float decades = Mathf.Log10(Mathf.Max(1f, marble.Weight));
                marble.Visual.localScale = Vector3.one * (baseDiameter * Mathf.Min(maxSwell, 1f + swellPerDecade * decades));

                if (marble.Renderer != null)
                {
                    marble.Renderer.color = Color.Lerp(Cream, Gold, Mathf.Clamp01(decades / 1.5f));
                }
            }
        }

        void UpdateHud()
        {
            if (_card == null)
            {
                return;
            }

            long count = _simulation.TotalCount;
            if (count != _lastCount)
            {
                _lastCount = count;
                _card.SetCounter(CounterText(count));
            }

            if (_simulation.IsFinal)
            {
                long banked = _simulation.Banked;
                long lost = _simulation.PeakCount - banked;
                _card.SetResultCaption(banked == 0
                    ? "EVERY MARBLE WAS LOST"
                    : lost > 0
                        ? CountFormat.Compact(banked) + " REACHED THE BOTTOM  -  PEAK " + CountFormat.Compact(_simulation.PeakCount)
                        : "ALL " + CountFormat.Compact(banked) + " REACHED THE BOTTOM");
                return;
            }

            float hold = _settings != null ? _settings.captionHoldSeconds : 1.5f;
            if (_hasPending && Time.time >= _captionFreeAt)
            {
                _card.SetResultCaption(CaptionFor(_pending));
                _captionFreeAt = Time.time + hold;
                _hasPending = false;
            }
        }

        static string CaptionFor(GateEvent e)
        {
            long delta = e.after - e.before;
            switch (e.gate.kind)
            {
                case GateKind.Wipe:
                    return e.gate.Label + " GATE  -  " + CountFormat.Compact(e.before) + (e.before == 1 ? " MARBLE GONE" : " MARBLES GONE");
                case GateKind.Halve:
                    if (e.before <= 1)
                    {
                        return e.after > 0 ? "HALF GATE  -  THE LONE MARBLE SURVIVES" : "HALF GATE  -  THE LONE MARBLE IS LOST";
                    }

                    return e.gate.Label + " GATE  -  LOSES " + CountFormat.Compact(-delta);
                default:
                    return e.gate.Label + " GATE  -  GAINS " + CountFormat.Compact(delta);
            }
        }

        static string CounterText(long count)
        {
            return "MARBLES\n" + CountFormat.Compact(count, 7);
        }

        // ------------------------------------------------------------------ gold pile

        void BuildPile()
        {
            if (_settings == null || _dotSprite == null)
            {
                return;
            }

            int max = _settings.pileMaxDots;
            float d = _settings.pileDotDiameter;
            var root = new GameObject("Gold Pile").transform;
            root.SetParent(transform, false);

            // Hex-packed mound, bottom row widest, a row narrower every step. Fixed jitter: it is only a look.
            var rng = new System.Random(7);
            _pile = new SpriteRenderer[max];
            int made = 0;
            for (int row = 0; made < max; row++)
            {
                int inRow = Mathf.Max(1, Mathf.FloorToInt(_pileWidth / d) - row * 2);
                float y = _pileCenterBottom.y + d * 0.5f + row * d * 0.84f;
                for (int c = 0; c < inRow && made < max; c++)
                {
                    float x = _pileCenterBottom.x + (c - (inRow - 1) * 0.5f) * d + (float)(rng.NextDouble() - 0.5) * d * 0.15f;
                    var go = new GameObject("Dot");
                    go.transform.SetParent(root, false);
                    go.transform.position = new Vector3(x, y, 0f);
                    go.transform.localScale = Vector3.one * (d * 0.96f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = _dotSprite;
                    float shade = 0.82f + (float)rng.NextDouble() * 0.3f;
                    sr.color = new Color(Gold.r * shade, Gold.g * shade, Gold.b * shade, 1f);
                    sr.sortingOrder = 4;
                    go.SetActive(false);
                    _pile[made] = sr;
                    made++;
                }
            }
        }

        void UpdatePile()
        {
            if (_pile == null)
            {
                return;
            }

            // One dot per marble banked up to the cap, then the pile stays full.
            int target = (int)Mathf.Min(_pile.Length, _simulation.Banked);
            int step = _settings != null ? _settings.pileDotsRevealedPerFrame : 6;
            for (int n = 0; n < step && _pileShown < target; n++)
            {
                _pile[_pileShown].gameObject.SetActive(true);
                _pileShown++;
            }
        }

        // ------------------------------------------------------------------ final number

        void UpdateFinal()
        {
            if (_finalGroup == null || _finalNumber == null || !_simulation.IsFinal)
            {
                return;
            }

            if (_finalStart < 0f)
            {
                _finalStart = Time.time;
                _finalNumber.text = CountFormat.Compact(_simulation.Banked, 9);
                if (_finalNote != null)
                {
                    _finalNote.text = _simulation.Banked == 0 ? "NOTHING LEFT" : _simulation.Banked == 1 ? "MARBLE" : "MARBLES AT THE BOTTOM";
                }
            }

            if (!_finalPlaced && _camera != null && _finalRoot != null)
            {
                _finalPlaced = true;
                Vector3 view = _camera.WorldToViewportPoint(new Vector3(_pileCenterBottom.x, _pileCenterBottom.y, 0f));
                _finalRoot.anchorMin = new Vector2(view.x, 0.52f);
                _finalRoot.anchorMax = new Vector2(view.x, 0.52f);
                _finalRoot.anchoredPosition = Vector2.zero;
            }

            float fade = _settings != null ? _settings.finalFadeSeconds : 0.5f;
            float t = Mathf.Clamp01((Time.time - _finalStart) / fade);
            _finalGroup.alpha = t;
            _finalRoot.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, 1f - (1f - t) * (1f - t));
        }

        // ------------------------------------------------------------------ helpers

        static Color ColorFor(GateDef gate)
        {
            switch (gate.kind)
            {
                case GateKind.Multiply:
                    return Color.HSVToRGB(Mathf.Lerp(0.5f, 0.33f, Mathf.Clamp01(Mathf.Log(gate.value, 2f) / 7f)), 0.75f, 1f);
                case GateKind.Add:
                    return new Color(1f, 0.85f, 0.25f, 1f);
                case GateKind.Halve:
                    return new Color(1f, 0.55f, 0.2f, 1f);
                default:
                    return new Color(0.9f, 0.12f, 0.2f, 1f);
            }
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(RomSimulation simulation, RomWideSettings settings, SegmentCardPresenter card, RomWideStarter starter, Camera camera,
                         SpriteRenderer[] gateBars, TMP_Text[] gateLabels, ShatterBurst2D burst, Sprite dotSprite,
                         int openingCount, Vector2 pileCenterBottom, float pileWidth,
                         RectTransform finalRoot, CanvasGroup finalGroup, TMP_Text finalNumber, TMP_Text finalNote)
        {
            _simulation = simulation;
            _settings = settings;
            _card = card;
            _starter = starter;
            _camera = camera;
            _gateBars = gateBars;
            _gateLabels = gateLabels;
            _burst = burst;
            _dotSprite = dotSprite;
            _openingCount = openingCount;
            _pileCenterBottom = pileCenterBottom;
            _pileWidth = pileWidth;
            _finalRoot = finalRoot;
            _finalGroup = finalGroup;
            _finalNumber = finalNumber;
            _finalNote = finalNote;
        }
    }
}
