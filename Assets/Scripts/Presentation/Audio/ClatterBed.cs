using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// The many-balls clatter of plastic balls on wood, metal and glass, synthesised live in <c>OnAudioFilterRead</c>:
    /// a Poisson stream of 2-6 ms damped pings (some with a low "tok" body and a hair of noise). Nothing sampled.
    /// Format-agnostic: whoever owns the scene sets <see cref="Density"/>, <see cref="Brightness"/> and <see cref="Gain"/>
    /// from the main thread. No allocation on the audio thread.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ClatterBed : MonoBehaviour
    {
        const int VoiceCount = 64;

        [Tooltip("Seed for the click stream. Cosmetic: a re-render sounds the same.")]
        public int seed = 4242;

        [Tooltip("Master gain on top of the Gain property.")]
        [Range(0f, 2f)] public float masterGain = 1f;

        volatile float _density;
        volatile float _brightness;
        volatile float _gain;

        // Audio-thread state.
        float _sDensity;
        float _sBrightness;
        float _sGain;
        uint _rng;
        int _sampleRate = 48000;

        readonly float[] _phase = new float[VoiceCount];
        readonly float[] _step = new float[VoiceCount];
        readonly float[] _env = new float[VoiceCount];
        readonly float[] _decay = new float[VoiceCount];
        readonly float[] _amp = new float[VoiceCount];
        readonly float[] _bodyPhase = new float[VoiceCount];
        readonly float[] _bodyStep = new float[VoiceCount];
        readonly float[] _bodyEnv = new float[VoiceCount];
        readonly float[] _bodyDecay = new float[VoiceCount];
        readonly float[] _noiseEnv = new float[VoiceCount];
        readonly float[] _noiseDecay = new float[VoiceCount];
        int _next;

        AudioSource _source;
        bool _ready;

        /// <summary>Clicks per second, 0-3000.</summary>
        public float Density
        {
            get => _density;
            set => _density = Mathf.Clamp(value, 0f, 3000f);
        }

        /// <summary>0 = dull wood, 1 = bright glass and metal.</summary>
        public float Brightness
        {
            get => _brightness;
            set => _brightness = Mathf.Clamp01(value);
        }

        public float Gain
        {
            get => _gain;
            set => _gain = Mathf.Max(0f, value);
        }

        void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            _rng = (uint)(new System.Random(seed).Next(1, int.MaxValue)) | 1u;
            _gain = 1f;

            _source = GetComponent<AudioSource>();
            // OnAudioFilterRead only runs while the source plays, so give it a silent looping clip.
            var silence = AudioClip.Create("ClatterBedSilence", 1024, 1, _sampleRate, false);
            silence.SetData(new float[1024], 0);
            _source.clip = silence;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
            _source.Play();
            _ready = true;
        }

        void OnEnable()
        {
            if (_ready && !_source.isPlaying)
            {
                _source.Play();
            }
        }

        float Rand01()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / 16777216f;
        }

        void Trigger(float brightness)
        {
            int v = _next;
            _next = (_next + 1) % VoiceCount;

            float centre = Mathf.Lerp(2000f, 4600f, brightness);
            float hz = Mathf.Clamp(centre * (0.65f + 0.85f * Rand01()), 1800f, 6200f);
            float lifeMs = 2f + 4f * Rand01();
            _phase[v] = 0f;
            _step[v] = 2f * Mathf.PI * hz / _sampleRate;
            _env[v] = 1f;
            _decay[v] = Mathf.Exp(-6.9f / (lifeMs * 0.001f * _sampleRate));
            _amp[v] = 0.16f * (0.35f + 0.65f * Rand01());

            if (Rand01() < 0.45f - 0.25f * brightness)
            {
                float body = 280f + 520f * Rand01();
                _bodyPhase[v] = 0f;
                _bodyStep[v] = 2f * Mathf.PI * body / _sampleRate;
                _bodyEnv[v] = 0.55f;
                _bodyDecay[v] = Mathf.Exp(-6.9f / (0.007f * _sampleRate));
            }
            else
            {
                _bodyEnv[v] = 0f;
            }

            _noiseEnv[v] = 0.35f * Rand01();
            _noiseDecay[v] = Mathf.Exp(-6.9f / (0.0015f * _sampleRate));
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_ready || channels <= 0)
            {
                return;
            }

            float tDensity = _density;
            float tBrightness = _brightness;
            float tGain = _gain * masterGain;
            const float smooth = 0.0004f;
            float invRate = 1f / _sampleRate;

            int frames = data.Length / channels;
            for (int f = 0; f < frames; f++)
            {
                _sDensity += (tDensity - _sDensity) * smooth;
                _sBrightness += (tBrightness - _sBrightness) * smooth;
                _sGain += (tGain - _sGain) * smooth;

                if (Rand01() < _sDensity * invRate)
                {
                    Trigger(_sBrightness);
                }

                float sum = 0f;
                for (int v = 0; v < VoiceCount; v++)
                {
                    float e = _env[v];
                    if (e > 0.002f)
                    {
                        float s = Mathf.Sin(_phase[v]) + 0.35f * Mathf.Sin(_phase[v] * 2.31f);
                        sum += s * e * _amp[v];
                        _phase[v] += _step[v];
                        _env[v] = e * _decay[v];

                        float be = _bodyEnv[v];
                        if (be > 0.002f)
                        {
                            sum += Mathf.Sin(_bodyPhase[v]) * be * _amp[v];
                            _bodyPhase[v] += _bodyStep[v];
                            _bodyEnv[v] = be * _bodyDecay[v];
                        }

                        float ne = _noiseEnv[v];
                        if (ne > 0.002f)
                        {
                            sum += (Rand01() * 2f - 1f) * ne * _amp[v];
                            _noiseEnv[v] = ne * _noiseDecay[v];
                        }
                    }
                }

                // Soft limiter (rational tanh), so a wall of clicks saturates instead of clipping.
                float x = Mathf.Clamp(sum * _sGain, -3f, 3f);
                float y = x * (27f + x * x) / (27f + 9f * x * x);

                int i = f * channels;
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = y;
                }
            }
        }
    }
}
