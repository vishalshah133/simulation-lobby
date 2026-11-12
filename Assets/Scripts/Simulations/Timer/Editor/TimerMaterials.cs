using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal;
using UnityEngine;

namespace SimulationLobby.Simulations.Timer.EditorTools
{
    /// <summary>Colours the scene is dressed in for one theme. Data only: the mechanism never changes with the theme.</summary>
    internal struct TimerPalette
    {
        public Color track, rail, cap, peg, pegCap, funnel, hopper, hopperRim, platform, support, board;
        public Color floor, trunk, foliage, cloud;
        public Color skyTop, skyHorizon, ambientSky, ambientEquator, ambientGround, key, fill, outline, glass;
    }

    /// <summary>Every material the timer scene uses, built from one palette.</summary>
    internal sealed class TimerLook
    {
        public Material track, rail, cap, peg, pegCap, funnel, hopper, hopperRim, platform, support, board;
        public Material floor, trunk, foliage, cloud, sky, glass;
        public Material outlineCourse, outlineThin, outlineMarble;
        public Material[] marbles;
    }

    /// <summary>
    /// Material helpers for the timer scene. The toon materials are rewritten on every build (their colours come from the palette and
    /// the theme), so there is nothing to tune by hand in them; the physics materials are created once.
    /// </summary>
    internal static class TimerMaterials
    {
        public const string Folder = TimerCourseFactory.Folder + "/Materials";
        const string ToonShader = "Simulation Lobby/Timer Toon";
        const string OutlineShader = "Simulation Lobby/Timer Outline";
        const string SkyShader = "Simulation Lobby/Timer Sky";

        static bool _warned;

        public static TimerPalette Palette(TimerTheme theme)
        {
            switch (theme)
            {
                case TimerTheme.Neon:
                    return new TimerPalette
                    {
                        track = H("#2A2F7A"), rail = H("#FF3EA5"), cap = H("#3CF0FF"), peg = H("#3CF0FF"), pegCap = H("#FF3EA5"),
                        funnel = H("#8A4DFF"), hopper = H("#3A3F9A"), hopperRim = H("#FFD93D"), platform = H("#23285C"), support = H("#4650B8"),
                        board = H("#151A3D"), floor = H("#1B2050"), trunk = H("#2A2F7A"), foliage = H("#00D1B2"), cloud = H("#6A5DC8"),
                        skyTop = H("#0B0B33"), skyHorizon = H("#5A3AA0"), ambientSky = H("#9AA0FF"), ambientEquator = H("#8A88E0"),
                        ambientGround = H("#4A4A90"), key = H("#EAE6FF"), fill = H("#7A6CFF"), outline = H("#05051A"), glass = H("#B8C8FF")
                    };
                case TimerTheme.Christmas:
                    return new TimerPalette
                    {
                        track = H("#F6F4EA"), rail = H("#D62828"), cap = H("#FFFFFF"), peg = H("#2E9E4E"), pegCap = H("#FFD23F"),
                        funnel = H("#D62828"), hopper = H("#2E9E4E"), hopperRim = H("#FFD23F"), platform = H("#B71C1C"), support = H("#FFFFFF"),
                        board = H("#1F7A3A"), floor = H("#EAF3FF"), trunk = H("#6B4226"), foliage = H("#1F7A3A"), cloud = H("#FFFFFF"),
                        skyTop = H("#4A86C8"), skyHorizon = H("#D6E6F5"), ambientSky = H("#E2EEFF"), ambientEquator = H("#DCE6F2"),
                        ambientGround = H("#B9C6D8"), key = H("#FFF3DC"), fill = H("#BFD8FF"), outline = H("#20202E"), glass = H("#CFE8FF")
                    };
                case TimerTheme.Halloween:
                    return new TimerPalette
                    {
                        track = H("#FFB347"), rail = H("#6A2FA0"), cap = H("#FF7A1A"), peg = H("#FF7A1A"), pegCap = H("#B6F23A"),
                        funnel = H("#6A2FA0"), hopper = H("#FF7A1A"), hopperRim = H("#B6F23A"), platform = H("#3A2255"), support = H("#6A4C93"),
                        board = H("#2A1A40"), floor = H("#3E2A5E"), trunk = H("#2A1A2A"), foliage = H("#7A3FA8"), cloud = H("#B8A0D8"),
                        skyTop = H("#1F1240"), skyHorizon = H("#C46A5A"), ambientSky = H("#C8A8E8"), ambientEquator = H("#B090C8"),
                        ambientGround = H("#7A5A90"), key = H("#FFE2C0"), fill = H("#9A6CFF"), outline = H("#100818"), glass = H("#D8C8F0")
                    };
                case TimerTheme.Winter:
                    return new TimerPalette
                    {
                        track = H("#EAF6FF"), rail = H("#3AA0E8"), cap = H("#FFFFFF"), peg = H("#7CC8F5"), pegCap = H("#FFFFFF"),
                        funnel = H("#3AA0E8"), hopper = H("#7CC8F5"), hopperRim = H("#FFFFFF"), platform = H("#9CCAE8"), support = H("#C8E4F5"),
                        board = H("#2A6FA8"), floor = H("#F2F8FF"), trunk = H("#6B4A3A"), foliage = H("#CFE8F5"), cloud = H("#FFFFFF"),
                        skyTop = H("#6FB0E8"), skyHorizon = H("#EAF6FF"), ambientSky = H("#E6F2FF"), ambientEquator = H("#DDE9F5"),
                        ambientGround = H("#B8C8DC"), key = H("#FFF6E8"), fill = H("#BFDCFF"), outline = H("#1A2A48"), glass = H("#D8EEFF")
                    };
                default:
                    return new TimerPalette
                    {
                        track = H("#FFEA8A"), rail = H("#F2333F"), cap = H("#FFFFFF"), peg = H("#3D7BFF"), pegCap = H("#FFD92E"),
                        funnel = H("#FF8A26"), hopper = H("#3D9BFF"), hopperRim = H("#FFD92E"), platform = H("#FF8A26"), support = H("#9CC8FF"),
                        board = H("#2B3FA8"), floor = H("#62C757"), trunk = H("#8A5A33"), foliage = H("#3FAE4A"), cloud = H("#FFFFFF"),
                        skyTop = H("#3D8FE8"), skyHorizon = H("#CFEAFF"), ambientSky = H("#D9ECFF"), ambientEquator = H("#DCE8E0"),
                        ambientGround = H("#8FB07A"), key = H("#FFF4DC"), fill = H("#BFDFFF"), outline = H("#14143A"), glass = H("#CFEFFF")
                    };
            }
        }

