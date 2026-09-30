using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// A static collider described by a signed distance function: negative inside, positive outside.
    /// What <see cref="SoftBodySolver"/> collides against instead of PhysX colliders.
    /// </summary>
    /// <remarks>
    /// Analytic shapes rather than PhysX for two reasons: the solver owns every float that decides a
    /// run (so it is reproducible by construction), and an SDF answers "how far inside, and which way
    /// out" exactly, which is the only question a position-based solver asks.
    /// </remarks>
    public abstract class SdfCollider
    {
        /// <summary>Coulomb friction used when a particle is resting (tangential slip below the limit).</summary>
        public float staticFriction = 0.6f;

        /// <summary>Friction once sliding.</summary>
        public float dynamicFriction = 0.4f;

        /// <summary>
        /// A pierceable collider impales a soft body instead of holding it off: particles are pushed
        /// out sideways (<see cref="PierceNormal"/>) rather than along the surface normal, so the skin
        /// parts around the collider and the body slides down it. Ignored by the rigid path.
        /// </summary>
        /// <remarks>
        /// <b>Experimental — not production-ready.</b> Impaling works, but without tearing the skin
        /// around the hole has to stretch to the collider's full girth; its edges keep dragging
        /// particles across the collider and they flip sides every substep (100+ m/s jitter in
        /// testing). Needs a tearing model before a video can use it (see
        /// <c>KnowledgeBase/formats/impact.md</c>).
        /// </remarks>
        public bool pierceable;

        /// <summary>
        /// Vertical drag per second on particles touching a pierceable collider — how hard the body
        /// grips the spike as it slides down it. High = stops near the tip, low = slumps to the base.
        /// </summary>
        public float pierceDrag;

        public abstract float Distance(Vector3 point);

        /// <summary>Outward surface normal at a point near the surface. Central differences by default.</summary>
        public virtual Vector3 Normal(Vector3 point)
        {
            const float e = 5e-4f;
            var n = new Vector3(
                Distance(point + new Vector3(e, 0f, 0f)) - Distance(point - new Vector3(e, 0f, 0f)),
                Distance(point + new Vector3(0f, e, 0f)) - Distance(point - new Vector3(0f, e, 0f)),
                Distance(point + new Vector3(0f, 0f, e)) - Distance(point - new Vector3(0f, 0f, e)));
            float length = n.magnitude;
            return length > 1e-8f ? n / length : Vector3.up;
        }

        /// <summary>
        /// Push-out direction when <see cref="pierceable"/>: horizontal, away from the collider's
        /// vertical axis. A particle dead on the axis goes +X, so the result stays deterministic.
        /// </summary>
        public virtual Vector3 PierceNormal(Vector3 point) => Normal(point);
    }

    /// <summary>Infinite plane (a floor). <see cref="normal"/> points to the free side.</summary>
    public sealed class SdfPlane : SdfCollider
    {
        public readonly Vector3 point;
        public readonly Vector3 normal;

        public SdfPlane(Vector3 point, Vector3 normal)
        {
            this.point = point;
            this.normal = normal.normalized;
        }

        public override float Distance(Vector3 p) => Vector3.Dot(p - point, normal);

        public override Vector3 Normal(Vector3 p) => normal;
    }

    /// <summary>Axis-aligned box (a plinth).</summary>
    public sealed class SdfBox : SdfCollider
    {
        public readonly Vector3 center;
        public readonly Vector3 halfExtents;

        public SdfBox(Vector3 center, Vector3 halfExtents)
        {
            this.center = center;
            this.halfExtents = halfExtents;
        }

        public override float Distance(Vector3 p)
        {
            Vector3 q = p - center;
            q = new Vector3(Mathf.Abs(q.x), Mathf.Abs(q.y), Mathf.Abs(q.z)) - halfExtents;
            Vector3 outside = Vector3.Max(q, Vector3.zero);
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        }
    }

    /// <summary>Vertical capped cylinder between <see cref="yMin"/> and <see cref="yMax"/>.</summary>
    public sealed class SdfCylinderY : SdfCollider
    {
        public readonly Vector3 axisPoint;
        public readonly float radius;
        public readonly float yMin;
        public readonly float yMax;

        public SdfCylinderY(Vector3 axisPoint, float radius, float yMin, float yMax)
        {
            this.axisPoint = axisPoint;
            this.radius = radius;
            this.yMin = yMin;
            this.yMax = yMax;
        }

        public override float Distance(Vector3 p)
        {
            float dx = p.x - axisPoint.x;
            float dz = p.z - axisPoint.z;
            float radial = Mathf.Sqrt(dx * dx + dz * dz) - radius;
            float vertical = Mathf.Max(yMin - p.y, p.y - yMax);
            float outsideR = Mathf.Max(radial, 0f);
            float outsideV = Mathf.Max(vertical, 0f);
            return Mathf.Min(Mathf.Max(radial, vertical), 0f) + Mathf.Sqrt(outsideR * outsideR + outsideV * outsideV);
        }
    }

    /// <summary>Horizontal torus (a ring lying flat) — a rounded lip around a hole.</summary>
    public sealed class SdfTorusY : SdfCollider
    {
        public readonly Vector3 center;
        public readonly float majorRadius;
        public readonly float minorRadius;

        public SdfTorusY(Vector3 center, float majorRadius, float minorRadius)
        {
            this.center = center;
            this.majorRadius = majorRadius;
            this.minorRadius = minorRadius;
        }

        public override float Distance(Vector3 p)
        {
            float dx = p.x - center.x;
            float dz = p.z - center.z;
            float qx = Mathf.Sqrt(dx * dx + dz * dz) - majorRadius;
            float qy = p.y - center.y;
            return Mathf.Sqrt(qx * qx + qy * qy) - minorRadius;
        }
    }

    /// <summary>
    /// <see cref="solid"/> with <see cref="cut"/> carved out of it (CSG subtraction) — a plinth with a
    /// hole in it. Distance is the standard max(a, −b); not exact near the cut's rim, which is fine for
    /// collision because the solver only needs the sign and a direction out.
    /// </summary>
    public sealed class SdfSubtract : SdfCollider
    {
        public readonly SdfCollider solid;
        public readonly SdfCollider cut;

        public SdfSubtract(SdfCollider solid, SdfCollider cut)
        {
            this.solid = solid;
            this.cut = cut;
        }

        public override float Distance(Vector3 p) => Mathf.Max(solid.Distance(p), -cut.Distance(p));
    }

    /// <summary>
    /// Vertical cone with rounded ends — a sphere of <see cref="baseRadius"/> at <see cref="basePoint"/>
    /// swept into a sphere of <see cref="tipRadius"/> <see cref="height"/> above it. A spike whose tip
    /// is a small sphere rather than a mathematical point, which is what keeps a soft body's surface
    /// from threading straight through it (see <see cref="TipCenter"/>).
    /// </summary>
    /// <remarks>Exact distance after Inigo Quilez's <c>sdRoundCone</c>.</remarks>
    public sealed class SdfRoundCone : SdfCollider
    {
        public readonly Vector3 basePoint;
        public readonly float baseRadius;
        public readonly float tipRadius;
        public readonly float height;

        readonly float _b;
        readonly float _a;

        public SdfRoundCone(Vector3 basePoint, float baseRadius, float tipRadius, float height)
        {
            this.basePoint = basePoint;
            this.baseRadius = baseRadius;
            this.tipRadius = tipRadius;
            this.height = height;
            _b = (baseRadius - tipRadius) / height;
            _a = Mathf.Sqrt(Mathf.Max(0f, 1f - _b * _b));
        }

        public Vector3 TipCenter => basePoint + Vector3.up * height;

        public override Vector3 PierceNormal(Vector3 point)
        {
            var radial = new Vector3(point.x - basePoint.x, 0f, point.z - basePoint.z);
            float length = radial.magnitude;
            return length > 1e-6f ? radial / length : Vector3.right;
        }

        /// <summary>Highest point of the spike — the tip sphere's top.</summary>
        public float TopY => basePoint.y + height + tipRadius;

        public override float Distance(Vector3 p)
        {
            Vector3 local = p - basePoint;
            float qx = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            float qy = local.y;
            float k = -_b * qx + _a * qy;
            if (k < 0f)
            {
                return Mathf.Sqrt(qx * qx + qy * qy) - baseRadius;
            }

            if (k > _a * height)
            {
                float dy = qy - height;
                return Mathf.Sqrt(qx * qx + dy * dy) - tipRadius;
            }

            return qx * _a + qy * _b - baseRadius;
        }

        /// <summary>
        /// Lathe mesh matching this exact shape, so what the viewer sees is what the solver collides
        /// with. The base sphere is replaced by a short straight skirt, since the base is buried.
        /// </summary>
        public Mesh BuildMesh(int segments = 72, int capSteps = 10, float skirt = 0.3f)
        {
            // Profile in (radius, height) from the buried skirt up to the tip's pole.
            var profile = new System.Collections.Generic.List<Vector2>();
            Vector2 baseTangent = new Vector2(baseRadius * _a, baseRadius * _b);
            Vector2 tipTangent = new Vector2(tipRadius * _a, height + tipRadius * _b);
            profile.Add(new Vector2(baseTangent.x, baseTangent.y - skirt));
            profile.Add(baseTangent);
            const int sideSteps = 24;
            for (int i = 1; i < sideSteps; i++)
            {
                profile.Add(Vector2.Lerp(baseTangent, tipTangent, i / (float)sideSteps));
            }

            float startAngle = Mathf.Atan2(_b, _a);
            for (int i = 0; i < capSteps; i++)
            {
                float angle = Mathf.Lerp(startAngle, Mathf.PI * 0.5f, i / (float)capSteps);
                profile.Add(new Vector2(tipRadius * Mathf.Cos(angle), height + tipRadius * Mathf.Sin(angle)));
            }

            return MeshLathe.Build(profile, segments, basePoint, "SpikeMesh");
        }
    }

    /// <summary>Revolves a (radius, height) profile about the Y axis into a welded, smooth mesh.</summary>
    public static class MeshLathe
    {
        /// <param name="profile">Bottom to top. The last point's radius is replaced by a pole vertex.</param>
        public static Mesh Build(System.Collections.Generic.IList<Vector2> profile, int segments, Vector3 origin, string name)
        {
            int rings = profile.Count;
            var vertices = new System.Collections.Generic.List<Vector3>(rings * segments + 2);
            var triangles = new System.Collections.Generic.List<int>();

            // Bottom centre closes the skirt so no gap shows from a low angle.
            vertices.Add(origin + new Vector3(0f, profile[0].y, 0f));
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    float angle = s * Mathf.PI * 2f / segments;
                    vertices.Add(origin + new Vector3(Mathf.Cos(angle) * profile[r].x, profile[r].y,
                        Mathf.Sin(angle) * profile[r].x));
                }
            }

            int pole = vertices.Count;
            Vector2 last = profile[rings - 1];
            vertices.Add(origin + new Vector3(0f, last.y + (last.x > 0f ? last.x * 0.25f : 0f), 0f));

            int Ring(int r, int s) => 1 + r * segments + (s % segments);

            for (int s = 0; s < segments; s++)
            {
                triangles.Add(0);
                triangles.Add(Ring(0, s));
                triangles.Add(Ring(0, s + 1));
            }

            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = Ring(r, s);
                    int b = Ring(r, s + 1);
                    int c = Ring(r + 1, s);
                    int d = Ring(r + 1, s + 1);
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            for (int s = 0; s < segments; s++)
            {
                triangles.Add(Ring(rings - 1, s));
                triangles.Add(pole);
                triangles.Add(Ring(rings - 1, s + 1));
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            MeshWinding.EnsureOutward(mesh, origin + new Vector3(0f, profile[rings / 2].y, 0f));
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Winding fix-up so generated meshes face outward without hand-checking each generator.</summary>
    public static class MeshWinding
    {
        /// <summary>
        /// Flips every triangle if, on average, their normals point toward <paramref name="interior"/>.
        /// Cheaper than getting each generator's winding right by reasoning, and impossible to get wrong.
        /// </summary>
        public static void EnsureOutward(Mesh mesh, Vector3 interior)
        {
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            if (SignedOutwardness(v, t, interior) >= 0f)
            {
                return;
            }

            for (int i = 0; i < t.Length; i += 3)
            {
                (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }

            mesh.triangles = t;
        }

        public static float SignedOutwardness(Vector3[] v, int[] t, Vector3 interior)
        {
            float sum = 0f;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]];
                Vector3 b = v[t[i + 1]];
                Vector3 c = v[t[i + 2]];
                // Unity's front face is clockwise seen from the front, so the outward normal of
                // (a, b, c) is (b - a) x (c - a).
                Vector3 n = Vector3.Cross(b - a, c - a);
                sum += Vector3.Dot(n, (a + b + c) / 3f - interior);
            }

            return sum;
        }
    }
}
