#nullable enable

using System.Collections.Generic;
using System.IO;
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
    /// with the old classes).
    ///
    /// Door destinations and spawn fromRooms point at other rooms, so they only exist as overrides on
    /// the room prefab instances in the deck scenes; inside a room prefab asset they are null. That is
    /// why the matching runs in the scenes:
    /// 0. door prefab assets get DoorArrival + ArrivalPoint (bases before variants, so a variant inherits
    ///    the component instead of adding a second one);
    /// 1. every production scene: each door instance gets its arrival placed (on the side of the spawn
    ///    it replaces), that spawn's camera and, for scene doors, its entry id; spawns that are scene
    ///    objects are deleted there, spawns that come from a room prefab are left for pass 2;
    /// 2. room prefab assets: their SpawnPoint / SceneSpawnPoint are deleted;
    /// 3. every production scene again: overrides left pointing at the deleted spawns are removed.
    /// Spawns no door claimed are logged as errors. Refuses to run while anything is unsaved, and once
    /// no spawn is left (a second run would reset every hand-tuned arrival).</summary>
    public static class SpawnPointMigration
    {
        private const string PrefabsRoot = "Assets/Prefabs";
        private const string ScenesRoot = "Assets/Scenes/Production";

        public const string Explanation =
            "Adds a DoorArrival to every door prefab, places every door arrival in every production scene, copies " +
            "the spawn cameras and entry ids to the doors, and deletes every SpawnPoint and SceneSpawnPoint. " +
            "Prefabs and scenes are saved. Commit or stash your own changes first.";

        private sealed class Stats
        {
            public int Saved, Doors, Cameras, Spawns, Unmatched;
        }

        [MenuItem("CrimsonDraft/Doors/Migrate Spawn Points To Door Arrivals")]
        internal static void MigrateWithConfirmation()
        {
            if (!CanRun(out var reason)) { Debug.LogError("[SpawnPointMigration] " + reason); return; }
            if (EditorUtility.DisplayDialog("Migrate spawn points to door arrivals", Explanation, "Migrate", "Cancel"))
                Run(dryRun: false);
        }

        [MenuItem("CrimsonDraft/Doors/Migrate Spawn Points To Door Arrivals (Dry Run)")]
        internal static void DryRun()
        {
            if (!CanRun(out var reason)) { Debug.LogError("[SpawnPointMigration] " + reason); return; }
            Run(dryRun: true);
        }

        /// <summary>Whether there is still anything to migrate; cheap enough for a button state.</summary>
        public static bool HasLegacySpawns()
        {
            var spawnGuid = ScriptGuid(typeof(SpawnPoint));
            var sceneSpawnGuid = ScriptGuid(typeof(SceneSpawnPoint));
            return AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsRoot }).Concat(AssetDatabase.FindAssets("t:Scene", new[] { ScenesRoot }))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Any(path => FileMentions(path, spawnGuid) || FileMentions(path, sceneSpawnGuid));
        }

        private static bool CanRun(out string reason)
        {
            reason = "";
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                reason = "Save or discard the changes in the open scenes first; nothing was migrated.";
                return false;
            }
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene.isDirty)
            {
                reason = "Save or discard the prefab open in Prefab Mode first; nothing was migrated.";
                return false;
            }
            if (!HasLegacySpawns())
            {
                reason = "No SpawnPoint or SceneSpawnPoint left: already migrated. Nothing was changed.";
                return false;
            }
            return true;
        }

        private static void Run(bool dryRun)
        {
            var openScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path)
                .Where(p => !string.IsNullOrEmpty(p)).ToArray();
            var stats = new Stats();
            float distance = DoorArrivalSettings.Distance;
            var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { ScenesRoot }).Select(AssetDatabase.GUIDToAssetPath).ToArray();

            try
            {
                var doorPrefabs = new List<string>();
                var roomPrefabs = new List<string>();
                foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsRoot }).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null) continue;
                    if (asset.GetComponentInChildren<RoomController>(true) != null) roomPrefabs.Add(path);
                    else if (asset.GetComponentInChildren<RoomDoorInteractable>(true) != null
                             || asset.GetComponentInChildren<SceneDoorInteractable>(true) != null) doorPrefabs.Add(path);
                }

                if (!dryRun)
                {
                    // Pass 0: bases before variants.
                    foreach (var path in doorPrefabs.OrderBy(VariantDepth))
                        EditPrefab(path, root =>
                        {
                            foreach (var door in Doors(root.transform))
                            {
                                var arrival = door.GetComponent<DoorArrival>();
                                if (arrival != null && arrival.ArrivalPoint != null) continue; // inherited from the base
                                arrival ??= door.gameObject.AddComponent<DoorArrival>();
                                arrival.PlaceAtDefault(distance, door.transform.position + door.transform.forward);
                            }
                            return true;
                        }, stats);
                }

                // Pass 1: scenes, where door destinations and spawn fromRooms resolve.
                foreach (var path in scenePaths)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var roots = scene.GetRootGameObjects();
                    var sceneSpawns = roots.SelectMany(g => g.GetComponentsInChildren<SceneSpawnPoint>(true)).ToList();
                    bool changed = false;
                    foreach (var room in roots.SelectMany(g => g.GetComponentsInChildren<RoomController>(true)))
                        changed |= MigrateRoom(room, sceneSpawns, distance, stats, scene.name, dryRun);
                    if (!changed || dryRun) continue;
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    stats.Saved++;
                }

                if (!dryRun)
                {
                    // Pass 2: the spawns that came from room prefabs.
                    foreach (var path in roomPrefabs)
                        EditPrefab(path, root =>
                        {
                            var spawns = root.GetComponentsInChildren<SpawnPoint>(true).Cast<Component>()
                                .Concat(root.GetComponentsInChildren<SceneSpawnPoint>(true)).ToList();
                            foreach (var spawn in spawns) Delete(spawn, path);
                            return spawns.Count > 0;
                        }, stats);

                    // Pass 3: overrides that pointed at the deleted spawns.
                    foreach (var path in scenePaths)
                    {
                        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                        var instances = scene.GetRootGameObjects()
                            .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                            .Select(t => t.gameObject)
                            .Where(PrefabUtility.IsOutermostPrefabInstanceRoot)
                            .ToArray();
                        if (instances.Length == 0) continue;
                        PrefabUtility.RemoveUnusedOverrides(instances, InteractionMode.AutomatedAction);
                        EditorSceneManager.SaveScene(scene);
                    }
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                for (int i = 0; i < openScenes.Length; i++)
                    EditorSceneManager.OpenScene(openScenes[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
            }

            Debug.Log($"[SpawnPointMigration] {(dryRun ? "DRY RUN, nothing saved. " : "")}Assets saved {stats.Saved}, door arrivals placed {stats.Doors}, " +
                      $"cameras copied {stats.Cameras}, spawns handled {stats.Spawns}, unmatched {stats.Unmatched}.");
        }

        private static int VariantDepth(string path)
        {
            int depth = 0;
            var current = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            while (current != null && PrefabUtility.GetPrefabAssetType(current) == PrefabAssetType.Variant)
            {
                current = PrefabUtility.GetCorrespondingObjectFromSource(current);
                depth++;
            }
            return depth;
        }

        private static void EditPrefab(string path, System.Func<GameObject, bool> edit, Stats stats)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!edit(root)) return;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                stats.Saved++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static IEnumerable<Component> Doors(Transform root) =>
            root.GetComponentsInChildren<RoomDoorInteractable>(true).Cast<Component>()
                .Concat(root.GetComponentsInChildren<SceneDoorInteractable>(true));

        private static bool MigrateRoom(RoomController room, List<SceneSpawnPoint> sceneSpawns, float distance, Stats stats, string where, bool dryRun)
        {
            var spawns = room.GetComponentsInChildren<SpawnPoint>(true);
            var roomSceneSpawns = sceneSpawns.Where(s => s != null && s.StartingRoom == room).ToArray();
            var doors = room.GetComponentsInChildren<RoomDoorInteractable>(true);
            var sceneDoors = room.GetComponentsInChildren<SceneDoorInteractable>(true);
            if (doors.Length == 0 && sceneDoors.Length == 0 && spawns.Length == 0 && roomSceneSpawns.Length == 0) return false;

            var used = new HashSet<Component>();
            var roomMatch = SpawnMatching.MatchNearest(
                doors.Select(d => d.transform.position).ToList(), spawns.Select(s => s.transform.position).ToList(),
                (d, s) => doors[d].Destination != null && spawns[s].FromRoom == doors[d].Destination);
            for (int i = 0; i < doors.Length; i++)
            {
                SpawnPoint? match = roomMatch.TryGetValue(i, out var s) ? spawns[s] : null;
                if (match != null) used.Add(match);
                if (!dryRun) Place(doors[i], match, room, distance, stats);
            }

            var sceneMatch = SpawnMatching.MatchNearest(
                sceneDoors.Select(d => d.transform.position).ToList(), roomSceneSpawns.Select(s => s.transform.position).ToList(),
                (d, s) => true);
            for (int i = 0; i < sceneDoors.Length; i++)
            {
                SceneSpawnPoint? match = sceneMatch.TryGetValue(i, out var s) ? roomSceneSpawns[s] : null;
                if (match != null) used.Add(match);
                if (dryRun) continue;
                Place(sceneDoors[i], match, room, distance, stats);
                if (match == null) continue;
                var so = new SerializedObject(sceneDoors[i]);
                so.FindProperty("entryPointId").stringValue = match.EntryPointId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var spawn in spawns.Cast<Component>().Concat(roomSceneSpawns))
            {
                stats.Spawns++;
                if (!used.Contains(spawn))
                {
                    var from = spawn is SpawnPoint sp ? (sp.FromRoom != null ? sp.FromRoom.name : "none") : "scene entry " + ((SceneSpawnPoint)spawn).EntryPointId;
                    Debug.LogError($"[SpawnPointMigration] Unmatched spawn {where}/{room.name}/{spawn.name} from {from} at {spawn.transform.position:F2}", spawn);
                    stats.Unmatched++;
                }
                // Spawns that come from a room prefab are deleted from the prefab in pass 2.
                if (!dryRun && !PrefabUtility.IsPartOfPrefabInstance(spawn)) Delete(spawn, where);
            }
            return true;
        }

        private static void Place(Component door, Component? match, RoomController room, float distance, Stats stats)
        {
            var arrival = door.GetComponent<DoorArrival>() ?? door.gameObject.AddComponent<DoorArrival>();
            var hint = match != null ? match.transform.position : DoorArrivalPlacement.InteriorHint(room);
            arrival.PlaceAtDefault(distance, hint);
            stats.Doors++;
            if (match == null) return;

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

        private static string ScriptGuid(System.Type type)
        {
            var script = AssetDatabase.FindAssets(type.Name + " t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == type.Name);
            return script != null ? AssetDatabase.AssetPathToGUID(script) : "";
        }

        private static bool FileMentions(string assetPath, string guid)
        {
            if (string.IsNullOrEmpty(guid)) return false;
            var full = Path.GetFullPath(assetPath);
            return File.Exists(full) && File.ReadAllText(full).Contains(guid);
        }
    }
}
