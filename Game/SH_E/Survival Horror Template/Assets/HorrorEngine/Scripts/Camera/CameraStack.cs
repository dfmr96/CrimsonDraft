using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class CameraStack : SingletonBehaviour<CameraStack>
    {
        [SerializeField] bool m_ShowDebug;
        
        private CameraPOV ActiveCamera
        {
            get
            {
                return m_CameraStack.Count > 0 ? m_CameraStack[m_CameraStack.Count - 1] : null;
            }
        }

        private List<CameraPOV> m_CameraStack = new List<CameraPOV>();

        // --------------------------------------------------------------------

        public List<CameraPOV> GetCameras()
        {
            return m_CameraStack;
        }

        // --------------------------------------------------------------------

        public void AddCamera(CameraPOV cam, int priority = 0)
        {
            CameraPOV prevCam = ActiveCamera;
            if (!m_CameraStack.Contains(cam))
            {
                if (!ActiveCamera || ActiveCamera.Priority <= priority)
                {
                    m_CameraStack.Add(cam);
                }
                else
                {
                    // Insert into the list based on priority
                    int i;
                    for (i = m_CameraStack.Count-1; i >= 0; --i)
                    {
                        if (m_CameraStack[i].Priority <= priority)
                        {
                            m_CameraStack.Insert(i+1, cam);
                            break;
                        }
                    }
                    if (i < 0)
                        m_CameraStack.Insert(0, cam);
                }
            }

            if (ActiveCamera != prevCam)
            {
                ActiveCamera.Activate();
                if (prevCam)
                    prevCam.Deactivate();

            }
        }

        // --------------------------------------------------------------------

        public void RemoveCamera(CameraPOV cam)
        {
            CameraPOV prevCam = ActiveCamera;

            m_CameraStack.Remove(cam);
            cam.Deactivate();

            if (ActiveCamera != prevCam && ActiveCamera != null)
            {
                ActiveCamera.Activate();
            }
        }

        // --------------------------------------------------------------------

        public void ClearAllCameras()
        {
            m_CameraStack.Clear();
        }

#if UNITY_EDITOR
        void OnGUI()
        {
            if (m_ShowDebug)
            {
                foreach(var cam in m_CameraStack)
                {
                    GUILayout.Label(cam.name + ", " + cam.Priority +" "+(cam.IsPOVActive ? "(Active)" : ""));
                }
            }
        }
#endif
    }

}