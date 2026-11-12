using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>Shared builder helpers for the C2 act sets (Trip, Mansion, Asylum, Final, StationReturn).</summary>
    internal static class ActSetBuildKit
    {
        public static Material M(string key, Color c, Color e = default, float smooth = 0.3f, float metal = 0f, bool transparent = false)
        {
            return CoasterMaterials.Get(key, c, e, smooth, metal, transparent);
        }

        public static float HashF(int a, int salt) { return ActSetKit.Hash01(a, salt); }

        public static Color Hdr(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

        public static GameObject Empty(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            return go;
        }

        public static GameObject Empty(string name, Transform parent)
        {
            return Empty(name, parent, Vector3.zero, Quaternion.identity);
        }

        /// <summary>World-space placed empty (parent keeps identity scale).</summary>
        public static GameObject EmptyWorld(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            return go;
        }

        /// <summary>A primitive with its collider stripped, parented with local pose, shadows off (small emissive set dressing).</summary>
        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
                                      Material mat, Quaternion? localRot = null, bool shadows = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            return go;
        }

        public static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material[] mats, Vector3 localPos,
                                         Quaternion localRot, Vector3 localScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public static Light PointLight(string name, Transform parent, Vector3 localPos, Color c, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        public static TextMeshPro Text(string name, Transform parent, Vector3 localPos, Vector2 size, string text, Color color, float maxSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tmp.rectTransform.sizeDelta = size;
            tmp.text = text;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 1f;
            tmp.fontSizeMax = maxSize;
            return tmp;
        }

        public static Vector3 RightOf(Vector3 up, Vector3 fwd)
        {
            Vector3 r = Vector3.Cross(up, fwd);
            return r.sqrMagnitude < 1e-6f ? Vector3.right : r.normalized;
        }

        /// <summary>Yaw-only frame (level floor) from a rail tangent.</summary>
        public static Quaternion Level(Vector3 fwd)
        {
            Vector3 f = new Vector3(fwd.x, 0f, fwd.z);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            return Quaternion.LookRotation(f.normalized, Vector3.up);
        }

        public static void Pose(ICoasterRail rail, float d, out Vector3 p, out Vector3 f, out Vector3 u)
        {
            rail.Sample(Mathf.Clamp(d, 0f, rail.Length), out p, out f, out u);
        }

        public static void Span(HorrorCoasterConfig cfg, ICoasterRail rail, ActId a, out float s, out float e)
        {
            s = cfg.ActStart(a);
            e = Mathf.Min(cfg.ActEnd(a), rail.Length);
            if (e <= s + 1f) e = Mathf.Min(rail.Length, s + 60f);
        }

        public static List<(int index, ScareEvent ev)> EventsOf(HorrorCoasterConfig cfg, ScareKind kind)
        {
            var list = new List<(int, ScareEvent)>();
            if (cfg.events == null) return list;
            for (int i = 0; i < cfg.events.Length; i++) if (cfg.events[i].kind == kind) list.Add((i, cfg.events[i]));
            return list;
        }

        // Generated meshes are always written as a fresh asset (delete-then-create), never overwritten in place.
        public static Mesh SaveMeshFresh(Mesh mesh, string name)
        {
            const string dir = "Assets/Art/Generated/HorrorCoaster/Meshes";
            EnsureFolder("Assets/Art");
            EnsureFolder("Assets/Art/Generated");
            EnsureFolder("Assets/Art/Generated/HorrorCoaster");
            EnsureFolder(dir);
            string path = dir + "/" + name + ".asset";
            mesh.name = name;
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
    }

    /// <summary>Tiny mesh accumulator plus the handful of procedural shapes the sets need.</summary>
    internal sealed class MeshAcc
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<List<int>> Sub = new List<List<int>>();

        public int Add(Vector3 p, Vector3 n) { V.Add(p); N.Add(n); return V.Count - 1; }

        public void Tri(int sub, int a, int b, int c)
        {
            while (Sub.Count <= sub) Sub.Add(new List<int>());
            Sub[sub].Add(a); Sub[sub].Add(b); Sub[sub].Add(c);
        }

        public Mesh ToMesh()
        {
            var m = new Mesh();
            if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetNormals(N);
            m.subMeshCount = Mathf.Max(1, Sub.Count);
            for (int i = 0; i < Sub.Count; i++) m.SetTriangles(Sub[i], i);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Convex-or-star-shaped CCW outline in the XY plane, visible from both sides.</summary>
        public void FlatFan(int sub, Vector2 center, IList<Vector2> outline, float z)
        {
            int n = outline.Count;
            int cf = Add(new Vector3(center.x, center.y, z), Vector3.forward);
            int cb = Add(new Vector3(center.x, center.y, z), Vector3.back);
            var f = new int[n];
            var b = new int[n];
            for (int i = 0; i < n; i++)
            {
                f[i] = Add(new Vector3(outline[i].x, outline[i].y, z), Vector3.forward);
                b[i] = Add(new Vector3(outline[i].x, outline[i].y, z), Vector3.back);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Tri(sub, cf, f[i], f[j]);
                Tri(sub, cb, b[j], b[i]);
            }
        }

        public static Mesh Torus(float majorR, float minorR, float beadAmp, int beads, int nMajor, int nMinor)
        {
            var mb = new MeshAcc();
            int cols = nMinor + 1;
            for (int i = 0; i <= nMajor; i++)
            {
                float th = 2f * Mathf.PI * i / nMajor;
                Vector3 nrm = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0f);
                float rm = minorR * (1f + beadAmp * Mathf.Cos(beads * th));
                for (int j = 0; j <= nMinor; j++)
                {
                    float ph = 2f * Mathf.PI * j / nMinor;
                    Vector3 dir = Mathf.Cos(ph) * nrm + Mathf.Sin(ph) * Vector3.forward;
                    mb.Add(nrm * majorR + dir * rm, dir);
                }
            }
            for (int i = 0; i < nMajor; i++)
                for (int j = 0; j < nMinor; j++)
                {
                    int a = i * cols + j, b = (i + 1) * cols + j, c = i * cols + j + 1, d = (i + 1) * cols + j + 1;
                    mb.Tri(0, a, b, c);
                    mb.Tri(0, b, d, c);
                }
            return mb.ToMesh();
        }

        /// <summary>Surface of revolution about Y. prof = (radius, y) listed top to bottom. segSub maps each segment to a submesh.</summary>
        public static Mesh Revolve(Vector2[] prof, int sides, int[] segSub, bool doubleSided, float hemAmp)
        {
            var mb = new MeshAcc();
            int rings = prof.Length, cols = sides + 1;
            var idx = new int[rings, cols];
            var idx2 = new int[rings, cols];
            for (int i = 0; i < rings; i++)
            {
                Vector2 t = prof[Mathf.Min(i + 1, rings - 1)] - prof[Mathf.Max(i - 1, 0)];
                Vector2 nr = new Vector2(-t.y, t.x);
                nr = nr.sqrMagnitude < 1e-8f ? Vector2.up : nr.normalized;
                for (int s = 0; s < cols; s++)
                {
                    float th = 2f * Mathf.PI * s / sides;
                    float c = Mathf.Cos(th), sn = Mathf.Sin(th);
                    float y = prof[i].y;
                    if (hemAmp > 0f && i == rings - 1) y -= hemAmp * (0.5f + 0.5f * Mathf.Cos(5f * th));
                    Vector3 p = new Vector3(prof[i].x * c, y, prof[i].x * sn);
                    Vector3 n = new Vector3(nr.x * c, nr.y, nr.x * sn).normalized;
                    idx[i, s] = mb.Add(p, n);
                    if (doubleSided) idx2[i, s] = mb.Add(p, -n);
                }
            }
            for (int i = 0; i < rings - 1; i++)
            {
                int sub = segSub != null && i < segSub.Length ? segSub[i] : 0;
                for (int s = 0; s < sides; s++)
                {
                    mb.Tri(sub, idx[i, s], idx[i, s + 1], idx[i + 1, s]);
                    mb.Tri(sub, idx[i, s + 1], idx[i + 1, s + 1], idx[i + 1, s]);
                    if (doubleSided)
                    {
                        mb.Tri(sub, idx2[i, s], idx2[i + 1, s], idx2[i, s + 1]);
                        mb.Tri(sub, idx2[i, s + 1], idx2[i + 1, s], idx2[i + 1, s + 1]);
                    }
                }
            }
            return mb.ToMesh();
        }

        public static Mesh CandyCorn()
        {
            var prof = new[]
            {
                new Vector2(0f, 1f), new Vector2(0.30f, 0.33f), new Vector2(0.60f, -0.33f),
                new Vector2(0.90f, -1f), new Vector2(0.62f, -1.08f), new Vector2(0f, -1.12f)
            };
            return Revolve(prof, 6, new[] { 0, 1, 2, 2, 2 }, false, 0f);
        }

        public static Mesh WrappedSweet()
        {
            var prof = new[]
            {
                new Vector2(0.05f, 1.8f), new Vector2(0.65f, 1.7f), new Vector2(0.14f, 1.2f), new Vector2(0.4f, 0.9f),
                new Vector2(0.62f, 0.5f), new Vector2(0.68f, 0f), new Vector2(0.62f, -0.5f), new Vector2(0.4f, -0.9f),
                new Vector2(0.14f, -1.2f), new Vector2(0.65f, -1.7f), new Vector2(0.05f, -1.8f)
            };
            return Revolve(prof, 12, null, false, 0f);
        }

        public static Mesh Ghost()
        {
            var prof = new[]
            {
                new Vector2(0f, 1f), new Vector2(0.3f, 0.95f), new Vector2(0.5f, 0.78f), new Vector2(0.6f, 0.5f),
                new Vector2(0.62f, 0.1f), new Vector2(0.64f, -0.3f), new Vector2(0.68f, -0.7f), new Vector2(0.7f, -1f)
            };
            return Revolve(prof, 20, null, true, 0.3f);
        }

        public static Mesh Bat()
        {
            var r = new[]
            {
                new Vector2(0.10f, 0.52f), new Vector2(0.17f, 0.30f), new Vector2(0.40f, 0.42f), new Vector2(0.72f, 0.66f),
                new Vector2(1.00f, 0.50f), new Vector2(0.92f, 0.10f), new Vector2(0.78f, 0.22f), new Vector2(0.66f, -0.10f),
                new Vector2(0.50f, 0.06f), new Vector2(0.36f, -0.20f), new Vector2(0.18f, -0.24f)
            };
            var outline = new List<Vector2> { new Vector2(0f, 0.28f) };
            for (int i = 0; i < r.Length; i++) outline.Add(new Vector2(-r[i].x, r[i].y));
            outline.Add(new Vector2(0f, -0.34f));
            for (int i = r.Length - 1; i >= 0; i--) outline.Add(r[i]);
            var mb = new MeshAcc();
            mb.FlatFan(0, new Vector2(0f, 0.05f), outline, 0f);
            return mb.ToMesh();
        }

        /// <summary>Jack-o'-lantern face: submesh 0 = pumpkin disc, submesh 1 = carved eyes / nose / mouth (both sides).</summary>
        public static Mesh PumpkinFace()
        {
            var mb = new MeshAcc();
            var disc = new List<Vector2>();
            for (int i = 0; i < 40; i++)
            {
                float a = 2f * Mathf.PI * i / 40f;
                disc.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.9f));
            }
            mb.FlatFan(0, Vector2.zero, disc, 0f);
            foreach (float z in new[] { 0.03f, -0.03f })
            {
                mb.FlatFan(1, new Vector2(-0.375f, 0.27f), new[] { new Vector2(-0.55f, 0.15f), new Vector2(-0.2f, 0.15f), new Vector2(-0.375f, 0.5f) }, z);
                mb.FlatFan(1, new Vector2(0.375f, 0.27f), new[] { new Vector2(0.2f, 0.15f), new Vector2(0.55f, 0.15f), new Vector2(0.375f, 0.5f) }, z);
                mb.FlatFan(1, new Vector2(0f, 0f), new[] { new Vector2(-0.08f, -0.05f), new Vector2(0.08f, -0.05f), new Vector2(0f, 0.12f) }, z);
                const int n = 8;
                for (int k = 0; k < n; k++)
                {
                    float x0 = -0.66f + 1.32f * k / n, x1 = -0.66f + 1.32f * (k + 1) / n;
                    float y0 = -0.42f + 0.12f * x0 * x0, y1 = -0.42f + 0.12f * x1 * x1;
                    float u0 = y0 + 0.10f + ((k % 2 == 0) ? 0.09f : 0f), u1 = y1 + 0.10f + ((k % 2 == 0) ? 0f : 0.09f);
                    float l0 = y0 - 0.10f + ((k % 2 == 0) ? 0f : 0.09f), l1 = y1 - 0.10f + ((k % 2 == 0) ? 0.09f : 0f);
                    mb.FlatFan(1, new Vector2((x0 + x1) * 0.5f, (u0 + l1) * 0.5f),
                        new[] { new Vector2(x0, l0), new Vector2(x1, l1), new Vector2(x1, u1), new Vector2(x0, u0) }, z);
                }
            }
            return mb.ToMesh();
        }
    }
}
