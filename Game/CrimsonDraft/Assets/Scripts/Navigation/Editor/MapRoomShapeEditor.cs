#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Map;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>SceneView editing for room silhouettes.</summary>
    [CustomEditor(typeof(MapRoomShape))]
    public sealed class MapRoomShapeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var shape = (MapRoomShape)target;

            if (GUILayout.Button("Trace From Bounds"))
            {
                Undo.RecordObject(shape, "Trace Map Shape From Bounds");
                TraceFromBounds(shape);
                EditorUtility.SetDirty(shape);
            }

            if (GUILayout.Button("Add Point"))
            {
                Undo.RecordObject(shape, "Add Map Shape Point");
                var points = new List<Vector2>(shape.LocalPoints);
                points.Add(points.Count > 0 ? points[^1] + Vector2.right : Vector2.zero);
                shape.LocalPoints = points.ToArray();
                EditorUtility.SetDirty(shape);
            }
        }

        private void OnSceneGUI()
        {
            var shape = (MapRoomShape)target;
            var points = shape.LocalPoints;
            if (points.Length == 0)
                return;

            var t = shape.transform;

            Handles.color = Color.cyan;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 world = t.TransformPoint(new Vector3(points[i].x, 0f, points[i].y));
                Vector3 next = t.TransformPoint(new Vector3(
                    points[(i + 1) % points.Length].x, 0f, points[(i + 1) % points.Length].y));

                Handles.DrawLine(world, next, 2f);

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(
                    world, HandleUtility.GetHandleSize(world) * 0.08f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(shape, "Move Map Shape Point");
                    Vector3 local = t.InverseTransformPoint(moved);
                    points[i] = new Vector2(local.x, local.z);
                    shape.LocalPoints = points;
                    EditorUtility.SetDirty(shape);
                }
            }
        }

        private static void TraceFromBounds(MapRoomShape shape)
        {
            var renderers = shape.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[MapRoomShapeEditor] No renderers under room.", shape);
                return;
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            var t = shape.transform;
            var min = t.InverseTransformPoint(bounds.min);
            var max = t.InverseTransformPoint(bounds.max);

            shape.LocalPoints = new[]
            {
                new Vector2(min.x, min.z),
                new Vector2(max.x, min.z),
                new Vector2(max.x, max.z),
                new Vector2(min.x, max.z),
            };
        }
    }
}
