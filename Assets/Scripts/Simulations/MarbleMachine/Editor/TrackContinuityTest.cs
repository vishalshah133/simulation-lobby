using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Proves the track has no gaps, by rolling a racer-sized test ball down every connector chute with the real colliders, and by
    /// checking that each chute meets the station before and after it. Run automatically at the end of a race scene build, and from
    /// Simulation Lobby ▸ Tools. Edit mode only: it switches physics to script stepping for the length of the test and restores it.
    /// </summary>
    public static class TrackContinuityTest
    {
        const float BallRadius = 0.2f;
        const float JointToleranceMetres = 0.12f;
        const float MaxTurnPerRowDegrees = 12f;
        const float JointHeightToleranceMetres = 0.35f;

        [MenuItem("Simulation Lobby/Tools/Track Continuity Test (mach-002)", false, 951)]
        static void Menu()
        {
            Debug.Log(Run());
        }

        /// <summary>Runs the test on the open scene and returns a report. Lines starting with FAIL need attention.</summary>
        public static string Run()
        {
            if (EditorApplication.isPlaying)
            {
                return "[mach-002] Track continuity test skipped: exit play mode first.";
            }

            var sb = new StringBuilder("[mach-002] Track continuity test (a 0.4 m test marble rolled down every chute):\n");
            SimulationMode previous = Physics.simulationMode;
            int failures = 0;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                List<Transform> chutes = FindChutes();
                var sim = Object.FindAnyObjectByType<MachineRaceSimulation>();
                for (int ci = 0; ci < chutes.Count; ci++)
                {
                    failures += TestChute(chutes[ci], ci, sim, sb);
                }

                if (chutes.Count == 0)
                {
                    sb.AppendLine("FAIL: no chutes found (is the Grand Prix scene open?)");
                    failures++;
                }
            }
            finally
            {
                Physics.simulationMode = previous;
            }

            sb.AppendLine(failures == 0 ? "Result: PASS, the track is continuous." : $"Result: {failures} problem(s), see the FAIL lines above.");
            return sb.ToString();
        }

        static List<Transform> FindChutes()
        {
            var chutes = new List<Transform>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.name.StartsWith("Chute ") && t.Find("Floor") != null)
                {
                    chutes.Add(t);
                }
            }

            chutes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return chutes;
        }

        static int TestChute(Transform chute, int index, MachineRaceSimulation sim, StringBuilder sb)
        {
            int failures = 0;
            Transform floor = chute.Find("Floor");
            var meshFilter = floor.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                sb.AppendLine($"FAIL {chute.name}: no floor mesh");
                return 1;
            }

            Vector3[] v = meshFilter.sharedMesh.vertices;
            int rows = v.Length / 8;
            var pts = new Vector3[rows];
            for (int i = 0; i < rows; i++)
            {
                pts[i] = floor.TransformPoint((v[2 * i] + v[2 * i + 1]) * 0.5f);
            }

            // 0. Bends: a chute 2.1 m wide folds its inner wall (a hole) when a bend is tighter than about 1.2 m radius, and fast marbles
            //    cannot hold it. Rows are ~0.25 m apart, so a turn of more than 12 degrees between rows is a radius under ~1.2 m.
            for (int i = 1; i < rows - 1; i++)
            {
                Vector3 a = pts[i] - pts[i - 1];
                Vector3 b = pts[i + 1] - pts[i];
                a.y = 0f;
                b.y = 0f;
                if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f)
                {
                    continue;
                }

                float turn = Vector3.Angle(a, b);
                if (turn > MaxTurnPerRowDegrees)
                {
                    float radius = a.magnitude / (turn * Mathf.Deg2Rad);
                    sb.AppendLine($"FAIL {chute.name}: tight bend at row {i} of {rows} ({turn:0} deg in {a.magnitude:0.00} m, radius about {radius:0.0} m): the inner wall folds and racers fly off. Lengthen the straight before it.");
                    failures++;
                    break;
                }
            }

            // 1. Joints: the chute must start at the previous station's exit and end at the next station's entry.
            string joints = string.Empty;
            if (sim != null && sim.Stations != null)
            {
                if (index < sim.Stations.Count && sim.Stations[index] != null && sim.Stations[index].exit != null)
                {
                    float d = NearestFlat(pts, sim.Stations[index].exit.position, out float dy);
                    joints += $" start {d:0.00} m";
                    if (d > JointToleranceMetres || Mathf.Abs(dy) > JointHeightToleranceMetres)
                    {
                        sb.AppendLine($"FAIL {chute.name}: starts {d:0.00} m / {dy:0.00} m height off the exit of {sim.Stations[index].displayName}");
                        failures++;
                    }
                }

                if (index + 1 < sim.Stations.Count && sim.Stations[index + 1] != null && sim.Stations[index + 1].entry != null)
                {
                    float d = NearestFlat(pts, sim.Stations[index + 1].entry.position, out float dy);
                    joints += $", end {d:0.00} m";
                    if (d > JointToleranceMetres || Mathf.Abs(dy) > JointHeightToleranceMetres)
                    {
                        sb.AppendLine($"FAIL {chute.name}: ends {d:0.00} m / {dy:0.00} m height off the entry of {sim.Stations[index + 1].displayName}");
                        failures++;
                    }
                }
            }

            // 2. Roll a stream of racer-sized test balls down it, launched at 4 m/s (they leave the vortex and the lifts at 3-7 m/s).
            const int Stream = 8;
            PhysicsScene ps = Physics.defaultPhysicsScene;
            var material = new PhysicsMaterial("ContinuityTest") { dynamicFriction = 0.3f, staticFriction = 0.3f, bounciness = 0.25f };
            var balls = new GameObject[Stream];
            var bodies = new Rigidbody[Stream];
            var best = new int[Stream];
            var finished = new bool[Stream];
            string outcome = "ok";
            int lost = 0, stalled = 0, completed = 0;
            string firstLoss = string.Empty;
            try
            {
                Vector3 dir0 = (pts[Mathf.Min(5, rows - 1)] - pts[Mathf.Min(3, rows - 1)]).normalized;
                float t = 0f, lastProgressT = 0f;
                for (int step = 0; step < 4000 && completed + lost < Stream; step++)
                {
                    for (int i = 0; i < Stream; i++)
                    {
                        if (balls[i] == null && !finished[i] && t >= i * 0.3f)
                        {
                            balls[i] = new GameObject("TEST_BALL") { hideFlags = HideFlags.DontSave };
                            balls[i].transform.position = pts[Mathf.Min(3, rows - 1)] + Vector3.up * (BallRadius + 0.03f);
                            var sphere = balls[i].AddComponent<SphereCollider>();
                            sphere.radius = BallRadius;
                            sphere.sharedMaterial = material;
                            bodies[i] = balls[i].AddComponent<Rigidbody>();
                            bodies[i].mass = 0.2f;
                            bodies[i].maxLinearVelocity = 14f;
                            bodies[i].solverIterations = 6;
                            bodies[i].maxDepenetrationVelocity = 2f;
                            bodies[i].collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                            bodies[i].linearVelocity = dir0 * 4f;
                            best[i] = 3;
                        }
                    }

                    ps.Simulate(0.01f);
                    t += 0.01f;
                    for (int i = 0; i < Stream; i++)
                    {
                        if (balls[i] == null)
                        {
                            continue;
                        }

                        Vector3 p = bodies[i].position;
                        int nearest = -1;
                        float nearestDistance = 99f;
                        for (int k = Mathf.Max(0, best[i] - 3); k < Mathf.Min(rows, best[i] + 80); k++)
                        {
                            float d = Vector3.Distance(p, pts[k]);
                            if (d < nearestDistance)
                            {
                                nearestDistance = d;
                                nearest = k;
                            }
                        }

                        if (nearest > best[i])
                        {
                            best[i] = nearest;
                            lastProgressT = t;
                        }

                        bool left = nearest < 0 || nearestDistance > 1.8f || p.y < pts[Mathf.Clamp(best[i], 0, rows - 1)].y - 0.6f;
                        if (left)
                        {
                            lost++;
                            if (firstLoss.Length == 0)
                            {
                                firstLoss = $"ball {i + 1} LEFT THE CHUTE after row {best[i]} of {rows} at {p:0.0}";
                            }

                            finished[i] = true;
                            Object.DestroyImmediate(balls[i]);
                            balls[i] = null;
                        }
                        else if (best[i] >= rows - 3)
                        {
                            completed++;
                            finished[i] = true;
                            Object.DestroyImmediate(balls[i]);
                            balls[i] = null;
                        }
                    }

                    if (t - lastProgressT > 6f && completed + lost < Stream)
                    {
                        stalled = Stream - completed - lost;
                        break;
                    }
                }

                if (lost > 0 || stalled > 0)
                {
                    outcome = lost > 0 ? $"{lost} of {Stream} balls fell out: {firstLoss}" : $"{stalled} of {Stream} balls STALLED";
                    sb.AppendLine($"FAIL {chute.name}: {outcome}");
                    failures++;
                }
                else
                {
                    outcome = $"ok, {Stream} of {Stream} balls reached the end";
                }
            }
            finally
            {
                for (int i = 0; i < Stream; i++)
                {
                    if (balls[i] != null)
                    {
                        Object.DestroyImmediate(balls[i]);
                    }
                }

                Object.DestroyImmediate(material);
            }

            sb.AppendLine($"{(failures == 0 ? "ok  " : "    ")} {chute.name}: {outcome};{joints}");
            return failures;
        }

        static float NearestFlat(Vector3[] pts, Vector3 anchor, out float heightDifference)
        {
            float best = 99f;
            heightDifference = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                float d = Vector2.Distance(new Vector2(pts[i].x, pts[i].z), new Vector2(anchor.x, anchor.z));
                if (d < best)
                {
                    best = d;
                    heightDifference = pts[i].y - anchor.y;
                }
            }

            return best;
        }
    }
}
