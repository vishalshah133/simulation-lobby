using System.Collections.Generic;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall
{
    /// <summary>
    /// Plays the soundtrack of a Freefall Symphony run: one pitched note per scheduled hit, a drone bed per
    /// world, a whoosh between worlds, a riser into the final hit and a big chord on it.
    /// </summary>
    /// <remarks>
    /// <b>Read-only.</b> It follows <see cref="FreefallSimulation.Time"/> with its own cursor over the
    /// schedule's hit list and never writes simulation state, so rendering with or without audio is
    /// identical. Runs in <c>Update</c> (presentation), not <c>FixedUpdate</c>.
    /// <para>
    /// Clips are synthesised, cached by (timbre, frequency, ring), generated for world 0 at
    /// <see cref="Bind"/> and for each next world in small time-boxed slices during the one before, so
    /// nothing is generated on the frame a note must sound.
    /// </para>
    /// </remarks>
    public sealed class FreefallAudio : MonoBehaviour
    {
        const int VoicePoolSize = 32;
        const float RingStep = 0.1f;
        const float MinRing = 0.25f;
        const float MaxRing = 3f;
        const float StaleHitSeconds = 0.3f;
        const float CanonPan = 0.35f;
        const float BedVolume = 0.16f;
        const float BedCrossfadeSeconds = 2f;
        const float RiserSeconds = 4f;
        const double PregenBudgetSeconds = 0.004;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] float noteVolume = 1f;
        [SerializeField, Range(0f, 1f)] float effectsVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] float canonVoiceVolume = 0.85f;

        FreefallSimulation _sim;
        FreefallConfig _config;
        FreefallSchedule _schedule;
        bool _ready;

        AudioSource[] _pool;
        int _poolCursor;
        AudioSource _effects;
        AudioSource _riserSource;
        AudioSource[] _bedSources;
        int _bedActive;

        readonly Dictionary<ClipKey, AudioClip> _clips = new Dictionary<ClipKey, AudioClip>();
        readonly Dictionary<int, AudioClip> _beds = new Dictionary<int, AudioClip>();
        readonly Queue<ClipKey> _pending = new Queue<ClipKey>();
        readonly HashSet<ClipKey> _queued = new HashSet<ClipKey>();
        readonly HashSet<int> _worldsQueued = new HashSet<int>();

        AudioClip _whoosh;
        AudioClip _riser;
        AudioClip _thud;

        int _cursor;
        float _lastTime = -1f;
        float _maxImpulse = 1f;
        int _bedWorld = -1;
        int _nextWhooshWorld;
        bool _riserPlayed;
        float _riserStartTime = float.MaxValue;
        float _bedFadeT = 1f;

        readonly struct ClipKey : System.IEquatable<ClipKey>
        {
            public readonly ProceduralInstrument.Timbre timbre;
            public readonly int freqTenths;
            public readonly int ringSteps;

            public ClipKey(ProceduralInstrument.Timbre timbre, float frequencyHz, float ringSeconds)
            {
                this.timbre = timbre;
                freqTenths = Mathf.RoundToInt(frequencyHz * 10f);
                ringSteps = Mathf.RoundToInt(Mathf.Clamp(ringSeconds, MinRing, MaxRing) / RingStep);
            }

            public float Frequency => freqTenths / 10f;
            public float Ring => ringSteps * RingStep;

            public bool Equals(ClipKey o) => timbre == o.timbre && freqTenths == o.freqTenths && ringSteps == o.ringSteps;
            public override bool Equals(object obj) => obj is ClipKey k && Equals(k);
            public override int GetHashCode() => ((int)timbre * 397 ^ freqTenths) * 397 ^ ringSteps;
        }

        // ------------------------------------------------------------------ setup

        public void Bind(FreefallSimulation sim, FreefallConfig config)
        {
            _sim = sim;
            _config = config;
            _ready = false;
            TryPrepare();
        }

        void TryPrepare()
        {
            if (_ready || _sim == null || _config == null) return;
            _schedule = _sim.Schedule;
            if (_schedule == null) return; // simulation not initialised yet; retry next frame

            BuildSources();
            EnsureListenerReverb();

            var hits = _schedule.Hits;
            // 90th percentile, not the max: each world's first hit lands off a ~4 s free fall at ~3x the
            // impulse of an ordinary note, and scaling to it would leave every real note at the quiet end.
            var impulses = new List<float>(hits.Count);
            for (int i = 0; i < hits.Count; i++) impulses.Add(hits[i].impulse);
            impulses.Sort();
            _maxImpulse = impulses.Count > 0 ? Mathf.Max(1e-3f, impulses[(int)(impulses.Count * 0.9f)]) : 1f;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].isRunFinal) _riserStartTime = hits[i].time - RiserSeconds;
            }

            _whoosh = ProceduralToneBank.CreateWhoosh(2.4f, 21, "FallWhoosh");
            _riser = ProceduralToneBank.CreateRiser(RiserSeconds, 90f, 520f, 31, "FallRiser");
            _thud = ProceduralToneBank.CreateImpactThud(0.7f, 0.7f, 41, "FallThud");

            // World 0 is generated now, synchronously; later worlds stream in during the previous one.
            QueueWorld(0);
            DrainPending(double.MaxValue);
            EnsureBed(0);

            _cursor = FirstHitAfter(_sim.Time - StaleHitSeconds);
            _lastTime = _sim.Time;
            _nextWhooshWorld = 0;
            _ready = true;
        }

        void BuildSources()
        {
            if (_pool != null) return;

            _pool = new AudioSource[VoicePoolSize];
            for (int i = 0; i < _pool.Length; i++) _pool[i] = NewSource($"NoteVoice{i:D2}", false);

            _effects = NewSource("Effects", false);
            _riserSource = NewSource("Riser", false);
            _bedSources = new[] { NewSource("BedA", true), NewSource("BedB", true) };
        }

        AudioSource NewSource(string childName, bool loop)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f; // 2D: the camera moves, the music must not
            s.dopplerLevel = 0f;
            return s;
        }

        /// <summary>One shared hall reverb on the listener, so every note sits in the same space.</summary>
        void EnsureListenerReverb()
        {
            var listener = FindFirstObjectByType<AudioListener>();
            GameObject host = listener != null ? listener.gameObject : gameObject;
            var reverb = host.GetComponent<AudioReverbFilter>();
            if (reverb == null) reverb = host.AddComponent<AudioReverbFilter>();
            reverb.reverbPreset = AudioReverbPreset.Concerthall;
        }

        // ------------------------------------------------------------------ clip generation

        void QueueWorld(int world)
        {
            if (world < 0 || world >= _config.worlds.Count || !_worldsQueued.Add(world)) return;

            var timbre = _config.worlds[world].timbre;
            var hits = _schedule.Hits;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].world != world) continue;
                Enqueue(new ClipKey(timbre, hits[i].frequencyHz, hits[i].ringSeconds));
                if (hits[i].isRunFinal) EnqueueChord(world);
            }
        }

        void EnqueueChord(int world)
        {
            var w = _config.worlds[world];
            if (w.melody == null) return;
            float tonic = w.melody.tonicHz;
            Enqueue(new ClipKey(w.timbre, tonic, MaxRing));
            Enqueue(new ClipKey(w.timbre, tonic * ChordFifth, MaxRing));
            Enqueue(new ClipKey(w.timbre, tonic * 2f, MaxRing));
        }

        static readonly float ChordFifth = Mathf.Pow(2f, 7f / 12f);

        void Enqueue(ClipKey key)
        {
            if (_clips.ContainsKey(key) || !_queued.Add(key)) return;
            _pending.Enqueue(key);
        }

        void DrainPending(double budgetSeconds)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_pending.Count > 0)
            {
                Generate(_pending.Dequeue());
                if (sw.Elapsed.TotalSeconds >= budgetSeconds) break;
            }
        }

        AudioClip Generate(ClipKey key)
        {
            if (_clips.TryGetValue(key, out var existing)) return existing;
            int seed = 1 + Mathf.Abs(key.freqTenths) % 997;
            var clip = ProceduralInstrument.CreateNote(key.timbre, key.Frequency, key.Ring, seed);
            _clips[key] = clip;
            return clip;
        }

        AudioClip GetClip(ClipKey key)
        {
            return _clips.TryGetValue(key, out var clip) ? clip : Generate(key); // miss = emergency generate
        }

        void EnsureBed(int world)
        {
            if (_beds.ContainsKey(world) || world < 0 || world >= _config.worlds.Count) return;
            var melody = _config.worlds[world].melody;
            float root = melody != null ? Mathf.Max(55f, melody.tonicHz * 0.5f) : 110f;
            _beds[world] = ProceduralToneBank.CreateAmbientBed(6f, root, $"FallBed{world}");
        }

        // ------------------------------------------------------------------ playback

        void Update()
        {
            if (!_ready)
            {
                TryPrepare();
                if (!_ready) return;
            }

            float t = _sim.Time;

            // Time went backwards (restart): re-seat everything.
            if (t < _lastTime - 0.5f)
            {
                _cursor = FirstHitAfter(t - StaleHitSeconds);
                _riserPlayed = false;
                _nextWhooshWorld = 0;
            }

            _lastTime = t;

            PlayDueHits(t);
            UpdateWorldAudio(t);
            DrainPending(PregenBudgetSeconds);
        }

        void PlayDueHits(float t)
        {
            var hits = _schedule.Hits;
            while (_cursor < hits.Count && hits[_cursor].time <= t)
            {
                FreefallHit hit = hits[_cursor];
                _cursor++;
                if (t - hit.time > StaleHitSeconds) continue; // don't dump a backlog after a hitch
                PlayHit(hit);
            }
        }

        void PlayHit(FreefallHit hit)
        {
            var world = _config.worlds[Mathf.Clamp(hit.world, 0, _config.worlds.Count - 1)];
            var clip = GetClip(new ClipKey(world.timbre, hit.frequencyHz, hit.ringSeconds));

            float impulse01 = Mathf.Clamp01(hit.impulse / _maxImpulse);
            float volume = (0.45f + 0.25f * impulse01) * noteVolume;
            if (hit.voice > 0) volume *= canonVoiceVolume;

            float pan = 0f;
            if (hit.voice > 0) pan = (hit.voice % 2 == 1 ? -CanonPan : CanonPan);

            Play(clip, volume, pan);

            if (hit.isRunFinal) PlayFinale(hit.world);
        }

        void Play(AudioClip clip, float volume, float pan)
        {
            var src = NextVoice();
            src.Stop();
            src.clip = clip;
            src.volume = volume;
            src.panStereo = pan;
            src.pitch = 1f;
            src.Play();
        }

        AudioSource NextVoice()
        {
            // Prefer an idle source; otherwise steal the one we handed out longest ago.
            for (int n = 0; n < _pool.Length; n++)
            {
                int i = (_poolCursor + n) % _pool.Length;
                if (!_pool[i].isPlaying)
                {
                    _poolCursor = (i + 1) % _pool.Length;
                    return _pool[i];
                }
            }

            var stolen = _pool[_poolCursor];
            _poolCursor = (_poolCursor + 1) % _pool.Length;
            return stolen;
        }

        void PlayFinale(int worldIndex)
        {
            var w = _config.worlds[Mathf.Clamp(worldIndex, 0, _config.worlds.Count - 1)];
            float tonic = w.melody != null ? w.melody.tonicHz : 220f;

            // Root, fifth and octave of the last world's tonic, all in that world's timbre.
            Play(GetClip(new ClipKey(w.timbre, tonic, MaxRing)), 0.55f * noteVolume, -0.15f);
            Play(GetClip(new ClipKey(w.timbre, tonic * ChordFifth, MaxRing)), 0.5f * noteVolume, 0.15f);
            Play(GetClip(new ClipKey(w.timbre, tonic * 2f, MaxRing)), 0.5f * noteVolume, 0f);

            _effects.PlayOneShot(_thud, 0.9f * effectsVolume);
        }

        void UpdateWorldAudio(float t)
        {
            int world = Mathf.Clamp(_sim.CurrentWorld, 0, _config.worlds.Count - 1);

            // New world: crossfade the drone, and start streaming the one after it.
            if (world != _bedWorld)
            {
                StartBed(world);
                QueueWorld(world + 1);
                if (world + 1 < _config.worlds.Count) EnsureBed(world + 1);
            }

            if (_bedFadeT < 1f)
            {
                _bedFadeT = Mathf.Min(1f, _bedFadeT + Time.deltaTime / BedCrossfadeSeconds);
                var incoming = _bedSources[_bedActive];
                var outgoing = _bedSources[1 - _bedActive];
                incoming.volume = BedVolume * effectsVolume * _bedFadeT;
                outgoing.volume = BedVolume * effectsVolume * (1f - _bedFadeT);
                if (_bedFadeT >= 1f) outgoing.Stop();
            }

            // Whoosh as each world hands over to the next (the camera pulls back and dives).
            var worlds = _schedule.Worlds;
            while (_nextWhooshWorld < worlds.Count - 1 && t >= worlds[_nextWhooshWorld].lastHitTime)
            {
                if (t - worlds[_nextWhooshWorld].lastHitTime < 1.5f)
                    _effects.PlayOneShot(_whoosh, 0.7f * effectsVolume);
                _nextWhooshWorld++;
            }

            // Riser over the last few seconds before the final hit.
            if (!_riserPlayed && t >= _riserStartTime)
            {
                _riserPlayed = true;
                float offset = Mathf.Clamp(t - _riserStartTime, 0f, RiserSeconds - 0.05f);
                _riserSource.clip = _riser;
                _riserSource.volume = 0.7f * effectsVolume;
                _riserSource.time = offset;
                _riserSource.Play();
            }
        }

        void StartBed(int world)
        {
            _bedWorld = world;
            EnsureBed(world);
            if (!_beds.TryGetValue(world, out var bed)) return;

            _bedActive = 1 - _bedActive;
            var incoming = _bedSources[_bedActive];
            incoming.clip = bed;
            incoming.volume = 0f;
            incoming.Play();
            _bedFadeT = 0f;
        }

        int FirstHitAfter(float time)
        {
            var hits = _schedule.Hits;
            int i = 0;
            while (i < hits.Count && hits[i].time <= time) i++;
            return i;
        }

        void OnDestroy()
        {
            foreach (var clip in _clips.Values) if (clip != null) Destroy(clip);
            foreach (var clip in _beds.Values) if (clip != null) Destroy(clip);
            if (_whoosh != null) Destroy(_whoosh);
            if (_riser != null) Destroy(_riser);
            if (_thud != null) Destroy(_thud);
        }
    }
}
