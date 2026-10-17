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
    /// Builds the territory scenes, one build path and two menu items: <c>Assets/Scenes/Territory_MultiplyRelease_3D.unity</c>
    /// (terr-001, <c>Simulation Lobby ▸ 3D Formats ▸ Territory (terr) ▸ Multiply or Release</c>) and
    /// <c>Assets/Scenes/Territory_CountryWars_3D.unity</c> (terr-002, <c>... ▸ Country Wars (terr-002)</c>). A flat tile grid and
    /// four team-coloured Plinko boards flush on one table, a physical drum on a stand behind it, on a themed floor,
    /// bright and toy-like (Look v3: portrait grid, low close camera). 16:9, long form. Every colour, the floor pattern and
    /// the HUD text come from the config's <see cref="TerritoryTheme"/>; the classic theme is exactly terr-001.
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
        const string ScenePath001 = "Assets/Scenes/Territory_MultiplyRelease_3D.unity";
        const string ScenePath002 = "Assets/Scenes/Territory_CountryWars_3D.unity";
        const string ConfigFolder = "Assets/Settings/Configs/Territory";
        const string ConfigPath001 = ConfigFolder + "/TerritoryConfig_001.asset";
        const string ConfigPath002 = ConfigFolder + "/TerritoryConfig_002.asset";
        const string ThemePathCountryWars = ConfigFolder + "/TerritoryTheme_CountryWars.asset";
        const float Aspect = 16f / 9f;

        [MenuItem("Simulation Lobby/3D Formats/Territory (terr)/Multiply or Release", false, 211)]
        static void BuildMultiplyRelease()
        {
            Build(ScenePath001, ConfigPath001, false);
        }

        [MenuItem("Simulation Lobby/3D Formats/Territory (terr)/Country Wars (terr-002)", false, 212)]
        static void BuildCountryWars()
        {
            Build(ScenePath002, ConfigPath002, true);
        }

        static void Build(string scenePath, string configPath, bool countryWars)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[terr] Exit play mode before building the scene.");
                return;
            }

            Directory.CreateDirectory(ConfigFolder);
            Directory.CreateDirectory(TerritoryArenaBuilder.MaterialFolder);

            TerritoryConfig config = EnsureConfig(configPath, countryWars);
            MigrateConfig(config);
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[terr] Config '{config.name}' is invalid: {error}. Scene not built.", config);
                return;
            }

            // The theme drives every colour. A config with no theme gets the in-memory classic (terr-001) one.
            TerritoryTheme theme = config.Theme;
            Texture2D[] flags = EnsureFlags(config, theme);

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
            TerritoryArenaBuilder.BuildEnvironment(layout, pose, camera, theme);

            // --- Materials (re-applied on every build; the look lives in code). ---
            Material charcoal = TerritoryArenaBuilder.ThemedLit(theme, "Charcoal", theme.table, theme.tableSmoothness);
            Material muzzle = TerritoryArenaBuilder.ThemedLit(theme, "Muzzle", theme.muzzle, theme.tableSmoothness);
            Material chipPink = TerritoryArenaBuilder.ThemedLit(theme, "ChipPink", theme.chipMultiply, 0.5f);
            Material chipWhite = TerritoryArenaBuilder.ThemedLit(theme, "ChipWhite", theme.chipRelease, 0.5f);
            Material chipGold = TerritoryArenaBuilder.ThemedLit(theme, "ChipGold", theme.chipSpin, 0.5f);
            Material chipNeutral = TerritoryArenaBuilder.ThemedLit(theme, "ChipNeutral", theme.chipNeutral, 0.5f);
            Material tileMaterial = TerritoryArenaBuilder.ThemedLit(theme, "Tiles", Color.white, theme.tileSmoothness);
            // Shots are tinted per team by the presenter through a property block on _BaseColor.
            Material shotMaterial = TerritoryArenaBuilder.ThemedLit(theme, "Shot", Color.white, 0.6f);
            // Only themes with flags get the mast finial material, so a classic build creates no new assets.
            Material gold = theme.showFlags ? TerritoryArenaBuilder.ThemedLit(theme, "FlagGold", theme.drumArrow, 0.6f) : null;

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
                gold = gold,
                theme = theme,
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
                Texture2D flag = flags != null && team < flags.Length ? flags[team] : null;
                Color surface = def.color * 0.75f;
                surface.a = 1f;
                var look = new TerritoryBoardBuilder.TeamLook
                {
                    name = label,
                    color = def.color,
                    surface = TerritoryArenaBuilder.ThemedLit(theme, "Board_" + label, surface, 0.3f),
                    ball = TerritoryArenaBuilder.ThemedLit(theme, "Ball_" + label, def.color, 0.6f),
                    muzzle = muzzle,
                    displayName = def.name,
                    flag = flag
                };

                var boardCentre = new Vector3(left ? -layout.boardCentreX : layout.boardCentreX, 0f,
                    top ? layout.boardTopZ : layout.boardBottomZ);
                Vector2Int baseCell = config.BaseCell(team);
                var cannonBase = new Vector3(layout.gridOrigin.x + (baseCell.x + 0.5f) * config.cellSize,
                    layout.gridOrigin.y + config.tileHeight,
                    layout.gridOrigin.z + (baseCell.y + 0.5f) * config.cellSize);

                rigs[team] = TerritoryBoardBuilder.BuildRig(team, config, kit, look, boardCentre, cannonBase, boardsRoot, !left, top,
                    cameraObject.transform.forward);
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
            TerritoryHudBuilder.Hud hud = TerritoryHudBuilder.Build(camera, teams, theme);
            CaptionHud caption = hud.caption;
            ShareBarHud shareBar = hud.shareBar;
            WorldLabelLayer labels = hud.labels;

            TerritoryPresenter presenter = simulationObject.AddComponent<TerritoryPresenter>();
            presenter.Bind(gridView, shareBar, drumRig.drum, drumRig.headerAnchor, drumRig.resultAnchor, caption, labels,
                cameraRig, audioKit, shotTemplate, shotRoot, tileMaterial);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[terr] Built {scenePath} (config {config.name}, theme {(config.theme != null ? config.theme.name : "classic (in-memory)")}): {config.gridWidth}x{config.gridHeight} grid ({layout.gridWidth:0.#}m x {layout.gridDepth:0.#}m, " +
                      $"tiles {config.tileHeight:0.00}m), 4 boards of {config.boardSlots.Length} slots / {config.pegRows}x{config.pegColumns} pegs " +
                      $"(flat; gravity tilts {config.boardTiltDegrees:0} deg). Camera {pose.position} pitch {TerritoryArenaBuilder.CameraPitch:0}, " +
                      $"FOV {TerritoryArenaBuilder.CameraFov:0}: grid near edge at {pose.nearEdgeV * 100f:0.0}% of frame height, " +
                      $"stand top at {pose.drumTopV * 100f:0.0}%, side boards crop {pose.boardCrop * 100f:0.0}% of their width. " +
                      "Random seed each Play (logged).");
        }

        // ------------------------------------------------------------------ assets

        /// <summary>
        /// Loads the config at <paramref name="path"/>, or creates it. A new terr-002 config gets the Country Wars teams,
        /// reel and theme; an existing config is never touched here.
        /// </summary>
        static TerritoryConfig EnsureConfig(string path, bool countryWars)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TerritoryConfig>(path);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<TerritoryConfig>();
            config.ResetLayoutToDefaults();
            if (countryWars)
            {
                config.teams = TerritoryConfig.CountryWarsTeams();
                config.spinFaces = TerritoryConfig.CountryWarsSpinFaces();
                config.theme = EnsureCountryWarsTheme();
                // Pacing (playtests 2026-10-15, "very slow"): every multiplier +1 each 10 min, and the SPIN lane open 2 s / shut 5 s,
                // open 2 s longer per team eliminated (2 s -> 4 s -> 6 s) so the endgame spins more.
                config.multiplierRampSeconds = 600f;
                config.multiplierRampStep = 1;
                config.laneGateOpenSeconds = 2f;
                config.laneGateClosedSeconds = 5f;
                config.laneGateOpenPerElimination = 2f;
                // Target = the cannon's whole base plate (1.4 m disc), not the single cell under it.
                config.basePlateRadiusCells = 2.1f;
                // Progression (playtest 2026-10-15, "takes very long"): every knock-out and the clock speed the game up.
                config.knockoutReward = true;                 // taker gets the loser's bank + a free spin
                config.cannonSpeedupPerElimination = 0.25f;   // x1.25, x1.5, x1.75 sweep and fire rate
                config.showdownPlateRadiusCells = 3.5f;       // two left: plates grow to 2.3 m, SPIN lanes stay open
                config.escalateAfterSeconds = 210f;           // ball drops start speeding up at 3:30 (was 6:00)
                config.heavyShotsAfterSeconds = 600f;         // from 10:00 every hit splashes radius 1
                config.heavyShotRadius = 1;
                config.intent = "terr-002 Country Wars: India, South Africa, Australia and West Indies fight for the board. " +
                                "x2 doubles the bank, R fires it, SPIN drives the reel; BOOM knocks a cannon out for 10 s and " +
                                "RAIN paints a patch of cells. Tune for lead changes first, length second.";
            }
            else
            {
                config.intent = "terr-001: four colours, four cannons, four Plinko boards. x2 doubles the bank, R fires it, " +
                                "a SPIN slot on each board drives a power-face reel. Tune for lead changes first, length second.";
            }

            AssetDatabase.CreateAsset(config, path);
            Debug.Log($"[terr] Created config {path}.", config);
            return config;
        }

        /// <summary>The Country Wars theme asset: created with <see cref="TerritoryTheme.ApplyCountryWars"/> if missing, never overwritten after.</summary>
        static TerritoryTheme EnsureCountryWarsTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<TerritoryTheme>(ThemePathCountryWars);
            if (theme != null)
            {
                return theme;
            }

            theme = ScriptableObject.CreateInstance<TerritoryTheme>();
            theme.ApplyCountryWars();
            AssetDatabase.CreateAsset(theme, ThemePathCountryWars);
            Debug.Log($"[terr] Created theme {ThemePathCountryWars}.", theme);
            return theme;
        }

        /// <summary>
        /// Paints the country flags (code is the source of truth, so they are rewritten on every build) and assigns them to
        /// the theme asset. Only for a saved theme that uses them; the in-memory classic theme never gets flags.
        /// </summary>
        static Texture2D[] EnsureFlags(TerritoryConfig config, TerritoryTheme theme)
        {
            if (config.theme == null || !(theme.showFlags || theme.boardNameDecals))
            {
                return null;
            }

            Texture2D[] flags = CountryFlags.BuildAll(config.teams);
            var four = new Texture2D[4];
            for (int i = 0; i < four.Length && i < flags.Length; i++)
            {
                four[i] = flags[i];
                if (flags[i] == null)
                {
                    Debug.LogWarning($"[terr] No flag for team '{config.teams[i].name}': the board and cannon go without one.");
                }
            }

            theme.flags = four;
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log($"[terr] Flags painted into {CountryFlags.Folder}: {System.Array.FindAll(four, f => f != null).Length} of 4 assigned to theme '{theme.name}'.");
            return four;
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

        /// <summary>"GREEN" -> "Green"; "SOUTH AFRICA" -> "SouthAfrica" (no spaces in asset or object names).</summary>
        static string TitleCase(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Team";
            }

            var result = new System.Text.StringBuilder();
            foreach (string word in name.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                result.Append(char.ToUpperInvariant(word[0])).Append(word.Substring(1).ToLowerInvariant());
            }

            return result.ToString();
        }
    }
}
