using System;
using System.IO;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Simulations.Survival.LavaRun;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.Survival.EditorTools
{
    /// <summary>
    /// Materials and physics materials for Lava Run. Most assets are created once and then left alone, so a hand-tuned
    /// material survives a rebuild (delete the asset to regenerate it). The course surfaces (track, basalt, ring, lip, door)
    /// are the exception: their values are re-applied on every build so a reflectivity change reaches existing assets.
    /// </summary>
    static class LavaRunMaterials
    {
        public const string Folder = "Assets/Settings/Configs/Survival/LavaRun";
        public const string MeshFolder = Folder + "/Meshes";

        /// <summary>Warm HDR orange used for lava: values above 1 are what the bloom picks up.</summary>
        public static readonly Color LavaHdr = new Color(2.2f, 0.72f, 0.14f, 1f);

        public struct Kit
        {
            public Material chrome;
            public Material track;        // slate lacquer, submesh 0 of a track body
            public Material trim;         // chrome, submesh 1 (rail caps)
            public Material basalt;
            public Material ring;
            public Material ringLip;
            public Material lavaLake;
            public Material lavaCrater;
            public Material lavaRibbon;
            public Material door;
            public Material gateGlass;
            public Material smokedGlass;
            public Material[] marbles;
            public PhysicsMaterial marblePhysics;
            public PhysicsMaterial trackPhysics;
            public PhysicsMaterial pegPhysics;
            public PhysicsMaterial ringPhysics;
        }

        public static Kit Ensure(LavaRunConfig config)
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(MeshFolder);
            Directory.CreateDirectory(StudioSetBuilder.Folder);

            // The house chrome and glass come from the shared studio kit (created there if missing).
            StudioSetBuilder.Materials studio = StudioSetBuilder.EnsureMaterials(new StudioSetBuilder.Options
            {
                hue = new Color(1f, 0.55f, 0.2f, 1f),
                glassName = "LavaRunGlass",
                subjectCenter = Vector3.zero,
                subjectHeight = 10f
            });

            Texture2D basaltAlbedo = LavaRunGeometry.EnsureBasaltAlbedo();
            Texture2D basaltEmission = LavaRunGeometry.EnsureBasaltEmission();
            Texture2D lavaNoise = LavaRunGeometry.EnsureLavaNoise();

            var kit = new Kit
            {
                chrome = studio.chrome,
                smokedGlass = studio.smokedGlass,
                trim = studio.chrome
            };

            kit.track = EnsureMaterial("LavaRunTrack", "Universal Render Pipeline/Complex Lit", m =>
            {
                // Slate-blue lacquer: lighter than the basalt around it and dark enough that the orange lava ribbon pops.
                m.SetColor("_BaseColor", new Color(0.085f, 0.10f, 0.15f, 1f));
                Satin(m, 0f, 0.35f);
            }, true);

            kit.basalt = EnsureMaterial("LavaRunBasalt", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", basaltAlbedo);
                m.SetColor("_BaseColor", Color.white);
                Satin(m, 0f, 0.3f);
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", basaltEmission);
                m.SetColor("_EmissionColor", new Color(2.4f, 2.4f, 2.4f, 1f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }, true);

            kit.ring = EnsureMaterial("LavaRunIslandRing", "Universal Render Pipeline/Complex Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.13f, 0.14f, 0.19f, 1f));
                Satin(m, 0f, 0.35f);
            }, true);

            kit.ringLip = EnsureMaterial("LavaRunIslandLip", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.95f, 0.8f, 0.55f, 1f));
                m.SetFloat("_Metallic", 0.6f);
                m.SetFloat("_Smoothness", 0.5f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.35f, 0.16f, 0.04f, 1f));
            }, true);

            kit.door = EnsureMaterial("LavaRunDoor", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.9f, 0.9f, 0.93f, 1f));
                m.SetFloat("_Metallic", 0.6f);
                m.SetFloat("_Smoothness", 0.5f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1.4f, 0.12f, 0.05f, 1f));
            }, true);

            kit.lavaLake = EnsureMaterial("LavaRunLake", "Universal Render Pipeline/Unlit", m =>
            {
                m.SetTexture("_BaseMap", lavaNoise);
                m.SetTextureScale("_BaseMap", new Vector2(110f, 110f));
                m.SetColor("_BaseColor", new Color(1.6f, 0.52f, 0.1f, 1f));
            });

            kit.lavaCrater = EnsureMaterial("LavaRunCrater", "Universal Render Pipeline/Unlit", m =>
            {
                m.SetTexture("_BaseMap", lavaNoise);
                m.SetTextureScale("_BaseMap", new Vector2(2.5f, 2.5f));
                m.SetColor("_BaseColor", new Color(2.6f, 0.9f, 0.2f, 1f));
            });

            // The presenter copies this at runtime and scrolls its _BaseMap offset. The particles-unlit shader multiplies
            // vertex colour, which is how PathRibbonView paints its brighter leading lip.
            kit.lavaRibbon = EnsureMaterial("LavaRunRibbon", "Universal Render Pipeline/Particles/Unlit", m =>
            {
                m.SetTexture("_BaseMap", lavaNoise);
                m.SetColor("_BaseColor", LavaHdr);
                m.SetFloat("_Cull", 0f);
            });

            kit.gateGlass = EnsureMaterial("LavaRunGatePanel", "Universal Render Pipeline/Unlit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.5f, 1.4f, 1.6f, 0.2f));
                MakeTransparentUnlit(m);
            });

            kit.marbles = new Material[LavaRunConfig.TeamCount];
            for (int i = 0; i < kit.marbles.Length; i++)
            {
                LavaRunTeamDef team = config.teams[i];
                Color baseColor = team.color;
                Color emission = team.color * 0.28f;
                if (i == 0)
                {
                    // Red is the colour that melts into orange lava: a hotter, lighter red with a stronger glow.
                    baseColor = Color.Lerp(team.color, new Color(1f, 0.55f, 0.58f), 0.12f);
                    emission = new Color(0.85f, 0.2f, 0.22f, 1f) * 0.6f;
                }

                kit.marbles[i] = EnsureMaterial($"LavaRunMarble_{team.name}", "Universal Render Pipeline/Complex Lit", m =>
                {
                    // A glass-lacquer bead: saturated base, mirror clear coat, a little self-light so it reads against orange.
                    m.SetColor("_BaseColor", baseColor);
                    m.SetFloat("_Metallic", 0.1f);
                    m.SetFloat("_Smoothness", 0.9f);
                    EnableClearCoat(m);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", emission);
                });
            }

            kit.marblePhysics = EnsurePhysicsMaterial("LavaRunMarble", 0.3f, 0.35f, 0.28f);
            kit.trackPhysics = EnsurePhysicsMaterial("LavaRunTrack", 0.32f, 0.4f, 0.2f);
            kit.pegPhysics = EnsurePhysicsMaterial("LavaRunPeg", 0.2f, 0.25f, 0.5f);
            kit.ringPhysics = EnsurePhysicsMaterial("LavaRunIsland", 0.35f, 0.4f, 0.1f);
            return kit;
        }

        /// <param name="reapply">Re-run <paramref name="setup"/> on an existing asset (keeps its GUID), so tuned values reach old builds.</param>
        public static Material EnsureMaterial(string name, string shaderName, Action<Material> setup, bool reapply = false)
        {
            string path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (reapply)
                {
                    setup(existing);
                    EditorUtility.SetDirty(existing);
                }

                return existing;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[surv-lava] Shader '{shaderName}' not found for {name}; falling back to URP Lit.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) { name = name };
            setup(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Satin course surface: no mirror clear coat, soft broad specular, little environment reflection.</summary>
        static void Satin(Material m, float metallic, float smoothness)
        {
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_ClearCoat"))
            {
                m.SetFloat("_ClearCoat", 0f);
                m.SetFloat("_ClearCoatMask", 0f);
                m.DisableKeyword("_CLEARCOAT");
            }

            if (m.HasProperty("_SpecularHighlights"))
            {
                m.SetFloat("_SpecularHighlights", 1f);
            }
        }

        static void EnableClearCoat(Material m)
        {
            if (m.HasProperty("_ClearCoat"))
            {
                m.SetFloat("_ClearCoat", 1f);
                m.SetFloat("_ClearCoatMask", 1f);
                m.SetFloat("_ClearCoatSmoothness", 0.97f);
                m.EnableKeyword("_CLEARCOAT");
            }
            else
            {
                m.SetFloat("_Smoothness", 0.9f);
            }
        }

        static void MakeTransparentUnlit(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        public static PhysicsMaterial EnsurePhysicsMaterial(string name, float dynamicFriction, float staticFriction, float bounciness)
        {
            string path = $"{Folder}/{name}.physicsMaterial";
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
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
