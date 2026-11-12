using System.Collections.Generic;
using SimulationLobby.Presentation;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.HauntedPumpkin
{
    /// <summary>
    /// Drives the HUD, the debris, the bats, the candle and the audio from run state. Reads in
    /// <c>LateUpdate</c> and never writes to the run.
    /// </summary>
    [RequireComponent(typeof(HauntedPumpkinSimulation))]
    public sealed class HauntedPumpkinPresenter : MonoBehaviour
    {
        [SerializeField] HauntedPumpkinSimulation _simulation;
        [SerializeField] HauntedPumpkinHud _hud;
        [SerializeField] BounceMelodyPlayer _melody;
        [SerializeField] ShatterBurst2D _shatter;
        [SerializeField] SpriteRenderer _glow;
        [SerializeField] Sprite _circleSprite;
        [SerializeField] Sprite _squareSprite;

        [Header("Words")]
        [SerializeField] string _hook = "ESCAPE BEFORE THE CANDLE DIES?";
        [SerializeField] string _guess = "GUESS THE SONG";
        [SerializeField] string _almostOut = "THE CANDLE IS ALMOST OUT...";
        [SerializeField] string _winText = "THEY ESCAPED!";
        [SerializeField] string _loseText = "THE PUMPKIN WINS";
        [SerializeField] string _whyText = "Every break splits a ghost, so more ghosts means more breaks.";

        [Header("Feel")]
        [Tooltip("Seconds for a broken segment to pop (swell + fade) before it disappears.")]
        [Range(0.02f, 0.4f)] public float popSeconds = 0.1f;

        [Tooltip("Candle fraction below which the 'almost out' line and the faster flicker kick in.")]
        [Range(0.05f, 0.5f)] public float lowCandle = 0.25f;

        [Range(1, 6)] public int maxNotesPerFrame = 2;

        [Range(0f, 1f)] public float bedVolume = 0.22f;
        [Range(0f, 1f)] public float sfxVolume = 0.6f;

        static readonly Color BatColor = new Color(0.72f, 0.38f, 1f, 1f);
        static readonly Color WinColor = new Color(0.55f, 1f, 0.55f, 1f);
        static readonly Color LoseColor = new Color(1f, 0.5f, 0.12f, 1f);
        static readonly Color StatusColor = new Color(1f, 0.82f, 0.4f, 1f);

        sealed class Bat
        {
            public Transform root;
            public SpriteRenderer body;
            public Transform wingLeft;
            public Transform wingRight;
            public SpriteRenderer wingLeftRenderer;
            public SpriteRenderer wingRightRenderer;
            public float age;
            public float life;
            public float speed;
            public float drift;
            public float phase;
            public bool active;
        }

        struct Pop
        {
            public Transform visual;
            public float start;
        }

        const int BatPoolSize = 90;

        readonly List<Bat> _bats = new List<Bat>(BatPoolSize);
        readonly List<Pop> _pops = new List<Pop>();

        MaterialPropertyBlock _block;
        System.Random _random;
        AudioSource _sfx;
        AudioSource _bedSource;
        AudioClip _crack;
        AudioClip _shatterClip;
        AudioClip _splitClip;
        AudioClip _creak;
        AudioClip _whoosh;

        int _runId = -1;
        int _eventIndex;
        int _batIndex;
        int _lastRingsBroken;
        int _nextBat;
        float _ringOpenUntil;
        int _ringOpenNumber;
        float _lastWhoosh;
        bool _resultShown;
        float _resultAt;
        Transform _batRoot;

        void Reset()
        {
            _simulation = GetComponent<HauntedPumpkinSimulation>();
        }

        void Start()
        {
            _block = new MaterialPropertyBlock();
            _random = new System.Random(1031);

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;

            _bedSource = gameObject.AddComponent<AudioSource>();
            _bedSource.playOnAwake = false;
            _bedSource.spatialBlend = 0f;
            _bedSource.loop = true;
            _bedSource.volume = bedVolume;
            _bedSource.clip = ProceduralToneBank.CreateAmbientBed(4f, 55f, "HauntBed");
            _bedSource.Play();

            _crack = ProceduralToneBank.CreateShatter(0.12f, 31, "HallCrack");
            _shatterClip = ProceduralToneBank.CreateShatter(0.45f, 32, "HallBreak");
            _splitClip = ProceduralToneBank.CreateClick(1.5f, 33, "HallSplit");
            _whoosh = ProceduralToneBank.CreateWhoosh(0.5f, 34, "HallBat");
            _creak = CreateCreak(1.1f);

            BuildBatPool();

            if (_hud != null)
            {
                _hud.SetCaption(_hook);
                _hud.SetSubline(_guess);
                _hud.SetWhy(string.Empty);
                _hud.SetStatus(string.Empty, StatusColor);
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

            ConsumeSegmentEvents();
            ConsumeBatEvents();
            WatchRings();
            RunPops();
            StyleGhosts();
            RunBats(Time.deltaTime);
            WatchResult();
            UpdateScenery();
            UpdateHud();
        }

        void ResetForNewRun()
        {
            _runId = _simulation.RunId;
            _eventIndex = 0;
            _batIndex = 0;
            _lastRingsBroken = 0;
            _ringOpenUntil = 0f;
            _resultShown = false;
            _pops.Clear();

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

            for (int i = 0; i < _bats.Count; i++)
            {
                _bats[i].active = false;
                _bats[i].root.gameObject.SetActive(false);
            }

            if (_shatter != null)
            {
                _shatter.Clear();
            }

            if (_melody != null)
            {
                _melody.ResetMelody();
            }

            if (_hud != null)
            {
                _hud.SetCaption(_hook);
                _hud.SetSubline(_guess);
                _hud.SetWhy(string.Empty);
                _hud.SetOverlay(Color.clear);
            }
        }

        // ---------------------------------------------------------------- segments

        void ConsumeSegmentEvents()
        {
            IReadOnlyList<SegmentEvent> events = _simulation.Events;
            int notes = 0;
            bool crackHeard = false;
            bool breakHeard = false;
            bool splitHeard = false;

            for (; _eventIndex < events.Count; _eventIndex++)
            {
                SegmentEvent e = events[_eventIndex];
                SegmentedRing2D ring = _simulation.Ring(e.ring);

                if (e.kind == SegmentEventKind.Cracked)
                {
                    // Darken toward the last hit point, so damage is readable at a glance.
                    float health = e.hitPointsLeft / (float)Mathf.Max(1, _simulation.MaxHitPoints(e.ring));
                    SetTint(ring.GetVisual(e.segment), Color.Lerp(new Color(0.4f, 0.35f, 0.4f, 1f), Color.white, health));
                    Burst(e.position, e.outward, ring.color, 0.35f, e.ring);
                    crackHeard = true;
                    continue;
                }

                StartPop(ring, e.segment);
                Burst(e.position, e.outward, ring.color, 1f, e.ring);
                breakHeard = true;
                splitHeard |= e.split;

                // One tune note per break; a dozen in one frame is noise, not music.
                if (_melody != null && notes < maxNotesPerFrame)
                {
                    _melody.PlayNote(1f);
                    notes++;
                }
            }

            if (breakHeard)
            {
                Play(_shatterClip, sfxVolume * 0.7f);
            }
            else if (crackHeard)
            {
                Play(_crack, sfxVolume * 0.4f);
            }

            if (splitHeard)
            {
                Play(_splitClip, sfxVolume * 0.5f);
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

                // Swell and flash white, then gone: the hit lands before the debris takes over.
                pop.visual.localScale = Vector3.one * (1f + 0.2f * t);
                SetTint(pop.visual, Color.Lerp(Color.white * 2f, new Color(1f, 1f, 1f, 0f), t));
            }
        }

        void WatchRings()
        {
            int broken = _simulation.RingsBroken;
            if (broken <= _lastRingsBroken)
            {
                return;
            }

            // A ghost got through ring `broken`: it creaks open.
            int ringIndex = Mathf.Clamp(broken - 1, 0, _simulation.RingCount - 1);
            SegmentedRing2D ring = _simulation.Ring(ringIndex);
            _lastRingsBroken = broken;
            _ringOpenNumber = broken;
            _ringOpenUntil = Time.time + 1.3f;
            Play(_creak, sfxVolume);
            if (_shatter != null)
            {
                _shatter.Shockwave(ring.transform.position, ring.OuterRadius + 0.5f, ring.color);
            }
        }

        // ---------------------------------------------------------------- ghosts

        void StyleGhosts()
        {
            IReadOnlyList<GhostBody> ghosts = _simulation.Ghosts;
            float diameter = _simulation.GhostRadius * 2f;
            for (int i = 0; i < ghosts.Count; i++)
            {
                GhostBody ghost = ghosts[i];
                if (ghost.StyledWeight == ghost.Weight || ghost.Visual == null)
                {
                    continue;
                }

                ghost.StyledWeight = ghost.Weight;
                // A body standing for several ghosts (cap reached) swells a little, so the pile reads as more.
                ghost.Visual.localScale = Vector3.one * (diameter * (1f + 0.12f * Mathf.Min(ghost.Weight - 1, 6)));
            }
        }

        // ---------------------------------------------------------------- bats

        void BuildBatPool()
        {
            _batRoot = new GameObject("Bats").transform;
            for (int i = 0; i < BatPoolSize; i++)
            {
                var root = new GameObject($"Bat_{i:00}").transform;
                root.SetParent(_batRoot, false);

                SpriteRenderer body = MakeSprite("Body", root, _circleSprite, new Vector3(0.3f, 0.34f, 1f), 12);
                body.color = BatColor;
                Transform leftWing;
                Transform rightWing;
                SpriteRenderer leftRenderer = MakeWing("WingL", root, -1f, out leftWing);
                SpriteRenderer rightRenderer = MakeWing("WingR", root, 1f, out rightWing);

                root.gameObject.SetActive(false);
                _bats.Add(new Bat
                {
                    root = root,
                    body = body,
                    wingLeft = leftWing,
                    wingRight = rightWing,
                    wingLeftRenderer = leftRenderer,
                    wingRightRenderer = rightRenderer
                });
            }
        }

        SpriteRenderer MakeWing(string name, Transform parent, float side, out Transform wing)
        {
            SpriteRenderer renderer = MakeSprite(name, parent, _squareSprite, new Vector3(0.46f, 0.14f, 1f), 11);
            renderer.color = BatColor * 0.85f;
            renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, 1f);
            wing = renderer.transform;
            wing.localPosition = new Vector3(0.26f * side, 0.04f, 0f);
            return renderer;
        }

        static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Vector3 scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        void ConsumeBatEvents()
        {
            IReadOnlyList<BatEvent> events = _simulation.BatEvents;
            bool any = false;
            for (; _batIndex < events.Count; _batIndex++)
            {
                BatEvent e = events[_batIndex];
                int count = Mathf.Min(e.weight, 4);
                for (int i = 0; i < count; i++)
                {
                    SpawnBat(e.position);
                }

                any = true;
            }

            if (any && Time.time - _lastWhoosh > 0.12f)
            {
                _lastWhoosh = Time.time;
                Play(_whoosh, sfxVolume * 0.35f);
            }
        }

        void SpawnBat(Vector2 position)
        {
            if (_bats.Count == 0)
            {
                return;
            }

            // Round-robin: a flood recycles the oldest bat rather than allocating.
            Bat bat = _bats[_nextBat];
            _nextBat = (_nextBat + 1) % _bats.Count;

            bat.active = true;
            bat.age = 0f;
            bat.life = 2.2f + (float)_random.NextDouble() * 0.8f;
            bat.speed = 4.5f + (float)_random.NextDouble() * 2.5f;
            bat.drift = ((float)_random.NextDouble() - 0.5f) * 1.6f;
            bat.phase = (float)_random.NextDouble() * 6.28f;
            bat.root.position = new Vector3(position.x, position.y, 0f);
            bat.root.gameObject.SetActive(true);
        }

        void RunBats(float dt)
        {
            for (int i = 0; i < _bats.Count; i++)
            {
                Bat bat = _bats[i];
                if (!bat.active)
                {
                    continue;
                }

                bat.age += dt;
                if (bat.age >= bat.life)
                {
                    bat.active = false;
                    bat.root.gameObject.SetActive(false);
                    continue;
                }

                // Always upward: "escaped" has to read as one direction, away from the pumpkin.
                float wobble = Mathf.Sin(bat.age * 5f + bat.phase) * 1.1f;
                Vector3 position = bat.root.position;
                position.x += (bat.drift + wobble) * dt;
                position.y += bat.speed * dt;
                bat.root.position = position;

                float flap = Mathf.Sin(bat.age * 22f + bat.phase);
                bat.wingLeft.localRotation = Quaternion.Euler(0f, 0f, 28f * flap);
                bat.wingRight.localRotation = Quaternion.Euler(0f, 0f, -28f * flap);

                float fade = Mathf.Clamp01((bat.life - bat.age) / 0.6f);
                Color body = BatColor;
                body.a = fade;
                bat.body.color = body;
                Color wing = BatColor * 0.85f;
                wing.a = fade;
                bat.wingLeftRenderer.color = wing;
                bat.wingRightRenderer.color = wing;
            }
        }

        // ---------------------------------------------------------------- result

        void WatchResult()
        {
            if (_resultShown || !_simulation.IsDecided)
            {
                return;
            }

            _resultShown = true;
            _resultAt = Time.time;

            if (_hud != null)
            {
                _hud.SetWhy(_whyText);
            }

            // The song reveal: the subline becomes the title and the tune plays out its line.
            if (_melody != null && _melody.melody != null)
            {
                if (_hud != null)
                {
                    _hud.SetSubline(_melody.melody.songTitle);
                }

                _melody.FinishPhrase();
            }

            if (_simulation.Outcome == HauntedOutcome.Escaped)
            {
                // Bats flood out of the pumpkin all round the outer ring, with a flash.
                SegmentedRing2D outer = _simulation.Ring(_simulation.RingCount - 1);
                const int flood = 36;
                for (int i = 0; i < flood; i++)
                {
                    float angle = i / (float)flood * Mathf.PI * 2f;
                    SpawnBat(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outer.OuterRadius);
                }

                if (_shatter != null)
                {
                    _shatter.Shockwave(outer.transform.position, outer.OuterRadius + 1.6f, new Color(1f, 0.7f, 0.2f));
                }

                Play(_whoosh, sfxVolume);
            }
        }

        void UpdateScenery()
        {
            float fraction = _simulation.CandleFraction;
            bool lost = _simulation.Outcome == HauntedOutcome.CandleOut;

            if (_glow != null)
            {
                float rate = Mathf.Lerp(14f, 5f, Mathf.Clamp01(fraction / lowCandle));
                float noise = Mathf.PerlinNoise(Time.time * rate, 0.37f);
                float flicker = 0.8f + 0.2f * noise;
                float alpha = Mathf.Lerp(0.16f, 0.62f, Mathf.Clamp01(fraction * 1.6f)) * flicker;
                if (lost)
                {
                    alpha *= Mathf.Clamp01(1f - (Time.time - _resultAt) / 0.8f);
                }

                Color color = _glow.color;
                color.a = alpha;
                _glow.color = color;
            }
        }

        void UpdateHud()
        {
            if (_hud == null)
            {
                return;
            }

            float fraction = _simulation.CandleFraction;
            bool lost = _simulation.Outcome == HauntedOutcome.CandleOut;
            bool won = _simulation.Outcome == HauntedOutcome.Escaped;

            _hud.SetGhosts(_simulation.GhostCount);
            _hud.SetRings(_simulation.RingsBroken, _simulation.RingCount);
            _hud.SetBats(_simulation.BatsOut);

            // Candle: flicker speeds up and gets jumpier as it runs low.
            float rate = Mathf.Lerp(16f, 6f, Mathf.Clamp01(fraction / lowCandle));
            float jump = fraction < lowCandle ? 0.28f : 0.12f;
            float flicker = 1f + (Mathf.PerlinNoise(Time.time * rate, 4.1f) - 0.5f) * 2f * jump;
            _hud.SetCandle(fraction, flicker, !lost && fraction > 0f);
            _hud.SetFlameColor(Color.Lerp(new Color(1f, 0.35f, 0.08f, 1f), new Color(1f, 0.82f, 0.25f, 1f), Mathf.Clamp01(fraction * 2f)));

            if (won)
            {
                _hud.SetStatus(_winText, WinColor);
                float t = Time.time - _resultAt;
                _hud.SetOverlay(new Color(1f, 0.8f, 0.45f, 0.7f * Mathf.Clamp01(1f - t / 0.8f)));
            }
            else if (lost)
            {
                _hud.SetStatus(_loseText, LoseColor);
                float t = Mathf.Clamp01((Time.time - _resultAt) / 1.4f);
                _hud.SetOverlay(new Color(0.02f, 0f, 0.04f, 0.86f * t * t * (3f - 2f * t)));
            }
            else if (Time.time < _ringOpenUntil)
            {
                _hud.SetStatus($"RING {_ringOpenNumber} OPEN!", StatusColor);
            }
            else if (!_simulation.IsHolding && fraction < lowCandle)
            {
                _hud.SetStatus(_almostOut, StatusColor);
            }
            else
            {
                _hud.SetStatus(string.Empty, StatusColor);
            }
        }

        // ---------------------------------------------------------------- helpers

        void Burst(Vector2 position, Vector2 outward, Color color, float intensity, int ring)
        {
            if (_shatter != null)
            {
                _shatter.Burst(position, outward, color, intensity, ring);
            }
        }

        void Play(AudioClip clip, float volume)
        {
            if (_sfx != null && clip != null && volume > 0f)
            {
                _sfx.PlayOneShot(clip, volume);
            }
        }

        void SetTint(Transform visual, Color tint)
        {
            var renderer = visual.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(_block);
            _block.SetColor("_Color", tint);
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// A door creak: a low saw sweeping down under a stuttering amplitude and a wobbling pitch, with a
        /// little filtered noise for the wood. Procedural like the rest of the sound, nothing sampled.
        /// </summary>
        static AudioClip CreateCreak(float duration)
        {
            const int sampleRate = 44100;
            int count = Mathf.RoundToInt(sampleRate * duration);
            var samples = new float[count];
            var noiseSource = new System.Random(77);
            float phase = 0f;
            float noise = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float t01 = i / (float)count;

                float frequency = Mathf.Lerp(190f, 92f, t01) * (1f + 0.22f * Mathf.Sin(t * 31f + 2f * Mathf.Sin(t * 7f)));
                phase += frequency / sampleRate;
                float saw = Mathf.Repeat(phase, 1f) * 2f - 1f;

                float raw = (float)(noiseSource.NextDouble() * 2.0 - 1.0);
                noise += (raw - noise) * 0.08f;

                float stutter = 0.55f + 0.45f * Mathf.Sin(t * 52f + 3f * Mathf.Sin(t * 11f));
                float envelope = Mathf.Sin(Mathf.PI * t01);
                samples[i] = (saw * 0.45f + noise * 0.9f) * stutter * envelope * 0.32f;
            }

            AudioClip clip = AudioClip.Create("HallCreak", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(HauntedPumpkinSimulation simulation, HauntedPumpkinHud hud, BounceMelodyPlayer melody,
                         ShatterBurst2D shatter, SpriteRenderer glow, Sprite circle, Sprite square)
        {
            _simulation = simulation;
            _hud = hud;
            _melody = melody;
            _shatter = shatter;
            _glow = glow;
            _circleSprite = circle;
            _squareSprite = square;
        }
    }
}
