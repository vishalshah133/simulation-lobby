using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Shared.EditorTools
{
    /// <summary>Saving generated course meshes as assets.</summary>
    public static class TrackMeshAssets
    {
        /// <summary>Where generated meshes go unless the caller names a folder.</summary>
        public const string DefaultFolder = "Assets/Settings/Studio/Meshes";

        /// <summary>
        /// Saves a generated mesh as a brand-new asset, deleting any earlier file at the same path first.
        /// An in-place overwrite (what <c>StudioSetBuilder.SaveMesh</c> does) is fine for a mesh that never
        /// changes shape, but a course changes shape whenever its layout does, and the editor kept drawing the
        /// previous build's mesh while physics and obstacles used the new data.
        /// </summary>
        public static Mesh SaveMeshFresh(Mesh mesh, string name, string folder = DefaultFolder)
        {
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
    }
}
