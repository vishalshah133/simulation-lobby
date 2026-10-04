using System.Collections.Generic;
using SimulationLobby.Presentation;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Breaker
{
    /// <summary>
    /// Turns ring-breaker state into the video: one melody note per touch, a crack or a shatter on the
    /// segment that was hit, and a ripple explosion + camera kick + glass crash when a whole ring goes.
    /// </summary>
    /// <remarks>
    /// Reads the simulation in <c>LateUpdate</c> and never writes it. It animates segment *visuals*
    /// (the collider is the simulation's), the camera, particles and audio — all presentation.
    /// Pops and ripples run on <c>Time.time</c>, which is fine here: nothing it drives feeds back into
    /// the run.
    /// </remarks>
    [RequireComponent(typeof(BreakerSimulation))]
    public sealed class BreakerPresenter : MonoBehaviour
    {
        [SerializeField] BreakerSimulation _simulation;
        [SerializeField] VersusScoreboardHud _hud;
        [SerializeField] BounceMelodyPlayer _melody;
        [SerializeField] ShatterBurst2D _shatter;
        [SerializeField] Camera _camera;

        [SerializeField] string _title = "GUESS THE SONG";
        [SerializeField] string _question = "CAN IT BREAK ALL THE RINGS?";

        [Header("Feel")]
        [Tooltip("Seconds for a broken segment to pop (swell + fade) before it disappears.")]
        [Range(0.02f, 0.4f)] public float popSeconds = 0.09f;

        [Tooltip("Seconds for a ring-clear explosion to ripple all the way round from the exit point.")]
        [Range(0f, 1f)] public float rippleSeconds = 0.35f;

        [Range(0f, 0.6f)] public float shakeAmount = 0.18f;
        [Range(0.05f, 1f)] public float shakeSeconds = 0.3f;

        [Range(0f, 1f)] public float breakCrackVolume = 0.18f;
        [Range(0f, 1f)] public float ringShatterVolume = 0.55f;

        [Tooltip("Seconds the 'RING n BROKEN!' line stays up.")]
        [Range(0.2f, 3f)] public float resultSeconds = 0.9f;

        struct Pop
        {
            public Transform visual;
            public float start;
            public Color color;
        }

        struct Pending
        {
            public SegmentEvent segmentEvent;
            public float at;
        }

        readonly List<Pop> _pops = new List<Pop>();
        readonly List<Pending> _pending = new List<Pending>();

        MaterialPropertyBlock _block;
        AudioSource _sfx;
        AudioClip _crack;
        AudioClip _ringCrash;
        int _runId = -1;
        int _eventIndex;
        int _lastHits;
        float _shakeUntil;
        float _resultUntil;
        Vector3 _cameraHome;

        void Reset()
        {
            _simulation = GetComponent<BreakerSimulation>();
        }

        void Start()
        {
            _block = new MaterialPropertyBlock();

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;
            _crack = ProceduralToneBank.CreateShatter(0.1f, 21, "SegmentCrack");
            _ringCrash = ProceduralToneBank.CreateShatter(1f, 22, "RingCrash");

            if (_camera != null)
            {
                _cameraHome = _camera.transform.position;
            }

            if (_hud != null)
            {
                _hud.SetTitle(_title);
                _hud.SetQuestion(_question);
                _hud.SetSides("RINGS", "BALL");
            }
        }

        void LateUpdate()
        {
            if (_simulation == null || _simulation.RingCount == 0)
            {
                return;
            }

            if (_simulation.RunId != _runId)
            {
                ResetForNewRun();
            }

            PlayNotes();
            ConsumeEvents();
            RunPending();
            RunPops();
            ShakeCamera();
            UpdateHud();
        }

        void ResetForNewRun()
        {
            _runId = _simulation.RunId;
            _eventIndex = 0;
            _lastHits = 0;
            _pops.Clear();
            _pending.Clear();
            _shakeUntil = 0f;
            _resultUntil = 0f;

            for (int ring = 0; ring < _simulation.RingCount; ring++)
            {
                SegmentedRing2D segments = _simulation.Ring(ring);
                for (int i = 0; i < segments.SegmentCount; i++)
                {
                    Transform visual = segments.GetVisual(i);
                    visual.gameObject.SetActive(true);
                    visual.localScale = Vector3.one;
                    SetTint(visual, Color.white);
                }
            }

            if (_shatter != null)
            {
                _shatter.Clear();
            }

            if (_melody != null)
            {
                _melody.ResetMelody();
            }
        }

        void PlayNotes()
        {
            if (_melody == null)
            {
                return;
            }

            // One note per touch, even if several landed in one rendered frame: notes played must
            // equal hits, which doubles as a cheap missed-collision check.
            int pending = _simulation.Hits - _lastHits;
            _lastHits = _simulation.Hits;

            float velocity01 = Mathf.Clamp01(_simulation.LastImpactSpeed / Mathf.Max(0.01f, _simulation.Speed * 2f));
            for (int i = 0; i < pending; i++)
            {
                _melody.PlayNote(velocity01);
            }
        }

        void ConsumeEvents()
        {
            IReadOnlyList<SegmentEvent> events = _simulation.Events;
            for (; _eventIndex < events.Count; _eventIndex++)
            {
                SegmentEvent e = events[_eventIndex];
                SegmentedRing2D ring = _simulation.Ring(e.ring);

                switch (e.kind)
                {
                    case SegmentEventKind.Cracked:
                        // Darken toward the ring's last hit point, so damage is readable at a glance.
                        float health = e.hitPointsLeft / (float)Mathf.Max(1, _simulation.MaxHitPoints(e.ring));
                        SetTint(ring.GetVisual(e.segment), Color.Lerp(new Color(0.35f, 0.35f, 0.4f, 1f), Color.white, health));
                        Burst(e.position, e.outward, ring.color, 0.35f, e.ring);
                        PlaySfx(_crack, breakCrackVolume * 0.6f);
                        break;

                    case SegmentEventKind.Broken:
                        StartPop(ring, e.segment);
                        Burst(e.position, e.outward, ring.color, 1f, e.ring);
                        PlaySfx(_crack, breakCrackVolume);
                        break;

                    case SegmentEventKind.Shattered:
                        QueueRipple(e, ring);
                        break;
                }
            }
        }

        /// <summary>
        /// A ring clearing explodes outward from where the ball got out: each remaining segment goes
        /// after a delay proportional to its angular distance from the exit, so the blast visibly
        /// travels round the ring instead of everything vanishing in one frame.
        /// </summary>
        void QueueRipple(SegmentEvent e, SegmentedRing2D ring)
        {
            bool firstOfRing = _pending.Count == 0 || _pending[_pending.Count - 1].segmentEvent.ring != e.ring;
            if (firstOfRing)
            {
                _shakeUntil = Time.time + shakeSeconds;
                _resultUntil = Time.time + resultSeconds;
                PlaySfx(_ringCrash, ringShatterVolume);
                Shockwave(ring.transform.position, ring.OuterRadius + 0.8f, ring.color);
            }

            Vector2 exit = ExitDirection(e.ring);
            float angle = Vector2.Angle(exit, e.outward) / 180f;
            _pending.Add(new Pending { segmentEvent = e, at = Time.time + angle * rippleSeconds });
        }

        Vector2 ExitDirection(int ring)
        {
            // The most recent Broken event on this ring is the hole the ball used.
            IReadOnlyList<SegmentEvent> events = _simulation.Events;
            for (int i = Mathf.Min(_eventIndex, events.Count - 1); i >= 0; i--)
            {
                if (events[i].ring == ring && events[i].kind == SegmentEventKind.Broken)
                {
                    return events[i].outward;
                }
            }

            return Vector2.up;
        }

        void RunPending()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < _pending[i].at)
                {
                    continue;
                }

                SegmentEvent e = _pending[i].segmentEvent;
                SegmentedRing2D ring = _simulation.Ring(e.ring);
                StartPop(ring, e.segment);
                // Where the segment is now, not where it was: the ring kept turning during the ripple.
                Burst(ring.SegmentCenter(e.segment), ring.SegmentOutward(e.segment), ring.color, 0.8f, e.ring);
                _pending.RemoveAt(i);
            }
        }

        void StartPop(SegmentedRing2D ring, int segment)
        {
            Transform visual = ring.GetVisual(segment);
            if (visual.gameObject.activeSelf)
            {
                _pops.Add(new Pop { visual = visual, start = Time.time, color = ring.color });
            }
        }

        void RunPops()
        {
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                Pop pop = _pops[i];
                float t = (Time.time - pop.start) / popSeconds;
                if (t >= 1f)
                {
                    pop.visual.gameObject.SetActive(false);
                    _pops.RemoveAt(i);
                    continue;
                }

                // Swell and flash white, then gone: the hit lands before the debris takes over.
                pop.visual.localScale = Vector3.one * (1f + 0.18f * t);
                SetTint(pop.visual, Color.Lerp(Color.white * 2f, new Color(1f, 1f, 1f, 0f), t));
            }
        }

        void ShakeCamera()
        {
            if (_camera == null)
            {
                return;
            }

            float remaining = _shakeUntil - Time.time;
            if (remaining <= 0f)
            {
                _camera.transform.position = _cameraHome;
                return;
            }

            float strength = shakeAmount * (remaining / shakeSeconds);
            float t = Time.time * 60f;
            _camera.transform.position = _cameraHome + new Vector3(Mathf.Sin(t * 1.3f), Mathf.Cos(t * 1.7f), 0f) * strength;
        }

        void UpdateHud()
        {
            if (_hud == null)
            {
                return;
            }

            int total = _simulation.RingCount;
            _hud.SetScore(total - _simulation.RingsBroken, _simulation.RingsBroken);
            _hud.SetRoundLabel(_simulation.IsFree
                ? "ALL RINGS BROKEN"
                : $"RING {_simulation.CurrentRing + 1} / {total}");
            if (_simulation.HasClock)
            {
                // Simulated time, not Time.time: the clock is part of the run and must match the seed.
                float left = _simulation.SecondsLeft;
                int whole = Mathf.CeilToInt(left);
                _hud.SetCounterText($"{whole / 60}:{whole % 60:00}", left <= 10f);
            }
            else
            {
                _hud.SetCounter("HITS", _simulation.Hits);
            }

            if (_simulation.TimeUp)
            {
                _hud.ShowResult($"TIME'S UP! {_simulation.RingsBroken} / {total} RINGS", false);
            }
            else if (_simulation.Won)
            {
                _hud.ShowResult(_simulation.HasClock
                    ? $"BROKE FREE WITH {_simulation.SecondsLeft:0.0}s LEFT!"
                    : "IT BROKE FREE!", true);
            }
            else if (Time.time < _resultUntil && _simulation.RingsBroken > 0)
            {
                _hud.ShowResult($"RING {_simulation.RingsBroken} BROKEN!", true);
            }
            else
            {
                _hud.ShowResult(null, false);
            }
        }

        /// <summary>Each ring breaks into its own debris shape (ring index = shape), so later rings look new.</summary>
        void Burst(Vector2 position, Vector2 outward, Color color, float intensity, int ring)
        {
            if (_shatter != null)
            {
                _shatter.Burst(position, outward, color, intensity, ring);
            }
        }

        void Shockwave(Vector2 center, float radius, Color color)
        {
            if (_shatter != null)
            {
                _shatter.Shockwave(center, radius, color);
            }
        }

        void PlaySfx(AudioClip clip, float volume)
        {
            if (_sfx != null && clip != null && volume > 0f)
            {
                _sfx.PlayOneShot(clip, volume);
            }
        }

        void SetTint(Transform visual, Color tint)
        {
            var renderer = visual.GetComponent<Renderer>();
            renderer.GetPropertyBlock(_block);
            _block.SetColor("_Color", tint);
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(BreakerSimulation simulation, VersusScoreboardHud hud, BounceMelodyPlayer melody,
                         ShatterBurst2D shatter, Camera camera, string title, string question)
        {
            _simulation = simulation;
            _hud = hud;
            _melody = melody;
            _shatter = shatter;
            _camera = camera;
            _title = title;
            _question = question;
        }
    }
}
