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
        private const float Spacing = 4f; // extra breathing room between this Hotspot's fields and the next one in the array

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var colliderProp       = property.FindPropertyRelative("collider");
            var dialogueProp       = property.FindPropertyRelative("dialogue");
            var requiredItemProp   = property.FindPropertyRelative("requiredItem");
            var promptDialogueProp = property.FindPropertyRelative("promptDialogue");
            var onUsedProp         = property.FindPropertyRelative("onUsed");
            var activationTransformProp = property.FindPropertyRelative("activationTransform");
            var rewardItemProp          = property.FindPropertyRelative("rewardItem");
            var rewardDialogueProp      = property.FindPropertyRelative("rewardDialogue");
            var rewardAnimationClipProp = property.FindPropertyRelative("rewardAnimationClip");

            float y = position.y;
            float lineH = EditorGUIUtility.singleLineHeight;
            float vSpace = EditorGUIUtility.standardVerticalSpacing;

            var colliderRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.PropertyField(colliderRect, colliderProp);
            y = colliderRect.yMax + vSpace;

            var dialogueRect = new Rect(position.x, y, position.width, ExamineDialogueField.Height);
            ExamineDialogueField.Draw(dialogueRect, dialogueProp, new GUIContent("Dialogue"));
            y = dialogueRect.yMax + vSpace * 2;

            var requiredItemRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.PropertyField(requiredItemRect, requiredItemProp,
                new GUIContent("Required Item", "Optional. If set and present in the player's inventory, examining this hotspot runs Prompt Dialogue (a Yes/No Yarn node) instead of Dialogue above."));
            y = requiredItemRect.yMax + vSpace;

            var promptDialogueRect = new Rect(position.x, y, position.width, ExamineDialogueField.Height);
            ExamineDialogueField.Draw(promptDialogueRect, promptDialogueProp, new GUIContent("Prompt Dialogue"));
            y = promptDialogueRect.yMax + vSpace;

            var activationTransformRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.PropertyField(activationTransformRect, activationTransformProp,
                new GUIContent("Activation Transform", "Optional. A child of this model's root -- before On Used fires, the preview rotates to match its localRotation, so the reveal always plays from the same angle regardless of how the player had the model rotated."));
            y = activationTransformRect.yMax + vSpace;

            float onUsedHeight = EditorGUI.GetPropertyHeight(onUsedProp, true);
            var onUsedRect = new Rect(position.x, y, position.width, onUsedHeight);
            EditorGUI.PropertyField(onUsedRect, onUsedProp,
                new GUIContent("On Used", "Fires after Required Item is consumed via the \"Sí\" branch of Prompt Dialogue's use_required_item command."), true);
            y = onUsedRect.yMax + vSpace * 2;

            var rewardItemRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.PropertyField(rewardItemRect, rewardItemProp,
                new GUIContent("Reward Item", "Optional. Granted after Reward Animation Clip finishes playing (if set) -- the item being inspected is consumed first to free up space, then this is added."));
            y = rewardItemRect.yMax + vSpace;

            var rewardDialogueRect = new Rect(position.x, y, position.width, ExamineDialogueField.Height);
            ExamineDialogueField.Draw(rewardDialogueRect, rewardDialogueProp, new GUIContent("Reward Dialogue"));
            y = rewardDialogueRect.yMax + vSpace;

            var rewardAnimationClipRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.PropertyField(rewardAnimationClipRect, rewardAnimationClipProp,
                new GUIContent("Reward Animation Clip", "Optional. The clip On Used's Animator plays -- its length is how long InspectPanel waits before granting Reward Item, so it doesn't appear mid-animation."));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var onUsedProp = property.FindPropertyRelative("onUsed");
            float lineH = EditorGUIUtility.singleLineHeight;
            float vSpace = EditorGUIUtility.standardVerticalSpacing;

            return lineH + vSpace                                  // collider
                 + ExamineDialogueField.Height + vSpace * 2          // dialogue
                 + lineH + vSpace                                   // requiredItem
                 + ExamineDialogueField.Height + vSpace              // promptDialogue
                 + lineH + vSpace                                   // activationTransform
                 + EditorGUI.GetPropertyHeight(onUsedProp, true) + vSpace * 2 // onUsed
                 + lineH + vSpace                                   // rewardItem
                 + ExamineDialogueField.Height + vSpace              // rewardDialogue
                 + lineH                                            // rewardAnimationClip
                 + Spacing;
        }
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
