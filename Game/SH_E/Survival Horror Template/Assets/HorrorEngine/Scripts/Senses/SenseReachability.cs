using UnityEngine;
using UnityEngine.AI;

namespace HorrorEngine
{
    public class SenseReachability : Sense
    {
        [SerializeField] SenseTarget m_Target;

        private NavMeshAgent m_MeshAgent;
        private NavMeshPath m_NavMeshPath;
        private bool m_InReach;

        public override void Init(SenseController controller)
        {
            base.Init(controller);
            m_NavMeshPath = new NavMeshPath();
            m_MeshAgent = controller.GetComponentInParent<NavMeshAgent>();
        }

        public override bool SuccessfullySensed()
        {
            return m_InReach;
        }

        public override void Tick()
        {
            var target = m_Target.GetTransform();
            bool wasInReach = m_InReach;

            m_InReach = false;
            if (target && m_MeshAgent && m_MeshAgent.isActiveAndEnabled && m_MeshAgent.isOnNavMesh)
            {
                if (m_MeshAgent.CalculatePath(target.position, m_NavMeshPath) && m_NavMeshPath.status == NavMeshPathStatus.PathComplete)
                {
                    m_InReach = true;
                }
            }

            if (wasInReach != m_InReach)
                OnChanged?.Invoke(this, target);
        }

    }
}