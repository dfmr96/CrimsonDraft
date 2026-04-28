using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif

namespace HorrorEngine
{
    public class Socket : MonoBehaviour
    {
        public SocketHandle Handle;

        [SerializeField] private float m_GizmoSize = 0.25f;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Handle != null)
                Handles.Label(transform.position + Vector3.one * 0.01f, Handle.name);
            GizmoUtils.DrawCross(transform.position, transform.right, transform.up, transform.forward, m_GizmoSize);
        }
#endif
    }
}
