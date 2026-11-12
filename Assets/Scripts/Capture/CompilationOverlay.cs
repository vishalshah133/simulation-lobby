using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Capture
{
    /// <summary>
    /// The persistent top-most canvas of a compilation: transitions, the segment banner and the intro/outro
    /// cards. Built entirely at runtime (uGUI + TextMeshPro, no scene dependency, no custom shader) and
    /// driven by <see cref="CompilationPlayer"/>. Presentation only: it never touches a simulation.
    /// </summary>
    public sealed class CompilationOverlay : MonoBehaviour
    {
        public enum Phase { Close, Open }

        const float RefW = 1920f;
        const float RefH = 1080f;
        const float WipeEdge = 60f;
        const float Big = 6000f;
        const int HoleTex = 1024;
        const float HoleTexRadius = 256f;

        static readonly Color CardBg = new Color(0.04f, 0.05f, 0.09f, 1f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
        static readonly Color DotOff = new Color(0.22f, 0.25f, 0.33f, 1f);
        static readonly Color WipeColor = new Color(0.05f, 0.07f, 0.16f, 1f);
        static readonly Color WipeAccent = new Color(0.2f, 0.9f, 1f, 1f);

        RectTransform _root;
        GameObject _cardRoot;
        TextMeshProUGUI _cardTitle;
        TextMeshProUGUI _cardSub;
        Image[] _dots;

        Image _fade;
        Image _flash;
        RectTransform _wipe;
        RectTransform _wipeStrip;
        RectTransform _irisRoot;
        RectTransform _irisHole;
        RectTransform _irisL, _irisR, _irisT, _irisB;

        TextMeshProUGUI _bannerNumber;
        TextMeshProUGUI _bannerLabel;
        CanvasGroup _bannerGroup;

        float _cardTime;
        float _cardDuration = 1f;
        bool _cardProgressive;
        bool _cardVisible;

        Sprite _circle;
        Sprite _hole;

        /// <summary>Create the overlay as a child of <paramref name="parent"/>.</summary>
        public static CompilationOverlay Create(Transform parent)
        {
            var go = new GameObject("CompilationOverlay", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var overlay = go.AddComponent<CompilationOverlay>();
            overlay.Build();
            return overlay;
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // 16:9-only series, so the reference is landscape; match height like the rest of the project.
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            _root = (RectTransform)transform;
            _circle = MakeCircleSprite(64, 30f, false);
            _hole = MakeCircleSprite(HoleTex, HoleTexRadius, true);

            BuildCard();
            BuildIris();
            BuildWipe();
            _fade = MakePanel("Fade", _root, Color.black);
            _flash = MakePanel("Flash", _root, Color.white);
            BuildBanner();

            SetCard(false);
            SetCovered(Color.black);
        }

        void BuildCard()
        {
            var bg = MakePanel("Card", _root, CardBg);
            _cardRoot = bg.gameObject;

            _cardTitle = MakeText("Title", bg.rectTransform, 170, Color.white, FontStyles.Bold);
            Anchor(_cardTitle.rectTransform, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.8f));
            _cardSub = MakeText("Sub", bg.rectTransform, 76, Gold, FontStyles.Bold);
            Anchor(_cardSub.rectTransform, new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.5f));

            _dots = new Image[10];
            const float spacing = 78f;
            for (int i = 0; i < _dots.Length; i++)
            {
                var d = new GameObject("Dot" + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)d.transform;
                rt.SetParent(bg.rectTransform, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.2f);
                rt.sizeDelta = new Vector2(40f, 40f);
                rt.anchoredPosition = new Vector2((i - (_dots.Length - 1) * 0.5f) * spacing, 0f);
                var img = d.GetComponent<Image>();
                img.sprite = _circle;
                img.color = DotOff;
                img.raycastTarget = false;
                _dots[i] = img;
            }
        }

        void BuildIris()
        {
            _irisRoot = NewRect("Iris", _root);
            Stretch(_irisRoot);
            _irisL = IrisPanel("L", new Vector2(1f, 0.5f));
            _irisR = IrisPanel("R", new Vector2(0f, 0.5f));
            _irisT = IrisPanel("T", new Vector2(0.5f, 0f));
            _irisB = IrisPanel("B", new Vector2(0.5f, 1f));

            var h = new GameObject("Hole", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _irisHole = (RectTransform)h.transform;
            _irisHole.SetParent(_irisRoot, false);
            _irisHole.anchorMin = _irisHole.anchorMax = new Vector2(0.5f, 0.5f);
            var img = h.GetComponent<Image>();
            img.sprite = _hole;
            img.raycastTarget = false;
        }

        RectTransform IrisPanel(string name, Vector2 pivot)
        {
            var p = new GameObject("Iris" + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)p.transform;
            rt.SetParent(_irisRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot;
            var img = p.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            return rt;
        }

        void BuildWipe()
        {
            _wipe = NewRect("Wipe", _root);
            _wipe.anchorMin = _wipe.anchorMax = new Vector2(0.5f, 0.5f);
            var img = _wipe.gameObject.AddComponent<Image>();
            img.color = WipeColor;
            img.raycastTarget = false;

            var s = NewRect("Strip", _wipe);
            _wipeStrip = s;
            var sImg = s.gameObject.AddComponent<Image>();
            sImg.color = WipeAccent;
            sImg.raycastTarget = false;
        }

        void BuildBanner()
        {
            var g = NewRect("Banner", _root);
            Stretch(g);
            _bannerGroup = g.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.interactable = false;
            _bannerGroup.alpha = 0f;

            _bannerNumber = MakeText("Number", g, 190, Color.white, FontStyles.Bold);
            Anchor(_bannerNumber.rectTransform, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.7f));
            _bannerLabel = MakeText("Label", g, 72, Gold, FontStyles.Bold);
            Anchor(_bannerLabel.rectTransform, new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.45f));
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Instantly set the screen to fully covered black (start of the run, after the outro).</summary>
        public void SnapCovered()
        {
            SetState(CompilationTransition.FadeBlack, Phase.Close, 1f);
            SetBanner(string.Empty, string.Empty, false, 0f);
        }

        /// <summary>
        /// Set the cover to <paramref name="t"/> (0..1) of <paramref name="phase"/>. Close: t 0 = open, 1 = covered.
        /// Open: t 0 = covered, 1 = open. Caller supplies eased time.
        /// </summary>
        public void SetState(CompilationTransition type, Phase phase, float t)
        {
            t = Mathf.Clamp01(t);
            HideCovers();

            switch (type)
            {
                case CompilationTransition.FadeBlack:
                    _fade.color = new Color(0f, 0f, 0f, phase == Phase.Close ? t : 1f - t);
                    break;

                case CompilationTransition.FlashWhite:
                    _flash.color = new Color(1f, 1f, 1f, phase == Phase.Close ? t : 1f - t);
                    break;

                case CompilationTransition.WipeLeft:
                case CompilationTransition.WipeRight:
                    SetWipe(type == CompilationTransition.WipeLeft, phase, t);
                    break;

                case CompilationTransition.IrisClose:
                    SetIris(phase == Phase.Close ? 1f - t : t);
                    break;
            }
        }

        /// <summary>Banner alpha rides the covered part of a cut so it never shows over live footage.</summary>
        public void SetBannerForPhase(Phase phase, float t, CompilationTransition type)
        {
            float a = phase == Phase.Close ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, t))
                                           : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.5f, t));
            _bannerGroup.alpha = a;
            Color c = type == CompilationTransition.FlashWhite ? new Color(0.05f, 0.05f, 0.12f, 1f) : Color.white;
            _bannerNumber.color = c;
            _bannerLabel.color = type == CompilationTransition.FlashWhite ? new Color(0.75f, 0.35f, 0f, 1f) : Gold;
        }

        public void SetBanner(string number, string label, bool visible, float alpha)
        {
            _bannerNumber.text = number;
            _bannerLabel.text = label;
            _bannerGroup.alpha = visible ? alpha : 0f;
        }

        /// <summary>Show a title card. <paramref name="progressive"/> lights the ten dots across the duration.</summary>
        public void ShowCard(string title, string sub, float titleSize, bool progressive, float duration)
        {
            _cardTitle.text = title;
            _cardTitle.fontSize = titleSize;
            _cardSub.text = sub;
            _cardProgressive = progressive;
            _cardDuration = Mathf.Max(0.1f, duration);
            _cardTime = 0f;
            SetCard(true);
            UpdateCard();
        }

        public void HideCard() { SetCard(false); }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (_cardVisible)
            {
                _cardTime += Time.unscaledDeltaTime;
                UpdateCard();
            }
        }

        void UpdateCard()
        {
            float pop = Mathf.SmoothStep(0.9f, 1f, Mathf.Clamp01(_cardTime / 0.8f));
            _cardTitle.rectTransform.localScale = new Vector3(pop, pop, 1f);

            int lit = _cardProgressive
                ? Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01((_cardTime - 0.4f) / (_cardDuration - 1f)) * _dots.Length + 0.0001f), 0, _dots.Length)
                : _dots.Length;
            for (int i = 0; i < _dots.Length; i++)
            {
                bool on = i < lit;
                float pulse = (!_cardProgressive && on) ? 1f + 0.18f * Mathf.Sin(_cardTime * 4f - i * 0.5f) : 1f;
                _dots[i].color = on ? Gold : DotOff;
                _dots[i].rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        // ------------------------------------------------------------------ internals

        void SetCard(bool on)
        {
            _cardVisible = on;
            _cardRoot.SetActive(on);
        }

        void SetCovered(Color c)
        {
            HideCovers();
            _fade.color = c;
        }

        void HideCovers()
        {
            _fade.color = new Color(0f, 0f, 0f, 0f);
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _wipe.gameObject.SetActive(false);
            _irisRoot.gameObject.SetActive(false);
        }

        void SetWipe(bool left, Phase phase, float t)
        {
            float w = _root.rect.width;
            float h = _root.rect.height;
            float travel = w + 2f * WipeEdge;
            _wipe.gameObject.SetActive(true);
            _wipe.sizeDelta = new Vector2(w + 2f * WipeEdge, h + 4f);
            float dir = left ? 1f : -1f; // left wipe: enters from the right, leaves to the left
            float x = phase == Phase.Close ? dir * travel * (1f - t) : -dir * travel * t;
            _wipe.anchoredPosition = new Vector2(x, 0f);

            // Cyan leading edge on the side the panel is travelling toward.
            _wipeStrip.anchorMin = new Vector2(left ? 0f : 1f, 0f);
            _wipeStrip.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            _wipeStrip.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            _wipeStrip.sizeDelta = new Vector2(WipeEdge, 0f);
            _wipeStrip.anchoredPosition = Vector2.zero;
        }

        /// <param name="open">0 = a point (fully covered), 1 = wide open.</param>
        void SetIris(float open)
        {
            float maxR = 0.5f * Mathf.Sqrt(_root.rect.width * _root.rect.width + _root.rect.height * _root.rect.height) + 30f;
            float r = maxR * Mathf.SmoothStep(0f, 1f, open);
            if (r <= 1.5f)
            {
                _fade.color = Color.black; // the iris has closed to a point
                return;
            }

            if (r >= maxR - 0.5f)
            {
                return;
            }

            _irisRoot.gameObject.SetActive(true);
            float side = 4f * r; // hole radius is a quarter of the texture side
            _irisHole.sizeDelta = new Vector2(side, side);
            _irisHole.anchoredPosition = Vector2.zero;
            float half = side * 0.5f;
            _irisL.sizeDelta = new Vector2(Big, Big * 2f);
            _irisL.anchoredPosition = new Vector2(-half + 1f, 0f);
            _irisR.sizeDelta = new Vector2(Big, Big * 2f);
            _irisR.anchoredPosition = new Vector2(half - 1f, 0f);
            _irisT.sizeDelta = new Vector2(side + 4f, Big);
            _irisT.anchoredPosition = new Vector2(0f, half - 1f);
            _irisB.sizeDelta = new Vector2(side + 4f, Big);
            _irisB.anchoredPosition = new Vector2(0f, -half + 1f);
        }

        // ------------------------------------------------------------------ builders

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image MakePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI MakeText(string name, Transform parent, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Anti-aliased disc (or a square with a transparent disc cut out when <paramref name="invert"/>).</summary>
        static Sprite MakeCircleSprite(int size, float radius, bool invert)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
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
                    float inside = Mathf.Clamp01(radius - d + 0.5f);
                    float a = invert ? 1f - inside : inside;
                    px[y * size + x] = invert ? new Color32(0, 0, 0, (byte)(a * 255f)) : new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
