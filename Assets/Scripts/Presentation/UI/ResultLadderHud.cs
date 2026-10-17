using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A stacked, left-aligned list of past results ("3", "31", "314" ...) on one TextMeshPro text, with an
    /// optional footnote under it. For any video whose answers form a pattern the viewer should see build
    /// up row by row.
    /// </summary>
    /// <remarks>
    /// Format-agnostic and read-only, like <see cref="CaptionHud"/>: it is handed strings and never looks at
    /// a run. Rows render in a monospace span so digit columns line up; callers may pass rich text (a colour
    /// tag round one digit). The newest row punches in by animating its font size and opacity, timed in
    /// unscaled seconds so slow-motion never stretches it.
    /// </remarks>
    public sealed class ResultLadderHud : MonoBehaviour
    {
        [SerializeField] TMP_Text _rowsText;
        [SerializeField] TMP_Text _footnoteText;

        [Tooltip("Width of every character cell, in em. Liberation Sans digits are about 0.556 em, so 0.6 lines " +
                 "the columns up with a little air.")]
        [Range(0.4f, 1f)] public float cellWidthEm = 0.6f;

        [Tooltip("Size of the newest row at the instant it lands, as a percentage of normal, easing back to 100.")]
        [Range(100f, 260f)] public float punchPercent = 160f;

        [Range(0.05f, 1f)] public float punchSeconds = 0.35f;

        [Tooltip("Seconds for the footnote to fade from clear to full opacity.")]
        [Range(0.05f, 3f)] public float footnoteFadeSeconds = 0.8f;

        readonly List<string> _rows = new List<string>(16);
        readonly StringBuilder _builder = new StringBuilder(256);

        float _punchLeft;
        bool _dirty;

        float _footnoteFade;
        bool _footnoteFading;
        Color _footnoteColor = Color.white;

        public int RowCount => _rows.Count;

        public void Bind(TMP_Text rows, TMP_Text footnote)
        {
            _rowsText = rows;
            _footnoteText = footnote;
            if (_footnoteText != null)
            {
                _footnoteColor = _footnoteText.color;
                _footnoteColor.a = 1f;
            }

            Clear();
        }

        /// <summary>Remove every row and hide the footnote.</summary>
        public void Clear()
        {
            _rows.Clear();
            _punchLeft = 0f;
            _dirty = true;
            SetFootnote(string.Empty);
            Rebuild(1f);
        }

        /// <summary>Add a row at the bottom of the stack. Rich text is passed through untouched.</summary>
        public void Push(string row, bool punch = true)
        {
            _rows.Add(row ?? string.Empty);
            _punchLeft = punch ? punchSeconds : 0f;
            _dirty = true;
        }

        /// <summary>Set the line under the stack and fade it in. An empty string hides it.</summary>
        public void SetFootnote(string text)
        {
            if (_footnoteText == null)
            {
                return;
            }

            text = text ?? string.Empty;
            if (_footnoteText.text == text)
            {
                return;
            }

            _footnoteText.text = text;
            _footnoteFade = 0f;
            _footnoteFading = text.Length > 0;
            ApplyFootnoteAlpha(text.Length > 0 ? 0f : 1f);
        }

        void Update()
        {
            if (_punchLeft > 0f)
            {
                _punchLeft = Mathf.Max(0f, _punchLeft - Time.unscaledDeltaTime);
                _dirty = true;
            }

            if (_dirty)
            {
                _dirty = false;
                float t = punchSeconds > 0f ? _punchLeft / punchSeconds : 0f;
                Rebuild(t);
            }

            if (_footnoteFading)
            {
                _footnoteFade = Mathf.Min(1f, _footnoteFade + Time.unscaledDeltaTime / Mathf.Max(0.01f, footnoteFadeSeconds));
                ApplyFootnoteAlpha(_footnoteFade);
                if (_footnoteFade >= 1f)
                {
                    _footnoteFading = false;
                }
            }
        }

        /// <param name="punch01">1 at the instant the newest row lands, 0 once settled.</param>
        void Rebuild(float punch01)
        {
            if (_rowsText == null)
            {
                return;
            }

            _builder.Clear();
            for (int i = 0; i < _rows.Count; i++)
            {
                bool newest = i == _rows.Count - 1;
                if (i > 0)
                {
                    _builder.Append('\n');
                }

                float ease = punch01 * punch01;
                bool animate = newest && ease > 0.001f;
                if (animate)
                {
                    int percent = Mathf.RoundToInt(Mathf.Lerp(100f, punchPercent, ease));
                    int alpha = Mathf.RoundToInt(Mathf.Lerp(255f, 90f, ease));
                    _builder.Append("<size=").Append(percent).Append("%><alpha=#").Append(alpha.ToString("X2")).Append('>');
                }

                _builder.Append("<mspace=").Append(cellWidthEm.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
                    .Append("em>").Append(_rows[i]).Append("</mspace>");
                if (animate)
                {
                    _builder.Append("</size>");
                }
            }

            string built = _builder.ToString();
            if (_rowsText.text != built)
            {
                _rowsText.text = built;
            }
        }

        void ApplyFootnoteAlpha(float alpha)
        {
            if (_footnoteText == null)
            {
                return;
            }

            Color c = _footnoteColor;
            c.a = _footnoteColor.a * alpha;
            _footnoteText.color = c;
        }
    }
}
