#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Turns the old BoxCollider-based camera zones into polygon zones. The box becomes a
    /// rectangle with the same footprint and height range, and the divider runs along the box's
    /// local X axis through its centre, which is the plane the old two-sided switch used. Only
    /// triggers that still have a BoxCollider are touched, so running it twice is harmless.
    /// Run "Open Scenes" first: scene instances of a room prefab carry their own box overrides,
    /// and converting the prefab asset first would discard them.</summary>
    public static class CameraZoneBoxMigration
    {
        [MenuItem("CrimsonDraft/Camera Zones/Convert Boxes To Polygons (Open Scenes)")]
        private static void ConvertOpenScenes()
        {
            int converted = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                bool changed = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var trigger in root.GetComponentsInChildren<FixedCameraZoneTrigger>(true))
                    {
                        if (!Convert(trigger, useUndo: true)) continue;
                        converted++;
                        changed = true;
                    }
                }
                if (changed) EditorSceneManager.MarkSceneDirty(scene);
            }
            Debug.Log($"[CameraZoneBoxMigration] Converted {converted} trigger(s) in the open scenes.");
        }

        [MenuItem("CrimsonDraft/Camera Zones/Convert Boxes To Polygons (Selected Prefabs Or Folders)")]
        private static void ConvertSelectedPrefabs()
        {
            var folders = Selection.objects.Select(AssetDatabase.GetAssetPath)
                .Where(path => AssetDatabase.IsValidFolder(path)).ToArray();
            var paths = new HashSet<string>(Selection.objects.Select(AssetDatabase.GetAssetPath)
                .Where(path => path.EndsWith(".prefab")));
            if (folders.Length > 0)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            int converted = 0;
            foreach (var path in paths)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int count = 0;
                    foreach (var trigger in contents.GetComponentsInChildren<FixedCameraZoneTrigger>(true))
                        if (Convert(trigger, useUndo: false)) count++;

                    if (count == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    converted += count;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CameraZoneBoxMigration] Converted {converted} trigger(s) in {paths.Count} prefab(s) checked.");
        }

        internal static bool Convert(FixedCameraZoneTrigger trigger, bool useUndo)
        {
            var box = trigger.GetComponent<BoxCollider>();
            if (box == null) return false;

            var shape = FixedCameraZoneTrigger.ShapeFromBox(box);

            var serialized = new SerializedObject(trigger);
            var polygon = serialized.FindProperty("polygon");
            polygon.arraySize = shape.Polygon.Length;
            for (int i = 0; i < shape.Polygon.Length; i++)
                polygon.GetArrayElementAtIndex(i).vector2Value = shape.Polygon[i];
            serialized.FindProperty("minHeight").floatValue = shape.MinHeight;
            serialized.FindProperty("maxHeight").floatValue = shape.MaxHeight;
            serialized.FindProperty("dividerStart").vector2Value = shape.DividerStart;
            serialized.FindProperty("dividerEnd").vector2Value = shape.DividerEnd;

            if (useUndo)
            {
                serialized.ApplyModifiedProperties();
                Undo.DestroyObjectImmediate(box);
            }
            else
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Object.DestroyImmediate(box, true);
            }
            return true;
        }
    }
}
