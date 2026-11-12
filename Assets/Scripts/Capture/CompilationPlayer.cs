using System.Collections;
using System.Collections.Generic;
using System.Text;
using SimulationLobby.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace SimulationLobby.Capture
{
    /// <summary>
    /// Plays a <see cref="CompilationPlaylist"/> in one Play session so it can be recorded in one take:
    /// intro card, then each segment scene behind a transition, then the outro. Lives in the master scene
    /// and survives scene loads.
    /// </summary>
    /// <remarks>
    /// Knows scenes by NAME only and finishes a segment by asking Core's <see cref="SimulationRunner"/>
    /// whether its run has a <see cref="SimulationRunner.Result"/>; it never references a format.
    /// Presentation-only: nothing here writes simulation state, and <c>Time.fixedDeltaTime</c> is left
    /// exactly as each scene's runner sets it. Time is the accumulated unscaled delta, so it is independent
    /// of any segment's timeScale (slow motion).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CompilationPlayer : MonoBehaviour
    {
        const float MinCoveredHold = 0.5f;

        [SerializeField] CompilationPlaylist _playlist;

        [Tooltip("Begin on Start. Turn off to start from a script.")]
        [SerializeField] bool _playOnStart = true;

        static CompilationPlayer _instance;

        CompilationOverlay _overlay;
        float _clock;
        float _t0;
        readonly List<string> _chapters = new List<string>();
        readonly List<string> _seedLines = new List<string>();

        /// <summary>Assigned by the master-scene builder.</summary>
        public void SetPlaylist(CompilationPlaylist playlist) { _playlist = playlist; }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // The master scene reloaded; the persistent player is already running.
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                AudioListener.volume = 1f;
            }
        }

        void Start()
        {
            if (_instance != this || !_playOnStart)
            {
                return;
            }

            if (_playlist == null || _playlist.entries == null || _playlist.entries.Length == 0)
            {
                Debug.LogError("[comp] No playlist (or it is empty). Run Simulation Lobby > Tools > Compilation > Rebuild Master Scene Only.", this);
                return;
            }

            _overlay = CompilationOverlay.Create(transform);
            StartCoroutine(Run());
        }

        void Update()
        {
            _clock += Time.unscaledDeltaTime;
        }

        // ------------------------------------------------------------------ sequence

        IEnumerator Run()
        {
            CompilationPlaylist p = _playlist;
            int total = p.entries.Length;
            AudioListener.volume = 1f;
            _overlay.SnapCovered();

            Debug.Log($"[comp] Starting in {p.startDelaySeconds:0.0}s. {total} segments. Recorder should be armed now.", this);
            yield return Wait(p.startDelaySeconds);

            // Intro.
            _t0 = _clock;
            _chapters.Add("0:00 Intro");
            _overlay.ShowCard(p.introTitle, p.introHook, 170f, true, p.introSeconds);
            yield return Reveal(CompilationTransition.FadeBlack, 0.6f, false);
            yield return Wait(Mathf.Max(0f, p.introSeconds - 0.6f));

            // Segments.
            for (int i = 0; i < total; i++)
            {
                CompilationEntry e = p.entries[i];
                string tag = $"{i + 1} / {total}";
                if (string.IsNullOrEmpty(e.sceneName) || !Application.CanStreamedLevelBeLoaded(e.sceneName))
                {
                    Debug.LogWarning($"[comp] Segment {tag}: scene '{e.sceneName}' is not in Build Settings; SKIPPED. " +
                                     "Run Simulation Lobby > Tools > Compilation > Rebuild Master Scene Only.", this);
                    continue;
                }

                yield return Cover(e.transitionIn, e.transitionSeconds, tag, e.label);
                float coveredAt = _clock;
                _overlay.HideCard();

                if (Time.timeScale != 1f)
                {
                    Time.timeScale = 1f; // a previous segment's slow motion must not leak into the next load
                }

                AsyncOperation op = SceneManager.LoadSceneAsync(e.sceneName, LoadSceneMode.Single);
                if (op == null)
                {
                    Debug.LogWarning($"[comp] Segment {tag}: LoadSceneAsync('{e.sceneName}') failed; SKIPPED.", this);
                    continue;
                }

                while (!op.isDone)
                {
                    yield return null;
                }

                yield return null; // let the scene's Awake/Start run (several builders delay their runner start)
                yield return null;
                Sanitize(e.sceneName);

                while (_clock - coveredAt < MinCoveredHold)
                {
                    yield return null;
                }

                _chapters.Add($"{FormatTime(_clock - _t0)} {e.label}");
                float segStart = _clock;
                Debug.Log($"[comp] Segment {tag} '{e.label}' ({e.sceneName}) starts at {FormatTime(_clock - _t0)}.", this);
                yield return Reveal(e.transitionIn, e.transitionSeconds, true);
                yield return RunSegment(e, tag, segStart);
            }

            // Outro.
            yield return Cover(CompilationTransition.FadeBlack, 0.6f, string.Empty, string.Empty);
            Scene empty = SceneManager.CreateScene("CompilationOutro");
            Scene last = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(empty);
            if (last.IsValid() && last != empty && last.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(last);
            }

            Time.timeScale = 1f;
            _chapters.Add($"{FormatTime(_clock - _t0)} Outro");
            _overlay.ShowCard(p.outroText, p.outroSub, 120f, false, p.outroSeconds);
            yield return Reveal(CompilationTransition.FadeBlack, 0.6f, false);
            yield return Wait(Mathf.Max(0f, p.outroSeconds - 0.6f));
            yield return Cover(CompilationTransition.FadeBlack, 1f, string.Empty, string.Empty);
            float totalSeconds = _clock - _t0;
            yield return Wait(p.endHoldSeconds);

            LogSummary(totalSeconds);
            Finish();
        }

        IEnumerator RunSegment(CompilationEntry e, string tag, float segStart)
        {
            SimulationRunner[] runners = null;
            float nextScan = 0f;
            float completeAt = -1f;
            bool noRunnerWarned = false;

            while (true)
            {
                float elapsed = _clock - segStart;
                if (elapsed >= e.maxSeconds)
                {
                    if (completeAt < 0f)
                    {
                        Debug.LogWarning($"[comp] Segment {tag} '{e.label}' hit maxSeconds ({e.maxSeconds:0}s) WITHOUT completing. " +
                                         "Cut anyway. Raise maxSeconds or check the scene. " + DescribeRunners(runners), this);
                    }

                    break;
                }

                if (completeAt < 0f && elapsed >= nextScan)
                {
                    nextScan = elapsed + 0.25f;
                    runners = Object.FindObjectsByType<SimulationRunner>();
                    if (runners.Length > 0 && AllHaveResults(runners))
                    {
                        completeAt = elapsed;
                    }
                    else if (runners.Length == 0 && elapsed > 3f && !noRunnerWarned)
                    {
                        noRunnerWarned = true;
                        Debug.LogWarning($"[comp] Segment {tag} '{e.label}': no SimulationRunner found; falling back to maxSeconds ({e.maxSeconds:0}s).", this);
                    }
                }

                if (completeAt >= 0f && elapsed - completeAt >= e.tailSeconds)
                {
                    break;
                }

                yield return null;
            }

            LogSeeds(e, tag, runners);
        }

        static bool AllHaveResults(SimulationRunner[] runners)
        {
            for (int i = 0; i < runners.Length; i++)
            {
                if (runners[i] == null || runners[i].IsRunning || runners[i].Result == null)
                {
                    return false;
                }
            }

            return true;
        }

        static string DescribeRunners(SimulationRunner[] runners)
        {
            if (runners == null || runners.Length == 0)
            {
                return "(no SimulationRunner seen)";
            }

            var sb = new StringBuilder("Runners: ");
            for (int i = 0; i < runners.Length; i++)
            {
                sb.Append($"[{runners[i].name} running={runners[i].IsRunning} result={(runners[i].Result != null)} sim={runners[i].SimulatedSeconds:0.0}s] ");
            }

            return sb.ToString();
        }

        void LogSeeds(CompilationEntry e, string tag, SimulationRunner[] runners)
        {
            runners = Object.FindObjectsByType<SimulationRunner>();
            string line;
            if (runners.Length == 0)
            {
                line = $"{tag} {e.label} ({e.sceneName}): seed unavailable (no SimulationRunner)";
            }
            else
            {
                var sb = new StringBuilder($"{tag} {e.label} ({e.sceneName}): seed ");
                for (int i = 0; i < runners.Length; i++)
                {
                    sb.Append(runners[i].seed).Append(i < runners.Length - 1 ? ", " : string.Empty);
                }

                sb.Append($" (sim {runners[0].SimulatedSeconds:0.0}s)");
                line = sb.ToString();
            }

            _seedLines.Add(line);
            Debug.Log("[comp] " + line, this);
        }

        // ------------------------------------------------------------------ transitions

        static float CloseScale(CompilationTransition t) { return t == CompilationTransition.FlashWhite ? 0.35f : 1f; }

        IEnumerator Cover(CompilationTransition type, float seconds, string number, string label)
        {
            _overlay.SetBanner(number, label.ToUpperInvariant(), true, 0f);
            float d = Mathf.Max(0.05f, seconds * CloseScale(type));
            for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
            {
                float e = Mathf.SmoothStep(0f, 1f, t / d);
                _overlay.SetState(type, CompilationOverlay.Phase.Close, e);
                _overlay.SetBannerForPhase(CompilationOverlay.Phase.Close, e, type);
                AudioListener.volume = 1f - e;
                yield return null;
            }

            _overlay.SetState(type, CompilationOverlay.Phase.Close, 1f);
            _overlay.SetBannerForPhase(CompilationOverlay.Phase.Close, 1f, type);
            AudioListener.volume = 0f;
        }

        IEnumerator Reveal(CompilationTransition type, float seconds, bool banner)
        {
            float d = Mathf.Max(0.05f, seconds);
            for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
            {
                float e = Mathf.SmoothStep(0f, 1f, t / d);
                _overlay.SetState(type, CompilationOverlay.Phase.Open, e);
                _overlay.SetBannerForPhase(CompilationOverlay.Phase.Open, e, type);
                AudioListener.volume = e;
                yield return null;
            }

            _overlay.SetState(type, CompilationOverlay.Phase.Open, 1f);
            _overlay.SetBanner(string.Empty, string.Empty, false, 0f);
            AudioListener.volume = 1f;
        }

        IEnumerator Wait(float seconds)
        {
            float end = _clock + seconds;
            while (_clock < end)
            {
                yield return null;
            }
        }

        // ------------------------------------------------------------------ scene hygiene

        /// <summary>Keep one AudioListener and one EventSystem alive; a segment scene brings its own camera.</summary>
        void Sanitize(string sceneName)
        {
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>();
            bool kept = false;
            for (int i = 0; i < listeners.Length; i++)
            {
                if (!listeners[i].enabled)
                {
                    continue;
                }

                if (!kept)
                {
                    kept = true;
                    continue;
                }

                Debug.LogWarning($"[comp] '{sceneName}': extra AudioListener on '{listeners[i].name}' disabled.", listeners[i]);
                listeners[i].enabled = false;
            }

            EventSystem[] systems = Object.FindObjectsByType<EventSystem>();
            for (int i = 1; i < systems.Length; i++)
            {
                Debug.LogWarning($"[comp] '{sceneName}': extra EventSystem on '{systems[i].name}' disabled.", systems[i]);
                systems[i].enabled = false;
            }

            if (Camera.allCamerasCount == 0)
            {
                Debug.LogWarning($"[comp] '{sceneName}' has no enabled Camera; the segment will render nothing.", this);
            }
        }

        // ------------------------------------------------------------------ end

        void LogSummary(float totalSeconds)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[comp] DONE. Total length {FormatTime(totalSeconds)} (excludes the {_playlist.startDelaySeconds:0.0}s lead-in and the end hold; trim the clip accordingly).");
            sb.AppendLine("YouTube chapters (paste into the description; t=0 is the intro card):");
            for (int i = 0; i < _chapters.Count; i++)
            {
                sb.AppendLine(_chapters[i]);
            }

            sb.AppendLine("Seeds (record these in the video's plan):");
            for (int i = 0; i < _seedLines.Count; i++)
            {
                sb.AppendLine(_seedLines[i]);
            }

            Debug.Log(sb.ToString(), this);
        }

        void Finish()
        {
#if UNITY_EDITOR
            if (_playlist.exitPlayModeWhenDone)
            {
                UnityEditor.EditorApplication.isPlaying = false;
            }
#else
            Application.Quit();
#endif
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
