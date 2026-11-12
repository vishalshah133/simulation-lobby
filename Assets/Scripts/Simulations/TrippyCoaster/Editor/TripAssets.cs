using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.TrippyCoaster.EditorTools
{
    /// <summary>Materials and meshes for the trip scene, saved as assets under Assets/Art/Generated/TrippyCoaster.</summary>
    public static class TripAssets
    {
        public const string Root = "Assets/Art/Generated/TrippyCoaster";
        public const string MeshFolder = Root + "/Meshes";
        public const string MaterialFolder = Root + "/Materials";

        static readonly int Mode = Shader.PropertyToID("_Mode");

        // ---------------------------------------------------------------- materials

        /// <summary>A TripPlasma material painted with the world's palette. Existing assets are reused and re-applied, so tuning in code always wins.</summary>
        public static Material Trip(string key, in TripWorldDef w, TripPattern pattern, float glowMul, bool worldUv, float pulse, float fog = 1f)
        {
            Shader shader = Shader.Find("SimulationLobby/TripPlasma");
            if (shader == null) throw new System.Exception("Shader SimulationLobby/TripPlasma not found (Assets/Shaders/Trip/TripPlasma.shader). Let Unity import it, then rebuild.");
            Material m = Fetch(key, shader);
            m.SetFloat(Mode, (float)pattern);
            m.SetColor("_PalA", w.palA);
            m.SetColor("_PalB", w.palB);
            m.SetColor("_PalC", w.palC);
            m.SetColor("_PalD", w.palD);
            m.SetFloat("_Glow", w.glow * glowMul);
            m.SetFloat("_Scale", Mathf.Max(0.01f, w.patternScale));
            m.SetFloat("_Period", 100f / Mathf.Max(0.5f, w.patternScale));
            m.SetFloat("_Speed", w.patternSpeed);
            m.SetFloat("_HueSpeed", w.hueSpeed);
            m.SetFloat("_Fold", w.lobes > 0 ? w.lobes : 10);
            m.SetFloat("_Pulse", pulse);
            m.SetFloat("_UseWorldUV", worldUv ? 1f : 0f);
            m.SetFloat("_Fog", fog);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Dark glossy URP Lit, for the cart body that the orbiting lights play over.</summary>
        public static Material CartBody()
        {
            Material m = Fetch("Trip_CartBody", Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.09f));
            m.SetFloat("_Metallic", 0.85f);
            m.SetFloat("_Smoothness", 0.9f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Fetch(string key, Shader shader)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = MaterialFolder + "/" + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            return m;
        }

        // ---------------------------------------------------------------- meshes

        /// <summary>Saves a generated mesh as a brand-new asset (delete first, never overwrite in place: a stale mesh once outlived a layout change).</summary>
        public static Mesh SaveMeshFresh(Mesh mesh, string name)
        {
            Directory.CreateDirectory(MeshFolder);
            string path = MeshFolder + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static readonly Dictionary<PrimitiveType, Mesh> Prims = new Dictionary<PrimitiveType, Mesh>();

        public static Mesh Primitive(PrimitiveType type)
        {
            if (Prims.TryGetValue(type, out Mesh cached) && cached != null) return cached;
            GameObject go = GameObject.CreatePrimitive(type);
            Mesh src = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            // a clean copy (positions, normals, uv0 only) so it combines with the generated meshes without attribute mismatches
            var m = new Mesh { indexFormat = IndexFormat.UInt32, name = "prim_" + type };
            m.SetVertices(src.vertices);
            m.SetNormals(src.normals);
            m.SetUVs(0, src.uv);
            m.SetTriangles(src.triangles, 0);
            m.RecalculateBounds();
            Prims[type] = m;
            return m;
        }

        /// <summary>Torus around the local Z axis (the ring faces forward when rotated with LookRotation).</summary>
        public static Mesh Torus(float majorR, float minorR, int segs, int sides)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var idx = new List<int>();
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                Vector3 c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (int j = 0; j <= sides; j++)
                {
                    float b = j / (float)sides * Mathf.PI * 2f;
                    Vector3 nn = c * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    v.Add(c * majorR + nn * minorR);
                    n.Add(nn);
                    uv.Add(new Vector2(i / (float)segs, j / (float)sides));
                }
            }
            int row = sides + 1;
            for (int i = 0; i < segs; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = i * row + j, b = a + row;
                    idx.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }
            return Build(v, n, uv, idx);
        }

        /// <summary>Unit octahedron (tips on the axes, radius 1).</summary>
        public static Mesh Octahedron()
        {
            Vector3[] p = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int[][] faces =
            {
                new[] { 2, 4, 0 }, new[] { 2, 0, 5 }, new[] { 2, 5, 1 }, new[] { 2, 1, 4 },
                new[] { 3, 0, 4 }, new[] { 3, 5, 0 }, new[] { 3, 1, 5 }, new[] { 3, 4, 1 }
            };
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var idx = new List<int>();
            foreach (int[] f in faces)
            {
                Vector3 nn = ((p[f[0]] + p[f[1]] + p[f[2]]) / 3f).normalized;
                for (int k = 0; k < 3; k++)
                {
                    idx.Add(v.Count);
                    v.Add(p[f[k]]);
                    n.Add(nn);
                    uv.Add(new Vector2(k == 1 ? 1f : 0f, k == 2 ? 1f : 0f));
                }
            }
            return Build(v, n, uv, idx);
        }

        static Mesh Build(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> idx)
        {
            var m = new Mesh { indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(idx, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Merges instances into one mesh (UInt32 indices so a long chunk never overflows 65k vertices).</summary>
        public static Mesh Combine(List<CombineInstance> parts)
        {
            var m = new Mesh { indexFormat = IndexFormat.UInt32 };
            m.CombineMeshes(parts.ToArray(), true, true);
            m.RecalculateBounds();
            return m;
        }
    }
}
