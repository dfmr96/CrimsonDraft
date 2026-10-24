#nullable enable

using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;
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
            go.transform.SetPositionAndRotation(FloorPointFor(camera, room), Quaternion.identity);

            var zone = go.AddComponent<FixedCameraZoneTrigger>();
            var so = new SerializedObject(zone);
            so.FindProperty("zoneCamera").objectReferenceValue = camera;
            so.FindProperty("useTwoSidedSwitch").boolValue = false;
            var polygon = so.FindProperty("polygon");
            polygon.arraySize = 4;
            polygon.GetArrayElementAtIndex(0).vector2Value = new Vector2(-1f, -1f);
            polygon.GetArrayElementAtIndex(1).vector2Value = new Vector2(-1f, 1f);
            polygon.GetArrayElementAtIndex(2).vector2Value = new Vector2(1f, 1f);
            polygon.GetArrayElementAtIndex(3).vector2Value = new Vector2(1f, -1f);
            so.FindProperty("minHeight").floatValue = -0.5f;
            so.FindProperty("maxHeight").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        /// <summary>Makes <paramref name="keep"/> the activation trigger of <paramref name="camera"/> only:
        /// single-sided, no divider camera.</summary>
        public static void KeepOnly(FixedCameraZoneTrigger keep, CinemachineCamera camera)
        {
            var so = new SerializedObject(keep);
            so.FindProperty("zoneCamera").objectReferenceValue = camera;
            so.FindProperty("useTwoSidedSwitch").boolValue = false;
            so.FindProperty("positiveSideCamera").objectReferenceValue = null;
            so.ApplyModifiedProperties();
        }

        public static void Delete(FixedCameraZoneTrigger trigger) => Undo.DestroyObjectImmediate(trigger.gameObject);

        // Where the camera looks on the floor; with nothing under its look ray, the floor of the room's
        // bounds under the camera, else a little below the camera.
        private static Vector3 FloorPointFor(CinemachineCamera camera, RoomController room)
        {
            var t = camera.transform;
            if (Physics.Raycast(t.position, t.forward, out var hit, LookDistance, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;

            var bounds = PlanViewFraming.RoomBounds(room);
            float y = bounds.size != Vector3.zero ? bounds.min.y : t.position.y - FallbackDrop;
            return new Vector3(t.position.x, y, t.position.z);
        }
    }
}
