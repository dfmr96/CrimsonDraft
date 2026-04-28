using UnityEngine;
using System.Collections.Generic;

namespace HorrorEngine
{

    [CreateAssetMenu(fileName = "StatusEffectAttachParticleSystemToSocket", menuName = "Horror Engine/Status/Effects/AttachParticleSystemToSocket")]
    public class StatusEffectAttachParticleSystemToSocket : StatusEffect
    {
        [SerializeField] private GameObject m_ParticlePrefab;
        [SerializeField] private SocketAttachment[] m_SocketAttachments;
        [SerializeField] private float m_StopDelay = 0;
        [SerializeField] private float m_DestroyDelay = 0;

        private List<PooledGameObject> m_AttachedParticles = new List<PooledGameObject>();
        private System.Action m_StopAllAction;
        private System.Action m_DestroyAllAction;

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            m_StopAllAction = StopAll;
            m_DestroyAllAction = DestroyAll;
        }

        // --------------------------------------------------------------------

        public override void StartEffect(StatusEffectHandler handler)
        {
            base.StartEffect(handler);

            Debug.Assert(m_StopDelay < m_DestroyDelay, "StopDelay can't be shorter than DestroyDelay. Make the delay shorter or set StopDelay to 0", this);

            SocketController socketController = handler.gameObject.GetComponent<SocketController>();
            if (socketController == null)
            {
                Debug.LogWarning("No SocketController found on target.");
                return;
            }

            foreach (var attachment in m_SocketAttachments)
            {
                var socket = socketController.GetSocket(attachment.Socket);
                if (socket != null)
                {
                    var particleInstance = GameObjectPool.Instance.GetFromPool(m_ParticlePrefab, null);
                    socketController.Attach(particleInstance.gameObject, attachment);
                    particleInstance.gameObject.SetActive(true);

                    m_AttachedParticles.Add(particleInstance);
                }
            }
        }

        // --------------------------------------------------------------------

        public override void EndEffect()
        {
            base.EndEffect();

            if (m_DestroyDelay > 0)
            {
                m_Handler.InvokeAction(m_DestroyAllAction, m_DestroyDelay);

                if (m_StopDelay > 0)
                {
                        m_Handler.InvokeAction(m_StopAllAction, m_StopDelay);
                }
            }
            else
            {
                DestroyAll();
            }
        }

        // --------------------------------------------------------------------

        private void DestroyAll()
        {   
            foreach (var particle in m_AttachedParticles)
            {
                if (particle != null)
                {
                    GameObjectPool.Instance.ReturnToPool(particle);
                }
            }

            m_AttachedParticles.Clear();
        }

        // -------------------------------------------------------------------- 

        private void StopAll()
        {
            foreach (var particle in m_AttachedParticles)
            {
                if (particle != null)
                {
                    particle.GetComponent<ParticleSystem>().Stop();
                }
            }
        }
    }
}
