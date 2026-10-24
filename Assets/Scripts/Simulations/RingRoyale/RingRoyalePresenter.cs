using System.Collections.Generic;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.RingRoyale
{
    /// <summary>
    /// Drives the share bar, captions, x2 flash, exit edging, audio, confetti and marble colours from run
    /// state. Reads in <c>LateUpdate</c> and never writes to the run: everything it touches is its own
    /// visuals (children of the ring included).
    /// </summary>
    [RequireComponent(typeof(RingRoyaleSimulation))]
    public sealed class RingRoyalePresenter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] RingRoyaleSimulation _simulation;
        [SerializeField] ShareBarHud _shareBar;
        [SerializeField] CaptionHud _caption;
        [SerializeField] ConfettiBurst2D _confetti;

        [Header("Ring dressing (children of the ring, set by the scene builder)")]
        [SerializeField] SpriteRenderer[] _x2Segments = new SpriteRenderer[0];
        [SerializeField] Transform _exitEdgeLow;
        [SerializeField] Transform _exitEdgeHigh;
        [SerializeField] SpriteRenderer _exitEdgeLowSprite;
        [SerializeField] SpriteRenderer _exitEdgeHighSprite;
        [Tooltip("Radius, in the ring's frame, the exit edging sits at.")]
        [SerializeField] float _edgeRadius = 4.9f;
        [Tooltip("Labels parented to the ring that are kept upright while it spins.")]
        [SerializeField] Transform[] _uprightLabels = new Transform[0];

        [Header("Text")]
        [Tooltip("One line on the top panel, above the share bar.")]
        [SerializeField] string _title = "COLOR RUMBLE";
        [Tooltip("Bottom line under the ring while nothing else is happening.")]
        [SerializeField] string _question = "WHICH COLOR SURVIVES?";
        [SerializeField] string _suddenDeathText = "SUDDEN DEATH: THE EXIT IS GROWING";

        [Header("Look")]
        [SerializeField] Color _x2Gold = new Color(1f, 0.72f, 0.1f, 1f);
        [SerializeField] Color _x2Flash = new Color(1f, 0.97f, 0.7f, 1f);
        [SerializeField] Color _exitRed = new Color(1f, 0.15f, 0.12f, 1f);
        [Tooltip("Seconds the x2 arc takes to fade back from its flash.")]
        [Range(0.05f, 1f)] [SerializeField] float _flashSeconds = 0.3f;
        [Tooltip("Seconds an 'X IS OUT!' headline holds before the footer falls back.")]
        [Range(0.5f, 6f)] [SerializeField] float _outHoldSeconds = 3f;

        [Header("Audio")]
        [Range(1, 6)] [SerializeField] int _maxNotesPerFrame = 2;
        [Range(0f, 1f)] [SerializeField] float _cloneVolume = 0.5f;
        [Range(0f, 1f)] [SerializeField] float _exitVolume = 0.7f;
        [Range(0f, 1f)] [SerializeField] float _stingVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] float _sealVolume = 1f;
        [Range(0f, 1f)] [SerializeField] float _winVolume = 0.7f;
        [Range(2, 16)] [SerializeField] int _voiceCount = 10;

        AudioSource[] _voices;
        int _nextVoice;
        AudioClip[] _cloneNotes;
        AudioClip _exitThud;
        AudioClip _outTone;
        AudioClip _outThud;
        AudioClip _sealThud;
        AudioClip _suddenDeathRiser;
        AudioClip[] _winScale;

        int _lastX2;
        int _lastClones;
        int _lastExits;
        bool _lastSuddenDeath;
        bool _celebrated;
        bool[] _seenEliminated = new bool[0];
        float _flash;
        float _outTimer;
        int _outColour = -1;
        string _captionShown;
        string _footerShown;

        void Reset()
        {
            _simulation = GetComponent<RingRoyaleSimulation>();
        }

        void Start()
        {
            BuildAudio();
            Prime();

            if (_shareBar != null && _simulation != null && _simulation.Config != null)
            {
                int n = _simulation.ColourCount;
                var colours = new Color[n];
                var names = new string[n];
                for (int c = 0; c < n; c++)
                {
                    colours[c] = _simulation.Config.colours[c];
                    names[c] = _simulation.Config.NameOf(c);
                }

                _shareBar.showNames = false;
                _shareBar.eliminatedShowsZero = false;
                _shareBar.Configure(n, colours, names);
            }
        }

        void Prime()
        {
            if (_simulation == null)
            {
                return;
            }

            _lastX2 = _simulation.X2HitCount;
            _lastClones = _simulation.CloneCount;
            _lastExits = _simulation.ExitCount;
            _lastSuddenDeath = _simulation.SuddenDeathActive;
            _seenEliminated = new bool[_simulation.ColourCount];
        }

        void LateUpdate()
        {
            if (_simulation == null || _simulation.Config == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;

            TintMarbles(_simulation.Inside);
            TintMarbles(_simulation.Escaping);
            DetectEvents(dt);
            UpdateRingDressing(dt);
            UpdateShareBar();
            UpdateCaptions(dt);
        }

        void TintMarbles(IReadOnlyList<RoyaleMarble> marbles)
        {
            Color[] palette = _simulation.Config.colours;
            for (int i = 0; i < marbles.Count; i++)
            {
                RoyaleMarble marble = marbles[i];
                if (marble.StyledColour == marble.Colour || marble.Renderer == null)
                {
                    continue;
                }

                marble.StyledColour = marble.Colour;
                marble.Renderer.color = palette[marble.Colour];
            }
        }

        void DetectEvents(float dt)
        {
            RingRoyaleSimulation sim = _simulation;

            if (sim.X2HitCount < _lastX2 || sim.CloneCount < _lastClones || sim.ExitCount < _lastExits)
            {
                // A new run: resync rather than fire a burst of stale events.
                Prime();
                _celebrated = false;
                _outTimer = 0f;
                _outColour = -1;
            }

            if (sim.X2HitCount > _lastX2)
            {
                _flash = 1f;
                _lastX2 = sim.X2HitCount;
            }

            int clones = sim.CloneCount - _lastClones;
            _lastClones = sim.CloneCount;
            if (clones > 0 && _cloneNotes != null)
            {
                int colour = Mathf.Clamp(sim.LastCloneColour, 0, _cloneNotes.Length - 1);
                int notes = Mathf.Min(clones, _maxNotesPerFrame);
                for (int i = 0; i < notes; i++)
                {
                    Play(_cloneNotes[colour], _cloneVolume);
                }
            }

            int exits = sim.ExitCount - _lastExits;
            _lastExits = sim.ExitCount;
            if (exits > 0)
            {
                int thuds = Mathf.Min(exits, _maxNotesPerFrame);
                for (int i = 0; i < thuds; i++)
                {
                    Play(_exitThud, _exitVolume);
                }
            }

            if (sim.SuddenDeathActive && !_lastSuddenDeath)
            {
                Play(_suddenDeathRiser, _stingVolume);
            }

            _lastSuddenDeath = sim.SuddenDeathActive;

            for (int c = 0; c < _seenEliminated.Length; c++)
            {
                bool now = sim.IsEliminated(c);
                if (now == _seenEliminated[c])
                {
                    continue;
                }

                _seenEliminated[c] = now;
                if (_shareBar != null)
                {
                    _shareBar.SetEliminated(c, now);
                }

                if (now && sim.Winner < 0)
                {
                    _outColour = c;
                    _outTimer = _outHoldSeconds;
                    Play(_outTone, _stingVolume);
                    Play(_outThud, _stingVolume);
                }
            }

            if (_outTimer > 0f)
            {
                _outTimer -= dt;
            }

            if (sim.Winner >= 0)
            {
                _outTimer = 0f;
            }

            // The payoff lands when the EXIT slams shut behind the winners, not when the last loser leaves.
            if (sim.IsSealed && !_celebrated)
            {
                _celebrated = true;
                Play(_sealThud, _sealVolume);
                Color winColour = sim.Config.colours[sim.Winner];
                if (_confetti != null)
                {
                    _confetti.Celebrate(winColour, Color.Lerp(winColour, Color.white, 0.5f), Color.white);
                }

                if (_winScale != null)
                {
                    for (int i = 0; i < _winScale.Length; i += 2)
                    {
                        Play(_winScale[i], _winVolume);
                    }
                }
            }
        }

        void UpdateRingDressing(float dt)
        {
            RingRoyaleSimulation sim = _simulation;

            // x2 arc: gold, flashing toward near-white on every hit inside it.
            _flash = Mathf.Max(0f, _flash - dt / _flashSeconds);
            Color arc = Color.Lerp(_x2Gold, _x2Flash, _flash);
            for (int i = 0; i < _x2Segments.Length; i++)
            {
                if (_x2Segments[i] != null)
                {
                    _x2Segments[i].color = arc;
                }
            }

            // Exit edging follows the widening gap. Positions are in the ring's frame, so the spin is free.
            float centre = sim.ExitCenterLocalDegrees;
            float half = sim.ExitDegrees * 0.5f;
            PlaceEdge(_exitEdgeLow, centre - half);
            PlaceEdge(_exitEdgeHigh, centre + half);

            Color edge = _exitRed;
            if (sim.SuddenDeathActive && !sim.IsResting)
            {
                edge = Color.Lerp(_exitRed, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f));
            }
            else if (sim.Winner >= 0)
            {
                // Closing behind the winners: the lock takes on their colour as it shuts.
                edge = Color.Lerp(_exitRed, sim.Config.colours[sim.Winner], sim.SealProgress);
            }

            if (_exitEdgeLowSprite != null)
            {
                _exitEdgeLowSprite.color = edge;
            }

            if (_exitEdgeHighSprite != null)
            {
                _exitEdgeHighSprite.color = edge;
            }

            // Decals ride the ring but stay upright, or half of every turn they'd read upside down.
            for (int i = 0; i < _uprightLabels.Length; i++)
            {
                if (_uprightLabels[i] != null)
                {
                    _uprightLabels[i].rotation = Quaternion.identity;
                }
            }
        }

        void PlaceEdge(Transform edge, float localDegrees)
        {
            if (edge == null)
            {
                return;
            }

            float radians = localDegrees * Mathf.Deg2Rad;
            edge.localPosition = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * _edgeRadius;
            edge.localRotation = Quaternion.Euler(0f, 0f, localDegrees);
        }

        void UpdateShareBar()
        {
            if (_shareBar != null)
            {
                _shareBar.SetCounts(_simulation.Counts);
            }
        }

        void UpdateCaptions(float dt)
        {
            if (_caption == null)
            {
                return;
            }

            RingRoyaleSimulation sim = _simulation;
            RingRoyaleConfig config = sim.Config;

            string headline = _title;
            string subline = string.Empty;
            string footer = _question;

            if (sim.Winner >= 0)
            {
                headline = $"{Tag(config, sim.Winner)} WINS!";
                int left = sim.WinnerMarbles;
                subline = $"with {left} marble{(left == 1 ? string.Empty : "s")} left";
                footer = string.Empty;
            }
            else if (_outTimer > 0f && _outColour >= 0)
            {
                footer = $"{Tag(config, _outColour)} IS OUT!";
            }
            else if (sim.SuddenDeathActive)
            {
                footer = _suddenDeathText;
            }

            if (headline != _captionShown)
            {
                _captionShown = headline;
                _caption.SetCaption(headline);
            }

            _caption.SetSubline(subline);

            if (footer != _footerShown)
            {
                _footerShown = footer;
                _caption.SetFooter(footer);
            }
        }

        static string Tag(RingRoyaleConfig config, int colour)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(config.colours[colour])}>{config.NameOf(colour)}</color>";
        }

        void Play(AudioClip clip, float volume)
        {
            if (clip == null || _voices == null || _voices.Length == 0)
            {
                return;
            }

            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.PlayOneShot(clip, volume);
        }

        void BuildAudio()
        {
            _voices = new AudioSource[_voiceCount];
            for (int i = 0; i < _voices.Length; i++)
            {
                var voiceObject = new GameObject($"Voice {i:00}");
                voiceObject.transform.SetParent(transform, false);
                AudioSource voice = voiceObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                _voices[i] = voice;
            }

            // One chime per colour, a step apart on the pentatonic, so the teams are audible too.
            int colours = _simulation != null && _simulation.Config != null ? _simulation.ColourCount : 4;
            _cloneNotes = new AudioClip[colours];
            for (int c = 0; c < colours; c++)
            {
                float hz = ProceduralToneBank.DegreeToFrequency(392f, c * 2);
                _cloneNotes[c] = ProceduralToneBank.CreateTone(hz, 0.45f, $"Clone{c}");
            }

            _exitThud = ProceduralToneBank.CreateImpactThud(0.8f, 0.22f, 21, "ExitThud");
            _outTone = ProceduralToneBank.CreateTone(98f, 0.9f, "OutTone");
            _outThud = ProceduralToneBank.CreateImpactThud(0.5f, 0.3f, 22, "OutThud");
            _sealThud = ProceduralToneBank.CreateImpactThud(0.6f, 0.45f, 23, "SealThud");
            _suddenDeathRiser = ProceduralToneBank.CreateRiser(1.6f, 80f, 300f, 7, "SuddenDeathRiser");
            _winScale = ProceduralToneBank.CreateScale(5, 392f, 1.8f);
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(RingRoyaleSimulation simulation, ShareBarHud shareBar, CaptionHud caption,
                         ConfettiBurst2D confetti)
        {
            _simulation = simulation;
            _shareBar = shareBar;
            _caption = caption;
            _confetti = confetti;
        }

        /// <summary>Assigned by the scene builder: the x2 arc, the exit edging and the upright decals.</summary>
        public void BindRingDressing(SpriteRenderer[] x2Segments, SpriteRenderer exitEdgeLow,
                                     SpriteRenderer exitEdgeHigh, float edgeRadius, Transform[] uprightLabels)
        {
            _x2Segments = x2Segments;
            _exitEdgeLow = exitEdgeLow.transform;
            _exitEdgeHigh = exitEdgeHigh.transform;
            _exitEdgeLowSprite = exitEdgeLow;
            _exitEdgeHighSprite = exitEdgeHigh;
            _edgeRadius = edgeRadius;
            _uprightLabels = uprightLabels;
        }
    }
}
