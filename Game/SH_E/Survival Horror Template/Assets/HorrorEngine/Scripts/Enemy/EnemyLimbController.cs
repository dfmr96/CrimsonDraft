using UnityEngine;

namespace HorrorEngine
{
    public class EnemyLimbController : MonoBehaviour
    {
        private SocketController m_SocketCtrl;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_SocketCtrl = GetComponent<SocketController>();
        }

        // --------------------------------------------------------------------

        public void RemoveLimb(SocketHandle socket)
        {
            var socketObj = m_SocketCtrl.GetSocket(socket);
            if (socketObj)
            {
                socketObj.transform.localScale = Vector3.zero;
            }
        }
    }
}