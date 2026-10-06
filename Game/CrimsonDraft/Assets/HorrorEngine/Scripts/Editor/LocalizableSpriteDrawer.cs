using UnityEditor;
using UnityEngine;

namespace HorrorEngine
{
    public static class LocalizableEditorUtils
    {
        private static Texture2D m_FlagIcon;
        private static Texture2D m_DeleteIcon;

        public static Texture2D GetLocalizeIcon()
        {
            if (m_FlagIcon == null)
            {
                m_FlagIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HorrorEngine/Scripts/Editor/Resources/FlagIcon.png");
            }
            return m_FlagIcon;
        }

        public static Texture2D GetUnlocalizeIcon()
        {
            if (m_DeleteIcon == null)
            {
                m_DeleteIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HorrorEngine/Scripts/Editor/Resources/DeleteIcon.png");
            }
            return m_DeleteIcon;
        }
    }

    [CustomPropertyDrawer(typeof(LocalizableSprite))]
    public class LocalizableSpriteDrawer : PropertyDrawer
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

                EditorGUI.PropertyField(textFieldRect, unlocalizedProperty, label);

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
                return base.GetPropertyHeight(isLocalizedProp, label);
            }
            else
            {
                var localizedProperty = property.FindPropertyRelative(nameof(LocalizableText.Localized));
                return EditorGUI.GetPropertyHeight(localizedProperty, true);
            }
            
        }

    }

}