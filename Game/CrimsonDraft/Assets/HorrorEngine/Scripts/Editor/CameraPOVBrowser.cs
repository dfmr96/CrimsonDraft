using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

namespace HorrorEngine
{
    public class CameraPOVBrowser : EditorWindow
    {
        public Texture WindowIcon;
        public Texture ViewIcon;

        private Vector2 m_Scroll;
        private List<Transform> m_Cameras;
        private string m_Filter = "";

        private int m_ToolbarSelected = 0;
        string[] m_ToolbarStrings = { "Fixed", "Closeup" };
        
        private static Texture2D m_RefreshIconOpen;


        // --------------------------------------------------------------------

        [MenuItem("Horror Engine/Camera POV Browser")]
        public static void ShowWindow()
        {
            
            CameraPOVBrowser browser = GetWindow<CameraPOVBrowser>();
            browser.titleContent = new GUIContent("Camera Browser", browser.WindowIcon);
        }

        // --------------------------------------------------------------------

        private void OnFocus()
        {
            RefreshList();
        }

        // --------------------------------------------------------------------

        private void RefreshList()
        {
            if (m_Cameras == null)
                m_Cameras = new List<Transform>();

            if (m_ToolbarSelected == 0) // Fixed cams
            {
                CameraPOV[] allCams = FindObjectsByType<CameraPOV>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                    );

                var orderedCams = allCams.OrderBy(c =>
                {
                    return c.name;
                });

                m_Cameras.Clear();
                foreach (var cam in orderedCams)
                {
                    m_Cameras.Add(cam.transform);
                }
            }
            else // Closeup cams
            {
                CameraCloseup[] allCams = FindObjectsByType<CameraCloseup>(
                   FindObjectsInactive.Include,
                   FindObjectsSortMode.None
                   );

                var orderedCams = allCams.OrderBy(c =>
                {
                    return c.name;
                });

                m_Cameras.Clear();
                foreach (var cam in orderedCams)
                {
                    m_Cameras.Add(cam.transform);
                }
            }
        }

        // --------------------------------------------------------------------

        private void OnGUI()
        {
            if (!m_RefreshIconOpen)
                m_RefreshIconOpen = EditorGUIUtility.IconContent("TreeEditor.Refresh").image as Texture2D;

            if (m_Cameras == null)
                RefreshList();

            GUILayout.BeginHorizontal();

            int prevtoolbar = m_ToolbarSelected;
            m_ToolbarSelected = GUILayout.Toolbar(m_ToolbarSelected, m_ToolbarStrings);
            if (prevtoolbar != m_ToolbarSelected)
                RefreshList();

            if (GUILayout.Button(m_RefreshIconOpen, GUILayout.Width(24)))
                RefreshList();

            GUILayout.EndHorizontal();

            GUILayout.Label("Filter by name or tag", EditorStyles.boldLabel);
            m_Filter = EditorGUILayout.TextField(m_Filter);

            GUILayout.Space(10);
            m_Scroll = GUILayout.BeginScrollView(m_Scroll);

            foreach (var cam in m_Cameras)
            {
                if (cam == null) continue;

                string label = cam.name;

                if (!string.IsNullOrEmpty(m_Filter))
                {
                    string tag = cam.tag;
                    if (!label.ToLower().Contains(m_Filter.ToLower()) &&
                        !tag.ToLower().Contains(m_Filter.ToLower()))
                        continue;
                }

                Transform targetCam = null;

#if UNITY_6000_0_OR_NEWER
                CinemachineCamera targetCCam = cam.GetComponentInChildren<CinemachineCamera>(true);
                if (targetCCam)
                    targetCam = targetCCam.transform;
#else
                CinemachineVirtualCamera targetCCam = cam.GetComponentInChildren<CinemachineVirtualCamera>(true);
                if (targetCCam)
                    targetCam = targetCCam.transform;
#endif
                if (targetCam)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(ViewIcon, GUILayout.Height(30), GUILayout.Width(30)))
                    {

                        Selection.activeTransform = targetCam;
                        SceneView.lastActiveSceneView.AlignViewToObject(targetCam);
                        SceneView.lastActiveSceneView.Repaint();

                    }
                
                    GUILayout.Label(label);
                    EditorGUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
        }
    }
}