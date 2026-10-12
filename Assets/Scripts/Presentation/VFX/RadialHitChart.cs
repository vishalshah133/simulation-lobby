using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// A spider graph of shots: lines that fan out from a centre, each at an angle and a length, drawn
    /// in over a moment. On a cricket ground this is the wagon wheel.
    /// </summary>
    /// <remarks>
    /// Knows nothing about any sport: it takes an angle (degrees clockwise from straight up the
    /// screen), a length as a fraction of the radius, a colour and a style. Reads what it is told and
    /// animates on <c>Time.time</c>, which is fine because nothing here feeds back into a run.
    /// </remarks>
    public sealed class RadialHitChart : MonoBehaviour
    {
        const int ArcSegments = 14;

        [Min(0.5f)] public float radius = 3f;
        [Range(0.05f, 1.5f)] public float drawSeconds = 0.45f;
        public int sortingOrder = 6;
        [Range(0.02f, 0.3f)] public float lineWidth = 0.1f;
        [Range(0f, 1f)] public float olderAlpha = 0.75f;

        [Tooltip("How far a lofted shot bows out sideways, as a fraction of its length.")]
        [Range(0f, 0.6f)] public float arcBow = 0.22f;

        Material _material;
        LineRenderer _boundary;
        LineRenderer _inner;
        readonly List<Shot> _shots = new List<Shot>();
        readonly List<LineRenderer> _spare = new List<LineRenderer>();

        sealed class Shot
        {
            public LineRenderer line;
            public Vector3[] points;
            public Color color;
            public float start;
        }

        /// <summary>Builds the rings. Call once, after setting <see cref="radius"/>.</summary>
        public void Build(Color ringColor)
        {
            if (_material == null)
            {
                _material = new Material(Shader.Find("Sprites/Default"));
            }

            if (_boundary == null)
            {
                _boundary = NewLine("Boundary", 0.14f, sortingOrder - 2);
                _boundary.loop = true;
            }

            if (_inner == null)
            {
                _inner = NewLine("Inner Ring", 0.05f, sortingOrder - 2);
                _inner.loop = true;
            }

            DrawRing(_boundary, radius);
            DrawRing(_inner, radius * 0.5f);
            SetRingColor(ringColor);
        }

        public void SetRingColor(Color color)
        {
            if (_boundary != null)
            {
                SetColor(_boundary, color);
            }

            if (_inner != null)
            {
                Color faint = color;
                faint.a *= 0.55f;
                SetColor(_inner, faint);
            }
        }

        /// <summary>
        /// Adds a shot line. <paramref name="angle"/> is degrees clockwise from straight up the screen;
        /// <paramref name="length"/> is 0-1 of the radius; a <paramref name="lofted"/> shot is drawn as an arc.
        /// </summary>
        public void AddShot(float angle, float length, Color color, bool lofted, float widthScale = 1f)
        {
            if (_material == null)
            {
                _material = new Material(Shader.Find("Sprites/Default"));
            }

            LineRenderer line = Rent();
            line.widthMultiplier = lineWidth * widthScale;

            float radians = angle * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            float reach = Mathf.Clamp01(length) * radius;

            Vector3[] points;
            if (lofted)
            {
                points = new Vector3[ArcSegments];
                var side = new Vector2(direction.y, -direction.x);
                for (int i = 0; i < ArcSegments; i++)
                {
                    float f = i / (float)(ArcSegments - 1);
                    // A parabola off the straight line, so a six visibly flies over the rope.
                    float bow = 4f * f * (1f - f) * arcBow * reach;
                    Vector2 p = direction * (reach * f) + side * bow;
                    points[i] = LocalToWorld(p);
                }
            }
            else
            {
                points = new[] { LocalToWorld(Vector2.zero), LocalToWorld(direction * reach) };
            }

            line.positionCount = 0;
            _shots.Add(new Shot { line = line, points = points, color = color, start = Time.time });
        }

        /// <summary>Removes every shot, e.g. between innings. The rings stay.</summary>
        public void Clear()
        {
            for (int i = 0; i < _shots.Count; i++)
            {
                _shots[i].line.gameObject.SetActive(false);
                _spare.Add(_shots[i].line);
            }

            _shots.Clear();
        }

        void LateUpdate()
        {
            for (int i = 0; i < _shots.Count; i++)
            {
                Shot shot = _shots[i];
                float t = Mathf.Clamp01((Time.time - shot.start) / drawSeconds);
                // Ease-out: the line leaves the bat fast and settles at its length.
                float eased = 1f - (1f - t) * (1f - t);

                int total = shot.points.Length;
                float scaled = eased * (total - 1);
                int whole = Mathf.Min(total - 1, Mathf.FloorToInt(scaled));
                int shown = Mathf.Min(total, whole + 2);

                shot.line.positionCount = shown;
                for (int p = 0; p < shown; p++)
                {
                    shot.line.SetPosition(p, shot.points[p]);
                }

                // The head of the line is interpolated, so it grows smoothly between points.
                if (whole < total - 1 && shown > 1)
                {
                    float within = scaled - whole;
                    shot.line.SetPosition(shown - 1, Vector3.Lerp(shot.points[whole], shot.points[whole + 1], within));
                }

                bool newest = i == _shots.Count - 1;
                Color c = shot.color;
                c.a = newest ? 1f : olderAlpha;
                SetColor(shot.line, c);
                shot.line.widthMultiplier = lineWidth * (newest ? 1.25f : 1f);
            }
        }

        Vector3 LocalToWorld(Vector2 local)
        {
            return transform.TransformPoint(new Vector3(local.x, local.y, 0f));
        }

        LineRenderer Rent()
        {
            LineRenderer line;
            if (_spare.Count > 0)
            {
                line = _spare[_spare.Count - 1];
                _spare.RemoveAt(_spare.Count - 1);
                line.gameObject.SetActive(true);
            }
            else
            {
                line = NewLine($"Shot_{_shots.Count + _spare.Count}", lineWidth, sortingOrder);
            }

            return line;
        }

        LineRenderer NewLine(string lineName, float width, int order)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _material;
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        void DrawRing(LineRenderer line, float ringRadius)
        {
            const int segments = 72;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, LocalToWorld(new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * ringRadius));
            }
        }

        static void SetColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }
    }
}
