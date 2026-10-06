using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace HorrorEngine
{
    [CustomEditor(typeof(DocumentData), true)]
    public class DocumentDataEditor : Editor
    {
        public Texture NoIconImage;
        private int m_CurrentPageIndex;
        private Editor m_PageEditor;
        private bool m_PageImageFoldout;
        private bool m_PageTextFoldout;
        private float m_PageImageFade;
        private float m_PageTextFade;


        private void OnEnable()
        {
            m_CurrentPageIndex = 0;

            if (!LocalizationSettings.InitializationOperation.IsDone)
            {
                LocalizationSettings.InitializationOperation.WaitForCompletion();
            }

            if (LocalizationSettings.SelectedLocale == null)
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.ProjectLocale;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var doc = target as DocumentData;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIStyle headStyle = new GUIStyle
            {
                fontSize = 24,
                normal = { textColor = Color.white }
            };
            GUILayout.Label(doc.Name, headStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Sprite sprite = doc.Image;
            GUILayout.Box(sprite ? sprite.texture : NoIconImage, GUILayout.Width(128), GUILayout.Height(128));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            //EditorGUILayout.PropertyField(serializedObject.FindProperty("Pages"), true);

            DrawPagesInspector();
            
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            serializedObject.ApplyModifiedProperties();
            
            DrawDefaultInspector();


        }


        private void DrawPagesInspector()
        {
            GUILayout.BeginVertical("Pages", "window");

            SerializedProperty pagesProperty = serializedObject.FindProperty("Pages");
            if (pagesProperty == null || !pagesProperty.isArray)
            {
                GUILayout.Label("Invalid property");
                return;
            }

            // Navigation bar
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("<")) 
            {
                GUI.FocusControl(null);
                m_CurrentPageIndex = Mathf.Max(m_CurrentPageIndex - 1, 0); 
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{m_CurrentPageIndex + 1}/{pagesProperty.arraySize}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(">")) 
            {
                GUI.FocusControl(null);
                m_CurrentPageIndex = Mathf.Min(m_CurrentPageIndex + 1, pagesProperty.arraySize - 1); 
            }
            EditorGUILayout.EndHorizontal();

            if (pagesProperty.arraySize > 0)
            {
                if (pagesProperty != null && pagesProperty.isArray && pagesProperty.arraySize > 0)
                {
                    SerializedProperty pageElement = pagesProperty.GetArrayElementAtIndex(m_CurrentPageIndex);
                    if (pageElement != null)
                    {
                        DrawPageInspector(pageElement);
                    }
                }
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("- Empty document -");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            var doc = target as DocumentData;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New Before"))
            {
                GUI.FocusControl(null);
                Undo.RecordObject(doc, "Insert new page before");
                doc.Pages.Insert(m_CurrentPageIndex, new DocumentPage());
                EditorUtility.SetDirty(doc);
            }

            if (GUILayout.Button("New After"))
            {
                GUI.FocusControl(null);
                Undo.RecordObject(doc, "Insert new page after");
                doc.Pages.Insert(m_CurrentPageIndex+1, new DocumentPage());
                ++m_CurrentPageIndex;
                EditorUtility.SetDirty(doc);
            }

            if (GUILayout.Button("Delete") && pagesProperty.arraySize > 0)
            {
                GUI.FocusControl(null);
                pagesProperty.DeleteArrayElementAtIndex(m_CurrentPageIndex);
                m_CurrentPageIndex = Mathf.Clamp(m_CurrentPageIndex, 0, pagesProperty.arraySize - 1);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawPageInspector(SerializedProperty pageElement)
        {
            var doc = target as DocumentData;
            var page = doc.Pages[m_CurrentPageIndex];

            GUIStyle pageBoxStyle = new GUIStyle(GUI.skin.box);
            pageBoxStyle.padding = new RectOffset(20, 20, 10, 10);

            EditorGUILayout.BeginVertical(pageBoxStyle);

            // Page text

            GUILayout.BeginVertical("Text", "window");
            // Text preview
            if (!string.IsNullOrEmpty(page.Text))
            {
                // Create a custom GUIStyle
                GUIStyle customLabelStyle = new GUIStyle(GUI.skin.label);
                customLabelStyle.richText = true;
                customLabelStyle.normal.background = Texture2D.whiteTexture;
                customLabelStyle.normal.textColor = Color.black; // Set text color to black
                customLabelStyle.wordWrap = true;
                customLabelStyle.alignment = TextAnchor.MiddleCenter; // Center alignment
                customLabelStyle.border = new RectOffset(1, 1, 1, 1);
                customLabelStyle.fontSize = 14; // Optional: Adjust font size
                customLabelStyle.padding = new RectOffset(10, 10, 5, 5); // Optional: Add padding

                GUILayout.Space(10);
                GUILayout.BeginVertical();
                GUILayout.Label(page.Text, customLabelStyle);
                GUILayout.EndVertical();
                GUILayout.Space(10);
            }
            
            EditorGUILayout.PropertyField(pageElement.FindPropertyRelative(nameof(DocumentPage.Text)));

            GUILayout.EndVertical();


            // Image data
            GUILayout.BeginVertical("Image", "window");

            // Image preview
            var image = page.Image;
            if (image)
            {
                GUILayout.BeginVertical("box");
                var texture = ((Sprite)image).texture;

                float inspectorWidth = EditorGUIUtility.currentViewWidth - 80; // Adjust for padding/margin

                // Calculate texture dimensions while maintaining aspect ratio
                float aspectRatio = (float)texture.width / texture.height;
                float textureWidth = Mathf.Min(inspectorWidth, texture.width); // Fit within inspector width
                float textureHeight = textureWidth / aspectRatio;

                // Center the texture by calculating the horizontal offset
                float horizontalOffset = (inspectorWidth - textureWidth) / 2.0f;

                // Create a rect for the texture
                Rect rect = GUILayoutUtility.GetRect(inspectorWidth, textureHeight, GUILayout.ExpandWidth(false));
                rect.x += horizontalOffset; // Adjust rect's x position to center the texture

                // Draw the texture
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true, 0, page.ChangeImageColor ? page.ImageColor : Color.white, 0, 0);

                // Page caption
                if (!string.IsNullOrEmpty(page.Caption))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(page.Caption, "box");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }


            EditorGUILayout.PropertyField(pageElement.FindPropertyRelative(nameof(DocumentPage.Caption)));
            EditorGUILayout.PropertyField(pageElement.FindPropertyRelative(nameof(DocumentPage.Image)));

            if (image)
            {
                EditorGUILayout.PropertyField(pageElement.FindPropertyRelative(nameof(DocumentPage.ChangeImageColor)));
                if (page.ChangeImageColor)
                    EditorGUILayout.PropertyField(pageElement.FindPropertyRelative(nameof(DocumentPage.ImageColor)));
            }

            GUILayout.EndVertical();


            EditorGUILayout.EndVertical();
        }

    }
}