using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Presentation
{
    /// <summary>What a board tile is showing.</summary>
    public enum TileState
    {
        Alive = 0,

        /// <summary>Knocked out: dimmed, with a red OUT stamp.</summary>
        Out = 1,

        /// <summary>Came back from out: bright again with a small BACK! badge.</summary>
        Returned = 2,

        /// <summary>The winner: gold, enlarged, glowing.</summary>
        Champion = 3
    }

    /// <summary>
    /// A grid of name tiles (colour, name, state stamp), centred on screen: up to 16 in rows of four. The
    /// format-agnostic half of an elimination show: it is handed names, colours and states, and never looks at
    /// a run. State changes animate (OUT drops in with a shake, RETURNED peels the stamp off, CHAMPION turns
    /// gold), timed in unscaled seconds.
    /// </summary>
    /// <remarks>
    /// Builds its own tiles under this object the first time <see cref="SetTiles"/> is called, so the scene only
    /// needs a screen-space canvas (1080x1920 reference, match height) with this component and a
    /// <see cref="CanvasGroup"/>. Tile sizes are in reference units: at 16:9 that reference canvas is 3413 units
    /// wide, so a 4x4 grid of 400-unit tiles sits comfortably in the middle.
    /// </remarks>
    public sealed class EliminationBoardHud : MonoBehaviour
    {
        public const int MaxTiles = 16;

        [Header("Layout (reference units)")]
        public int columns = 4;
        public Vector2 tileSize = new Vector2(400f, 230f);
        [Min(0f)] public float gap = 28f;
        [Tooltip("Vertical offset of the grid from the screen centre. Positive moves it up.")]
        public float verticalOffset = 70f;

        [Header("Text")]
        [Tooltip("Name text auto-sizes between these, in reference units. The floor is the readability rule.")]
        public Vector2 nameFontRange = new Vector2(56f, 84f);

        [Header("Colours")]
        public Color outStampColor = new Color(0.86f, 0.09f, 0.12f, 1f);
        public Color backBadgeColor = new Color(0.12f, 0.62f, 0.30f, 1f);
        public Color goldColor = new Color(1f, 0.82f, 0.22f, 1f);
        public Color glowColor = new Color(1f, 0.93f, 0.55f, 1f);
        [Range(0f, 1f)] public float outDim = 0.62f;

        sealed class Tile
        {
            public RectTransform rect;
            public Image background;
            public Image dim;
            public Image glow;
            public TMP_Text name;
            public RectTransform stampRect;
            public CanvasGroup stampGroup;
            public CanvasGroup badgeGroup;
            public Color baseColor;
            public Color textColor;
            public Vector2 home;
            public TileState state;

            // Animation clocks, all in seconds.
            public float stampT = -1f;   // < 0: no stamp; 0..: drop/hold time since it landed
            public float peelT = -1f;    // >= 0: peeling off
            public float shakeT;         // landing shake, counts down
            public float popT;           // pop on return/champion, counts down
            public bool highlight;
            public float phase;
        }

        readonly List<Tile> _tiles = new List<Tile>();
        RectTransform _grid;
        CanvasGroup _group;
        float _alphaTarget = 1f;
        float _fadeSpeed = 4f;
        bool _built;

        public int TileCount => _tiles.Count;

        public TileState GetState(int index)
        {
            return index >= 0 && index < _tiles.Count ? _tiles[index].state : TileState.Alive;
        }

        /// <summary>Lays out one tile per name (max 16), all Alive. Safe to call again with a new field.</summary>
        public void SetTiles(IReadOnlyList<string> names, IReadOnlyList<Color> colors)
        {
            EnsureRoot();
            for (int i = _tiles.Count - 1; i >= 0; i--)
            {
                Destroy(_tiles[i].rect.gameObject);
            }

            _tiles.Clear();
            int count = Mathf.Min(names.Count, MaxTiles);
            int cols = Mathf.Max(1, columns);
            int rows = Mathf.CeilToInt(count / (float)cols);
            float width = cols * tileSize.x + (cols - 1) * gap;
            float height = rows * tileSize.y + (rows - 1) * gap;

            for (int i = 0; i < count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                Vector2 home = new Vector2(
                    -width * 0.5f + tileSize.x * 0.5f + col * (tileSize.x + gap),
                    height * 0.5f - tileSize.y * 0.5f - row * (tileSize.y + gap));
                Color color = i < colors.Count ? colors[i] : Color.white;
                _tiles.Add(BuildTile(i, names[i], color, home));
            }

            _built = true;
        }

        /// <summary>Move a tile to <paramref name="state"/>. <paramref name="animate"/> false snaps (for a resync).</summary>
        public void SetState(int index, TileState state, bool animate = true)
        {
            if (index < 0 || index >= _tiles.Count)
            {
                return;
            }

            Tile t = _tiles[index];
            if (t.state == state && animate)
            {
                return;
            }

            TileState previous = t.state;
            t.state = state;

            if (state == TileState.Out)
            {
                t.peelT = -1f;
                t.stampT = 0f;
                if (animate)
                {
                    t.shakeT = 0.45f;
                }
                else
                {
                    t.stampT = 10f;
                }
            }
            else
            {
                if (previous == TileState.Out && t.stampT >= 0f)
                {
                    if (animate)
                    {
                        t.peelT = 0f;
                    }
                    else
                    {
                        t.peelT = -1f;
                        t.stampT = -1f;
                    }
                }

                if (animate && (state == TileState.Returned || state == TileState.Champion))
                {
                    t.popT = 0.6f;
                }

                if (state == TileState.Champion)
                {
                    // Scaled up and glowing: draw it over its neighbours.
                    t.rect.SetAsLastSibling();
                }
            }

            if (!animate)
            {
                t.shakeT = 0f;
                t.popT = 0f;
            }
        }

        /// <summary>Keeps a tile shaking and glowing (the "these marbles get a last chance" beat).</summary>
        public void SetHighlight(int index, bool on)
        {
            if (index >= 0 && index < _tiles.Count)
            {
                _tiles[index].highlight = on;
            }
        }

        public void ClearHighlights()
        {
            foreach (Tile t in _tiles)
            {
                t.highlight = false;
            }
        }

        /// <summary>Fade the whole board in or out.</summary>
        public void SetVisible(bool visible, float fadeSeconds = 0.3f)
        {
            EnsureRoot();
            _alphaTarget = visible ? 1f : 0f;
            _fadeSpeed = fadeSeconds > 0.01f ? 1f / fadeSeconds : 1000f;
            if (fadeSeconds <= 0f)
            {
                _group.alpha = _alphaTarget;
            }
        }

        public bool IsVisible => _group != null && _group.alpha > 0.01f;

        // ------------------------------------------------------------------ build

        void EnsureRoot()
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
                if (_group == null)
                {
                    _group = gameObject.AddComponent<CanvasGroup>();
                }

                _group.interactable = false;
                _group.blocksRaycasts = false;
            }

            if (_grid == null)
            {
                var go = new GameObject("Tile Grid", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                _grid = (RectTransform)go.transform;
                _grid.anchorMin = _grid.anchorMax = new Vector2(0.5f, 0.5f);
                _grid.pivot = new Vector2(0.5f, 0.5f);
                _grid.anchoredPosition = new Vector2(0f, verticalOffset);
                _grid.sizeDelta = Vector2.zero;
            }
        }

        Tile BuildTile(int index, string label, Color color, Vector2 home)
        {
            var t = new Tile { baseColor = color, home = home, phase = index * 0.37f };

            GameObject root = NewRect($"Tile {index + 1:00} {label}", _grid, tileSize, home);
            t.rect = (RectTransform)root.transform;

            // Glow sits behind the tile and is a little larger: a lit border when highlighted or champion.
            GameObject glow = NewRect("Glow", t.rect, tileSize + new Vector2(36f, 36f), Vector2.zero);
            t.glow = glow.AddComponent<Image>();
            t.glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            t.glow.raycastTarget = false;

            GameObject bg = NewRect("Body", t.rect, tileSize, Vector2.zero);
            t.background = bg.AddComponent<Image>();
            t.background.color = color;
            t.background.raycastTarget = false;

            // Dark band under the name: keeps white or black text readable on any jewel colour.
            t.textColor = Luminance(color) > 0.55f ? new Color(0.07f, 0.07f, 0.08f) : Color.white;

            GameObject nameObject = NewRect("Name", t.rect, new Vector2(tileSize.x - 36f, tileSize.y - 36f), Vector2.zero);
            var text = nameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = t.textColor;
            text.enableAutoSizing = true;
            text.fontSizeMin = nameFontRange.x;
            text.fontSizeMax = nameFontRange.y;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.characterSpacing = 4f;
            text.raycastTarget = false;
            t.name = text;

            GameObject dim = NewRect("Dim", t.rect, tileSize, Vector2.zero);
            t.dim = dim.AddComponent<Image>();
            t.dim.color = new Color(0.04f, 0.04f, 0.05f, 0f);
            t.dim.raycastTarget = false;

            // Red OUT stamp: a plate with big white letters, tilted like a rubber stamp.
            GameObject stamp = NewRect("Stamp", t.rect, new Vector2(tileSize.x * 0.78f, tileSize.y * 0.56f), Vector2.zero);
            t.stampRect = (RectTransform)stamp.transform;
            var stampPlate = stamp.AddComponent<Image>();
            stampPlate.color = outStampColor;
            stampPlate.raycastTarget = false;
            t.stampGroup = stamp.AddComponent<CanvasGroup>();
            t.stampGroup.alpha = 0f;
            GameObject stampText = NewRect("Text", stamp.transform, t.stampRect.sizeDelta, Vector2.zero);
            var out_ = stampText.AddComponent<TextMeshProUGUI>();
            out_.text = "OUT";
            out_.fontStyle = FontStyles.Bold;
            out_.alignment = TextAlignmentOptions.Center;
            out_.color = Color.white;
            out_.fontSize = 120f;
            out_.characterSpacing = 12f;
            out_.textWrappingMode = TextWrappingModes.NoWrap;
            out_.raycastTarget = false;
            t.stampRect.localRotation = Quaternion.Euler(0f, 0f, -11f);

            GameObject badge = NewRect("Badge", t.rect, new Vector2(190f, 64f), new Vector2(tileSize.x * 0.5f - 105f, tileSize.y * 0.5f - 24f));
            var badgePlate = badge.AddComponent<Image>();
            badgePlate.color = backBadgeColor;
            badgePlate.raycastTarget = false;
            t.badgeGroup = badge.AddComponent<CanvasGroup>();
            t.badgeGroup.alpha = 0f;
            GameObject badgeText = NewRect("Text", badge.transform, new Vector2(190f, 64f), Vector2.zero);
            var back = badgeText.AddComponent<TextMeshProUGUI>();
            back.text = "BACK!";
            back.fontStyle = FontStyles.Bold;
            back.alignment = TextAlignmentOptions.Center;
            back.color = Color.white;
            back.fontSize = 46f;
            back.textWrappingMode = TextWrappingModes.NoWrap;
            back.raycastTarget = false;
            return t;
        }

        static GameObject NewRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return go;
        }

        static float Luminance(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        // ------------------------------------------------------------------ animate

        void Update()
        {
            if (_group != null)
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, _alphaTarget, _fadeSpeed * Time.unscaledDeltaTime);
            }

            if (!_built || _group == null || _group.alpha <= 0.001f)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            foreach (Tile t in _tiles)
            {
                Animate(t, dt, now);
            }
        }

        void Animate(Tile t, float dt, float now)
        {
            // --- stamp ---
            float stampAlpha = 0f;
            float stampScale = 1f;
            Vector2 stampOffset = Vector2.zero;
            float stampSpin = -11f;
            if (t.peelT >= 0f)
            {
                t.peelT += dt;
                float p = Mathf.Clamp01(t.peelT / 0.45f);
                stampAlpha = 1f - p;
                stampScale = 1f + 0.5f * p;
                stampOffset = new Vector2(30f * p, 90f * p);
                stampSpin = -11f - 40f * p;
                if (p >= 1f)
                {
                    t.peelT = -1f;
                    t.stampT = -1f;
                }
            }
            else if (t.stampT >= 0f)
            {
                t.stampT += dt;
                const float drop = 0.22f;
                float p = Mathf.Clamp01(t.stampT / drop);
                float eased = 1f - (1f - p) * (1f - p);
                stampScale = Mathf.Lerp(3.2f, 1f, eased);
                stampAlpha = Mathf.Clamp01(t.stampT / 0.08f);
            }

            t.stampGroup.alpha = stampAlpha;
            t.stampRect.localScale = new Vector3(stampScale, stampScale, 1f);
            t.stampRect.anchoredPosition = stampOffset;
            t.stampRect.localRotation = Quaternion.Euler(0f, 0f, stampSpin);

            // --- dim + colour ---
            float dimTarget = t.state == TileState.Out ? outDim : 0f;
            Color dimColor = t.dim.color;
            dimColor.a = Mathf.MoveTowards(dimColor.a, dimTarget, dt * 3f);
            t.dim.color = dimColor;

            Color body = t.baseColor;
            Color textColor = t.textColor;
            if (t.state == TileState.Champion)
            {
                body = goldColor;
                textColor = new Color(0.1f, 0.07f, 0.02f);
            }

            t.background.color = Color.Lerp(t.background.color, body, 1f - Mathf.Exp(-8f * dt));
            t.name.color = Color.Lerp(t.name.color, textColor, 1f - Mathf.Exp(-8f * dt));

            // --- badge ---
            float badgeTarget = t.state == TileState.Returned ? 1f : 0f;
            t.badgeGroup.alpha = Mathf.MoveTowards(t.badgeGroup.alpha, badgeTarget, dt * 4f);

            // --- glow ---
            float glowTarget = 0f;
            if (t.state == TileState.Champion)
            {
                glowTarget = 0.75f + 0.25f * Mathf.Sin(now * 5f);
            }
            else if (t.highlight)
            {
                glowTarget = 0.45f + 0.4f * Mathf.Sin(now * 7f + t.phase);
            }

            Color glow = t.glow.color;
            glow.a = Mathf.MoveTowards(glow.a, glowTarget, dt * 6f);
            glow.r = glowColor.r;
            glow.g = glowColor.g;
            glow.b = glowColor.b;
            t.glow.color = glow;

            // --- shake, pop, scale ---
            Vector2 offset = Vector2.zero;
            float roll = 0f;
            if (t.shakeT > 0f)
            {
                t.shakeT = Mathf.Max(0f, t.shakeT - dt);
                float k = t.shakeT / 0.45f;
                offset += new Vector2(Mathf.Sin(now * 70f), Mathf.Cos(now * 83f)) * (16f * k);
                roll += Mathf.Sin(now * 60f) * 3f * k;
            }

            if (t.highlight && t.state == TileState.Out)
            {
                offset += new Vector2(Mathf.Sin(now * 32f + t.phase), Mathf.Cos(now * 27f + t.phase)) * 6f;
                roll += Mathf.Sin(now * 24f + t.phase) * 1.4f;
            }

            float scale = 1f;
            if (t.state == TileState.Champion)
            {
                scale = 1.12f;
            }

            if (t.popT > 0f)
            {
                t.popT = Mathf.Max(0f, t.popT - dt);
                float k = t.popT / 0.6f;
                scale *= 1f + 0.25f * Mathf.Sin(k * Mathf.PI);
            }

            t.rect.anchoredPosition = t.home + offset;
            t.rect.localRotation = Quaternion.Euler(0f, 0f, roll);
            t.rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
