using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Sweeps the <see cref="ICoasterRail"/> into combined meshes: two steel rails, cross ties with a centre spine, support
    /// columns to the ground, and a modest orange glow trim so the track reads at night. Meshes are saved fresh (delete,
    /// then create) under <c>Assets/Art/Generated/HorrorCoaster/Meshes</c>; an in-place overwrite once left the editor drawing the previous build.
    /// </summary>
    public static class CoasterTrackBuilder
    {
        public const string MeshFolder = "Assets/Art/Generated/HorrorCoaster/Meshes";

        const float RailGauge = 0.62f;       // rail centre, either side of the centreline
        const float RailHalfW = 0.06f;
        const float RailHalfH = 0.08f;
        const float RailDrop = 0.05f;        // rail centre below the sample point
        const float SpineDrop = 0.55f;
        const float TieSpacing = 1.5f;
        const float SupportSpacing = 13f;
        const float SampleStep = 0.75f;

        /// <summary>Delete-then-create save, the same rule as the shared SaveMeshFresh (that assembly is not referenced by this asmdef).</summary>
        public static Mesh SaveMeshFresh(Mesh mesh, string name)
        {
            Directory.CreateDirectory(MeshFolder);
            string path = $"{MeshFolder}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        /// <summary>
        /// Builds "Track" under <paramref name="parent"/>. <paramref name="noSupportRanges"/> are (startDistance, endDistance) pairs
        /// where no support columns and no tie lamps are generated (tunnels, interiors, the trip).
        /// </summary>
        public static GameObject Build(Transform parent, ICoasterRail rail, float groundY, IList<Vector2> noSupportRanges)
        {
            var root = new GameObject("Track");
            root.transform.SetParent(parent, false);

            var rails = new MeshData();
            var ties = new MeshData();
            var supports = new MeshData();
            var glow = new MeshData();

            int count = Mathf.CeilToInt(rail.Length / SampleStep) + 1;
            var pos = new Vector3[count];
            var up = new Vector3[count];
            var right = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float d = Mathf.Min(i * SampleStep, rail.Length);
                rail.Sample(d, out pos[i], out Vector3 f, out up[i]);
                right[i] = Vector3.Cross(up[i], f).normalized;
            }

            // Rails and the spine are continuous sweeps.
            for (int side = -1; side <= 1; side += 2)
            {
                Sweep(rails, pos, up, right, side * RailGauge, -RailDrop, RailHalfW, RailHalfH);
            }

            Sweep(rails, pos, up, right, 0f, -SpineDrop, 0.11f, 0.1f);

            // Glow trim: a thin orange line on the outside of each rail head, kept modest.
            for (int side = -1; side <= 1; side += 2)
            {
                Sweep(glow, pos, up, right, side * (RailGauge + RailHalfW + 0.025f), -RailDrop + 0.01f, 0.018f, 0.03f);
            }

            // Ties, struts to the spine, and a lamp at the end of every eighth.
            int tieIndex = 0;
            for (float d = 0.5f; d < rail.Length; d += TieSpacing, tieIndex++)
            {
                rail.Sample(d, out Vector3 p, out Vector3 f, out Vector3 u);
                Vector3 r = Vector3.Cross(u, f).normalized;
                AddBox(ties, p - u * 0.2f, r, u, f, new Vector3(0.82f, 0.045f, 0.1f));
                AddBox(ties, p - u * 0.38f, r, u, f, new Vector3(0.05f, 0.2f, 0.07f));
                if (tieIndex % 8 == 0 && !InRanges(d, noSupportRanges))
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        AddBox(glow, p - u * 0.2f + r * (side * 0.84f), r, u, f, new Vector3(0.05f, 0.05f, 0.05f));
                    }
                }
            }

            // Supports: a column from under the spine to the ground with a cross arm and a footing.
            for (float d = SupportSpacing * 0.5f; d < rail.Length; d += SupportSpacing)
            {
                if (InRanges(d, noSupportRanges))
                {
                    continue;
                }

                rail.Sample(d, out Vector3 p, out Vector3 f, out Vector3 u);
                if (u.y < 0.3f)
                {
                    continue; // inverted or on its side: a column would pierce the track
                }

                float top = p.y - SpineDrop - 0.1f;
                float height = top - groundY;
                if (height < 1.2f)
                {
                    continue;
                }

                Vector3 r = Vector3.Cross(u, f).normalized;
                Vector3 flatF = new Vector3(f.x, 0f, f.z);
                flatF = flatF.sqrMagnitude < 1e-4f ? Vector3.forward : flatF.normalized;
                Vector3 flatR = Vector3.Cross(Vector3.up, flatF);
                Vector3 mid = new Vector3(p.x, groundY + height * 0.5f, p.z);
                AddBox(supports, mid, flatR, Vector3.up, flatF, new Vector3(0.22f, height * 0.5f, 0.22f));
                AddBox(supports, p - u * (SpineDrop + 0.18f), r, u, f, new Vector3(1.05f, 0.08f, 0.14f));
                AddBox(supports, new Vector3(p.x, groundY + 0.15f, p.z), flatR, Vector3.up, flatF, new Vector3(0.7f, 0.15f, 0.7f));
            }

            Material steel = CoasterMaterials.Get("TrackSteel", new Color(0.2f, 0.21f, 0.27f), default, 0.55f, 0.65f);
            Material wood = CoasterMaterials.Get("TrackTie", new Color(0.17f, 0.11f, 0.09f), default, 0.2f);
            Material support = CoasterMaterials.Get("TrackSupport", new Color(0.1f, 0.1f, 0.14f), default, 0.3f, 0.5f);
            Material trim = CoasterMaterials.Get("TrackGlow", CoasterPalette.Pumpkin * 0.35f, CoasterPalette.Glow(CoasterPalette.Pumpkin, 1.5f), 0.2f);

            AddMeshChild(root.transform, "Rails", rails, "Coaster_Rails", steel, true);
            AddMeshChild(root.transform, "Ties", ties, "Coaster_Ties", wood, true);
            AddMeshChild(root.transform, "Supports", supports, "Coaster_Supports", support, true);
            AddMeshChild(root.transform, "Glow Trim", glow, "Coaster_Glow", trim, false);

            AssetDatabase.SaveAssets();
            return root;
        }

        static bool InRanges(float d, IList<Vector2> ranges)
        {
            if (ranges == null)
            {
                return false;
            }

            for (int i = 0; i < ranges.Count; i++)
            {
                if (d >= ranges[i].x && d <= ranges[i].y)
                {
                    return true;
                }
            }

            return false;
        }

        static void AddMeshChild(Transform parent, string name, MeshData data, string assetName, Material material, bool shadows)
        {
            Mesh mesh = SaveMeshFresh(data.ToMesh(), assetName);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        // ---------------------------------------------------------------- geometry

        internal sealed class MeshData
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<int> t = new List<int>();

            /// <summary>Flat quad whose normal faces <paramref name="hint"/>.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 hint)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f)
                {
                    normal = Vector3.Cross(c - a, d - a);
                }

                bool flip = Vector3.Dot(normal, hint) < 0f;
                normal = normal.sqrMagnitude < 1e-12f ? hint.normalized : normal.normalized * (flip ? -1f : 1f);
                int i = v.Count;
                v.Add(a);
                v.Add(b);
                v.Add(c);
                v.Add(d);
                for (int k = 0; k < 4; k++)
                {
                    n.Add(normal);
                }

                if (!flip)
                {
                    t.Add(i); t.Add(i + 1); t.Add(i + 2);
                    t.Add(i); t.Add(i + 2); t.Add(i + 3);
                }
                else
                {
                    t.Add(i); t.Add(i + 2); t.Add(i + 1);
                    t.Add(i); t.Add(i + 3); t.Add(i + 2);
                }
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        internal static void AddBox(MeshData m, Vector3 c, Vector3 r, Vector3 u, Vector3 f, Vector3 h)
        {
            Vector3 x = r * h.x, y = u * h.y, z = f * h.z;
            Vector3 p000 = c - x - y - z, p100 = c + x - y - z, p110 = c + x + y - z, p010 = c - x + y - z;
            Vector3 p001 = c - x - y + z, p101 = c + x - y + z, p111 = c + x + y + z, p011 = c - x + y + z;
            m.Quad(p000, p100, p110, p010, -f);
            m.Quad(p001, p101, p111, p011, f);
            m.Quad(p000, p100, p101, p001, -u);
            m.Quad(p010, p110, p111, p011, u);
            m.Quad(p000, p010, p011, p001, -r);
            m.Quad(p100, p110, p111, p101, r);
        }

        /// <summary>Sweeps a rectangle (halfW x halfH, centred at lateral/vertical offset in the rail frame) along the samples.</summary>
        internal static void Sweep(MeshData m, Vector3[] pos, Vector3[] up, Vector3[] right, float lateral, float vertical, float halfW, float halfH)
        {
            for (int i = 0; i + 1 < pos.Length; i++)
            {
                Vector3 c0 = pos[i] + right[i] * lateral + up[i] * vertical;
                Vector3 c1 = pos[i + 1] + right[i + 1] * lateral + up[i + 1] * vertical;
                Vector3 r0 = right[i] * halfW, u0 = up[i] * halfH;
                Vector3 r1 = right[i + 1] * halfW, u1 = up[i + 1] * halfH;
                Vector3 a0 = c0 - r0 - u0, b0 = c0 + r0 - u0, e0 = c0 + r0 + u0, d0 = c0 - r0 + u0;
                Vector3 a1 = c1 - r1 - u1, b1 = c1 + r1 - u1, e1 = c1 + r1 + u1, d1 = c1 - r1 + u1;
                m.Quad(a0, b0, b1, a1, -(up[i] + up[i + 1]));
                m.Quad(b0, e0, e1, b1, right[i] + right[i + 1]);
                m.Quad(e0, d0, d1, e1, up[i] + up[i + 1]);
                m.Quad(d0, a0, a1, d1, -(right[i] + right[i + 1]));
            }
        }
    }
}
