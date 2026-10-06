using UnityEngine;
using UnityEditor;

namespace HorrorEngine
{
    [CustomEditor(typeof(Shape))]
    public class ShapeEditor : Editor
    {
        private int m_SelectedIndex = -1;

        // --------------------------------------------------------------------

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript(); 

            Shape shape = (Shape)target;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Points:");
            if (GUILayout.Button("Add Point"))
            {
                Undo.RecordObject(shape, "Add Shape Point");
                shape.Points.Insert(0, shape.Points.Count > 0 ? shape.Points[0] : Vector3.right);
                m_SelectedIndex = 0;
                EditorUtility.SetDirty(shape);
            }
            for (int i = 0; i < shape.Points.Count; i++)
            {
                GUIStyle style = i == m_SelectedIndex ? EditorStyles.whiteLabel : EditorStyles.label;
                if (i == m_SelectedIndex)
                {
                    style = new GUIStyle(style);
                    style.normal.background = EditorGUIUtility.whiteTexture;
                }

                EditorGUILayout.BeginHorizontal(style);
                if (GUILayout.Button(i.ToString(), GUILayout.Width(30)))
                {
                    m_SelectedIndex = i;
                    SceneView.RepaintAll();
                }

                EditorGUI.BeginChangeCheck();
                shape.Points[i] = EditorGUILayout.Vector3Field("", shape.Points[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(shape, "Move Shape Point");
                    EditorUtility.SetDirty(shape);
                }

                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    Undo.RecordObject(shape, "Delete Shape Point");
                    shape.Points.RemoveAt(i);
                    if (m_SelectedIndex >= shape.Points.Count)
                    {
                        m_SelectedIndex = -1;
                    }
                    EditorUtility.SetDirty(shape);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (shape.Points.Count > 0 && GUILayout.Button("Add Point"))
            {
                Undo.RecordObject(shape, "Add Shape Point");
                shape.Points.Add(shape.Points.Count > 0 ? shape.Points[shape.Points.Count - 1] : Vector3.right);
                m_SelectedIndex = shape.Points.Count-1;
                EditorUtility.SetDirty(shape);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();


            shape.CloseShape = EditorGUILayout.Toggle("Close Shape", shape.CloseShape);

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(shape.GizmoColor)));

            SceneView.RepaintAll();
            

            serializedObject.ApplyModifiedProperties();
           
        }

        // --------------------------------------------------------------------

        void OnSceneGUI()
        {
            Shape shape = (Shape)target;
            Color shapeColor = Color.yellow;
            Handles.color = shapeColor;
            for (int i = 0; i < shape.Points.Count; i++)
            {
                Vector3 position = shape.transform.TransformPoint(shape.Points[i]);
                Handles.color = Color.blue;
                if (Handles.Button(position, Quaternion.identity, 0.25f, 0.06f, Handles.SphereHandleCap))
                {
                    m_SelectedIndex = i;
                }
                if (i > 0)
                {
                    Vector3 prevPosition = shape.transform.TransformPoint(shape.Points[i - 1]);
                    Handles.color = shapeColor;
                    Handles.DrawLine(position, prevPosition);

                    // Draw a button between this and the previous point
                    float distance = Vector3.Distance(position, prevPosition);
                    float buttonOffset = 0.5f;
                    if (distance > buttonOffset * 2)
                    {
                        Vector3 buttonPosition = Vector3.Lerp(position, prevPosition, 0.5f);
                        Handles.color = Color.green;
                        if (Handles.Button(buttonPosition, Quaternion.identity, 0.15f, 0.15f, Handles.SphereHandleCap))
                        {
                            // Add a new point between this and the previous point
                            var newPointPosition = shape.transform.InverseTransformPoint(buttonPosition);
                            shape.Points.Insert(i, newPointPosition);
                            m_SelectedIndex = i;
                            EditorUtility.SetDirty(shape);
                        }
                    }
                }
            }

            if (shape.Points.Count >= 3 && shape.CloseShape) 
            {
                Vector3 p1 = shape.transform.TransformPoint(shape.Points[shape.Points.Count - 1]);
                Vector3 p2 = shape.transform.TransformPoint(shape.Points[0]);
                Handles.color = shapeColor;
                Handles.DrawLine(p1, p2);

                Vector3 buttonPosition = Vector3.Lerp(p1, p2, 0.5f);
                Handles.color = Color.green;
                if (Handles.Button(buttonPosition, Quaternion.identity, 0.15f, 0.15f, Handles.SphereHandleCap))
                {
                    // Add a new point between this and the previous point
                    var newPointPosition = shape.transform.InverseTransformPoint(buttonPosition);
                    shape.Points.Add(newPointPosition);
                    m_SelectedIndex = shape.Points.Count-1;
                    EditorUtility.SetDirty(shape);
                }
            }

            if (m_SelectedIndex != -1)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPosition = Handles.PositionHandle(shape.transform.TransformPoint(shape.Points[m_SelectedIndex]), Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(shape, "Move Shape Point");
                    shape.Points[m_SelectedIndex] = shape.transform.InverseTransformPoint(newPosition);
                }
            }
        }

    }
}
