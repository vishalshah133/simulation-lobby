using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Screen-space text that follows points in the world: gate values, contender names, anything that
    /// has to sit on an arena object. It lives on an overlay canvas, so it stays crisp at both aspect
    /// targets, and it is never a world-space <c>TextMesh</c> parented into the arena.
    /// </summary>
    /// <remarks>
    /// Presentation only: positions are read from the world in <c>LateUpdate</c> by whoever calls
    /// <see cref="Place"/>, and nothing here touches the simulation. Format-agnostic by design.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public sealed class WorldLabelLayer : MonoBehaviour
    {
        [SerializeField] Camera _camera;
        [SerializeField] RectTransform _root;
        [SerializeField] TMP_FontAsset _font;

        readonly List<TMP_Text> _labels = new List<TMP_Text>();

        public int Count => _labels.Count;

        /// <summary>Assigned by the scene builder.</summary>
        public void Bind(Camera camera, RectTransform root)
        {
            _camera = camera;
            _root = root;
        }

        /// <summary>Adds a bold label and returns its index. Callers need no TextMeshPro reference.</summary>
        public int Add(string text, float fontSize, Color color)
        {
            return Add(text, fontSize, color, FontStyles.Bold);
        }

        /// <summary>Adds a label and returns its index.</summary>
        public int Add(string text, float fontSize, Color color, FontStyles style)
        {
            var labelObject = new GameObject($"Label_{_labels.Count:00}", typeof(RectTransform));
            labelObject.transform.SetParent(_root != null ? _root : transform, false);

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            if (_font != null)
            {
                label.font = _font;
            }

            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400f, fontSize * 1.4f);

            _labels.Add(label);
            return _labels.Count - 1;
        }

        public void SetText(int index, string text)
        {
            if (_labels[index].text != text)
            {
                _labels[index].text = text;
            }
        }

        public void SetColor(int index, Color color)
        {
            _labels[index].color = color;
        }

        /// <summary>Outlines a label so it reads on any board colour. Width is the TMP outline width, 0..1; 0 removes it.</summary>
        public void SetOutline(int index, Color color, float width)
        {
            TMP_Text label = _labels[index];
            label.outlineColor = color;
            label.outlineWidth = Mathf.Clamp01(width);
        }

        /// <summary>
        /// Sets which edge of the label sits on the placed point: 0 = left edge (left-aligned text),
        /// 0.5 = centred (default), 1 = right edge (right-aligned text).
        /// </summary>
        public void SetPivot(int index, float pivotX)
        {
            TMP_Text label = _labels[index];
            label.rectTransform.pivot = new Vector2(pivotX, 0.5f);
            label.alignment = pivotX <= 0.25f ? TextAlignmentOptions.MidlineLeft
                : pivotX >= 0.75f ? TextAlignmentOptions.MidlineRight
                : TextAlignmentOptions.Midline;
        }

        public void SetScale(int index, float scale)
        {
            RectTransform rect = _labels[index].rectTransform;
            if (!Mathf.Approximately(rect.localScale.x, scale))
            {
                rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>Moves a label to the screen position of a world point.</summary>
        public void Place(int index, Vector3 worldPosition)
        {
            if (_camera == null || _root == null)
            {
                return;
            }

            Vector3 screen = _camera.WorldToScreenPoint(worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
            RectTransform rect = _labels[index].rectTransform;
            if ((rect.anchoredPosition - local).sqrMagnitude > 0.01f)
            {
                rect.anchoredPosition = local;
            }
        }
    }
}
