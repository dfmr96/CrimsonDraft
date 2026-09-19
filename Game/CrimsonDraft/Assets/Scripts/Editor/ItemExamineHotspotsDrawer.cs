#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Editor
{
    // Draws ItemExamineHotspots.Hotspot.dialogue and ItemExamineHotspots.defaultDialogue
    // with a Yarn node picker restricted to nodes tagged "examine" (a Yarn node header,
    // e.g. `tags: examine`) instead of YarnSpinner.Unity's own DialogueReferenceDrawer,
    // which lists every node in the project regardless of purpose.
    [CustomPropertyDrawer(typeof(ItemExamineHotspots.Hotspot))]
    public sealed class HotspotDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var colliderProp = property.FindPropertyRelative("collider");
            var dialogueProp = property.FindPropertyRelative("dialogue");

            var colliderRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(colliderRect, colliderProp);

            var dialogueRect = new Rect(position.x, colliderRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width, ExamineDialogueField.Height);
            ExamineDialogueField.Draw(dialogueRect, dialogueProp, new GUIContent("Dialogue"));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + ExamineDialogueField.Height;
    }

    // defaultDialogue lives directly on the component (not inside Hotspot), so it needs
    // its own inspector to get the same tag-filtered node picker.
    [CustomEditor(typeof(ItemExamineHotspots))]
    public sealed class ItemExamineHotspotsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();

            EditorGUILayout.PropertyField(this.serializedObject.FindProperty("hotspots"), true);

            EditorGUILayout.Space();
            var defaultDialogueProp = this.serializedObject.FindProperty("defaultDialogue");
            var rect = EditorGUILayout.GetControlRect(true, ExamineDialogueField.Height);
            ExamineDialogueField.Draw(rect, defaultDialogueProp, new GUIContent("Default Dialogue"));

            this.serializedObject.ApplyModifiedProperties();
        }
    }

    // Shared rendering for a DialogueReference field restricted to nodes tagged
    // "examine". Mirrors YarnSpinner.Unity's DialogueReferenceDrawer layout (project
    // field + node dropdown, two lines) but filters the dropdown by node tag.
    internal static class ExamineDialogueField
    {
        private const string RequiredTag = "examine";

        public static float Height => 2 * EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public static void Draw(Rect position, SerializedProperty dialogueProp, GUIContent label)
        {
            position = EditorGUI.PrefixLabel(position, label);

            var projectProp  = dialogueProp.FindPropertyRelative("project");
            var nodeNameProp = dialogueProp.FindPropertyRelative("nodeName");

            var projectRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(projectRect, projectProp, GUIContent.none);

            var nodeRect = new Rect(position.x, projectRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width, EditorGUIUtility.singleLineHeight);
            DrawNodeDropdown(nodeRect, nodeNameProp, projectProp.objectReferenceValue as YarnProject);
        }

        private static void DrawNodeDropdown(Rect rect, SerializedProperty nodeNameProp, YarnProject? project)
        {
            var nodeName = nodeNameProp.stringValue;
            var nodeSet  = !string.IsNullOrEmpty(nodeName);

            if (project == null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.DropdownButton(rect, new GUIContent(nodeSet ? nodeName : "(No Project)"), FocusType.Passive);
                return;
            }

            var taggedNodes = project.Program.Nodes.Values
                .Where(n => !n.Name.StartsWith("$", StringComparison.Ordinal)
                    && n.Tags.Any(t => string.Equals(t, RequiredTag, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .ToList();

            var inTaggedSet = nodeSet && taggedNodes.Any(n => n.Name == nodeName);

            var content = new GUIContent(nodeSet ? nodeName : "(Choose examine node)");
            if (nodeSet && !inTaggedSet)
            {
                content.image = EditorGUIUtility.IconContent(
                    EditorGUIUtility.isProSkin ? "d_console.warnicon.sml" : "console.warnicon.sml").image;
                content.tooltip = $"'{nodeName}' isn't tagged '{RequiredTag}' (or doesn't exist) in this project.";
            }

            if (!EditorGUI.DropdownButton(rect, content, FocusType.Keyboard)) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("<None>"), !nodeSet, () => SetNode(nodeNameProp, string.Empty));

            if (nodeSet && !inTaggedSet)
                menu.AddItem(new GUIContent(nodeName + " (current, untagged/missing)"), true, () => { });

            menu.AddSeparator("");
            foreach (var node in taggedNodes)
            {
                var name = node.Name;
                menu.AddItem(new GUIContent(name), name == nodeName, () => SetNode(nodeNameProp, name));
            }

            menu.DropDown(rect);
        }

        private static void SetNode(SerializedProperty nodeNameProp, string name)
        {
            nodeNameProp.stringValue = name;
            nodeNameProp.serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
