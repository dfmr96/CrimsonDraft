using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{

    [System.Serializable]
    public class DamageableAnimEntry
    {
        public Damageable Damageable;
        public AnimatorStateHandle AnimationState;
    }

    public class EnemyStateDeath : ActorState
    {
        [SerializeField] private List<DamageableAnimEntry> m_DamageableSpecificAnimation;

        private Health m_Health;

        protected override void Awake()
        {
            base.Awake();

            m_Health = GetComponentInParent<Health>();
        }

        public override void StateEnter(IActorState fromState)
        {
            Damageable lastDamageable = m_Health.LastDamageableHit;
            if (lastDamageable) 
            {
                foreach (var entry in m_DamageableSpecificAnimation)
                {
                    if (entry.Damageable == lastDamageable)
                    {
                        m_AnimationState = entry.AnimationState;
                    }
                }
            }

            base.StateEnter(fromState);
        }
    }
}