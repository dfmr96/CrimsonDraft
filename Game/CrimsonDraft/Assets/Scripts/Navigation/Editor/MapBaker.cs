#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Bakes each room's pickup and door ids from the open scene into its MapData,
    /// upserting by RoomId / DoorId so the layout authored in the Map Editor is never touched.
    /// Runs only from the Map Editor's Bake button: never on scene save or entering Play, so a
    /// MapData changes only when someone asks for it. Each door excluded from the map is paired
    /// with the drawn half of the same doorway (MapDoorPairing) and stored as its other side.</summary>
    public static class MapBaker
    {
        public static void BakeAllInOpenScenes()
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

            // __GAMEPLAYCORE carries a MapSceneConfig, so any scene holding it (a test or temp scene)
            // would otherwise bake an empty floor and drop every door's editor layout.
            if (controllers.Count == 0)
            {
                Debug.LogWarning($"[MapBaker] Skipped {config.Map.name}: scene '{scene.name}' has no RoomController, so it isn't that floor's scene.", config);
                return;
            }

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

            var doors     = new List<MapDoorData>(markers.Count);
            var ownerOf   = new Dictionary<string, MapDoorMarker>();
            var drawnEnds = new List<MapDoorEnd>();
            var farEnds   = new List<MapDoorEnd>();
            foreach (var marker in markers)
            {
                if (marker.ExcludeFromMap)
                {
                    if (EndOf(marker) is { } farEnd)
                        farEnds.Add(farEnd);
                    continue;
                }

                var doorId = marker.ResolveDoorId();
                if (string.IsNullOrWhiteSpace(doorId))
                {
                    Debug.LogWarning("[MapBaker] MapDoorMarker is missing a DoorId.", marker);
                    continue;
                }

                if (!ownerOf.TryAdd(doorId!, marker))
                {
                    Debug.LogWarning(
                        $"[MapBaker] DoorId '{doorId}' is used by both '{PathOf(ownerOf[doorId!])}' and '{PathOf(marker)}'. " +
                        "Doors sharing an id share lock state; only the first is put on the map.", marker);
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

                if (EndOf(marker) is { } drawnEnd)
                    drawnEnds.Add(drawnEnd);
            }

            var pairing = MapDoorPairing.Pair(drawnEnds, farEnds);
            foreach (var door in doors)
                door.OtherSides = pairing.OtherSidesOf(door.DoorId).ToArray();
            ReportUnmatched(config.Map, pairing);

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

            var rooms       = MapLayoutMerge.Upsert(config.Map.Rooms, baked);
            var mergedDoors = MapLayoutMerge.UpsertDoors(config.Map.Doors, doors.OrderBy(d => d.DoorId, System.StringComparer.Ordinal).ToList());
            config.Map.EditorSetBakedContent(rooms, mergedDoors);
            EditorUtility.SetDirty(config.Map);
            AssetDatabase.SaveAssets();
        }

        // A door half as the pairing sees it; null when it has no id or sits outside a room.
        private static MapDoorEnd? EndOf(MapDoorMarker marker)
        {
            var doorId = marker.ResolveDoorId();
            var room   = marker.GetComponentInParent<RoomController>();
            if (string.IsNullOrWhiteSpace(doorId) || room == null || string.IsNullOrWhiteSpace(room.RoomId))
                return null;

            var destinationId = marker.GetComponent<RoomDoorInteractable>()?.DestinationRoomId;
            return new MapDoorEnd(doorId!, room.RoomId, string.IsNullOrEmpty(destinationId) ? null : destinationId);
        }

        // An excluded half that finds no drawn half reveals nothing on the map, so say so.
        private static void ReportUnmatched(MapData map, MapDoorPairing.Result pairing)
        {
            foreach (var end in pairing.Ambiguous)
                Debug.LogWarning(
                    $"[MapBaker] {map.name}: excluded door '{end.DoorId}' ({end.RoomId}) matches several drawn doors leading back from " +
                    $"'{end.DestinationRoomId}', so it was paired with none and its room won't reveal any of them.", map);

            if (pairing.Unpaired.Count > 0)
                Debug.LogWarning(
                    $"[MapBaker] {map.name}: these excluded doors have no drawn door leading back through the same doorway, " +
                    "so their room won't reveal it on the map: " +
                    string.Join(", ", pairing.Unpaired.Select(end => $"{end.DoorId} ({end.RoomId})")), map);
        }

        private static string PathOf(Component component)
        {
            var path = component.name;
            for (var t = component.transform.parent; t != null; t = t.parent)
                path = t.name + "/" + path;
            return path;
        }

        // Only the config's own scene: with several decks (or a test scene) open at once, a
        // floor must never absorb another scene's rooms, since the upsert keeps them for good.
        private static List<T> InScene<T>(Scene scene) where T : Component
            => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(component => component.gameObject.scene == scene)
                .ToList();
    }
}
