using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Capture.EditorTools
{
    /// <summary>
    /// Editor tooling for the comp-001 master scene. Calls the ten segment builders by MENU-PATH STRING
    /// (EditorApplication.ExecuteMenuItem), so this assembly never references a format.
    /// </summary>
    static class CompilationMenu
    {
        const string Root = "Simulation Lobby/Tools/Compilation (comp-001)/";
        const string MasterScenePath = "Assets/Scenes/Comp001_Master.unity";
        const string PlaylistFolder = "Assets/Settings/Compilation";
        const string PlaylistPath = PlaylistFolder + "/Comp001Playlist.asset";

        sealed class Segment
        {
            public string MenuPath;   // copied verbatim from the builders' [MenuItem] attributes
            public string ScenePath;  // copied verbatim from the builders' ScenePath constants
            public string Label;
            public CompilationTransition Transition;

            public string SceneName { get { return Path.GetFileNameWithoutExtension(ScenePath); } }
        }

        // Transitions: segment 1 is the cut from the intro card; segments 2-10 follow the rotating pattern
        // FadeBlack, WipeLeft, IrisClose, FlashWhite, WipeRight, FadeBlack, IrisClose, WipeLeft, FlashWhite.
        // Playing order: mve, brk, bell, conv, press, esc, brach, clack, rom, royale. The Comp001_NN scene names are ids, not positions.
        static readonly Segment[] Segments =
        {
            new Segment { MenuPath = "Simulation Lobby/2D Formats/Multiply vs Escape (mve)/Wide: Fill or Escape (comp-001)",
                          ScenePath = "Assets/Scenes/Comp001_10_FillOrEscape_Wide.unity", Label = "Fill or Escape", Transition = CompilationTransition.WipeRight },
            new Segment { MenuPath = "Simulation Lobby/2D Formats/Ring Breaker (brk)/Wide: Three Rings (comp-001)",
                          ScenePath = "Assets/Scenes/Comp001_08_ThreeRings_Wide.unity", Label = "Three Rings", Transition = CompilationTransition.FadeBlack },
            new Segment { MenuPath = "Simulation Lobby/2D Formats/Bell Curve Drop (bell)/Wide: Rigged Board (comp-001)",
                          ScenePath = "Assets/Scenes/Comp001_09_RiggedBoard_Wide.unity", Label = "Rigged Board", Transition = CompilationTransition.WipeLeft },
            new Segment { MenuPath = "Simulation Lobby/2D Formats/Math Convergence (conv)/Wide: Needle Ellipse (comp-001)",
                          ScenePath = "Assets/Scenes/Comp001_04_NeedleEllipse_Wide.unity", Label = "Needle Ellipse", Transition = CompilationTransition.IrisClose },
            new Segment { MenuPath = "Simulation Lobby/2D Formats/Hydraulic Press (press)/Wide: Iron Press (comp-001)",
                          ScenePath = "Assets/Scenes/Comp001_05_IronPress_Wide.unity", Label = "Iron Press", Transition = CompilationTransition.FlashWhite },
            new Segment { MenuPath = "Simulation Lobby/2D Formats/Escape (esc)/Wide: Shrinking Gap (comp-001)",
                          ScenePath = "Assets/Scenes/Comp001_02_ShrinkingGap_Wide.unity", Label = "Shrinking Gap", Transition = CompilationTransition.WipeRight },
            new Segment { MenuPath = "Simulation Lobby/2D Formats/Brachistochrone (brach)/Wide: Racetrack (comp-001)",
                          ScenePath = "Assets/Scenes/Comp001_07_Racetrack_Wide.unity", Label = "Racetrack", Transition = CompilationTransition.FadeBlack },
            new Segment { MenuPath = "Simulation Lobby/2D Formats/Pi Clacks (clack)/Wide: Digit Jump (comp-001)",
                          ScenePath = "Assets/Scenes/Comp001_01_DigitJump_Wide.unity", Label = "Digit Jump", Transition = CompilationTransition.IrisClose },
            new Segment { MenuPath = "Simulation Lobby/2D Formats/Release or Multiply (rom)/Wide: Gate Funnel (comp-001)",
                          ScenePath = "Assets/Scenes/Comp001_03_GateFunnel_Wide.unity", Label = "Gate Funnel", Transition = CompilationTransition.WipeLeft },
            new Segment { MenuPath = "Simulation Lobby/2D Formats/Color Rumble (royale)/Wide: Six Colours (comp-001)",
                          ScenePath = "Assets/Scenes/Comp001_06_SixColours_Wide.unity", Label = "Six Colours", Transition = CompilationTransition.FlashWhite },
        };

        [MenuItem(Root + "Build All (10 segments + master scene)", false, 930)]
        static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[comp] Exit Play mode first.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[comp] Build All cancelled (unsaved scenes).");
                return;
            }

            for (int i = 0; i < Segments.Length; i++)
            {
                Segment s = Segments[i];
                DateTime started = DateTime.UtcNow.AddSeconds(-1);
                Debug.Log($"[comp] Building segment {i + 1}/{Segments.Length}: {s.MenuPath}");

                bool executed = EditorApplication.ExecuteMenuItem(s.MenuPath);
                if (!executed)
                {
                    Fail(i, s, "ExecuteMenuItem returned false: the menu path does not exist (renamed or not compiled).");
                    return;
                }

                string abs = Path.GetFullPath(s.ScenePath);
                if (!File.Exists(abs) || File.GetLastWriteTimeUtc(abs) < started)
                {
                    Fail(i, s, "the scene file was not written (the builder threw or aborted: check the Console above this message).");
                    return;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            FinishMaster();
            Debug.Log("[comp] Build All done: 10 segment scenes + master built. Master scene is open. Press Play to run the compilation.");
        }

        [MenuItem(Root + "Rebuild Master Scene Only", false, 931)]
        static void RebuildMaster()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[comp] Exit Play mode first.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var missing = Segments.Where(s => !File.Exists(Path.GetFullPath(s.ScenePath))).Select(s => s.ScenePath).ToList();
            if (missing.Count > 0)
            {
                Debug.LogError("[comp] Segment scenes missing; run 'Build All' first:\n" + string.Join("\n", missing));
                return;
            }

            FinishMaster();
            Debug.Log("[comp] Master scene rebuilt (segment scenes untouched). Master scene is open.");
        }

        [MenuItem(Root + "Print Recording Checklist", false, 951)]
        static void PrintChecklist()
        {
            Debug.Log(
                "[comp] RECORDING CHECKLIST (comp-001)\n" +
                "1. Open Assets/Scenes/Comp001_Master.unity.\n" +
                "2. Game view: set the resolution to 1920x1080 (Full HD fixed, NOT 'Free Aspect'), scale 1x, Play Focused is fine.\n" +
                "3. Window > General > Recorder > Recorder Window. Add Recorder > Movie.\n" +
                "   - Recording Mode: Manual (stops when Play mode exits; the player exits Play mode itself after the outro).\n" +
                "   - Frame rate: Constant 60 fps (Playback Constant). Cap: leave 'Cap FPS' on.\n" +
                "   - Source: Game View. Output resolution: 1920x1080 (16:9).\n" +
                "   - Encoder: Unity Media Encoder, H.264 MP4, quality High. Include Audio: ON.\n" +
                "   - Output file: outside Assets/ (the repo ignores rendered video).\n" +
                "4. Click START RECORDING in the Recorder window FIRST (it enters Play mode).\n" +
                "   If you prefer, press Play first then Start Recording within the lead-in; startDelaySeconds (default 3 s) is black lead-in.\n" +
                "5. Do not touch the Editor until Play mode exits by itself. Trim the black lead-in from the front.\n" +
                "6. Copy the chapters + seeds block the player logs at the end ('[comp] DONE') into the plan and the YouTube description.");
        }

        // ------------------------------------------------------------------ master scene

        static void FinishMaster()
        {
            CompilationPlaylist playlist = EnsurePlaylist();
            CreateMasterScene(playlist); // saves the file, then registers all eleven scenes in Build Settings
        }

        static CompilationPlaylist EnsurePlaylist()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            if (!AssetDatabase.IsValidFolder(PlaylistFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Compilation");
            }

            var playlist = AssetDatabase.LoadAssetAtPath<CompilationPlaylist>(PlaylistPath);
            if (playlist == null)
            {
                playlist = ScriptableObject.CreateInstance<CompilationPlaylist>();
                AssetDatabase.CreateAsset(playlist, PlaylistPath);
            }

            // Hand-edited values survive: only segments whose scene is not in the list yet are appended.
            var entries = new List<CompilationEntry>(playlist.entries ?? new CompilationEntry[0]);
            bool changed = false;
            foreach (Segment s in Segments)
            {
                if (entries.Any(e => e != null && e.sceneName == s.SceneName))
                {
                    continue;
                }

                entries.Add(new CompilationEntry { sceneName = s.SceneName, label = s.Label, transitionIn = s.Transition });
                changed = true;
            }

            if (changed)
            {
                playlist.entries = entries.ToArray();
                EditorUtility.SetDirty(playlist);
            }

            AssetDatabase.SaveAssets();
            return playlist;
        }

        static void AddToBuildSettings()
        {
            var wanted = new List<string> { MasterScenePath };
            wanted.AddRange(Segments.Select(s => s.ScenePath));

            var scenes = EditorBuildSettings.scenes.Where(s => !wanted.Contains(s.path)).ToList();
            scenes.InsertRange(0, wanted.Select(p => new EditorBuildSettingsScene(p, true)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void CreateMasterScene(CompilationPlaylist playlist)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.cullingMask = 0;
            camGo.AddComponent<AudioListener>();

            var go = new GameObject("CompilationPlayer");
            var player = go.AddComponent<CompilationPlayer>();
            player.SetPlaylist(playlist);

            if (!EditorSceneManager.SaveScene(scene, MasterScenePath))
            {
                Debug.LogError("[comp] Could not save " + MasterScenePath);
                return;
            }

            // Build Settings entry must exist now that the file does; AddToBuildSettings ran first, so re-assert.
            AddToBuildSettings();
            Debug.Log("[comp] Master scene saved: " + MasterScenePath);
        }

        static void Fail(int index, Segment s, string why)
        {
            Debug.LogError($"[comp] BUILD STOPPED at segment {index + 1}/{Segments.Length} '{s.Label}': {why}\n" +
                           $"Menu: {s.MenuPath}\nScene: {s.ScenePath}\n" +
                           "Segments before this one are built; the master scene and Build Settings were NOT updated. Fix the error, then run Build All again.");
        }
    }
}
