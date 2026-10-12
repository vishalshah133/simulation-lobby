using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.Territory.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Territory_MultiplyRelease_3D.unity</c>: a flat tile grid and four team-coloured
    /// Plinko boards flush on one charcoal table, a physical drum on a stand behind it, on a floor of raised
    /// purple slabs, bright and toy-like (Look v3: portrait grid, low close camera). 16:9, long form. Menu: <c>Simulation Lobby ▸ Build Scene ▸ Territory_MultiplyRelease_3D</c>.
    /// </summary>
    /// <remarks>
    /// Every dimension comes from <see cref="TerritoryConfig"/>, so changing the grid or board geometry means
    /// rebuilding the scene. Re-running replaces the scene; the config is only ever
    /// touched to migrate an older layout (<see cref="TerritoryConfig.layoutVersion"/>), and that is logged. Everything is a primitive: there
    /// are no generated meshes to go stale. The table and environment live in <see cref="TerritoryArenaBuilder"/>,
    /// the boards and cannons in <see cref="TerritoryBoardBuilder"/>.
    /// </remarks>
    static class TerritorySceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Territory_MultiplyRelease_3D.unity";
        const string ConfigFolder = "Assets/Settings/Configs/Territory";
        const string ConfigPath = ConfigFolder + "/TerritoryConfig_001.asset";
        const float Aspect = 16f / 9f;

        [MenuItem("Simulation Lobby/Build Scene/Territory_MultiplyRelease_3D")]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[terr] Exit play mode before building the scene.");
                return;
            }

            Directory.CreateDirectory(ConfigFolder);
            Directory.CreateDirectory(TerritoryArenaBuilder.MaterialFolder);

            TerritoryConfig config = EnsureConfig();
            MigrateConfig(config);
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[terr] Config '{config.name}' is invalid: {error}. Scene not built.", config);
                return;
            }

            PhysicsMaterial ballPhysics = EnsurePhysicsMaterial("TerritoryBall", 0.12f, 0.15f, PhysicsMaterialCombine.Average, 0.55f);
            PhysicsMaterial pegPhysics = EnsurePhysicsMaterial("TerritoryPeg", 0.1f, 0.12f, PhysicsMaterialCombine.Average, 0.45f);
            PhysicsMaterial boardPhysics = EnsurePhysicsMaterial("TerritoryBoard", 0.14f, 0.2f, PhysicsMaterialCombine.Average, 0.1f);

            // --- Layout, all derived from the config. ---
            TerritoryArenaBuilder.Layout layout = TerritoryArenaBuilder.ComputeLayout(config);
            TerritoryArenaBuilder.CameraPose pose = TerritoryArenaBuilder.FitCamera(layout, Aspect);
            if (config.useCameraOverride)
            {
                // The hand-framed pose wins over the fit. lookAt is a point down the view ray, which is all the
                // rig and the environment need from it.
                // Fog is keyed off the distance to the table centre (the fit's lookAt), so keep that, not the 20 m ray.
                pose.distance = Vector3.Distance(config.cameraPosition, pose.lookAt);
                pose.position = config.cameraPosition;
                Quaternion rotation = config.cameraRotation.normalized;
                pose.forward = rotation * Vector3.forward;
                pose.up = rotation * Vector3.up;
                pose.lookAt = config.cameraPosition + pose.forward * 20f;
                Debug.Log($"[terr] Camera: using the config's hand-framed pose {config.cameraPosition} " +
                          $"(pitch {config.cameraRotation.eulerAngles.x:0.#}°). Untick useCameraOverride to auto-fit.");
            }
            else if (pose.boardCrop > 0.15f)
            {
                Debug.LogWarning($"[terr] The side boards crop {pose.boardCrop * 100f:0}% of their width at the frame edge (target <= 15%).");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera. ---
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = TerritoryArenaBuilder.CameraFov;
            camera.aspect = Aspect;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 300f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = pose.position;
            cameraObject.transform.LookAt(pose.lookAt);
            FocusCameraRig cameraRig = cameraObject.AddComponent<FocusCameraRig>();
            cameraRig.Bind(camera);
            cameraRig.SetHome(pose.position, pose.lookAt);

            // --- Environment: floor, fog, key light, ambient, post. ---
            TerritoryArenaBuilder.BuildEnvironment(layout, pose, camera);

            // --- Materials (re-applied on every build; the look lives in code). ---
            Material charcoal = TerritoryArenaBuilder.LitMaterial("TerritoryCharcoal", TerritoryArenaBuilder.Hex(0x22, 0x23, 0x2B), 0.4f);
            Material muzzle = TerritoryArenaBuilder.LitMaterial("TerritoryMuzzle", TerritoryArenaBuilder.Hex(0x10, 0x11, 0x16), 0.4f);
            Material chipPink = TerritoryArenaBuilder.LitMaterial("TerritoryChipPink", TerritoryArenaBuilder.Hex(0xFF, 0x4F, 0xA0), 0.5f);
            Material chipWhite = TerritoryArenaBuilder.LitMaterial("TerritoryChipWhite", TerritoryArenaBuilder.Hex(0xF4, 0xF4, 0xF4), 0.5f);
            Material chipGold = TerritoryArenaBuilder.LitMaterial("TerritoryChipGold", TerritoryArenaBuilder.Hex(0xFF, 0xC9, 0x3A), 0.5f);
            Material chipNeutral = TerritoryArenaBuilder.LitMaterial("TerritoryChipNeutral", TerritoryArenaBuilder.Hex(0x8A, 0x8D, 0x9A), 0.5f);
            Material tileMaterial = TerritoryArenaBuilder.LitMaterial("TerritoryTiles", Color.white, 0.45f);
            // Shots are tinted per team by the presenter through a property block on _BaseColor.
            Material shotMaterial = TerritoryArenaBuilder.LitMaterial("TerritoryShot", Color.white, 0.6f);

            // --- Table and grid. ---
            TerritoryArenaBuilder.BuildTable(layout, charcoal);
            TerritoryArenaBuilder.BuildGridFrame(layout, charcoal);

            var gridOriginTransform = new GameObject("Grid Origin").transform;
            gridOriginTransform.position = layout.gridOrigin;

            var tilesObject = new GameObject("Tile Grid");
            tilesObject.transform.SetParent(gridOriginTransform, false);
            tilesObject.AddComponent<MeshFilter>();
            tilesObject.AddComponent<MeshRenderer>();
            TileGridView gridView = tilesObject.AddComponent<TileGridView>();

            // --- Four team rigs. Quadrant order: 0 GREEN top-left, 1 YELLOW top-right, 2 BLUE bottom-left, 3 RED bottom-right. ---
            TerritoryTeamDef[] teams = config.teams;
            var boardsRoot = new GameObject("Boards").transform;
            var rigs = new TerritoryTeamRig[4];
            var kit = new TerritoryBoardBuilder.Kit
            {
                charcoal = charcoal,
                chipPink = chipPink,
                chipWhite = chipWhite,
                chipGold = chipGold,
                chipNeutral = chipNeutral,
                boardPhysics = boardPhysics,
                pegPhysics = pegPhysics,
                ballPhysics = ballPhysics
            };

            for (int team = 0; team < 4; team++)
            {
                bool left = team == 0 || team == 2;
                bool top = team < 2;
                TerritoryTeamDef def = teams[team];
                string label = TitleCase(def.name);
                Color surface = def.color * 0.75f;
                surface.a = 1f;
                var look = new TerritoryBoardBuilder.TeamLook
                {
                    name = label,
                    color = def.color,
                    surface = TerritoryArenaBuilder.LitMaterial("TerritoryBoard_" + label, surface, 0.3f),
                    ball = TerritoryArenaBuilder.LitMaterial("TerritoryBall_" + label, def.color, 0.6f),
                    muzzle = muzzle
                };

                var boardCentre = new Vector3(left ? -layout.boardCentreX : layout.boardCentreX, 0f,
                    top ? layout.boardTopZ : layout.boardBottomZ);
                Vector2Int baseCell = config.BaseCell(team);
                var cannonBase = new Vector3(layout.gridOrigin.x + (baseCell.x + 0.5f) * config.cellSize,
                    layout.gridOrigin.y + config.tileHeight,
                    layout.gridOrigin.z + (baseCell.y + 0.5f) * config.cellSize);

                rigs[team] = TerritoryBoardBuilder.BuildRig(team, config, kit, look, boardCentre, cannonBase, boardsRoot, !left);
            }

            // --- The drum, its stand, arrow and label anchors behind the grid's far edge. ---
            TerritoryArenaBuilder.DrumRig drumRig = TerritoryArenaBuilder.BuildDrum(layout, config, charcoal);

            // --- Shots: a Shot Root and an inactive glossy sphere the presenter clones. ---
            var shotRoot = new GameObject("Shot Root").transform;
            var shotHolder = new GameObject("Shot Template Holder").transform;
            GameObject shotTemplate = TerritoryBoardBuilder.Prim(PrimitiveType.Sphere, "Shot Template", shotHolder,
                Vector3.zero, Vector3.one, shotMaterial, false, null);
            shotTemplate.SetActive(false);

            // --- Simulation, audio, presenter. Random seed per Play, logged by the runner. ---
            var simulationObject = new GameObject("Simulation");
            TerritorySimulation simulation = simulationObject.AddComponent<TerritorySimulation>();
            var runner = simulationObject.GetComponent<SimulationRunner>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;
            simulation.Bind(rigs, gridOriginTransform);

            var audioObject = new GameObject("Audio");
            TerritoryAudioKit audioKit = audioObject.AddComponent<TerritoryAudioKit>();

            // --- HUD: built by TerritoryHudBuilder (screen-space canvas + world labels). ---
            TerritoryHudBuilder.Hud hud = TerritoryHudBuilder.Build(camera, teams);
            CaptionHud caption = hud.caption;
            ShareBarHud shareBar = hud.shareBar;
            WorldLabelLayer labels = hud.labels;

            TerritoryPresenter presenter = simulationObject.AddComponent<TerritoryPresenter>();
            presenter.Bind(gridView, shareBar, drumRig.drum, drumRig.headerAnchor, drumRig.resultAnchor, caption, labels,
                cameraRig, audioKit, shotTemplate, shotRoot, tileMaterial);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[terr] Built {ScenePath}: {config.gridWidth}x{config.gridHeight} grid ({layout.gridWidth:0.#}m x {layout.gridDepth:0.#}m, " +
                      $"tiles {config.tileHeight:0.00}m), 4 boards of {config.boardSlots.Length} slots / {config.pegRows}x{config.pegColumns} pegs " +
                      $"(flat; gravity tilts {config.boardTiltDegrees:0} deg). Camera {pose.position} pitch {TerritoryArenaBuilder.CameraPitch:0}, " +
                      $"FOV {TerritoryArenaBuilder.CameraFov:0}: grid near edge at {pose.nearEdgeV * 100f:0.0}% of frame height, " +
                      $"stand top at {pose.drumTopV * 100f:0.0}%, side boards crop {pose.boardCrop * 100f:0.0}% of their width. " +
                      "Random seed each Play (logged).");
        }

        // ------------------------------------------------------------------ assets

        static TerritoryConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TerritoryConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<TerritoryConfig>();
            config.ResetLayoutToDefaults();
            config.intent = "terr-001: four colours, four cannons, four Plinko boards. x2 doubles the bank, R fires it, " +
                            "a SPIN slot on each board drives a power-face reel. Tune for lead changes first, length second.";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        /// <summary>
        /// The only write to an existing config: an asset serialized before the current layout generation gets its
        /// layout fields (grid, board, pegs, slots, tiles, spinner) reset to the new defaults. Logged.
        /// </summary>
        static void MigrateConfig(TerritoryConfig config)
        {
            if (config.layoutVersion >= TerritoryConfig.CurrentLayoutVersion)
            {
                return;
            }

            int from = config.layoutVersion;
            config.ResetLayoutToDefaults();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[terr] Migrated '{config.name}' layout v{from} -> v{config.layoutVersion}: grid {config.gridWidth}x{config.gridHeight}, " +
                      $"board {config.boardWidth:0.#}x{config.boardLength:0.#}, {config.pegRows}x{config.pegColumns} pegs, " +
                      $"{config.boardSlots.Length} slots, tile height {config.tileHeight:0.00}. Non-layout fields untouched.", config);
        }

        static PhysicsMaterial EnsurePhysicsMaterial(string name, float dynamicFriction, float staticFriction,
            PhysicsMaterialCombine combine, float bounciness)
        {
            string path = $"{ConfigFolder}/{name}.physicsMaterial";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new PhysicsMaterial(name)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                frictionCombine = combine,
                bounceCombine = combine
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static string TitleCase(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Team";
            }

            return char.ToUpperInvariant(name[0]) + name.Substring(1).ToLowerInvariant();
        }
    }
}