        public static TimerLook Build(TimerPalette p, TimerConfig config)
        {
            string theme = config.theme.ToString();
            var look = new TimerLook
            {
                track = Toon($"Toon_Track_{theme}", p.track, 0.35f),
                rail = Toon($"Toon_Rail_{theme}", p.rail, 0.4f),
                cap = Toon($"Toon_RailCap_{theme}", p.cap, 0.45f),
                peg = Toon($"Toon_Peg_{theme}", p.peg, 0.5f),
                pegCap = Toon($"Toon_PegCap_{theme}", p.pegCap, 0.5f),
                funnel = Toon($"Toon_Funnel_{theme}", p.funnel, 0.4f),
                hopper = Toon($"Toon_Hopper_{theme}", p.hopper, 0.4f),
                hopperRim = Toon($"Toon_HopperRim_{theme}", p.hopperRim, 0.4f),
                platform = Toon($"Toon_Platform_{theme}", p.platform, 0.3f),
                support = Toon($"Toon_Support_{theme}", p.support, 0.35f),
                board = Toon($"Toon_Board_{theme}", p.board, 0.3f),
                floor = Toon($"Toon_Floor_{theme}", p.floor, 0f, ambient: 0.5f),
                trunk = Toon($"Toon_Trunk_{theme}", p.trunk, 0.2f),
                foliage = Toon($"Toon_Foliage_{theme}", p.foliage, 0.35f),
                cloud = Toon($"Toon_Cloud_{theme}", p.cloud, 0.2f, ambient: 0.6f),
                sky = Sky($"Toon_Sky_{theme}", p),
                glass = Glass($"Toon_CollectorGlass_{theme}", p.glass),
                outlineCourse = Outline($"Toon_Outline_Course_{theme}", p.outline, 0.04f),
                outlineThin = Outline($"Toon_Outline_Thin_{theme}", p.outline, 0.025f),
                outlineMarble = Outline($"Toon_Outline_Marble_{theme}", p.outline, 0.022f)
            };

            Color[] colours = config.colors != null && config.colors.Length > 0 ? config.colors : new[] { Color.white };
            look.marbles = new Material[colours.Length];
            for (int i = 0; i < colours.Length; i++)
            {
                look.marbles[i] = Toon($"Toon_Marble_{theme}_{i}", colours[i], 0.62f, highlight: 0.05f, rimSoftness: 0.07f);
            }

            AssetDatabase.SaveAssets();
            return look;
        }

