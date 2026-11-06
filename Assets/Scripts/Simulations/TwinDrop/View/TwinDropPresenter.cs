using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.TwinDrop
{
    /// <summary>
    /// Draws and sounds a <see cref="TwinDropSimulation"/>: the blue and red twin, the slot highlights, the
    /// SAME / DIFFERENT tally and the verdict line. Reads in <c>LateUpdate</c> and never writes to the run.
    /// </summary>
    [RequireComponent(typeof(TwinDropSimulation))]
    public sealed class TwinDropPresenter : MonoBehaviour
    {
        static readonly Color Blue = new Color(0.25f, 0.65f, 1f, 1f);
        static readonly Color Red = new Color(1f, 0.3f, 0.32f, 1f);
        static readonly Color Green = new Color(0.35f, 1f, 0.55f, 1f);

        [Header("Wiring (set by the scene builder via Bind)")]
        [SerializeField] TwinDropSimulation _simulation;
        [SerializeField] VersusScoreboardHud _hud;
        [SerializeField] SpriteRenderer[] _balls;
        [SerializeField] TrailRenderer[] _trails;
        [SerializeField] SpriteRenderer[] _binGlow;
        [SerializeField] SpriteRenderer _releaseGlow;

        [Header("Audio")]
        [SerializeField] float _rootHz = 261.63f;
        [Range(0f, 1f)] [SerializeField] float _noteVolume = 0.45f;
        [Range(0f, 1f)] [SerializeField] float _sfxVolume = 0.8f;

        TwinPhase _lastPhase = TwinPhase.Done;
        int _lastPair = -1;
        int _lastLandSerial;
        long _lastHits;
        AudioClip[] _scale;
        AudioClip _thud;
        AudioClip _glass;
        AudioClip _lowThud;
        AudioClip _riser;
        AudioSource _voice;
        AudioSource _sfx;

        public void Bind(TwinDropSimulation simulation, VersusScoreboardHud hud, SpriteRenderer[] balls,
                         TrailRenderer[] trails, SpriteRenderer[] binGlow, SpriteRenderer releaseGlow)
        {
            _simulation = simulation;
            _hud = hud;
            _balls = balls;
            _trails = trails;
            _binGlow = binGlow;
            _releaseGlow = releaseGlow;
        }

        void Start()
        {
            _scale = ProceduralToneBank.CreateScale(8, _rootHz, 0.7f);
            _thud = ProceduralToneBank.CreateImpactThud(1f, 0.2f, 21, "Land");
            _lowThud = ProceduralToneBank.CreateImpactThud(0.5f, 0.35f, 22, "LandApart");
            _glass = ProceduralToneBank.CreateGlassClink(1f);
            _riser = ProceduralToneBank.CreateRiser(1.2f, 90f, 260f);
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
        }

        void LateUpdate()
        {
            if (_simulation == null || _simulation.Config == null)
            {
                return;
            }

            TwinDropConfig cfg = _simulation.Config;
            TwinPhase phase = _simulation.Phase;
            int pair = _simulation.RoundIndex * 100 + _simulation.PairInRound;
            if (phase == TwinPhase.Hold && (phase != _lastPhase || pair != _lastPair))
            {
                // New pair: the marbles jump back to the top, so drop the old trails.
                for (int i = 0; i < _trails.Length; i++)
                {
                    _trails[i].Clear();
                }

                _lastHits = 0;
                _sfx.PlayOneShot(_riser, _sfxVolume * 0.5f);
            }

            _lastPhase = phase;
            _lastPair = pair;

            DrawBalls(cfg, phase);
            DrawBins(cfg, phase);
            DrawReleaseGlow(phase);
            DrawHud(cfg, phase);
            PlayAudio(phase);
        }

        void DrawBalls(TwinDropConfig cfg, TwinPhase phase)
        {
            int count = _simulation.BallCount;
            for (int i = 0; i < _balls.Length; i++)
            {
                bool on = i < count;
                SpriteRenderer sr = _balls[i];
                sr.enabled = on;
                if (!on)
                {
                    _trails[i].emitting = false;
                    continue;
                }

                // Blue sits on top; red is a little bigger behind it, so a red rim shows while they are
                // stacked ("there are two") and the red one peels away once they part.
                bool blue = i == 0;
                float size = cfg.ballRadius * 2f * (blue ? 1f : 1.4f);
                Vector2 p = _simulation.GetBallPosition(i);
                sr.transform.position = new Vector3(p.x, p.y, 0f);
                sr.transform.localScale = Vector3.one * size;
                Color c = blue ? Blue : Red;
                sr.color = c;
                _trails[i].emitting = phase == TwinPhase.Falling && !_simulation.BallLanded(i);
                _trails[i].widthMultiplier = size * (blue ? 0.6f : 0.85f);
                _trails[i].startColor = new Color(c.r, c.g, c.b, 0.8f);
                _trails[i].endColor = new Color(c.r, c.g, c.b, 0f);
            }
        }

        void DrawBins(TwinDropConfig cfg, TwinPhase phase)
        {
            for (int b = 0; b < _binGlow.Length; b++)
            {
                _binGlow[b].enabled = false;
            }

            if (_simulation.BallCount < 2 || phase == TwinPhase.Hold)
            {
                return;
            }

            bool done = _simulation.PairDone;
            bool same = _simulation.PairWasSame;
            float pulse = 0.65f + 0.2f * Mathf.Sin(Time.time * 8f);
            for (int i = 0; i < 2; i++)
            {
                if (!_simulation.BallLanded(i))
                {
                    continue;
                }

                int slot = _simulation.BallSlot(i);
                if (slot < 0 || slot >= _binGlow.Length)
                {
                    continue;
                }

                Color c = done && same ? Green : (i == 0 ? Blue : Red);
                _binGlow[slot].enabled = true;
                _binGlow[slot].color = new Color(c.r, c.g, c.b, done ? pulse : 0.5f);
            }
        }

        void DrawReleaseGlow(TwinPhase phase)
        {
            if (_releaseGlow == null)
            {
                return;
            }

            _releaseGlow.enabled = phase == TwinPhase.Hold;
            if (_releaseGlow.enabled)
            {
                Vector2 r = _simulation.ReleasePoint;
                float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 7f);
                _releaseGlow.transform.position = new Vector3(r.x, r.y, 0f);
                _releaseGlow.transform.localScale = Vector3.one * (1.6f * pulse);
                _releaseGlow.color = new Color(1f, 1f, 1f, 0.5f);
            }
        }

        void DrawHud(TwinDropConfig cfg, TwinPhase phase)
        {
            if (_hud == null)
            {
                return;
            }

            if (phase == TwinPhase.EndCard)
            {
                _hud.SetTitle(cfg.endTitle
                    .Replace("{apart}", _simulation.NudgedApart.ToString())
                    .Replace("{n}", _simulation.NudgedPairs.ToString()));
                _hud.SetRoundLabel(string.Empty);
                _hud.ShowNeutral(cfg.endWhy);
                _hud.SetScore(_simulation.SameCount, _simulation.ApartCount);
                return;
            }

            _hud.SetScoreVisible(true);
            _hud.SetTitle(cfg.title);
            _hud.SetQuestion(cfg.question);
            _hud.SetSides(cfg.sameName, cfg.apartName);
            _hud.SetScore(_simulation.SameCount, _simulation.ApartCount);

            TwinDropConfig.TwinRound round = _simulation.CurrentRound;
            _hud.SetRoundLabel(round.gapLabel);

            if (_simulation.PairDone)
            {
                _hud.ShowResult(_simulation.PairWasSame ? cfg.sameResult : cfg.apartResult, _simulation.PairWasSame);
            }
            else
            {
                _hud.ShowNeutral(string.Empty);
            }
        }

        void PlayAudio(TwinPhase phase)
        {
            if (phase != TwinPhase.Falling && phase != TwinPhase.Result)
            {
                return;
            }

            long hits = _simulation.LeaderHits;
            if (phase == TwinPhase.Falling && hits > _lastHits)
            {
                _lastHits = hits;
                _voice.PlayOneShot(_scale[(int)(hits % _scale.Length)], _noteVolume);
            }

            if (_simulation.LandSerial != _lastLandSerial)
            {
                _lastLandSerial = _simulation.LandSerial;
                _sfx.PlayOneShot(_thud, _sfxVolume);
                if (_simulation.PairDone)
                {
                    _sfx.PlayOneShot(_simulation.PairWasSame ? _glass : _lowThud, _sfxVolume);
                }
            }
        }
    }
}
