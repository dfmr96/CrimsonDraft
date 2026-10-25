#nullable enable

using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Undoable actions that bring a camera to "one activation trigger": create it, keep one of
    /// several, delete the rest.</summary>
    public static class CameraActivationEditing
    {
        private const float LookDistance = 50f;
        private const float FallbackDrop = 2f;

        /// <summary>A single-sided 2×2 m zone for <paramref name="camera"/>, on the floor where the camera
        /// looks, under the room's <c>Cameras</c> object.</summary>
        public static FixedCameraZoneTrigger CreateTrigger(CinemachineCamera camera, RoomController room)
        {
            var parent = room.transform.Find("Cameras");
            if (parent == null)
            {
                var camerasRoot = new GameObject("Cameras");
                Undo.RegisterCreatedObjectUndo(camerasRoot, "Create Camera Trigger");
                camerasRoot.transform.SetParent(room.transform, false);
                parent = camerasRoot.transform;
            }

            var go = new GameObject("FixedCameraZoneTrigger_" + camera.name);
            Undo.RegisterCreatedObjectUndo(go, "Create Camera Trigger");
            go.transform.SetParent(parent, false);
            var floor = FloorPointFor(camera, room);
            go.transform.SetPositionAndRotation(floor, Quaternion.identity);

            // The polygon lives in the parent's space: a 2x2 square around the floor point.
            var center = parent.InverseTransformPoint(floor);
            var zone = go.AddComponent<FixedCameraZoneTrigger>();
            var so = new SerializedObject(zone);
            so.FindProperty("zoneCamera").objectReferenceValue = camera;
            var polygon = so.FindProperty("polygon");
            polygon.arraySize = 4;
            polygon.GetArrayElementAtIndex(0).vector2Value = new Vector2(center.x - 1f, center.z - 1f);
            polygon.GetArrayElementAtIndex(1).vector2Value = new Vector2(center.x - 1f, center.z + 1f);
            polygon.GetArrayElementAtIndex(2).vector2Value = new Vector2(center.x + 1f, center.z + 1f);
            polygon.GetArrayElementAtIndex(3).vector2Value = new Vector2(center.x + 1f, center.z - 1f);
            so.FindProperty("minHeight").floatValue = center.y - 0.5f;
            so.FindProperty("maxHeight").floatValue = center.y + 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        /// <summary>Makes <paramref name="keep"/> the activation trigger of <paramref name="camera"/>.</summary>
        public static void KeepOnly(FixedCameraZoneTrigger keep, CinemachineCamera camera)
        {
            var so = new SerializedObject(keep);
            so.FindProperty("zoneCamera").objectReferenceValue = camera;
            so.ApplyModifiedProperties();
        }

        public static void Delete(FixedCameraZoneTrigger trigger) => Undo.DestroyObjectImmediate(trigger.gameObject);

        // Where the camera looks on the floor. The look ray's first hit is only taken when it is floor
        // (upward-facing, not the player); a wall or prop hit drops straight down to the floor in front of
        // it. With no floor found, the floor of the room's bounds under the camera, else a little below it.
        // A zone placed on a wall or at player height would never contain the player's point.
        private static Vector3 FloorPointFor(CinemachineCamera camera, RoomController room)
        {
            Physics.SyncTransforms();
            var t = camera.transform;
            if (TryFirstHit(t.position, t.forward, LookDistance, out var hit))
            {
                if (IsFloor(hit)) return hit.point;
                if (TryFirstHit(hit.point + hit.normal * 0.2f, Vector3.down, LookDistance, out var below) && IsFloor(below))
                    return below.point;
            }

            var bounds = PlanViewFraming.RoomBounds(room);
            float y = bounds.size != Vector3.zero ? bounds.min.y : t.position.y - FallbackDrop;
            return new Vector3(t.position.x, y, t.position.z);
        }

        private static bool IsFloor(RaycastHit hit) => hit.normal.y > 0.7f;

        // Nearest non-trigger hit that is not the player's own collider.
        private static bool TryFirstHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit first)
        {
            first = default;
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= best || hit.collider.GetComponentInParent<PlayerController>() != null) continue;
                first = hit;
                best = hit.distance;
            }
            return best < float.MaxValue;
        }
    }
}
