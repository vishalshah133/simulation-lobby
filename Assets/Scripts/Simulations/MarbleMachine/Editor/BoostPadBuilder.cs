using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds a speed-boost pad (see <see cref="BoostPad"/>): a row of glowing chevrons painted on the floor and a trigger box over
    /// them. No moving parts.
    /// </summary>
    public static class BoostPadBuilder
    {
        const int Chevrons = 3;
        const float Spacing = 0.5f;

        /// <param name="floorCentre">Station-local point on the floor's top surface at the middle of the pad.</param>
        /// <param name="frame">Station-local orientation of the floor there: Z along the flow, Y up from the floor.</param>
        public static BoostPad Build(Transform station, string name, Vector3 floorCentre, Quaternion frame, float length, MachineKit kit, MachineConfig config)
        {
            var go = new GameObject(name);
            go.transform.SetParent(station, false);
            go.transform.localPosition = floorCentre;
            go.transform.localRotation = frame;

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.2f, 0f);
            trigger.size = new Vector3(config.chuteWidth - 0.1f, 0.4f, length);
            var pad = go.AddComponent<BoostPad>();
            pad.targetSpeed = config.boosterSurfaceSpeed;

            float scale = (config.chuteWidth - 0.4f) / 1.2f;
            GameObject first = null;
            for (int i = 0; i < Chevrons; i++)
            {
                float z = (i - (Chevrons - 1) * 0.5f) * Spacing;
                GameObject chevron;
                if (first == null)
                {
                    // One mesh per pad, saved under this pad's own name: sharing one asset name between pads makes a later build delete
                    // the mesh an earlier pad is still using.
                    chevron = MachineGeometry.MeshObject(go.transform, "Arrow", ChevronMesh(scale), "BoostArrow_" + HierarchyName(station) + "_" + name.Replace(' ', '_'),
                        kit.boostArrow, false);
                    first = chevron;
                }
                else
                {
                    chevron = new GameObject("Arrow");
                    chevron.transform.SetParent(go.transform, false);
                    chevron.AddComponent<MeshFilter>().sharedMesh = first.GetComponent<MeshFilter>().sharedMesh;
                    chevron.AddComponent<MeshRenderer>().sharedMaterial = kit.boostArrow;
                }

                chevron.transform.localPosition = new Vector3(0f, 0.012f, z);
            }

            return pad;
        }

        static string HierarchyName(Transform t)
        {
            var parts = new List<string>();
            for (Transform c = t; c != null; c = c.parent)
            {
                if (c.name == "Stations" || c.name == "Machine" || c.name == "__probe") continue;
                parts.Insert(0, c.name);
            }

            return string.Join("_", parts).Replace(' ', '_');
        }

        /// <summary>A flat chevron pointing along +Z, about 1.2 m wide times <paramref name="scale"/>, lying in the XZ plane.</summary>
        static Mesh ChevronMesh(float scale)
        {
            Vector3 P(float x, float z) => new Vector3(x * scale, 0f, z * 0.8f);
            Vector3 a = P(0f, 0.35f), b = P(0.6f, -0.25f), c = P(0.6f, -0.55f), d = P(0f, -0.05f), e = P(-0.6f, -0.55f), f = P(-0.6f, -0.25f);
            var verts = new List<Vector3> { a, b, c, d, e, f };
            // Wound clockwise seen from above, so the arrows face up.
            var tris = new List<int> { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 };
            var normals = new List<Vector3>();
            for (int i = 0; i < verts.Count; i++) normals.Add(Vector3.up);
            var uvs = new List<Vector2>();
            foreach (Vector3 v in verts) uvs.Add(new Vector2(v.x, v.z));
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
