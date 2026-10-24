#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;
using Unity.Cinemachine;
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

            var roomDoors  = Object.FindObjectsOfType<RoomDoorInteractable>(includeInactive: true);
            var sceneDoors = Object.FindObjectsOfType<SceneDoorInteractable>(includeInactive: true);

            // Every door is where somebody arrives: it needs its arrival point. Until the migration has
            // run none have one, so report a single line instead of one per door.
            var withoutArrival = roomDoors.Cast<Component>().Concat(sceneDoors)
                .Where(d => d.GetComponent<DoorArrival>() == null || d.GetComponent<DoorArrival>().ArrivalPoint == null)
                .ToArray();
            if (withoutArrival.Length > 0)
            {
                Debug.LogWarning($"[NavValidation] {withoutArrival.Length} door(s) have no DoorArrival (e.g. '{withoutArrival[0].name}'). " +
                                 "Run CrimsonDraft > Doors > Migrate Spawn Points To Door Arrivals, or Migrate Doors in the Camera Zone Viewer.",
                                 withoutArrival[0]);
                issues++;
            }

            // Crossing S -> D places the player at D's door back to S; without one the runtime logs an error.
            foreach (var door in roomDoors)
            {
                var origin = door.GetComponentInParent<RoomController>(includeInactive: true);
                if (origin == null || door.Destination == null) continue;
                if (DoorArrivalLookup.FindArrivalDoor(door.Destination, origin, door.transform.position) != null) continue;

                Debug.LogWarning($"[NavValidation] No door back to '{origin.name}' in '{door.Destination.name}' (crossing '{door.name}').", door);
                issues++;
            }

            // One camera, one activation polygon: per room, cameras with none or with several / two-sided zones.
            int twoSidedLeft = 0;
            foreach (var room in Object.FindObjectsOfType<RoomController>(includeInactive: true))
            {
                var triggers = room.GetComponentsInChildren<FixedCameraZoneTrigger>(true);
                twoSidedLeft += triggers.Count(t => t.UseTwoSidedSwitch);
                var statuses = ZoneCameras.Of(room)
                    .Select(c => (camera: c, state: CameraActivation.For(c, triggers).State)).ToArray();
                var none = statuses.Where(s => s.state == CameraActivationState.None).ToArray();
                var legacy = statuses.Where(s => s.state == CameraActivationState.Legacy).ToArray();
                if (none.Length == 0 && legacy.Length == 0) continue;

                var first = none.Length > 0 ? none[0].camera : legacy[0].camera;
                Debug.LogWarning($"[NavValidation] Room '{room.name}': {none.Length} camera(s) without activation trigger, " +
                                 $"{legacy.Length} with several or two-sided (e.g. '{first.name}'). Fix them in the Camera Zone Viewer.", first);
                issues++;
            }
            if (twoSidedLeft > 0)
                Debug.LogWarning($"[NavValidation] {twoSidedLeft} two-sided camera zone(s) left (legacy).");

            foreach (var group in sceneDoors.Where(d => !string.IsNullOrEmpty(d.EntryPointId)).GroupBy(d => d.EntryPointId))
            {
                if (group.Count() < 2) continue;
                Debug.LogWarning($"[NavValidation] Entry point '{group.Key}' is used by several scene doors: {string.Join(", ", group.Select(d => d.name))}.", group.First());
                issues++;
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