        public static PhysicsMaterial PhysicsMat(string name, float dynamicFriction, float staticFriction, float bounciness)
        {
            Directory.CreateDirectory(Folder);
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

        /// <summary>Appends the outline material to a renderer's list. A null outline (shader missing) leaves the renderer as it was.</summary>
        public static void AddOutline(Renderer renderer, Material outline)
        {
            if (renderer == null || outline == null)
            {
                return;
            }

            Material[] current = renderer.sharedMaterials;
            var list = new List<Material>(current) { outline };
            renderer.sharedMaterials = list.ToArray();
        }

        // ------------------------------------------------------------------ materials

        /// <summary>A cel-shaded surface. The shade colour is the base pushed darker and cooler.</summary>
        static Material Toon(string name, Color colour, float rimAmount, float highlight = 0f, float rimSoftness = 0.06f, float ambient = 0.35f)
        {
            Material material = Ensure(name, FindToon());
            colour.a = 1f;
            Set(material, "_BaseColor", colour);
            Set(material, "_ShadeColor", Shade(colour));
            Set(material, "_RimColor", Color.Lerp(colour, Color.white, 0.65f));
            Set(material, "_HighlightColor", Color.white);
            SetF(material, "_RimAmount", rimAmount);
            SetF(material, "_RimSoftness", rimSoftness);
            SetF(material, "_HighlightSize", highlight);
            SetF(material, "_AmbientStrength", ambient);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Outline(string name, Color colour, float width)
        {
            Shader shader = Shader.Find(OutlineShader);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                Warn(OutlineShader);
                return null;
            }

            Material material = Ensure(name, shader);
            Set(material, "_OutlineColor", colour);
            SetF(material, "_OutlineWidth", width);
            SetF(material, "_DistanceScale", 0.5f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Sky(string name, TimerPalette p)
        {
            Shader shader = Shader.Find(SkyShader);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                Warn(SkyShader);
                shader = Shader.Find("Universal Render Pipeline/Unlit");
                Material fallback = Ensure(name, shader);
                Set(fallback, "_BaseColor", p.skyHorizon);
                return fallback;
            }

            Material material = Ensure(name, shader);
            Set(material, "_TopColor", p.skyTop);
            Set(material, "_HorizonColor", p.skyHorizon);
            SetF(material, "_Power", 0.7f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Transparent URP Lit, two-sided, glossy: the collector wall. Marbles stay visible through it.</summary>
        static Material Glass(string name, Color tint)
        {
            Material material = Ensure(name, Shader.Find("Universal Render Pipeline/Lit"));
            Color colour = tint;
            colour.a = 0.22f;
            Set(material, "_BaseColor", colour);
            SetF(material, "_Smoothness", 0.95f);
            SetF(material, "_Metallic", 0f);
            SetF(material, "_Cull", 0f);
            SetF(material, "_Surface", 1f);
            SetF(material, "_Blend", 0f);
            SetF(material, "_BlendModePreserveSpecular", 1f);
            SetF(material, "_ZWrite", 0f);
            BaseShaderGUI.SetupMaterialBlendMode(material);
            SetF(material, "_Cull", 0f);
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Shader FindToon()
        {
            Shader shader = Shader.Find(ToonShader);
            if (shader != null && !ShaderUtil.ShaderHasError(shader))
            {
                return shader;
            }

            Warn(ToonShader);
            return Shader.Find("Universal Render Pipeline/Lit");
        }

        static void Warn(string shaderName)
        {
            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning($"[timer] Shader '{shaderName}' is missing or has errors (check the Console and Assets/Shaders/Timer); " +
                                 "falling back to URP Lit / no outlines. Fix it and rebuild the scene.");
            }
        }

        static Material Ensure(string name, Shader shader)
        {
            Directory.CreateDirectory(Folder);
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        static Color Shade(Color c)
        {
            Color shade = Color.Lerp(new Color(c.r * 0.62f, c.g * 0.62f, c.b * 0.62f, 1f), new Color(0.3f, 0.25f, 0.55f, 1f), 0.3f);
            shade.a = 1f;
            return shade;
        }

        static void Set(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        static void SetF(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        static Color H(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color colour);
            return colour;
        }
    }
}
