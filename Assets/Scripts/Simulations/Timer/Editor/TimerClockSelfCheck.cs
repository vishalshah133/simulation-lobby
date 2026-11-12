using SimulationLobby.Shared.Timing;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Simulations.Timer.EditorTools
{
    /// <summary>Steps the clock profile at 50 and 60 Hz and checks it lands on 1 at the duration and never runs backwards.</summary>
    public static class TimerClockSelfCheck
    {
        const float Tolerance = 1e-6f;

        [MenuItem("Simulation Lobby/Tools/Timer Clock Self-Check", false, 960)]
        static void Run()
        {
            float[] durations = { 300f, 600f, 900f };
            float[] rates = { 50f, 60f };
            float[] eases = { 0f, 0.03f };
            int failures = 0;
            int cases = 0;

            foreach (float duration in durations)
            {
                foreach (float rate in rates)
                {
                    foreach (float ease in eases)
                    {
                        cases++;
                        var profile = new CountdownProfile(duration, ease);
                        float dt = 1f / rate;
                        int steps = Mathf.RoundToInt(duration * rate);

                        float endError = Mathf.Abs(profile.Progress(duration) - 1f);
                        float previous = 0f;
                        bool monotone = Mathf.Abs(profile.Progress(0f)) <= Tolerance;
                        float worstSpeedJump = 0f;
                        float lastSpeed = 0f;
                        for (int i = 1; i <= steps; i++)
                        {
                            float t = Mathf.Min(i * dt, duration);
                            float p = profile.Progress(t);
                            if (p < previous - Tolerance)
                            {
                                monotone = false;
                            }

                            previous = p;
                            float speed = profile.Speed(t);
                            worstSpeedJump = Mathf.Max(worstSpeedJump, Mathf.Abs(speed - lastSpeed));
                            lastSpeed = speed;
                        }

                        float steppedError = Mathf.Abs(previous - 1f);
                        bool pass = endError <= Tolerance && steppedError <= Tolerance && monotone;
                        if (!pass)
                        {
                            failures++;
                        }

                        string line = $"[timer self-check] {(pass ? "PASS" : "FAIL")} duration={duration:0}s rate={rate:0}Hz ease={ease:0.00} " +
                                      $"endError={endError:E2} steppedEndError={steppedError:E2} monotone={monotone} maxStepSpeedJump={worstSpeedJump:E2}";
                        if (pass)
                        {
                            Debug.Log(line);
                        }
                        else
                        {
                            Debug.LogError(line);
                        }
                    }
                }
            }

            Debug.Log($"[timer self-check] {(failures == 0 ? "ALL PASS" : failures + " FAILED")} ({cases} cases)");
        }
    }
}
