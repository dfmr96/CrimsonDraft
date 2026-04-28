using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

namespace PreRenderBackgrounds
{
    public class MimicCamera : MonoBehaviour
    {
        public Camera Camera;
        public bool DisableCamera = true;
        public bool MimicOnUpdate = true;

#if UNITY_6000_0_OR_NEWER
        private CinemachineCamera m_VirtualCam;
#else
        private CinemachineVirtualCamera m_VirtualCam;
#endif

        private void OnValidate()
        {
            if (Camera)
                Mimic(Camera);
        }
        private void Awake()
        {
            if (Camera)
                Mimic(Camera);
        }

        private void OnEnable()
        {
            if (Camera)
                Mimic(Camera);
        }

        private void Update()
        {
            if (MimicOnUpdate)
                Mimic(Camera);
        }



        [ContextMenu("Mimic Camera")]
        private void Mimic()
        {
            if (Camera)
                Mimic(Camera);
        }

        private void Mimic(Camera cam)
        {
            if (!m_VirtualCam)
            {
#if UNITY_6000_0_OR_NEWER
                m_VirtualCam = GetComponentInChildren<CinemachineCamera>(true);
#else
                m_VirtualCam = GetComponentInChildren<CinemachineVirtualCamera>(true);
#endif
            }

            if (m_VirtualCam)
            {
                m_VirtualCam.transform.position = cam.transform.position;
                m_VirtualCam.transform.rotation = cam.transform.rotation;

                LensSettings lens = LensSettings.FromCamera(cam);
                
                if (cam.usePhysicalProperties)
                    lens.ModeOverride = LensSettings.OverrideModes.Physical;

#if UNITY_6000_0_OR_NEWER
                m_VirtualCam.Lens = lens;
#else
                m_VirtualCam.m_Lens = lens;
#endif
            }

            if (DisableCamera)
                cam.gameObject.SetActive(false);
        }
    }
}
