using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Presentation.EditorTools
{
    /// <summary>
    /// Builds the landscape canvas a <see cref="LeaderboardHud"/> drives: standings down the left edge, a
    /// timer top-right and a centre banner. Long-form is 16:9, so this one is laid out for 1920x1080
    /// (reference 1920x1080, match height) instead of the Shorts canvases' 1080x1920.
    /// </summary>
    public static class LeaderboardCanvasBuilder
    {
        const float RowHeight = 62f;
        const float PanelWidth = 470f;

        static readonly Color PanelColor = new Color(0.06f, 0.06f, 0.07f, 0.62f);
        static readonly Color Ivory = new Color(0.95f, 0.93f, 0.89f, 1f);
        static readonly Color Brass = new Color(0.93f, 0.71f, 0.38f, 1f);

        public static LeaderboardHud Build(string title, int rowCapacity)
        {
            var canvasObject = new GameObject("Leaderboard Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            LeaderboardHud hud = canvasObject.AddComponent<LeaderboardHud>();
            hud.rowHeight = RowHeight;

            // Standings: top-left, a title above a stack of rows the hud slides into place.
            RectTransform board = NewRect(canvasObject.transform, "Board", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(48f, -44f), new Vector2(PanelWidth, RowHeight * (rowCapacity + 1.4f)));

            TMP_Text titleText = NewText(board, "Title", title, 40f, Brass, FontStyles.Bold, TextAlignmentOptions.Left);
            titleText.characterSpacing = 8f;
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(8f, 0f);
            titleRect.sizeDelta = new Vector2(PanelWidth, 52f);

            RectTransform rowsRoot = NewRect(board, "Rows", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, -64f), new Vector2(PanelWidth, RowHeight * rowCapacity));

            var rows = new LeaderboardHud.Row[rowCapacity];
            for (int i = 0; i < rowCapacity; i++)
            {
                rows[i] = BuildRow(rowsRoot, i);
            }

            // Timer: top-right.
            RectTransform timerPanel = NewRect(canvasObject.transform, "Timer", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-48f, -44f), new Vector2(300f, 76f));
            timerPanel.gameObject.AddComponent<Image>().color = PanelColor;
            TMP_Text timer = NewText(timerPanel, "Time", "0:00.0", 52f, Ivory, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(timer.rectTransform);

            // Banner: centre, for the countdown and the winner.
            RectTransform bannerRect = NewRect(canvasObject.transform, "Banner", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(980f, 230f));
            // A dark plate behind the text keeps the ivory legible on the bright backdrop.
            bannerRect.gameObject.AddComponent<Image>().color = PanelColor;
            var group = bannerRect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            TMP_Text banner = NewText(bannerRect, "Text", string.Empty, 150f, Ivory, FontStyles.Bold, TextAlignmentOptions.Center);
            banner.characterSpacing = 10f;
            Stretch(banner.rectTransform);

            hud.Bind(rows, titleText, timer, banner, group);
            hud.Configure(rowCapacity);
            return hud;
        }

        static LeaderboardHud.Row BuildRow(RectTransform parent, int index)
        {
            RectTransform rect = NewRect(parent, $"Row {index + 1:00}", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, -index * RowHeight), new Vector2(PanelWidth, RowHeight - 6f));
            rect.gameObject.AddComponent<Image>().color = PanelColor;

            // Colour bar on the left edge, the one glanceable link between the board and the marble.
            RectTransform swatchRect = NewRect(rect, "Swatch", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(14f, 0f));
            var swatch = swatchRect.gameObject.AddComponent<Image>();

            TMP_Text place = NewText(rect, "Place", (index + 1).ToString(), 38f, Ivory, FontStyles.Bold, TextAlignmentOptions.Center);
            SetBox(place.rectTransform, 16f, 70f);

            TMP_Text name = NewText(rect, "Name", "-", 36f, Ivory, FontStyles.Bold, TextAlignmentOptions.Left);
            name.characterSpacing = 3f;
            SetBox(name.rectTransform, 92f, 250f);

            TMP_Text note = NewText(rect, "Note", string.Empty, 30f, Brass, FontStyles.Normal, TextAlignmentOptions.Right);
            RectTransform noteRect = note.rectTransform;
            noteRect.anchorMin = new Vector2(1f, 0f);
            noteRect.anchorMax = new Vector2(1f, 1f);
            noteRect.pivot = new Vector2(1f, 0.5f);
            noteRect.anchoredPosition = new Vector2(-12f, 0f);
            noteRect.sizeDelta = new Vector2(150f, 0f);

            return new LeaderboardHud.Row { rect = rect, swatch = swatch, place = place, name = name, note = note };
        }

        // ------------------------------------------------------------------ helpers

        static RectTransform NewRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        static TMP_Text NewText(Transform parent, string name, string content, float size, Color color,
            FontStyles style, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Place a text box at a fixed left offset and width, full row height.</summary>
        static void SetBox(RectTransform rect, float left, float width)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width, 0f);
        }
    }
}
