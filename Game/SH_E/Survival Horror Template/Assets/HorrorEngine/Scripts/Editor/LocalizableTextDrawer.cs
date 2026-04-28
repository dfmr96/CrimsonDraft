using UnityEditor;
using UnityEngine;

namespace HorrorEngine
{
    [CustomPropertyDrawer(typeof(LocalizableText))]
    public class LocalizableTextDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Begin the property field
            EditorGUI.BeginProperty(position, label, property);

            
            float buttonW = 20;
            Rect buttonRect = new Rect(position.x + position.width - buttonW, position.y, 20, 20);

            var isLocalizedProp = property.FindPropertyRelative(nameof(LocalizableText.IsLocalized));
            if (!isLocalizedProp.boolValue)
            {
                Rect textFieldRect = new Rect(position.x, position.y, position.width - buttonW - 5, EditorGUIUtility.singleLineHeight);
                var unlocalizedProperty = property.FindPropertyRelative(nameof(LocalizableText.Unlocalized));

                // Detect if the [LocalizableTextAreaAttribute] attribute is applied to the field
                LocalizableTextAreaAttribute textAreaAttribute = (LocalizableTextAreaAttribute)System.Attribute.GetCustomAttribute(fieldInfo, typeof(LocalizableTextAreaAttribute));
                if (textAreaAttribute != null)
                {
                    EditorGUI.LabelField(textFieldRect, label);
                    position.y += EditorGUIUtility.singleLineHeight+5; // Move to the next line
                    position.height -= EditorGUIUtility.singleLineHeight+10; // Adjust height
                    unlocalizedProperty.stringValue = EditorGUI.TextArea(position, unlocalizedProperty.stringValue, EditorStyles.textArea);
                }
                else
                {
                    unlocalizedProperty.stringValue = EditorGUI.TextField(textFieldRect, label, unlocalizedProperty.stringValue);
                }

                if (GUI.Button(buttonRect, LocalizableEditorUtils.GetLocalizeIcon()))
                {
                    isLocalizedProp.boolValue = true;
                }
            }
            else
            {
                Rect textFieldRect = new Rect(position.x, position.y, position.width - buttonW - 5, position.height);
                var localizedProperty = property.FindPropertyRelative(nameof(LocalizableText.Localized));
                EditorGUI.PropertyField(textFieldRect, localizedProperty, label);

                if (GUI.Button(buttonRect, LocalizableEditorUtils.GetUnlocalizeIcon()))
                {
                    isLocalizedProp.boolValue = false;
                }
            }

            // End the property field
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var isLocalizedProp = property.FindPropertyRelative(nameof(LocalizableText.IsLocalized));
            if (!isLocalizedProp.boolValue)
            {
                var textAreaAttribute = (LocalizableTextAreaAttribute)System.Attribute.GetCustomAttribute(fieldInfo, typeof(LocalizableTextAreaAttribute));
                if (textAreaAttribute != null)
                {
                    // Adjust height based on TextArea attribute's min height
                    return EditorGUIUtility.singleLineHeight * (textAreaAttribute.Size + 1);
                }
                else
                {
                    return base.GetPropertyHeight(property, label);
                }
            }
            else
            {
                var localizedProperty = property.FindPropertyRelative(nameof(LocalizableText.Localized));
                return EditorGUI.GetPropertyHeight(localizedProperty, true);
            }
            
        }

    }

}