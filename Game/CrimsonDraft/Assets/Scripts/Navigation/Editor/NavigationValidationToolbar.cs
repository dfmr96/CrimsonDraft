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

            // Every door is where somebody arrives: it needs its arrival point.
            foreach (var door in roomDoors.Cast<Component>().Concat(sceneDoors))
            {
                var arrival = door.GetComponent<DoorArrival>();
                if (arrival == null || arrival.ArrivalPoint == null)
                {
                    Debug.LogWarning($"[NavValidation] Door '{door.name}' has no DoorArrival arrival point.", door);
                    issues++;
                }
            }

            // Crossing S -> D places the player at D's door back to S; without one the runtime logs an error.
            var rooms = Object.FindObjectsOfType<RoomController>(includeInactive: true);
            foreach (var door in roomDoors)
            {
                var origin      = door.GetComponentInParent<RoomController>(includeInactive: true);
                var destination = RoomLookup.Find(rooms, door.DestinationRoomId);
                if (origin == null || destination == null) continue;
                if (DoorArrivalLookup.FindArrivalDoor(destination, origin, door.transform.position) != null) continue;

                Debug.LogWarning($"[NavValidation] No door back to '{origin.name}' in '{destination.name}' (crossing '{door.name}').", door);
                issues++;
            }

            // One camera, one activation polygon: per room, cameras with none or with several zones.
            foreach (var room in Object.FindObjectsOfType<RoomController>(includeInactive: true))
            {
                var triggers = room.GetComponentsInChildren<FixedCameraZoneTrigger>(true);
                var statuses = ZoneCameras.Of(room)
                    .Select(c => (camera: c, state: CameraActivation.For(c, triggers).State)).ToArray();
                var none = statuses.Where(s => s.state == CameraActivationState.None).ToArray();
                var legacy = statuses.Where(s => s.state == CameraActivationState.Legacy).ToArray();
                if (none.Length == 0 && legacy.Length == 0) continue;

                var first = none.Length > 0 ? none[0].camera : legacy[0].camera;
                Debug.LogWarning($"[NavValidation] Room '{room.name}': {none.Length} camera(s) without activation trigger, " +
                                 $"{legacy.Length} with several (e.g. '{first.name}'). Fix them in the Camera Zone Viewer.", first);
                issues++;
            }

            foreach (var group in sceneDoors.Where(d => !string.IsNullOrEmpty(d.EntryPointId)).GroupBy(d => d.EntryPointId))
            {
                if (group.Count() < 2) continue;
                Debug.LogWarning($"[NavValidation] Entry point '{group.Key}' is used by several scene doors: {string.Join(", ", group.Select(d => d.name))}.", group.First());
                issues++;
            }

            foreach (var door in Object.FindObjectsOfType<RoomDoorInteractable>(includeInactive: true))
            {
                if (string.IsNullOrEmpty(door.DestinationRoomId))
                {
                    Debug.LogWarning($"[NavValidation] RoomDoorInteractable '{door.name}' has no destination set.", door);
                    issues++;
                }
                else if (RoomLookup.Find(rooms, door.DestinationRoomId) == null)
                {
                    Debug.LogWarning($"[NavValidation] RoomDoorInteractable '{door.name}' leads to '{door.DestinationRoomId}', which is not a room of this scene.", door);
                    issues++;
                }
            }

            var catalog = RoomCatalogEditor.Find();
            if (catalog == null)
            {
                Debug.LogWarning("[NavValidation] No RoomCatalog asset: create one (CrimsonDraft > Navigation > Room Catalog) and press Rebuild From Scenes.");
                issues++;
            }
            else
            {
                foreach (var message in RoomIdValidation.Check(rooms, roomDoors, catalog))
                {
                    Debug.LogWarning("[NavValidation] " + message);
                    issues++;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:MapData"))
                {
                    var map     = AssetDatabase.LoadAssetAtPath<CrimsonDraft.Infrastructure.Map.MapData>(AssetDatabase.GUIDToAssetPath(guid));
                    var unknown = map.Rooms.Select(r => r.RoomId)
                        .Concat(map.Doors.SelectMany(d => d.OtherSides).Select(s => s.RoomId))
                        .Where(id => !string.IsNullOrEmpty(id) && !catalog.Contains(id))
                        .Distinct()
                        .ToList();
                    if (unknown.Count == 0) continue;
                    Debug.LogWarning($"[NavValidation] {map.name} names rooms the catalog doesn't know: {string.Join(", ", unknown)}.", map);
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
