using SimulationLobby.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Capture.EditorTools
{
    /// <summary>
    /// Puts a <see cref="SeedScanner"/> on the open scene's runner, so a scan is: menu item, Play,
    /// read the console. The scan itself runs in Play mode because that is the only place the
    /// physics callbacks behave exactly as they do in a render.
    /// </summary>
    static class SeedScanMenu
    {
        const string Root = "Simulation Lobby/Seed Scan/";

        [MenuItem(Root + "Add Scanner To Open Scene")]
        static void AddScanner()
        {
            SimulationRunner runner = Object.FindAnyObjectByType<SimulationRunner>();
            if (runner == null)
            {
                EditorUtility.DisplayDialog("Seed Scan", "No SimulationRunner in the open scene.", "OK");
                return;
            }

            SeedScanner scanner = runner.GetComponent<SeedScanner>();
            if (scanner == null)
            {
                scanner = Undo.AddComponent<SeedScanner>(runner.gameObject);
            }

            Undo.RecordObject(scanner, "Configure Seed Scanner");
            scanner.runner = runner;
            scanner.scanOnPlay = true;
            scanner.profile = PickProfile(runner) ?? scanner.profile;

            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
            Selection.activeObject = scanner;

            Debug.Log(scanner.profile != null
                ? $"[SeedScan] Scanner added with profile '{scanner.profile.name}'. Press Play to scan."
                : "[SeedScan] Scanner added, but no SeedScanProfile was found — assign one, then press Play.",
                scanner);
        }

        [MenuItem(Root + "Turn Scanner Off In Open Scene")]
        static void DisableScanner()
        {
            SeedScanner scanner = Object.FindAnyObjectByType<SeedScanner>();
            if (scanner == null)
            {
                return;
            }

            Undo.RecordObject(scanner, "Turn Seed Scanner Off");
            scanner.scanOnPlay = false;
            EditorSceneManager.MarkSceneDirty(scanner.gameObject.scene);
        }

        [MenuItem(Root + "Add Scanner To Open Scene", true)]
        [MenuItem(Root + "Turn Scanner Off In Open Scene", true)]
        static bool NotPlaying()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>
        /// The profile selected in the Project window, else one whose name mentions the runner's
        /// config, else the only profile in the project.
        /// </summary>
        static SeedScanProfile PickProfile(SimulationRunner runner)
        {
            if (Selection.activeObject is SeedScanProfile selected)
            {
                return selected;
            }

            string[] guids = AssetDatabase.FindAssets("t:" + nameof(SeedScanProfile));
            SeedScanProfile only = null;
            foreach (string guid in guids)
            {
                var profile = AssetDatabase.LoadAssetAtPath<SeedScanProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null)
                {
                    continue;
                }

                if (runner.config != null && profile.name.Contains(runner.config.name))
                {
                    return profile;
                }

                only = profile;
            }

            return guids.Length == 1 ? only : null;
        }
    }
}
