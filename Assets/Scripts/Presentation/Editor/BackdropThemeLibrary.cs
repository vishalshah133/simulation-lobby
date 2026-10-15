using System.IO;
using SimulationLobby.Core;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Presentation.EditorTools
{
    /// <summary>
    /// The stock backdrop themes, written once to <c>Assets/Settings/Backdrops/</c>, and the one call a
    /// 2D scene builder makes to give its scene a <see cref="BackdropStyler"/>. After the first build the
    /// assets are the truth: edit them in the Inspector, and the defaults here are only used if one is
    /// missing.
    /// </summary>
    public static class BackdropThemeLibrary
    {
        const string Folder = "Assets/Settings/Backdrops";

        /// <summary>Every stock theme, created on first use. All dark, so arena contrast holds.</summary>
        public static BackdropTheme[] All()
        {
            return new[]
            {
                Ensure("Midnight", t =>
                {
                    // The original esc / rom look: navy, slow blue motes.
                    t.background = new Color(0.04f, 0.05f, 0.09f, 1f);
                    t.moteTint = new Color(0.55f, 0.65f, 1f, 1f);
                    t.moteTexture = GeneratedArt.SoftDot();
                }),
                Ensure("Ember", t =>
                {
                    t.background = new Color(0.075f, 0.035f, 0.03f, 1f);
                    t.moteTint = new Color(1f, 0.55f, 0.25f, 1f);
                    t.moteAlpha = 0.16f;
                    t.moteSize = new Vector2(0.04f, 0.14f);
                    t.moteCount = 80;
                    t.flow = new Vector2(0f, 0.28f);
                    t.moteTexture = GeneratedArt.SoftDot();
                }),
                Ensure("Frost", t =>
                {
                    t.background = new Color(0.025f, 0.06f, 0.075f, 1f);
                    t.moteTint = new Color(0.78f, 0.95f, 1f, 1f);
                    t.moteAlpha = 0.14f;
                    t.moteSize = new Vector2(0.08f, 0.22f);
                    t.flow = new Vector2(0.05f, -0.22f);
                    t.spinSpeed = 40f;
                    t.moteTexture = GeneratedArt.Star();
                }),
                Ensure("Nebula", t =>
                {
                    t.background = new Color(0.06f, 0.03f, 0.09f, 1f);
                    t.moteTint = new Color(0.85f, 0.5f, 1f, 1f);
                    t.moteAlpha = 0.14f;
                    t.moteSize = new Vector2(0.06f, 0.18f);
                    t.driftSpeed = 0.18f;
                    t.spinSpeed = 60f;
                    t.moteTexture = GeneratedArt.Diamond();
                }),
                Ensure("Deep Sea", t =>
                {
                    t.background = new Color(0.015f, 0.05f, 0.06f, 1f);
                    t.moteTint = new Color(0.3f, 1f, 0.8f, 1f);
                    t.moteAlpha = 0.15f;
                    t.moteSize = new Vector2(0.08f, 0.25f);
                    t.moteCount = 60;
                    t.flow = new Vector2(0f, 0.12f);
                    t.moteTexture = GeneratedArt.Ring();
                })
            };
        }

        /// <summary>
        /// Adds a styler beside the backdrop, wired and loaded with every stock theme. Variance defaults
        /// live on the component; tune them per scene in the Inspector.
        /// </summary>
        public static BackdropStyler AddStyler(GameObject host, SimulationRunner runner, Camera camera, AmbientDriftField field)
        {
            BackdropStyler styler = host.AddComponent<BackdropStyler>();
            styler.Bind(runner, camera, field);
            styler.themes = All();
            return styler;
        }

        static BackdropTheme Ensure(string name, System.Action<BackdropTheme> defaults)
        {
            string path = $"{Folder}/Backdrop_{name.Replace(" ", "")}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BackdropTheme>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Folder);
            var theme = ScriptableObject.CreateInstance<BackdropTheme>();
            theme.displayName = name;
            defaults(theme);
            AssetDatabase.CreateAsset(theme, path);
            return theme;
        }
    }
}
