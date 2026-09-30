using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Presentation.EditorTools
{
    /// <summary>
    /// Builds the screen-space scoreboard canvas a <see cref="VersusScoreboardHud"/> drives, from
    /// code. Shared by every format's scene builder.
    /// </summary>
    /// <remarks>
    /// Promoted out of <c>EscapeSceneBuilder</c> the moment the 3D wall format (now <c>surv</c>) needed the same canvas:
    /// copy-pasting it into a second format is the signal to promote, not a fix. The layout rules it
    /// encodes (match-height scaling at 1080x1920, top and bottom panels only so the arena stays
    /// clear, equal-width versus sides) are project-wide conventions, and a second hand-maintained
    /// copy is how they drift apart.
    /// </remarks>
    public static class ScoreboardCanvasBuilder
    {
        /// <summary>Text a freshly built scoreboard starts with. All of it is overwritten at runtime.</summary>
        public struct Labels
        {
            public string title;
            public string question;
            public string leftName;
            public string rightName;
            public string round;
            public string counter;
        }

        /// <summary>The two sides' accent colours — the only thing a format usually needs to vary.</summary>
        public struct Palette
        {
            public Color leftAccent;
            public Color rightAccent;

            public static Palette Default => new Palette
            {
                leftAccent = new Color(0.72f, 0.76f, 0.9f, 1f),
                rightAccent = new Color(1f, 0.35f, 0.5f, 1f)
            };
        }

        /// <summary>
        /// Screen-space canvas: top panel carries the title, the question and the versus score; bottom
        /// panel the round line, the counter and the result. Anchored to the screen edges rather than
        /// placed in world space, so the same scene reads correctly at 1080x1920 and 1920x1080.
        /// </summary>
        public static VersusScoreboardHud Build(Labels labels, Palette palette)
        {
            var canvasObject = new GameObject("Scoreboard Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // Match height: the Shorts frame is height-dominant, and matching width would shrink the
            // whole scoreboard when the same scene is rendered 16:9.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            VersusScoreboardHud hud = canvasObject.AddComponent<VersusScoreboardHud>();

            // --- Top panel: title, question, versus score. Panels hug the top and bottom edges so the
            //     middle of the frame — where the simulation lives — is never behind text. ---
            RectTransform topPanel = CreatePanel(canvasObject.transform, "TopPanel",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -28f), new Vector2(-80f, 420f));
            AddVerticalLayout(topPanel, 16f);

            TMP_Text title = CreateText(topPanel, "Title", labels.title, 72f,
                new Color(0.93f, 0.95f, 1f, 1f), FontStyles.Bold);
            TMP_Text question = CreateText(topPanel, "Question", labels.question, 46f,
                new Color(0.6f, 0.64f, 0.78f, 1f), FontStyles.Bold);

            // The versus row. Force-expand plus child-control gives the two sides equal width, so the
            // score stays symmetrical instead of shifting as the digits change.
            RectTransform versusRow = CreateRow(topPanel, "VersusRow");
            var versusLayout = versusRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            versusLayout.childControlWidth = true;
            versusLayout.childControlHeight = true;
            versusLayout.childForceExpandWidth = true;
            versusLayout.childForceExpandHeight = true;
            versusLayout.childAlignment = TextAnchor.MiddleCenter;
            versusLayout.spacing = 12f;

            RectTransform leftSide = CreateRow(versusRow, "LeftSide");
            AddVerticalLayout(leftSide, 0f);
            TMP_Text leftLabel = CreateText(leftSide, "LeftLabel", labels.leftName, 40f,
                Desaturate(palette.leftAccent), FontStyles.Bold);
            TMP_Text leftScore = CreateText(leftSide, "LeftScore", "0", 120f, palette.leftAccent, FontStyles.Bold);

            CreateText(versusRow, "Vs", "vs", 44f, new Color(0.45f, 0.48f, 0.6f, 1f), FontStyles.Italic);

            RectTransform rightSide = CreateRow(versusRow, "RightSide");
            AddVerticalLayout(rightSide, 0f);
            TMP_Text rightLabel = CreateText(rightSide, "RightLabel", labels.rightName, 40f,
                Desaturate(palette.rightAccent), FontStyles.Bold);
            TMP_Text rightScore = CreateText(rightSide, "RightScore", "0", 120f, palette.rightAccent, FontStyles.Bold);

            // --- Bottom panel: round, counter, result ---
            RectTransform bottomPanel = CreatePanel(canvasObject.transform, "BottomPanel",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 28f), new Vector2(-80f, 420f));
            AddVerticalLayout(bottomPanel, 10f);

            TMP_Text round = CreateText(bottomPanel, "Round", labels.round, 44f,
                new Color(0.6f, 0.64f, 0.78f, 1f), FontStyles.Bold);
            TMP_Text counter = CreateText(bottomPanel, "Counter", labels.counter, 56f,
                new Color(0.82f, 0.85f, 0.94f, 1f), FontStyles.Bold);
            TMP_Text result = CreateText(bottomPanel, "Result", string.Empty, 72f,
                new Color(0.85f, 0.87f, 0.95f, 1f), FontStyles.Bold);

            hud.Bind(title, question, leftLabel, leftScore, rightLabel, rightScore, round, counter, result);
            return hud;
        }

        static Color Desaturate(Color accent)
        {
            // Labels sit above their score and must not compete with it for attention.
            return Color.Lerp(accent, new Color(0.6f, 0.64f, 0.78f, 1f), 0.45f);
        }

        static void AddVerticalLayout(RectTransform target, float spacing)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = spacing;
        }

        /// <summary>Panel anchored to one screen edge, sized in reference-resolution pixels.</summary>
        public static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);

            var rect = (RectTransform)panel.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, anchorMax.y > 0.5f ? 1f : 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        /// <summary>Layout container with no anchoring of its own — the parent layout group sizes it.</summary>
        public static RectTransform CreateRow(Transform parent, string name)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            return (RectTransform)row.transform;
        }

        public static TMP_Text CreateText(Transform parent, string name, string content, float fontSize,
            Color color, FontStyles style)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            // Auto-sizing off: a score that resizes itself as digits change reads as unstable, and the
            // panels are already sized for the longest string each one shows.
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            return text;
        }
    }
}
