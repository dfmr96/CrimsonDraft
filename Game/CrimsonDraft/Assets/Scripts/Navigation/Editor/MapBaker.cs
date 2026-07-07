#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Bakes scene-authored map geometry into the scene's MapData asset.</summary>
    [InitializeOnLoad]
    public static class MapBaker
    {
        static MapBaker()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnSceneSaved(Scene scene) => BakeAllInOpenScenes();

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                BakeAllInOpenScenes();
        }

        private static void BakeAllInOpenScenes()
        {
            foreach (var config in Object.FindObjectsByType<MapSceneConfig>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (config.Map == null)
                {
                    Debug.LogWarning("[MapBaker] MapSceneConfig has no MapData assigned.", config);
                    continue;
                }

                Bake(config);
            }
        }

        public static void Bake(MapSceneConfig config)
        {
            if (config.Map == null)
                return;

            var shapes = Object.FindObjectsByType<MapRoomShape>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var markers = Object.FindObjectsByType<MapDoorMarker>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var pickups = Object.FindObjectsByType<PickupInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var rooms = new List<MapRoomData>(shapes.Length);
            var doors = new List<MapDoorData>(markers.Length);
            var roomByController = new Dictionary<RoomController, MapRoomData>();

            foreach (var shape in shapes)
            {
                var room = shape.Room;
                if (string.IsNullOrWhiteSpace(room.RoomId))
                {
                    Debug.LogWarning("[MapBaker] MapRoomShape has no RoomId.", shape);
                    continue;
                }

                var roomData = new MapRoomData
                {
                    RoomId = room.RoomId,
                    Polygon = shape.LocalPoints,
                    Transform = new MapElementTransform
                    {
                        Offset = shape.MapOffset,
                        Rotation = shape.MapRotation,
                        Scale = shape.MapScale,
                        ZOrder = shape.ZOrder,
                    },
                    DoorIds = System.Array.Empty<string>(),
                    PickupIds = System.Array.Empty<string>(),
                };

                rooms.Add(roomData);
                roomByController[room] = roomData;
            }

            foreach (var marker in markers)
            {
                if (marker.ExcludeFromMap)
                    continue;

                var doorId = marker.ResolveDoorId();
                if (string.IsNullOrWhiteSpace(doorId))
                {
                    Debug.LogWarning("[MapBaker] MapDoorMarker is missing a DoorId.", marker);
                    continue;
                }

                doors.Add(new MapDoorData
                {
                    DoorId = doorId!,
                    Transform = new MapElementTransform
                    {
                        Offset = marker.MapOffset,
                        Rotation = marker.MapRotation,
                        Scale = Vector2.one,
                        ZOrder = 0f,
                    },
                    Size = marker.Size,
                });

                var room = marker.GetComponentInParent<RoomController>();
                if (room != null && roomByController.TryGetValue(room, out var roomData))
                {
                    var ids = new List<string>(roomData.DoorIds) { doorId! };
                    roomData.DoorIds = ids.ToArray();
                }
            }

            foreach (var pickup in pickups)
            {
                if (string.IsNullOrWhiteSpace(pickup.PickupId))
                    continue;

                var room = pickup.GetComponentInParent<RoomController>();
                if (room == null || !roomByController.TryGetValue(room, out var roomData))
                    continue;

                var ids = new List<string>(roomData.PickupIds) { pickup.PickupId };
                roomData.PickupIds = ids.ToArray();
            }

            config.Map.EditorSetBakedContent(rooms, doors);
            EditorUtility.SetDirty(config.Map);
            AssetDatabase.SaveAssets();
        }
    }
}
