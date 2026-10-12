using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.Territory.EditorTools
{
    /// <summary>
    /// The terr HUD: one screen-space caption canvas carrying the share bar, captions and clock, plus a
    /// separate world-label canvas for the drum header and result labels. The reel itself is a physical 3D
    /// drum now (Look v3), and board counters and chip labels are TextMeshPro decals. Split out of
    /// <see cref="TerritorySceneBuilder"/> so the arena and the HUD can be restyled independently.
    /// </summary>
    static class TerritoryHudBuilder
    {
        // Bottom HUD, in canvas units of the 1080x1920 reference (match-height, so height is always 1920).
        // The camera fit in TerritoryArenaBuilder derives its near-edge target from these.
        public const float CanvasHeight = 1920f;
        public const float ShareBarBottom = 16f;
        public const float ShareBarHeight = 70f;
        public const float CaptionBottom = 92f;
        public const float CaptionTop = 230f;
        public const float CaptionMargin = 10f;

        /// <summary>Fraction of the frame height the arena must stay above: top of the caption band plus a margin.</summary>
        public const float ArenaFloorV = (CaptionTop + CaptionMargin) / CanvasHeight;

        public struct Hud
        {
            public CaptionHud caption;
            public ShareBarHud shareBar;
            public WorldLabelLayer labels;
        }

        public static Hud Build(Camera camera, TerritoryTeamDef[] teams)
        {
            CaptionHud caption = CaptionCanvasBuilder.Build(
                "WHICH COLOUR TAKES THE BOARD?",
                "×2 doubles it  ·  R fires it all  ·  lose your cannon, you're out",
                "Pick your colour before it starts.",
                CaptionCanvasBuilder.Style.Dark);
            var canvasRect = (RectTransform)caption.transform;
            LayoutCaption(canvasRect);

            ShareBarHud shareBar = ShareBarCanvasBuilder.BuildFramed(canvasRect, 4);
            var shareColors = new Color[4];
            var shareNames = new string[4];
            for (int i = 0; i < 4; i++)
            {
                shareColors[i] = teams[i].color;
                shareNames[i] = teams[i].name;
            }

            shareBar.Configure(4, shareColors, shareNames);
            PlaceBar((RectTransform)shareBar.transform);

            WorldLabelLayer labels = BuildLabelLayer(camera);

            return new Hud { caption = caption, shareBar = shareBar, labels = labels };
        }

        /// <summary>
        /// 16:9 with match-height: the canvas is about 3413 units wide and 1920 tall. Captions sit just above
        /// the share bar at the bottom; the footer clock moves to the top-right corner.
        /// </summary>
        static void LayoutCaption(RectTransform canvasRect)
        {
            Transform top = canvasRect.Find("TopPanel");
            Transform bottom = canvasRect.Find("BottomPanel");
            if (top != null)
            {
                // Bottom-anchored stack, CaptionBottom-CaptionTop up from the bottom edge (share bar sits below it).
                var rect = (RectTransform)top;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, CaptionBottom);
                rect.sizeDelta = new Vector2(-80f, CaptionTop - CaptionBottom);

                SetFont(top.Find("Caption"), 76f, FontStyles.Bold);
                SetFont(top.Find("Subline"), 40f, FontStyles.Normal);
            }

            if (bottom != null)
            {
                var rect = (RectTransform)bottom;
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-40f, -24f);
                rect.sizeDelta = new Vector2(700f, 70f);
                Transform footer = bottom.Find("Footer");
                SetFont(footer, 48f, FontStyles.Bold);
                if (footer != null && footer.TryGetComponent(out TMP_Text tmp))
                {
                    tmp.alignment = TextAlignmentOptions.MidlineRight;
                }
            }
        }

        static void SetFont(Transform text, float size, FontStyles style)
        {
            if (text != null && text.TryGetComponent(out TMP_Text tmp))
            {
                tmp.fontSize = size;
                tmp.fontStyle = style;
            }
        }

        /// <summary>Bottom edge: x 0.03-0.97 of the width, ShareBarHeight tall, ShareBarBottom above the bottom.</summary>
        static void PlaceBar(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.03f, 0f);
            rect.anchorMax = new Vector2(0.97f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, ShareBarBottom);
            rect.sizeDelta = new Vector2(0f, ShareBarHeight);
        }

        static WorldLabelLayer BuildLabelLayer(Camera camera)
        {
            var canvasObject = new GameObject("World Label Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Under the HUD canvas, so the share bar is never behind a drum label.
            canvas.sortingOrder = -1;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            WorldLabelLayer layer = canvasObject.AddComponent<WorldLabelLayer>();
            layer.Bind(camera, (RectTransform)canvasObject.transform);
            return layer;
        }
    }
}
