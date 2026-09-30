using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Presentation.EditorTools
{
    /// <summary>
    /// Builds the screen-space canvas a <see cref="CaptionHud"/> drives. Same conventions as
    /// <see cref="ScoreboardCanvasBuilder"/> (overlay, 1080x1920 reference, match height, top and
    /// bottom panels only) and reuses its panel and text helpers.
    /// </summary>
    public static class CaptionCanvasBuilder
    {
        public static CaptionHud Build(string caption, string subline, string footer)
        {
            var canvasObject = new GameObject("Caption Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            CaptionHud hud = canvasObject.AddComponent<CaptionHud>();

            RectTransform topPanel = ScoreboardCanvasBuilder.CreatePanel(canvasObject.transform, "TopPanel",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -150f), new Vector2(-80f, 300f));
            var layout = topPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 6f;

            // White and heavy: the caption is the only text that matters and must survive 480px wide.
            TMP_Text captionText = ScoreboardCanvasBuilder.CreateText(topPanel, "Caption", caption, 132f,
                new Color(0.98f, 0.98f, 1f, 1f), FontStyles.Bold);
            captionText.characterSpacing = 4f;
            AddShadow(captionText);
            TMP_Text sublineText = ScoreboardCanvasBuilder.CreateText(topPanel, "Subline", subline, 50f,
                new Color(0.82f, 0.8f, 0.78f, 1f), FontStyles.Normal);
            AddShadow(sublineText);

            RectTransform bottomPanel = ScoreboardCanvasBuilder.CreatePanel(canvasObject.transform, "BottomPanel",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 120f), new Vector2(-80f, 120f));
            TMP_Text footerText = ScoreboardCanvasBuilder.CreateText(bottomPanel, "Footer", footer, 40f,
                new Color(0.7f, 0.68f, 0.66f, 0.85f), FontStyles.Normal);
            var footerRect = footerText.rectTransform;
            footerRect.anchorMin = Vector2.zero;
            footerRect.anchorMax = Vector2.one;
            footerRect.offsetMin = Vector2.zero;
            footerRect.offsetMax = Vector2.zero;

            hud.Bind(captionText, sublineText, footerText);
            return hud;
        }

        /// <summary>Soft drop shadow via the font material's underlay, so white text holds on a bright highlight.</summary>
        static void AddShadow(TMP_Text text)
        {
            if (text.fontSharedMaterial == null)
            {
                return;
            }

            // A saved copy, so the shared TMP default material isn't modified project-wide and the
            // scene references an asset rather than an unsaved object.
            const string path = "Assets/Settings/Studio/CaptionUnderlay.mat";
            Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                System.IO.Directory.CreateDirectory("Assets/Settings/Studio");
                material = new Material(text.fontSharedMaterial);
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.65f));
                material.SetFloat("_UnderlayOffsetX", 0.35f);
                material.SetFloat("_UnderlayOffsetY", -0.35f);
                material.SetFloat("_UnderlaySoftness", 0.5f);
                UnityEditor.AssetDatabase.CreateAsset(material, path);
            }

            text.fontSharedMaterial = material;
        }
    }
}
