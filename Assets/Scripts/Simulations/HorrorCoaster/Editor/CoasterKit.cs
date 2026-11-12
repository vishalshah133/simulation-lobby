using UnityEngine;
using UnityEngine.Rendering;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>Small helpers for building primitive-based sets: no colliders (nothing here is physical), world-space placement.</summary>
    public static class CoasterKit
    {
        public static GameObject Empty(Transform parent, string name, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            return go;
        }

        /// <summary>A collider-free primitive placed in world space under <paramref name="parent"/> (parents must have unit scale).</summary>
        public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 worldPos, Quaternion worldRot,
                                      Vector3 scale, Material material, bool castShadows = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = worldRot;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 worldPos, Vector3 scale, Material material)
        {
            return Prim(type, parent, name, worldPos, Quaternion.identity, scale, material, false);
        }

        /// <summary>A primitive placed in the parent's local space (use for pieces of a rig that moves).</summary>
        public static GameObject Local(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 localScale,
                                       Material material)
        {
            return Local(type, parent, name, localPos, localScale, material, Quaternion.identity);
        }

        public static GameObject Local(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 localScale,
                                       Material material, Quaternion localRot)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public static Mesh BuiltinMesh(PrimitiveType type)
        {
            GameObject tmp = GameObject.CreatePrimitive(type);
            Mesh mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return mesh;
        }

        /// <summary>Rail frame at distance d: position, forward, up (banked) and right.</summary>
        public static void Frame(ICoasterRail rail, float d, out Vector3 pos, out Vector3 fwd, out Vector3 up, out Vector3 right)
        {
            rail.Sample(d, out pos, out fwd, out up);
            right = Vector3.Cross(up, fwd).normalized;
        }

        /// <summary>Rotation looking along <paramref name="forward"/> with <paramref name="up"/>, safe for degenerate input.</summary>
        public static Quaternion Look(Vector3 forward, Vector3 up)
        {
            return forward.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(forward, up);
        }

        /// <summary>Lowest point of the rail, minus clearance: where the ground sits.</summary>
        public static float GroundY(ICoasterRail rail, float clearance = 2.0f)
        {
            float min = float.MaxValue;
            for (float d = 0f; d <= rail.Length; d += 2f)
            {
                rail.Sample(d, out Vector3 p, out _, out _);
                min = Mathf.Min(min, p.y);
            }

            return min - clearance;
        }

        /// <summary>Deterministic hash noise 0..1 for scattering props, so rebuilds are identical.</summary>
        public static float Hash(int a, int b = 0)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
