using System.Collections.Generic;
using SimulationLobby.Presentation;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.ReachCore
{
    /// <summary>
    /// Turns "reach the core" state into the video: one melody note per touch, a crack or shatter on the
    /// segment that was hit, a ripple explosion when a ring goes, and the tension layer: a core that beats
    /// faster as time runs out, a wall that visibly closes in, a camera that pushes toward the core and a
    /// tick for each of the last 10 seconds. Every text line is a short plain sentence.
    /// </summary>
    /// <remarks>
    /// Reads the simulation in <c>LateUpdate</c> and never writes it. Animates visuals, the camera,
    /// particles and audio only; nothing it drives feeds back into the run, so <c>Time.time</c> is fine here.
    /// </remarks>
    [RequireComponent(typeof(CoreSimulation))]
    public sealed class CorePresenter : MonoBehaviour
    {
        [SerializeField] CoreSimulation _simulation;
        [SerializeField] VersusScoreboardHud _hud;
        [SerializeField] BounceMelodyPlayer _melody;
        [SerializeField] ShatterBurst2D _shatter;
        [SerializeField] Camera _camera;

        [Header("Core + wall visuals (placed by the scene builder)")]
        [SerializeField] SpriteRenderer _coreBody;
        [SerializeField] SpriteRenderer _coreHalo;
        [SerializeField] SpriteRenderer _wallVisual;
        [Tooltip("World radius of the wall sprite at scale 1.")]
        [SerializeField] float _wallSpriteRadius = 1f;

        [SerializeField] string _title = "GUESS THE SONG";
        [SerializeField] string _question = "CAN IT BREAK IN TO THE CORE?";

        [Header("Feel")]
        [Range(0.02f, 0.4f)] public float popSeconds = 0.09f;
        [Range(0f, 1f)] public float rippleSeconds = 0.35f;
        [Range(0f, 0.6f)] public float shakeAmount = 0.18f;
        [Range(0.05f, 1f)] public float shakeSeconds = 0.3f;
        [Range(0f, 1f)] public float breakCrackVolume = 0.18f;
        [Range(0f, 1f)] public float ringShatterVolume = 0.55f;
        [Range(0.2f, 3f)] public float resultSeconds = 1.1f;

        [Header("Tension")]
        [Tooltip("Seconds left at which the clock turns red, the core races and the ticking starts.")]
        [Range(3f, 30f)] public float dangerSeconds = 10f;

        [Tooltip("Camera pushes in to this fraction of its size by the time every ring is gone.")]
        [Range(0.6f, 1f)] public float pushInFraction = 0.82f;

        [Range(0f, 1f)] public float tickVolume = 0.35f;

        struct Pop
        {
            public Transform visual;
            public float start;
        }

        struct Pending
        {
            public CoreSegmentEvent segmentEvent;
            public float at;
        }

        readonly List<Pop> _pops = new List<Pop>();
        readonly List<Pending> _pending = new List<Pending>();

        MaterialPropertyBlock _block;
        AudioSource _sfx;
        AudioClip _crack;
        AudioClip _ringCrash;
        AudioClip _tick;
        AudioClip _coreChime;
        int _runId = -1;
        int _eventIndex;
        int _lastHits;
        int _lastTickSecond;
        bool _coreBurstDone;
        float _shakeUntil;
        float _resultUntil;
        float _lastRingBroken;
        float _beatPhase;
        float _wallShown;
        float _pushShown;
        Vector3 _cameraHome;
        float _cameraHomeSize;
        Color _coreColor;

        void Reset()
        {
            _simulation = GetComponent<CoreSimulation>();
        }

        void Start()
        {
            _block = new MaterialPropertyBlock();

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;
            _crack = ProceduralToneBank.CreateShatter(0.1f, 21, "SegmentCrack");
            _ringCrash = ProceduralToneBank.CreateShatter(1f, 22, "RingCrash");
            _tick = ProceduralToneBank.CreateClick(0.7f, 31, "ClockTick");
            _coreChime = ProceduralToneBank.CreateGlassClink(0.8f, 1.6f, 33, "CoreChime");

            if (_camera != null)
            {
                _cameraHome = _camera.transform.position;
                _cameraHomeSize = _camera.orthographicSize;
            }

            if (_coreBody != null)
            {
                _coreColor = _coreBody.color;
            }

            if (_hud != null)
            {
                _hud.SetTitle(_title);
                _hud.SetQuestion(_question);
                _hud.SetScoreVisible(false);
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
            AnimateWall();
            AnimateCore();
            TickClock();
            MoveCamera();
            UpdateHud();
        }

        void ResetForNewRun()
        {
            _runId = _simulation.RunId;
            _eventIndex = 0;
            _lastHits = 0;
            _lastTickSecond = int.MaxValue;
            _coreBurstDone = false;
            _pops.Clear();
            _pending.Clear();
            _shakeUntil = 0f;
            _resultUntil = 0f;
            _lastRingBroken = 0f;
            _beatPhase = 0f;
            _wallShown = _simulation.WallRadius;
            _pushShown = 0f;

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
            IReadOnlyList<CoreSegmentEvent> events = _simulation.Events;
            for (; _eventIndex < events.Count; _eventIndex++)
            {
                CoreSegmentEvent e = events[_eventIndex];
                SegmentedRing2D ring = _simulation.Ring(e.ring);

                switch (e.kind)
                {
                    case CoreSegmentEventKind.Cracked:
                        // Darken toward the segment's last hit point, so damage is readable at a glance.
                        float health = e.hitPointsLeft / (float)Mathf.Max(1, _simulation.MaxHitPoints(e.ring));
                        SetTint(ring.GetVisual(e.segment), Color.Lerp(new Color(0.35f, 0.35f, 0.4f, 1f), Color.white, health));
                        Burst(e.position, e.outward, ring.color, 0.35f, e.ring);
                        PlaySfx(_crack, breakCrackVolume * 0.6f);
                        break;

                    case CoreSegmentEventKind.Broken:
                        StartPop(ring, e.segment);
                        Burst(e.position, e.outward, ring.color, 1f, e.ring);
                        PlaySfx(_crack, breakCrackVolume);
                        break;

                    case CoreSegmentEventKind.Shattered:
                        QueueRipple(e, ring);
                        break;
                }
            }
        }

        /// <summary>
        /// A ring clearing explodes outward from where the ball got through: each remaining segment goes
        /// after a delay proportional to its angular distance from the hole, so the blast visibly
        /// travels round the ring instead of everything vanishing in one frame.
        /// </summary>
        void QueueRipple(CoreSegmentEvent e, SegmentedRing2D ring)
        {
            bool firstOfRing = _pending.Count == 0 || _pending[_pending.Count - 1].segmentEvent.ring != e.ring;
            if (firstOfRing)
            {
                _shakeUntil = Time.time + shakeSeconds;
                _resultUntil = Time.time + resultSeconds;
                _lastRingBroken = _simulation.RingsBroken;
                PlaySfx(_ringCrash, ringShatterVolume);
                Shockwave(ring.transform.position, ring.OuterRadius + 0.8f, ring.color);
            }

            Vector2 hole = HoleDirection(e.ring);
            float angle = Vector2.Angle(hole, e.outward) / 180f;
            _pending.Add(new Pending { segmentEvent = e, at = Time.time + angle * rippleSeconds });
        }

        Vector2 HoleDirection(int ring)
        {
            // The most recent Broken event on this ring is the hole the ball used.
            IReadOnlyList<CoreSegmentEvent> events = _simulation.Events;
            for (int i = Mathf.Min(_eventIndex, events.Count - 1); i >= 0; i--)
            {
                if (events[i].ring == ring && events[i].kind == CoreSegmentEventKind.Broken)
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

                CoreSegmentEvent e = _pending[i].segmentEvent;
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
                _pops.Add(new Pop { visual = visual, start = Time.time });
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

                pop.visual.localScale = Vector3.one * (1f + 0.18f * t);
                SetTint(pop.visual, Color.Lerp(Color.white * 2f, new Color(1f, 1f, 1f, 0f), t));
            }
        }

        /// <summary>The wall glides in to the simulation's radius, so "the arena closes behind you" is a visible cause.</summary>
        void AnimateWall()
        {
            if (_wallVisual == null)
            {
                return;
            }

            _wallShown = Mathf.MoveTowards(_wallShown, _simulation.WallRadius, 6f * Time.deltaTime);
            float scale = _wallShown / Mathf.Max(0.01f, _wallSpriteRadius);
            _wallVisual.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// The core is the prize and the clock: it glows brighter as rings fall and beats faster as time
        /// drains, so the viewer feels the deadline without reading the number.
        /// </summary>
        void AnimateCore()
        {
            if (_coreBody == null)
            {
                return;
            }

            float progress = _simulation.RingsBroken / (float)Mathf.Max(1, _simulation.RingCount);
            float timeUsed = _simulation.HasClock ? 1f - _simulation.SecondsLeft / _simulation.TimeLimit : 0f;
            bool danger = _simulation.HasClock && _simulation.SecondsLeft <= dangerSeconds && !_simulation.Won;

            float rate = Mathf.Lerp(1.1f, 2.4f, timeUsed) + (danger ? 1.6f : 0f);
            _beatPhase += rate * Time.deltaTime * Mathf.PI * 2f;
            float beat = 0.5f + 0.5f * Mathf.Sin(_beatPhase);

            float radius = _simulation.CoreRadius;
            float body = radius * (1f + 0.08f * beat + (danger ? 0.12f * beat : 0f));
            _coreBody.transform.localScale = Vector3.one * body * 2f;

            Color tint = danger ? Color.Lerp(_coreColor, new Color(1f, 0.35f, 0.3f), beat) : _coreColor;
            _coreBody.color = tint;

            if (_coreHalo != null)
            {
                float halo = radius * (2.2f + 1.4f * progress + 0.6f * beat);
                _coreHalo.transform.localScale = Vector3.one * halo * 2f;
                Color haloColor = tint;
                haloColor.a = Mathf.Lerp(0.18f, 0.55f, progress) * (0.7f + 0.3f * beat);
                _coreHalo.color = haloColor;
            }

            if (_simulation.Won && !_coreBurstDone)
            {
                _coreBurstDone = true;
                _shakeUntil = Time.time + shakeSeconds * 2f;
                PlaySfx(_coreChime, 0.8f);
                Shockwave(Vector2.zero, 3.2f, _coreColor);
                Burst(Vector2.zero, Vector2.up, _coreColor, 1.6f, 4);
            }
        }

        /// <summary>One tick per whole second left once the danger window opens. It is the sound of the deadline.</summary>
        void TickClock()
        {
            if (!_simulation.HasClock || _simulation.Won || _simulation.TimeUp || _simulation.IsComplete)
            {
                return;
            }

            int whole = Mathf.CeilToInt(_simulation.SecondsLeft);
            if (whole <= dangerSeconds && whole < _lastTickSecond && whole > 0)
            {
                float urgency = 1f - whole / dangerSeconds;
                _sfx.pitch = 1f;
                PlaySfx(_tick, tickVolume * Mathf.Lerp(0.6f, 1.2f, urgency));
            }

            _lastTickSecond = Mathf.Min(_lastTickSecond, whole);
        }

        void MoveCamera()
        {
            if (_camera == null)
            {
                return;
            }

            // Push in toward the core as the rings fall: the frame tightens while the stakes rise.
            float target = _simulation.RingsBroken / (float)Mathf.Max(1, _simulation.RingCount);
            _pushShown = Mathf.MoveTowards(_pushShown, target, 0.6f * Time.deltaTime);
            _camera.orthographicSize = Mathf.Lerp(_cameraHomeSize, _cameraHomeSize * pushInFraction, _pushShown);

            float remaining = _shakeUntil - Time.time;
            Vector3 offset = Vector3.zero;
            if (remaining > 0f)
            {
                float strength = shakeAmount * (remaining / shakeSeconds);
                float t = Time.time * 60f;
                offset = new Vector3(Mathf.Sin(t * 1.3f), Mathf.Cos(t * 1.7f), 0f) * strength;
            }

            _camera.transform.position = _cameraHome + offset;
        }

        void UpdateHud()
        {
            if (_hud == null)
            {
                return;
            }

            int left = _simulation.RingsLeft;
            _hud.SetRoundLabel(_simulation.InFinalChamber
                ? "THE CORE IS EXPOSED"
                : left == 1 ? "1 RING LEFT" : $"{left} RINGS LEFT");

            float secondsLeft = _simulation.SecondsLeft;
            bool danger = _simulation.HasClock && secondsLeft <= dangerSeconds && !_simulation.Won;
            if (_simulation.HasClock)
            {
                // Simulated time, not Time.time: the clock is part of the run and must match the seed.
                int whole = Mathf.CeilToInt(secondsLeft);
                _hud.SetCounterText($"{whole / 60}:{whole % 60:00}", danger);
            }
            else
            {
                _hud.SetCounter("HITS", _simulation.Hits);
            }

            float elapsed = _simulation.HasClock ? _simulation.TimeLimit - secondsLeft : 0f;

            if (_simulation.TimeUp)
            {
                _hud.ShowResult(_simulation.InFinalChamber
                    ? "SO CLOSE! THE CORE SURVIVED"
                    : $"TIME'S UP! {(left == 1 ? "1 RING" : left + " RINGS")} STILL STANDING", false);
            }
            else if (_simulation.Won)
            {
                _hud.ShowResult(_simulation.HasClock
                    ? $"REACHED THE CORE WITH {secondsLeft:0.0}s LEFT!"
                    : "IT REACHED THE CORE!", true);
            }
            else if (Time.time < _resultUntil && _simulation.RingsBroken > 0 && !_simulation.InFinalChamber)
            {
                int togo = _simulation.RingsLeft;
                _hud.ShowResult($"RING {(int)_lastRingBroken} BROKEN! {togo} TO GO", true);
            }
            else if (Time.time < _resultUntil && _simulation.InFinalChamber)
            {
                _hud.ShowResult("ALL RINGS BROKEN!", true);
            }
            else if (danger)
            {
                _hud.ShowNeutral("TIME IS RUNNING OUT!");
            }
            else if (_simulation.InFinalChamber)
            {
                _hud.ShowNeutral("HIT THE CORE!");
            }
            else if (left == 1)
            {
                _hud.ShowNeutral("LAST RING! KEEP GOING!");
            }
            else if (elapsed < 4f)
            {
                _hud.ShowNeutral("BREAK THROUGH EVERY RING!");
            }
            else
            {
                _hud.ShowResult(null, false);
            }
        }

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
        public void Bind(CoreSimulation simulation, VersusScoreboardHud hud, BounceMelodyPlayer melody,
                         ShatterBurst2D shatter, Camera camera, SpriteRenderer coreBody, SpriteRenderer coreHalo,
                         SpriteRenderer wallVisual, float wallSpriteRadius, string title, string question)
        {
            _simulation = simulation;
            _hud = hud;
            _melody = melody;
            _shatter = shatter;
            _camera = camera;
            _coreBody = coreBody;
            _coreHalo = coreHalo;
            _wallVisual = wallVisual;
            _wallSpriteRadius = wallSpriteRadius;
            _title = title;
            _question = question;
        }
    }
}
