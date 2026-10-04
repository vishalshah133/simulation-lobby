using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using SimulationLobby.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace SimulationLobby.Capture
{
    /// <summary>
    /// Runs a range of seeds through the scene's <see cref="SimulationRunner"/> as fast as the CPU
    /// allows, scores each with a <see cref="SeedScanProfile"/>, and writes a ranked table. Seeds are
    /// chosen, never scripted — this is how a good run is found without faking one.
    /// </summary>
    /// <remarks>
    /// Works on any format because it only touches <see cref="SimulationRunner"/> and the physics
    /// engines. Each step is <c>StepOnce()</c> then <c>Physics2D.Simulate</c>/<c>Physics.Simulate</c>,
    /// the same order Unity's own loop runs <c>FixedUpdate</c> and the physics step in, so collision
    /// callbacks land exactly where they would on a normal Play.
    /// <para>
    /// Every seed runs in the same scene, so bodies are put back to their authored pose before each
    /// one. The verify pass re-runs the best seeds in reverse order; if anything still leaked between
    /// seeds, those rows come back DIVERGED. The final proof is still a fresh Play of the chosen seed
    /// matching the expected line this prints.
    /// </para>
    /// Runs before <see cref="SimulationRunner"/> so it can switch off auto-start in time.
    /// </remarks>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class SeedScanner : MonoBehaviour
    {
        [Tooltip("Scan instead of playing the run. Turn off to Play the scene normally.")]
        public bool scanOnPlay = true;

        public SeedScanProfile profile;

        [Tooltip("Found on this GameObject if left empty.")]
        public SimulationRunner runner;

        [Tooltip("Folder for the CSV, relative to the project root. Gitignored.")]
        public string outputFolder = "Scans";

        [Tooltip("Wall-clock milliseconds of scanning per rendered frame, so the editor stays responsive.")]
        [Min(5)]
        public int frameBudgetMs = 100;

        struct BodyPose
        {
            public Transform transform;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        sealed class Row
        {
            public int seed;
            public RunResult result;
            public string rejection;
            public float score;
            public string verified = "-";
        }

        readonly List<BodyPose> _poses = new List<BodyPose>();
        Rigidbody2D[] _bodies2D;
        Rigidbody[] _bodies3D;

        /// <summary>Seeds finished so far, for an editor progress readout.</summary>
        public int SeedsDone { get; private set; }

        public bool IsScanning { get; private set; }

        void Awake()
        {
            if (!scanOnPlay)
            {
                return;
            }

            if (runner == null)
            {
                runner = GetComponent<SimulationRunner>();
            }

            if (runner != null)
            {
                runner.autoStart = false;
            }

            SnapshotBodies();
        }

        void Start()
        {
            if (!scanOnPlay)
            {
                return;
            }

            if (runner == null || profile == null || runner.config == null)
            {
                Debug.LogError("[SeedScanner] Needs a SimulationRunner with a config, and a SeedScanProfile.", this);
                return;
            }

            // Belt and braces: if execution order didn't hold, the run started but hasn't ticked yet
            // (Start precedes the first FixedUpdate), so aborting it loses nothing.
            if (runner.IsRunning)
            {
                runner.Abort();
            }

            StartCoroutine(Scan());
        }

        IEnumerator Scan()
        {
            IsScanning = true;
            SimulationConfig config = runner.config;
            float dt = config.fixedTimestep;

            SimulationMode2D restoreMode2D = Physics2D.simulationMode;
            SimulationMode restoreMode3D = Physics.simulationMode;
            float restoreVolume = AudioListener.volume;
            bool restoreLog = runner.logResult;

            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics.simulationMode = SimulationMode.Script;
            AudioListener.volume = 0f;
            runner.logResult = false;

            var rows = new List<Row>(profile.seedCount);
            var total = Stopwatch.StartNew();
            var slice = Stopwatch.StartNew();
            int progressStep = Mathf.Max(1, profile.seedCount / 10);

            Debug.Log($"[SeedScanner] Scanning {profile.seedCount} seeds from {profile.firstSeed} " +
                      $"({config.name}, profile {profile.name})…", this);

            for (int i = 0; i < profile.seedCount; i++)
            {
                int seed = profile.firstSeed + i;
                RunResult result = RunSeed(seed, dt);
                var row = new Row { seed = seed, result = result };

                if (result == null)
                {
                    row.rejection = "no result";
                }
                else
                {
                    row.rejection = profile.Reject(result);
                    row.score = profile.Score(result);
                }

                rows.Add(row);
                SeedsDone = i + 1;

                if (SeedsDone % progressStep == 0)
                {
                    Debug.Log($"[SeedScanner] {SeedsDone}/{profile.seedCount} " +
                              $"({total.Elapsed.TotalSeconds:0.0}s)", this);
                }

                if (slice.ElapsedMilliseconds >= frameBudgetMs)
                {
                    yield return null;
                    slice.Restart();
                }
            }

            rows.Sort(CompareRows);

            // Reverse order on purpose: each verified seed now follows a different predecessor than it
            // did in the main pass, so leaked state shows up as a mismatch instead of repeating itself.
            int verifyCount = 0;
            for (int i = 0; i < rows.Count && verifyCount < profile.verifyTopCount; i++)
            {
                if (rows[i].rejection == null)
                {
                    verifyCount++;
                }
            }

            int diverged = 0;
            for (int i = verifyCount - 1; i >= 0; i--)
            {
                Row row = rows[i];
                RunResult again = RunSeed(row.seed, dt);
                DeterminismVerifier.Comparison comparison =
                    DeterminismVerifier.Compare(row.result, again);
                row.verified = comparison.identical ? "yes" : "DIVERGED";

                if (!comparison.identical)
                {
                    diverged++;
                    Debug.LogError($"[SeedScanner] Seed {row.seed} {comparison.ToSummaryLine()}", this);
                }

                if (slice.ElapsedMilliseconds >= frameBudgetMs)
                {
                    yield return null;
                    slice.Restart();
                }
            }

            total.Stop();

            Physics2D.simulationMode = restoreMode2D;
            Physics.simulationMode = restoreMode3D;
            AudioListener.volume = restoreVolume;
            runner.logResult = restoreLog;
            RestoreBodies();

            string path = WriteCsv(rows, config, total.Elapsed.TotalSeconds);
            LogTop(rows, config, total.Elapsed.TotalSeconds, path, diverged);
            IsScanning = false;
        }

        RunResult RunSeed(int seed, float dt)
        {
            RestoreBodies();

            if (!runner.Begin(seed))
            {
                return null;
            }

            int limit = runner.config.MaxTicks + 1;
            for (int step = 0; runner.IsRunning && step < limit; step++)
            {
                runner.StepOnce();
                Physics2D.Simulate(dt);
                Physics.Simulate(dt);
            }

            if (runner.IsRunning)
            {
                runner.Abort();
            }

            return runner.Result;
        }

        static int CompareRows(Row a, Row b)
        {
            bool aOk = a.rejection == null;
            bool bOk = b.rejection == null;
            if (aOk != bOk)
            {
                return aOk ? -1 : 1;
            }

            int byScore = b.score.CompareTo(a.score);
            return byScore != 0 ? byScore : a.seed.CompareTo(b.seed);
        }

        void SnapshotBodies()
        {
            _poses.Clear();
            _bodies2D = FindObjectsByType<Rigidbody2D>();
            _bodies3D = FindObjectsByType<Rigidbody>();

            foreach (Rigidbody2D body in _bodies2D)
            {
                AddPose(body.transform);
            }

            foreach (Rigidbody body in _bodies3D)
            {
                AddPose(body.transform);
            }
        }

        void AddPose(Transform target)
        {
            _poses.Add(new BodyPose
            {
                transform = target,
                position = target.position,
                rotation = target.rotation,
                scale = target.localScale
            });
        }

        /// <summary>
        /// Put every body back where the scene authored it, at rest, so each seed starts as it would
        /// on a fresh Play. Formats still set their own start state in <c>Initialize</c>; this only
        /// clears what a previous seed left behind that a format has no reason to reset.
        /// </summary>
        void RestoreBodies()
        {
            foreach (BodyPose pose in _poses)
            {
                if (pose.transform == null)
                {
                    continue;
                }

                pose.transform.SetPositionAndRotation(pose.position, pose.rotation);
                pose.transform.localScale = pose.scale;
            }

            if (_bodies2D != null)
            {
                foreach (Rigidbody2D body in _bodies2D)
                {
                    if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
                    {
                        body.linearVelocity = Vector2.zero;
                        body.angularVelocity = 0f;
                    }
                }
            }

            if (_bodies3D != null)
            {
                foreach (Rigidbody body in _bodies3D)
                {
                    if (body != null && !body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                }
            }

            Physics2D.SyncTransforms();
            Physics.SyncTransforms();
        }

        string WriteCsv(List<Row> rows, SimulationConfig config, double seconds)
        {
            var metricKeys = new List<string>();
            var seen = new HashSet<string>();
            foreach (Row row in rows)
            {
                if (row.result == null)
                {
                    continue;
                }

                foreach (RunMetric metric in row.result.metrics)
                {
                    if (seen.Add(metric.key))
                    {
                        metricKeys.Add(metric.key);
                    }
                }
            }

            var csv = new StringBuilder();
            csv.Append("rank,seed,score,verdict,verified,completion,seconds,summary");
            foreach (string key in metricKeys)
            {
                csv.Append(',').Append(key);
            }

            csv.AppendLine();

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                RunResult result = row.result;
                csv.Append(i + 1).Append(',')
                   .Append(row.seed).Append(',')
                   .Append(Number(row.score)).Append(',')
                   .Append(Quote(row.rejection == null ? "ok" : "rejected: " + row.rejection)).Append(',')
                   .Append(row.verified).Append(',')
                   .Append(result != null ? result.completionReason.ToString() : "-").Append(',')
                   .Append(result != null ? Number(result.simulatedSeconds) : "-").Append(',')
                   .Append(Quote(result != null && result.standings.Count > 0 ? result.standings[0] : "-"));

                foreach (string key in metricKeys)
                {
                    csv.Append(',').Append(result != null ? Number(result.GetMetric(key)) : "");
                }

                csv.AppendLine();
            }

            string root = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(root, outputFolder);
            Directory.CreateDirectory(folder);

            int last = profile.firstSeed + profile.seedCount - 1;
            string name = $"{SceneManager.GetActiveScene().name}_{config.name}_{profile.firstSeed}-{last}";
            string path = Path.Combine(folder, name + ".csv");
            File.WriteAllText(path, csv.ToString());

            // Sidecar rather than a comment line, so the CSV opens cleanly in a spreadsheet.
            File.WriteAllText(Path.Combine(folder, name + ".txt"),
                $"scene: {SceneManager.GetActiveScene().path}\nconfig: {config.name}\nprofile: {profile.name}\n" +
                $"seeds: {profile.firstSeed}-{last}\nfixedTimestep: {config.fixedTimestep}\n" +
                $"unity: {Application.unityVersion}\nscan seconds: {seconds:0.0}\n");

            return path;
        }

        void LogTop(List<Row> rows, SimulationConfig config, double seconds, string path, int diverged)
        {
            int passed = 0;
            foreach (Row row in rows)
            {
                if (row.rejection == null)
                {
                    passed++;
                }
            }

            var log = new StringBuilder();
            log.AppendLine($"[SeedScanner] {rows.Count} seeds in {seconds:0.0}s " +
                           $"({rows.Count / Mathf.Max(0.001f, (float)seconds):0} seeds/s) · {passed} pass · " +
                           (diverged == 0 ? "verify OK" : $"{diverged} DIVERGED — table not trustworthy"));
            log.AppendLine(path);

            int shown = Mathf.Min(profile.printTopCount, rows.Count);
            for (int i = 0; i < shown; i++)
            {
                Row row = rows[i];
                string summary = row.result != null && row.result.standings.Count > 0 ? row.result.standings[0] : "-";
                string verdict = row.rejection == null ? "" : $" REJECTED {row.rejection}";
                log.Append($"  #{i + 1} seed {row.seed} · score {row.score:0.##} · " +
                           $"{(row.result != null ? row.result.simulatedSeconds : 0f):0.0}s · {summary}");

                foreach (SeedScanProfile.Weight weight in profile.weights)
                {
                    if (row.result != null)
                    {
                        log.Append($" · {weight.metric}={SeedScanProfile.Resolve(row.result, weight.metric):0.##}");
                    }
                }

                log.AppendLine($" · verified {row.verified}{verdict}");
            }

            if (rows.Count > 0 && rows[0].result != null)
            {
                log.AppendLine("To use one: exit Play, untick Scan On Play, set SimulationRunner.seed, Play. " +
                               "It must log exactly the line from the scan, e.g. for #1:");
                log.AppendLine("  " + rows[0].result.ToSummaryLine());
            }

            Debug.Log(log.ToString(), this);
        }

        static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
