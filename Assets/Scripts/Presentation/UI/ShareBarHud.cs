using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A stacked N-way horizontal bar: one coloured segment per contender, sized by its share, with a
    /// percentage label on each. Widths glide toward their targets so a land-grab reads as a tug of war
    /// rather than a jump. Format-agnostic ("segments", "shares"); a thin binder in the format pushes the
    /// values in. Presentation only: nothing here reads or writes the simulation.
    /// </summary>
    public sealed class ShareBarHud : MonoBehaviour
    {
        [SerializeField] RectTransform _bar;
        [SerializeField] Image[] _segments = new Image[0];
        [SerializeField] TMP_Text[] _labels = new TMP_Text[0];

        [Range(1f, 30f)] public float glideSpeed = 7f;
        [Tooltip("Hide a segment's label when the segment is narrower than this, in canvas pixels.")]
        [Min(0f)] public float minLabelWidth = 96f;
        [Tooltip("An eliminated segment keeps at least this share of the bar, so its OUT tag stays readable.")]
        [Range(0f, 0.2f)] public float outMinShare = 0.05f;
        [Range(0f, 1f)] public float eliminatedAlpha = 0.35f;
        [Tooltip("Gap between neighbouring segments, in canvas units. 0 butts them together.")]
        [Min(0f)] public float segmentGap = 0f;

        [Tooltip("Prefix each label with the segment's name from Configure (\"IND 34%\", \"IND OUT\").")]
        public bool showNames = false;

        [Tooltip("An eliminated segment reads \"0\" instead of \"OUT\" (count-label mode only).")]
        public bool eliminatedShowsZero = false;

        Color[] _colors = new Color[0];
        string[] _names = new string[0];
        float[] _target = new float[0];     // normalised widths, summing to 1
        float[] _current = new float[0];
        float[] _shares = new float[0];     // raw shares as last pushed
        bool[] _eliminated = new bool[0];
        int[] _percent = new int[0];
        int[] _counts = new int[0];         // raw counts as last pushed (count-label mode)
        bool _countMode;
        int _count;
        bool _snap = true;

        public int Count => _count;

        public void Bind(RectTransform bar, Image[] segments, TMP_Text[] labels)
        {
            _bar = bar;
            _segments = segments;
            _labels = labels;
        }

        /// <summary>Show the first <paramref name="count"/> segments, coloured and named; hide the rest.</summary>
        public void Configure(int count, Color[] colors, string[] names)
        {
            _count = Mathf.Clamp(count, 0, _segments.Length);
            _colors = colors;
            _names = names;
            _target = new float[_segments.Length];
            _current = new float[_segments.Length];
            _shares = new float[_segments.Length];
            _eliminated = new bool[_segments.Length];
            _percent = new int[_segments.Length];
            _counts = new int[_segments.Length];

            for (int i = 0; i < _segments.Length; i++)
            {
                bool active = i < _count;
                _segments[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                _shares[i] = 1f / Mathf.Max(1, _count);
                _percent[i] = -1;
                if (colors != null && i < colors.Length)
                {
                    _segments[i].color = colors[i];
                }

                if (_labels != null && i < _labels.Length && _labels[i] != null && names != null && i < names.Length)
                {
                    _labels[i].gameObject.name = $"Label {names[i]}";
                }
            }

            RecomputeTargets();
            _snap = true;
            Apply(0f);
        }

        /// <summary>Shares as fractions 0..1 per segment. They need not sum to exactly 1; the bar normalises.</summary>
        public void SetShares(IReadOnlyList<float> shares01)
        {
            if (shares01 == null)
            {
                return;
            }

            if (_countMode)
            {
                _countMode = false;
                for (int i = 0; i < _percent.Length; i++)
                {
                    _percent[i] = -1;
                }
            }

            int n = Mathf.Min(_count, shares01.Count);
            for (int i = 0; i < n; i++)
            {
                _shares[i] = Mathf.Max(0f, shares01[i]);
            }

            RecomputeTargets();
        }

        /// <summary>
        /// Count-label mode: each label shows its integer count ("RED 1,234") instead of a percentage, and
        /// segment widths are proportional to the counts. All zero gives equal widths. Switches the bar into
        /// count mode on first call; <see cref="SetShares"/> switches it back. Names still follow
        /// <see cref="showNames"/>. Eliminated segments still read OUT.
        /// </summary>
        public void SetCounts(IReadOnlyList<int> counts)
        {
            if (counts == null)
            {
                return;
            }

            if (!_countMode)
            {
                _countMode = true;
                for (int i = 0; i < _percent.Length; i++)
                {
                    _percent[i] = -1;
                }
            }

            int n = Mathf.Min(_count, counts.Count);
            long total = 0;
            for (int i = 0; i < n; i++)
            {
                _counts[i] = Mathf.Max(0, counts[i]);
                total += _counts[i];
            }

            for (int i = 0; i < n; i++)
            {
                _shares[i] = total > 0 ? (float)((double)_counts[i] / total) : 0f;
            }

            RecomputeTargets();
        }

        /// <summary>Dim a segment (colour kept, alpha lowered) and label it OUT.</summary>
        public void SetEliminated(int index, bool eliminated)
        {
            if (index < 0 || index >= _count)
            {
                return;
            }

            _eliminated[index] = eliminated;
            if (_colors != null && index < _colors.Length)
            {
                Color c = _colors[index];
                c.a = eliminated ? eliminatedAlpha : 1f;
                _segments[index].color = c;
            }

            _percent[index] = -1;
            RecomputeTargets();
        }

        void RecomputeTargets()
        {
            float sum = 0f;
            for (int i = 0; i < _count; i++)
            {
                float w = _shares[i];
                if (_eliminated[i])
                {
                    w = Mathf.Max(w, outMinShare);
                }

                _target[i] = w;
                sum += w;
            }

            for (int i = 0; i < _count; i++)
            {
                _target[i] = sum > 1e-6f ? _target[i] / sum : 1f / Mathf.Max(1, _count);
            }
        }

        void Update()
        {
            float blend = _snap ? 1f : 1f - Mathf.Exp(-glideSpeed * Time.unscaledDeltaTime);
            _snap = false;
            Apply(blend);
        }

        void Apply(float blend)
        {
            if (_bar == null)
            {
                return;
            }

            float barWidth = _bar.rect.width;
            float start = 0f;
            for (int i = 0; i < _count; i++)
            {
                _current[i] = Mathf.Lerp(_current[i], _target[i], blend);

                // The last visible segment closes the bar exactly, so rounding never leaves a gap.
                float end = i == _count - 1 ? 1f : Mathf.Min(1f, start + _current[i]);
                RectTransform rect = _segments[i].rectTransform;
                rect.anchorMin = new Vector2(start, 0f);
                rect.anchorMax = new Vector2(end, 1f);
                float half = segmentGap * 0.5f;
                rect.offsetMin = new Vector2(i > 0 ? half : 0f, 0f);
                rect.offsetMax = new Vector2(i < _count - 1 ? -half : 0f, 0f);

                UpdateLabel(i, (end - start) * barWidth);
                start = end;
            }
        }

        void UpdateLabel(int index, float pixelWidth)
        {
            if (_labels == null || index >= _labels.Length || _labels[index] == null)
            {
                return;
            }

            TMP_Text label = _labels[index];
            bool eliminated = _eliminated[index];
            bool visible = pixelWidth >= (eliminated ? minLabelWidth * 0.6f : minLabelWidth);
            if (label.enabled != visible)
            {
                label.enabled = visible;
            }

            if (!visible)
            {
                return;
            }

            // Cache the integer so the string is only rebuilt when the digits change.
            int percent = eliminated ? -2 : _countMode ? _counts[index] : Mathf.RoundToInt(_shares[index] * 100f);
            if (percent == _percent[index])
            {
                return;
            }

            _percent[index] = percent;
            string text = eliminated ? (eliminatedShowsZero && _countMode ? "0" : "OUT") : _countMode ? CountFormat.Compact(percent, 6) : percent + "%";
            if (showNames && _names != null && index < _names.Length && !string.IsNullOrEmpty(_names[index]))
            {
                text = _names[index] + " " + text;
            }

            label.text = text;
        }
    }
}
