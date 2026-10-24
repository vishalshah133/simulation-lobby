using System.Collections.Generic;
using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared.EditorTools;
using SimulationLobby.Shared.Tracks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.Race.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Race_GrandPrix_3D.unity</c>: a field of jewel-coloured glass marbles racing
    /// down a piano-black lacquer course with brass rails, on a pale polished studio floor. 16:9, for long form.
    /// Menu: <c>Simulation Lobby ▸ 3D Formats ▸ Race (race) ▸ Grand Prix</c>.
    /// </summary>
    /// <remarks>
    /// Everything is generated from primitives and swept meshes, so there are no model files to import.
    /// Re-running replaces the scene but never the config, layout or material assets, so tuning survives.
    /// The course, marbles, camera, HUD and audio come from <see cref="RaceRigBuilder"/> (shared with the
    /// elimination show's heats); the floor, lights and reflections from <see cref="RaceStudioEnvironment"/>.
    /// </remarks>
    static class RaceSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Race_GrandPrix_3D.unity";
        const string ConfigFolder = RaceAssets.ConfigFolder;
        const string LayoutPath = ConfigFolder + "/RaceTrackLayout_GrandPrix.asset";
        const string ConfigPath = ConfigFolder + "/RaceConfig_001.asset";
        const int LeaderboardRows = 14;

        [MenuItem("Simulation Lobby/3D Formats/Race (race)/Reset Track Layout (default course)", false, 222)]
        static void ResetLayout()
        {
            Directory.CreateDirectory(ConfigFolder);
            TrackLayout layout = EnsureLayout();
            layout.ResetToDefault();
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            Debug.Log($"[race] Reset {LayoutPath} to the default course ({layout.sections.Length} sections, {layout.obstacles.Length} obstacles).", layout);
        }

        [MenuItem("Simulation Lobby/3D Formats/Race (race)/Grand Prix", false, 201)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[race] Exit play mode before building the scene.");
                return;
            }

            Directory.CreateDirectory(ConfigFolder);
            Directory.CreateDirectory(RaceAssets.MaterialFolder);
            TrackLayout layout = EnsureLayout();
            RaceConfig config = EnsureConfig(layout);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- The rig: course, marbles, simulation, presenter, camera, leaderboard and audio. ---
            RaceRig rig = RaceRigBuilder.BuildRig(layout, config, Vector3.zero, null, "GRAND PRIX",
                $"{StudioSetBuilder.Folder}/Meshes", "Race", LeaderboardRows);
            rig.root.name = "Simulation";
            rig.camera.gameObject.name = "Main Camera";

            // --- Runner: random seed per Play, logged by the runner; paste it back to re-render. ---
            var runner = rig.root.AddComponent<SimulationRunner>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            // --- Studio: post, ambient, floor, lights, reflections. ---
            RaceStudioEnvironment.Build(rig.camera, rig.bounds, Vector3.zero);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Report(rig.samples, rig.totalLength, rig.finishDistance, rig.lift);
        }

        // ------------------------------------------------------------------ assets

        static TrackLayout EnsureLayout()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TrackLayout>(LayoutPath);
            if (existing != null)
            {
                // A layout saved before obstacles existed is rebuilt from the current default once. After
                // that its version matches and any hand edits are left alone.
                if (existing.version < TrackLayout.CurrentVersion)
                {
                    existing.ResetToDefault();
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[race] Upgraded {LayoutPath} to course version {TrackLayout.CurrentVersion} (obstacles added).", existing);
                }

                return existing;
            }

            var layout = ScriptableObject.CreateInstance<TrackLayout>();
            layout.version = TrackLayout.CurrentVersion;
            AssetDatabase.CreateAsset(layout, LayoutPath);
            return layout;
        }

        static RaceConfig EnsureConfig(TrackLayout layout)
        {
            var config = AssetDatabase.LoadAssetAtPath<RaceConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RaceConfig>();
                config.fixedTimestep = 1f / 120f;
                config.maxDurationSeconds = 240f;
                config.intent = "race-001: twelve jewel-glass marbles down a black-lacquer course with brass rails. " +
                                "Tune for lead changes first, length second.";
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            if (config.track != layout && config.track == null)
            {
                config.track = layout;
                EditorUtility.SetDirty(config);
            }

            return config;
        }

        // ------------------------------------------------------------------ utilities

        static void Report(List<TrackMeshBuilder.Sample> samples, float totalLength, float finishDistance, float lift)
        {
            float estimate = TrackMeshBuilder.EstimateSeconds(samples, finishDistance);
            int crossings = TrackMeshBuilder.CountCrossings(samples, out string where);
            float drop = samples[0].position.y - samples[samples.Count - 1].position.y;
            Debug.Log($"[race] Built {ScenePath}: {totalLength:0}m of track, {drop:0.0}m drop, start {lift:0.0}m up. " +
                      $"Rough race time ~{estimate:0}s (a model, not a measurement: read the real one off the first Play).");
            if (crossings > 0)
            {
                Debug.LogWarning($"[race] The layout passes through itself {crossings} times, first {where}. " +
                                 "Loosen a radius or add drop before using it.");
            }
        }
    }
}
