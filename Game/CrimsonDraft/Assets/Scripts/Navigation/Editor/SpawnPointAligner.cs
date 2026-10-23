#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Puts every SpawnPoint of a room at the same distance in front of the door the player came
    /// through, facing into the room (back to the door). A SpawnPoint belongs to the RoomDoorInteractable
    /// of its own room whose destination is the SpawnPoint's fromRoom; with several such doors the one
    /// closest to the current spawn wins. The door side comes from where the spawn already is, and the
    /// door plane from the thinnest horizontal axis of its renderers, so a model whose forward axis does
    /// not match its look still works. Height is left untouched.</summary>
    public static class SpawnPointAligner
    {
        private const string DistancePref = "CrimsonDraft.SpawnDoorDistance";
        private const float DefaultDistance = 1f;

        public static float Distance
        {
            get => EditorPrefs.GetFloat(DistancePref, DefaultDistance);
            set => EditorPrefs.SetFloat(DistancePref, Mathf.Max(0.1f, value));
        }

        [MenuItem("CrimsonDraft/Spawn Points/Align To Doors (All Rooms In Open Scenes)")]
        private static void AlignAllRooms()
        {
            int aligned = 0;
            var skipped = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                    foreach (var room in root.GetComponentsInChildren<RoomController>(true))
                        aligned += Align(room, Distance, skipped);
            }
            Report(aligned, skipped);
        }

        /// <summary>Aligns the SpawnPoints of one room. Returns how many moved; the rest are listed in <paramref name="skipped"/>.</summary>
        public static int Align(RoomController room, float distance, List<string> skipped)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Align Spawn Points To Doors");

            var doors = room.GetComponentsInChildren<RoomDoorInteractable>(true);
            int aligned = 0;

            foreach (var spawn in room.GetComponentsInChildren<SpawnPoint>(true))
            {
                // A spawn that is only a room-wide fallback (fromRoom is its own room or empty) has no door.
                var candidates = doors.Where(d => d.Destination != null && d.Destination == spawn.FromRoom).ToArray();
                if (candidates.Length == 0)
                {
                    skipped.Add($"{room.name}/{spawn.name} (from {(spawn.FromRoom != null ? spawn.FromRoom.name : "none")}): no door leads there");
                    continue;
                }

                var position = spawn.transform.position;
                var door = candidates.OrderBy(d => HorizontalDistance(DoorBounds(d).center, position)).First();
                var center = DoorBounds(door);
                var size = center.size;
                var normal = size.x < size.z ? Vector3.right : Vector3.forward;
                if (Vector3.Dot(position - center.center, normal) < 0f) normal = -normal;

                Undo.RecordObject(spawn.transform, "Align Spawn Points To Doors");
                var target = center.center + normal * distance;
                target.y = position.y;
                spawn.transform.SetPositionAndRotation(target, Quaternion.LookRotation(normal, Vector3.up));
                EditorUtility.SetDirty(spawn.transform);
                aligned++;
            }
            return aligned;
        }

        public static void Report(int aligned, List<string> skipped)
        {
            foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
                if (scene.isLoaded) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            var message = $"[SpawnPointAligner] Aligned {aligned} spawn point(s) at {Distance:0.##} m from their door.";
            if (skipped.Count > 0)
                message += $"\nSkipped {skipped.Count}:\n  " + string.Join("\n  ", skipped);
            Debug.Log(message);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Bounds DoorBounds(RoomDoorInteractable door)
        {
            Bounds bounds = default;
            bool any = false;
            foreach (var renderer in door.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            // No model: fall back to a thin slab on the pivot, aligned like the transform.
            return any ? bounds : new Bounds(door.transform.position, new Vector3(0.2f, 2f, 1.2f));
        }
    }
}
