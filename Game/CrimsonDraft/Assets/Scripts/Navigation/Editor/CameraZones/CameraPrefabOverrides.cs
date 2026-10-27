#nullable enable

using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Writes the scene-side changes made to a room's cameras and camera zones back into the room's
    /// prefab asset. Only the room's cameras and activation triggers are applied; every other override of
    /// the instance is left alone.</summary>
    public static class CameraPrefabOverrides
    {
        /// <summary>The prefab instance the room belongs to, or null when the room is not a prefab instance
        /// in a scene (loose object, or open in Prefab Mode where edits already target the asset).</summary>
        public static GameObject? InstanceRoot(RoomController room)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return null;
            return PrefabUtility.GetNearestPrefabInstanceRoot(room.gameObject);
        }

        /// <returns>The number of overrides written to the prefab.</returns>
        public static int Apply(RoomController room, IEnumerable<CinemachineCamera> cameras)
        {
            var root = InstanceRoot(room);
            if (root == null) return 0;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            if (string.IsNullOrEmpty(path)) return 0;

            var targets = new HashSet<GameObject>(cameras.Where(c => c != null).Select(c => c.gameObject));
            foreach (var trigger in room.GetComponentsInChildren<FixedCameraZoneTrigger>(true))
                targets.Add(trigger.gameObject);
            // Zones and cameras are often nested prefab instances; their overrides are still stored in the
            // room prefab, so they are applied to the room's asset like any other.
            targets.RemoveWhere(go => go == null || !go.transform.IsChildOf(root.transform));

            int applied = 0;
            // Added objects first: their components and properties travel with them.
            foreach (var added in PrefabUtility.GetAddedGameObjects(root))
            {
                if (!targets.Contains(added.instanceGameObject)) continue;
                added.Apply(path, InteractionMode.UserAction);
                applied++;
            }
            foreach (var added in PrefabUtility.GetAddedComponents(root))
            {
                if (added.instanceComponent == null || !targets.Contains(added.instanceComponent.gameObject)) continue;
                added.Apply(path, InteractionMode.UserAction);
                applied++;
            }
            foreach (var over in PrefabUtility.GetObjectOverrides(root, false))
            {
                var go = over.instanceObject switch
                {
                    GameObject g => g,
                    Component c => c.gameObject,
                    _ => null,
                };
                if (go == null || !targets.Contains(go)) continue;
                over.Apply(path, InteractionMode.UserAction);
                applied++;
            }

            if (applied > 0) AssetDatabase.SaveAssets();
            return applied;
        }
    }
}
