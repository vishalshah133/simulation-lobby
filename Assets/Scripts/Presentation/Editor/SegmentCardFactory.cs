using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Presentation.EditorTools
{
    /// <summary>Scene-builder entry points for the compilation segment card + wide-frame HUD.</summary>
    public static class SegmentCardFactory
    {
        public const string ConfigFolder = "Assets/Settings/SegmentCards";

        /// <summary>Adds the canvas + presenter to the active scene with <paramref name="cfg"/> assigned.</summary>
        public static SegmentCardPresenter CreateInScene(SegmentCardConfig cfg, AspectMode mode)
        {
            SegmentCardPresenter presenter = SegmentCardPresenter.Build(cfg, mode);
            Undo.RegisterCreatedObjectUndo(presenter.gameObject, "Create Segment Card");
            return presenter;
        }

        /// <summary>Creates or updates the config asset at <paramref name="path"/> (e.g. Assets/Settings/SegmentCards/comp001_01.asset).</summary>
        public static SegmentCardConfig CreateConfigAsset(string path, int index, int total, string title, string hook, string explainer)
        {
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(dir);

            var cfg = AssetDatabase.LoadAssetAtPath<SegmentCardConfig>(path);
            bool isNew = cfg == null;
            if (isNew)
            {
                cfg = ScriptableObject.CreateInstance<SegmentCardConfig>();
            }

            cfg.index = index;
            cfg.total = total;
            cfg.title = title;
            cfg.hook = hook;
            cfg.explainer = explainer;

            if (isNew)
            {
                AssetDatabase.CreateAsset(cfg, path);
            }
            else
            {
                EditorUtility.SetDirty(cfg);
            }

            AssetDatabase.SaveAssets();
            return cfg;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
