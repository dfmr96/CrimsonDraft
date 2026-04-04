using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Combat/Filters/Grabber")]
    public class AttackFilterGrabber : AttackFilter
    {
        public override bool Passes(AttackInfo info)
        {
            var grabber = info.Damageable.GetComponentInParent<Grabber>();
            var grabHandler = info.Attack.GetComponentInParent<PlayerGrabHandler>();
            if (!grabHandler)
                return false;

            return grabber == grabHandler.Grabber;
        }
    }
}