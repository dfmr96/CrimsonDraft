using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Combat/Effects/Attach Status Effect")]
    public class AttackEffectAttachStatusEffect : AttackEffect
    {
        [SerializeField] private StatusEffect[] m_StatusEffects;
        
        public override void Apply(AttackInfo info)
        {
            base.Apply(info);

            var statusEffectHandler = info.Damageable.GetComponentInParent<StatusEffectHandler>();
            Debug.Assert(statusEffectHandler!=null, $"AttackEffectAttachStatusEffect couldn't find StatusEffectHandler on target {info.Damageable}", this);     

            if (statusEffectHandler != null)
            {
                foreach (var statusEffect in m_StatusEffects)
                {
                    if (statusEffect != null)
                    {
                        statusEffectHandler.AddStatusEffect(statusEffect);
                    }
                }
            }
        }
    }
}
