using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>Named colours shared by every set so the ride reads as one look. Bright scare colours against deep indigo.</summary>
    public static class CoasterPalette
    {
        public static readonly Color Pumpkin = Hex(0xFF, 0x7A, 0x18);
        public static readonly Color Bone = Hex(0xED, 0xE6, 0xD0);
        public static readonly Color ToxicGreen = Hex(0x7C, 0xFF, 0x4F);
        public static readonly Color BloodRed = Hex(0xC8, 0x10, 0x18);
        public static readonly Color Indigo = Hex(0x0B, 0x0B, 0x24);
        public static readonly Color IndigoLight = Hex(0x1C, 0x1C, 0x4A);
        public static readonly Color Lantern = Hex(0xFF, 0xC8, 0x5A);
        public static readonly Color Steel = Hex(0x2A, 0x2C, 0x38);
        public static readonly Color Night = Hex(0x07, 0x07, 0x14);
        public static readonly Color Wood = Hex(0x2B, 0x1B, 0x14);
        public static readonly Color Moon = Hex(0x9C, 0xB4, 0xFF);

        /// <summary>HDR emission: <paramref name="c"/> scaled by <paramref name="intensity"/> (alpha stays 1).</summary>
        public static Color Glow(Color c, float intensity)
        {
            return new Color(c.r * intensity, c.g * intensity, c.b * intensity, 1f);
        }

        static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }

    /// <summary>
    /// URP Lit / Unlit materials created on demand and saved as assets under
    /// <c>Assets/Art/Generated/HorrorCoaster/Materials</c>. Every call re-applies the properties, so a rebuild
    /// always reflects the code (delete the asset to regenerate from scratch).
    /// </summary>
    public static class CoasterMaterials
    {
        public const string Folder = "Assets/Art/Generated/HorrorCoaster/Materials";
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static Material Get(string key, Color baseColor, Color emission = default, float smoothness = 0.3f,
                                   float metallic = 0f, bool transparent = false)
        {
            Material m = Fetch(key, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.maxColorComponent > 0.0001f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }

            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", (float)BaseShaderGUI.BlendMode.Alpha);
                m.SetFloat("_BlendModePreserveSpecular", 1f);
                BaseShaderGUI.SetupMaterialBlendMode(m);
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                BaseShaderGUI.SetupMaterialBlendMode(m);
            }

            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Unlit material for glows, mist and particles ("Universal Render Pipeline/Particles/Unlit"), additive or alpha blended,
        /// double sided, optional soft texture. Colour is multiplied by each particle's start colour.
        /// </summary>
        public static Material GetParticle(string key, Color tint, bool additive, Texture texture = null)
        {
            Material m = Fetch(key, "Universal Render Pipeline/Particles/Unlit");
            m.SetColor("_BaseColor", tint);
            if (texture != null)
            {
                m.SetTexture("_BaseMap", texture);
            }

            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? (float)BaseShaderGUI.BlendMode.Additive : (float)BaseShaderGUI.BlendMode.Alpha);
            m.SetFloat("_BlendModePreserveSpecular", 0f);
            BaseShaderGUI.SetupMaterialBlendMode(m);
            m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Opaque unlit colour (emissive-looking surfaces that must not be shaded: eyes, bulbs, candy).</summary>
        public static Material GetUnlit(string key, Color color)
        {
            Material m = Fetch(key, "Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Fetch(string key, string shaderName)
        {
            string id = shaderName + "|" + key;
            if (Cache.TryGetValue(id, out Material cached) && cached != null)
            {
                return cached;
            }

            Directory.CreateDirectory(Folder);
            string safe = key.Replace('/', '_').Replace('\\', '_').Replace(':', '_').Replace(' ', '_');
            string tag = shaderName.Contains("Particles") ? "_P" : shaderName.EndsWith("/Lit") ? string.Empty : "_U";
            string path = $"{Folder}/Coaster_{safe}{tag}.mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[coast] Shader '{shaderName}' not found; falling back to URP Lit for {key}.");
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

            Cache[id] = material;
            return material;
        }
    }
}
