using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.Timer.EditorTools
{
    /// <summary>
    /// Builds the restrained corner canvas a <see cref="TimerPresenter"/> drives. Screen Space Overlay, Scale With Screen Size at
    /// 1920x1080 (match 0.5). Every element is anchored to its own corner (or top-centre) with the pivot in that corner and a
    /// 40 px margin, so nothing leaves the view at 16:9, 16:10, 4:3 or 9:16, and nothing sits below 85% of the screen height.
    /// Sprites and text materials are saved under Assets/Settings/Timer/UI.
    /// </summary>
    public static class TimerHudBuilder
    {
        const string UiFolder = "Assets/Settings/Timer/UI";
        const string SpritePath = UiFolder + "/RoundedRect.png";
        const int SpriteSize = 128;
        const float SpriteRadius = 48f;
        const float SpriteBorder = 52f;
        const float Margin = 40f;

        static readonly Color Ink = new Color(0.03f, 0.04f, 0.09f, 0.9f);
        static readonly Color Pill = new Color(0.05f, 0.07f, 0.13f, 0.5f);
        static readonly Color Track = new Color(1f, 1f, 1f, 0.16f);
        static readonly Color Fill = new Color(0.93f, 0.96f, 1f, 0.92f);
        static readonly Color MarbleTint = new Color(0.55f, 0.82f, 1f, 1f);

        public static GameObject Build(TimerSimulation sim)
        {
            Sprite round = EnsureSprite();
            Material heavy = EnsureTextMaterial("TimerHud_Heavy", 0.14f, true);
            Material lite = EnsureTextMaterial("TimerHud_Lite", 0.12f, false);

            var canvasObject = new GameObject("Timer HUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var presenter = canvasObject.AddComponent<TimerPresenter>();
            Transform root = canvasObject.transform;

            // TOP-RIGHT countdown chip (230x96) with a thin progress bar directly under it.
            RectTransform badge = Corner(root, "CountdownChip", new Vector2(1f, 1f), 230f, 96f, -Margin, -Margin);
            Slice(Stretch(badge, "Pill", Vector2.zero, Vector2.zero), round, Pill, 1f);
            TMP_Text countdown = Text(badge, "Countdown", "<mspace=0.6em>5:00</mspace>", 68f, Color.white, heavy, TextAlignmentOptions.Center);
            Stretch(countdown.rectTransform, Vector2.zero, Vector2.zero);

            RectTransform bar = Corner(root, "ProgressBar", new Vector2(1f, 1f), 230f, 8f, -Margin, -Margin - 96f - 10f);
            Slice(Stretch(bar, "Track", Vector2.zero, Vector2.zero), round, Track, 4f);
            RectTransform fill = Stretch(bar, "Fill", Vector2.zero, Vector2.zero);
            fill.anchorMax = new Vector2(0f, 1f);
            Image fillImage = Slice(fill, round, Fill, 4f);

            // Setup line (STARTING IN N, then GO!): small pill right under the bar.
            RectTransform setup = Corner(root, "SetupPill", new Vector2(1f, 1f), 280f, 56f, -Margin, -Margin - 96f - 10f - 8f - 12f);
            var setupGroup = setup.gameObject.AddComponent<CanvasGroup>();
            setupGroup.alpha = 1f;
            setupGroup.blocksRaycasts = false;
            setupGroup.interactable = false;
            Slice(Stretch(setup, "Pill", Vector2.zero, Vector2.zero), round, Pill, 1f);
            TMP_Text setupText = Text(setup, "Label", "STARTING IN 5", 34f, Color.white, lite, TextAlignmentOptions.Center);
            Stretch(setupText.rectTransform, new Vector2(10f, 0f), new Vector2(-10f, 0f));

            // TOP-LEFT marbles chip (220x64): a tinted marble dot, "IN THE BUCKET" and "123 / 300".
            RectTransform chip = Corner(root, "MarbleChip", new Vector2(0f, 1f), 280f, 84f, Margin, -Margin);
            Slice(Stretch(chip, "Pill", Vector2.zero, Vector2.zero), round, Pill, 1f);
            var dot = new GameObject("MarbleDot", typeof(RectTransform));
            dot.transform.SetParent(chip, false);
            var dotRect = (RectTransform)dot.transform;
            dotRect.anchorMin = new Vector2(0f, 0.5f);
            dotRect.anchorMax = new Vector2(0f, 0.5f);
            dotRect.pivot = new Vector2(0f, 0.5f);
            dotRect.sizeDelta = new Vector2(26f, 26f);
            dotRect.anchoredPosition = new Vector2(20f, 0f);
            Slice(dotRect, round, MarbleTint, 1f);
            TMP_Text chipText = Text(chip, "Count", "<size=22>IN THE BUCKET</size>\n0 / 300", 40f, Color.white, lite, TextAlignmentOptions.Center);
            Stretch(chipText.rectTransform, new Vector2(54f, 0f), new Vector2(-14f, 0f));

            // Top-centre cards (anchor top-centre, pivot centre; the presenter sizes and places them from the canvas width).
            // Question card: big during setup, then a small persistent subtitle.
            Card(root, "QuestionCard", Pill, 62f, heavy, round, out RectTransform question, out CanvasGroup questionGroup, out TMP_Text questionText, false);
            Card(root, "QuestionSubtitle", new Color(0.05f, 0.07f, 0.13f, 0.42f), 29f, lite, round, out RectTransform subtitle, out CanvasGroup subtitleGroup, out TMP_Text subtitleText, false);
            // Result card at 0:00 ("247 OF 300 MADE IT").
            Card(root, "ResultCard", new Color(0.05f, 0.07f, 0.13f, 0.62f), 70f, heavy, round, out RectTransform card, out CanvasGroup cardGroup, out TMP_Text cardText, false);
            // All-in celebration card: gold rim, gold text.
            Card(root, "AllInCard", new Color(0.2f, 0.13f, 0.02f, 0.82f), 90f, heavy, round, out RectTransform allIn, out CanvasGroup allInGroup, out TMP_Text allInText, true);
            allInText.color = new Color(1f, 0.86f, 0.28f, 1f);

            // Subtle full-screen flash for the all-in moment, behind everything else.
            var flashObject = new GameObject("AllInFlash", typeof(RectTransform));
            flashObject.transform.SetParent(root, false);
            flashObject.transform.SetAsFirstSibling();
            Stretch((RectTransform)flashObject.transform, Vector2.zero, Vector2.zero);
            var flash = flashObject.AddComponent<Image>();
            flash.color = new Color(1f, 0.9f, 0.5f, 0f);
            flash.raycastTarget = false;

            Wire(presenter, sim, badge, countdown, fill, fillImage, chipText, setup, setupGroup, setupText, card, cardGroup, cardText);
            var so = new SerializedObject(presenter);
            Set(so, "questionRoot", question);
            Set(so, "questionGroup", questionGroup);
            Set(so, "questionText", questionText);
            Set(so, "subtitleRoot", subtitle);
            Set(so, "subtitleGroup", subtitleGroup);
            Set(so, "subtitleText", subtitleText);
            Set(so, "allInRoot", allIn);
            Set(so, "allInGroup", allInGroup);
            Set(so, "allInText", allInText);
            Set(so, "flashImage", flash);
            so.ApplyModifiedPropertiesWithoutUndo();
            return canvasObject;
        }

        static void Set(SerializedObject so, string name, Object value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null)
            {
                p.objectReferenceValue = value;
            }
            else
            {
                Debug.LogWarning("[timer] TimerPresenter has no serialized field '" + name + "'.");
            }
        }

        // A translucent rounded pill with wrapped, centred text. The presenter sets the size; the text keeps 28x14 px of padding.
        static void Card(Transform parent, string name, Color pillColor, float fontSize, Material material, Sprite round,
            out RectTransform rect, out CanvasGroup group, out TMP_Text text, bool gold)
        {
            rect = Corner(parent, name, new Vector2(0.5f, 1f), 900f, 120f, 0f, -Margin - 60f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            if (gold)
            {
                Slice(Stretch(rect, "Rim", new Vector2(-7f, -7f), new Vector2(7f, 7f)), round, new Color(1f, 0.8f, 0.2f, 0.95f), 1f);
            }

            Slice(Stretch(rect, "Pill", Vector2.zero, Vector2.zero), round, pillColor, 1f);
            text = Text(rect, "Title", "", fontSize, Color.white, material, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            Stretch(text.rectTransform, new Vector2(28f, 14f), new Vector2(-28f, -14f));
        }

        static void Wire(TimerPresenter presenter, TimerSimulation sim, RectTransform badge, TMP_Text countdown,
            RectTransform fill, Image fillImage, TMP_Text chipText, RectTransform setup, CanvasGroup setupGroup, TMP_Text setupText,
            RectTransform card, CanvasGroup cardGroup, TMP_Text cardText)
        {
            var so = new SerializedObject(presenter);
            so.FindProperty("sim").objectReferenceValue = sim;
            so.FindProperty("badgeRoot").objectReferenceValue = badge;
            so.FindProperty("countdownText").objectReferenceValue = countdown;
            so.FindProperty("barFill").objectReferenceValue = fill;
            so.FindProperty("barFillImage").objectReferenceValue = fillImage;
            so.FindProperty("chipText").objectReferenceValue = chipText;
            so.FindProperty("setupRoot").objectReferenceValue = setup;
            so.FindProperty("setupGroup").objectReferenceValue = setupGroup;
            so.FindProperty("setupText").objectReferenceValue = setupText;
            so.FindProperty("timesUpRoot").objectReferenceValue = card;
            so.FindProperty("timesUpGroup").objectReferenceValue = cardGroup;
            so.FindProperty("timesUpText").objectReferenceValue = cardText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Corner-anchored element: anchor and pivot at the same corner/edge point, so it never leaves the screen at any aspect.
        static RectTransform Corner(Transform parent, string name, Vector2 anchor, float width, float height, float x, float y)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }

        static RectTransform Stretch(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect, offsetMin, offsetMax);
            return rect;
        }

        static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static Image Slice(RectTransform rect, Sprite sprite, Color color, float pixelsPerUnitMultiplier)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text Text(Transform parent, string name, string content, float size, Color color, Material material, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (material != null)
            {
                text.fontSharedMaterial = material;
            }

            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // A white rounded rectangle with 9-slice borders, tinted by Image.color wherever it is used.
        static Sprite EnsureSprite()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Settings/Timer/UI"));
            if (!File.Exists(SpritePath))
            {
                var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
                float half = SpriteSize * 0.5f;
                float inner = half - SpriteRadius;
                var pixels = new Color32[SpriteSize * SpriteSize];
                for (int y = 0; y < SpriteSize; y++)
                {
                    for (int x = 0; x < SpriteSize; x++)
                    {
                        float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                        float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                        float distance = Mathf.Sqrt(dx * dx + dy * dy) - SpriteRadius;
                        byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
                        pixels[y * SpriteSize + x] = new Color32(255, 255, 255, alpha);
                    }
                }

                texture.SetPixels32(pixels);
                File.WriteAllBytes(Path.Combine(Application.dataPath, "Settings/Timer/UI/RoundedRect.png"), texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = new Vector4(SpriteBorder, SpriteBorder, SpriteBorder, SpriteBorder);
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        // The project's TMP font with a thin dark outline (and optionally a soft, subtle drop shadow), saved as a material asset
        // so the scene keeps referencing it after a reload.
        static Material EnsureTextMaterial(string name, float outline, bool shadow)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null || font.material == null)
            {
                return null;
            }

            string path = UiFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            SetFloat(material, "_OutlineWidth", outline);
            SetColor(material, "_OutlineColor", Ink);
            material.EnableKeyword("OUTLINE_ON");
            if (shadow)
            {
                SetColor(material, "_UnderlayColor", new Color(0f, 0f, 0.05f, 0.3f));
                SetFloat(material, "_UnderlayOffsetX", 0.3f);
                SetFloat(material, "_UnderlayOffsetY", -0.3f);
                SetFloat(material, "_UnderlayDilate", 0f);
                SetFloat(material, "_UnderlaySoftness", 0.5f);
                material.EnableKeyword("UNDERLAY_ON");
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }
    }
}
