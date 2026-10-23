#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>One-shot migration from SpawnPoint / SceneSpawnPoint to door arrivals (deleted together
    /// with the old classes). Pass 0 saves DoorArrival + ArrivalPoint into the door prefab assets, so the
    /// component is part of the door; pass 1 converts every room prefab; pass 2 every production scene.
    /// Each door gets its arrival re-placed at the default distance (on the side of the spawn it replaces
    /// when there is one) and inherits that spawn's camera and, for scene doors, its entry id. Every old
    /// spawn is deleted; the ones no door claimed are logged as errors first.</summary>
    public static class SpawnPointMigration
    {
        private const string PrefabsRoot = "Assets/Prefabs";
        private const string ScenesRoot = "Assets/Scenes/Production";

        private sealed class Stats
        {
            public int Assets, Doors, Cameras, Spawns, Unmatched;
        }

        [MenuItem("CrimsonDraft/Doors/Migrate Spawn Points To Door Arrivals")]
        private static void Migrate()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                Debug.LogError("[SpawnPointMigration] Save or discard the changes in the open scenes first; nothing was migrated.");
                return;
            }
            var reopen = SceneManager.GetActiveScene().path;
            var stats = new Stats();
            float distance = DoorArrivalSettings.Distance;

            var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsRoot }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var doorPrefabs = new List<string>();
            var roomPrefabs = new List<string>();
            foreach (var path in prefabPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;
                if (asset.GetComponentInChildren<RoomController>(true) != null) roomPrefabs.Add(path);
                else if (asset.GetComponentInChildren<RoomDoorInteractable>(true) != null
                         || asset.GetComponentInChildren<SceneDoorInteractable>(true) != null) doorPrefabs.Add(path);
            }

            // Pass 0: door prefabs own the component and its child; rooms only override the pose.
            foreach (var path in doorPrefabs)
                EditPrefab(path, root =>
                {
                    foreach (var door in Doors(root.transform))
                    {
                        var arrival = door.GetComponent<DoorArrival>() ?? door.gameObject.AddComponent<DoorArrival>();
                        arrival.PlaceAtDefault(distance, door.transform.position + door.transform.forward);
                    }
                    return true;
                }, stats);

            // Pass 1: room prefabs.
            foreach (var path in roomPrefabs)
                EditPrefab(path, root =>
                {
                    bool changed = false;
                    var sceneSpawns = root.GetComponentsInChildren<SceneSpawnPoint>(true).ToList();
                    foreach (var room in root.GetComponentsInChildren<RoomController>(true))
                        changed |= MigrateRoom(room, sceneSpawns, distance, stats, path);
                    return changed;
                }, stats);

            // Pass 2: scenes (rooms that only exist there, scene-level spawns and camera assignments).
            foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { ScenesRoot }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var sceneSpawns = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SceneSpawnPoint>(true)).ToList();
                bool changed = false;
                foreach (var room in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoomController>(true)))
                    changed |= MigrateRoom(room, sceneSpawns, distance, stats, scene.name);
                if (!changed) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                stats.Assets++;
            }

            if (!string.IsNullOrEmpty(reopen)) EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpawnPointMigration] Assets saved {stats.Assets}, doors placed {stats.Doors}, cameras copied {stats.Cameras}, " +
                      $"spawns deleted {stats.Spawns}, unmatched {stats.Unmatched}.");
        }

        private static void EditPrefab(string path, System.Func<GameObject, bool> edit, Stats stats)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!edit(root)) return;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                stats.Assets++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static IEnumerable<Component> Doors(Transform root) =>
            root.GetComponentsInChildren<RoomDoorInteractable>(true).Cast<Component>()
                .Concat(root.GetComponentsInChildren<SceneDoorInteractable>(true));

        private static bool MigrateRoom(RoomController room, List<SceneSpawnPoint> sceneSpawns, float distance, Stats stats, string where)
        {
            var spawns = room.GetComponentsInChildren<SpawnPoint>(true).ToList();
            var roomSceneSpawns = sceneSpawns.Where(s => s != null && s.StartingRoom == room).ToList();
            var doors = room.GetComponentsInChildren<RoomDoorInteractable>(true);
            var sceneDoors = room.GetComponentsInChildren<SceneDoorInteractable>(true);
            if (doors.Length == 0 && sceneDoors.Length == 0 && spawns.Count == 0 && roomSceneSpawns.Count == 0) return false;

            var used = new HashSet<Component>();
            foreach (var door in doors)
            {
                var match = Nearest(spawns.Where(s => door.Destination != null && s.FromRoom == door.Destination && !used.Contains(s)), door.transform.position);
                Place(door, match, room, distance, stats, used);
            }
            foreach (var door in sceneDoors)
            {
                var match = Nearest(roomSceneSpawns.Where(s => !used.Contains(s)), door.transform.position);
                Place(door, match, room, distance, stats, used);
                if (match == null) continue;
                var so = new SerializedObject(door);
                so.FindProperty("entryPointId").stringValue = ((SceneSpawnPoint)match).EntryPointId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var spawn in spawns.Cast<Component>().Concat(roomSceneSpawns))
            {
                if (!used.Contains(spawn))
                {
                    var from = spawn is SpawnPoint sp ? (sp.FromRoom != null ? sp.FromRoom.name : "none") : "scene entry " + ((SceneSpawnPoint)spawn).EntryPointId;
                    Debug.LogError($"[SpawnPointMigration] Unmatched spawn {where}/{room.name}/{spawn.name} from {from} at {spawn.transform.position:F2}");
                    stats.Unmatched++;
                }
                Delete(spawn, where);
                stats.Spawns++;
            }
            return true;
        }

        private static Component? Nearest(IEnumerable<Component> candidates, Vector3 position) =>
            candidates.OrderBy(c => (c.transform.position - position).sqrMagnitude).FirstOrDefault();

        private static void Place(Component door, Component? match, RoomController room, float distance, Stats stats, HashSet<Component> used)
        {
            var arrival = door.GetComponent<DoorArrival>() ?? door.gameObject.AddComponent<DoorArrival>();
            var hint = match != null ? match.transform.position : DoorArrivalPlacement.InteriorHint(room);
            arrival.PlaceAtDefault(distance, hint);
            stats.Doors++;
            if (match == null) return;

            used.Add(match);
            var camera = new SerializedObject(match).FindProperty("camera").objectReferenceValue;
            if (camera == null) return;
            var so = new SerializedObject(arrival);
            so.FindProperty("arrivalCamera").objectReferenceValue = camera;
            so.ApplyModifiedPropertiesWithoutUndo();
            stats.Cameras++;
        }

        private static void Delete(Component spawn, string where)
        {
            var go = spawn.gameObject;
            bool bare = go.transform.childCount == 0 && go.GetComponents<Component>().Length == 2;
            try
            {
                if (bare) Object.DestroyImmediate(go, true);
                else Object.DestroyImmediate(spawn, true);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SpawnPointMigration] Could not delete {where}/{go.name}: {e.Message}", go);
            }
        }
    }
}
