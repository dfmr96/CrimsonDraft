#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>One-shot move from room object references to game-wide room ids (deleted once run):
    /// every room gets its <c>Deck/ROOM</c> id on its prefab, door destinations and unlocksOnCross
    /// are written as ids on the room prefabs (they were scene overrides), the scene overrides go,
    /// and MapData / RoomVisitMutation ids follow.</summary>
    public static class RoomIdMigration
    {
        private sealed class DoorPlan
        {
            public string  DoorId        = "";
            public string? DestinationId;
            public DoorRef Unlocks;
        }

        private sealed class RoomPlan
        {
            public string         PrefabPath = "";
            public string         LegacyId   = "";
            public string         NewId      = "";
            public List<DoorPlan> Doors      = new List<DoorPlan>();
        }

        private sealed class Stats
        {
            public int Rooms, Doors, Unlocks, SceneOnlyDoors, MapEntries, Mutations;
            public readonly List<string> Errors = new List<string>();
        }

        [MenuItem("CrimsonDraft/Rooms/Migrate Room Ids (Dry Run)")]
        private static void DryRunMenu() => Run(dryRun: true);

        [MenuItem("CrimsonDraft/Rooms/Migrate Room Ids")]
        private static void MigrateMenu() => Run(dryRun: false);

        public static bool Run(bool dryRun)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                Debug.LogError("[RoomIdMigration] Save or discard the open scenes first; nothing was migrated.");
                return false;
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene.isDirty)
            {
                Debug.LogError("[RoomIdMigration] Save or discard the prefab open in Prefab Mode first; nothing was migrated.");
                return false;
            }

            var open  = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path).ToArray();
            var stats = new Stats();
            try
            {
                var plans = ReadScenes(stats);

                var collisions = RoomIds.FindCollisions(plans.Select(p => (RoomIds.DeckOf(p.NewId) ?? "", p.LegacyId)));
                if (collisions.Count > 0)
                {
                    Debug.LogError("[RoomIdMigration] Several rooms would get the same id, nothing was migrated: " + string.Join(", ", collisions));
                    return false;
                }

                if (!dryRun)
                {
                    var global = GlobalTable(plans);
                    foreach (var plan in plans) WritePrefab(plan, global, stats);
                    foreach (var path in RoomCatalogEditor.DeckScenes) CleanScene(path, plans, stats);
                    RewriteMapData(plans, stats);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    stats.Rooms = plans.Count;
                    stats.Doors = plans.Sum(p => p.Doors.Count(d => d.DestinationId != null));
                    stats.Unlocks = plans.Sum(p => p.Doors.Count(d => d.Unlocks.IsSet));
                }
            }
            finally
            {
                for (int i = 0; i < open.Length; i++)
                    if (!string.IsNullOrEmpty(open[i]))
                        EditorSceneManager.OpenScene(open[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
            }

            if (!dryRun)
            {
                var catalog = RoomCatalogEditor.Find();
                if (catalog != null) RoomCatalogEditor.Rebuild(catalog);
            }

            foreach (var error in stats.Errors) Debug.LogError("[RoomIdMigration] " + error);
            Debug.Log($"[RoomIdMigration] {(dryRun ? "DRY RUN, nothing saved. " : "")}Rooms {stats.Rooms}, door destinations {stats.Doors}, " +
                      $"unlocks {stats.Unlocks}, doors only in the scene {stats.SceneOnlyDoors}, map entries {stats.MapEntries}, " +
                      $"visit mutations {stats.Mutations}, problems {stats.Errors.Count}.");
            return true;
        }

        // Pass 1: the scenes are the only place the old references resolve.
        private static List<RoomPlan> ReadScenes(Stats stats)
        {
            var plans = new List<RoomPlan>();
            foreach (var path in RoomCatalogEditor.DeckScenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var deck  = RoomIds.DeckOfScene(scene.name);
                var rooms = Rooms(scene);

                foreach (var room in rooms)
                {
                    var plan = new RoomPlan
                    {
                        PrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(room.gameObject),
                        LegacyId   = room.RoomId,
                        NewId      = RoomIds.Normalize(deck, room.RoomId),
                    };

                    foreach (var door in room.GetComponentsInChildren<RoomDoorInteractable>(true))
                    {
                        var so      = new SerializedObject(door);
                        var target  = so.FindProperty("destination").objectReferenceValue as RoomController;
                        var unlocks = so.FindProperty("unlocksOnCross").objectReferenceValue as RoomDoorInteractable;
                        var unlocksRoom = unlocks != null ? unlocks.GetComponentInParent<RoomController>(true) : null;

                        var doorPlan = new DoorPlan
                        {
                            DoorId        = door.DoorId,
                            DestinationId = target != null ? RoomIds.Normalize(deck, target.RoomId) : null,
                            Unlocks       = unlocks != null && unlocksRoom != null
                                ? new DoorRef(RoomIds.Normalize(deck, unlocksRoom.RoomId), unlocks.DoorId)
                                : default,
                        };
                        if (target == null)
                            stats.Errors.Add($"{scene.name}/{room.name}/{door.name}: no destination to migrate.");
                        if (unlocks != null && unlocksRoom == null)
                            stats.Errors.Add($"{scene.name}/{room.name}/{door.name}: unlocksOnCross '{unlocks.name}' is outside every room.");
                        plan.Doors.Add(doorPlan);
                    }

                    var duplicateDoorIds = plan.Doors.GroupBy(d => d.DoorId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                    foreach (var id in duplicateDoorIds)
                        stats.Errors.Add($"{scene.name}/{room.name}: door id '{id}' is used twice in the room; its doors are matched by order.");

                    plans.Add(plan);
                }
            }
            return plans;
        }

        // Old id -> new id across decks; an old id used on several decks is ambiguous (null).
        private static Dictionary<string, string?> GlobalTable(List<RoomPlan> plans)
        {
            var table = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var plan in plans)
                table[plan.LegacyId] = table.ContainsKey(plan.LegacyId) ? null : plan.NewId;
            return table;
        }

        // Pass 2: the ids live on the room prefabs from now on.
        private static void WritePrefab(RoomPlan plan, Dictionary<string, string?> global, Stats stats)
        {
            var root = PrefabUtility.LoadPrefabContents(plan.PrefabPath);
            try
            {
                var room = root.GetComponent<RoomController>();
                SetString(room, "roomId", plan.NewId);
                stats.Rooms++;

                var doors = root.GetComponentsInChildren<RoomDoorInteractable>(true).ToList();
                foreach (var group in plan.Doors.GroupBy(d => d.DoorId))
                {
                    var prefabDoors = doors.Where(d => d.DoorId == group.Key).ToList();
                    int i = 0;
                    foreach (var doorPlan in group)
                    {
                        if (i >= prefabDoors.Count)
                        {
                            stats.SceneOnlyDoors++;
                            continue; // added in the scene: written there by CleanScene
                        }
                        Apply(prefabDoors[i++], doorPlan, stats);
                    }
                }

                foreach (var mutation in root.GetComponentsInChildren<RoomVisitMutation>(true))
                {
                    var so       = new SerializedObject(mutation);
                    var property = so.FindProperty("triggerRoomId");
                    var legacy   = property.stringValue;
                    if (global.TryGetValue(legacy, out var mapped) && mapped != null) property.stringValue = mapped;
                    else stats.Errors.Add($"{plan.PrefabPath}: visit mutation room '{legacy}' is unknown or exists on several decks.");
                    so.ApplyModifiedPropertiesWithoutUndo();
                    stats.Mutations++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, plan.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Apply(RoomDoorInteractable door, DoorPlan plan, Stats stats)
        {
            var so = new SerializedObject(door);
            if (plan.DestinationId != null)
            {
                so.FindProperty("destinationRoomId").stringValue = plan.DestinationId;
                stats.Doors++;
            }
            if (plan.Unlocks.IsSet)
            {
                so.FindProperty("unlocksOnCrossRef.roomId").stringValue = plan.Unlocks.RoomId;
                so.FindProperty("unlocksOnCrossRef.doorId").stringValue = plan.Unlocks.DoorId;
                stats.Unlocks++;
            }
            so.FindProperty("destination").objectReferenceValue    = null;
            so.FindProperty("unlocksOnCross").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Pass 3: drop the scene overrides so the prefab values show; doors that exist only in the
        // scene (added overrides) get their ids written there.
        private static void CleanScene(string path, List<RoomPlan> plans, Stats stats)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var room in Rooms(scene))
            {
                var roomPlan = plans.FirstOrDefault(p => p.PrefabPath == PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(room.gameObject));
                Revert(room, "roomId");
                foreach (var door in room.GetComponentsInChildren<RoomDoorInteractable>(true))
                {
                    if (PrefabUtility.IsAddedComponentOverride(door) || !PrefabUtility.IsPartOfPrefabInstance(door))
                    {
                        var plan = roomPlan?.Doors.FirstOrDefault(d => d.DoorId == door.DoorId);
                        if (plan != null) Apply(door, plan, stats);
                        continue;
                    }
                    Revert(door, "destination");
                    Revert(door, "unlocksOnCross");
                    Revert(door, "destinationRoomId");
                    Revert(door, "unlocksOnCrossRef");
                }
                foreach (var mutation in room.GetComponentsInChildren<RoomVisitMutation>(true))
                    Revert(mutation, "triggerRoomId");
            }

            var instances = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject)
                .Where(PrefabUtility.IsOutermostPrefabInstanceRoot)
                .ToArray();
            if (instances.Length > 0) PrefabUtility.RemoveUnusedOverrides(instances, InteractionMode.AutomatedAction);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // Pass 4: map entries carry room ids too.
        private static void RewriteMapData(List<RoomPlan> plans, Stats stats)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:MapData"))
            {
                var map  = AssetDatabase.LoadAssetAtPath<MapData>(AssetDatabase.GUIDToAssetPath(guid));
                var deck = RoomIds.DeckOfScene(map.SceneName);
                var table = plans.Where(p => RoomIds.DeckOf(p.NewId) == deck)
                    .ToDictionary(p => p.LegacyId, p => p.NewId, StringComparer.Ordinal);
                string Map(string legacy) => table.TryGetValue(legacy, out var id) ? id : RoomIds.Normalize(deck, legacy);

                var so    = new SerializedObject(map);
                var rooms = so.FindProperty("rooms");
                for (int i = 0; i < rooms.arraySize; i++)
                {
                    var id = rooms.GetArrayElementAtIndex(i).FindPropertyRelative("RoomId");
                    if (string.IsNullOrEmpty(id.stringValue)) continue;
                    id.stringValue = Map(id.stringValue);
                    stats.MapEntries++;
                }
                var doors = so.FindProperty("doors");
                for (int i = 0; i < doors.arraySize; i++)
                {
                    var sides = doors.GetArrayElementAtIndex(i).FindPropertyRelative("OtherSides");
                    for (int j = 0; j < sides.arraySize; j++)
                    {
                        var id = sides.GetArrayElementAtIndex(j).FindPropertyRelative("RoomId");
                        if (string.IsNullOrEmpty(id.stringValue)) continue;
                        id.stringValue = Map(id.stringValue);
                        stats.MapEntries++;
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(map);
            }
        }

        private static List<RoomController> Rooms(Scene scene)
            => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoomController>(true)).ToList();

        private static void SetString(UnityEngine.Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Revert(UnityEngine.Object target, string property)
        {
            var prop = new SerializedObject(target).FindProperty(property);
            if (prop != null && prop.prefabOverride)
                PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
        }
    }
}
