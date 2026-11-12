using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>Procedural meshes for the micro scares. Always saved fresh (delete-then-create) with a CoastMicro_ prefix.</summary>
    internal static class MicroScareMeshes
    {
        public static Mesh Finger() { return ActSetBuildKit.SaveMeshFresh(BuildFinger(0.30f, 0.020f, 0.004f, 0.35f), "CoastMicro_Finger"); }
        public static Mesh CoffinBody() { return ActSetBuildKit.SaveMeshFresh(BuildCoffinBody(), "CoastMicro_CoffinBody"); }
        public static Mesh CoffinLid() { return ActSetBuildKit.SaveMeshFresh(BuildCoffinLid(), "CoastMicro_CoffinLid"); }
        public static Mesh Shard() { return ActSetBuildKit.SaveMeshFresh(BuildShard(), "CoastMicro_Shard"); }
        public static Mesh Bat() { return ActSetBuildKit.SaveMeshFresh(MeshAcc.Bat(), "CoastMicro_Bat"); }

        // Coffin outline (x, y), feet at y = 0, front plane at z = 0, depth runs toward -z.
        static readonly Vector2[] CoffinOutline =
        {
            new Vector2(-0.20f, 0f), new Vector2(0.20f, 0f), new Vector2(0.42f, 1.40f),
            new Vector2(0.20f, 2.00f), new Vector2(-0.20f, 2.00f), new Vector2(-0.42f, 1.40f)
        };

        /// <summary>Tapered 6-sided finger along +y, bending toward +z, last 15% is submesh 1 (the claw).</summary>
        static Mesh BuildFinger(float length, float r0, float r1, float bend)
        {
            const int segs = 8, sides = 6;
            var mb = new MeshAcc();
            var ring = new int[segs + 1, sides];
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                Vector3 c = new Vector3(0f, length * t, bend * length * t * t);
                Vector3 tan = new Vector3(0f, 1f, 2f * bend * t).normalized;
                Vector3 side = Vector3.right;
                Vector3 up2 = Vector3.Cross(tan, side).normalized;
                float knuckle = 1f + 0.3f * Mathf.Pow(Mathf.Sin(t * Mathf.PI * 2f), 2f) * (1f - t);
                float r = Mathf.Lerp(r0, r1, t) * knuckle;
                if (i == segs) r = 0.0005f;
                for (int s = 0; s < sides; s++)
                {
                    float a = 2f * Mathf.PI * s / sides;
                    Vector3 n = side * Mathf.Cos(a) + up2 * Mathf.Sin(a);
                    ring[i, s] = mb.Add(c + n * r, n);
                }
            }

            for (int i = 0; i < segs; i++)
            {
                int sub = i >= segs - 2 ? 1 : 0;
                for (int s = 0; s < sides; s++)
                {
                    int s2 = (s + 1) % sides;
                    mb.Tri(sub, ring[i, s], ring[i + 1, s], ring[i, s2]);
                    mb.Tri(sub, ring[i, s2], ring[i + 1, s], ring[i + 1, s2]);
                }
            }

            Mesh m = mb.ToMesh();
            if (m.subMeshCount < 2) m.subMeshCount = 2;
            return m;
        }

        static Mesh BuildCoffinBody()
        {
            var mb = new MeshAcc();
            const float depth = 0.5f;
            int n = CoffinOutline.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = CoffinOutline[i], b = CoffinOutline[(i + 1) % n];
                Vector2 e2 = (b - a).normalized;
                Vector3 nrm = new Vector3(e2.y, -e2.x, 0f);
                int v0 = mb.Add(new Vector3(a.x, a.y, 0f), nrm);
                int v1 = mb.Add(new Vector3(b.x, b.y, 0f), nrm);
                int v2 = mb.Add(new Vector3(a.x, a.y, -depth), nrm);
                int v3 = mb.Add(new Vector3(b.x, b.y, -depth), nrm);
                mb.Tri(0, v0, v1, v2); mb.Tri(0, v1, v3, v2);
                mb.Tri(0, v0, v2, v1); mb.Tri(0, v1, v2, v3);
            }

            var outline = new List<Vector2>(CoffinOutline);
            mb.FlatFan(0, new Vector2(0f, 1.0f), outline, -depth);
            return mb.ToMesh();
        }

        /// <summary>Lid panel: the coffin outline, centred on the hinge-edge x = 0.42 (pivot at the right shoulder), double sided.</summary>
        static Mesh BuildCoffinLid()
        {
            var mb = new MeshAcc();
            var outline = new List<Vector2>();
            foreach (Vector2 v in CoffinOutline) outline.Add(new Vector2(v.x - 0.42f, v.y - 1.0f));
            mb.FlatFan(0, new Vector2(-0.42f, 0f), outline, 0f);
            return mb.ToMesh();
        }

        static Mesh BuildShard()
        {
            var mb = new MeshAcc();
            var outline = new List<Vector2>
            {
                new Vector2(-0.06f, -0.05f), new Vector2(0.07f, -0.03f), new Vector2(0.03f, 0.09f), new Vector2(-0.05f, 0.04f)
            };
            mb.FlatFan(0, new Vector2(0f, 0f), outline, 0f);
            return mb.ToMesh();
        }
    }
}
