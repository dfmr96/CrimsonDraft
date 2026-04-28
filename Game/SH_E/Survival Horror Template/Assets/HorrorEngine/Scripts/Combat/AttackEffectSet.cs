using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Combat/Effects/Effect Set")]
    public class AttackEffectSet : AttackEffect
    {
        public AttackEffect[] Effects;

        public override void Apply(AttackInfo info)
        {
            base.Apply(info);

            foreach (var effect in Effects)
            {
                effect.Apply(info);
            }
        }
    }
}