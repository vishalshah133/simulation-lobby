using System.Collections.Generic;
using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.Impact.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Impact_SoftSweep_3D.unity</c> — impact-003, "0% vs 100% Soft": an amber
    /// glass capsule dropped onto a piano-black plinth five times, from 0% to 100% soft.
    /// Menu: <c>Simulation Lobby ▸ Build Scene ▸ Impact_SoftSweep_3D</c>.
    /// </summary>
    /// <remarks>
    /// Re-running replaces the scene but never the config or material assets, so tuning survives a
    /// rebuild. <c>Reset Config ▸ Soft Sweep</c> puts the configs back to the plan's baseline.
    /// </remarks>
    static class SoftSweepSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Impact_SoftSweep_3D.unity";
        const string ConfigFolder = "Assets/Settings/Configs/SoftSweep";
        const string SweepPath = ConfigFolder + "/SoftSweep_001.asset";

        static readonly float[] Softnesses = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        /// <summary>Cognac amber — the video's one hue (plans/videos/impact-003-soft-sweep.md).</summary>
        static readonly Color Hue = new Color(0.93f, 0.5f, 0.17f, 1f);

        [MenuItem("Simulation Lobby/Reset Config/Soft Sweep (tuning baseline)")]
        static void ResetConfigs()
        {
            SweepConfig sweep = EnsureConfigs(out List<SoftDropConfig> takes);
            for (int i = 0; i < takes.Count; i++)
            {
                ApplyTakeDefaults(takes[i], Softnesses[i]);
                EditorUtility.SetDirty(takes[i]);
            }

            ApplySweepDefaults(sweep, takes);
            EditorUtility.SetDirty(sweep);
            AssetDatabase.SaveAssets();
            Debug.Log($"[impact] Reset {ConfigFolder} to the tuning baseline.", sweep);
        }

        [MenuItem("Simulation Lobby/Build Scene/Impact_SoftSweep_3D")]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[impact] Exit play mode before building the scene.");
                return;
            }

            SweepConfig sweep = EnsureConfigs(out List<SoftDropConfig> takes);
            SoftDropConfig layout = takes[0];
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera: a long lens (28°) for product-shot compression, framed so everything that
            //     moves sits between the caption panel and the footer at 9:16. ---
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 28f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            cameraObject.AddComponent<AudioListener>();
            float contentTop = layout.DropCenter.y + layout.CapsuleHalfHeight + 0.05f;
            FrameCamera(camera, layout, contentTop);

            // --- Studio: cyclorama, lights, softbox cards, reflection probe, post, materials. ---
            StudioSetBuilder.Materials materials = StudioSetBuilder.Build(camera, new StudioSetBuilder.Options
            {
                hue = Hue,
                glassName = "SoftSweepGlass",
                subjectCenter = layout.RestCenter,
                subjectHeight = contentTop - layout.PlinthTopY
            });

            // --- Plinth: a smoked-glass block with the hole cut into its top — the exact shape the
            //     solver collides with (box minus cylinder). Glass, not lacquer, so the squeeze into the
            //     hole is visible: from a product-shot camera only a few degrees above the top you can't
            //     see down into a hole, and an opaque plinth hid everything that went in (playtest 3). ---
            var plinth = new GameObject("Plinth");
            plinth.AddComponent<MeshFilter>().sharedMesh = StudioSetBuilder.SaveMesh(
                BuildPlinth(layout.plinthSize, layout.holeRadius, layout.holeDepth), "SoftSweepPlinth");
            plinth.AddComponent<MeshRenderer>().sharedMaterial = materials.smokedGlass;

            // --- Brass lip: the rounded ring around the hole, identical to the solver's torus. The glass
            //     rests on it; the soft takes roll over it into the hole. ---
            if (layout.holeRadius > 0f)
            {
                var lip = new GameObject("Brass Lip");
                lip.transform.position = new Vector3(0f, layout.PlinthTopY, 0f);
                lip.AddComponent<MeshFilter>().sharedMesh = StudioSetBuilder.SaveMesh(
                    BuildTorus(layout.holeRadius + layout.lipRadius, layout.lipRadius, 128, 32), "SoftSweepLip");
                lip.AddComponent<MeshRenderer>().sharedMaterial = materials.brass;
            }

            // --- Brass edge trim around the plinth top: a hairline of warm metal on the black block. ---
            var trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim.name = "Brass Trim";
            trim.transform.position = new Vector3(0f, layout.PlinthTopY - 0.02f, 0f);
            trim.transform.localScale = new Vector3(layout.plinthSize.x + 0.012f, 0.018f, layout.plinthSize.z + 0.012f);
            trim.GetComponent<MeshRenderer>().sharedMaterial = materials.brass;
            Object.DestroyImmediate(trim.GetComponent<Collider>());

            // --- Runner + simulation + presenter. ---
            var simulationObject = new GameObject("Simulation");
            SweepRunner runner = simulationObject.AddComponent<SweepRunner>();
            runner.sweep = sweep;
            runner.seed = 0;
            runner.autoStart = true;
            SoftDropSimulation simulation = simulationObject.AddComponent<SoftDropSimulation>();
            SoftDropPresenter presenter = simulationObject.AddComponent<SoftDropPresenter>();

            // --- The capsule: a live mesh drawn from the solver every frame, in tinted glass. ---
            var capsule = new GameObject("Capsule (soft body view)");
            capsule.AddComponent<MeshFilter>();
            var capsuleRenderer = capsule.AddComponent<MeshRenderer>();
            capsuleRenderer.sharedMaterial = materials.glass;
            SoftBodyMeshView view = capsule.AddComponent<SoftBodyMeshView>();
            view.Bind(simulation);

            // --- Audio: clink ↔ squelch, creak, wobble, whoosh, caption notes, ambient bed. ---
            var audioObject = new GameObject("Material Audio");
            audioObject.AddComponent<AudioSource>();
            MaterialContactAudio audio = audioObject.AddComponent<MaterialContactAudio>();

            // --- HUD: the caption is the whole storyline. ---
            CaptionHud hud = CaptionCanvasBuilder.Build(sweep.Caption(0), presenter.hookLine, presenter.footer);

            presenter.Bind(runner, simulation, audio, hud);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  {sweep.TakeCount} takes ({string.Join(", ", TakeLabels(sweep))}), " +
                $"{sweep.TotalSeconds:0.0}s total at {1f / sweep.FixedTimestep:0} Hz\n" +
                $"  capsule r={layout.capsuleRadius:0.00} half-length={layout.capsuleHalfLength:0.00}, " +
                $"drop {layout.dropHeight:0.00}m onto the plinth ({layout.FallSeconds:0.00}s fall)\n" +
                "\n  Press Play to run the full sweep. Set SweepRunner.soloTake to re-run one take alone.");
            EditorGUIUtility.PingObject(sweep);
        }

        static IEnumerable<string> TakeLabels(SweepConfig sweep)
        {
            foreach (SweepConfig.Take take in sweep.takes)
            {
                yield return take.label;
            }
        }

        /// <summary>
        /// Pulls the camera back until the plinth, spike and the capsule at its drop height all fit a
        /// 9:16 frame between the caption (top ~25%) and the footer (bottom ~12%), looking slightly down.
        /// </summary>
        static void FrameCamera(Camera camera, SoftDropConfig layout, float contentTop)
        {
            const float aspect = 1080f / 1920f;
            const float bandBottom = 0.14f;
            const float bandTop = 0.73f;
            const float pitchDegrees = 7f;

            Vector3 half = layout.plinthSize * 0.5f;
            float halfWidth = Mathf.Max(half.x, layout.capsuleRadius + Mathf.Abs(layout.dropOffsetX) + 0.2f);
            var points = new[]
            {
                new Vector3(-halfWidth, 0f, half.z), new Vector3(halfWidth, 0f, half.z),
                new Vector3(-halfWidth, 0f, -half.z), new Vector3(halfWidth, 0f, -half.z),
                new Vector3(-halfWidth, contentTop, 0f), new Vector3(halfWidth, contentTop, 0f)
            };

            Transform t = camera.transform;
            camera.aspect = aspect;
            Vector3 direction = Quaternion.Euler(-pitchDegrees, 0f, 0f) * Vector3.back; // up and toward +Z
            direction = new Vector3(0f, Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            float targetY = contentTop * 0.5f;
            float distance = 8f;

            for (int i = 0; i < 200; i++)
            {
                Vector3 target = new Vector3(0f, targetY, 0f);
                t.position = target + direction * distance;
                t.rotation = Quaternion.LookRotation(target - t.position);

                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (Vector3 point in points)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(point);
                    minX = Mathf.Min(minX, viewport.x);
                    maxX = Mathf.Max(maxX, viewport.x);
                    minY = Mathf.Min(minY, viewport.y);
                    maxY = Mathf.Max(maxY, viewport.y);
                }

                bool fitsY = maxY - minY <= bandTop - bandBottom;
                bool fitsX = minX >= 0.06f && maxX <= 0.94f;
                float centreError = (minY + maxY) * 0.5f - (bandBottom + bandTop) * 0.5f;
                bool tight = maxY - minY >= (bandTop - bandBottom) * 0.97f || !fitsX;

                if (fitsY && fitsX && Mathf.Abs(centreError) < 0.004f && tight)
                {
                    break;
                }

                if (!fitsY || !fitsX)
                {
                    distance *= 1.02f;
                }
                else if (!tight)
                {
                    distance *= 0.99f;
                }

                // Content sits high in the frame → aim higher, and vice versa.
                float viewHeight = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                targetY += centreError * viewHeight * 0.5f;
            }

            camera.ResetAspect();
        }

        /// <summary>
        /// A box standing on the floor with a round blind hole in the middle of its top. Hard-edged
        /// faces (each face has its own vertices and normals), a smooth hole wall. No bottom face.
        /// </summary>
        static Mesh BuildPlinth(Vector3 size, float holeRadius, float holeDepth, int segments = 96)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            float hx = size.x * 0.5f;
            float hz = size.z * 0.5f;
            float top = size.y;

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Vector3 facing)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                normals.Add(na); normals.Add(nb); normals.Add(nc); normals.Add(nd);
                // Wind so the face points along `facing` (Unity: front face = (b-a)x(c-a)).
                bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f;
                if (!flip)
                {
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
                else
                {
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                    triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
                }
            }

            void Flat(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n) => Quad(a, b, c, d, n, n, n, n, n);

            // Sides.
            Flat(new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), new Vector3(hx, top, hz), new Vector3(-hx, top, hz), Vector3.forward);
            Flat(new Vector3(hx, 0f, -hz), new Vector3(-hx, 0f, -hz), new Vector3(-hx, top, -hz), new Vector3(hx, top, -hz), Vector3.back);
            Flat(new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz), new Vector3(hx, top, -hz), new Vector3(hx, top, hz), Vector3.right);
            Flat(new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(-hx, top, hz), new Vector3(-hx, top, -hz), Vector3.left);

            if (holeRadius <= 0f)
            {
                Flat(new Vector3(-hx, top, -hz), new Vector3(hx, top, -hz), new Vector3(hx, top, hz), new Vector3(-hx, top, hz), Vector3.up);
            }
            else
            {
                // Top: an annulus from the hole's circle out to the square edge, along rays from the
                // centre. Starting at 45° with a multiple-of-4 segment count puts a ray through every
                // corner, so the outline is exactly square.
                segments = Mathf.Max(8, segments / 4 * 4);
                float bottom = top - holeDepth;
                Vector3 Ray(int k, out Vector3 inner)
                {
                    float angle = Mathf.PI * 0.25f + k * Mathf.PI * 2f / segments;
                    float c = Mathf.Cos(angle);
                    float s = Mathf.Sin(angle);
                    float reach = Mathf.Min(hx / Mathf.Max(Mathf.Abs(c), 1e-6f), hz / Mathf.Max(Mathf.Abs(s), 1e-6f));
                    inner = new Vector3(c * holeRadius, top, s * holeRadius);
                    return new Vector3(c * reach, top, s * reach);
                }

                for (int k = 0; k < segments; k++)
                {
                    Vector3 outerA = Ray(k, out Vector3 innerA);
                    Vector3 outerB = Ray(k + 1, out Vector3 innerB);
                    Flat(innerA, outerA, outerB, innerB, Vector3.up);

                    // Hole wall: normals point in toward the axis (it's seen from inside the hole).
                    Vector3 wallA = new Vector3(-innerA.x, 0f, -innerA.z).normalized;
                    Vector3 wallB = new Vector3(-innerB.x, 0f, -innerB.z).normalized;
                    Vector3 lowA = new Vector3(innerA.x, bottom, innerA.z);
                    Vector3 lowB = new Vector3(innerB.x, bottom, innerB.z);
                    Quad(innerA, innerB, lowB, lowA, wallA, wallB, wallB, wallA, (wallA + wallB) * 0.5f);

                    // Hole floor: a fan slice.
                    Vector3 centre = new Vector3(0f, bottom, 0f);
                    Quad(centre, lowA, lowB, centre, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up);
                }
            }

            var mesh = new Mesh { name = "Plinth" };
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildTorus(float majorRadius, float minorRadius, int majorSegments = 96, int minorSegments = 20)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < majorSegments; i++)
            {
                float u = i * Mathf.PI * 2f / majorSegments;
                var centre = new Vector3(Mathf.Cos(u) * majorRadius, 0f, Mathf.Sin(u) * majorRadius);
                Vector3 outward = centre.normalized;
                for (int j = 0; j < minorSegments; j++)
                {
                    float v = j * Mathf.PI * 2f / minorSegments;
                    vertices.Add(centre + outward * (Mathf.Cos(v) * minorRadius) + Vector3.up * (Mathf.Sin(v) * minorRadius));
                }
            }

            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < minorSegments; j++)
                {
                    int a = i * minorSegments + j;
                    int b = ((i + 1) % majorSegments) * minorSegments + j;
                    int c = i * minorSegments + (j + 1) % minorSegments;
                    int d = ((i + 1) % majorSegments) * minorSegments + (j + 1) % minorSegments;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            var mesh = new Mesh { name = "Torus" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();

            // Outward check against the tube's own centre line: sample the first vertex.
            Vector3 firstCentre = new Vector3(majorRadius, 0f, 0f);
            if (Vector3.Dot(mesh.normals[0], vertices[0] - firstCentre) < 0f)
            {
                int[] t = mesh.triangles;
                for (int k = 0; k < t.Length; k += 3)
                {
                    (t[k + 1], t[k + 2]) = (t[k + 2], t[k + 1]);
                }

                mesh.triangles = t;
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ configs

        static SweepConfig EnsureConfigs(out List<SoftDropConfig> takes)
        {
            Directory.CreateDirectory(ConfigFolder);
            takes = new List<SoftDropConfig>();
            for (int i = 0; i < Softnesses.Length; i++)
            {
                string path = $"{ConfigFolder}/SoftDrop_{Mathf.RoundToInt(Softnesses[i] * 100f):000}.asset";
                var take = AssetDatabase.LoadAssetAtPath<SoftDropConfig>(path);
                if (take == null)
                {
                    take = ScriptableObject.CreateInstance<SoftDropConfig>();
                    ApplyTakeDefaults(take, Softnesses[i]);
                    AssetDatabase.CreateAsset(take, path);
                }

                takes.Add(take);
            }

            var sweep = AssetDatabase.LoadAssetAtPath<SweepConfig>(SweepPath);
            if (sweep == null)
            {
                sweep = ScriptableObject.CreateInstance<SweepConfig>();
                ApplySweepDefaults(sweep, takes);
                AssetDatabase.CreateAsset(sweep, SweepPath);
            }

            AssetDatabase.SaveAssets();
            return sweep;
        }

        /// <summary>
        /// Baseline from the plan. Every take is identical except <see cref="SoftDropConfig.softness"/> —
        /// the drop offset and tilt are the same on all five so the comparison has one variable.
        /// </summary>
        static void ApplyTakeDefaults(SoftDropConfig config, float softness)
        {
            // Field initialisers are the baseline; copy them over whatever tuning the asset holds.
            string assetName = config.name;
            var defaults = ScriptableObject.CreateInstance<SoftDropConfig>();
            EditorUtility.CopySerialized(defaults, config);
            Object.DestroyImmediate(defaults);
            config.name = assetName;

            config.fixedTimestep = 1f / 60f;
            config.maxDurationSeconds = 10f;
            config.softness = softness;
            config.intent = $"impact-003 take at {softness:P0} soft. Identical to its siblings except softness.";
        }

        static void ApplySweepDefaults(SweepConfig sweep, List<SoftDropConfig> takes)
        {
            sweep.captionFormat = "SOFT {0}";
            sweep.takes = new SweepConfig.Take[takes.Count];
            for (int i = 0; i < takes.Count; i++)
            {
                sweep.takes[i] = new SweepConfig.Take
                {
                    config = takes[i],
                    label = $"{Mathf.RoundToInt(Softnesses[i] * 100f)}%"
                };
            }

            sweep.countdownSeconds = 3;
            sweep.firstLeadInSeconds = 1.2f;
            sweep.leadInSeconds = 0.35f;
            sweep.takeSeconds = 3.4f;
            sweep.endHoldSeconds = 0.4f;
            sweep.intent = "impact-003 '0% vs 100% Soft': amber glass capsule onto a chrome spike, five takes. " +
                           "Target 14-22s, loopable.";
        }
    }
}
