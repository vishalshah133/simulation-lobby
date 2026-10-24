using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Simulations.Race.EditorTools
{
    /// <summary>Ensure-style helpers for the race look's shared assets (physics and display materials). Created once, never overwritten.</summary>
    internal static class RaceAssets
    {
        public const string ConfigFolder = "Assets/Settings/Configs/Race";
        public const string MaterialFolder = "Assets/Settings/Studio";

        public static PhysicsMaterial EnsurePhysicsMaterial(string name, float dynamicFriction, float staticFriction,
            PhysicsMaterialCombine combine, float bounciness)
        {
            string path = $"{ConfigFolder}/{name}.physicsMaterial";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new PhysicsMaterial(name)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                frictionCombine = combine,
                bounceCombine = combine
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static Material EnsureMarbleMaterial()
        {
            return EnsureMaterial("RaceMarble", "Universal Render Pipeline/Complex Lit", m =>
            {
                // The roster colour arrives per marble through a property block. A lacquered glass bead:
                // saturated base under a mirror clear coat.
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Metallic", 0.1f);
                m.SetFloat("_Smoothness", 0.88f);
                if (m.HasProperty("_ClearCoat"))
                {
                    m.SetFloat("_ClearCoat", 1f);
                    m.SetFloat("_ClearCoatMask", 1f);
                    m.SetFloat("_ClearCoatSmoothness", 0.98f);
                    m.EnableKeyword("_CLEARCOAT");
                }
            });
        }

        public static Material EnsureMaterial(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[race] Shader '{shaderName}' not found for {name}.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) { name = name };
            setup(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
