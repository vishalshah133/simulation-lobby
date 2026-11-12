using System.Collections;
using System.Collections.Generic;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// All sound for the haunted coaster: procedural beds plus sampled voices and impacts (<see cref="CoasterSfx"/>). Read-only on the state; runs in <c>Update</c>.
    /// </summary>
    /// <remarks>
    /// <b>MIX TABLE v2</b> (linear gain on an AudioSource; "bed" = everything marked yes in the Ducked column; the final peak is held
    /// at -3 dBFS by <see cref="CoasterMasterLimiter"/> on the listener, a soft tanh clipper, plus a soft clip baked into every sting):
    /// <code>
    /// Layer             Source / driver                                      Base gain   Ducked  Notes
    /// Sting body        per-kind clip, 2 variants (hash of event index)      1.00        -       STARTS ON THE STING FRAME (PhaseTime 0). Sub-boom 45 Hz/40 ms
    ///                                                                                            + broadband burst + screeching cluster + the kind's signature.
    ///                                                                                            Peak -3 dBFS. Bed is at 0.25 (-12 dB) for 0.4 s => >= 15 dB over it.
    /// Scream voice      female / male / inhuman clip, never the same type    0.85        -       Same frame as the body. Seeded pitch 0.93-1.07.
    ///                   twice in a row. Quieter where the body has its own   0.35-0.6
    ///                   voice (WindowFace, ShadowRush, BatSwarm, SpiderDrop).
    /// Taken scream      short human scream, Doppler fall (pitch 1.15 -> 0.55, 0.60       -       0.8 s after the sting of an event with takesRider; panned to
    ///                   volume -> 0 over 0.9 s)                                                  the left lane (riders ahead sit left).
    /// Micro cue         per-kind lead-in (skitter, rattle, creak, rush...)   0.40        -       Seeked so it ENDS on the sting; PreQuiet of micro kinds only.
    /// Heartbeat         lub-dub, 60 -> 140 bpm with Fear01                   0.30-0.70   NO      x1.25 in PreQuiet, x1.6 in the pre-skull silence, x0.5 on Sting
    /// Drone             6 s loop, detuned 55/55.17/58.3 Hz + 82 Hz, noise    0.20        yes     x(0.7 + 0.5 Fear01)
    /// Siren             far 380-560 Hz wail, swells in and out every 37 s    0.06        yes
    /// Far scream/laugh  5 clips, panned, every 10-25 s (seeded), Idle only   0.18-0.38   yes
    /// Creeping whisper  existing whisper clips, 6-14 s, Idle only            0.12        yes
    /// Chain lift        3 s tick loop (0.30) + 4 clank clips per 0.9 m       0.30 / 0.5  yes     only while OnLift
    /// Wheel rumble      loop, gain 0.06+0.30*speed01*(1+0.3 fast), pitch up  0.30        yes
    /// Wind (low)        loop 250-1800 Hz, gain 0.30*speed01^1.2              0.30        yes     fast = clamp((v-12)/18)
    /// Wind (high)       loop 1.8-7.5 kHz, gain 0.22*fast^1.3                 0.22        yes     the "this is FAST" layer, >= 20 m/s
    /// Pass-by whoosh    doppler band noise, act change + every 5-9 s > 18 m/s 0.25-0.6    no      pan seeded
    /// Wood creaks       3 clips, seeded interval 4-11 s                      0.30        yes
    /// Music box         Dies Irae phrase, pitch 1 -> 0.70 in Trip            0.16        yes     silent in FinalClimb, half in FinalDrop
    /// Whispers (cue)    2 band-noise swells, PreQuiet of BIG real scares     0.30        n/a     none for micro, fakes, treat doors, or the skull
    /// Door creak        warm low creak, DoorGhoul PreQuiet / TreatDoor       0.35        no
    /// Candy shower      crinkle + 4 bell notes, TreatDoor sting              0.55        no
    ///
    /// Bed target (smoothed): PreQuiet BIG -12 dB over 0.25 s | PreQuiet MICRO -6 dB over 0.10 s | Sting 0.25 (-12 dB) hard, held 0.4 s,
    /// then back to 1.0 over 1.2 s | the 2 s before the SkullFinale: bed 0 (dead silence, heartbeat only) | TreatDoor: untouched.
    /// </code>
    /// <para>
    /// Determinism: every random choice (variant, voice type, pan, far-sound timing, creak timing) is a hash of an event or counter
    /// index and a salt, never <c>UnityEngine.Random</c>; clips are synthesised from fixed seeds. Cue timing follows
    /// <see cref="ICoasterState.SimTime"/>, so the same ride sounds the same. Clip synthesis is spread over frames
    /// (about 10 ms of work per frame) so there is no start-up hitch; loops and one-shots start when <c>_ready</c> flips, a second or two in.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CoasterAudio : MonoBehaviour
    {
        // Serialized under new names on purpose (old scenes keep the first build's quieter mix under the old names).
        [Header("Master")]
        [SerializeField, Range(0f, 1f)] float masterGain = 1f;
        [Tooltip("Speed that counts as full speed for rumble, wind and pitch (the config maxSpeed, about 30 m/s).")]
        [SerializeField, Min(1f)] float referenceSpeedMps = 30f;
        [Tooltip("Mixed into every hash so a different value reshuffles variants, creaks and far sounds.")]
        [SerializeField] int seedSalt = 1031;

        [Header("Mix (see the table above)")]
        [SerializeField, Range(0f, 1f)] float mixHeartbeat = 0.5f;
        [SerializeField, Range(0f, 1f)] float mixClank = 0.5f;
        [SerializeField, Range(0f, 1f)] float mixChain = 0.3f;
        [SerializeField, Range(0f, 1f)] float mixRumble = 0.3f;
        [SerializeField, Range(0f, 1f)] float mixWind = 0.3f;
        [SerializeField, Range(0f, 1f)] float mixWindHigh = 0.22f;
        [SerializeField, Range(0f, 1f)] float mixCreak = 0.3f;
        [SerializeField, Range(0f, 1f)] float mixMusic = 0.16f;
        [SerializeField, Range(0f, 1f)] float mixWhisper = 0.3f;
        [SerializeField, Range(0f, 1f)] float mixDoor = 0.35f;
        [SerializeField, Range(0f, 1f)] float mixCandy = 0.55f;
        [SerializeField, Range(0f, 1f)] float mixDrone = 0.2f;
        [SerializeField, Range(0f, 1f)] float mixSiren = 0.06f;
        [SerializeField, Range(0f, 1f)] float mixFar = 0.3f;
        [SerializeField, Range(0f, 1f)] float mixCreepWhisper = 0.12f;
        [SerializeField, Range(0f, 1f)] float mixPass = 0.45f;
        [SerializeField, Range(0f, 1f)] float mixCue = 0.4f;
        [SerializeField, Range(0f, 1f)] float mixScream = 0.85f;
        [SerializeField, Range(0f, 1f)] float mixTaken = 0.6f;
        [Tooltip("Master level of the sampled voices and impacts (Resources/CoasterSfx).")]
        [SerializeField, Range(0f, 1f)] float mixSample = 0.9f;
        [Tooltip("Level of the looping horror bed (omen_bed), swelling with fear.")]
        [SerializeField, Range(0f, 1f)] float mixOmen = 0.3f;

        [Header("Scare ducking")]
        [SerializeField, Range(-24f, 0f)] float bigDuckDb = -12f;
        [SerializeField, Min(0.05f)] float bigDuckSeconds = 0.25f;
        [SerializeField, Range(-24f, 0f)] float microDuckDb = -6f;
        [SerializeField, Min(0.02f)] float microDuckSeconds = 0.1f;
        [Tooltip("Bed level during a sting (linear). 0.25 = -12 dB.")]
        [SerializeField, Range(0.05f, 0.6f)] float stingBed = 0.25f;
        [SerializeField, Min(0.1f)] float stingHoldSeconds = 0.4f;
        [SerializeField, Min(0.1f)] float recoverSeconds = 1.2f;
        [Tooltip("Dead-silent beat (heartbeat only) before the SkullFinale sting.")]
        [SerializeField, Min(0f)] float skullSilenceSeconds = 2f;
        [SerializeField, Min(0.1f)] float takenDelaySeconds = 0.8f;
        [SerializeField, Min(0.2f)] float takenFallSeconds = 0.9f;
        [Tooltip("-1: the riders ahead sit in the left lane, so their screams come from the left.")]
        [SerializeField, Range(-1f, 1f)] float riderLaneSide = -1f;

        [Header("Music box")]
        [SerializeField, Min(0.2f)] float beatSeconds = 0.62f;
        [Tooltip("How far the pitch (and so the tempo) sinks in the Trip. 0.3 = 30 percent down.")]
        [SerializeField, Range(0f, 0.5f)] float tripPitchDrop = 0.3f;
        [SerializeField, Range(0f, 0.12f)] float tripDetune = 0.05f;

        [Header("Chain lift")]
        [Tooltip("One clank per this many metres of rail while on the lift.")]
        [SerializeField, Min(0.2f)] float clankMeters = 0.9f;

        /// <summary>Length of the total-silence beat that ends a big scare's quiet, so the sting arrives from nothing.</summary>
        const float SilentBeatSeconds = 0.3f;

        ICoasterState _state;
        bool _ready;
        bool _building;

        // Sources.
        AudioSource _rumble, _wind, _windHi, _heart, _clank, _chain, _creak, _musicA, _musicB, _whisperL, _whisperR, _door, _candy;
        AudioSource _sting, _voice, _voice2, _cue, _taken, _far, _pass, _drone, _siren, _creep, _omen;
        readonly AudioSource[] _smp = new AudioSource[10];
        readonly float[] _smpPitch = new float[10];
        int _smpNext;
        int _preEvent = -1;
        float _preNextT;
        int _preCount;
        bool _roared;

        // Clips (all registered in _all so OnDestroy frees them).
        readonly List<AudioClip> _all = new List<AudioClip>();
        AudioClip _heartClip, _rumbleClip, _windClip, _windHiClip, _droneClip, _sirenClip, _chainClip;
        AudioClip[] _clankClips, _creakClips, _passClips;
        readonly Dictionary<int, AudioClip> _noteClips = new Dictionary<int, AudioClip>();
        MelodySequence _melody;

        // Runtime state (presentation-only).
        float _bed = 1f;
        float _speedF, _fastF, _chainBlend;
        float _musicTrip;
        float _musicAct = 1f;
        float _lastSim = -1f;
        ScarePhase _lastPhase;
        int _lastIndex = -1;
        ActId _lastAct;
        float _stingSim = -100f;
        bool _stingReal;
        int _lastVoice = -1;

        bool _takenPending;
        float _takenStart;
        bool _takenActive;
        int _takenSeat;
        int _takenCount;

        float _nextBeat;
        float _lastClankDistance;
        int _clankCount;
        float _nextCreak;
        int _creakCount;
        int _noteIndex;
        float _nextNote;
        float _nextFar;
        int _farCount;
        float _nextCreep;
        int _creepCount;
        float _nextPass;
        int _passCount;

        public void SetReferenceSpeed(float metersPerSecond)
        {
            referenceSpeedMps = Mathf.Max(1f, metersPerSecond);
        }

        public void Bind(ICoasterState state)
        {
            _state = state;
            if (_ready)
            {
                ResetRuntime();
                StartLoops();
                return;
            }

            if (!_building)
            {
                _building = true;
                BuildSources();
                StartCoroutine(GenerateClips());
            }
        }

        void StartLoops()
        {
            _rumble.Play();
            _wind.Play();
            _windHi.Play();
            _chain.Play();
            _drone.Play();
            _siren.Play();
            AudioClip omen = CoasterSfx.Get("omen_bed");
            if (omen != null)
            {
                _omen.clip = omen;
                _omen.Play();
            }
        }

        void ResetRuntime()
        {
            _bed = 1f;
            _speedF = 0f;
            _fastF = 0f;
            _chainBlend = 0f;
            _musicTrip = 0f;
            _musicAct = 1f;
            _lastSim = _state != null ? _state.SimTime : -1f;
            _lastPhase = ScarePhase.Idle;
            _lastIndex = -1;
            _lastAct = _state != null ? _state.Act : ActId.Station;
            _stingSim = -100f;
            _stingReal = false;
            _lastVoice = -1;
            _takenPending = false;
            _takenActive = false;
            _takenCount = 0;
            _nextBeat = _lastSim;
            _lastClankDistance = _state != null ? _state.Distance : 0f;
            _clankCount = 0;
            _creakCount = 0;
            _nextCreak = _lastSim + 5f;
            _noteIndex = 0;
            _nextNote = _lastSim + 1f;
            _farCount = 0;
            _nextFar = _lastSim + 9f + 6f * Hash01(0, 30);
            _creepCount = 0;
            _nextCreep = _lastSim + 4f + 4f * Hash01(0, 32);
            _passCount = 0;
            _nextPass = _lastSim + 5f;
        }

        void OnDestroy()
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i] != null)
                {
                    Destroy(_all[i]);
                }
            }

            _all.Clear();
            foreach (var kv in _noteClips)
            {
                if (kv.Value != null)
                {
                    Destroy(kv.Value);
                }
            }

            if (_melody != null)
            {
                Destroy(_melody);
            }
        }

        // ------------------------------------------------------------------ setup

        AudioSource NewSource(string sourceName, bool loop, float pan)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.panStereo = pan;
            s.volume = 0f;
            return s;
        }

        void BuildSources()
        {
            _rumble = NewSource("Rumble", true, 0f);
            _wind = NewSource("Wind", true, 0f);
            _windHi = NewSource("Wind High", true, 0f);
            _heart = NewSource("Heartbeat", false, 0f);
            _clank = NewSource("Clank", false, 0f);
            _chain = NewSource("Chain Loop", true, 0f);
            _creak = NewSource("Creak", false, 0f);
            _musicA = NewSource("MusicBox A", false, -0.15f);
            _musicB = NewSource("MusicBox B (detuned)", false, 0.15f);
            _whisperL = NewSource("Whisper L", false, -0.65f);
            _whisperR = NewSource("Whisper R", false, 0.65f);
            _door = NewSource("Door", false, 0f);
            _candy = NewSource("Candy", false, 0f);
            _sting = NewSource("Sting", false, 0f);
            _voice = NewSource("Scream", false, 0f);
            _voice2 = NewSource("Scream 2", false, 0f);
            _cue = NewSource("Micro Cue", false, 0f);
            _taken = NewSource("Taken Scream", false, 0f);
            _far = NewSource("Far Sounds", false, 0f);
            _pass = NewSource("Pass-by", false, 0f);
            _drone = NewSource("Drone", true, 0f);
            _siren = NewSource("Siren", true, 0f);
            _creep = NewSource("Creeping Whisper", false, 0f);
            _omen = NewSource("Omen Bed", true, 0f);
            for (int i = 0; i < _smp.Length; i++)
            {
                _smp[i] = NewSource("Sample " + i, false, 0f);
            }
        }

        AudioClip Reg(AudioClip c)
        {
            _all.Add(c);
            return c;
        }

        /// <summary>Synthesises every clip in small jobs, yielding when a frame has used about 10 ms, so there is no start-up hitch.</summary>
        IEnumerator GenerateClips()
        {
            int s = seedSalt;
            var jobs = new List<System.Action>();

            jobs.Add(() => CoasterSfx.Warm()); // start decoding the scare recordings; their loudness-matched copies are built below
            jobs.Add(() => _heartClip = Reg(CoasterSynth.Heartbeat()));
            jobs.Add(() => _rumbleClip = Reg(ProceduralToneBank.CreateRumbleLoop(3f, 11 + s, "CoasterRumble")));
            jobs.Add(() => _windClip = Reg(CoasterSynth.WindLoop(4f, 21 + s)));
            jobs.Add(() => _windHiClip = Reg(CoasterSynth.BrightWindLoop(4f, 22 + s)));
            jobs.Add(() => _droneClip = Reg(CoasterSynth.DroneLoop(51 + s)));
            jobs.Add(() => _sirenClip = Reg(CoasterSynth.SirenLoop()));
            jobs.Add(() => _chainClip = Reg(CoasterSynth.ChainLoop(61 + s)));

            _clankClips = new AudioClip[4];
            for (int i = 0; i < _clankClips.Length; i++)
            {
                int k = i;
                jobs.Add(() => _clankClips[k] = Reg(CoasterSynth.Clank(100 + k + s, k)));
            }

            _creakClips = new AudioClip[3];
            for (int i = 0; i < _creakClips.Length; i++)
            {
                int k = i;
                jobs.Add(() => _creakClips[k] = Reg(CoasterSynth.Creak(200 + k + s, k)));
            }

            _passClips = new AudioClip[3];
            for (int i = 0; i < _passClips.Length; i++)
            {
                int k = i;
                jobs.Add(() => _passClips[k] = Reg(CoasterSynth.PassBy(350 + k + s, k)));
            }

            // Every jump-scare sound (stings, screams, cues, whispers, door, candy, the taken rider, far screams) is a sampled recording
            // from Resources/CoasterSfx; none of it is synthesised. Only the ride's own beds and the music box are procedural.

            _melody = CoasterSynth.DiesIrae();
            for (int i = 0; i < _melody.Count; i++)
            {
                int semi = _melody.notes[i].semitone;
                float hz = _melody.FrequencyAt(i);
                if (!_noteClips.ContainsKey(semi))
                {
                    _noteClips[semi] = null;
                    jobs.Add(() => _noteClips[semi] = CoasterSynth.MusicBoxNote(hz, 500 + semi + s));
                }
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int j = 0; j < jobs.Count; j++)
            {
                try { jobs[j](); }
                catch (System.Exception ex) { Debug.LogError($"[coast] audio clip job {j} failed, skipped: {ex.Message}"); }
                if (sw.ElapsedMilliseconds >= 10)
                {
                    yield return null;
                    sw.Restart();
                }
            }

            for (int tries = 0; tries < 180 && !CoasterSfx.Warm(); tries++)
            {
                yield return null; // decoding is asynchronous; the hits must be shaped before the first scare
            }

            _rumble.clip = _rumbleClip;
            _wind.clip = _windClip;
            _windHi.clip = _windHiClip;
            _chain.clip = _chainClip;
            _drone.clip = _droneClip;
            _siren.clip = _sirenClip;
            _ready = true;
            _building = false;
            ResetRuntime();
            StartLoops();
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!_ready || _state == null)
            {
                return;
            }

            AudioListener.volume = masterGain;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            float simT = _state.SimTime;
            if (simT < _lastSim - 0.01f)
            {
                ResetRuntime();
            }

            _lastSim = simT;

            UpdateTransitions(simT);
            UpdatePreSamples(simT);
            UpdateBed(dt, simT);
            UpdateLoops(dt, simT);
            UpdateDread(simT);
            UpdateTaken(simT);
            ApplySlowMoPitch();
            UpdateHeart(simT);
            UpdateClank();
            UpdateCreak(simT);
            UpdateMusic(simT, dt);
        }

        static bool IsReal(ScareEvent ev)
        {
            return !ev.IsFake && ev.kind != ScareKind.TreatDoor;
        }

        void UpdateTransitions(float simT)
        {
            ScarePhase phase = _state.Phase;
            int index = _state.CurrentEventIndex;
            if (phase == _lastPhase && index == _lastIndex)
            {
                return;
            }

            _lastPhase = phase;
            _lastIndex = index;
            if (phase != ScarePhase.PreQuiet)
            {
                _roared = false;
            }

            if (phase == ScarePhase.Idle || phase == ScarePhase.Aftermath)
            {
                return;
            }

            ScareEvent ev = _state.CurrentEvent;
            bool real = IsReal(ev);
            bool micro = ev.kind.IsMicro();

            if (phase == ScarePhase.PreQuiet)
            {
                if (real && !micro && ev.kind != ScareKind.SkullFinale)
                {
                    PlayWhispers(index);
                }

                if (ev.kind == ScareKind.DoorGhoul)
                {
                    Sfx("growl_deep", 0.35f, 0.55f, 0.3f); // the door's low groan, a slowed recorded growl
                }

                if (micro)
                {
                    PlayCue(ev);
                }

                OnPreStart(ev, index);
            }
            else if (phase == ScarePhase.Sting)
            {
                _stingSim = simT;
                _stingReal = real;
                if (ev.kind == ScareKind.TreatDoor)
                {
                    Sfx("bells", 0.7f, 1.35f);
                    Sfx("bells", 0.45f, 1.7f, 0.3f, 0.12f);
                    return;
                }

                PlayStingSamples(ev, index, real);
                if (real && ev.takesRider)
                {
                    _takenPending = true;
                    _takenStart = simT + takenDelaySeconds;
                    _takenSeat = _state.TakenSeat;
                }
            }
        }

        /// <summary>The short lead-in of a micro scare (a skitter, a rattle, a rush), from recordings, played as the brief quiet starts.</summary>
        void PlayCue(ScareEvent ev)
        {
            float v = mixCue;
            switch (ev.kind)
            {
                case ScareKind.SpiderDrop: Sfx("snake_hiss_long", v * 0.8f, 1.9f, 0.2f, 0f, 0.3f); break;   // a dry skitter
                case ScareKind.WindowFace: Sfx("whisper", v * 1.2f, 0.7f, -0.5f); break;
                case ScareKind.SkeletonPop: Sfx("stab", v * 0.5f, 2.2f, 0.4f); Sfx("stab", v * 0.4f, 2.6f, -0.3f, 0.12f); break; // bones rattling
                case ScareKind.ClawHand: Sfx("snake_hiss_short", v * 0.9f, 0.7f); break;
                case ScareKind.BatSwarm: Sfx("bat_wings", v * 1.1f, 1.1f, -0.2f); break;
                case ScareKind.CoffinLid: Sfx("growl_deep", v * 1.0f, 0.5f, 0.3f); break;                   // wood groaning, slowed
                case ScareKind.ShadowRush: Sfx("whisper", v * 1.1f, 0.6f, 0.5f); Sfx("growl_deep", v * 0.6f, 0.7f, -0.4f, 0.1f); break;
                case ScareKind.MirrorFace: Sfx("bells", v * 0.6f, 1.9f, 0f, 0f, 0.2f); break;               // a glassy ring
            }
        }

        void PlayWhispers(int index)
        {
            bool left = Hash01(index, 3) > 0.5f;
            Sfx("whisper", 0.7f, 0.95f, left ? -0.6f : 0.6f);
            Sfx("whisper", 0.45f, 1.08f, left ? 0.6f : -0.6f, 0.35f);
        }

        // ------------------------------------------------------------------ sampled sounds

        AudioClip Smp(string clipName)
        {
            return CoasterSfx.Get(clipName);
        }

        /// <summary>Plays a sampled clip on the next free sample source. <paramref name="delay"/> is in seconds.</summary>
        void Sfx(string clipName, float vol, float pitch = 1f, float pan = 0f, float delay = 0f, float from = 0f)
        {
            AudioClip c = Smp(clipName);
            if (c == null)
            {
                return;
            }

            AudioSource s = _smp[_smpNext];
            _smpPitch[_smpNext] = pitch;
            _smpNext = (_smpNext + 1) % _smp.Length;
            s.Stop();
            s.clip = c;
            s.volume = Mathf.Clamp01(vol * mixSample);
            s.pitch = pitch * ScareSlowMo.Current;
            s.panStereo = pan;
            s.time = Mathf.Clamp(from, 0f, Mathf.Max(0f, c.length - 0.05f));
            if (delay > 0f)
            {
                s.PlayDelayed(delay / Mathf.Max(0.1f, pitch));
            }
            else
            {
                s.Play();
            }
        }

        /// <summary>
        /// The sting: the loud hit on the sting frame, built only from recordings. Every real scare gets a low "impact" boom under its own
        /// voice (the loudest moment of the video), so the slow motion that follows always comes AFTER this peak. Fake-outs get a soft cue.
        /// </summary>
        void PlayStingSamples(ScareEvent ev, int index, bool real)
        {
            float pv = 0.92f + 0.14f * Hash01(index, 25);
            if (!real)
            {
                switch (ev.kind)
                {
                    case ScareKind.FakeBat: Sfx("bat_wings", 0.4f, 1.2f, 0.3f); break;
                    case ScareKind.FakeLantern: Sfx("bells", 0.3f, 0.8f); break;
                    case ScareKind.FakeWheelchair: Sfx("impact", 0.25f, 1.3f, -0.3f); break;
                    default: Sfx("whisper", 0.35f, 0.7f); break;
                }

                return;
            }

            switch (ev.kind)
            {
                case ScareKind.SnakeStrike: Sfx("snake_hiss_short", 0.9f, 0.92f); Sfx("impact", 0.9f, 0.55f); Sfx("scream_female", 0.6f, 1.05f, -0.6f, 0.1f); break;
                case ScareKind.SpikeWall:
                    Sfx("stab", 1f, 0.9f);
                    Sfx("impact", 1f, 0.5f);
                    Sfx("scream_agony", 0.7f, 1.1f, -0.6f, 0.05f);
                    Sfx("stab", 0.85f, 1.1f, 0.5f, 0.22f); // the second volley, past the viewer's face
                    break;
                case ScareKind.SprinterLeap: Sfx("roar_a", 1f, 0.85f); Sfx("screech_a", 0.6f, 1f, 0f, 0.04f); Sfx("impact", 1f, 0.5f); break;
                case ScareKind.SideLeap: Sfx("screech_b", 0.9f, 1.1f, 0.4f); Sfx("impact", 0.9f, 0.55f); break;
                case ScareKind.BatFlood: Sfx("bat_screech", 0.8f, 1f); Sfx("bat_wings", 0.9f, 1f); Sfx("impact", 0.8f, 0.55f); break;
                case ScareKind.DoorGhoul:
                case ScareKind.GraveHand: Sfx("growl_deep", 1f, pv); Sfx("screech_a", 0.45f, 0.8f); Sfx("impact", 0.9f, 0.55f); break;
                case ScareKind.GraveyardGhost: Sfx("screech_b", 0.9f, 0.85f); Sfx("impact", 0.8f, 0.6f); break;
                case ScareKind.AsylumFigure: Sfx("screech_a", 0.9f, 0.9f); Sfx("impact", 0.9f, 0.55f); break;
                case ScareKind.CrowBurst: Sfx("screech_a", 0.8f, 1.25f); Sfx("bat_screech", 0.6f, 0.9f); Sfx("impact", 0.8f, 0.6f); break;
                case ScareKind.TunnelFace:
                case ScareKind.PaintingLunge: Sfx("scream_female", 1f, pv - 0.05f); Sfx("impact", 0.9f, 0.55f); break;
                case ScareKind.SkullFinale: Sfx("roar_b", 1f, 0.7f); Sfx("screech_a", 0.8f, 0.75f); Sfx("scream_agony", 0.7f, 1f); Sfx("impact", 1f, 0.45f); break;
                case ScareKind.WindowFace: Sfx("scream_female", 0.55f, 1.1f); Sfx("impact", 0.8f, 0.7f, -0.4f); break;
                case ScareKind.ShadowRush: Sfx("growl_deep", 0.8f, 1.1f); Sfx("impact", 0.7f, 0.65f); break;
                case ScareKind.BatSwarm: Sfx("bat_screech", 0.75f, 1.1f); Sfx("bat_wings", 0.6f, 1.2f); Sfx("impact", 0.6f, 0.7f); break;
                case ScareKind.SpiderDrop: Sfx("screech_a", 0.6f, 1.6f); Sfx("impact", 0.7f, 0.75f); break;
                case ScareKind.SkeletonPop: Sfx("screech_b", 0.7f, 1.2f); Sfx("stab", 0.6f, 1.6f); Sfx("impact", 0.7f, 0.7f); break;
                case ScareKind.ClawHand: Sfx("growl_deep", 0.8f, 1f); Sfx("snake_hiss_short", 0.5f, 0.8f); Sfx("impact", 0.7f, 0.7f); break;
                case ScareKind.CoffinLid: Sfx("roar_a", 0.8f, 1.15f); Sfx("impact", 0.9f, 0.55f); break;
                case ScareKind.MirrorFace: Sfx("screech_b", 0.7f, 1.35f); Sfx("bells", 0.5f, 2.2f, 0f, 0.04f); Sfx("impact", 0.7f, 0.7f); break;
                default: Sfx("impact", 0.9f, 0.6f); break;
            }
        }

        void OnPreStart(ScareEvent ev, int index)
        {
            _preEvent = index;
            _preCount = 0;
            _preNextT = _lastSim;
            _roared = false;
            switch (ev.kind)
            {
                case ScareKind.SnakeStrike: Sfx("snake_hiss_long", 0.8f, 1f, -0.4f, 0f, 0.5f); break;
                case ScareKind.SprinterLeap: Sfx("growl_deep", 0.3f, 0.8f); break;
                case ScareKind.BatFlood: Sfx("bat_screech", 0.3f, 0.9f, 0.3f); break;
                case ScareKind.SideLeap: Sfx("wolf", 0.35f, 1f, 0.6f, 0.2f); break;
            }
        }

        /// <summary>Per-frame lead-ins: the beast's footfalls getting faster and louder, the spike wall's ratchet.</summary>
        void UpdatePreSamples(float simT)
        {
            if (_state.Phase != ScarePhase.PreQuiet || _state.CurrentEventIndex != _preEvent)
            {
                return;
            }

            ScareEvent ev = _state.CurrentEvent;
            float u = Mathf.Clamp01(_state.PhaseTime / Mathf.Max(0.5f, ev.preQuietSeconds));
            if (ev.kind == ScareKind.SprinterLeap)
            {
                if (simT >= _preNextT)
                {
                    Sfx("impact", Mathf.Lerp(0.05f, 0.5f, u * u), 0.5f + 0.15f * Hash01(_preCount, 63));
                    _preCount++;
                    _preNextT = simT + Mathf.Lerp(0.5f, 0.17f, u);
                }

                if (u > 0.65f && !_roared)
                {
                    _roared = true;
                    Sfx("roar_b", 0.55f, 1.15f);
                }
            }
            else if (ev.kind == ScareKind.SpikeWall && simT >= _preNextT)
            {
                Sfx("stab", 0.16f, 2f);
                _preNextT = simT + Mathf.Lerp(0.45f, 0.18f, u);
            }
        }

        static float Db(float db)
        {
            return Mathf.Pow(10f, db / 20f);
        }

        void UpdateBed(float dt, float simT)
        {
            float target = 1f;
            float tc = 0.06f;
            ScareEvent ev = _state.CurrentEvent;
            ScarePhase phase = _state.Phase;
            bool treat = phase != ScarePhase.Idle && ev.kind == ScareKind.TreatDoor;
            if (!treat)
            {
                float sinceSting = simT - _stingSim;
                if (phase == ScarePhase.PreQuiet)
                {
                    if (ev.kind.IsMicro())
                    {
                        target = Mathf.Lerp(1f, Db(microDuckDb), CameraSpring.SmoothStep01(_state.PhaseTime / microDuckSeconds));
                        tc = 0.03f;
                    }
                    else if (ev.kind == ScareKind.SkullFinale && _state.PhaseTime >= ev.preQuietSeconds - skullSilenceSeconds)
                    {
                        target = 0f;
                        tc = 0.15f;
                    }
                    else
                    {
                        target = Mathf.Lerp(1f, Db(bigDuckDb), CameraSpring.SmoothStep01(_state.PhaseTime / bigDuckSeconds));
                        if (ev.preQuietSeconds - _state.PhaseTime < SilentBeatSeconds)
                        {
                            target = 0f; // the held breath: total silence for the last beat, then the sting
                            tc = 0.04f;
                        }
                    }
                }
                else if ((phase == ScarePhase.Sting || phase == ScarePhase.Aftermath) && _stingReal)
                {
                    if (sinceSting < stingHoldSeconds)
                    {
                        target = stingBed;
                        tc = 0.02f;
                    }
                    else
                    {
                        target = Mathf.Lerp(stingBed, 1f, CameraSpring.SmoothStep01((sinceSting - stingHoldSeconds) / recoverSeconds));
                    }
                }
                else if (phase == ScarePhase.Sting)
                {
                    target = Db(bigDuckDb);
                }
                else if (phase == ScarePhase.Aftermath)
                {
                    target = Mathf.Lerp(Db(bigDuckDb), 1f, CameraSpring.SmoothStep01(_state.PhaseTime / recoverSeconds));
                }
            }

            _bed += (target - _bed) * (1f - Mathf.Exp(-dt / tc));
        }

        void UpdateLoops(float dt, float simT)
        {
            float v = _state.Speed;
            float sp = Mathf.Clamp01(v / referenceSpeedMps);
            _speedF += (sp - _speedF) * (1f - Mathf.Exp(-dt / 0.2f));
            float fastT = Mathf.Clamp01((v - 12f) / 18f);
            _fastF += (fastT - _fastF) * (1f - Mathf.Exp(-dt / 0.3f));
            float s = _speedF;
            float fast = _fastF;

            float gust = 1f + 0.25f * Mathf.Sin(0.7f * simT + 1.3f) + 0.12f * Mathf.Sin(1.9f * simT);
            _rumble.volume = mixRumble * (0.06f / 0.3f + (0.94f) * s) * (1f + 0.3f * fast) * _bed;
            _rumble.pitch = 0.75f + 0.6f * s + 0.2f * fast;
            _wind.volume = mixWind * Mathf.Pow(s, 1.2f) * gust * (1f + 0.4f * fast) * _bed;
            _wind.pitch = 0.85f + 0.4f * s + 0.2f * fast;
            _windHi.volume = mixWindHigh * Mathf.Pow(fast, 1.3f) * gust * _bed;
            _windHi.pitch = 0.9f + 0.3f * fast;

            // Dread bed.
            float fear = Mathf.Clamp01(_state.Fear01);
            _drone.volume = mixDrone * (0.7f + 0.5f * fear) * _bed;
            _drone.pitch = 1f + 0.03f * fear;
            float swell = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * (simT / 37f + 0.2f)));
            _siren.volume = mixSiren * swell * swell * _bed;
            _omen.volume = mixOmen * (0.45f + 0.9f * fear) * _bed;

            _chainBlend += ((_state.OnLift ? 1f : 0f) - _chainBlend) * (1f - Mathf.Exp(-dt / 0.4f));
            _chain.volume = mixChain * _chainBlend * _bed;
            _chain.pitch = 0.9f + 0.2f * s;
        }

        void UpdateDread(float simT)
        {
            bool quiet = _state.Phase == ScarePhase.Idle && _bed > 0.8f;

            if (simT >= _nextFar)
            {
                if (quiet)
                {
                    // Recordings only: a wolf howl, a tolling bell, or a distant human scream, pitched down and panned out in the dark.
                    float side = Hash01(_farCount, 34) > 0.5f ? 1f : -1f;
                    float pan = side * (0.5f + 0.4f * Hash01(_farCount, 35));
                    float roll = Hash01(_farCount, 61);
                    float pit = 0.85f + 0.2f * Hash01(_farCount, 62);
                    if (roll < 0.4f) { Sfx("wolf", 0.5f, pit, pan); }
                    else if (roll < 0.7f) { Sfx("bells", 0.4f, pit, pan); }
                    else { Sfx("scream_agony", 0.22f, 0.75f + 0.15f * Hash01(_farCount, 63), pan); }
                }

                _farCount++;
                _nextFar = simT + 10f + 15f * Hash01(_farCount, 37);
            }

            if (simT >= _nextCreep)
            {
                if (quiet)
                {
                    float pan = (Hash01(_creepCount, 39) > 0.5f ? 1f : -1f) * (0.4f + 0.5f * Hash01(_creepCount, 40));
                    Sfx("whisper", mixCreepWhisper * 2.2f * _bed, 0.85f + 0.3f * Hash01(_creepCount, 38), pan);
                }

                _creepCount++;
                _nextCreep = simT + 6f + 8f * Hash01(_creepCount, 41);
            }

            // Doppler-ish pass-bys: at every act change, and every 5-9 s while fast.
            float v = _state.Speed;
            bool actChanged = _state.Act != _lastAct;
            _lastAct = _state.Act;
            bool periodic = simT >= _nextPass && v > 18f;
            if (simT >= _nextPass)
            {
                _nextPass = simT + 5f + 4f * Hash01(_passCount, 42);
            }

            if ((actChanged && v > 10f) || (periodic && _state.Phase == ScarePhase.Idle))
            {
                int c = Mathf.FloorToInt(Hash01(_passCount, 43) * _passClips.Length) % _passClips.Length;
                _pass.panStereo = (Hash01(_passCount, 44) > 0.5f ? 1f : -1f) * (0.3f + 0.6f * Hash01(_passCount, 45));
                _pass.PlayOneShot(_passClips[c], mixPass * Mathf.Clamp01(0.4f + v / referenceSpeedMps));
                _passCount++;
            }
        }

        /// <summary>
        /// While the slow motion holds (it only ever starts after the sting's loud hit) the recorded voices still ringing out sink in pitch, the
        /// classic "time is stretching" sound. Reads <see cref="ScareSlowMo.Current"/>, writes only audio sources.
        /// </summary>
        void ApplySlowMoPitch()
        {
            float k = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(ScareSlowMo.Current));
            for (int i = 0; i < _smp.Length; i++)
            {
                if (_smp[i].isPlaying)
                {
                    _smp[i].pitch = _smpPitch[i] * k;
                }
            }
        }

        void UpdateTaken(float simT)
        {
            if (_takenPending && simT >= _takenStart)
            {
                _takenPending = false;
                _takenActive = true;
                bool female = Hash01(_takenCount + _lastIndex, 46) > 0.5f;
                _takenCount++;
                int seat = Mathf.Clamp(_takenSeat, 0, 4);
                _taken.Stop();
                _taken.clip = Smp(female ? "scream_female" : "scream_agony");
                _taken.panStereo = Mathf.Clamp(riderLaneSide * (0.75f - 0.06f * seat), -1f, 1f);
                _taken.pitch = 1.15f;
                _taken.volume = mixTaken;
                _taken.Play();
            }

            if (_takenActive)
            {
                float u = (simT - _takenStart) / takenFallSeconds;
                if (u >= 1f || !_taken.isPlaying)
                {
                    _takenActive = false;
                    _taken.Stop();
                    return;
                }

                // Doppler fall: the pitch sinks and the voice recedes.
                _taken.pitch = Mathf.Lerp(1.15f, 0.55f, u);
                _taken.volume = mixTaken * Mathf.Pow(1f - u, 1.5f);
            }
        }

        void UpdateHeart(float simT)
        {
            float fear = Mathf.Clamp01(_state.Fear01);
            float bpm = Mathf.Lerp(60f, 140f, fear);
            if (simT >= _nextBeat)
            {
                float vol = mixHeartbeat * (0.6f + 0.4f * fear) * 1.4f;
                if (_state.Phase == ScarePhase.PreQuiet && !_state.CurrentEvent.kind.IsMicro()
                    && _state.CurrentEvent.preQuietSeconds - _state.PhaseTime < SilentBeatSeconds)
                {
                    vol = 0f;
                }
                else if (_state.Phase == ScarePhase.PreQuiet)
                {
                    vol *= 1.25f;
                    if (_state.CurrentEvent.kind == ScareKind.SkullFinale && _state.PhaseTime >= _state.CurrentEvent.preQuietSeconds - skullSilenceSeconds)
                    {
                        vol *= 1.3f; // the only sound left
                    }
                }
                else if (_state.Phase == ScarePhase.Sting)
                {
                    vol *= 0.5f;
                }

                _heart.volume = Mathf.Clamp(vol, 0f, 1f);
                _heart.PlayOneShot(_heartClip);
                _nextBeat = Mathf.Max(_nextBeat, simT - 0.5f) + 60f / bpm;
            }
        }

        void UpdateClank()
        {
            float d = _state.Distance;
            if (!_state.OnLift)
            {
                _lastClankDistance = d;
                return;
            }

            int guard = 0;
            while (d - _lastClankDistance >= clankMeters && guard++ < 3)
            {
                _lastClankDistance += clankMeters;
                int v = Mathf.FloorToInt(Hash01(_clankCount, 5) * _clankClips.Length) % _clankClips.Length;
                _clankCount++;
                _clank.volume = mixClank * _bed * (0.85f + 0.3f * Hash01(_clankCount, 6));
                _clank.PlayOneShot(_clankClips[v]);
            }

            if (d - _lastClankDistance > clankMeters * 3f)
            {
                _lastClankDistance = d;
            }
        }

        void UpdateCreak(float simT)
        {
            if (simT < _nextCreak)
            {
                return;
            }

            if (_bed > 0.8f && _state.Phase == ScarePhase.Idle)
            {
                int v = Mathf.FloorToInt(Hash01(_creakCount, 7) * _creakClips.Length) % _creakClips.Length;
                _creak.volume = mixCreak * _bed * (0.6f + 0.4f * Hash01(_creakCount, 8));
                _creak.PlayOneShot(_creakClips[v]);
            }

            _creakCount++;
            _nextCreak = simT + 4f + 7f * Hash01(_creakCount, 9);
        }

        void UpdateMusic(float simT, float dt)
        {
            bool trip = _state.Act == ActId.Trip;
            float tc = trip ? 1.5f : 3f;
            _musicTrip += ((trip ? 1f : 0f) - _musicTrip) * (1f - Mathf.Exp(-dt / tc));

            float actTarget;
            switch (_state.Act)
            {
                case ActId.FinalClimb: actTarget = 0f; break;
                case ActId.FinalDrop: actTarget = 0.5f; break;
                case ActId.Station: actTarget = 0.6f; break;
                default: actTarget = 1f; break;
            }

            _musicAct += (actTarget - _musicAct) * (1f - Mathf.Exp(-dt / 0.8f));

            float pitch = (1f - tripPitchDrop * _musicTrip) * (1f + 0.015f * _musicTrip * Mathf.Sin(2f * Mathf.PI * 0.4f * simT));
            _musicA.pitch = pitch;
            _musicB.pitch = pitch * (1f + tripDetune * _musicTrip);
            float vol = mixMusic * _musicAct * _bed;
            _musicA.volume = vol;
            _musicB.volume = vol * 0.7f * _musicTrip;

            if (simT < _nextNote)
            {
                return;
            }

            if (simT - _nextNote > 1f)
            {
                _nextNote = simT;
            }

            if (_musicAct < 0.05f || _state.Phase == ScarePhase.PreQuiet || _state.Phase == ScarePhase.Sting)
            {
                _nextNote = simT + 0.25f;
                return;
            }

            MelodyNote note = _melody.notes[_noteIndex];
            if (_noteClips.TryGetValue(note.semitone, out AudioClip clip) && clip != null)
            {
                float velocity = 0.8f + 0.2f * Hash01(_noteIndex, 11);
                _musicA.PlayOneShot(clip, velocity);
                if (_musicTrip > 0.05f)
                {
                    _musicB.PlayOneShot(clip, velocity);
                }
            }

            float beats = note.beats + (note.endsPhrase ? 2f : 0f);
            _nextNote += beats * beatSeconds / Mathf.Max(0.3f, pitch);
            _noteIndex = (_noteIndex + 1) % _melody.Count;
        }

        float Hash01(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263 + seedSalt * 1442695041 + 12345);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x1000000;
            }
        }
    }

    internal static class ScareKindAudioExt
    {
        /// <summary>The fake kinds, spelled out so the audio does not need an event instance.</summary>
        public static bool IsFake_Safe(this ScareKind k)
        {
            return k == ScareKind.FakeBat || k == ScareKind.FakeLantern || k == ScareKind.FakeWheelchair || k == ScareKind.FakeEmptySeat || k == ScareKind.TreatDoor;
        }
    }

    /// <summary>
    /// The synthesis for <see cref="CoasterAudio"/>: small, allocation-honest DSP helpers and one builder per sound. Everything is
    /// seeded (<see cref="System.Random"/>) so the same seed renders the same clip.
    /// </summary>
    internal static class CoasterSynth
    {
        const int SR = 44100;
        const float Peak = 0.70795f; // -3 dBFS

        // ---------------------------------------------------------------- primitives

        static float[] Buf(float seconds)
        {
            return new float[Mathf.Max(1, Mathf.RoundToInt(seconds * SR))];
        }

        static AudioClip MakeClip(float[] data, string clipName)
        {
            var clip = AudioClip.Create(clipName, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float OnePole(float hz)
        {
            return 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Max(1f, hz) / SR);
        }

        static void PeakNormalize(float[] b, float peak)
        {
            float max = 1e-6f;
            for (int i = 0; i < b.Length; i++)
            {
                float a = Mathf.Abs(b[i]);
                if (a > max)
                {
                    max = a;
                }
            }

            float k = peak / max;
            for (int i = 0; i < b.Length; i++)
            {
                b[i] *= k;
            }
        }

        static void FadeEnds(float[] b, float inSeconds, float outSeconds)
        {
            int ni = Mathf.Min(b.Length, Mathf.RoundToInt(inSeconds * SR));
            int no = Mathf.Min(b.Length, Mathf.RoundToInt(outSeconds * SR));
            for (int i = 0; i < ni; i++)
            {
                b[i] *= i / (float)ni;
            }

            for (int i = 0; i < no; i++)
            {
                b[b.Length - 1 - i] *= i / (float)no;
            }
        }

        static void LowPass(float[] b, float hz)
        {
            float a = OnePole(hz);
            float y = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                y += (b[i] - y) * a;
                b[i] = y;
            }
        }

        /// <summary>Soft clip, normalise to the -3 dBFS peak times level, fade the tail, hard clamp. The sting's own limiter.</summary>
        static void Finish(float[] b, float level)
        {
            PeakNormalize(b, 1f);
            const float drive = 1.5f;
            float norm = (float)System.Math.Tanh(drive);
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = (float)System.Math.Tanh(drive * b[i]) / norm;
            }

            PeakNormalize(b, Peak * Mathf.Clamp01(level));
            FadeEnds(b, 0.001f, 0.03f);
            float ceiling = Peak * Mathf.Clamp01(level);
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = Mathf.Clamp(b[i], -ceiling, ceiling);
            }
        }

        static float[] Samples(AudioClip clip)
        {
            var d = new float[clip.samples * clip.channels];
            clip.GetData(d, 0);
            Object.Destroy(clip);
            return d;
        }

        static void AddSamples(float[] b, float[] src, float startSeconds, float gain)
        {
            int i0 = Mathf.Max(0, Mathf.RoundToInt(startSeconds * SR));
            for (int k = 0; k < src.Length; k++)
            {
                int i = i0 + k;
                if (i >= b.Length)
                {
                    break;
                }

                b[i] += src[k] * gain;
            }
        }

        /// <summary>Band-limited noise burst with an exponential decay. Roughly rms-normalised so gain means the same in any band.</summary>
        static void AddNoise(float[] b, System.Random r, float start, float dur, float loHz, float hiHz, float gain, float decay, float attack = 0.003f)
        {
            int i0 = Mathf.Max(0, Mathf.RoundToInt(start * SR));
            int n = Mathf.RoundToInt(dur * SR);
            float aHi = OnePole(hiHz);
            float aLo = OnePole(loHz);
            float bw = Mathf.Clamp((hiHz - loHz) / (SR * 0.5f), 0.01f, 1f);
            float comp = Mathf.Min(12f, 1f / Mathf.Sqrt(bw));
            int att = Mathf.Max(1, Mathf.RoundToInt(attack * SR));
            float hi = 0f;
            float lo = 0f;
            for (int k = 0; k < n; k++)
            {
                int i = i0 + k;
                if (i >= b.Length)
                {
                    break;
                }

                float x = (float)(r.NextDouble() * 2.0 - 1.0);
                hi += (x - hi) * aHi;
                lo += (hi - lo) * aLo;
                float band = hi - lo;
                float env = Mathf.Exp(-decay * k / n) * Mathf.Min(1f, k / (float)att);
                b[i] += band * comp * 0.6f * env * gain;
            }
        }

        /// <summary>Kick-like body: sine with a fast pitch drop, a little 2nd/3rd harmonic so it reads on phone speakers.</summary>
        static void AddThud(float[] b, float start, float f0, float f1, float dur, float gain, float decay = 5f)
        {
            int i0 = Mathf.Max(0, Mathf.RoundToInt(start * SR));
            int n = Mathf.RoundToInt(dur * SR);
            float phase = 0f;
            float drop = dur * 0.12f;
            for (int k = 0; k < n; k++)
            {
                int i = i0 + k;
                if (i >= b.Length)
                {
                    break;
                }

                float t = k / (float)SR;
                float f = f1 + (f0 - f1) * Mathf.Exp(-t / drop);
                phase += 2f * Mathf.PI * f / SR;
                float env = Mathf.Exp(-decay * k / n) * Mathf.Min(1f, k / (0.002f * SR));
                float w = Mathf.Sin(phase) + 0.45f * Mathf.Sin(2f * phase) + 0.2f * Mathf.Sin(3f * phase);
                b[i] += w * env * gain * 0.7f;
            }
        }

        /// <summary>A dissonant cluster: each semitone offset is two slightly detuned buzzy voices.</summary>
        static void AddCluster(float[] b, float start, float dur, float rootHz, int[] semitones, float gain, float attack, float decay)
        {
            int i0 = Mathf.Max(0, Mathf.RoundToInt(start * SR));
            int n = Mathf.RoundToInt(dur * SR);
            int att = Mathf.Max(1, Mathf.RoundToInt(attack * SR));
            for (int v = 0; v < semitones.Length; v++)
            {
                float f = rootHz * Mathf.Pow(2f, semitones[v] / 12f);
                for (int d = 0; d < 2; d++)
                {
                    float fd = f * (d == 0 ? 0.997f : 1.004f);
                    float inc = 2f * Mathf.PI * fd / SR;
                    float phase = v * 1.7f + d * 0.9f;
                    for (int k = 0; k < n; k++)
                    {
                        int i = i0 + k;
                        if (i >= b.Length)
                        {
                            break;
                        }

                        phase += inc;
                        float env = Mathf.Exp(-decay * k / n) * Mathf.Min(1f, k / (float)att);
                        float w = Mathf.Sin(phase) + 0.5f * Mathf.Sin(2f * phase) + 0.25f * Mathf.Sin(3f * phase);
                        b[i] += w * env * gain * 0.2f;
                    }
                }
            }
        }

        /// <summary>A gliding voice (scream, wail, squeak) with vibrato.</summary>
        static void AddGlide(float[] b, float start, float dur, float f0, float f1, float vibHz, float vibDepth, float gain, float decay = 2.5f)
        {
            int i0 = Mathf.Max(0, Mathf.RoundToInt(start * SR));
            int n = Mathf.RoundToInt(dur * SR);
            float phase = 0f;
            for (int k = 0; k < n; k++)
            {
                int i = i0 + k;
                if (i >= b.Length)
                {
                    break;
                }

                float u = k / (float)n;
                float t = k / (float)SR;
                float f = Mathf.Lerp(f0, f1, u) * (1f + vibDepth * Mathf.Sin(2f * Mathf.PI * vibHz * t));
                phase += 2f * Mathf.PI * f / SR;
                float env = Mathf.Min(1f, u * 12f) * Mathf.Exp(-decay * u);
                float w = Mathf.Sin(phase) + 0.5f * Mathf.Sin(2f * phase) + 0.3f * Mathf.Sin(3f * phase);
                b[i] += w * env * gain * 0.45f;
            }
        }

        static void AddBell(float[] b, float start, float hz, float dur, float gain)
        {
            int i0 = Mathf.Max(0, Mathf.RoundToInt(start * SR));
            int n = Mathf.RoundToInt(dur * SR);
            for (int k = 0; k < n; k++)
            {
                int i = i0 + k;
                if (i >= b.Length)
                {
                    break;
                }

                float t = k / (float)SR;
                float env = Mathf.Exp(-3.2f * t) * Mathf.Min(1f, k / (0.003f * SR));
                float w = Mathf.Sin(2f * Mathf.PI * hz * t)
                          + 0.35f * Mathf.Sin(2f * Mathf.PI * hz * 2.76f * t) * Mathf.Exp(-5f * t)
                          + 0.15f * Mathf.Sin(2f * Mathf.PI * hz * 5.4f * t) * Mathf.Exp(-9f * t);
                b[i] += w * env * gain * 0.5f;
            }
        }

        static void Seamless(float[] raw, float[] dst)
        {
            int half = raw.Length / 2;
            for (int i = 0; i < raw.Length; i++)
            {
                float fade = i / (float)raw.Length;
                dst[i] = Mathf.Lerp(raw[i], raw[(i + half) % raw.Length], fade);
            }
        }

        // ---------------------------------------------------------------- loops and one-shots

        public static AudioClip Heartbeat()
        {
            float[] b = Buf(0.7f);
            AddThud(b, 0f, 66f, 40f, 0.24f, 1f, 6f);
            AddThud(b, 0.26f, 74f, 44f, 0.2f, 0.7f, 6f);
            PeakNormalize(b, 0.8f);
            FadeEnds(b, 0.002f, 0.04f);
            return MakeClip(b, "CoasterHeartbeat");
        }

        public static AudioClip WindLoop(float seconds, int seed)
        {
            float[] raw = Buf(seconds);
            AddNoise(raw, new System.Random(seed), 0f, seconds, 250f, 1800f, 1f, 0f, 0.001f);
            // Slow swell so the loop is never a flat hiss.
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SR;
                raw[i] *= 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.35f * t);
            }

            float[] dst = new float[raw.Length];
            Seamless(raw, dst);
            PeakNormalize(dst, 0.8f);
            return MakeClip(dst, "CoasterWind");
        }

        public static AudioClip Clank(int seed, int variant)
        {
            var r = new System.Random(seed);
            float[] b = Buf(0.28f);
            AddNoise(b, r, 0f, 0.05f, 1500f, 7000f, 0.9f, 7f, 0.0005f);
            float[] partials = { 780f, 1290f, 2140f };
            for (int p = 0; p < partials.Length; p++)
            {
                float f = partials[p] * (1f + 0.05f * variant + 0.02f * p);
                for (int k = 0; k < b.Length; k++)
                {
                    float t = k / (float)SR;
                    b[k] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-(14f + 6f * p) * t) * (0.5f / (p + 1));
                }
            }

            AddThud(b, 0f, 160f, 80f, 0.18f, 0.9f, 8f);
            PeakNormalize(b, 0.8f);
            FadeEnds(b, 0.0005f, 0.03f);
            return MakeClip(b, "CoasterClank" + variant);
        }

        /// <summary>Stick-slip wood creak: a gliding buzzy tone, amplitude chopped into pulses, then low-passed.</summary>
        public static AudioClip Creak(int seed, int variant)
        {
            var r = new System.Random(seed);
            float dur = 0.8f + 0.15f * variant;
            float[] b = Buf(dur);
            float phase = 0f;
            float pulse = 0f;
            float f0 = 150f + 25f * variant;
            float f1 = 105f + 15f * variant;
            for (int i = 0; i < b.Length; i++)
            {
                float u = i / (float)b.Length;
                float f = Mathf.Lerp(f0, f1, u) * (1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 9f * i / SR));
                phase += 2f * Mathf.PI * f / SR;
                float rate = 22f * (1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 1.3f * i / SR + variant));
                pulse += 2f * Mathf.PI * rate / SR;
                float chop = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(pulse), 2f);
                float w = 0f;
                for (int h = 1; h <= 6; h++)
                {
                    w += Mathf.Sin(h * phase) / h;
                }

                float env = Mathf.Sin(Mathf.PI * u);
                b[i] = w * chop * env + (float)(r.NextDouble() * 2.0 - 1.0) * 0.04f * env;
            }

            LowPass(b, 1400f);
            PeakNormalize(b, 0.7f);
            FadeEnds(b, 0.01f, 0.05f);
            return MakeClip(b, "CoasterCreak" + variant);
        }

        /// <summary>A warm, slower, lower hinge creak (no harsh harmonics).</summary>
        public static AudioClip DoorCreak(int seed)
        {
            var r = new System.Random(seed);
            float[] b = Buf(1.6f);
            float phase = 0f;
            float pulse = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float u = i / (float)b.Length;
                float f = 92f + 40f * Mathf.Sin(Mathf.PI * u) + 8f * u;
                phase += 2f * Mathf.PI * f / SR;
                pulse += 2f * Mathf.PI * (13f + 5f * u) / SR;
                float chop = 0.55f + 0.45f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(pulse), 1.5f);
                float w = Mathf.Sin(phase) + 0.6f * Mathf.Sin(2f * phase) + 0.35f * Mathf.Sin(3f * phase) + 0.15f * Mathf.Sin(5f * phase);
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.8f);
                b[i] = w * chop * env + (float)(r.NextDouble() * 2.0 - 1.0) * 0.03f * env;
            }

            LowPass(b, 900f);
            PeakNormalize(b, 0.7f);
            FadeEnds(b, 0.02f, 0.08f);
            return MakeClip(b, "CoasterDoorCreak");
        }

        /// <summary>Breathy whisper swell: syllable-chopped band noise that grows to a peak at about 2.0 s, ends at 2.6 s.</summary>
        public static AudioClip Whisper(int seed)
        {
            var r = new System.Random(seed);
            const float dur = 2.6f;
            float[] mid = Buf(dur);
            float[] top = Buf(dur);
            AddNoise(mid, r, 0f, dur, 1200f, 2800f, 1f, 0f, 0.001f);
            AddNoise(top, r, 0f, dur, 3500f, 7500f, 0.6f, 0f, 0.001f);

            // Syllable windows: denser and louder toward the peak.
            var env = new float[mid.Length];
            float t = 0.1f;
            while (t < 2.3f)
            {
                float len = 0.12f + 0.18f * (float)r.NextDouble();
                float amp = 0.5f + 0.5f * (float)r.NextDouble();
                int i0 = Mathf.Max(0, Mathf.RoundToInt(t * SR));
                int n = Mathf.RoundToInt(len * SR);
                for (int k = 0; k < n && i0 + k < env.Length; k++)
                {
                    float w = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * k / n);
                    env[i0 + k] = Mathf.Max(env[i0 + k], w * amp);
                }

                t += len * (0.55f + 0.5f * (float)r.NextDouble());
            }

            var b = new float[mid.Length];
            for (int i = 0; i < b.Length; i++)
            {
                float u = i / (float)SR;
                float swell = u < 2f ? Mathf.Pow(u / 2f, 1.6f) : Mathf.Clamp01(1f - (u - 2f) / 0.6f);
                b[i] = (mid[i] + top[i]) * env[i] * swell;
            }

            PeakNormalize(b, 0.7f);
            FadeEnds(b, 0.01f, 0.05f);
            return MakeClip(b, "CoasterWhisper");
        }

        public static AudioClip CandyShower(int seed)
        {
            var r = new System.Random(seed);
            float[] b = Buf(1.8f);

            // Crinkle: dozens of tiny bright ticks, thickest at the start.
            for (int i = 0; i < 70; i++)
            {
                float t = 0.9f * Mathf.Pow((float)r.NextDouble(), 1.7f);
                float g = 0.3f + 0.7f * (float)r.NextDouble();
                AddNoise(b, r, t, 0.006f + 0.01f * (float)r.NextDouble(), 3000f, 9500f, g, 6f, 0.0003f);
            }

            // Four-bell run, pentatonic so nothing is sour.
            float[] semis = { 0f, 4f, 7f, 12f };
            for (int i = 0; i < semis.Length; i++)
            {
                AddBell(b, 0.05f + 0.11f * i, 880f * Mathf.Pow(2f, semis[i] / 12f), 1.2f, 0.8f);
            }

            AddSamples(b, Samples(ProceduralToneBank.CreateGlassClink(0.7f, 1.0f, seed + 3)), 0.3f, 0.35f);
            PeakNormalize(b, 0.8f);
            FadeEnds(b, 0.001f, 0.1f);
            return MakeClip(b, "CoasterCandy");
        }

        // ---------------------------------------------------------------- music box

        public static MelodySequence DiesIrae()
        {
            // Gregorian sequence "Dies irae" (public domain). First phrase F E F D E C D, then an answering phrase
            // in the same shape. Semitones are relative to D5.
            var m = ScriptableObject.CreateInstance<MelodySequence>();
            m.songTitle = "DIES IRAE";
            m.source = "Dies irae, Gregorian sequence (public domain). First phrase as chanted; the answering phrase is an approximation.";
            m.tonicHz = 587.33f;
            int[] a = { 3, 2, 3, 0, 2, -2, 0 };
            int[] c = { 3, 5, 3, 2, 3, 2, 0 };
            AddPhrase(m, a);
            AddPhrase(m, a);
            AddPhrase(m, c);
            return m;
        }

        static void AddPhrase(MelodySequence m, int[] semis)
        {
            for (int i = 0; i < semis.Length; i++)
            {
                bool last = i == semis.Length - 1;
                m.notes.Add(new MelodyNote { semitone = semis[i], beats = last ? 2f : 1f, endsPhrase = last });
            }
        }

        public static AudioClip MusicBoxNote(float hz, int seed)
        {
            float[] b = Buf(1.9f);
            var r = new System.Random(seed);
            float[] ratio = { 1f, 2f, 3.01f, 6.27f };
            float[] amp = { 1f, 0.35f, 0.18f, 0.12f };
            float[] dec = { 2.6f, 4.2f, 5.5f, 13f };
            for (int p = 0; p < ratio.Length; p++)
            {
                float f = hz * ratio[p];
                for (int k = 0; k < b.Length; k++)
                {
                    float t = k / (float)SR;
                    b[k] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-dec[p] * t) * amp[p];
                }
            }

            AddNoise(b, r, 0f, 0.004f, 2500f, 9000f, 0.35f, 3f, 0.0003f);
            PeakNormalize(b, 0.8f);
            FadeEnds(b, 0.0005f, 0.12f);
            return MakeClip(b, "MusicBox" + Mathf.RoundToInt(hz));
        }

        // ---------------------------------------------------------------- DSP for the jump-scare sound design

        static float Rnd(System.Random r)
        {
            return (float)(r.NextDouble() * 2.0 - 1.0);
        }

        static float Rnd01(System.Random r)
        {
            return (float)r.NextDouble();
        }

        /// <summary>State-variable band-pass. q is the damping (1/Q): 0.12 is a resonant formant, 0.4 a wide sweep.</summary>
        struct Svf
        {
            float _low, _band;

            public float Band(float x, float fc, float q)
            {
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(fc, SR * 0.16f) / SR);
                _low += f * _band;
                float high = x - _low - q * _band;
                _band += f * high;
                return _band;
            }
        }

        internal enum VoiceKind { Female = 0, Male = 1, Inhuman = 2 }

        /// <summary>Normalise a scratch buffer to 1 and mix it in. All the "tmp" helpers below shape their own envelope first.</summary>
        static void Place(float[] b, float[] tmp, float start, float gain)
        {
            PeakNormalize(tmp, 1f);
            AddSamples(b, tmp, start, gain);
        }

        static float Release(float u, float from)
        {
            return u < from ? 1f : 1f - CameraSpring.SmoothStep01((u - from) / Mathf.Max(1e-3f, 1f - from));
        }

        /// <summary>
        /// A screaming voice: sawtooth source (plus a sub-octave for the inhuman one) with a fast pitch glide up, vibrato, jitter and
        /// breath noise, pushed through three formant band-passes that open as the mouth does, then driven into a soft clip for grit.
        /// </summary>
        internal static float[] MakeVoice(System.Random r, float dur, VoiceKind vk, float pitchScale, float lowpassHz)
        {
            int n = Mathf.Max(8, Mathf.RoundToInt(dur * SR));
            var o = new float[n];
            float f0, f1, vibHz, vibD, breath, drive, c0, c1, c2, a1, a2;
            switch (vk)
            {
                case VoiceKind.Female:
                    f0 = 620f; f1 = 1480f; vibHz = 6.5f; vibD = 0.035f; breath = 0.12f; drive = 2f;
                    c0 = 850f; c1 = 1300f; c2 = 3000f; a1 = 0.7f; a2 = 0.35f;
                    break;
                case VoiceKind.Male:
                    f0 = 210f; f1 = 500f; vibHz = 5.5f; vibD = 0.04f; breath = 0.18f; drive = 2.5f;
                    c0 = 720f; c1 = 1100f; c2 = 2500f; a1 = 0.7f; a2 = 0.3f;
                    break;
                default:
                    f0 = 140f; f1 = 1150f; vibHz = 9f; vibD = 0.08f; breath = 0.35f; drive = 4f;
                    c0 = 430f; c1 = 1900f; c2 = 3300f; a1 = 1f; a2 = 0.6f;
                    break;
            }

            bool sub = vk == VoiceKind.Inhuman;
            var s0 = default(Svf);
            var s1 = default(Svf);
            var s2 = default(Svf);
            const float q = 0.12f;
            float phase = 0f, phase2 = 0f, jit = 0f;
            float norm = 1f / (1f - Mathf.Exp(-6f));
            for (int k = 0; k < n; k++)
            {
                float u = k / (float)n;
                float t = k / (float)SR;
                float g = (1f - Mathf.Exp(-6f * u)) * norm;
                jit += (Rnd(r) * 0.05f - jit) * 0.004f;
                float vib = vibD * Mathf.Sin(2f * Mathf.PI * vibHz * t) * Mathf.Min(1f, u * 5f);
                float f = Mathf.Lerp(f0, f1, g) * pitchScale * (1f + vib + jit);
                phase += f / SR;
                phase -= Mathf.Floor(phase);
                float src = 2f * phase - 1f;
                if (sub)
                {
                    phase2 += 0.5f * f / SR;
                    phase2 -= Mathf.Floor(phase2);
                    src = 0.7f * src + 0.6f * (2f * phase2 - 1f);
                }

                src += breath * Rnd(r);
                float open = 1f + 0.2f * g;
                o[k] = s0.Band(src, c0 * open, q) + a1 * s1.Band(src, c1 * (1f + 0.1f * g), q) + a2 * s2.Band(src, c2, q);
            }

            PeakNormalize(o, 1f);
            float dn = (float)System.Math.Tanh(drive);
            int att = Mathf.Max(1, Mathf.RoundToInt(0.012f * SR));
            for (int k = 0; k < n; k++)
            {
                float u = k / (float)n;
                float t = k / (float)SR;
                float env = Mathf.Min(1f, k / (float)att) * (1f + 0.12f * Mathf.Sin(2f * Mathf.PI * 11f * t)) * Release(u, 0.72f);
                o[k] = (float)System.Math.Tanh(drive * o[k]) / dn * env;
            }

            if (lowpassHz > 0f)
            {
                LowPass(o, lowpassHz);
            }

            PeakNormalize(o, 0.9f);
            return o;
        }

        /// <summary>Low sub-harmonic growl: a falling sawtooth, chopped by a 26-32 Hz amplitude modulation, with noise, through two low formants.</summary>
        static void AddGrowl(float[] b, System.Random r, float start, float dur, float f0, float gain)
        {
            int n = Mathf.RoundToInt(dur * SR);
            var tmp = new float[n];
            var s0 = default(Svf);
            var s1 = default(Svf);
            float phase = 0f;
            int att = Mathf.Max(1, Mathf.RoundToInt(0.01f * SR));
            for (int k = 0; k < n; k++)
            {
                float u = k / (float)n;
                float t = k / (float)SR;
                float f = f0 * (1f - 0.2f * u) * (1f + 0.03f * Mathf.Sin(2f * Mathf.PI * 7f * t));
                phase += f / SR;
                phase -= Mathf.Floor(phase);
                float am = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (26f + 6f * u) * t);
                float src = (0.8f * (2f * phase - 1f) + 0.35f * Rnd(r)) * am;
                tmp[k] = (s0.Band(src, 380f, 0.2f) + 0.8f * s1.Band(src, 800f, 0.2f)) * Mathf.Min(1f, k / (float)att) * Release(u, 0.65f);
            }

            Place(b, tmp, start, gain);
        }

        /// <summary>Sub-boom: a cosine at <paramref name="hz"/> that holds for <paramref name="holdSec"/> (40 ms), then dies away.</summary>
        static void AddBoom(float[] b, float start, float hz, float holdSec, float tailSec, float gain)
        {
            int i0 = Mathf.Max(0, Mathf.RoundToInt(start * SR));
            int n = Mathf.RoundToInt((holdSec + tailSec) * SR);
            for (int k = 0; k < n; k++)
            {
                int i = i0 + k;
                if (i >= b.Length)
                {
                    break;
                }

                float t = k / (float)SR;
                float env = Mathf.Min(1f, k / (0.0008f * SR)) * (t < holdSec ? 1f : Mathf.Exp(-(t - holdSec) / (tailSec * 0.35f)));
                float w = Mathf.Cos(2f * Mathf.PI * hz * t) + 0.4f * Mathf.Cos(4f * Mathf.PI * hz * t) + 0.15f * Mathf.Cos(6f * Mathf.PI * hz * t);
                b[i] += w * env * gain * 0.7f;
            }
        }

        static void AddPing(float[] b, float start, float hz, float dur, float gain, float decay)
        {
            int i0 = Mathf.Max(0, Mathf.RoundToInt(start * SR));
            int n = Mathf.RoundToInt(dur * SR);
            for (int k = 0; k < n; k++)
            {
                int i = i0 + k;
                if (i >= b.Length)
                {
                    break;
                }

                float t = k / (float)SR;
                b[i] += Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-decay * t) * Mathf.Min(1f, k / (0.0003f * SR)) * gain;
            }
        }

        /// <summary>A dry snap: a hard noise tick plus two damped resonances. Bone cracking, wood splitting.</summary>
        static void AddCrack(float[] b, System.Random r, float start, float gain)
        {
            AddNoise(b, r, start, 0.014f, 1500f, 9000f, gain, 8f, 0.0002f);
            AddPing(b, start, 1800f + 600f * Rnd01(r), 0.05f, gain * 0.5f, 90f);
            AddPing(b, start + 0.03f, 3100f + 800f * Rnd01(r), 0.04f, gain * 0.35f, 110f);
            AddThud(b, start, 220f, 110f, 0.05f, gain * 0.4f, 8f);
        }

        static void AddWetThud(float[] b, System.Random r, float start, float gain, int seed)
        {
            AddThud(b, start, 75f, 38f, 0.4f, gain, 5f);
            AddNoise(b, r, start, 0.3f, 250f, 1100f, gain * 0.7f, 7f, 0.002f);
            AddSamples(b, Samples(ProceduralToneBank.CreateSquelch(1f, seed)), start, gain * 0.5f);
        }

        static void AddGlassBreak(float[] b, System.Random r, float start, float gain, int seed)
        {
            AddSamples(b, Samples(ProceduralToneBank.CreateShatter(1.3f, seed)), start, gain);
            AddNoise(b, r, start, 0.15f, 3000f, 12000f, gain * 0.8f, 6f, 0.0003f);
            for (int i = 0; i < 40; i++)
            {
                float t = start + 0.02f + 0.9f * Mathf.Pow(Rnd01(r), 1.5f);
                AddPing(b, t, 3000f + 6000f * Rnd01(r), 0.1f + 0.1f * Rnd01(r), gain * (0.12f + 0.25f * Rnd01(r)), 28f);
            }
        }

        /// <summary>Fast tick bursts, like many tiny legs on a floor.</summary>
        static void AddSkitter(float[] b, System.Random r, float start, float dur, float gain)
        {
            float t = start;
            float end = start + dur;
            while (t < end)
            {
                int ticks = 3 + r.Next(4);
                for (int i = 0; i < ticks; i++)
                {
                    AddNoise(b, r, t + i * (0.014f + 0.008f * Rnd01(r)), 0.004f, 2500f, 9000f, gain * (0.5f + 0.5f * Rnd01(r)), 8f, 0.0002f);
                }

                t += 0.06f + 0.1f * Rnd01(r);
            }
        }

        /// <summary>Clattering bones: bursts of short, bright, non-harmonic pings.</summary>
        static void AddRattle(float[] b, System.Random r, float start, float dur, float gain)
        {
            int count = Mathf.RoundToInt(dur * 42f);
            for (int i = 0; i < count; i++)
            {
                float u = Mathf.Pow(Rnd01(r), 0.8f);
                float t = start + dur * u;
                AddPing(b, t, 700f + 2000f * Rnd01(r), 0.03f, gain * (0.35f + 0.65f * Rnd01(r)) * (1f - 0.5f * u), 75f);
                AddNoise(b, r, t, 0.004f, 2000f, 8000f, gain * 0.3f, 8f, 0.0002f);
            }
        }

        /// <summary>Metallic scratch: a narrow, resonant noise band sweeping up, chopped at about 30 Hz.</summary>
        static void AddScrape(float[] b, System.Random r, float start, float dur, float f0, float f1, float gain)
        {
            int n = Mathf.RoundToInt(dur * SR);
            var tmp = new float[n];
            var s = default(Svf);
            for (int k = 0; k < n; k++)
            {
                float u = k / (float)n;
                float t = k / (float)SR;
                float fc = Mathf.Lerp(f0, f1, u) * (1f + 0.05f * Mathf.Sin(2f * Mathf.PI * 40f * t));
                float chop = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (26f + 8f * Mathf.Sin(t * 5f)) * t), 1.5f);
                tmp[k] = s.Band(Rnd(r), fc, 0.07f) * (0.3f + 0.7f * chop) * Mathf.Min(1f, u * 15f) * Release(u, 0.8f);
            }

            Place(b, tmp, start, gain);
        }

        /// <summary>Band-passed noise sweeping from lo to hi; rising = crescendo (a rush toward the listener).</summary>
        static void AddSweepNoise(float[] b, System.Random r, float start, float dur, float fLo, float fHi, float gain)
        {
            int n = Mathf.RoundToInt(dur * SR);
            var tmp = new float[n];
            var s = default(Svf);
            for (int k = 0; k < n; k++)
            {
                float u = k / (float)n;
                float fc = Mathf.Lerp(fLo, fHi, u * u);
                tmp[k] = s.Band(Rnd(r), fc, 0.35f) * u * u * Mathf.Min(1f, (1f - u) * 80f);
            }

            Place(b, tmp, start, gain);
        }

        /// <summary>A doppler-ish pass-by: wide band-passed noise, loudest about a third in, the centre frequency falling fast after it.</summary>
        static void AddPassBy(float[] b, System.Random r, float start, float dur, float gain, float fHigh)
        {
            int n = Mathf.RoundToInt(dur * SR);
            var tmp = new float[n];
            var s = default(Svf);
            for (int k = 0; k < n; k++)
            {
                float u = k / (float)n;
                float fc = 320f + fHigh * Mathf.Exp(-2.2f * u);
                float env = u < 0.33f ? Mathf.Pow(u / 0.33f, 2f) : Mathf.Exp(-(u - 0.33f) * 5.5f);
                tmp[k] = s.Band(Rnd(r), fc, 0.45f) * env * Mathf.Min(1f, (1f - u) * 40f);
            }

            Place(b, tmp, start, gain);
        }

        /// <summary>Wood creak, array form: rising or falling buzzy tone chopped into stick-slip pulses, low-passed.</summary>
        static float[] CreakArray(System.Random r, float dur, float f0, float f1, float rate)
        {
            float[] b = Buf(dur);
            float phase = 0f;
            float pulse = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float u = i / (float)b.Length;
                float f = Mathf.Lerp(f0, f1, u) * (1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 9f * i / SR));
                phase += 2f * Mathf.PI * f / SR;
                pulse += 2f * Mathf.PI * rate * (1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 1.1f * i / SR)) / SR;
                float chop = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(pulse), 2f);
                float w = 0f;
                for (int h = 1; h <= 6; h++)
                {
                    w += Mathf.Sin(h * phase) / h;
                }

                b[i] = (w * chop + Rnd(r) * 0.04f) * Mathf.Sin(Mathf.PI * u);
            }

            LowPass(b, 1500f);
            PeakNormalize(b, 1f);
            return b;
        }

        static void AddPanned(float[] l, float[] rr, float[] src, float start, float pan, float gain)
        {
            float ang = (Mathf.Clamp(pan, -1f, 1f) + 1f) * Mathf.PI * 0.25f;
            AddSamples(l, src, start, gain * Mathf.Cos(ang));
            AddSamples(rr, src, start, gain * Mathf.Sin(ang));
        }

        static AudioClip MakeStereo(float[] l, float[] rr, string clipName)
        {
            var d = new float[l.Length * 2];
            for (int i = 0; i < l.Length; i++)
            {
                d[2 * i] = l[i];
                d[2 * i + 1] = rr[i];
            }

            var clip = AudioClip.Create(clipName, l.Length, 2, SR, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>The ordinary Finish for two channels with one shared gain, so the pan image survives.</summary>
        static void FinishStereo(float[] l, float[] rr, float level)
        {
            float max = 1e-6f;
            for (int i = 0; i < l.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(l[i]), Mathf.Abs(rr[i]));
            }

            const float drive = 1.5f;
            float dn = (float)System.Math.Tanh(drive);
            float ceiling = Peak * Mathf.Clamp01(level);
            float max2 = 1e-6f;
            for (int i = 0; i < l.Length; i++)
            {
                l[i] = (float)System.Math.Tanh(drive * l[i] / max) / dn;
                rr[i] = (float)System.Math.Tanh(drive * rr[i] / max) / dn;
                max2 = Mathf.Max(max2, Mathf.Abs(l[i]), Mathf.Abs(rr[i]));
            }

            FadeEnds(l, 0.001f, 0.03f);
            FadeEnds(rr, 0.001f, 0.03f);
            for (int i = 0; i < l.Length; i++)
            {
                l[i] = Mathf.Clamp(l[i] / max2 * ceiling, -ceiling, ceiling);
                rr[i] = Mathf.Clamp(rr[i] / max2 * ceiling, -ceiling, ceiling);
            }
        }

        /// <summary>
        /// The shared jump-scare hit, always at sample 0 of the sting: a 40 ms sub-boom at 45 Hz, a broadband noise burst and a
        /// screeching dissonant cluster. Everything kind-specific is layered on top of it.
        /// </summary>
        static void AddImpact(float[] b, System.Random r, float screechRoot, float boom, float noise)
        {
            AddBoom(b, 0f, 45f, 0.04f, 0.5f, boom);
            AddNoise(b, r, 0f, 0.28f, 90f, 12000f, noise, 6f, 0.0004f);
            AddCluster(b, 0f, 1.0f, screechRoot, new[] { 0, 1, 6, 11 }, 0.55f, 0.002f, 3.2f);
            AddGlide(b, 0f, 0.5f, screechRoot * 3f, screechRoot * 5f, 13f, 0.04f, 0.3f, 3f);
        }

        // ---------------------------------------------------------------- new loops and one-shots

        /// <summary>Bright wind: the high band that makes speed feel fast. Same construction as <see cref="WindLoop"/>.</summary>
        public static AudioClip BrightWindLoop(float seconds, int seed)
        {
            float[] raw = Buf(seconds);
            AddNoise(raw, new System.Random(seed), 0f, seconds, 1800f, 7500f, 1f, 0f, 0.001f);
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SR;
                raw[i] *= 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
            }

            float[] dst = new float[raw.Length];
            Seamless(raw, dst);
            PeakNormalize(dst, 0.8f);
            return MakeClip(dst, "CoasterWindHi");
        }

        /// <summary>A low detuned drone that loops exactly: every partial sits on a whole number of cycles per loop.</summary>
        public static AudioClip DroneLoop(int seed)
        {
            const float len = 6f;
            float[] b = Buf(len);
            // Frequencies as k / len, so the loop is seamless. 330 -> 55.0 Hz, 331 -> 55.17 Hz (slow beat), 350 -> 58.33 Hz (minor second).
            int[] ks = { 330, 331, 350, 495, 494 };
            float[] amps = { 1f, 0.9f, 0.55f, 0.45f, 0.4f };
            for (int p = 0; p < ks.Length; p++)
            {
                float f = ks[p] / len;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float ph = 2f * Mathf.PI * f * t;
                    float w = Mathf.Sin(ph) + 0.5f * Mathf.Sin(2f * ph) + 0.3f * Mathf.Sin(3f * ph) + 0.15f * Mathf.Sin(4f * ph);
                    b[i] += w * amps[p] * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * (p + 1) / len * t));
                }
            }

            float[] noiseRaw = Buf(len);
            AddNoise(noiseRaw, new System.Random(seed), 0f, len, 40f, 240f, 1f, 0f, 0.001f);
            var noise = new float[b.Length];
            Seamless(noiseRaw, noise);
            PeakNormalize(noise, 1f);
            PeakNormalize(b, 1f);
            for (int i = 0; i < b.Length; i++)
            {
                b[i] += 0.35f * noise[i];
            }

            LowPass(b, 420f);
            PeakNormalize(b, 0.8f);
            return MakeClip(b, "CoasterDrone");
        }

        /// <summary>A tonal civil-defence wail, glide 380-560 Hz with a minor-second twin. Loops exactly (period 4 s, 8 s clip).</summary>
        public static AudioClip SirenLoop()
        {
            float[] b = Buf(8f);
            float[] ratios = { 1f, 1.0625f };
            for (int v = 0; v < ratios.Length; v++)
            {
                float phase = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float f = (470f + 90f * Mathf.Sin(2f * Mathf.PI * t / 4f)) * ratios[v];
                    phase += 2f * Mathf.PI * f / SR;
                    b[i] += (Mathf.Sin(phase) + 0.35f * Mathf.Sin(2f * phase) + 0.15f * Mathf.Sin(3f * phase)) * (v == 0 ? 1f : 0.6f);
                }
            }

            LowPass(b, 1800f);
            PeakNormalize(b, 0.7f);
            return MakeClip(b, "CoasterSiren");
        }

        /// <summary>Heavy chain-lift rattle: 33 irregular metallic ticks in a 3 s loop.</summary>
        public static AudioClip ChainLoop(int seed)
        {
            var r = new System.Random(seed);
            float[] b = Buf(3f);
            for (int i = 0; i < 33; i++)
            {
                float t = i * (3f / 33f) + 0.004f * Rnd(r);
                float g = 0.55f + 0.45f * Rnd01(r);
                AddNoise(b, r, t, 0.03f, 1200f, 6500f, g, 7f, 0.0004f);
                AddPing(b, t, 700f + 90f * (i % 3), 0.08f, g * 0.5f, 40f);
                AddThud(b, t, 130f, 70f, 0.06f, g * 0.6f, 8f);
            }

            PeakNormalize(b, 0.8f);
            return MakeClip(b, "CoasterChainLoop");
        }

        public static AudioClip PassBy(int seed, int variant)
        {
            var r = new System.Random(seed);
            float[] b = Buf(1.8f + 0.3f * variant);
            AddPassBy(b, r, 0f, b.Length / (float)SR, 1f, 2200f + 600f * variant);
            PeakNormalize(b, 0.8f);
            FadeEnds(b, 0.01f, 0.05f);
            return MakeClip(b, "CoasterPassBy" + variant);
        }

        /// <summary>A scream as a ready clip (female, male, inhuman), 0.9-1.2 s. Played by the audio on the sting frame.</summary>
        public static AudioClip Scream(VoiceKind vk, int seed)
        {
            var r = new System.Random(seed);
            float dur = vk == VoiceKind.Inhuman ? 1.2f : (vk == VoiceKind.Male ? 1.0f : 0.9f);
            float[] v = MakeVoice(r, dur, vk, 1f, 0f);
            // A dissonant twin a semitone and a bit up thickens it (scream chorus).
            float[] twin = MakeVoice(r, dur, vk, 1.07f, 0f);
            for (int i = 0; i < v.Length; i++)
            {
                v[i] += 0.5f * twin[i];
            }

            PeakNormalize(v, Peak);
            FadeEnds(v, 0.0015f, 0.04f);
            return MakeClip(v, "CoasterScream_" + vk);
        }

        /// <summary>The short human scream of a rider being taken (0.55 s of voice, then a ragged tail). Pitch and volume fall in the audio.</summary>
        public static AudioClip TakenScream(bool female, int seed)
        {
            var r = new System.Random(seed);
            float[] b = Buf(0.9f);
            float[] v = MakeVoice(r, 0.6f, female ? VoiceKind.Female : VoiceKind.Male, 1f, 0f);
            AddSamples(b, v, 0f, 1f);
            PeakNormalize(b, 0.85f);
            FadeEnds(b, 0.002f, 0.1f);
            return MakeClip(b, female ? "CoasterTakenF" : "CoasterTakenM");
        }

        /// <summary>A far-away scream, filtered dark and smeared with echoes.</summary>
        public static AudioClip DistantScream(int seed)
        {
            var r = new System.Random(seed);
            VoiceKind vk = (VoiceKind)(seed % 3);
            float[] v = MakeVoice(r, 1.0f, vk, 1f, 1300f);
            float[] b = Buf(2.2f);
            AddSamples(b, v, 0f, 1f);
            AddSamples(b, v, 0.33f, 0.4f);
            AddSamples(b, v, 0.7f, 0.18f);
            LowPass(b, 1500f);
            PeakNormalize(b, 0.8f);
            FadeEnds(b, 0.01f, 0.1f);
            return MakeClip(b, "CoasterDistantScream");
        }

        /// <summary>A far-away cackle: six falling "ha" pulses with echo.</summary>
        public static AudioClip DistantLaugh(int seed)
        {
            var r = new System.Random(seed);
            bool female = (seed & 1) == 0;
            float[] b = Buf(2.6f);
            for (int i = 0; i < 6; i++)
            {
                float[] ha = MakeVoice(r, 0.15f, female ? VoiceKind.Female : VoiceKind.Male, 0.55f * (1f - 0.05f * i), 1800f);
                float t = 0.05f + i * 0.21f + 0.01f * Rnd(r);
                AddSamples(b, ha, t, 1f - 0.1f * i);
                AddSamples(b, ha, t + 0.3f, 0.3f * (1f - 0.1f * i));
            }

            LowPass(b, 1700f);
            PeakNormalize(b, 0.8f);
            FadeEnds(b, 0.01f, 0.1f);
            return MakeClip(b, "CoasterDistantLaugh");
        }

        // ---------------------------------------------------------------- PreQuiet cues (micro scares)

        public const float CueSeconds = 0.9f;

        /// <summary>
        /// The 0.9 s lead-in of a micro scare, built so its LAST sample is its loudest (the audio seeks into it to end on the sting).
        /// Quiet by design: a micro PreQuiet is only 0.3-0.9 s.
        /// </summary>
        public static AudioClip Cue(ScareKind kind, int seed)
        {
            var r = new System.Random(seed);
            float[] b = Buf(CueSeconds);
            switch (kind)
            {
                case ScareKind.SpiderDrop:
                    AddSkitter(b, r, 0.15f, 0.7f, 0.8f);
                    break;
                case ScareKind.WindowFace:
                    AddNoise(b, r, 0.2f, 0.4f, 300f, 2500f, 0.4f, 1.5f, 0.2f);
                    AddThud(b, 0.62f, 120f, 70f, 0.15f, 0.7f, 8f);
                    AddPing(b, 0.62f, 320f, 0.2f, 0.4f, 25f);
                    break;
                case ScareKind.SkeletonPop:
                    AddRattle(b, r, 0.3f, 0.6f, 0.45f);
                    break;
                case ScareKind.ClawHand:
                    AddScrape(b, r, 0.1f, 0.78f, 900f, 2600f, 0.6f);
                    break;
                case ScareKind.BatSwarm:
                    for (int i = 0; i < 16; i++)
                    {
                        float u = i / 16f;
                        AddNoise(b, r, 0.05f + 0.052f * i, 0.05f, 600f, 3000f, 0.15f + 0.85f * u * u, 5f, 0.003f);
                    }

                    break;
                case ScareKind.CoffinLid:
                    {
                        float[] c = CreakArray(r, 0.8f, 70f, 190f, 16f);
                        AddSamples(b, c, 0.08f, 1f);
                    }

                    break;
                case ScareKind.ShadowRush:
                    AddSweepNoise(b, r, 0f, CueSeconds, 200f, 3200f, 1f);
                    AddNoise(b, r, 0.1f, 0.78f, 1300f, 2800f, 0.5f, 0f, 0.4f);
                    break;
                default: // MirrorFace
                    AddCrack(b, r, 0.35f, 0.35f);
                    AddCrack(b, r, 0.62f, 0.5f);
                    AddPing(b, 0.1f, 1760f, 0.8f, 0.2f, 3f);
                    break;
            }

            PeakNormalize(b, 0.8f);
            FadeEnds(b, 0.002f, 0.003f);
            return MakeClip(b, "CoasterCue_" + kind);
        }

        // ---------------------------------------------------------------- stings

        /// <summary>
        /// One sting body per scare kind and variant. Every REAL scare starts at sample 0 with <see cref="AddImpact"/> (sub-boom,
        /// broadband burst, screeching cluster); the scream voice is a separate clip the audio plays on the same frame. The kind adds
        /// its own signature on top (growl, wet thud, bone crack, glass, creak and bang, skitter, flaps, rattle, scrape, rush, slam).
        /// Soft clipped and held at -3 dBFS. Fakes stay small. <paramref name="variant"/> 0/1 reseeds and shifts pitches so
        /// back-to-back uses of one kind do not sound identical.
        /// </summary>
        public static AudioClip Sting(ScareKind kind, int seed, int variant)
        {
            if (kind == ScareKind.BatSwarm || kind == ScareKind.BatFlood)
            {
                return BatSwarmSting(seed, variant);
            }

            var r = new System.Random(seed);
            float vs = variant == 0 ? 1f : 1.12f;
            float length;
            float level = 1f;
            switch (kind)
            {
                case ScareKind.SkullFinale: length = 3.8f; break;
                case ScareKind.SprinterLeap: length = 2.0f; break;
                case ScareKind.SideLeap: length = 1.6f; break;
                case ScareKind.SnakeStrike: length = 2.0f; break;
                case ScareKind.SpikeWall: length = 1.8f; break;
                case ScareKind.AsylumFigure: length = 2.6f; break;
                case ScareKind.GraveyardGhost: length = 2.4f; break;
                case ScareKind.GraveHand: length = 2.0f; break;
                case ScareKind.TunnelFace: length = 2.0f; break;
                case ScareKind.PaintingLunge: length = 1.8f; break;
                case ScareKind.DoorGhoul: length = 1.8f; break;
                case ScareKind.CrowBurst: length = 1.8f; break;
                case ScareKind.SpiderDrop: length = 1.5f; break;
                case ScareKind.WindowFace: length = 1.4f; break;
                case ScareKind.SkeletonPop: length = 1.5f; break;
                case ScareKind.ClawHand: length = 1.4f; break;
                case ScareKind.CoffinLid: length = 1.9f; break;
                case ScareKind.ShadowRush: length = 1.6f; break;
                case ScareKind.MirrorFace: length = 1.9f; break;
                case ScareKind.FakeBat: length = 0.9f; level = 0.35f; break;
                case ScareKind.FakeLantern: length = 1.0f; level = 0.3f; break;
                case ScareKind.FakeWheelchair: length = 1.4f; level = 0.35f; break;
                default: length = 1.2f; level = 0.3f; break; // FakeEmptySeat and anything new
            }

            float[] b = Buf(length);
            switch (kind)
            {
                // ----------------------------------------------------- big scares
                case ScareKind.SprinterLeap:
                case ScareKind.SideLeap:
                case ScareKind.SnakeStrike:
                case ScareKind.SpikeWall:
                    // the recordings carry the voice; this is only the sub-boom and a low thud under them
                    AddImpact(b, r, 150f * vs, 1.0f, 0.8f);
                    AddThud(b, 0f, 80f, 38f, 0.5f, 1.0f);
                    break;

                case ScareKind.DoorGhoul:
                    AddImpact(b, r, 196f * vs, 1.2f, 1f);
                    AddGrowl(b, r, 0.05f, 1.2f, 78f * vs, 0.9f);
                    AddNoise(b, r, 0.1f, 0.7f, 3500f, 10000f, 0.5f, 2.5f, 0.04f); // hiss
                    AddThud(b, 0f, 95f, 40f, 0.5f, 0.9f);
                    break;

                case ScareKind.GraveHand:
                    AddImpact(b, r, 174.6f * vs, 1.2f, 0.8f);
                    AddWetThud(b, r, 0.02f, 1.1f, seed + 1);
                    AddCrack(b, r, 0.18f, 0.9f);
                    AddCrack(b, r, 0.31f + 0.04f * variant, 0.7f);
                    AddNoise(b, r, 0.1f, 0.6f, 80f, 2500f, 0.6f, 4f);
                    break;

                case ScareKind.GraveyardGhost:
                    AddImpact(b, r, 233f * vs, 1.0f, 0.8f);
                    AddSamples(b, Samples(ProceduralToneBank.CreateWhoosh(1.1f, seed + 2)), 0f, 0.7f);
                    AddGlide(b, 0.1f, 1.5f, 420f * vs, 900f * vs, 5.5f, 0.04f, 0.45f, 1.5f);
                    AddCluster(b, 0f, 2.2f, 116f, new[] { 0, 1, 7 }, 0.35f, 0.05f, 2.5f);
                    break;

                case ScareKind.CrowBurst:
                    AddImpact(b, r, 207f * vs, 1.0f, 0.9f);
                    for (int i = 0; i < 18; i++)
                    {
                        AddNoise(b, r, 1.0f * Rnd01(r), 0.05f + 0.04f * Rnd01(r), 700f, 3500f, 0.6f + 0.4f * Rnd01(r), 4f, 0.002f);
                    }

                    for (int i = 0; i < 6; i++)
                    {
                        AddGlide(b, 0.05f + 1.2f * Rnd01(r), 0.16f, (900f + 250f * Rnd01(r)) * vs, 520f, 0f, 0f, 0.5f, 3f);
                    }

                    break;

                case ScareKind.TunnelFace:
                    AddImpact(b, r, 185f * vs, 1.2f, 1f);
                    AddSamples(b, Samples(ProceduralToneBank.CreateWhoosh(0.9f, seed + 2)), 0f, 0.9f);
                    AddNoise(b, r, 0.1f, 0.8f, 3500f, 10000f, 0.55f, 2.5f, 0.05f); // hiss
                    AddThud(b, 0f, 85f, 38f, 0.7f, 0.9f);
                    break;

                case ScareKind.PaintingLunge:
                    AddImpact(b, r, 220f * vs, 1.1f, 1.1f);
                    AddNoise(b, r, 0f, 0.5f, 500f, 9000f, 0.9f, 6f);
                    AddGlide(b, 0f, 0.5f, 1800f * vs, 700f, 9f, 0.02f, 0.45f);
                    AddCrack(b, r, 0.05f, 0.8f); // canvas / frame tearing
                    break;

                case ScareKind.AsylumFigure:
                    AddImpact(b, r, 123.5f * vs, 1.2f, 0.8f);
                    AddGrowl(b, r, 0.05f, 1.6f, 58f * vs, 1f);
                    AddScrape(b, r, 0.3f, 1.2f, 700f, 2200f, 0.5f);
                    AddCluster(b, 0f, 2.4f, 123.5f, new[] { 0, 1, 6 }, 0.5f, 0.03f, 2.5f);
                    break;

                case ScareKind.SkullFinale:
                    AddImpact(b, r, 98f * vs, 1.5f, 1.2f);
                    AddThud(b, 0f, 50f, 24f, 1.6f, 1.5f, 3.5f);
                    AddSamples(b, Samples(ProceduralToneBank.CreateImpactThud(0.45f, 0.9f, seed + 1)), 0f, 1.0f);
                    AddSamples(b, Samples(ProceduralToneBank.CreateWhoosh(0.9f, seed + 2)), 0f, 0.8f);
                    AddCluster(b, 0f, 3.6f, 98f, new[] { 0, 1, 6, 11, 13 }, 0.65f, 0.01f, 2.2f);
                    AddGrowl(b, r, 0.1f, 2.0f, 46f, 1f);
                    AddRattle(b, r, 0.3f, 1.2f, 0.7f);
                    AddCrack(b, r, 0.15f, 1f);
                    AddCrack(b, r, 0.4f, 0.8f);
                    AddNoise(b, r, 0.2f, 3.2f, 30f, 180f, 0.6f, 2f, 0.05f);
                    break;

                // ----------------------------------------------------- micro scares
                case ScareKind.SpiderDrop:
                    AddImpact(b, r, 392f * vs, 0.9f, 0.9f);
                    AddNoise(b, r, 0.02f, 0.9f, 4000f, 11000f, 0.8f, 2f, 0.03f); // the hiss
                    AddThud(b, 0.01f, 130f, 60f, 0.15f, 0.7f, 8f);
                    AddSkitter(b, r, 0.25f, 1.0f, 0.5f);
                    break;

                case ScareKind.WindowFace:
                    AddImpact(b, r, 330f * vs, 1.2f, 0.7f);
                    AddThud(b, 0f, 80f, 45f, 0.35f, 1.2f, 6f); // the slam
                    AddPing(b, 0.01f, 280f, 0.4f, 0.5f, 14f); // the pane
                    AddNoise(b, r, 0f, 0.2f, 200f, 2500f, 0.9f, 8f, 0.0005f);
                    {
                        float[] muffled = MakeVoice(r, 0.9f, VoiceKind.Female, 1f * vs, 850f);
                        AddSamples(b, muffled, 0.02f, 0.9f);
                    }

                    break;

                case ScareKind.SkeletonPop:
                    AddImpact(b, r, 262f * vs, 1.0f, 0.9f);
                    AddRattle(b, r, 0.02f, 1.0f, 1f);
                    AddCrack(b, r, 0.06f, 0.9f);
                    AddCrack(b, r, 0.2f, 0.6f);
                    AddGrowl(b, r, 0.1f, 0.8f, 95f * vs, 0.45f);
                    break;

                case ScareKind.ClawHand:
                    AddImpact(b, r, 294f * vs, 1.0f, 0.8f);
                    AddScrape(b, r, 0.0f, 0.7f, 1100f * vs, 3600f * vs, 1f);
                    AddPing(b, 0.02f, 1500f * vs, 0.5f, 0.45f, 9f);
                    AddWetThud(b, r, 0.05f, 0.6f, seed + 1);
                    break;

                case ScareKind.CoffinLid:
                    AddImpact(b, r, 155.6f * vs, 1.3f, 1f);
                    AddThud(b, 0f, 62f, 32f, 0.6f, 1.2f, 5f); // the bang
                    AddNoise(b, r, 0f, 0.3f, 300f, 3000f, 0.9f, 7f, 0.0005f);
                    AddRattle(b, r, 0.05f, 0.4f, 0.5f);
                    AddSamples(b, CreakArray(r, 1.0f, 190f * vs, 90f, 14f), 0.35f, 0.5f);
                    break;

                case ScareKind.ShadowRush:
                    AddImpact(b, r, 164.8f * vs, 1.2f, 0.9f);
                    AddThud(b, 0f, 70f, 36f, 0.5f, 1.1f, 6f);
                    AddNoise(b, r, 0.1f, 1.2f, 1300f, 2800f, 0.4f, 1f, 0.05f); // whisper tail
                    AddGrowl(b, r, 0.0f, 1.0f, 66f * vs, 0.7f);
                    AddPassBy(b, r, 0.15f, 1.2f, 0.7f, 2400f);
                    break;

                case ScareKind.MirrorFace:
                    AddImpact(b, r, 349f * vs, 1.0f, 0.9f);
                    AddGlassBreak(b, r, 0f, 1.0f, seed + 3);
                    AddNoise(b, r, 0.05f, 0.7f, 4000f, 11000f, 0.5f, 2.5f, 0.03f);
                    break;

                // ----------------------------------------------------- fakes (small, unchanged in spirit)
                case ScareKind.FakeBat:
                    for (int i = 0; i < 5; i++)
                    {
                        AddNoise(b, r, 0.08f * i, 0.06f, 900f, 4000f, 0.5f, 4f, 0.002f);
                    }

                    AddGlide(b, 0.1f, 0.12f, 2400f, 3200f, 0f, 0f, 0.3f);
                    break;

                case ScareKind.FakeLantern:
                    AddNoise(b, r, 0f, 0.5f, 400f, 2500f, 0.5f, 4f, 0.02f);
                    AddThud(b, 0f, 120f, 70f, 0.3f, 0.4f);
                    break;

                case ScareKind.FakeWheelchair:
                    AddGlide(b, 0f, 0.6f, 1100f, 1400f, 14f, 0.05f, 0.4f, 1.5f);
                    AddNoise(b, r, 0.1f, 1.0f, 200f, 1500f, 0.35f, 1.5f, 0.05f);
                    break;

                default:
                    AddNoise(b, r, 0f, 1.0f, 100f, 500f, 0.5f, 2f, 0.05f);
                    AddThud(b, 0f, 70f, 45f, 0.5f, 0.6f);
                    AddCluster(b, 0f, 1.1f, 130f, new[] { 0, 1 }, 0.2f, 0.05f, 2.5f);
                    break;
            }

            Finish(b, level);
            return MakeClip(b, "CoasterSting_" + kind + variant);
        }

        /// <summary>The bat swarm sting is stereo: dozens of wing flaps and squeaks pan across the image over 1.2 s, direction by variant.</summary>
        static AudioClip BatSwarmSting(int seed, int variant)
        {
            var r = new System.Random(seed);
            const float length = 1.8f;
            float[] l = Buf(length);
            float[] rr = Buf(length);

            // Impact in both ears, then the swarm.
            var imp = Buf(length);
            AddImpact(imp, r, 370f, 1.0f, 0.9f);
            AddSamples(l, imp, 0f, 1f);
            AddSamples(rr, imp, 0f, 1f);

            var flaps = new float[3][];
            for (int i = 0; i < flaps.Length; i++)
            {
                flaps[i] = Buf(0.08f);
                AddNoise(flaps[i], r, 0f, 0.07f, 500f, 3200f, 1f, 5f, 0.004f);
                AddThud(flaps[i], 0f, 150f + 30f * i, 70f, 0.06f, 0.5f);
                PeakNormalize(flaps[i], 1f);
            }

            float dir = variant == 0 ? 1f : -1f;
            for (int i = 0; i < 70; i++)
            {
                float u = Mathf.Pow(Rnd01(r), 0.85f);
                float t = 0.02f + 1.2f * u;
                float pan = Mathf.Clamp(dir * Mathf.Lerp(-0.95f, 0.95f, u) + 0.25f * Rnd(r), -1f, 1f);
                float g = (0.4f + 0.6f * Rnd01(r)) * (1f - 0.5f * u);
                AddPanned(l, rr, flaps[r.Next(flaps.Length)], t, pan, g);
            }

            for (int i = 0; i < 14; i++)
            {
                float u = Rnd01(r);
                var sq = Buf(0.15f);
                AddGlide(sq, 0f, 0.12f, 3200f + 1500f * Rnd01(r), 5200f + 1500f * Rnd01(r), 0f, 0f, 1f, 3f);
                PeakNormalize(sq, 1f);
                float pan = Mathf.Clamp(dir * Mathf.Lerp(-0.9f, 0.9f, u) + 0.2f * Rnd(r), -1f, 1f);
                AddPanned(l, rr, sq, 0.05f + 1.2f * u, pan, 0.3f + 0.3f * Rnd01(r));
            }

            FinishStereo(l, rr, 1f);
            return MakeStereo(l, rr, "CoasterSting_BatSwarm" + variant);
        }
    }
}
