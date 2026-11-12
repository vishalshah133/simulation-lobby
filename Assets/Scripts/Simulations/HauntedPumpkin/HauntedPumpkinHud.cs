using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.HauntedPumpkin
{
    /// <summary>
    /// The on-screen text and the candle bar for <c>hall</c>: a uGUI canvas with TextMeshPro, per the project's
    /// On-screen UI rules. Pure display: the presenter pushes values in, nothing here reads the simulation.
    /// </summary>
    /// <remarks>
    /// Lives in the format (not in <c>Presentation</c>) because it names this format's things: ghosts, rings,
    /// bats, a candle. The generic panels (top question, bottom counters) follow the shared layout.
    /// </remarks>
    public sealed class HauntedPumpkinHud : MonoBehaviour
    {
        [Header("Top panel")]
        [SerializeField] TMP_Text _caption;
        [SerializeField] TMP_Text _subline;

        [Header("Bottom panel")]
        [SerializeField] TMP_Text _ghosts;
        [SerializeField] TMP_Text _rings;
        [SerializeField] TMP_Text _bats;
        [SerializeField] TMP_Text _status;
        [SerializeField] TMP_Text _why;

        [Header("Candle (right edge, clear of the arena)")]
        [SerializeField] Image _candleFill;
        [SerializeField] RectTransform _flame;
        [SerializeField] Image _flameImage;

        [Header("Full-screen wash (flash on a win, darkness on a loss)")]
        [SerializeField] Image _overlay;

        public void SetCaption(string text)
        {
            Set(_caption, text);
        }

        public void SetSubline(string text)
        {
            Set(_subline, text);
        }

        public void SetGhosts(int count)
        {
            Set(_ghosts, $"GHOSTS  {count}");
        }

        public void SetRings(int broken, int total)
        {
            int left = Mathf.Max(0, total - broken);
            Set(_rings, left == 0 ? "PUMPKIN BROKEN!" : left == 1 ? "LAST RING!" : $"RINGS LEFT  {left}");
        }

        /// <summary>Blank until the first bat; after that a running count of the swarm pouring out.</summary>
        public void SetBats(int out_)
        {
            Set(_bats, out_ > 0 ? $"BATS OUT  {out_}" : string.Empty);
        }

        public void SetStatus(string text, Color color)
        {
            Set(_status, text);
            if (_status != null)
            {
                _status.color = color;
            }
        }

        public void SetWhy(string text)
        {
            Set(_why, text);
        }

        /// <summary>
        /// <paramref name="fraction"/> 1 = fresh candle. <paramref name="flicker"/> scales the flame (about 1).
        /// <paramref name="lit"/> false puts the flame out.
        /// </summary>
        public void SetCandle(float fraction, float flicker, bool lit)
        {
            if (_candleFill != null)
            {
                _candleFill.fillAmount = Mathf.Clamp01(fraction);
            }

            if (_flame != null)
            {
                _flame.anchorMin = new Vector2(0.5f, Mathf.Clamp01(fraction));
                _flame.anchorMax = new Vector2(0.5f, Mathf.Clamp01(fraction));
                _flame.anchoredPosition = Vector2.zero;
                _flame.localScale = new Vector3(flicker, flicker * (0.9f + 0.2f * flicker), 1f);
                _flame.gameObject.SetActive(lit);
            }
        }

        public void SetFlameColor(Color color)
        {
            if (_flameImage != null)
            {
                _flameImage.color = color;
            }
        }

        public void SetOverlay(Color color)
        {
            if (_overlay != null)
            {
                _overlay.color = color;
            }
        }

        static void Set(TMP_Text target, string text)
        {
            if (target != null && target.text != text)
            {
                target.text = text;
            }
        }

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(TMP_Text caption, TMP_Text subline, TMP_Text ghosts, TMP_Text rings, TMP_Text bats,
                         TMP_Text status, TMP_Text why, Image candleFill, RectTransform flame, Image flameImage,
                         Image overlay)
        {
            _caption = caption;
            _subline = subline;
            _ghosts = ghosts;
            _rings = rings;
            _bats = bats;
            _status = status;
            _why = why;
            _candleFill = candleFill;
            _flame = flame;
            _flameImage = flameImage;
            _overlay = overlay;
        }
    }
}
