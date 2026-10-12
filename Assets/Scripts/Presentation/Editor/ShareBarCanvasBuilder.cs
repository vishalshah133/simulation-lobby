using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Presentation.EditorTools
{
    /// <summary>
    /// Builds the strip a <see cref="ShareBarHud"/> drives: a wide dark plate holding N coloured segments
    /// with a percentage label on each. Built under a panel the caller already owns, so the caller decides
    /// where on the canvas it sits; this only fills it. Sized for the 1920x1080 long-form reference
    /// resolution with match-height, and legible when the same frame is downscaled to 480px wide.
    /// </summary>
    public static class ShareBarCanvasBuilder
    {
        const float BarHeight = 118f;
        const float LabelSize = 64f;

        static readonly Color FrameColor = new Color32(0x22, 0x23, 0x2B, 255);
        static readonly Color PlateColor = new Color(0.04f, 0.04f, 0.05f, 0.82f);

        /// <summary>
        /// Builds the bar under <paramref name="parent"/>, stretched across its width. Anchors are the
        /// caller's to change on the returned component's rect.
        /// </summary>
        public static ShareBarHud Build(RectTransform parent, int segments)
        {
            return BuildInternal(parent, segments, 10f, LabelSize, 0f, false);
        }

        /// <summary>
        /// Framed variant: a rounded charcoal frame (<paramref name="border"/> units thick) around rounded
        /// segments with <paramref name="gap"/> units between them, and a bold outlined label on each.
        /// Anchors and height are the caller's to set on the returned component's rect.
        /// </summary>
        public static ShareBarHud BuildFramed(RectTransform parent, int segments, float border = 12f,
            float gap = 6f, float labelSize = 54f)
        {
            return BuildInternal(parent, segments, border, labelSize, gap, true);
        }

        static ShareBarHud BuildInternal(RectTransform parent, int segments, float border, float labelSize,
            float gap, bool rounded)
        {
            segments = Mathf.Max(1, segments);

            RectTransform root = ScoreboardCanvasBuilder.CreatePanel(parent, "Share Bar",
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, BarHeight));
            root.pivot = new Vector2(0.5f, 0.5f);

            // Dark plate with a small margin: the coloured segments sit inside it like an inlay, so
            // adjacent colours never bleed into the background behind the bar.
            var plate = root.gameObject.AddComponent<Image>();
            plate.color = rounded ? FrameColor : PlateColor;
            plate.raycastTarget = false;
            if (rounded)
            {
                plate.sprite = GeneratedArt.RoundedRect();
                plate.type = Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = 1.4f;
            }

            RectTransform bar = ScoreboardCanvasBuilder.CreateRow(root, "Bar");
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = Vector2.one;
            bar.offsetMin = new Vector2(border, border);
            bar.offsetMax = new Vector2(-border, -border);

            var images = new Image[segments];
            var labels = new TMP_Text[segments];
            var colors = new Color[segments];
            var names = new string[segments];
            for (int i = 0; i < segments; i++)
            {
                RectTransform segment = ScoreboardCanvasBuilder.CreateRow(bar, $"Segment {i + 1:00}");
                segment.anchorMin = new Vector2(i / (float)segments, 0f);
                segment.anchorMax = new Vector2((i + 1) / (float)segments, 1f);
                segment.offsetMin = Vector2.zero;
                segment.offsetMax = Vector2.zero;

                colors[i] = Color.HSVToRGB(i / (float)segments, 0.75f, 0.95f);
                names[i] = $"Team {i + 1}";
                var image = segment.gameObject.AddComponent<Image>();
                image.color = colors[i];
                image.raycastTarget = false;
                if (rounded)
                {
                    image.sprite = GeneratedArt.RoundedRect();
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = 2.5f;
                }
                images[i] = image;

                TMP_Text label = ScoreboardCanvasBuilder.CreateText(segment, "Label", "0%", labelSize,
                    Color.white, FontStyles.Bold);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.raycastTarget = false;
                // A dark outline keeps white type readable on every team colour, light ones included.
                label.outlineWidth = rounded ? 0.35f : 0.28f;
                label.outlineColor = new Color32(0, 0, 0, 255);
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                labels[i] = label;
            }

            ShareBarHud hud = root.gameObject.AddComponent<ShareBarHud>();
            hud.segmentGap = gap;
            hud.Bind(bar, images, labels);
            hud.Configure(segments, colors, names);
            return hud;
        }
    }
}
