#nullable enable

using System.Collections.Generic;
using System.Linq;
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
    /// <summary>Bakes each room's pickup and door ids from the open scene into its MapData,
    /// upserting by RoomId so the layout authored in the Map Editor is never touched.</summary>
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

            var scene       = config.gameObject.scene;
            var controllers = InScene<RoomController>(scene);
            var markers     = InScene<MapDoorMarker>(scene);
            var pickups     = InScene<PickupInteractable>(scene);

            var pickupIds = new Dictionary<string, SortedSet<string>>();
            var doorIds   = new Dictionary<string, SortedSet<string>>();

            foreach (var controller in controllers)
            {
                if (string.IsNullOrWhiteSpace(controller.RoomId))
                {
                    Debug.LogWarning("[MapBaker] RoomController has no RoomId; it will not appear on the map.", controller);
                    continue;
                }

                pickupIds.TryAdd(controller.RoomId, new SortedSet<string>());
                doorIds.TryAdd(controller.RoomId, new SortedSet<string>());
            }

            var doors = new List<MapDoorData>(markers.Count);
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
                if (room != null && doorIds.TryGetValue(room.RoomId, out var set))
                    set.Add(doorId!);
            }

            foreach (var pickup in pickups)
            {
                if (string.IsNullOrWhiteSpace(pickup.PickupId))
                    continue;

                var room = pickup.GetComponentInParent<RoomController>();
                if (room != null && pickupIds.TryGetValue(room.RoomId, out var set))
                    set.Add(pickup.PickupId);
            }

            var baked = pickupIds.Keys
                .OrderBy(id => id, System.StringComparer.Ordinal)
                .Select(id => new MapRoomData
                {
                    RoomId    = id,
                    PickupIds = pickupIds[id].ToArray(),
                    DoorIds   = doorIds[id].ToArray(),
                })
                .ToList();

            var rooms = MapLayoutMerge.Upsert(config.Map.Rooms, baked);
            config.Map.EditorSetBakedContent(rooms, doors.OrderBy(d => d.DoorId, System.StringComparer.Ordinal).ToList());
            EditorUtility.SetDirty(config.Map);
            AssetDatabase.SaveAssets();
        }

        // Only the config's own scene: with several decks (or a test scene) open at once, a
        // floor must never absorb another scene's rooms, since the upsert keeps them for good.
        private static List<T> InScene<T>(Scene scene) where T : Component
            => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(component => component.gameObject.scene == scene)
                .ToList();
    }
}
