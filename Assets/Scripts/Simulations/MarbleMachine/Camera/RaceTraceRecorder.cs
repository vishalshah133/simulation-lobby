using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// Records a live lap race into a <see cref="RaceTrace"/> (play mode only): one sample per FixedUpdate, saved under
    /// Temp/RaceCamTraces when the race has been finished for a while, or on demand. Read only: it never writes the simulation.
    /// Optional tooling for replaying a real run through <c>RaceCameraHarness</c>; nothing in the race depends on it.
    /// </summary>
    public static class RaceTraceRecorder
    {
        sealed class Driver : MonoBehaviour
        {
            [System.NonSerialized] public IRaceState state;
            [System.NonSerialized] public RaceTrace header;
            public float finishedSeconds;
            public string path;
            public bool saved;

            readonly List<float> _time = new List<float>();
            readonly List<byte> _phase = new List<byte>();
            readonly List<short> _leader = new List<short>();
            readonly List<short> _winner = new List<short>();
            readonly List<Vector3> _pos = new List<Vector3>();
            readonly List<float> _progress = new List<float>();
            readonly List<byte> _rank = new List<byte>();
            readonly List<byte> _recycles = new List<byte>();
            readonly List<bool> _finished = new List<bool>();
            float _finishedAt = -1f;

            void FixedUpdate()
            {
                if (state == null || saved)
                {
                    return;
                }

                _time.Add(state.SimTime);
                _phase.Add((byte)state.Phase);
                _leader.Add((short)state.LeaderIndex);
                _winner.Add((short)state.WinnerIndex);
                for (int i = 0; i < state.RacerCount; i++)
                {
                    _pos.Add(state.RacerPosition(i));
                    _progress.Add(state.Progress(i));
                    _rank.Add((byte)Mathf.Clamp(state.RankOf(i), 0, 255));
                    _recycles.Add((byte)Mathf.Clamp(state.Recycles(i), 0, 255));
                    _finished.Add(state.FinishTime(i) >= 0f);
                }

                if (state.Phase == RacePhase.Finished)
                {
                    if (_finishedAt < 0f)
                    {
                        _finishedAt = state.SimTime;
                    }
                    else if (state.SimTime - _finishedAt >= finishedSeconds)
                    {
                        Save();
                    }
                }
            }

            public void Save()
            {
                if (saved || _time.Count == 0)
                {
                    return;
                }

                saved = true;
                header.AllocateTicks(_time.Count);
                _time.CopyTo(header.simTime);
                _phase.CopyTo(header.phase);
                _leader.CopyTo(header.leader);
                _winner.CopyTo(header.winner);
                _pos.CopyTo(header.pos);
                _progress.CopyTo(header.progress);
                _rank.CopyTo(header.rank);
                _recycles.CopyTo(header.recycles);
                _finished.CopyTo(header.finished);
                header.Save(path);
                Debug.Log("[RaceCam] trace saved: " + path + " (" + _time.Count + " ticks)");
            }

            void OnDestroy()
            {
                Save();
            }
        }

        static Driver _driver;

        /// <summary>Starts recording the given running simulation. Saves automatically a few seconds after the race finishes.</summary>
        public static void Begin(MachineRaceSimulation sim, string fileName = "live.trace", float finishedSeconds = 15f)
        {
            if (!Application.isPlaying || sim == null)
            {
                return;
            }

            Stop();
            var go = new GameObject("RaceTraceRecorder") { hideFlags = HideFlags.HideAndDontSave };
            _driver = go.AddComponent<Driver>();
            _driver.state = sim;
            _driver.header = RaceTrace.HeaderFromScene(sim);
            _driver.finishedSeconds = finishedSeconds;
            _driver.path = Path.Combine(RaceTrace.TraceDirectory, fileName);
        }

        /// <summary>Saves what has been recorded so far and stops.</summary>
        public static void Stop()
        {
            if (_driver != null)
            {
                _driver.Save();
                Object.Destroy(_driver.gameObject);
                _driver = null;
            }
        }
    }
}
