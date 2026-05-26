using UnityEngine;
using UnityEditor;
using UnityEditor.AI;

namespace CrimsonDraft.Editor
{
    /// <summary>
    /// Editor utility to mark all MeshRenderers in the active scene as
    /// Navigation Static and then bake the legacy built-in NavMesh.
    /// </summary>
    public static class NavigationSetupUtility
    {
        [MenuItem("CrimsonDraft/Navigation/Mark All Meshes Navigation Static")]
        public static void MarkAllMeshesNavigationStatic()
        {
            int count = 0;
            // StaticEditorFlags.NavigationStatic = 8
            const StaticEditorFlags NavStatic = StaticEditorFlags.NavigationStatic;

            var renderers = Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var r in renderers)
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if ((flags & NavStatic) == 0)
                {
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | NavStatic);
                    EditorUtility.SetDirty(r.gameObject);
                    count++;
                }
            }

            Debug.Log($"[NavigationSetupUtility] Marked {count} MeshRenderer(s) as Navigation Static.");
        }

        [MenuItem("CrimsonDraft/Navigation/Bake NavMesh (Legacy)")]
        public static void BakeNavMesh()
        {
            // Mark first, then bake
            MarkAllMeshesNavigationStatic();
            NavMeshBuilder.BuildNavMesh();
            Debug.Log("[NavigationSetupUtility] NavMesh bake complete.");
        }
    }
}
