using UnityEditor;
using UnityEngine;

namespace CrimsonDraft.Audio.Editor
{
    [CustomPropertyDrawer(typeof(WwiseTrigger))]
    public sealed class WwiseTriggerDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var kind        = property.FindPropertyRelative("kind");
            var wwiseEvent  = property.FindPropertyRelative("wwiseEvent");
            var wwiseState  = property.FindPropertyRelative("wwiseState");
            var wwiseSwitch = property.FindPropertyRelative("wwiseSwitch");
            var wwiseRtpc   = property.FindPropertyRelative("wwiseRtpc");
            var rtpcValue   = property.FindPropertyRelative("rtpcValue");

            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(line, kind);

            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            switch ((WwiseTrigger.Kind)kind.enumValueIndex)
            {
                case WwiseTrigger.Kind.Event:
                    EditorGUI.PropertyField(line, wwiseEvent, new GUIContent("Event"));
                    break;
                case WwiseTrigger.Kind.State:
                    EditorGUI.PropertyField(line, wwiseState, new GUIContent("State"));
                    break;
                case WwiseTrigger.Kind.Switch:
                    EditorGUI.PropertyField(line, wwiseSwitch, new GUIContent("Switch"));
                    break;
                case WwiseTrigger.Kind.Rtpc:
                    var half = new Rect(line.x, line.y, line.width * 0.6f, line.height);
                    var rest = new Rect(line.x + line.width * 0.6f, line.y, line.width * 0.4f, line.height);
                    EditorGUI.PropertyField(half, wwiseRtpc, new GUIContent("RTPC"));
                    EditorGUI.PropertyField(rest, rtpcValue, GUIContent.none);
                    break;
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3;
        }
    }
}
