using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Presentation.EditorTools
{
    /// <summary>
    /// Builds the reel a <see cref="SlotReelHud"/> drives: a header line above a compact masked window,
    /// both on one panel the caller positions. The window shows three faces at the 1920x1080 reference
    /// resolution (match-height); type is large enough to read at 480px wide.
    /// </summary>
    public static class SlotReelCanvasBuilder
    {
        const float FaceHeight = 190f;
        const float WindowWidth = 420f;
        const float HeaderHeight = 96f;
        const float HeaderSize = 66f;
        const float FaceSize = 150f;

        static readonly Color PlateColor = new Color(0.04f, 0.04f, 0.06f, 0.88f);

        /// <summary>
        /// Builds the reel under <paramref name="parent"/>, centred in it. Anchors are the caller's to
        /// change on the returned component's rect.
        /// </summary>
        public static SlotReelHud Build(RectTransform parent)
        {
            float windowHeight = FaceHeight * 3f;
            float rootWidth = 760f;
            float rootHeight = HeaderHeight + windowHeight + 24f;

            RectTransform root = ScoreboardCanvasBuilder.CreatePanel(parent, "Slot Reel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(rootWidth, rootHeight));
            root.pivot = new Vector2(0.5f, 0.5f);

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Header, above the window: "BLUE is SPINNING".
            TMP_Text header = ScoreboardCanvasBuilder.CreateText(root, "Header", string.Empty, HeaderSize,
                Color.white, FontStyles.Bold);
            header.textWrappingMode = TextWrappingModes.NoWrap;
            header.overflowMode = TextOverflowModes.Overflow;
            header.raycastTarget = false;
            header.characterSpacing = 6f;
            Outline(header, 0.3f);
            RectTransform headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, HeaderHeight);

            // The window: dark plate, rect-masked, showing three faces. The punch scale lands on this.
            RectTransform window = ScoreboardCanvasBuilder.CreateRow(root, "Window");
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0f);
            window.pivot = new Vector2(0.5f, 0f);
            window.anchoredPosition = new Vector2(0f, 12f);
            window.sizeDelta = new Vector2(WindowWidth, windowHeight);
            var plate = window.gameObject.AddComponent<Image>();
            plate.color = PlateColor;
            plate.raycastTarget = false;
            window.gameObject.AddComponent<RectMask2D>();

            RectTransform strip = ScoreboardCanvasBuilder.CreateRow(window, "Strip");
            strip.anchorMin = Vector2.zero;
            strip.anchorMax = Vector2.one;
            strip.offsetMin = Vector2.zero;
            strip.offsetMax = Vector2.zero;

            // Template the hud clones for its rows. It stays inactive in the built scene.
            TMP_Text template = ScoreboardCanvasBuilder.CreateText(strip, "Face Template", "?", FaceSize,
                Color.white, FontStyles.Bold);
            template.textWrappingMode = TextWrappingModes.NoWrap;
            template.overflowMode = TextOverflowModes.Overflow;
            template.raycastTarget = false;
            Outline(template, 0.22f);
            template.gameObject.SetActive(false);

            SlotReelHud hud = root.gameObject.AddComponent<SlotReelHud>();
            hud.faceHeight = FaceHeight;
            hud.Bind(group, header, window, strip, template);
            return hud;
        }

        /// <summary>
        /// Builds a compact reel for a top strip. The root is a band the caller anchors; the window
        /// (<paramref name="windowSize"/>) hugs its right edge. With <paramref name="headerBeside"/> the header
        /// sits left of the window on one line (right-aligned, vertically centred on it, shrinking to fit);
        /// otherwise it sits above. Faces are spaced so the centre face reads and its neighbours peek in.
        /// </summary>
        public static SlotReelHud Build(RectTransform parent, Vector2 windowSize, float faceFontSize, bool headerBeside)
        {
            const float Gap = 18f;
            const float BesideHeaderSize = 64f;
            float headerAbove = headerBeside ? 0f : HeaderHeight;
            float rootHeight = windowSize.y + headerAbove;

            RectTransform root = ScoreboardCanvasBuilder.CreatePanel(parent, "Slot Reel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(windowSize.x, rootHeight));
            root.pivot = new Vector2(0.5f, 0.5f);

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            TMP_Text header = ScoreboardCanvasBuilder.CreateText(root, "Header", string.Empty,
                headerBeside ? BesideHeaderSize : HeaderSize, Color.white, FontStyles.Bold);
            header.textWrappingMode = TextWrappingModes.NoWrap;
            header.overflowMode = TextOverflowModes.Overflow;
            header.raycastTarget = false;
            header.characterSpacing = headerBeside ? 2f : 6f;
            Outline(header, 0.3f);
            RectTransform headerRect = header.rectTransform;
            if (headerBeside)
            {
                header.alignment = TextAlignmentOptions.MidlineRight;
                header.enableAutoSizing = true;
                header.fontSizeMax = BesideHeaderSize;
                header.fontSizeMin = 20f;
                headerRect.anchorMin = new Vector2(0f, 0.5f);
                headerRect.anchorMax = new Vector2(1f, 0.5f);
                headerRect.pivot = new Vector2(0.5f, 0.5f);
                headerRect.offsetMin = new Vector2(0f, -windowSize.y * 0.5f);
                headerRect.offsetMax = new Vector2(-(windowSize.x + Gap), windowSize.y * 0.5f);
            }
            else
            {
                headerRect.anchorMin = new Vector2(0f, 1f);
                headerRect.anchorMax = new Vector2(1f, 1f);
                headerRect.pivot = new Vector2(0.5f, 1f);
                headerRect.anchoredPosition = Vector2.zero;
                headerRect.sizeDelta = new Vector2(0f, HeaderHeight);
            }

            RectTransform window = ScoreboardCanvasBuilder.CreateRow(root, "Window");
            window.anchorMin = window.anchorMax = headerBeside ? new Vector2(1f, 0.5f) : new Vector2(0.5f, 0f);
            window.pivot = headerBeside ? new Vector2(1f, 0.5f) : new Vector2(0.5f, 0f);
            window.anchoredPosition = Vector2.zero;
            window.sizeDelta = windowSize;
            var plate = window.gameObject.AddComponent<Image>();
            plate.color = PlateColor;
            plate.raycastTarget = false;
            window.gameObject.AddComponent<RectMask2D>();

            RectTransform strip = ScoreboardCanvasBuilder.CreateRow(window, "Strip");
            strip.anchorMin = Vector2.zero;
            strip.anchorMax = Vector2.one;
            strip.offsetMin = Vector2.zero;
            strip.offsetMax = Vector2.zero;

            TMP_Text template = ScoreboardCanvasBuilder.CreateText(strip, "Face Template", "?", faceFontSize,
                Color.white, FontStyles.Bold);
            template.textWrappingMode = TextWrappingModes.NoWrap;
            template.overflowMode = TextOverflowModes.Overflow;
            template.raycastTarget = false;
            Outline(template, 0.22f);
            template.gameObject.SetActive(false);

            SlotReelHud hud = root.gameObject.AddComponent<SlotReelHud>();
            // Neighbours sit just inside the window edge (partly visible); the centre face stays clear.
            hud.faceHeight = windowSize.y * 0.56f;
            hud.Bind(group, header, window, strip, template);
            return hud;
        }

        static readonly Color DrumFrame = new Color32(0x22, 0x23, 0x2B, 255);
        static readonly Color DrumFace = new Color32(0xFF, 0x4F, 0xA0, 255);
        static readonly Color DrumFaceTop = new Color32(0xFF, 0x86, 0xBE, 255);
        static readonly Color DrumArrow = new Color32(0xFF, 0xD2, 0x3A, 255);

        /// <summary>
        /// Builds the styled drum: a rounded charcoal frame holding a hot-pink face panel (lighter top band),
        /// a yellow arrow pointing at it from the left, and the header (bold italic, outlined) left of the arrow.
        /// The returned root is the frame, sized <paramref name="faceSize"/> plus padding, centred in
        /// <paramref name="parent"/>; the arrow and header hang off its left edge, so anchor the root where the
        /// frame should sit. <c>alwaysVisible</c> is left for the caller; <c>tintLandedFace</c> is off (white faces).
        /// </summary>
        public static SlotReelHud BuildDrum(RectTransform parent, Vector2 faceSize, float faceFontSize)
        {
            const float Pad = 14f;
            const float ArrowWidth = 72f;
            const float ArrowHeight = 84f;
            const float ArrowGap = 12f;
            const float HeaderGap = 18f;
            const float HeaderFont = 70f;
            Vector2 frameSize = faceSize + new Vector2(Pad * 2f, Pad * 2f);

            RectTransform root = ScoreboardCanvasBuilder.CreatePanel(parent, "Slot Reel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, frameSize);
            root.pivot = new Vector2(0.5f, 0.5f);

            var frame = root.gameObject.AddComponent<Image>();
            frame.sprite = GeneratedArt.RoundedRect();
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 1.6f;
            frame.color = DrumFrame;
            frame.raycastTarget = false;

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Yellow arrow on the frame's left edge, pointing right at the face.
            RectTransform arrowRect = ScoreboardCanvasBuilder.CreateRow(root, "Arrow");
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0f, 0.5f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-ArrowGap, 0f);
            arrowRect.sizeDelta = new Vector2(ArrowWidth, ArrowHeight);
            var arrow = arrowRect.gameObject.AddComponent<Image>();
            arrow.sprite = GeneratedArt.ArrowRight();
            arrow.color = DrumArrow;
            arrow.raycastTarget = false;

            // Header left of the arrow, right-aligned so it grows leftwards. Team colour, dark outline.
            TMP_Text header = ScoreboardCanvasBuilder.CreateText(root, "Header", string.Empty, HeaderFont,
                Color.white, FontStyles.Bold | FontStyles.Italic);
            header.alignment = TextAlignmentOptions.MidlineRight;
            header.textWrappingMode = TextWrappingModes.NoWrap;
            header.overflowMode = TextOverflowModes.Overflow;
            header.raycastTarget = false;
            header.characterSpacing = 2f;
            header.outlineWidth = 0.35f;
            header.outlineColor = new Color32(0x10, 0x10, 0x16, 255);
            RectTransform headerRect = header.rectTransform;
            headerRect.anchorMin = headerRect.anchorMax = new Vector2(0f, 0.5f);
            headerRect.pivot = new Vector2(1f, 0.5f);
            headerRect.anchoredPosition = new Vector2(-(ArrowGap + ArrowWidth + HeaderGap), 0f);
            headerRect.sizeDelta = new Vector2(1100f, 120f);

            // Pink face panel, rect-masked. The punch scale lands on this.
            RectTransform window = ScoreboardCanvasBuilder.CreateRow(root, "Window");
            window.anchorMin = Vector2.zero;
            window.anchorMax = Vector2.one;
            window.offsetMin = new Vector2(Pad, Pad);
            window.offsetMax = new Vector2(-Pad, -Pad);
            var plate = window.gameObject.AddComponent<Image>();
            plate.sprite = GeneratedArt.RoundedRect();
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 4f;
            plate.color = DrumFace;
            plate.raycastTarget = false;
            window.gameObject.AddComponent<RectMask2D>();

            // Lighter band across the top half: a cheap vertical gradient.
            RectTransform band = ScoreboardCanvasBuilder.CreateRow(window, "Top Band");
            band.anchorMin = new Vector2(0f, 0.5f);
            band.anchorMax = Vector2.one;
            band.offsetMin = new Vector2(5f, 0f);
            band.offsetMax = new Vector2(-5f, -4f);
            var bandImage = band.gameObject.AddComponent<Image>();
            bandImage.sprite = GeneratedArt.Square();
            bandImage.color = DrumFaceTop;
            bandImage.raycastTarget = false;

            RectTransform strip = ScoreboardCanvasBuilder.CreateRow(window, "Strip");
            strip.anchorMin = Vector2.zero;
            strip.anchorMax = Vector2.one;
            strip.offsetMin = Vector2.zero;
            strip.offsetMax = Vector2.zero;

            TMP_Text template = ScoreboardCanvasBuilder.CreateText(strip, "Face Template", "?", faceFontSize,
                Color.white, FontStyles.Bold);
            template.textWrappingMode = TextWrappingModes.NoWrap;
            template.overflowMode = TextOverflowModes.Overflow;
            template.raycastTarget = false;
            template.outlineWidth = 0.2f;
            template.outlineColor = new Color32(0x6A, 0x12, 0x3C, 255);
            template.gameObject.SetActive(false);

            SlotReelHud hud = root.gameObject.AddComponent<SlotReelHud>();
            // Pitch >= 1.15 x the rendered line height (~1.2 x font size) so neighbours never overlap the centre face.
            hud.faceHeight = Mathf.Max(20f, faceFontSize * 1.4f);
            hud.tintLandedFace = false;
            hud.Bind(group, header, window, strip, template);
            return hud;
        }

        static void Outline(TMP_Text text, float width)
        {
            text.outlineWidth = width;
            text.outlineColor = new Color32(0, 0, 0, 255);
        }
    }
}
