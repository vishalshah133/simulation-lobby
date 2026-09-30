using TMPro;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// One big caption in the top panel ("SOFT 50%"), a smaller hook line under it, and an optional
    /// footer in the bottom panel. The HUD for a sweep, where the swept value is the only thing on
    /// screen that needs reading.
    /// </summary>
    /// <remarks>
    /// Format-agnostic and read-only, like <see cref="VersusScoreboardHud"/>: it is handed strings and
    /// never looks at a run. A new caption lands with a small scale punch so the change reads even in
    /// peripheral vision — timed in unscaled seconds, so slow-motion never stretches it.
    /// </remarks>
    public sealed class CaptionHud : MonoBehaviour
    {
        [SerializeField] TMP_Text _captionText;
        [SerializeField] TMP_Text _sublineText;
        [SerializeField] TMP_Text _footerText;

        [Tooltip("Caption scale at the instant it changes, easing back to 1.")]
        [Range(1f, 1.5f)] public float punchScale = 1.14f;

        [Range(0.05f, 1f)] public float punchSeconds = 0.28f;

        float _punchLeft;

        public void Bind(TMP_Text caption, TMP_Text subline, TMP_Text footer)
        {
            _captionText = caption;
            _sublineText = subline;
            _footerText = footer;
        }

        public void SetCaption(string text, bool punch = true)
        {
            if (_captionText == null)
            {
                return;
            }

            _captionText.text = text ?? string.Empty;
            if (punch)
            {
                _punchLeft = punchSeconds;
            }
        }

        public void SetSubline(string text)
        {
            if (_sublineText != null)
            {
                _sublineText.text = text ?? string.Empty;
            }
        }

        public void SetFooter(string text)
        {
            if (_footerText != null)
            {
                _footerText.text = text ?? string.Empty;
            }
        }

        void Update()
        {
            if (_captionText == null)
            {
                return;
            }

            if (_punchLeft > 0f)
            {
                _punchLeft = Mathf.Max(0f, _punchLeft - Time.unscaledDeltaTime);
            }

            // Ease-out: fast snap to the big size, slow settle back.
            float t = punchSeconds > 0f ? _punchLeft / punchSeconds : 0f;
            float scale = 1f + (punchScale - 1f) * t * t;
            _captionText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
