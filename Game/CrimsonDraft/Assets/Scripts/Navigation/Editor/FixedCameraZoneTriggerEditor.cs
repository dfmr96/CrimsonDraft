#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Scene-view handles for a camera zone: drag a polygon vertex (white cube) on the
    /// floor plane, click a "+" between two vertices to split that edge, and, with the two-sided
    /// switch on, drag the divider endpoints (yellow). Vertices can be removed from the array in
    /// the inspector.</summary>
    [CustomEditor(typeof(FixedCameraZoneTrigger))]
    public sealed class FixedCameraZoneTriggerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            this.DrawDefaultInspector();
            EditorGUILayout.HelpBox(
                "Select the object to edit the zone in the Scene view: drag the cubes to move vertices, click '+' on an edge to add one.",
                MessageType.None);
        }

        private void OnSceneGUI()
        {
            var trigger = (FixedCameraZoneTrigger)this.target;
            this.serializedObject.Update();

            var polygon = this.serializedObject.FindProperty("polygon");
            float height = this.serializedObject.FindProperty("minHeight").floatValue;
            var transform = trigger.transform;

            for (int i = 0; i < polygon.arraySize; i++)
            {
                var element = polygon.GetArrayElementAtIndex(i);
                var world = trigger.ToWorld(element.vector2Value, height);

                Handles.color = Color.white;
                EditorGUI.BeginChangeCheck();
                var moved = this.Slide(transform, world);
                if (EditorGUI.EndChangeCheck())
                {
                    var local = transform.InverseTransformPoint(moved);
                    element.vector2Value = new Vector2(local.x, local.z);
                }
            }

            // Orange handle at the centre moves the whole zone: outline and divider together.
            Handles.color = new Color(1f, 0.55f, 0.1f);
            var centerWorld = trigger.ToWorld(trigger.Centroid, height);
            EditorGUI.BeginChangeCheck();
            var centerMoved = Handles.Slider2D(centerWorld, transform.up, transform.right, transform.forward,
                HandleUtility.GetHandleSize(centerWorld) * 0.12f, Handles.RectangleHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                var delta = transform.InverseTransformPoint(centerMoved) - transform.InverseTransformPoint(centerWorld);
                Translate(this.serializedObject, new Vector2(delta.x, delta.z));
            }

            // "+" buttons on each edge midpoint. Insert after the loop so indices stay stable.
            int insertAfter = -1;
            Vector2 insertPoint = default;
            for (int i = 0; i < polygon.arraySize && polygon.arraySize >= 2; i++)
            {
                var a = polygon.GetArrayElementAtIndex(i).vector2Value;
                var b = polygon.GetArrayElementAtIndex((i + 1) % polygon.arraySize).vector2Value;
                var mid = (a + b) * 0.5f;
                var world = trigger.ToWorld(mid, height);

                Handles.color = new Color(0.4f, 1f, 0.4f);
                float size = HandleUtility.GetHandleSize(world) * 0.06f;
                if (Handles.Button(world, Quaternion.identity, size, size, Handles.DotHandleCap))
                {
                    insertAfter = i;
                    insertPoint = mid;
                }
            }
            if (insertAfter >= 0)
            {
                polygon.InsertArrayElementAtIndex(insertAfter);
                polygon.GetArrayElementAtIndex(insertAfter + 1).vector2Value = insertPoint;
            }

            if (this.serializedObject.FindProperty("useTwoSidedSwitch").boolValue)
            {
                Handles.color = Color.yellow;
                this.DividerHandle(trigger, "dividerStart", height);
                this.DividerHandle(trigger, "dividerEnd", height);

                var start = trigger.ToWorld(this.serializedObject.FindProperty("dividerStart").vector2Value, height);
                var end = trigger.ToWorld(this.serializedObject.FindProperty("dividerEnd").vector2Value, height);
                Handles.DrawDottedLine(start, end, 4f);
            }

            this.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Shifts the whole zone (outline and divider) by a local-space XZ offset. Applies the change itself.</summary>
        internal static void Translate(SerializedObject serialized, Vector2 delta)
        {
            serialized.Update();
            var polygon = serialized.FindProperty("polygon");
            for (int i = 0; i < polygon.arraySize; i++)
            {
                var element = polygon.GetArrayElementAtIndex(i);
                element.vector2Value += delta;
            }
            serialized.FindProperty("dividerStart").vector2Value += delta;
            serialized.FindProperty("dividerEnd").vector2Value += delta;
            serialized.ApplyModifiedProperties();
        }

        private void DividerHandle(FixedCameraZoneTrigger trigger, string propertyName, float height)
        {
            var property = this.serializedObject.FindProperty(propertyName);
            var world = trigger.ToWorld(property.vector2Value, height);

            EditorGUI.BeginChangeCheck();
            var moved = this.Slide(trigger.transform, world);
            if (!EditorGUI.EndChangeCheck()) return;

            var local = trigger.transform.InverseTransformPoint(moved);
            property.vector2Value = new Vector2(local.x, local.z);
        }

        // Moves a point freely inside the zone's floor plane.
        private Vector3 Slide(Transform space, Vector3 world) =>
            Handles.Slider2D(world, space.up, space.right, space.forward,
                HandleUtility.GetHandleSize(world) * 0.08f, Handles.CubeHandleCap, 0f);
    }
}
