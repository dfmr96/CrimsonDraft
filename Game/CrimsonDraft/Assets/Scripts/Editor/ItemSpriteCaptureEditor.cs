#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Editor
{
    [CustomEditor(typeof(ItemSpriteCapture))]
    public sealed class ItemSpriteCaptureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (GUILayout.Button("Capture Sprite", GUILayout.Height(32)))
                ((ItemSpriteCapture)target).CaptureSprite();
        }
    }
}
#endif
