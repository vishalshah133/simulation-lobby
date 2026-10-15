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
        [Range(2f, 30f)] public float slideSpeed = 9f;
        public Color leaderColor = new Color(0.93f, 0.71f, 0.38f, 1f);
        public Color normalColor = new Color(0.93f, 0.92f, 0.9f, 1f);

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
            _active = Mathf.Clamp(count, 0, _rows.Length);
            _targetY = new float[_rows.Length];
            _rank = new int[_rows.Length];
            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i].rect.gameObject.SetActive(i < _active);
                _rank[i] = i;
                _targetY[i] = -i * rowHeight;
                _rows[i].rect.anchoredPosition = new Vector2(_rows[i].rect.anchoredPosition.x, _targetY[i]);
            }
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

            _rank[index] = rank;
            _targetY[index] = -rank * rowHeight;

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
            if (_rows[index].note.text != text)
            {
                _rows[index].note.text = text;
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
