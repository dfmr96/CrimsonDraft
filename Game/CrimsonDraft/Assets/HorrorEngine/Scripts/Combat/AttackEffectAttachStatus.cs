using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Combat/Effects/Attach Status")]
    public class AttackEffectAttachStatus : AttackEffect
    {
        [SerializeField] private StatusData m_Status;

        public override void Apply(AttackInfo info)
        {
            base.Apply(info);

            var status = info.Damageable.GetComponentInParent<Status>();

            if (status != null && m_Status != null)
            {
                status.AddStatus(m_Status);
            }
            
        }
    }
}
