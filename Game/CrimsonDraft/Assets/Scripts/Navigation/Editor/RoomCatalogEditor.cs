#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>"Rebuild From Scenes": opens every production deck scene and lists its rooms.</summary>
    [CustomEditor(typeof(RoomCatalog))]
    public sealed class RoomCatalogEditor : UnityEditor.Editor
    {
        public static readonly string[] DeckScenes =
        {
            "Assets/Scenes/Production/Deck_B_Development.unity",
            "Assets/Scenes/Production/Deck_C_Development.unity",
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild From Scenes"))
                Rebuild((RoomCatalog)this.target);
        }

        /// <summary>The asset the project uses, if any.</summary>
        public static RoomCatalog? Find()
        {
            var guid = AssetDatabase.FindAssets("t:RoomCatalog").FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<RoomCatalog>(AssetDatabase.GUIDToAssetPath(guid));
        }

        public static bool Rebuild(RoomCatalog catalog)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                Debug.LogError("[RoomCatalog] Save or discard the open scenes first; nothing was rebuilt.");
                return false;
            }

            // Opening a scene in Single mode unloads unreferenced assets, the catalog included:
            // reload it by path afterwards instead of writing through a dead reference.
            var assetPath = AssetDatabase.GetAssetPath(catalog);
            var open      = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path).ToArray();
            var entries   = new List<RoomCatalog.Entry>();
            try
            {
                foreach (var path in DeckScenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var room in root.GetComponentsInChildren<RoomController>(true))
                        entries.Add(new RoomCatalog.Entry(room.RoomId, scene.name));
                }
            }
            finally
            {
                for (int i = 0; i < open.Length; i++)
                    if (!string.IsNullOrEmpty(open[i]))
                        EditorSceneManager.OpenScene(open[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
            }

            if (catalog == null && !string.IsNullOrEmpty(assetPath))
                catalog = AssetDatabase.LoadAssetAtPath<RoomCatalog>(assetPath);
            Undo.RecordObject(catalog, "Rebuild Room Catalog");
            catalog.EditorSetRooms(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log($"[RoomCatalog] {catalog.Rooms.Count} room(s) from {DeckScenes.Length} scene(s).", catalog);
            return true;
        }
    }
}
