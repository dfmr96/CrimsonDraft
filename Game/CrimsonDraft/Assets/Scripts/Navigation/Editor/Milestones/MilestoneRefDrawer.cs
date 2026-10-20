#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    [CustomPropertyDrawer(typeof(MilestoneRef))]
    public sealed class MilestoneRefDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var graphProp = property.FindPropertyRelative("graph");
            var idProp    = property.FindPropertyRelative("nodeId");
            float line = EditorGUIUtility.singleLineHeight;

            EditorGUI.BeginProperty(position, label, property);
            var top = new Rect(position.x, position.y, position.width, line);
            EditorGUI.PropertyField(top, graphProp, label);

            var bottom = new Rect(position.x, position.y + line + EditorGUIUtility.standardVerticalSpacing, position.width, line);
            var graph = graphProp.objectReferenceValue as MilestoneGraph;
            var nodes = graph != null ? graph.Nodes : new List<MilestoneNode>();
            var titles = nodes.Select(n => n.title).Prepend("(none)").ToArray();
            int current = nodes.FindIndex(n => n.id == idProp.stringValue) + 1;
            int chosen  = EditorGUI.Popup(EditorGUI.IndentedRect(bottom), " ", current, titles);
            if (chosen != current)
                idProp.stringValue = chosen > 0 ? nodes[chosen - 1].id : "";
            EditorGUI.EndProperty();
        }
    }
}
