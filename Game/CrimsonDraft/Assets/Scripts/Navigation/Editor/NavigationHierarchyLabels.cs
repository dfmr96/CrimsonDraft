#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    [InitializeOnLoad]
    internal static class NavigationHierarchyLabels
    {
        private static GUIStyle? style;

        static NavigationHierarchyLabels()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        private static void OnHierarchyGUI(int instanceID, Rect rect)
        {
            var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (go == null) return;

            string? label = null;
            Color   color = Color.gray;

            var room = go.GetComponent<RoomController>();
            if (room != null)
            {
                if (!string.IsNullOrEmpty(room.RoomId))
                {
                    label = $"[{room.RoomId}]";
                    color = new Color(0.5f, 1f, 0.5f);
                }
                else
                {
                    label = "[no id]";
                    color = Color.red;
                }
            }
            else
            {
                var door = go.GetComponent<RoomDoorInteractable>();
                if (door != null)
                {
                    var destId = door.Destination?.RoomId;
                    if (!string.IsNullOrEmpty(destId))
                    {
                        label = $"→ {destId}";
                        color = new Color(1f, 0.85f, 0.4f);
                    }
                    else
                    {
                        label = "→ no id";
                        color = Color.red;
                    }
                }
                else
                {
                    // The arrival point child of a door: where the player appears coming in through it.
                    var arrival = go.transform.parent != null ? go.transform.parent.GetComponent<DoorArrival>() : null;
                    if (arrival != null && arrival.ArrivalPoint == go.transform)
                    {
                        label = arrival.ArrivalCamera != null ? $"⇠ arrival · {arrival.ArrivalCamera.name}" : "⇠ arrival";
                        color = new Color(0.5f, 0.8f, 1f);
                    }
                }
            }

            if (label == null) return;

            style ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight, fontSize = 10 };
            style.normal.textColor = color;
            GUI.Label(rect, label, style);
        }
    }
}
