#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    public static class NavigationValidationToolbar
    {
        private const string k_ElementPath = "CrimsonDraft/NavigationValidation";

        [MainToolbarElement(k_ElementPath, defaultDockPosition = MainToolbarDockPosition.Right)]
        private static IEnumerable<MainToolbarElement> CreateButton()
        {
            var icon = EditorGUIUtility.IconContent("d_console.warnicon.sml").image as Texture2D;
            yield return new MainToolbarButton(
                new MainToolbarContent(icon, "Validate Navigation References"),
                () => EditorApplication.delayCall += Validate);
        }

        [MenuItem("Tools/Validate Navigation References")]
        public static void Validate()
        {
            var issues = 0;

            foreach (var room in Object.FindObjectsOfType<RoomController>(includeInactive: true))
            {
                if (string.IsNullOrEmpty(room.RoomId))
                {
                    Debug.LogWarning($"[NavValidation] RoomController '{room.name}' has no roomId set.", room);
                    issues++;
                }
            }

            foreach (var spawn in Object.FindObjectsOfType<SpawnPoint>(includeInactive: true))
            {
                if (spawn.FromRoom == null)
                {
                    Debug.LogWarning($"[NavValidation] SpawnPoint '{spawn.name}' has no fromRoom set.", spawn);
                    issues++;
                    continue;
                }

                var ownerRoom = spawn.GetComponentInParent<RoomController>(includeInactive: true);
                if (ownerRoom != null && spawn.FromRoom == ownerRoom)
                {
                    Debug.LogWarning($"[NavValidation] SpawnPoint '{spawn.name}' points to its own room '{ownerRoom.RoomId}'.", spawn);
                    issues++;
                }
            }

            foreach (var door in Object.FindObjectsOfType<RoomDoorInteractable>(includeInactive: true))
            {
                if (door.Destination == null)
                {
                    Debug.LogWarning($"[NavValidation] RoomDoorInteractable '{door.name}' has no destination set.", door);
                    issues++;
                }
            }

            if (issues == 0)
                Debug.Log("[NavValidation] All navigation references are set.");
            else
                Debug.LogWarning($"[NavValidation] Found {issues} issue(s). See warnings above.");
        }
    }
}
