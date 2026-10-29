using System.Collections.Generic;
using SimulationLobby.Shared.EditorTools;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Small geometry helpers shared by every mach station builder, so builders don't each grow their own.
    /// Contract file for mach-001: add a helper by reporting it, don't edit inside a work package.
    /// </summary>
    public static class MachineGeometry
    {
        public const float GlassMinThickness = 0.12f;
        public const string MeshFolder = "Assets/Art/Generated/MarbleMachine/Meshes";

        static PhysicsMaterial _wallMaterial;

        /// <summary>Low-bounce, moderate-friction material for every static and kinematic surface.</summary>
        public static PhysicsMaterial WallPhysics
        {
            get
            {
                if (_wallMaterial == null)
                {
                    _wallMaterial = new PhysicsMaterial("MachineWall")
                    {
                        bounciness = 0.15f,
                        dynamicFriction = 0.25f,
                        staticFriction = 0.3f,
                        bounceCombine = PhysicsMaterialCombine.Average,
                        frictionCombine = PhysicsMaterialCombine.Average
                    };
                }

                return _wallMaterial;
            }
        }

        static PhysicsMaterial _gripMaterial;

        /// <summary>High-friction, non-bouncy surface: where balls must not slide back (the screw lift's glass tube and ribs).</summary>
        public static PhysicsMaterial GripPhysics
        {
            get
            {
                if (_gripMaterial == null)
                {
                    _gripMaterial = new PhysicsMaterial("MachineGrip")
                    {
                        bounciness = 0f,
                        dynamicFriction = 0.9f,
                        staticFriction = 1.0f,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                        frictionCombine = PhysicsMaterialCombine.Maximum
                    };
                }

                return _gripMaterial;
            }
        }

        static PhysicsMaterial _glideMaterial;

        /// <summary>Zero-bounce, low-friction surface for moving floors: balls are carried, not bounced.</summary>
        public static PhysicsMaterial GlidePhysics
        {
            get
            {
                if (_glideMaterial == null)
                {
                    _glideMaterial = new PhysicsMaterial("MachineGlide")
                    {
                        bounciness = 0f,
                        dynamicFriction = 0.12f,
                        staticFriction = 0.15f,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                        frictionCombine = PhysicsMaterialCombine.Average
                    };
                }

                return _glideMaterial;
            }
        }

        static PhysicsMaterial _slickMaterial;

        /// <summary>Near-frictionless, non-bouncy surface for ledges where balls must keep their speed.</summary>
        public static PhysicsMaterial SlickPhysics
        {
            get
            {
                if (_slickMaterial == null)
                {
                    _slickMaterial = new PhysicsMaterial("MachineSlick")
                    {
                        bounciness = 0f,
                        dynamicFriction = 0.05f,
                        staticFriction = 0.05f,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                        frictionCombine = PhysicsMaterialCombine.Minimum
                    };
                }

                return _slickMaterial;
            }
        }

        public static GameObject Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        public static Transform Anchor(Transform parent, string name, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        /// <summary>A box with a BoxCollider (optional). Size is the full extent in local space.</summary>
        public static GameObject Box(Transform parent, string name, Vector3 localPosition, Quaternion localRotation,
            Vector3 size, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            // Glass walls are at least GlassMinThickness thick (their thinnest side): a ball at 12 m/s moves 0.12 m a step and
            // slips through anything thinner. The thickening is centred, so a wall that must keep a face in place moves its centre.
            if (collider && material != null && material.name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
                if (size[thin] < GlassMinThickness) size[thin] = GlassMinThickness;
            }

            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var box = go.GetComponent<BoxCollider>();
            if (collider) box.sharedMaterial = WallPhysics;
            else Object.DestroyImmediate(box);
            return go;
        }

        /// <summary>A cylinder along local Y (full height <paramref name="height"/>) with an optional CapsuleCollider.</summary>
        public static GameObject Cylinder(Transform parent, string name, Vector3 localPosition, Quaternion localRotation,
            float radius, float height, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (collider)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.direction = 1;
                cap.radius = 0.5f;
                cap.height = 2f;
                cap.sharedMaterial = WallPhysics;
            }

            return go;
        }

        /// <summary>Renderer + MeshCollider (non-convex unless asked) from a generated mesh, saved fresh as an asset.</summary>
        public static GameObject MeshObject(Transform parent, string name, Mesh mesh, string assetName, Material material,
            bool collider = true, bool convex = false, Material wallMaterial = null)
        {
            Mesh saved = TrackMeshAssets.SaveMeshFresh(mesh, assetName, MeshFolder);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = saved;
            var mr = go.AddComponent<MeshRenderer>();
            if (wallMaterial != null) mr.sharedMaterials = new[] { material, wallMaterial };
            else mr.sharedMaterial = material;
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = saved;
                mc.convex = convex;
                mc.sharedMaterial = WallPhysics;
            }

            return go;
        }

        /// <summary>
        /// Adds a kinematic Rigidbody for a part the station moves with MovePosition/MoveRotation.
        /// Interpolated so it renders smoothly between fixed steps.
        /// </summary>
        public static Rigidbody MakeKinematic(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            return rb;
        }

        /// <summary>
        /// Surface of revolution about local Y. <paramref name="profile"/> is (radius, height) from bottom to top.
        /// Faces point outward; pass <paramref name="doubleSided"/> for glass or anything balls ride inside.
        /// </summary>
        public static Mesh Lathe(IList<Vector2> profile, int segments, bool doubleSided)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int rings = profile.Count;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * profile[r].x, profile[r].y, Mathf.Sin(a) * profile[r].x));
                    uvs.Add(new Vector2(s / (float)segments, r / (float)(rings - 1)));
                }
            }

            int stride = segments + 1;
            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * stride + s, b = a + 1, c = a + stride, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            if (doubleSided) AppendBackFaces(verts, uvs, tris);
            return Finish(verts, uvs, tris);
        }

        /// <summary>Open cylinder wall (no caps) about local Y from y=0 to y=height.</summary>
        public static Mesh OpenCylinder(float radius, float height, int segments, bool doubleSided)
        {
            return Lathe(new[] { new Vector2(radius, 0f), new Vector2(radius, height) }, segments, doubleSided);
        }

        /// <summary>
        /// U-channel swept along a polyline of centreline points (local space), floor width <paramref name="width"/>,
        /// wall height <paramref name="wallHeight"/>. Up is world-ish up projected per segment. Double-sided.
        /// Good for connectors, the helix, inruns.
        /// </summary>
        public static Mesh Channel(IList<Vector3> centreline, float width, float wallHeight, IList<float> bankDegrees = null, bool splitWalls = false)
        {
            var wallTris = new List<int>();
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float run = 0f;
            for (int i = 0; i < centreline.Count; i++)
            {
                Vector3 fwd = (i < centreline.Count - 1 ? centreline[i + 1] - centreline[i] : centreline[i] - centreline[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
                float bank = bankDegrees != null ? bankDegrees[i] : 0f;
                side = Quaternion.AngleAxis(bank, fwd) * side;
                Vector3 up = Vector3.Cross(fwd, side).normalized;
                if (i > 0) run += Vector3.Distance(centreline[i], centreline[i - 1]);
                Vector3 c = centreline[i];
                float h = width * 0.5f;
                // Cross-section: left lip, left floor, right floor, right lip.
                verts.Add(c - side * h + up * wallHeight);
                verts.Add(c - side * h);
                verts.Add(c + side * h);
                verts.Add(c + side * h + up * wallHeight);
                uvs.Add(new Vector2(0f, run)); uvs.Add(new Vector2(0.25f, run));
                uvs.Add(new Vector2(0.75f, run)); uvs.Add(new Vector2(1f, run));
            }

            for (int i = 0; i < centreline.Count - 1; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = i * 4 + k, b = a + 1, c = a + 4, d = c + 1;
                    List<int> target = splitWalls && k != 1 ? wallTris : tris;
                    target.Add(a); target.Add(b); target.Add(c);
                    target.Add(b); target.Add(d); target.Add(c);
                }
            }

            if (splitWalls)
            {
                // Floor is submesh 0, the two side walls submesh 1 (so they can take a glass material).
                AppendBackFaces(verts, uvs, tris, wallTris);
                var mesh = Finish(verts, uvs, tris);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(tris, 0);
                mesh.SetTriangles(wallTris, 1);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            AppendBackFaces(verts, uvs, tris);
            return Finish(verts, uvs, tris);
        }

        static void AppendBackFaces(List<Vector3> verts, List<Vector2> uvs, List<int> tris, List<int> extraTris = null)
        {
            int n = verts.Count;
            if (extraTris != null)
            {
                int e = extraTris.Count;
                for (int i = 0; i < e; i += 3)
                {
                    extraTris.Add(extraTris[i] + n);
                    extraTris.Add(extraTris[i + 2] + n);
                    extraTris.Add(extraTris[i + 1] + n);
                }
            }

            int t = tris.Count;
            for (int i = 0; i < n; i++)
            {
                verts.Add(verts[i]);
                uvs.Add(uvs[i]);
            }

            for (int i = 0; i < t; i += 3)
            {
                tris.Add(tris[i] + n);
                tris.Add(tris[i + 2] + n);
                tris.Add(tris[i + 1] + n);
            }
        }

        /// <summary>
        /// Gives a stand-in normal (up) to any vertex RecalculateNormals left at zero: vertices whose triangles are all degenerate or
        /// cancel out. A zero normal lights as an enormous white blob once bloom gets hold of it.
        /// </summary>
        public static void FixDegenerateNormals(Mesh mesh)
        {
            Vector3[] normals = mesh.normals;
            bool changed = false;
            for (int i = 0; i < normals.Length; i++)
            {
                float m = normals[i].sqrMagnitude;
                if (float.IsNaN(m) || m < 0.25f)
                {
                    normals[i] = Vector3.up;
                    changed = true;
                }
            }

            if (changed) mesh.normals = normals;
        }

        static Mesh Finish(List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            var mesh = new Mesh();
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            FixDegenerateNormals(mesh);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
