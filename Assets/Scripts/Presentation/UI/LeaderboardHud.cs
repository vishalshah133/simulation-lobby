using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A live standings board: one row per contender that slides to its current place, plus a race timer
    /// and a centre banner for countdowns and the winner. Format-agnostic ("rows", "a banner"); a thin
    /// binder in the format pushes values in. Presentation only: nothing here reads or writes the simulation.
    /// </summary>
    public sealed class LeaderboardHud : MonoBehaviour
    {
        [Serializable]
        public sealed class Row
        {
            public RectTransform rect;
            public Image swatch;
            public TMP_Text place;
            public TMP_Text name;
            public TMP_Text note;
        }

        [SerializeField] Row[] _rows = new Row[0];
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _timer;
        [SerializeField] TMP_Text _banner;
        [SerializeField] CanvasGroup _bannerGroup;

        [Min(10f)] public float rowHeight = 62f;

        [Tooltip("With more rows than this, the row pitch shrinks to fit the 1080 reference height (16 rows must fit).")]
        [Min(1)] public int fullSizeRowLimit = 12;

        [Tooltip("Row pitch used once the count exceeds the limit above.")]
        [Min(10f)] public float compactRowHeight = 54f;

        [Header("Cut line")]
        public Color cutLineColor = new Color(0.95f, 0.14f, 0.16f, 1f);
        public Color outRowColor = new Color(0.55f, 0.05f, 0.08f, 0.78f);
        [Range(2f, 30f)] public float slideSpeed = 9f;
        public Color leaderColor = new Color(0.93f, 0.71f, 0.38f, 1f);
        public Color normalColor = new Color(0.93f, 0.92f, 0.9f, 1f);

        float _baseRowHeight;
        bool _showNotes = true;
        int _cutLine;
        Image _cutLineImage;
        Image[] _rowBackground = new Image[0];
        Color[] _rowBaseColor = new Color[0];
        float[] _targetY = new float[0];
        int[] _rank = new int[0];
        int _active;
        float _bannerSeconds;
        float _bannerFade = 0.5f;

        public int RowCapacity => _rows.Length;

        public void Bind(Row[] rows, TMP_Text title, TMP_Text timer, TMP_Text banner, CanvasGroup bannerGroup)
        {
            _rows = rows;
            _title = title;
            _timer = timer;
            _banner = banner;
            _bannerGroup = bannerGroup;
        }

        /// <summary>Show the first <paramref name="count"/> rows; hide the rest.</summary>
        public void Configure(int count)
        {
            if (_baseRowHeight <= 0f)
            {
                _baseRowHeight = rowHeight;
            }

            _active = Mathf.Clamp(count, 0, _rows.Length);

            // Up to the limit the pitch is whatever the canvas was built with. Beyond it, rows tighten so they still fit.
            bool compact = _active > fullSizeRowLimit;
            rowHeight = compact ? Mathf.Min(_baseRowHeight, compactRowHeight) : _baseRowHeight;

            _targetY = new float[_rows.Length];
            _rank = new int[_rows.Length];
            if (_rowBackground.Length != _rows.Length)
            {
                _rowBackground = new Image[_rows.Length];
                _rowBaseColor = new Color[_rows.Length];
                for (int i = 0; i < _rows.Length; i++)
                {
                    _rowBackground[i] = _rows[i].rect.GetComponent<Image>();
                    _rowBaseColor[i] = _rowBackground[i] != null ? _rowBackground[i].color : Color.clear;
                }
            }

            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i].rect.gameObject.SetActive(i < _active);
                _rank[i] = i;
                _targetY[i] = -i * rowHeight;
                _rows[i].rect.anchoredPosition = new Vector2(_rows[i].rect.anchoredPosition.x, _targetY[i]);
                if (compact || _rows[i].rect.sizeDelta.y != _baseRowHeight - 6f)
                {
                    // Keep the 6-unit gap between rows whatever the pitch.
                    _rows[i].rect.sizeDelta = new Vector2(_rows[i].rect.sizeDelta.x, rowHeight - 6f);
                }

                _rows[i].note.gameObject.SetActive(_showNotes);
            }

            ApplyCutLine();
        }

        /// <summary>
        /// Draw a red line under row <paramref name="safeCount"/> (rows 1..safeCount are safe) and tint the rows below it
        /// red. Zero turns it off. Cheap to call every frame.
        /// </summary>
        public void SetCutLine(int safeCount)
        {
            safeCount = Mathf.Max(0, safeCount);
            if (safeCount == _cutLine)
            {
                return;
            }

            _cutLine = safeCount;
            ApplyCutLine();
        }

        /// <summary>Names only when false: the per-row time/gap notes are hidden.</summary>
        public bool ShowNotes
        {
            get => _showNotes;
            set => SetShowNotes(value);
        }

        public void SetShowNotes(bool show)
        {
            if (show == _showNotes)
            {
                return;
            }

            _showNotes = show;
            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i].note.gameObject.SetActive(show);
            }
        }

        void ApplyCutLine()
        {
            bool on = _cutLine > 0 && _cutLine < _active;
            if (on && _cutLineImage == null && _rows.Length > 0)
            {
                var go = new GameObject("Cut Line", typeof(RectTransform));
                RectTransform parent = (RectTransform)_rows[0].rect.parent;
                go.transform.SetParent(parent, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                _cutLineImage = go.AddComponent<Image>();
                _cutLineImage.raycastTarget = false;
            }

            if (_cutLineImage != null)
            {
                _cutLineImage.gameObject.SetActive(on);
                if (on)
                {
                    _cutLineImage.color = cutLineColor;
                    var rect = _cutLineImage.rectTransform;
                    rect.sizeDelta = new Vector2(_rows[0].rect.sizeDelta.x, 5f);
                    // Rows sit with a 6-unit gap under each; the line is centred in the gap above row `_cutLine`.
                    rect.anchoredPosition = new Vector2(_rows[0].rect.anchoredPosition.x, -_cutLine * rowHeight + 3f);
                    rect.SetAsLastSibling();
                }
            }

            for (int i = 0; i < _rows.Length && i < _rowBackground.Length; i++)
            {
                TintRow(i);
            }
        }

        void TintRow(int index)
        {
            Image background = _rowBackground[index];
            if (background == null)
            {
                return;
            }

            bool isOut = _cutLine > 0 && _cutLine < _active && _rank[index] >= _cutLine;
            background.color = isOut ? outRowColor : _rowBaseColor[index];
        }

        public void SetTitle(string text)
        {
            if (_title != null)
            {
                _title.text = text;
            }
        }

        public void SetContender(int index, string contenderName, Color color)
        {
            Row row = _rows[index];
            row.name.text = contenderName;
            row.swatch.color = color;
            row.note.text = string.Empty;
        }

        /// <summary>Move a contender's row to a place (0 is first). The row slides there.</summary>
        public void SetRank(int index, int rank)
        {
            if (index < 0 || index >= _active)
            {
                return;
            }

            bool crossed = _cutLine > 0 && (_rank[index] >= _cutLine) != (rank >= _cutLine);
            _rank[index] = rank;
            _targetY[index] = -rank * rowHeight;
            if (crossed && index < _rowBackground.Length)
            {
                TintRow(index);
            }

            Row row = _rows[index];
            string label = (rank + 1).ToString();
            if (row.place.text != label)
            {
                row.place.text = label;
            }

            row.place.color = rank == 0 ? leaderColor : normalColor;
        }

        public void SetNote(int index, string text)
        {
            if (!_showNotes)
            {
                return;
            }

            if (_rows[index].note.text != text)
            {
                _rows[index].note.text = text;
            }
        }

        /// <summary>Turns the clock off for formats that don't want one (the marble machine). Safe to call every frame.</summary>
        public void HideTimer()
        {
            if (_timer != null && _timer.gameObject.activeSelf)
            {
                _timer.gameObject.SetActive(false);
            }
        }

        public void SetTimer(string text)
        {
            if (_timer != null && _timer.text != text)
            {
                _timer.text = text;
            }
        }

        /// <summary>Big centre text that holds, then fades. Pass zero seconds to hold until the next banner.</summary>
        public void ShowBanner(string text, Color color, float seconds, float fadeSeconds = 0.5f)
        {
            if (_banner == null)
            {
                return;
            }

            _banner.text = text;
            _banner.color = color;
            _bannerSeconds = seconds > 0f ? seconds : float.PositiveInfinity;
            _bannerFade = Mathf.Max(0.05f, fadeSeconds);
            if (_bannerGroup != null)
            {
                _bannerGroup.alpha = 1f;
            }
        }

        public void HideBanner()
        {
            _bannerSeconds = 0f;
            if (_bannerGroup != null)
            {
                _bannerGroup.alpha = 0f;
            }
        }

        void Update()
        {
            float blend = 1f - Mathf.Exp(-slideSpeed * Time.unscaledDeltaTime);
            for (int i = 0; i < _active; i++)
            {
                RectTransform rect = _rows[i].rect;
                Vector2 position = rect.anchoredPosition;
                position.y = Mathf.Lerp(position.y, _targetY[i], blend);
                rect.anchoredPosition = position;
            }

            if (_bannerGroup != null && _bannerSeconds > 0f && !float.IsPositiveInfinity(_bannerSeconds))
            {
                _bannerSeconds -= Time.unscaledDeltaTime;
                if (_bannerSeconds < _bannerFade)
                {
                    _bannerGroup.alpha = Mathf.Clamp01(_bannerSeconds / _bannerFade);
                }
            }
        }
    }
}
