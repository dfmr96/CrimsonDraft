using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

namespace HorrorEngine 
{

    public class CameraPOV : MonoBehaviour
    {
        public int Priority;

#if UNITY_6000_0_OR_NEWER
        private CinemachineCamera m_VirtualCam;
#else
        private CinemachineVirtualCamera m_VirtualCam;
#endif
        private int m_TriggerEnterCount;
        private Dictionary<OnDisableNotifier, Collider> m_TargetTracked = new Dictionary<OnDisableNotifier, Collider>();
        private Action<OnDisableNotifier> m_TargetDisabledCallback;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_TargetDisabledCallback = OnTargetDisabled;

#if UNITY_6000_0_OR_NEWER
            m_VirtualCam = GetComponentInChildren<CinemachineCamera>();
#else
            m_VirtualCam = GetComponentInChildren<CinemachineVirtualCamera>();
#endif

            var disableNotifier = GetComponentInChildren<OnDisableNotifier>();
            if (disableNotifier) 
            {
                disableNotifier.AddCallback((notif) => 
                {
                    m_TriggerEnterCount = 0;
                    CameraStack.Instance.RemoveCamera(this);
                });
            }

            Deactivate();
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            foreach(var entry in m_TargetTracked)
            {
                var disableNotifier = entry.Key;
                disableNotifier?.RemoveCallback(m_TargetDisabledCallback);
            }
            m_TargetTracked.Clear();
        }

        // --------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (!other.GetComponent<CameraPOVTarget>())
                return;

            if (!m_TargetTracked.ContainsValue(other)) 
            {
                var notifier = other.GetComponentInParent<OnDisableNotifier>();
                notifier.AddCallback(m_TargetDisabledCallback);
                m_TargetTracked.Add(notifier, other);
            }

            ++m_TriggerEnterCount;
            CameraStack.Instance.AddCamera(this, Priority);
        }

        // --------------------------------------------------------------------

        private void OnTriggerExit(Collider other)
        {
            if (!other.GetComponent<CameraPOVTarget>())
                return;

            if (m_TargetTracked.ContainsValue(other))
            {
                var notifier = other.GetComponentInParent<OnDisableNotifier>();
                RemoveCallback(notifier, other);
            }

            --m_TriggerEnterCount;
            Debug.Assert(m_TriggerEnterCount >= 0, $"Trigger count went negative in CameraPOV {name}");
            if (m_TriggerEnterCount == 0)
                CameraStack.Instance.RemoveCamera(this);
        }

        // --------------------------------------------------------------------

        private void OnTargetDisabled(OnDisableNotifier notifier)
        {
            var collider = m_TargetTracked[notifier];
            RemoveCallback(notifier, collider);
            OnTriggerExit(collider);
        }

        // --------------------------------------------------------------------

        private void RemoveCallback(OnDisableNotifier notifier, Collider collider)
        {
            notifier.RemoveCallback(m_TargetDisabledCallback);
            m_TargetTracked.Remove(notifier);
        }

        // --------------------------------------------------------------------

        public void Activate()
        {
            m_VirtualCam.gameObject.SetActive(true);
        }

        // --------------------------------------------------------------------

        public void Deactivate()
        {
            m_VirtualCam.gameObject.SetActive(false);
        }
    }
}