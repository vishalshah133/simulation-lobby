using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Two shapes the built-in primitives lack, saved once as assets (they never change shape, so create-if-missing is safe):
    /// a unit cone (base radius 0.5 at y=0, apex at y=1) and a unit gable prism (x -0.5..0.5, apex at y=1, ridge along z).
    /// </summary>
    public static class CoasterMeshes
    {
        public const string Folder = "Assets/Art/Generated/HorrorCoaster/Meshes/Kit";
        static Mesh cone;
        static Mesh prism;

        public static Mesh Cone
        {
            get
            {
                if (cone == null)
                {
                    cone = Ensure("Kit_Cone", MakeCone);
                }

                return cone;
            }
        }

        public static Mesh Prism
        {
            get
            {
                if (prism == null)
                {
                    prism = Ensure("Kit_Prism", MakePrism);
                }

                return prism;
            }
        }

        static Mesh Ensure(string name, Func<Mesh> make)
        {
            Directory.CreateDirectory(Folder);
            string path = $"{Folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            Mesh m = make();
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static void Tri(CoasterTrackBuilder.MeshData m, Vector3 a, Vector3 b, Vector3 c, Vector3 hint)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            bool flip = Vector3.Dot(n, hint) < 0f;
            n = n.sqrMagnitude < 1e-12f ? hint.normalized : n.normalized * (flip ? -1f : 1f);
            int i = m.v.Count;
            m.v.Add(a);
            m.v.Add(flip ? c : b);
            m.v.Add(flip ? b : c);
            m.n.Add(n);
            m.n.Add(n);
            m.n.Add(n);
            m.t.Add(i);
            m.t.Add(i + 1);
            m.t.Add(i + 2);
        }

        static Mesh Finish(CoasterTrackBuilder.MeshData m)
        {
            Mesh mesh = m.ToMesh();
            return mesh;
        }

        static Mesh MakeCone()
        {
            var m = new CoasterTrackBuilder.MeshData();
            const int seg = 16;
            Vector3 apex = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg;
                float a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                Vector3 mid = (b0 + b1) * 0.5f;
                Tri(m, apex, b0, b1, mid + Vector3.up * 0.25f);
                Tri(m, Vector3.zero, b0, b1, Vector3.down);
            }

            return Finish(m);
        }

        static Mesh MakePrism()
        {
            var m = new CoasterTrackBuilder.MeshData();
            Vector3 l0 = new Vector3(-0.5f, 0f, -0.5f), l1 = new Vector3(-0.5f, 0f, 0.5f);
            Vector3 r0 = new Vector3(0.5f, 0f, -0.5f), r1 = new Vector3(0.5f, 0f, 0.5f);
            Vector3 t0 = new Vector3(0f, 1f, -0.5f), t1 = new Vector3(0f, 1f, 0.5f);
            Tri(m, l1, r1, t1, Vector3.forward);
            Tri(m, l0, r0, t0, Vector3.back);
            m.Quad(l0, l1, t1, t0, new Vector3(-1f, 0.5f, 0f));
            m.Quad(r0, r1, t1, t0, new Vector3(1f, 0.5f, 0f));
            m.Quad(l0, r0, r1, l1, Vector3.down);
            return Finish(m);
        }
    }
}
