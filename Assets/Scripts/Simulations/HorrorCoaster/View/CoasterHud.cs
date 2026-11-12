using System.Collections.Generic;
using SimulationLobby.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// The on-screen text of the haunted coaster: the <c>RIDERS n</c> counter with six seat pips, the hook caption, the
    /// TRICK? / TREAT? cue caption, the opening warning card and the end card. uGUI Canvas with TextMeshPro only (CLAUDE.md
    /// On-screen UI), Screen Space Overlay, Scale With Screen Size at 1080x1920 matching height, so the same scene reads at
    /// 9:16 and 16:9 from the same anchors.
    /// </summary>
    /// <remarks>
    /// <b>Layout.</b> Everything hangs off the edges and thirds; the middle band of the frame stays clear after the opening card.
    /// Counter + pips: top-left. Cue caption: upper band (y 0.73-0.90, huge, thick outline; the central 40 percent, y 0.3-0.7, stays clear). Hook caption and end card: lower third. At 16:9 the canvas is
    /// 3413 x 1920 reference units, so edge-anchored items sit in the corners and the stretched captions auto-size to their
    /// box; at 9:16 the same boxes are 1080 wide and the text auto-sizes down and wraps to two lines. Read-only: it reads the
    /// state in <c>Update</c> and never writes it. Text outlines are applied at runtime in <c>Awake</c> so no material
    /// instances get serialised into the scene.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CoasterHud : MonoBehaviour
    {
        static readonly Color Orange = new Color(1f, 0.478f, 0.094f, 1f);
        static readonly Color Bone = new Color(0.929f, 0.902f, 0.816f, 1f);
        static readonly Color Blood = new Color(0.86f, 0.1f, 0.1f, 1f);
        const int PipCount = 6;
        const string OrangeHex = "FF7A18";
        const string BoneHex = "EDE6D0";

        [Header("Elements (set by Build)")]
        [SerializeField] TextMeshProUGUI _ridersText;
        [SerializeField] RectTransform _ridersRect;
        [SerializeField] Image[] _pips = new Image[0];
        [SerializeField] CanvasGroup _hookGroup;
        [SerializeField] TextMeshProUGUI _hookText;
        [SerializeField] CanvasGroup _cueGroup;
        [SerializeField] RectTransform _cueRect;
        [SerializeField] TextMeshProUGUI _cueText;
        [SerializeField] CanvasGroup _warningGroup;
        [SerializeField] CanvasGroup _endGroup;
        [SerializeField] TextMeshProUGUI _endText;
        [SerializeField] TextMeshProUGUI _titleText;
        [SerializeField] TextMeshProUGUI _riskText;

        [Header("Copy")]
        [SerializeField] string _hookCaption = "6 RIDERS. EVERY SCARE TAKES ONE.";
        [SerializeField] string _endCaption = "YOU WERE THE <color=#" + OrangeHex + ">SIXTH</color> RIDER.";

        [Header("Timing (sim seconds)")]
        [SerializeField] float _warningSeconds = 5.5f;
        [SerializeField] float _hookStart = 5.5f;
        [SerializeField] float _hookEnd = 14f;
        [Tooltip("The end card shows once the cart is this close to the end of the rail (m) in the StationReturn act.")]
        [SerializeField] float _endCardRemainingMeters = 14f;

        ICoasterState _state;
        int _shownRiders = -1;
        float _punch;
        float _lastSim = -1f;
        float _endStart = -1f;
        string _shownCue = "";
        static Sprite s_circle;
        static readonly Color DeepBlood = new Color(0.78f, 0.04f, 0.04f, 1f);

        public void Bind(ICoasterState state)
        {
            _state = state;
            _shownRiders = -1;
            _lastSim = -1f;
            _endStart = -1f;
            _punch = 0f;
            _shownCue = "";
        }

        // ------------------------------------------------------------------ build

        /// <summary>Creates the HUD canvas under <paramref name="parent"/> and returns its component. Safe to call in edit mode.</summary>
        public static CoasterHud Build(Transform parent)
        {
            var root = new GameObject("Coaster HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var hud = root.AddComponent<CoasterHud>();

            // --- Rider counter, top-left.
            RectTransform riders = NewRect("Riders", root.transform);
            riders.anchorMin = new Vector2(0f, 1f);
            riders.anchorMax = new Vector2(0f, 1f);
            riders.pivot = new Vector2(0f, 1f);
            riders.anchoredPosition = new Vector2(56f, -44f);
            riders.sizeDelta = new Vector2(860f, 170f);
            hud._ridersRect = riders;
            hud._ridersText = NewText("Riders Text", riders, "", 140f, Orange, TextAlignmentOptions.TopLeft);
            Stretch(hud._ridersText.rectTransform);

            // --- Seat pips under the counter: five seats ahead (front first) then the viewer.
            RectTransform pipRow = NewRect("Seat Pips", root.transform);
            pipRow.anchorMin = new Vector2(0f, 1f);
            pipRow.anchorMax = new Vector2(0f, 1f);
            pipRow.pivot = new Vector2(0f, 1f);
            pipRow.anchoredPosition = new Vector2(62f, -214f);
            pipRow.sizeDelta = new Vector2(PipCount * 76f, 64f);
            hud._pips = new Image[PipCount];
            for (int i = 0; i < PipCount; i++)
            {
                var pip = new GameObject("Pip " + i, typeof(RectTransform), typeof(Image));
                pip.transform.SetParent(pipRow, false);
                var rt = (RectTransform)pip.transform;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(i * 76f, 0f);
                rt.sizeDelta = new Vector2(58f, 58f);
                var img = pip.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = Orange;
                hud._pips[i] = img;
            }

            // --- Cue caption (TRICK? / TREAT?), upper third.
            RectTransform cue = NewRect("Cue", root.transform);
            cue.anchorMin = new Vector2(0.04f, 0.73f);
            cue.anchorMax = new Vector2(0.96f, 0.90f);
            cue.offsetMin = Vector2.zero;
            cue.offsetMax = Vector2.zero;
            hud._cueRect = cue;
            hud._cueGroup = AddGroup(cue.gameObject);
            hud._cueText = NewText("Cue Text", cue, "", 260f, Orange, TextAlignmentOptions.Center);
            Stretch(hud._cueText.rectTransform);
            AutoSize(hud._cueText, 110f, 260f);

            // --- Hook caption, lower third.
            RectTransform hook = NewRect("Hook", root.transform);
            hook.anchorMin = new Vector2(0.06f, 0.09f);
            hook.anchorMax = new Vector2(0.94f, 0.22f);
            hook.offsetMin = Vector2.zero;
            hook.offsetMax = Vector2.zero;
            hud._hookGroup = AddGroup(hook.gameObject);
            hud._hookText = NewText("Hook Text", hook, hud._hookCaption, 100f, Bone, TextAlignmentOptions.Center);
            Stretch(hud._hookText.rectTransform);
            AutoSize(hud._hookText, 44f, 100f);

            // --- End card, lower third on a dark band.
            RectTransform end = NewRect("End Card", root.transform);
            end.anchorMin = new Vector2(0f, 0.08f);
            end.anchorMax = new Vector2(1f, 0.26f);
            end.offsetMin = Vector2.zero;
            end.offsetMax = Vector2.zero;
            hud._endGroup = AddGroup(end.gameObject);
            var band = new GameObject("Band", typeof(RectTransform), typeof(Image));
            band.transform.SetParent(end, false);
            Stretch((RectTransform)band.transform);
            var bandImage = band.GetComponent<Image>();
            bandImage.color = new Color(0f, 0f, 0f, 0.55f);
            bandImage.raycastTarget = false;
            hud._endText = NewText("End Text", end, hud._endCaption, 120f, Bone, TextAlignmentOptions.Center);
            hud._endText.rectTransform.anchorMin = new Vector2(0.05f, 0f);
            hud._endText.rectTransform.anchorMax = new Vector2(0.95f, 1f);
            hud._endText.rectTransform.offsetMin = Vector2.zero;
            hud._endText.rectTransform.offsetMax = Vector2.zero;
            AutoSize(hud._endText, 50f, 120f);

            // --- Opening warning card, full screen, on top.
            RectTransform warning = NewRect("Warning Card", root.transform);
            Stretch(warning);
            hud._warningGroup = AddGroup(warning.gameObject);
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(warning, false);
            Stretch((RectTransform)bg.transform);
            var bgImage = bg.GetComponent<Image>();
            bgImage.color = new Color(0.02f, 0.01f, 0.04f, 0.94f);
            bgImage.raycastTarget = false;

            // Title card: the channel-of-nightmares name and the dare, in horror display faces (loaded in Awake).
            hud._titleText = NewText("Title", warning, "HAUNTED COASTER", 260f, DeepBlood, TextAlignmentOptions.Center);
            hud._titleText.rectTransform.anchorMin = new Vector2(0.03f, 0.64f);
            hud._titleText.rectTransform.anchorMax = new Vector2(0.97f, 0.90f);
            hud._titleText.rectTransform.offsetMin = Vector2.zero;
            hud._titleText.rectTransform.offsetMax = Vector2.zero;
            AutoSize(hud._titleText, 80f, 260f);

            hud._riskText = NewText("Enter At Your Own Risk", warning, "ENTER AT YOUR OWN RISK", 100f, Bone, TextAlignmentOptions.Center);
            hud._riskText.rectTransform.anchorMin = new Vector2(0.04f, 0.52f);
            hud._riskText.rectTransform.anchorMax = new Vector2(0.96f, 0.63f);
            hud._riskText.rectTransform.offsetMin = Vector2.zero;
            hud._riskText.rectTransform.offsetMax = Vector2.zero;
            AutoSize(hud._riskText, 30f, 100f);

            TextMeshProUGUI title = NewText("Warning Title", warning, "FLASHING LIGHTS AND LOUD SOUNDS", 90f, Orange, TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = new Vector2(0.08f, 0.36f);
            title.rectTransform.anchorMax = new Vector2(0.92f, 0.46f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;
            AutoSize(title, 30f, 80f);

            TextMeshProUGUI sub = NewText("Warning Sub", warning, "Headphones recommended.", 64f, Bone, TextAlignmentOptions.Center);
            sub.rectTransform.anchorMin = new Vector2(0.08f, 0.26f);
            sub.rectTransform.anchorMax = new Vector2(0.92f, 0.34f);
            sub.rectTransform.offsetMin = Vector2.zero;
            sub.rectTransform.offsetMax = Vector2.zero;
            AutoSize(sub, 26f, 56f);

            hud._hookGroup.alpha = 0f;
            hud._cueGroup.alpha = 0f;
            hud._endGroup.alpha = 0f;
            hud._warningGroup.alpha = 1f;
            return hud;
        }

        static RectTransform NewRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static CanvasGroup AddGroup(GameObject go)
        {
            var g = go.AddComponent<CanvasGroup>();
            g.interactable = false;
            g.blocksRaycasts = false;
            return g;
        }

        static TextMeshProUGUI NewText(string objectName, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = FontStyles.Bold;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static void AutoSize(TextMeshProUGUI t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
            t.overflowMode = TextOverflowModes.Truncate;
        }

        // ------------------------------------------------------------------ runtime

        void Awake()
        {
            // Outlines are material properties; applying them here (not in Build) keeps material instances out of the saved scene.
            TMP_FontAsset creepster = HorrorFace("Creepster");
            TMP_FontAsset nosifer = HorrorFace("Nosifer");
            TMP_FontAsset eater = HorrorFace("Eater");
            // Order matters: a text that has had its outline touched keeps its old material instance, and a later font swap then renders the new
            // font's glyphs from the OLD atlas (overlapping junk). So the horror faces go on first, each setting its own outline, and only the
            // remaining texts get the generic outline. Missing TTFs leave the default face.
            ApplyFace(_titleText, creepster, 0.3f);
            ApplyFace(_riskText, nosifer != null ? nosifer : eater, 0.18f);
            ApplyFace(_cueText, creepster, 0.34f);
            ApplyFace(_ridersText, creepster, 0.22f);
            ApplyFace(_endText, creepster, 0.24f);
            ApplyFace(_hookText, eater, 0.2f);
            foreach (TextMeshProUGUI t in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t == _titleText || t == _riskText || t == _cueText || t == _ridersText || t == _endText || t == _hookText)
                {
                    if (t.font != null && t.font.name.EndsWith("(runtime)"))
                    {
                        continue;
                    }
                }

                t.outlineWidth = t == _cueText ? 0.34f : 0.22f;
                t.outlineColor = new Color32(0, 0, 0, 255);
            }

            Sprite circle = CircleSprite();
            if (_pips != null)
            {
                for (int i = 0; i < _pips.Length; i++)
                {
                    if (_pips[i] != null)
                    {
                        _pips[i].sprite = circle;
                    }
                }
            }
        }

        static readonly Dictionary<string, TMP_FontAsset> s_faces = new Dictionary<string, TMP_FontAsset>();

        /// <summary>
        /// A horror display face built in memory from the TTF in Resources/CoasterFonts. Built at runtime on purpose: font assets generated in the
        /// editor and saved had atlases that did not match their glyph tables and rendered as overlapping junk. Null when the TTF is missing.
        /// </summary>
        static TMP_FontAsset HorrorFace(string name)
        {
            if (s_faces.TryGetValue(name, out TMP_FontAsset cached) && cached != null)
            {
                return cached;
            }

            var ttf = Resources.Load<Font>("CoasterFonts/" + name + "-Regular");
            TMP_FontAsset fa = ttf != null
                ? TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true)
                : null;
            if (fa != null)
            {
                fa.name = name + " SDF (runtime)";
            }

            s_faces[name] = fa;
            return fa;
        }

        static void ApplyFace(TextMeshProUGUI t, TMP_FontAsset face, float outline)
        {
            if (t == null || face == null)
            {
                return;
            }

            // Swapping the font alone leaves the text on its old material instance (the default font's atlas), so the new font's glyph
            // rectangles are read from the wrong texture and render as overlapping junk. Hand it the new font's material as well.
            t.font = face;
            t.fontSharedMaterial = face.material;
            t.fontStyle = FontStyles.Normal;
            t.outlineWidth = outline;
            t.outlineColor = new Color32(0, 0, 0, 255);
        }

        static Sprite CircleSprite()
        {
            if (s_circle != null)
            {
                return s_circle;
            }

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CoasterPip (runtime)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01((c - d) / 1.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            s_circle.name = "CoasterPip (runtime)";
            return s_circle;
        }

        void Update()
        {
            if (_state == null || _ridersText == null)
            {
                return;
            }

            float t = _state.SimTime;
            if (t < _lastSim - 0.01f)
            {
                Bind(_state);
            }

            _lastSim = t;

            UpdateWarning(t);
            UpdateHook(t);
            UpdateRiders();
            UpdateCue();
            UpdateEndCard(t);
        }

        void UpdateWarning(float t)
        {
            float a = t < _warningSeconds - 0.5f ? 1f : Mathf.Clamp01((_warningSeconds - t) / 0.5f);
            _warningGroup.alpha = a;
            _warningGroup.gameObject.SetActive(a > 0.001f);
            if (_titleText != null && a > 0.001f)
            {
                // A guttering-candle flicker (slow, bounded well under 3 Hz of full-frame change) and a slow creep-in of scale.
                float flick = 0.88f + 0.12f * Mathf.Sin(t * 7.1f) * Mathf.Sin(t * 2.3f + 1f);
                Color c = DeepBlood;
                c.a = flick;
                _titleText.color = c;
                _titleText.rectTransform.localScale = Vector3.one * (0.94f + 0.06f * Mathf.Clamp01(t / _warningSeconds));
                if (_riskText != null)
                {
                    _riskText.color = new Color(Bone.r, Bone.g, Bone.b, Mathf.Clamp01((t - 0.9f) / 0.8f));
                }
            }
        }

        void UpdateHook(float t)
        {
            float a = Mathf.Clamp01((t - _hookStart) / 0.4f) * Mathf.Clamp01((_hookEnd - t) / 0.6f);
            _hookGroup.alpha = a;
        }

        void UpdateRiders()
        {
            int left = _state.RidersLeft;
            if (left != _shownRiders)
            {
                if (_shownRiders >= 0 && left < _shownRiders)
                {
                    _punch = 1f;
                }

                _shownRiders = left;
                _ridersText.text = "<color=#" + BoneHex + "><size=48%>RIDERS</size></color> " + left;
            }

            if (_punch > 0f)
            {
                _punch = Mathf.Max(0f, _punch - Time.deltaTime / 0.6f);
            }

            float ease = CameraSpring.SmoothStep01(_punch);
            _ridersText.color = Color.Lerp(Orange, Blood, ease);
            _ridersRect.localScale = Vector3.one * (1f + 0.22f * ease);

            IReadOnlyList<bool> present = _state.RiderPresent;
            for (int j = 0; j < _pips.Length; j++)
            {
                if (_pips[j] == null)
                {
                    continue;
                }

                bool you = j == PipCount - 1;
                int seat = PipCount - 2 - j;
                bool aboard = you || (present != null && seat >= 0 && seat < present.Count && present[seat]);
                Color c = you ? Bone : (aboard ? Orange : new Color(0.22f, 0.18f, 0.28f, 0.8f));
                _pips[j].color = c;
                _pips[j].rectTransform.localScale = Vector3.one * (you ? 1.08f : 1f);
            }
        }

        void UpdateCue()
        {
            float alpha = 0f;
            float scale = 1f;
            ScarePhase phase = _state.Phase;
            string cue = phase == ScarePhase.Idle || phase == ScarePhase.Aftermath ? "" : (_state.CurrentEvent.cueCaption ?? "");

            if (cue.Length > 0 && (phase == ScarePhase.PreQuiet || phase == ScarePhase.Sting))
            {
                if (cue != _shownCue)
                {
                    _shownCue = cue;
                    _cueText.text = cue;
                }

                float pop = phase == ScarePhase.PreQuiet ? Mathf.Clamp01(_state.PhaseTime / 0.25f) : 1f;
                scale = Mathf.Lerp(0.6f, 1f, CameraSpring.SmoothStep01(pop)) + 0.15f * Mathf.Sin(pop * Mathf.PI);
                alpha = pop;
                if (phase == ScarePhase.Sting)
                {
                    alpha = Mathf.Clamp01(1f - _state.PhaseTime / 0.3f);
                }
            }
            else if (phase == ScarePhase.Idle || phase == ScarePhase.Aftermath)
            {
                _shownCue = "";
            }

            _cueGroup.alpha = alpha;
            _cueRect.localScale = Vector3.one * scale;
        }

        void UpdateEndCard(float t)
        {
            bool near = false;
            if (_state.Act == ActId.StationReturn)
            {
                float length = _state.Rail != null ? _state.Rail.Length : 0f;
                near = _state.IsComplete || (length > 0f && length - _state.Distance <= _endCardRemainingMeters);
            }

            if (near)
            {
                if (_endStart < 0f)
                {
                    _endStart = t;
                }

                _endGroup.alpha = CameraSpring.SmoothStep01((t - _endStart) / 1f);
            }
            else
            {
                _endStart = -1f;
                _endGroup.alpha = 0f;
            }
        }
    }
}
