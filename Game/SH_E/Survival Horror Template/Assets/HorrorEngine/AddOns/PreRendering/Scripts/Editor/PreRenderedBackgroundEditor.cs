using UnityEngine;
using UnityEditor;

#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif


namespace PreRenderBackgrounds
{
    [CustomEditor(typeof(PreRenderedBackground))]
    public class PreRenderedBackgroundEditor : Editor
    {
        [SerializeField] private Material m_RenderBgMaterial;
        [SerializeField] private Shader m_Shader;
        
        private float m_Size = 250;

        // --------------------------------------------------------------------

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            PreRenderedBackground prBg = target as PreRenderedBackground;

#if UNITY_6000_0_OR_NEWER
            CinemachineCamera virtualCam = prBg.GetComponentInParent<CinemachineCamera>(true);
#else
            CinemachineVirtualCamera virtualCam = prBg.GetComponentInParent<CinemachineVirtualCamera>(true);
#endif

            if (!virtualCam)
            {
                EditorGUILayout.HelpBox("Parent virtual camera not found", MessageType.Error);
                return;
            }

            EditorGUILayout.Separator();
      
            if (prBg.ColorTexture || prBg.FinalRenderTexture)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Box("", GUILayout.Height(m_Size));
                EditorGUILayout.EndHorizontal();
                Rect scale = GUILayoutUtility.GetLastRect();

                if (prBg.ColorTexture)
                    EditorGUI.DrawTextureTransparent(scale, prBg.ColorTexture, ScaleMode.ScaleToFit);
                else 
                    EditorGUI.DrawTextureTransparent(scale, prBg.FinalRenderTexture, ScaleMode.ScaleToFit);
            }

        }


    }
}
