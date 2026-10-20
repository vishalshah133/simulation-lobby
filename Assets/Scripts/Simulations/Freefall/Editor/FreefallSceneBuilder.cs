using System.Collections.Generic;
using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.Freefall.EditorTools
{
    /// <summary>
    /// Builds the <c>fall</c> scenes: <c>Freefall_Symphony_3D</c> (fall-001, 16:9, seven worlds) and
    /// <c>Freefall_MountainKing_3D</c> (fall-002, 9:16, one world). Re-running replaces the scene and keeps an
    /// existing config asset (hand tuning survives); <b>Reset Configs</b> rewrites both to the plan's baseline.
    /// The Game view size is not set from here: use 1920x1080 for 001 and 1080x1920 for 002.
    /// </summary>
    static class FreefallSceneBuilder
    {
        const string SymphonyScene = "Assets/Scenes/Freefall_Symphony_3D.unity";
        const string ShortScene = "Assets/Scenes/Freefall_MountainKing_3D.unity";
        const string ConfigFolder = "Assets/Settings/Configs/Freefall";
        const string SymphonyConfig = ConfigFolder + "/FreefallConfig_001.asset";
        const string ShortConfig = ConfigFolder + "/FreefallConfig_002.asset";
        const string MaterialFolder = "Assets/Settings/Freefall";
        const string MelodyFolder = "Assets/Settings/Melodies";

        [MenuItem("Simulation Lobby/3D Formats/Freefall (fall)/Freefall Symphony (fall-001)", false, 281)]
        static void BuildSymphony()
        {
            Build(SymphonyScene, SymphonyConfig, false);
        }

        [MenuItem("Simulation Lobby/3D Formats/Freefall (fall)/Mountain King Short (fall-002)", false, 282)]
        static void BuildShort()
        {
            Build(ShortScene, ShortConfig, true);
        }

        [MenuItem("Simulation Lobby/3D Formats/Freefall (fall)/Reset Configs (tuning baseline)", false, 301)]
        static void ResetConfigs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[fall] Exit play mode before resetting configs.");
                return;
            }

            FreefallConfig a = EnsureConfig(SymphonyConfig, false, true);
            FreefallConfig b = EnsureConfig(ShortConfig, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[fall] Reset to the tuning baseline:\n  {SymphonyConfig} ({a.worlds.Count} worlds)\n  {ShortConfig} ({b.worlds.Count} world)");
        }

        // ---------------------------------------------------------------- build

        static void Build(string scenePath, string configPath, bool isShort)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[fall] Exit play mode before building the scene.");
                return;
            }

            FreefallConfig config = EnsureConfig(configPath, isShort, false);
            if (!config.Validate(out string configError))
            {
                Debug.LogWarning($"[fall] Config {configPath} does not validate yet: {configError}");
            }

            FreefallMaterials materials = EnsureMaterials();
            VolumeProfile post = EnsurePostProfile();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera: HDR, URP post, SMAA, the listener. The presenter drives position, fov, background and fog.
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 4f, -4f);
            cameraObject.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = config.worlds.Count > 0 ? config.worlds[0].skyTop : Color.black;
            camera.allowHDR = true;
            camera.fieldOfView = 68f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 2000f;
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;

            // --- Post: bloom so the HDR emissives glow, a light vignette.
            var volumeObject = new GameObject("Freefall Post");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = post;

            // --- Light: a cool key, a warm rim, dim flat ambient. Depth comes from fog (the presenter drives it).
            CreateLight("Key Light", new Color(0.72f, 0.8f, 1f), 0.55f, Quaternion.Euler(55f, -30f, 0f));
            CreateLight("Rim Light", new Color(1f, 0.82f, 0.7f), 0.35f, Quaternion.Euler(-30f, 160f, 0f));
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.08f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 4f;
            RenderSettings.fogEndDistance = 28f;

            // --- Runner + simulation + presenter + audio, all on one object. Runner first (DisallowMultiple).
            var simulationObject = new GameObject("Simulation");
            SimulationRunner runner = simulationObject.AddComponent<SimulationRunner>();
            FreefallSimulation simulation = simulationObject.AddComponent<FreefallSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            FreefallPresenter presenter = simulationObject.AddComponent<FreefallPresenter>();
            simulationObject.AddComponent<FreefallAudio>();

            // --- HUD: caption canvas, progress dots in the footer, the white-out overlay on top of it all.
            CaptionHud hud = CaptionCanvasBuilder.Build("EVERY BOUNCE IS A NOTE", string.Empty, string.Empty,
                                                        CaptionCanvasBuilder.Style.Dark);
            StyleCaptionCanvas(hud, isShort);
            CanvasGroup whiteOut = BuildWhiteOut(hud.transform);

            // Bind is serialized into the scene here. FreefallAudio is bound at runtime by the presenter's Start
            // (it needs the schedule, which exists only after the runner has begun).
            presenter.Bind(simulation, hud, camera);
            presenter.SetRig(materials, whiteOut);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);

            string aspect = isShort ? "9:16 (1080x1920)" : "16:9 (1920x1080)";
            Debug.Log(
                $"Built {scenePath}\n" +
                $"  config {configPath}: {config.worlds.Count} world(s)\n" +
                $"  materials {MaterialFolder}\n" +
                $"  Set the Game view to {aspect} before Play or recording. Seed is random per Play and logged at start (cosmetic only).\n" +
                "  FreefallAudio is bound to the simulation at runtime by the presenter.");
        }

        static void CreateLight(string name, Color colour, float intensity, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.rotation = rotation;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = colour;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        // ---------------------------------------------------------------- HUD

        static void StyleCaptionCanvas(CaptionHud hud, bool isShort)
        {
            foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                switch (text.name)
                {
                    case "Caption":
                        text.fontSize = isShort ? 68f : 100f;
                        text.characterSpacing = 3f;
                        text.textWrappingMode = TextWrappingModes.Normal;
                        text.richText = true;
                        break;
                    case "Subline":
                        text.fontSize = isShort ? 46f : 56f;
                        text.richText = true;
                        break;
                    case "Footer":
                        text.fontSize = 64f;
                        text.fontStyle = FontStyles.Bold;
                        text.alignment = TextAlignmentOptions.Center;
                        text.textWrappingMode = TextWrappingModes.NoWrap;
                        text.richText = true;
                        break;
                }
            }
        }

        static CanvasGroup BuildWhiteOut(Transform canvas)
        {
            var go = new GameObject("White Out", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            CanvasGroup group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            go.transform.SetAsLastSibling();
            return group;
        }

        // ---------------------------------------------------------------- post

        static VolumeProfile EnsurePostProfile()
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = MaterialFolder + "/FreefallPost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);

                Tonemapping tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.ACES);
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1.15f);
                bloom.intensity.Override(0.75f);
                bloom.scatter.Override(0.7f);
                Vignette vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.26f);
                vignette.smoothness.Override(0.5f);
                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.saturation.Override(8f);
                color.contrast.Override(6f);

                foreach (VolumeComponent component in profile.components)
                {
                    component.name = component.GetType().Name;
                    AssetDatabase.AddObjectToAsset(component, profile);
                }

                EditorUtility.SetDirty(profile);
            }

            return profile;
        }

        // ---------------------------------------------------------------- materials

        static FreefallMaterials EnsureMaterials()
        {
            Directory.CreateDirectory(MaterialFolder);
            Texture2D softDot = GeneratedArt.SoftDot();

            var m = new FreefallMaterials();

            m.padOpaque = EnsureMaterial("FallPadOpaque", "Universal Render Pipeline/Lit", mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.12f, 1f));
                mat.SetFloat("_Metallic", 0.7f);
                mat.SetFloat("_Smoothness", 0.88f);
                EnableEmission(mat);
            });

            m.padGlass = EnsureMaterial("FallPadGlass", "Universal Render Pipeline/Lit", mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.4f, 0.5f, 0.6f, 0.55f));
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.96f);
                EnableEmission(mat);
                MakeTransparent(mat, BaseShaderGUI.BlendMode.Alpha, false);
            });

            m.glowFlat = EnsureMaterial("FallGlowFlat", "Universal Render Pipeline/Particles/Unlit", mat =>
            {
                mat.SetColor("_BaseColor", Color.white);
                MakeTransparent(mat, BaseShaderGUI.BlendMode.Additive, false);
                mat.SetFloat("_Cull", 0f);
            });

            m.glowSoft = EnsureMaterial("FallGlowSoft", "Universal Render Pipeline/Particles/Unlit", mat =>
            {
                mat.SetTexture("_BaseMap", softDot);
                mat.SetColor("_BaseColor", Color.white);
                MakeTransparent(mat, BaseShaderGUI.BlendMode.Additive, false);
                mat.SetFloat("_Cull", 0f);
            });

            m.marbleGlass = EnsureMaterial("FallMarbleGlass", "Universal Render Pipeline/Lit", mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.82f, 0.92f, 1f, 0.2f));
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 1f);
                MakeTransparent(mat, BaseShaderGUI.BlendMode.Alpha, true);
            });

            m.marbleCore = EnsureMaterial("FallMarbleCore", "Universal Render Pipeline/Unlit", mat =>
            {
                mat.SetColor("_BaseColor", Color.white);
            });

            m.crystal = EnsureMaterial("FallCrystal", "Universal Render Pipeline/Lit", mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.3f, 0.25f, 0.45f, 1f));
                mat.SetFloat("_Metallic", 0.15f);
                mat.SetFloat("_Smoothness", 0.94f);
                EnableEmission(mat);
            });

            m.coreSphere = EnsureMaterial("FallCore", "Universal Render Pipeline/Unlit", mat =>
            {
                mat.SetColor("_BaseColor", new Color(3.5f, 3.5f, 3.8f, 1f));
            });

            AssetDatabase.SaveAssets();
            return m;
        }

        static void EnableEmission(Material material)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        /// <summary>URP's own blend setup, so every keyword and blend state matches what the Inspector would write.</summary>
        static void MakeTransparent(Material material, BaseShaderGUI.BlendMode blend, bool preserveSpecular)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", (float)blend);
            material.SetFloat("_BlendModePreserveSpecular", preserveSpecular ? 1f : 0f);
            BaseShaderGUI.SetupMaterialBlendMode(material);
        }

        /// <summary>
        /// Creates the material if missing; the setup is re-applied on every build so the look the presenter
        /// relies on (additive, double sided, emission keyword) cannot drift. Delete the asset to regenerate.
        /// </summary>
        static Material EnsureMaterial(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[fall] Shader '{shaderName}' not found; falling back to URP Lit for {name}.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            setup(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------------------------------------------------------- config

        sealed class WorldSpec
        {
            public string name;
            public string melody;
            public string composer;
            public float bpm;
            public float bpmEnd;
            public int passes;
            public float gravity;
            public ProceduralInstrument.Timbre timbre;
            public PadShape pad;
            public WallStyle wall;
            public float hue;
            public float span;
            public string top;
            public string bottom;
            public float padRadius = 0.32f;
            public float roll = 4f;
            public float degreesPerSecond = 40f;
            public int canonVoices = 1;
        }

        static readonly WorldSpec[] SymphonyWorlds =
        {
            new WorldSpec { name = "GLASS BELLS", melody = "FurElise", composer = "BEETHOVEN", bpm = 140f, bpmEnd = 140f, passes = 3, gravity = 9.81f,
                            timbre = ProceduralInstrument.Timbre.GlassBell, pad = PadShape.Disc, wall = WallStyle.RingStack, hue = 0.60f, span = 0.15f,
                            top = "#0B0B2E", bottom = "#000000" },
            new WorldSpec { name = "AURORA", melody = "MorningMood", composer = "GRIEG", bpm = 96f, bpmEnd = 96f, passes = 3, gravity = 7f,
                            timbre = ProceduralInstrument.Timbre.Flute, pad = PadShape.Petal, wall = WallStyle.None, hue = 0.38f, span = 0.22f,
                            top = "#0B2A30", bottom = "#1E1508" },
            new WorldSpec { name = "MOON POOL", melody = "GymnopedieNo1", composer = "SATIE", bpm = 80f, bpmEnd = 80f, passes = 3, gravity = 0.8f,
                            timbre = ProceduralInstrument.Timbre.SoftPiano, pad = PadShape.Lily, wall = WallStyle.Mist, hue = 0.55f, span = 0.10f,
                            top = "#10203A", bottom = "#04070F", padRadius = 0.4f },
            new WorldSpec { name = "CRYSTAL CAVE", melody = "OdeToJoy", composer = "BEETHOVEN", bpm = 100f, bpmEnd = 100f, passes = 2, gravity = 9.81f,
                            timbre = ProceduralInstrument.Timbre.Celesta, pad = PadShape.Crystal, wall = WallStyle.CrystalShards, hue = 0.80f, span = 0.18f,
                            top = "#1A0830", bottom = "#050010" },
            new WorldSpec { name = "KALEIDOSCOPE", melody = "CanonInD", composer = "PACHELBEL", bpm = 72f, bpmEnd = 72f, passes = 3, gravity = 6f,
                            timbre = ProceduralInstrument.Timbre.Harp, pad = PadShape.Petal, wall = WallStyle.Mandala, hue = 0f, span = 1f,
                            top = "#160A22", bottom = "#02040A", roll = 6f, canonVoices = 3 },
            new WorldSpec { name = "ECLIPSE", melody = "MoonlightSonata", composer = "BEETHOVEN", bpm = 60f, bpmEnd = 60f, passes = 4, gravity = 5f,
                            timbre = ProceduralInstrument.Timbre.SoftPiano, pad = PadShape.Crescent, wall = WallStyle.Starfield, hue = 0.62f, span = 0.05f,
                            top = "#05060C", bottom = "#000000", padRadius = 0.34f, roll = 2f },
            new WorldSpec { name = "SINGULARITY", melody = "HallOfTheMountainKing", composer = "GRIEG", bpm = 80f, bpmEnd = 190f, passes = 2, gravity = 9.81f,
                            timbre = ProceduralInstrument.Timbre.Mallet, pad = PadShape.Hex, wall = WallStyle.Vortex, hue = 0.02f, span = 0.10f,
                            top = "#2A0600", bottom = "#000000", padRadius = 0.3f, roll = 14f, degreesPerSecond = 70f }
        };

        static readonly WorldSpec ShortWorld = new WorldSpec
        {
            name = "SINGULARITY", melody = "HallOfTheMountainKing", composer = "GRIEG", bpm = 96f, bpmEnd = 200f, passes = 2, gravity = 9.81f,
            timbre = ProceduralInstrument.Timbre.Mallet, pad = PadShape.Hex, wall = WallStyle.Vortex, hue = 0.02f, span = 0.10f,
            top = "#2A0600", bottom = "#000000", padRadius = 0.3f, roll = 14f, degreesPerSecond = 70f
        };

        /// <summary>Loads the config, creating it (or, with <paramref name="reset"/>, rewriting it) from the plan's table.</summary>
        static FreefallConfig EnsureConfig(string path, bool isShort, bool reset)
        {
            Directory.CreateDirectory(ConfigFolder);
            var config = AssetDatabase.LoadAssetAtPath<FreefallConfig>(path);
            bool created = config == null;
            if (created)
            {
                config = ScriptableObject.CreateInstance<FreefallConfig>();
            }

            if (created || reset)
            {
                FillConfig(config, isShort);
            }

            if (created)
            {
                AssetDatabase.CreateAsset(config, path);
            }
            else
            {
                EditorUtility.SetDirty(config);
            }

            return config;
        }

        static void FillConfig(FreefallConfig config, bool isShort)
        {
            config.fixedTimestep = 1f / 240f;
            config.maxDurationSeconds = isShort ? 300f : 900f;
            config.introDropSeconds = 1.2f;
            config.transitionSeconds = 4f;
            config.worldGap = 6f;
            config.outroSeconds = 5f;
            config.marbleRadius = 0.12f;
            config.padLeadSeconds = 0.9f;
            config.intent = isShort
                ? "fall-002 Mountain King. The last world of Freefall Symphony on its own: an accelerando from 96 to 200 bpm, 9:16. Seed is cosmetic only."
                : "fall-001 Freefall Symphony. One marble falls through seven worlds and every bounce plays the next note of a famous song. " +
                  "Pads sit where the marble will be when the note is due; world 5 splits into a three-voice canon; world 7 accelerates. Seed is cosmetic only.";

            config.worlds = new List<FreefallWorld>();
            if (isShort)
            {
                config.worlds.Add(MakeWorld(ShortWorld));
            }
            else
            {
                for (int i = 0; i < SymphonyWorlds.Length; i++)
                {
                    config.worlds.Add(MakeWorld(SymphonyWorlds[i]));
                }
            }
        }

        static FreefallWorld MakeWorld(WorldSpec spec)
        {
            string melodyPath = $"{MelodyFolder}/{spec.melody}.asset";
            var melody = AssetDatabase.LoadAssetAtPath<MelodySequence>(melodyPath);
            if (melody == null)
            {
                Debug.LogError($"[fall] Missing melody asset '{melodyPath}' for world {spec.name}. The config will not validate until it exists.");
            }

            Color top = Parse(spec.top);
            Color bottom = Parse(spec.bottom);
            return new FreefallWorld
            {
                worldName = spec.name,
                melody = melody,
                composer = spec.composer,
                bpm = spec.bpm,
                bpmEnd = spec.bpmEnd,
                passes = spec.passes,
                gravity = spec.gravity,
                degreesPerSecond = spec.degreesPerSecond,
                canonVoices = spec.canonVoices,
                canonDelayBeats = 8f,
                padShape = spec.pad,
                wallStyle = spec.wall,
                timbre = spec.timbre,
                skyTop = top,
                skyBottom = bottom,
                fogColor = Color.Lerp(top, bottom, 0.5f),
                wallColor = Color.HSVToRGB(Mathf.Repeat(spec.hue + spec.span * 0.5f, 1f), 0.55f, 0.95f),
                hueStart = spec.hue,
                hueSpan = spec.span,
                padRadius = spec.padRadius,
                cameraRollDegreesPerSecond = spec.roll
            };
        }

        static Color Parse(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out Color c) ? c : Color.black;
        }
    }
}
