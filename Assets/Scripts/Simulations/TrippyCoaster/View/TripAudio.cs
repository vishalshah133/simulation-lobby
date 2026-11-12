using UnityEngine;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>
    /// All procedural sound for the ride (house style: nothing sampled, nothing to license). Each world gets its own looping bed built
    /// from its root note and tempo: a detuned pad, a pentatonic arpeggio and a soft sub pulse. Beds cross-fade on world change with a
    /// rising sweep; wind follows speed; a chain click ticks while the cart is on the lift. Reads state only.
    /// </summary>
    public sealed class TripAudio : MonoBehaviour
    {
        const int Rate = 24000;
        const float BaseHz = 110f; // A2

        static readonly int[] Pentatonic = { 0, 3, 5, 7, 10, 12, 15, 17 };

        public TrippyCoasterHost host;

        AudioSource _a, _b, _wind, _fx;
        AudioClip[] _beds;
        AudioClip _sweep, _click;
        int _world = -1;
        bool _aActive;
        float _fade = 1f;
        float _nextClick;

        void Start()
        {
            _a = Source(true);
            _b = Source(true);
            _wind = Source(true);
            _fx = Source(false);
            _wind.clip = MakeWind();
            _wind.volume = 0f;
            _wind.Play();
            _sweep = MakeSweep();
            _click = MakeClick();
            // beds are built lazily on the first Update, once the config is known
        }

        AudioSource Source(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void Update()
        {
            if (host == null || !host.Ready) return;
            ITripState st = host.State;
            TrippyCoasterConfig cfg = st.Config;

            if (_beds == null)
            {
                _beds = new AudioClip[cfg.worlds.Length];
                for (int i = 0; i < _beds.Length; i++) _beds[i] = MakeBed(cfg.worlds[i], i);
            }

            if (st.WorldIndex != _world)
            {
                bool first = _world < 0;
                _world = st.WorldIndex;
                AudioSource next = _aActive ? _b : _a;
                next.clip = _beds[_world];
                next.volume = 0f;
                next.Play();
                _aActive = !_aActive;
                _fade = first ? 0.7f : 0f;
                if (!first) _fx.PlayOneShot(_sweep, 0.5f);
            }

            _fade = Mathf.Min(1f, _fade + Time.deltaTime / 2.2f);
            AudioSource cur = _aActive ? _a : _b;
            AudioSource old = _aActive ? _b : _a;
            cur.volume = 0.5f * _fade;
            old.volume = 0.5f * (1f - _fade);
            if (_fade >= 1f && old.isPlaying) old.Stop();

            float speed01 = Mathf.Clamp01(st.Speed / Mathf.Max(1f, cfg.maxSpeed));
            _wind.volume = Mathf.Lerp(_wind.volume, 0.05f + 0.4f * speed01 * speed01, 1f - Mathf.Exp(-4f * Time.deltaTime));
            _wind.pitch = 0.8f + 0.5f * speed01;

            if (st.OnLift && Time.time >= _nextClick)
            {
                _fx.PlayOneShot(_click, 0.35f);
                _nextClick = Time.time + 0.3f;
            }
        }

        // ------------------------------------------------------------------ synthesis

        static float Hz(int rootSemitone, int step)
        {
            return BaseHz * Mathf.Pow(2f, (rootSemitone + step) / 12f);
        }

        AudioClip MakeBed(TripWorldDef w, int seedSalt)
        {
            float beat = 60f / Mathf.Max(60f, w.tempoBpm);
            int beats = 16;
            int n = Mathf.RoundToInt(beat * beats * Rate);
            var data = new float[n];

            // pad: root, fifth and octave, each a pair of detuned sines with a slow swell, loop-safe frequencies
            float len = n / (float)Rate;
            int[] padSteps = { 0, 7, 12 };
            for (int v = 0; v < padSteps.Length; v++)
            {
                float f = SnapToLoop(Hz(w.rootSemitone, padSteps[v]), len);
                float f2 = SnapToLoop(f * 1.004f, len);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float swell = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * t / len * (v + 1));
                    data[i] += 0.07f * swell * (Mathf.Sin(2f * Mathf.PI * f * t) + Mathf.Sin(2f * Mathf.PI * f2 * t));
                }
            }

            // arpeggio: eighth notes over the pentatonic, a pattern fixed per world
            var rng = new System.Random(1000 + seedSalt * 31);
            int noteLen = Mathf.RoundToInt(beat * 0.5f * Rate);
            int notes = beats * 2;
            for (int k = 0; k < notes; k++)
            {
                int step = Pentatonic[(k * 3 + rng.Next(3)) % Pentatonic.Length];
                float f = Hz(w.rootSemitone, step + 12);
                int start = k * noteLen;
                for (int i = 0; i < noteLen * 2 && start + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 5.5f) * Mathf.Min(1f, i / 80f);
                    float s = Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * f * t);
                    data[start + i] += 0.11f * env * s;
                }
            }

            // sub pulse on every beat
            for (int b = 0; b < beats; b++)
            {
                int start = Mathf.RoundToInt(b * beat * Rate);
                float f = Hz(w.rootSemitone, -12);
                for (int i = 0; i < Rate / 3 && start + i < n; i++)
                {
                    float t = i / (float)Rate;
                    data[start + i] += 0.2f * Mathf.Exp(-t * 9f) * Mathf.Sin(2f * Mathf.PI * (f + 40f * Mathf.Exp(-t * 30f)) * t);
                }
            }

            Soften(data);
            var clip = AudioClip.Create("TripBed_" + w.name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float SnapToLoop(float f, float len)
        {
            return Mathf.Max(1f, Mathf.Round(f * len)) / len;
        }

        static void Soften(float[] d)
        {
            // gentle limiter so stacked voices never clip
            for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] * 1.3f) * 0.8f;
        }

        static AudioClip MakeWind()
        {
            int n = Rate * 4;
            var d = new float[n];
            var rng = new System.Random(77);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (white - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.2f;
                d[i] = lp2 * 1.6f;
            }
            // cross-fade the seam so the loop is click-free
            int xf = Rate / 20;
            for (int i = 0; i < xf; i++)
            {
                float k = i / (float)xf;
                d[i] = Mathf.Lerp(d[n - xf + i], d[i], k);
            }
            var clip = AudioClip.Create("TripWind", n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static AudioClip MakeSweep()
        {
            int n = Mathf.RoundToInt(Rate * 1.6f);
            var d = new float[n];
            var rng = new System.Random(5);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(180f, 1400f, t * t);
                phase += 2f * Mathf.PI * f / Rate;
                float env = Mathf.Sin(Mathf.PI * t);
                d[i] = env * (0.25f * Mathf.Sin(phase) + 0.05f * (float)(rng.NextDouble() * 2.0 - 1.0));
            }
            var clip = AudioClip.Create("TripSweep", n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static AudioClip MakeClick()
        {
            int n = Rate / 10;
            var d = new float[n];
            var rng = new System.Random(9);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Exp(-t * 90f) * ((float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 520f * t));
            }
            var clip = AudioClip.Create("TripClick", n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
