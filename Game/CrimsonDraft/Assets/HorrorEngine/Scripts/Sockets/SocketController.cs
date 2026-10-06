using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    [Serializable]
    public class SocketAttachment
    {
        [FormerlySerializedAs("Handle")]
        public SocketHandle Socket;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale = Vector3.one;
        public bool ApplyScale = true;
    }

    public class SocketController : MonoBehaviour
    {
        private Socket[] m_Sockets;
        private Dictionary<SocketHandle, Socket> m_HashedSockets = new Dictionary<SocketHandle, Socket>();

        // --------------------------------------------------------------------

        private void Awake()
        {
            Init();
        }

        // --------------------------------------------------------------------

        public Socket GetSocket(SocketHandle handle)
        {
            if (m_Sockets == null || m_HashedSockets.Count == 0)
                Init();

            if (!m_HashedSockets.ContainsKey(handle))
            {
                Debug.LogWarning("SocketController doesn't contain socket with handle : " + handle.name);
                return null;
            }

            return m_HashedSockets[handle];
        }

        // --------------------------------------------------------------------

        public void Attach(GameObject go, SocketAttachment attachment)
        {
            var socket = GetSocket(attachment.Socket);
            go.transform.SetParent(socket.transform);
            go.transform.SetLocalPositionAndRotation(attachment.Position, Quaternion.Euler(attachment.Rotation));
            
            if (attachment.ApplyScale)
                go.transform.localScale = attachment.Scale;
        }

        // --------------------------------------------------------------------

        public void Init()
        {
            m_HashedSockets.Clear();

            m_Sockets = GetComponentsInChildren<Socket>();
            foreach (var socket in m_Sockets)
            {
                Debug.Assert(!m_HashedSockets.ContainsKey(socket.Handle), "Duplicated socket handle");
                m_HashedSockets.Add(socket.Handle, socket);
            }
        }
    }
}